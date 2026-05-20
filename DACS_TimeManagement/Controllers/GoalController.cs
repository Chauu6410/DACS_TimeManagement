using DACS_TimeManagement.Models;
using DACS_TimeManagement.Repositories;
using DACS_TimeManagement.DTOs;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using DACS_TimeManagement.Services.Interfaces;

namespace DACS_TimeManagement.Controllers
{
    [Authorize]
    public class GoalController : Controller
    {
        private readonly IGoalRepository _goalRepo;
        private readonly ApplicationDbContext _db;
        private readonly IGoalService _goalService;

        public GoalController(IGoalRepository goalRepo, ApplicationDbContext db, IGoalService goalService)
        {
            _goalRepo = goalRepo;
            _db = db;
            _goalService = goalService;
        }

        // GET: Goal
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            // Eager load Project and GoalTasks to avoid N+1 issues in the view
            var goals = await _db.PersonalGoals
                .AsNoTracking()
                .AsSplitQuery()
                .Include(g => g.Project)
                .Include(g => g.GoalProgressHistories)
                .Include(g => g.GoalTasks)
                    .ThenInclude(gt => gt.WorkTask)
                        .ThenInclude(wt => wt.TimeLogs)
                .Where(g => g.UserId == userId)
                .OrderByDescending(g => g.CreatedAt)
                .ToListAsync();

            var projects = await _db.Projects
                .AsNoTracking()
                .Where(p => p.UserId == userId)
                .Select(p => new { p.Id, p.Name, p.EndDate })
                .ToListAsync();

            ViewBag.Projects = projects;
            return View(goals);
        }

        // GET: Goal/Create
        public IActionResult Create()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var projects = _db.Projects
                .AsNoTracking()
                .Where(p => p.UserId == userId)
                .Select(p => new { p.Id, p.Name, p.EndDate })
                .ToList();

