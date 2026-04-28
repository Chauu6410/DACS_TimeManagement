using DACS_TimeManagement.DTOs;
using DACS_TimeManagement.Hubs;
using DACS_TimeManagement.Models;
using DACS_TimeManagement.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DACS_TimeManagement.Services
{
    // Business logic for goals. Keeps operations testable and focused.
    public class GoalService : IGoalService
    {
        private readonly ApplicationDbContext _db;
        private readonly IHubContext<GoalHub> _hubContext;
        private readonly IMemoryCache _cache;

        public GoalService(ApplicationDbContext db, IHubContext<GoalHub> hubContext, IMemoryCache cache)
        {
            _db = db;
            _hubContext = hubContext;
            _cache = cache;
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

        public async Task RecalculateProgressForGoalAsync(int goalId, string userId)
        {
            int maxRetries = 3;
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    // Lấy Goal (Không cần Include toàn bộ TimeLogs/Tasks để tránh N+1 và Cartesian explosion)
                    var goal = await _db.PersonalGoals
                        .FirstOrDefaultAsync(g => g.Id == goalId && g.UserId == userId);

                    if (goal == null) return;

                    // Aggregate directly in DB: Hiệu suất cao, tránh load hàng ngàn TimeLogs vào RAM
                    if (goal.Type == GoalType.TimeBased)
                    {
                        goal.CompletedHours = await _db.GoalTasks
                            .Where(gt => gt.GoalId == goalId)
                            .SelectMany(gt => gt.WorkTask.TimeLogs)
                            .SumAsync(tl => tl.DurationHours);
                    }
                    else
                    {
                        goal.CompletedTasks = await _db.GoalTasks
                            .Where(gt => gt.GoalId == goalId)
                            .CountAsync(gt => gt.WorkTask.Status == Models.TaskStatus.Completed);
                    }

                    // update status and records
                    await UpdateStatusAndHistoryAsync(goal);
                    await _db.SaveChangesAsync();
                    
                    break; // Thành công thì thoát vòng lặp
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    // Xử lý Race Condition: Nếu có 2 luồng cùng update, luồng sau sẽ retry
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
        public string GetAIPrediction(PersonalGoal goal)
        {
            string cacheKey = $"AIPrediction_Goal_{goal.Id}";

            if (!_cache.TryGetValue(cacheKey, out string cachedPrediction))
            {
                if (goal.CurrentValue <= 0) 
                    cachedPrediction = "Hãy bắt đầu làm việc để AI có thể phân tích tốc độ của bạn.";
                else
                {
                    var timeTotal = (goal.TargetDate - goal.StartDate).TotalDays;
                    var timePassed = (DateTime.Now - goal.StartDate).TotalDays;

                    if (timePassed < 0.5) 
                        cachedPrediction = "AI đang thu thập thêm dữ liệu điểm tin...";
                    else
                    {
                        // Tốc độ trung bình: Giá trị/Ngày
                        double velocity = goal.CurrentValue / timePassed;
                        double remainingValue = goal.TargetValue - goal.CurrentValue;
                        double daysNeeded = remainingValue / velocity;

                        DateTime estimatedFinishDate = DateTime.Now.AddDays(daysNeeded);

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

                // Caching prediction trong 5 phút để tránh CPU spikes do tính toán liên tục
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
                
                _cache.Set(cacheKey, cachedPrediction, cacheOptions);
            }

            return cachedPrediction;
        }

        public string GetAIShortStatus(PersonalGoal goal)
        {
            if (goal.CurrentValue <= 0) return "Starting Up";

            var timePassed = (DateTime.Now - goal.StartDate).TotalDays;
            if (timePassed < 0.2) return "Initializing";

            double velocity = goal.CurrentValue / Math.Max(0.1, timePassed);
            double remainingValue = goal.TargetValue - goal.CurrentValue;
            double daysNeeded = remainingValue / Math.Max(0.01, velocity);
            DateTime estimatedFinishDate = DateTime.Now.AddDays(daysNeeded);

            if (estimatedFinishDate > goal.TargetDate) return "At Risk";
            
            var progress = (goal.CurrentValue / Math.Max(1, goal.TargetValue)) * 100;
            if (progress > 90) return "Finishing";
            if (estimatedFinishDate < goal.TargetDate.AddDays(-3)) return "Excellent";
            if (estimatedFinishDate < goal.TargetDate.AddDays(-1)) return "Ahead";
            
            return "On Track";
        }

        private async Task UpdateStatusAndHistoryAsync(PersonalGoal goal)
        {
            // Calculate progress percent
            double progress = 0;
            if (goal.Type == GoalType.TimeBased && goal.TargetHours.GetValueOrDefault() > 0)
            {
                progress = (goal.CompletedHours / goal.TargetHours.Value) * 100.0;
            }
            else if (goal.Type == GoalType.TaskBased && goal.TargetTasks.GetValueOrDefault() > 0)
            {
                progress = (double)goal.CompletedTasks / goal.TargetTasks.Value * 100.0;
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
                Note = $"Auto update: status={goal.Status}"
            };
            _db.Add(hist);

            goal.UpdatedAt = DateTime.UtcNow;

            // Gửi thông báo Real-time tới đúng User
            await _hubContext.Clients.User(goal.UserId).SendAsync("ReceiveGoalUpdate", new
            {
                goalId = goal.Id,
                newProgress = progress,
                status = goal.Status.ToString(),
                currentValue = goal.CurrentValue
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

        internal async Task<string> GenerateSmartAIStrategy(int projectId, DateTime targetDate)
        {
            // Load project and tasks
            var project = await _db.Projects
                .AsNoTracking()
                .Include(p => p.Tasks)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null) return "Dự án không tồn tại.";

            var tasks = project.Tasks ?? new List<WorkTask>();
            var totalTasks = tasks.Count;
            var urgentCount = tasks.Count(t => t.Priority == Priority.Urgent);
            var highCount = tasks.Count(t => t.Priority == Priority.High);

            // Time windows
            var now = DateTime.UtcNow.Date;
            var start = DateTime.UtcNow.Date; // assume starting now for simplicity
            var totalWindow = (targetDate.Date - start).TotalDays;
            if (totalWindow <= 0)
            {
                return "TargetDate đã qua hoặc là hôm nay. Vui lòng chọn ngày hợp lệ.";
            }

            // Determine progress fraction by comparing elapsed time since start of goal to full window.
            // If we have a StartDate on the project use that, otherwise use created date.
            double elapsed = 0;
            if (project.CreatedDate != DateTime.MinValue)
            {
                elapsed = (now - project.CreatedDate.Date).TotalDays;
            }

            var fraction = Math.Max(0.0, Math.Min(1.0, elapsed / Math.Max(1.0, totalWindow)));

            // Strategy phases
            string phase;
            if (fraction < 0.33)
            {
                phase = "Khởi động"; // setup phase
            }
            else if (fraction < 0.75)
            {
                phase = "Tăng tốc"; // acceleration
            }
            else
            {
                phase = "Về đích"; // finishing
            }

            // Compute daily target for middle phase
            double daysLeft = (targetDate.Date - now).TotalDays;
            daysLeft = Math.Max(1.0, daysLeft);
            var tasksPerDay = Math.Ceiling((double)totalTasks / daysLeft);

            // Build human-friendly recommendation
            var pieces = new List<string>();
            pieces.Add($"🤖 **AI ACTION PLAN - GIAI ĐOẠN: {phase.ToUpper()}**");
            pieces.Add($"------------------------------------------");
            pieces.Add($"📊 **Tổng quan:** Dự án hiện có {totalTasks} công việc cần hoàn thành.");
            
            if (phase == "Khởi động")
            {
                pieces.Add("🚀 **Chiến lược:** Tập trung thiết lập nền tảng. Hãy xác định các task then chốt và đảm bảo mọi mục tiêu được định nghĩa rõ ràng.");
                pieces.Add("💡 *Lời khuyên:* Đừng vội vã, sự chuẩn bị kỹ lưỡng sẽ giúp bạn tăng tốc ở giai đoạn sau.");
            }
            else if (phase == "Tăng tốc")
            {
                pieces.Add($"⚡ **Chiến lược:** Cần duy trì hiệu suất khoảng **{tasksPerDay} task/ngày** để bám sát mục tiêu.");
                if (highCount + urgentCount > 0)
                {
                    pieces.Add($"🔥 **Ưu tiên:** Tập trung xử lý {urgentCount} task Khẩn cấp và {highCount} task Quan trọng trước.");
                }
            }
            else // Về đích
            {
                pieces.Add("🏁 **Chiến lược:** Giai đoạn nước rút! Ưu tiên tuyệt đối các task Khẩn cấp để đảm bảo chất lượng bàn giao.");
                if (urgentCount > 0)
                {
                    pieces.Add($"⚠️ **Cảnh báo:** Còn {urgentCount} task Khẩn cấp cần giải quyết ngay lập tức.");
                }
            }

            // Motivational predictive sentence
            var velocity = totalTasks / Math.Max(1.0, Math.Max(1.0, elapsed)); // rough tasks/day so far
            if (velocity > 0)
            {
                var estDays = Math.Ceiling((totalTasks) / velocity);
                var diff = Math.Round(daysLeft - estDays);
                if (diff > 0)
                {
                    pieces.Add($"\n✨ **Dự báo:** Với tốc độ hiện tại, bạn có thể về đích sớm **{diff} ngày**. Tuyệt vời!");
                }
                else if (diff < 0)
                {
                    pieces.Add($"\n📉 **Dự báo:** Bạn có nguy cơ trễ hạn **{Math.Abs(diff)} ngày**. Hãy cân nhắc tăng năng suất hoặc điều chỉnh phạm vi.");
                }
                else
                {
                    pieces.Add("\n🎯 **Dự báo:** Bạn đang đi đúng lộ trình đề ra.");
                }
            }

            return string.Join("\n", pieces);
        }
    }
}
