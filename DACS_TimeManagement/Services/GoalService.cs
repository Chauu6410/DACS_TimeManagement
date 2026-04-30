using DACS_TimeManagement.DTOs;
using DACS_TimeManagement.Hubs;
using DACS_TimeManagement.Models;
using DACS_TimeManagement.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore.ChangeTracking;

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using System.Net.Http;

namespace DACS_TimeManagement.Services
{
    // Business logic for goals. Keeps operations testable and focused.
    public class GoalService : IGoalService
    {
        private readonly ApplicationDbContext _db;
        private readonly IHubContext<GoalHub> _hubContext;
        private readonly IMemoryCache _cache;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;

        public GoalService(ApplicationDbContext db, IHubContext<GoalHub> hubContext, IMemoryCache cache, IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _db = db;
            _hubContext = hubContext;
            _cache = cache;
            _httpClientFactory = httpClientFactory;
            _config = config;
        }

        public async Task<GoalDetailDto> CreateAsync(CreateGoalDto dto, string userId)
        {
            // Create a minimal goal linked to a project
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == dto.ProjectId);
            var goal = new PersonalGoal
            {
                Title = (project != null ? project.Name + " - Goal" : "Project Goal"),
                Description = dto.Description,
                ProjectId = dto.ProjectId,
                Type = dto.ProjectId > 0 ? GoalType.TaskBased : GoalType.TimeBased,
                StartDate = DateTime.UtcNow,
                TargetDate = dto.TargetDate,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.PersonalGoals.Add(goal);
            await _db.SaveChangesAsync();

            return MapToDto(goal);
        }
        public async Task<GoalDetailDto?> UpdateAsync(UpdateGoalDto dto, string userId)
        {
            var goal = await _db.PersonalGoals.FirstOrDefaultAsync(g => g.Id == dto.Id && g.UserId == userId);
            if (goal == null) return null;

            // Minimal update: description and target date
            goal.Description = dto.Description;
            goal.TargetDate = dto.TargetDate;
            goal.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return MapToDto(goal);
        }

        public async Task<bool> LinkTasksAsync(int goalId, List<int> taskIds, string userId)
        {
            var goal = await _db.PersonalGoals.Include(g => g.GoalTasks).FirstOrDefaultAsync(g => g.Id == goalId && g.UserId == userId);
            if (goal == null) return false;

            // clear existing that are not in new list
            var toRemove = goal.GoalTasks.Where(gt => !taskIds.Contains(gt.WorkTaskId)).ToList();
            _db.RemoveRange(toRemove);

            // add missing
            foreach (var tid in taskIds)
            {
                if (!goal.GoalTasks.Any(gt => gt.WorkTaskId == tid))
                {
                    goal.GoalTasks.Add(new GoalTask { GoalId = goal.Id, WorkTaskId = tid });
                }
            }

            goal.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task RecalculateProgressForGoalAsync(int goalId, string userId, string? note = null)
        {
            const int maxRetries = 3;
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    var goal = await _db.PersonalGoals.FirstOrDefaultAsync(g => g.Id == goalId && g.UserId == userId);
                    if (goal == null) return;

                    if (goal.Type == GoalType.TimeBased)
                    {
                        // Time-based goal: Progress is based on CompletedHours (which are synced from TimeLogs or manual entries)
                        goal.TargetValue = goal.TargetHours ?? 0;
                        goal.CurrentValue = goal.CompletedHours;
                    }
                    else if (goal.ProjectId.HasValue)
                    {
                        // Task-based (Project) goal: Progress is based on project tasks assigned to user
                        var projectTasks = _db.WorkTasks.Where(t => t.ProjectId == goal.ProjectId.Value && t.AssigneeId == goal.UserId);
                        int total = await projectTasks.CountAsync();
                        int completed = await projectTasks.CountAsync(t => t.Status == Models.TaskStatus.Completed);
                        
                        goal.TargetTasks = total;
                        goal.CompletedTasks = completed;
                        
                        goal.TargetValue = total;
                        goal.CurrentValue = completed;
                    }
                    else
                    {
                        // Task-based (Linked) goal: Progress is based on specifically linked tasks
                        goal.CompletedTasks = await _db.GoalTasks
                            .Where(gt => gt.GoalId == goalId)
                            .CountAsync(gt => gt.WorkTask.Status == Models.TaskStatus.Completed);
                        
                        goal.TargetTasks = await _db.GoalTasks.CountAsync(gt => gt.GoalId == goalId);
                        
                        goal.TargetValue = goal.TargetTasks ?? 0;
                        goal.CurrentValue = goal.CompletedTasks;
                    }

                    // update status and records
                    await UpdateStatusAndHistoryAsync(goal, note);
                    await _db.SaveChangesAsync();
                    
                    break; // Thành công thì thoát vòng lặp
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    // Xử lý Race Condition: Nếu có 2 luồng cùng update, luồng sau sẽ retry
                    // Quan trọng: Clear tracker để xóa các bản ghi History bị kẹt do lỗi Save trước đó
                    _db.ChangeTracker.Clear();

                    if (i == maxRetries - 1)
                    {
                        throw new Exception("Hệ thống đang bận. Quá trình cập nhật bị xung đột dữ liệu, vui lòng thử lại sau.", ex);
                    }
                    
                    foreach (var entry in ex.Entries)
                    {
                        await entry.ReloadAsync();
                    }
                }
            }
        }

