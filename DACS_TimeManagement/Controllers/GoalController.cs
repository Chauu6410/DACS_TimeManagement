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
                .Include(g => g.Project)
                .Include(g => g.GoalProgressHistories)
                .Include(g => g.GoalTasks)
                    .ThenInclude(gt => gt.WorkTask)
                        .ThenInclude(wt => wt.TimeLogs)
                .Where(g => g.UserId == userId)
                .OrderByDescending(g => g.CreatedAt)
                .ToListAsync();

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
                
                return RedirectToAction(nameof(Index));
            }
            return View(goal);
        }

        // GET: Goal/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var goal = await _db.PersonalGoals
                .Include(g => g.Project)
                .Include(g => g.GoalProgressHistories)
                .Include(g => g.GoalTasks)
                    .ThenInclude(gt => gt.WorkTask)
                        .ThenInclude(wt => wt.TimeLogs)
                .FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId);

            if (goal == null) return NotFound();
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
        public async Task<IActionResult> RecordFocusSession(int goalId, int? taskId, double durationHours, string? note = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var goal = await _db.PersonalGoals.FirstOrDefaultAsync(g => g.Id == goalId && g.UserId == userId);
            
            if (goal == null) 
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = false, message = "Goal not found." });
                return NotFound();
            }

            if (taskId.HasValue && taskId.Value > 0)
            {
                // Task-based logging via TimeLog
                var timeLog = new TimeLog
                {
                    WorkTaskId = taskId.Value,
                    LogDate = DateTime.UtcNow,
                    DurationHours = durationHours,
                    Note = note ?? "Pomodoro Focus Session"
                };
                _db.TimeLogs.Add(timeLog);
                await _db.SaveChangesAsync();

                // Call GoalService to recalc everything related to this task
                await _goalService.HandleTimeLogAsync(timeLog);
            }
            else
            {
                // Time-based (Personal) goal logging directly to goal
                goal.CurrentValue += durationHours; // Note: setter of CurrentValue handles CompletedHours for TimeBased goals
                goal.UpdatedAt = DateTime.UtcNow;
                _db.PersonalGoals.Update(goal);
                await _db.SaveChangesAsync();
                
                await _goalService.RecalculateProgressForGoalAsync(goal.Id, userId, note);
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = true });

            return RedirectToAction(nameof(Index));
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

            var list = goals.Select(g => new
            {
                id = g.Id,
                goalName = g.Title,
                currentValue = g.Type == GoalType.TimeBased ? g.CompletedHours : g.CompletedTasks,
                targetValue = g.Type == GoalType.TimeBased ? g.TargetHours ?? 0 : (double?)(g.TargetTasks ?? 0),
                streak = g.CurrentStreak,
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
                    date = h.RecordedAt.ToString("dd/MM HH:mm"),
                    progress = Math.Round(h.Progress, 1)
                })
                .ToListAsync();

            return Json(history);
        }

        [HttpGet]
        public IActionResult GetGlobalStreak()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var goals = _db.PersonalGoals.Where(g => g.UserId == userId).ToList();
            var streak = goals.Any() ? goals.Max(g => g.CurrentStreak) : 0;
            return Json(new { streak });
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
    }
}