            ViewBag.Projects = projects;
            return View(new CreateGoalDto { TargetDate = DateTime.UtcNow.Date.AddDays(7) });
        }

        // POST: Goal/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateGoalDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (dto.Type == GoalType.TaskBased)
            {
                var project = await _db.Projects
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == dto.ProjectId && p.UserId == userId);

                if (project == null)
                {
                    ModelState.AddModelError(nameof(dto.ProjectId), "Vui lòng chọn Project cho mục tiêu Task-based.");
                }
                else if (project.EndDate.HasValue && dto.TargetDate.Date > project.EndDate.Value.Date)
                {
                    ModelState.AddModelError(nameof(dto.TargetDate), $"Target date cannot exceed project deadline ({project.EndDate.Value:dd/MM/yyyy}).");
                }
            }
            else
            {
                // TimeBased (Personal)
                if (dto.TargetHours <= 0 || dto.TargetHours == null)
                {
                    ModelState.AddModelError(nameof(dto.TargetHours), "Vui lòng nhập số giờ mục tiêu hợp lệ.");
                }
                if (string.IsNullOrWhiteSpace(dto.Title))
                {
                    ModelState.AddModelError(nameof(dto.Title), "Vui lòng nhập tên công việc.");
                }
            }

            if (dto.Type == GoalType.TaskBased)
            {
                ModelState.Remove(nameof(dto.Title));
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Projects = _db.Projects.AsNoTracking().Where(p => p.UserId == userId).Select(p => new { p.Id, p.Name, p.EndDate }).ToList();
                return View(dto);
            }

            var goal = new PersonalGoal
            {
                Description = dto.Description,
                ProjectId = dto.Type == GoalType.TaskBased ? dto.ProjectId : null,
                Type = dto.Type,
                StartDate = DateTime.UtcNow,
                TargetDate = dto.TargetDate,
                TargetHours = dto.TargetHours,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CurrentValue = 0
            };

            if (dto.Type == GoalType.TaskBased)
            {
                var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == dto.ProjectId && p.UserId == userId);
                goal.Title = project?.Name + " - Commitment";
                var totalTasks = await _db.WorkTasks.Where(t => t.ProjectId == dto.ProjectId && t.AssigneeId == userId).CountAsync();
                goal.TargetValue = totalTasks;
                goal.TargetTasks = totalTasks;
            }
            else
            {
                goal.Title = dto.Title;
                goal.TargetValue = dto.TargetHours ?? 0;
            }

            await _goalRepo.AddAsync(goal);
            await _goalRepo.SaveAsync();

            // Generate AI strategy for both goal types
            await _goalService.RegenerateSmartAIStrategyAsync(goal.Id, userId);

            _db.GoalProgressHistories.Add(new GoalProgressHistory
            {
                GoalId = goal.Id,
                Progress = 0,
                RecordedAt = DateTime.UtcNow,
                Note = "Initial Commitment Established"
            });
            await _db.SaveChangesAsync();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = true });

            return RedirectToAction(nameof(Index));
        }

        // GET: Goal/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var goal = await _db.PersonalGoals
                .Include(g => g.Project)
                .FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId);
            
            if (goal == null) return NotFound();
            return View(goal);
        }

        // POST: Goal/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PersonalGoal goal)
        {
            if (id != goal.Id) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var existingGoal = await _db.PersonalGoals.FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId);
            
            if (existingGoal == null) return NotFound();

            ModelState.Remove("UserId");
            if (ModelState.IsValid)
            {
                existingGoal.Title = goal.Title;
                existingGoal.Description = goal.Description;
                existingGoal.TargetDate = goal.TargetDate;
                
                if (existingGoal.Type == GoalType.TimeBased && goal.TargetHours.HasValue)
                {
                    existingGoal.TargetValue = goal.TargetHours.Value;
                }
                else
                {
                    existingGoal.TargetHours = goal.TargetHours;
                }

                existingGoal.UpdatedAt = DateTime.UtcNow;

                _db.PersonalGoals.Update(existingGoal);
                await _db.SaveChangesAsync();

                // Recalculate to ensure status and progress are in sync
                await _goalService.RecalculateProgressForGoalAsync(existingGoal.Id, userId);
                
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = true });

                return RedirectToAction(nameof(Index));
            }
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = false, message = "Invalid data." });

            return View(goal);
        }

        // GET: Goal/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            // 1. Fetch Goal with basic info (Optimized: No deep tasks/logs here)
            var goal = await _db.PersonalGoals
                .AsNoTracking()
                .Include(g => g.Project)
                .Include(g => g.GoalTasks)
                    .ThenInclude(gt => gt.WorkTask)
                .FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId);

            if (goal == null) return NotFound();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new {
                    id = goal.Id,
                    title = goal.Title,
                    description = goal.Description,
                    targetDate = goal.TargetDate.ToString("yyyy-MM-ddTHH:mm"),
                    targetHours = goal.TargetHours,
                    type = goal.Type.ToString(),
                    projectId = goal.ProjectId
                });

            // 2. Optimized History Fetching
            var taskIds = goal.GoalTasks.Select(gt => gt.WorkTaskId).ToList();
            
            // Fetch latest 100 TimeLogs
            var timeLogsQuery = _db.TimeLogs.AsNoTracking().Include(tl => tl.WorkTask).AsQueryable();
            if (goal.ProjectId.HasValue)
            {
                timeLogsQuery = timeLogsQuery.Where(tl => tl.WorkTask.ProjectId == goal.ProjectId.Value);
            }
            else
            {
                timeLogsQuery = timeLogsQuery.Where(tl => taskIds.Contains(tl.WorkTaskId));
            }

            var timeLogs = await timeLogsQuery
                .OrderByDescending(tl => tl.LogDate)
                .Take(100)
                .Select(tl => new GoalHistoryItemDto { 
                    Id = tl.Id,
                    Title = tl.WorkTask.Title, 
                    Date = tl.LogDate, 
                    Duration = (double?)tl.DurationHours, 
                    Note = tl.Note,
                    Type = goal.ProjectId.HasValue ? "Project Focus" : "Task Focus"
                })
                .ToListAsync();

            // Fetch latest 100 Progress Histories
            var progressHistoryRaw = await _db.GoalProgressHistories
                .AsNoTracking()
                .Where(h => h.GoalId == id && !string.IsNullOrEmpty(h.Note) && !h.Note.Contains("Auto update"))
                .OrderByDescending(h => h.RecordedAt)
                .Take(100)
                .ToListAsync();

            var progressHistory = progressHistoryRaw.Select(h => {
                double? parsedDuration = null;
                if (h.Note != null && h.Note.Contains("[Focus "))
                {
                    try {
                        var start = h.Note.IndexOf("[Focus ") + 7;
                        var end = h.Note.IndexOf("]", start);
                        if (end > start) {
                            var durStr = h.Note.Substring(start, end - start);
                            if (durStr.Contains(":")) {
                                var parts = durStr.Split(':');
                                if (parts.Length == 3) parsedDuration = int.Parse(parts[0]) + int.Parse(parts[1])/60.0 + int.Parse(parts[2])/3600.0;
                                else if (parts.Length == 2) parsedDuration = int.Parse(parts[0])/60.0 + int.Parse(parts[1])/3600.0;
                            } else if (durStr.EndsWith("m")) {
                                parsedDuration = double.Parse(durStr.Replace("m","")) / 60.0;
                            } else if (durStr.EndsWith("s")) {
                                parsedDuration = double.Parse(durStr.Replace("s","")) / 3600.0;
                            }
                        }
                    } catch {}
                }
                
                return new GoalHistoryItemDto { 
                    Id = h.Id,
                    Title = goal.Title, 
                    Date = h.RecordedAt, 
                    Duration = parsedDuration, 
                    Note = h.Note,
                    Type = "Goal Focus"
                };
            }).ToList();

            // Combine and sort
            var allHistory = timeLogs.Concat(progressHistory)
                .OrderByDescending(x => x.Date)
                .Take(100)
                .ToList();

            ViewBag.AllHistory = allHistory;

            // 3. Pre-calculate summary stats
            double totalLogged = 0;
            if (goal.ProjectId.HasValue)
            {
                totalLogged = await _db.TimeLogs.Where(tl => tl.WorkTask.ProjectId == goal.ProjectId.Value).SumAsync(tl => tl.DurationHours);
            }
            else
            {
                totalLogged = await _db.TimeLogs.Where(tl => taskIds.Contains(tl.WorkTaskId)).SumAsync(tl => tl.DurationHours);
            }
            ViewBag.TotalLogged = totalLogged;

            return View(goal);
        }

        // GET: Goal/Delete/5
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var goal = await _db.PersonalGoals
                .Include(g => g.Project)
                .FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId);
            
            if (goal == null) return NotFound();
            return View(goal);
        }

        // POST: Goal/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var goal = await _db.PersonalGoals.FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId);
            
            if (goal == null)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = false, message = "Goal not found." });
                return NotFound();
            }

            try
            {
                _db.PersonalGoals.Remove(goal);
                await _db.SaveChangesAsync();

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = true });

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = false, message = ex.Message });
                return View("Error");
            }
        }

        // GET: Goal/Focus/5
        public async Task<IActionResult> Focus(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var goal = await _db.PersonalGoals
                .Include(g => g.GoalTasks)
                    .ThenInclude(gt => gt.WorkTask)
                .Include(g => g.Project)
                    .ThenInclude(p => p.Tasks)
                .FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId);

            if (goal == null) return NotFound();

            // Auto-populate tasks if none are linked but project exists
            if (!goal.GoalTasks.Any() && goal.ProjectId.HasValue && goal.Project != null)
            {
                foreach (var task in goal.Project.Tasks)
                {
                    goal.GoalTasks.Add(new GoalTask { GoalId = goal.Id, WorkTaskId = task.Id, WorkTask = task });
                }
            }

            return View(goal);
        }

        // POST: Goal/RecordFocusSession
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordFocusSession(int goalId, int? taskId, int durationSeconds, string? note = null)
        {
            double durationHours = durationSeconds / 3600.0;
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var goal = await _db.PersonalGoals
                .Include(g => g.GoalTasks)
                .FirstOrDefaultAsync(g => g.Id == goalId && g.UserId == userId);
            
            if (goal == null) 
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = false, message = "Goal not found." });
                return NotFound();
            }

            // Format duration for note
            int hours = durationSeconds / 3600;
            int minutes = (durationSeconds % 3600) / 60;
            int seconds = durationSeconds % 60;
            string durationStr = hours > 0 
                ? $"{hours}h {minutes}m {seconds}s" 
                : minutes > 0 
                    ? $"{minutes}m {seconds}s" 
                    : $"{seconds}s";
            
            string focusNote = $"🌊 Nox Ocean Focus [{durationStr}]" + (string.IsNullOrEmpty(note) ? "" : $" - {note}");

            if (taskId.HasValue && taskId.Value > 0)
            {
                // Task-based: Create TimeLog entry with Goal link
                var task = await _db.WorkTasks.FirstOrDefaultAsync(t => t.Id == taskId.Value);
                if (task == null)
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                        return Json(new { success = false, message = "Task not found." });
                    return NotFound();
                }

                var timeLog = new TimeLog
                {
                    WorkTaskId = taskId.Value,
                    LogDate = DateTime.UtcNow,
                    DurationHours = durationHours,
                    Note = focusNote,
                    GoalId = goalId,
                    IsFocusSession = true
                };
                _db.TimeLogs.Add(timeLog);
                await _db.SaveChangesAsync();

                // Update goal progress via service
                await _goalService.HandleTimeLogAsync(timeLog);
            }
            else
            {
                // Time-based (Personal): Create TimeLog for first linked task or update goal directly
                var firstTask = goal.GoalTasks.FirstOrDefault();
                
                if (firstTask != null)
                {
                    // If goal has linked tasks, log to first task
                    var timeLog = new TimeLog
                    {
                        WorkTaskId = firstTask.WorkTaskId,
                        LogDate = DateTime.UtcNow,
                        DurationHours = durationHours,
                        Note = focusNote,
                        GoalId = goalId,
                        IsFocusSession = true
                    };
                    _db.TimeLogs.Add(timeLog);
                    await _db.SaveChangesAsync();
                    
                    await _goalService.HandleTimeLogAsync(timeLog);
                }
                else
                {
                    // No linked tasks: update goal directly and create progress history
                    goal.CompletedHours += durationHours;
                    goal.CurrentValue = goal.CompletedHours;
                    goal.UpdatedAt = DateTime.UtcNow;
                    _db.PersonalGoals.Update(goal);
                    
                    // Create progress history entry
                    var progressHistory = new GoalProgressHistory
                    {
                        GoalId = goal.Id,
                        Progress = goal.TargetHours.HasValue && goal.TargetHours.Value > 0 
                            ? (goal.CompletedHours / goal.TargetHours.Value) * 100 
                            : 0,
                        RecordedAt = DateTime.UtcNow,
                        Note = focusNote
                    };
                    _db.GoalProgressHistories.Add(progressHistory);
                    
                    await _db.SaveChangesAsync();
                    await _goalService.RecalculateProgressForGoalAsync(goal.Id, userId);
                }
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { 
                    success = true, 
                    message = $"Logged {durationStr} successfully!",
                    durationHours = Math.Round(durationHours, 2)
                });

            return RedirectToAction(nameof(Details), new { id = goalId });
        }

        // AJAX: Sync goal status for Index view
        [HttpGet]
        public async Task<IActionResult> GetAllGoalsStatus()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var goals = await _db.PersonalGoals
                .AsNoTracking()
                .Where(g => g.UserId == userId)
                .ToListAsync();

            var today = DateTime.UtcNow.Date;

            var list = goals.Select(g => new
            {
                id = g.Id,
                goalName = g.Title,
                currentValue = g.Type == GoalType.TimeBased ? g.CompletedHours : g.CompletedTasks,
                targetValue = g.Type == GoalType.TimeBased ? g.TargetHours ?? 0 : (double?)(g.TargetTasks ?? 0),

                aiPrediction = _goalService.GetAIPrediction(g),
                aiStatus = _goalService.GetAIShortStatus(g)
            });

            return Json(list);
        }

        // AJAX: Get progress history for charts
        [HttpGet]
        public async Task<IActionResult> GetGoalHistory(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var history = await _db.GoalProgressHistories
                .Where(h => h.GoalId == id && h.Goal.UserId == userId)
                .OrderBy(h => h.RecordedAt)
                .Take(20)
                .Select(h => new {
                    date = h.RecordedAt.ToString("dd/MM HH:mm", System.Globalization.CultureInfo.InvariantCulture),

                    progress = Math.Round(h.Progress, 1)
                })
                .ToListAsync();

            return Json(history);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegenerateAIStrategy(int goalId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var strategy = await _goalService.RegenerateSmartAIStrategyAsync(goalId, userId);
            
            if (strategy == "Không tìm thấy mục tiêu.") return NotFound();

            return Json(new { success = true, actionPlan = strategy });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteHistory(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var history = await _db.GoalProgressHistories
                .Include(h => h.Goal)
                .FirstOrDefaultAsync(h => h.Id == id && h.Goal.UserId == userId);
                
            if (history == null) return NotFound();

            _db.GoalProgressHistories.Remove(history);
            await _db.SaveChangesAsync();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = true });

            return RedirectToAction(nameof(Details), new { id = history.GoalId });
        }
    }
}