        public async Task HandleTimeLogAsync(TimeLog timeLog)
        {
            // Validation: Ngăn chặn log thời gian ảo
            if (timeLog.LogDate > DateTime.UtcNow)
            {
                throw new InvalidOperationException("Không thể ghi nhận thời gian ở tương lai.");
            }
            if (timeLog.DurationHours <= 0 || timeLog.DurationHours > 24)
            {
                throw new InvalidOperationException("Thời lượng làm việc không hợp lệ.");
            }

            // When a timelog is created for a task, find related goals and recalc
            var relatedGoals = await _db.GoalTasks
                .Where(gt => gt.WorkTaskId == timeLog.WorkTaskId)
                .Select(gt => gt.Goal)
                .ToListAsync();

            foreach (var goal in relatedGoals)
            {
                // Thay vì += tay có thể gây sai lệch (Race condition) nếu có nhiều request,
                // Ta gọi lại hàm Recalculate để lấy con số chính xác nhất từ DB
                await RecalculateProgressForGoalAsync(goal.Id, goal.UserId);
            }
        }

        public async Task SyncProjectGoalsAsync(int projectId)
        {
            var goals = await _db.PersonalGoals
                .Where(g => g.ProjectId == projectId)
                .ToListAsync();

            foreach (var goal in goals)
            {
                await RecalculateProgressForGoalAsync(goal.Id, goal.UserId);
            }
        }

        public async Task SyncTaskGoalsAsync(int taskId)
        {
            // 1. Find goals linked via GoalTasks
            var goalIdsFromTasks = await _db.GoalTasks
                .Where(gt => gt.WorkTaskId == taskId)
                .Select(gt => gt.GoalId)
                .ToListAsync();

            // 2. Find goals linked via Project
            var task = await _db.WorkTasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskId);
            if (task != null && task.ProjectId.HasValue)
            {
                var projectGoalIds = await _db.PersonalGoals
                    .Where(g => g.ProjectId == task.ProjectId.Value)
                    .Select(g => g.Id)
                    .ToListAsync();
                
                goalIdsFromTasks.AddRange(projectGoalIds);
            }

