using DACS_TimeManagement.DTOs;
using DACS_TimeManagement.Hubs;
using DACS_TimeManagement.Models;
using DACS_TimeManagement.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore.ChangeTracking;

using System;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using System.Threading;

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
        private readonly IGeminiService _geminiService;
        private readonly ILogger<GoalService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private static readonly SemaphoreSlim _aiSemaphore = new SemaphoreSlim(1, 1);

        public GoalService(ApplicationDbContext db, IHubContext<GoalHub> hubContext, IMemoryCache cache, IHttpClientFactory httpClientFactory, IConfiguration config, IGeminiService geminiService, ILogger<GoalService> logger, IServiceScopeFactory scopeFactory)
        {
            _db = db;
            _hubContext = hubContext;
            _cache = cache;
            _httpClientFactory = httpClientFactory;
            _config = config;
            _geminiService = geminiService;
            _logger = logger;
            _scopeFactory = scopeFactory;
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

                    // --- Update LastUpdated Logic ---
                    goal.LastUpdated = DateTime.UtcNow;

                    // update status and records
                    await UpdateStatusAndHistoryAsync(goal, note);
                    await _db.SaveChangesAsync();

                    // Trigger AI regeneration in background with a new scope to avoid context disposal
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using (var scope = _scopeFactory.CreateScope())
                            {
                                var scopedGoalService = scope.ServiceProvider.GetRequiredService<IGoalService>();
                                await scopedGoalService.RegenerateSmartAIStrategyAsync(goalId, userId);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Background AI update failed for goal {GoalId}", goalId);
                        }
                    });

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
            // Lock to prevent concurrent AI requests for the same goal
            await _aiSemaphore.WaitAsync();
            try
            {
                // Throttling: avoid spamming AI for the same goal recently
                string throttleKey = $"AI_Throttle_{goalId}";
                if (_cache.TryGetValue(throttleKey, out _))
                {
                    var existingGoal = await _db.PersonalGoals.AsNoTracking().FirstOrDefaultAsync(g => g.Id == goalId);
                    return existingGoal?.AIActionPlan ?? "Phân tích đang được xử lý...";
                }

                var goal = await _db.PersonalGoals.FirstOrDefaultAsync(g => g.Id == goalId && g.UserId == userId);
                if (goal == null) return "Không tìm thấy mục tiêu.";

                _cache.Set(throttleKey, true, TimeSpan.FromSeconds(30));

                var now = DateTime.UtcNow;
                var target = goal.TargetDate;
                double daysLeft = Math.Max(1.0, (target.Date - now.Date).TotalDays);

                string projectName = "N/A";
                string projectDetail = "N/A";
                if (goal.ProjectId.HasValue)
                {
                    var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == goal.ProjectId.Value);
                    if (project != null)
                    {
                        projectName = project.Name;
                        projectDetail = project.Description ?? "No details provided.";
                    }
                }

                var profile = await _db.Set<UserProfile>().AsNoTracking().FirstOrDefaultAsync(up => up.UserId == userId);
                var currentUiCulture = System.Threading.Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName;
                var lang = currentUiCulture == "vi" || currentUiCulture == "en" ? currentUiCulture : (profile?.Language ?? "en");
                bool isVi = lang.StartsWith("vi", StringComparison.OrdinalIgnoreCase);

                string context = isVi ? "Bạn là một cố vấn chiến lược và chuyên gia hiệu suất." : "You are a strategic advisor and performance expert.";
                string goalText = isVi
                    ? "Dựa trên dữ liệu dưới đây, hãy: 1. Phân tích mức độ khó của mục tiêu (Dễ/Trung bình/Khó) dựa trên thời gian còn lại và tiến độ. 2. Đưa ra 3 hành động cụ thể, ngắn gọn, ưu tiên theo thứ tự. Trả lời bằng tiếng Việt, ngắn gọn, sử dụng Markdown nhẹ."
                    : "Based on the data below, please: 1. Analyze the difficulty of the goal (Easy/Average/Hard) based on remaining time and progress. 2. Provide 3 concise, prioritized actions. Reply in English, concisely, using light Markdown.";

                var progressInfo = goal.Type == GoalType.TaskBased
                    ? $"Tiến độ: {goal.CompletedTasks}/{goal.TargetTasks} công việc đã hoàn thành."
                    : $"Tiến độ: {goal.CompletedHours:F1}/{goal.TargetHours:F1} giờ đã thực hiện.";

                string userInput = $@"