            var uniqueGoalIds = goalIdsFromTasks.Distinct().ToList();
            foreach (var gid in uniqueGoalIds)
            {
                var goal = await _db.PersonalGoals.AsNoTracking().FirstOrDefaultAsync(g => g.Id == gid);
                if (goal != null)
                {
                    await RecalculateProgressForGoalAsync(gid, goal.UserId);
                }
            }
        }
        public string GetAIPrediction(PersonalGoal goal)
        {
            string cacheKey = $"AIPrediction_Goal_{goal.Id}";

            if (!_cache.TryGetValue(cacheKey, out string cachedPrediction))
            {
                // Caching prediction trong 5 phút để tránh CPU spikes do tính toán liên tục
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));

                if (goal.CurrentValue <= 0) 
                    cachedPrediction = "Hãy bắt đầu làm việc để AI có thể phân tích tốc độ của bạn.";
                else
                {
                    var timeTotal = (goal.TargetDate - goal.StartDate).TotalDays;
                    var timePassed = (DateTime.UtcNow - goal.StartDate).TotalDays;

                    if (timePassed < 0.5) 
                        cachedPrediction = "AI đang thu thập thêm dữ liệu điểm tin...";
                    else
                    {
                        // Tốc độ trung bình: Giá trị/Ngày
                        double velocity = goal.CurrentValue / timePassed;
                        double remainingValue = goal.TargetValue - goal.CurrentValue;
                        
                        if (velocity <= 0 || remainingValue <= 0)
                        {
                            cachedPrediction = "Dữ liệu hiện tại cho thấy bạn đang đi đúng lộ trình. Hãy duy trì phong độ!";
                            _cache.Set(cacheKey, cachedPrediction, cacheOptions);
                            return cachedPrediction;
                        }

                        double daysNeeded = remainingValue / velocity;
                        // Clamp daysNeeded to avoid DateTime overflow
                        if (daysNeeded > 3650) daysNeeded = 3650; 

                        DateTime estimatedFinishDate = DateTime.UtcNow.AddDays(daysNeeded);

                        if (estimatedFinishDate > goal.TargetDate)
                        {
                            var lateDays = Math.Ceiling((estimatedFinishDate - goal.TargetDate).TotalDays);
                            cachedPrediction = $"⚠️ AI dự báo: Bạn có thể trễ hạn {lateDays} ngày. Hãy tăng tốc độ làm việc!";
                        }
                        else
                        {
                            cachedPrediction = $"✅ AI dự báo: Bạn đang làm rất tốt! Mục tiêu dự kiến hoàn thành vào {estimatedFinishDate:dd/MM/yyyy}.";
                        }
                    }
                }

                _cache.Set(cacheKey, cachedPrediction, cacheOptions);
            }

            return cachedPrediction;
        }

        public string GetAIShortStatus(PersonalGoal goal)
        {
            if (goal.CurrentValue <= 0) return "Starting Up";

            var timePassed = (DateTime.UtcNow - goal.StartDate).TotalDays;
            if (timePassed < 0.2) return "Initializing";

            double velocity = goal.CurrentValue / Math.Max(0.1, timePassed);
            double remainingValue = goal.TargetValue - goal.CurrentValue;
            double daysNeeded = remainingValue / Math.Max(0.01, velocity);
            DateTime estimatedFinishDate = DateTime.UtcNow.AddDays(daysNeeded);

            if (estimatedFinishDate > goal.TargetDate) return "At Risk";
            
            var progress = (goal.CurrentValue / Math.Max(1, goal.TargetValue)) * 100;
            if (progress >= 100) return "Completed";
            if (progress > 90) return "Finishing";
            if (estimatedFinishDate < goal.TargetDate.AddDays(-3)) return "Excellent";
            if (estimatedFinishDate < goal.TargetDate.AddDays(-1)) return "Ahead";
            
            return "On Track";
        }

        private async Task UpdateStatusAndHistoryAsync(PersonalGoal goal, string? note = null)
        {
            // Calculate progress percent based on tasks
            double progress = 0;
            if (goal.TargetTasks.GetValueOrDefault() > 0)
            {
                progress = (double)goal.CompletedTasks / goal.TargetTasks.Value * 100.0;
            }
            else if (goal.TargetValue > 0)
            {
                progress = (goal.CurrentValue / goal.TargetValue) * 100.0;
            }

            // clamp
            progress = Math.Max(0, Math.Min(100, progress));

            // update status according to business rules
            var expected = CalculateExpectedProgress(goal);
            var now = DateTime.UtcNow;

            if (progress >= 100)
            {
                goal.Status = GoalStatus.Completed;
            }
            else if (now > goal.TargetDate)
            {
                goal.Status = GoalStatus.Overdue;
            }
            else if (progress < expected)
            {
                goal.Status = GoalStatus.Behind;
            }
            else
            {
                goal.Status = GoalStatus.OnTrack;
            }

            // record history
            var hist = new GoalProgressHistory
            {
                GoalId = goal.Id,
                Progress = progress,
                RecordedAt = DateTime.UtcNow,
                Note = note ?? $"Auto update: status={goal.Status}"
            };
            _db.Add(hist);

            goal.UpdatedAt = DateTime.UtcNow;

            // Gửi thông báo Real-time tới đúng User
            await _hubContext.Clients.User(goal.UserId).SendAsync("ReceiveGoalUpdate", new
            {
                goalId = goal.Id,
                newProgress = progress,
                status = goal.Status.ToString(),
                currentValue = goal.CurrentValue,
                completedTasks = goal.CompletedTasks,
                targetTasks = goal.TargetTasks ?? 0
            });
        }
 
        private double CalculateExpectedProgress(PersonalGoal goal)
        {
            var total = (goal.TargetDate - goal.StartDate).TotalSeconds;
            if (total <= 0) return 1.0; // if invalid range, assume expected 100%
            var passed = (DateTime.UtcNow - goal.StartDate).TotalSeconds;
            var expected = passed / total * 100.0;
            expected = Math.Max(0, Math.Min(100, expected));
            return expected;
        }

        private GoalDetailDto MapToDto(PersonalGoal goal)
        {
            return new GoalDetailDto
            {
                Id = goal.Id,
                Title = goal.Title,
                Description = goal.Description,
                Type = goal.Type,
                TargetHours = goal.TargetHours,
                TargetTasks = goal.TargetTasks,
                CompletedHours = goal.CompletedHours,
                CompletedTasks = goal.CompletedTasks,
                Status = goal.Status,
                StartDate = goal.StartDate,
                TargetDate = goal.TargetDate
            };
        }

        public async Task<string> RegenerateSmartAIStrategyAsync(int goalId, string userId)
        {
            var goal = await _db.PersonalGoals
                .FirstOrDefaultAsync(g => g.Id == goalId && g.UserId == userId);
                
            if (goal == null) return "Không tìm thấy mục tiêu.";

            var now = DateTime.UtcNow;
            var target = goal.TargetDate;
            double daysLeft = (target.Date - now.Date).TotalDays;
            daysLeft = Math.Max(1.0, daysLeft);

            string promptData = "";
            var localPieces = new List<string>();

            if (goal.Type == GoalType.TimeBased)
            {
                double remainingHours = Math.Max(0, (goal.TargetHours ?? 0) - goal.CompletedHours);
                
                // Chuẩn bị Prompt cho OpenAI
                promptData = $"Mục tiêu cá nhân: {goal.Title}\n" +
                             $"Mô tả: {goal.Description ?? "Không có"}\n" +
                             $"Tổng số giờ mục tiêu: {goal.TargetHours}\n" +
                             $"Số giờ đã hoàn thành: {goal.CompletedHours}\n" +
                             $"Số giờ còn lại: {remainingHours}\n" +
                             $"Số ngày còn lại: {daysLeft} ngày (Hạn chót: {goal.TargetDate:dd/MM/yyyy}).\n" +
                             $"Hãy đóng vai một chuyên gia quản lý thời gian, đưa ra chiến lược làm việc (như số giờ mỗi ngày, chia nhỏ phiên làm việc, cảnh báo rủi ro nếu có) để hoàn thành mục tiêu này.";

                // Fallback cục bộ
                localPieces.Add($"📊 **Tổng quan:** Bạn còn {remainingHours:F1} giờ phải hoàn thành trong {daysLeft} ngày tới.");
                if (remainingHours <= 0)
                {
                    localPieces.Add("🎉 **Tuyệt vời!** Bạn đã hoàn thành hoặc vượt mục tiêu thời gian đề ra.");
                }
                else
                {
                    double hoursPerDay = remainingHours / daysLeft;
                    localPieces.Add($"⚡ **Kế hoạch Cụ thể:** Để kịp tiến độ, bạn cần duy trì **{hoursPerDay:F1} giờ / ngày**.");
                    if (hoursPerDay > 8)
                        localPieces.Add("⚠️ **Cảnh báo Khả thi:** Số giờ yêu cầu mỗi ngày quá cao (>8 giờ). Bạn nên cân nhắc gia hạn thêm thời gian.");
                    else if (hoursPerDay > 4)
                        localPieces.Add("🔥 **Chiến thuật Pomodoro:** Gợi ý chia nhỏ thành 4-6 phiên làm việc, mỗi phiên 25-50 phút, xen kẽ nghỉ dài.");
                    else
                        localPieces.Add("💡 **Chiến thuật Dễ dàng:** Lịch trình hoàn toàn nằm trong tầm tay.");
                }
            }
            else
            {
                int projectId = goal.ProjectId ?? 0;
                var tasks = await _db.WorkTasks
                    .AsNoTracking()
                    .Where(t => t.ProjectId == projectId && t.AssigneeId == userId && t.Status != Models.TaskStatus.Completed)
                    .ToListAsync();

                var remainingTasks = tasks.Count;
                var urgentTasks = tasks.Where(t => t.Priority == Priority.Urgent).Take(3).ToList();
                var highTasks = tasks.Where(t => t.Priority == Priority.High).Take(3).ToList();
                var overdueTasks = tasks.Where(t => t.EndDate < now).Take(3).ToList();

                // Chuẩn bị Prompt cho OpenAI
                var taskNames = string.Join(", ", tasks.Take(5).Select(t => t.Title));
                promptData = $"Dự án liên kết có {remainingTasks} công việc chưa hoàn thành.\n" +
                             $"Số ngày còn lại: {daysLeft} ngày (Hạn chót: {goal.TargetDate:dd/MM/yyyy}).\n" +
                             $"Số task quá hạn: {overdueTasks.Count}\n" +
                             $"Số task khẩn cấp (Urgent): {urgentTasks.Count}\n" +
                             $"Một số task tiêu biểu: {taskNames}...\n" +
                             $"Hãy đóng vai một chuyên gia quản lý dự án, đưa ra chiến lược phân bổ nguồn lực và thứ tự ưu tiên các công việc để hoàn thành dự án đúng hạn.";

                // Fallback cục bộ
                localPieces.Add($"📊 **Tổng quan:** Dự án còn {remainingTasks} công việc chưa hoàn thành.");
                if (remainingTasks == 0)
                {
                    localPieces.Add("🎉 **Tuyệt vời!** Bạn không còn công việc nào tồn đọng trong dự án này.");
                }
                else
                {
                    double tasksPerDay = Math.Ceiling(remainingTasks / daysLeft);
                    localPieces.Add($"⚡ **Chỉ tiêu:** Cần xử lý tối thiểu **{tasksPerDay} task / ngày** để đảm bảo đúng hạn.");
                    if (overdueTasks.Any())
                    {
                        localPieces.Add("\n🚨 **XỬ LÝ KHẨN CẤP (QUÁ HẠN):**");
                        foreach (var t in overdueTasks) localPieces.Add($"- Khắc phục ngay: *{t.Title}*");
                    }
                    if (urgentTasks.Any())
                    {
                        localPieces.Add("\n🔥 **ƯU TIÊN HÀNG ĐẦU (URGENT):**");
                        foreach (var t in urgentTasks) 
                            if (!overdueTasks.Any(ot => ot.Id == t.Id)) localPieces.Add($"- *{t.Title}*");
                    }
                    if (!overdueTasks.Any() && !urgentTasks.Any() && highTasks.Any())
                    {
                        localPieces.Add("\n⭐ **BƯỚC TIẾP THEO (HIGH PRIORITY):**");
                        foreach (var t in highTasks) localPieces.Add($"- Cần làm: *{t.Title}*");
                    }
                }
            }

            string aiResponse = "";
            string apiKey = _config["OpenAI:ApiKey"];
            
            if (!string.IsNullOrEmpty(apiKey))
            {
                try
                {
                    var client = _httpClientFactory.CreateClient();
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                    
                    var model = _config["OpenAI:Model"] ?? "gpt-4o-mini";
                    
                    var payload = new
                    {
                        model = model,
                        messages = new[]
                        {
                            new { role = "system", content = "Bạn là một trợ lý AI quản lý thời gian chuyên nghiệp. Phân tích dữ liệu người dùng cung cấp và đưa ra một kế hoạch hành động cụ thể, cá nhân hóa. Sử dụng tiếng Việt, định dạng Markdown (có gạch đầu dòng, in đậm) và thêm các emoji sinh động. Câu văn ngắn gọn, đi thẳng vào vấn đề (tối đa 200 từ)." },
                            new { role = "user", content = promptData }
                        },
                        temperature = 0.7
                    };
                    
                    var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                    var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var responseString = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(responseString);
                        var message = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
                        
                        aiResponse = $"🤖 **AI ACTION PLAN (Powered by LLM)**\n" +
                                     $"*Cập nhật lúc: {now:dd/MM/yyyy HH:mm}*\n\n" + message;
                    }
                }
                catch (Exception)
                {
                    // Ignore exception to fallback to local pieces
                }
            }
            
            if (string.IsNullOrEmpty(aiResponse))
            {
                // Sử dụng kết quả tính toán cục bộ
                aiResponse = $"🤖 **CHIẾN LƯỢC HÀNH ĐỘNG AI (Hệ thống phân tích)**\n" +
                             $"*Cập nhật lúc: {now:dd/MM/yyyy HH:mm}*\n\n" + string.Join("\n", localPieces);
            }

            goal.AIActionPlan = aiResponse;
            goal.UpdatedAt = DateTime.UtcNow;
            
            _db.PersonalGoals.Update(goal);
            await _db.SaveChangesAsync();

            return goal.AIActionPlan;
        }
    }
}