Dữ liệu Mục tiêu:
- Tiêu đề: {goal.Title}
- Mô tả: {goal.Description ?? "Không có"}
- Trạng thái: {goal.Status}
- Loại: {goal.Type}
- {progressInfo}
- Hạn chót: {goal.TargetDate:dd/MM/yyyy} (Còn khoảng {daysLeft:F0} ngày)
Thông tin Dự án liên kết:
- Tên dự án: {projectName}
- Chi tiết: {projectDetail}
";

                string prompt = _geminiService.BuildAdvancedPrompt(context, goalText, userInput);

                int timeoutSeconds = _config.GetValue<int>("Gemini:TimeoutSeconds", 60);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

                string aiResponse;
                try
                {
                    aiResponse = await _geminiService.GenerateContent(prompt, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    _logger?.LogWarning("AI request timed out after {Seconds}s for goal {GoalId}", timeoutSeconds, goalId);
                    return "Lỗi: Yêu cầu tới dịch vụ AI đã hết thời gian chờ. Vui lòng thử lại sau.";
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Exception while calling AI for goal {GoalId}", goalId);
                    return "Lỗi khi kết nối tới dịch vụ AI. Vui lòng thử lại sau.";
                }

                if (string.IsNullOrWhiteSpace(aiResponse))
                {
                    return "Lỗi: AI không trả về nội dung. Vui lòng thử lại sau hoặc kiểm tra cấu hình API.";
                }

                if (aiResponse.StartsWith("Lỗi", StringComparison.OrdinalIgnoreCase)
                    || aiResponse.StartsWith("Error", StringComparison.OrdinalIgnoreCase)
                    || aiResponse.StartsWith("AI không", StringComparison.OrdinalIgnoreCase))
                {
                    return aiResponse;
                }

                try
                {
                    var lines = aiResponse.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(l => l.Trim()).ToList();
                    while (lines.Count > 0 && (lines[0].StartsWith("Chào", StringComparison.OrdinalIgnoreCase)
                                               || lines[0].StartsWith("Xin chào", StringComparison.OrdinalIgnoreCase)
                                               || lines[0].StartsWith("Hello", StringComparison.OrdinalIgnoreCase)
                                               || lines[0].StartsWith("Hi", StringComparison.OrdinalIgnoreCase)
                                               || lines[0].StartsWith("Dear", StringComparison.OrdinalIgnoreCase)
                                               || lines[0].StartsWith("Bạn là", StringComparison.OrdinalIgnoreCase)))
                    {
                        lines.RemoveAt(0);
                    }

                    if (lines.Count > 0 && lines[0].Length < 120 && lines[0].EndsWith(".", StringComparison.Ordinal))
                    {
                        lines.RemoveAt(0);
                    }

                    var trimmed = string.Join("\n\n", lines.Take(30));
                    const int maxStoredLength = 3000;
                    if (trimmed.Length > maxStoredLength)
                        trimmed = trimmed.Substring(0, maxStoredLength - 1) + "…";

                    aiResponse = trimmed;
                }
                catch
                {
                    const int maxStoredLength = 3000;
                    if (aiResponse.Length > maxStoredLength)
                        aiResponse = aiResponse.Substring(0, maxStoredLength - 1) + "…";
                }

                if (isVi)
                {
                    goal.AIActionPlanVi = aiResponse;
                    goal.AIActionPlanEn = null;
                }
                else
                {
                    goal.AIActionPlanEn = aiResponse;
                    goal.AIActionPlanVi = null;
                }
                goal.UpdatedAt = DateTime.UtcNow;
                _db.PersonalGoals.Update(goal);
                await _db.SaveChangesAsync();

                await _hubContext.Clients.User(userId).SendAsync("ReceiveAIPlanUpdate", new
                {
                    goalId = goal.Id,
                    actionPlan = aiResponse
                });

                return aiResponse;
            }
            catch (Exception ex)
            {
                return $"Error generating strategy: {ex.Message}";
            }
            finally
            {
                _aiSemaphore.Release();
            }
        }
    }
}
