using DACS_TimeManagement.Models;
using DACS_TimeManagement.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DACS_TimeManagement.Controllers
{
    [Authorize]
    public class GoalController : Controller
    {
        private readonly IGoalRepository _goalRepo;

        public GoalController(IGoalRepository goalRepo) => _goalRepo = goalRepo;

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var goals = await _goalRepo.GetAllAsync(userId);
            return View(goals);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PersonalGoal goal)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ModelState.Remove("UserId");
            if (ModelState.IsValid)
            {
                goal.UserId = userId;
                await _goalRepo.AddAsync(goal);
                await _goalRepo.SaveAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(goal);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var goal = await _goalRepo.GetByIdAsync(id, userId);
            
            if (goal == null) return NotFound();
            return View(goal);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(PersonalGoal goal)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ModelState.Remove("UserId");
            if (ModelState.IsValid)
            {
                goal.UserId = userId;
                _goalRepo.Update(goal);
                await _goalRepo.SaveAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(goal);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProgress(int id, double value)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var goal = await _goalRepo.GetByIdAsync(id, userId);
            if (goal != null)
            {
                goal.CurrentValue = value;
                
                UpdateStreak(goal);
                
                _goalRepo.Update(goal);
                await _goalRepo.SaveAsync();
                
                var forecast = ForecastGoal(goal);
                return Json(new { success = true, forecastStatus = forecast, streak = goal.CurrentStreak });
            }
            return Json(new { success = false });
        }

        [HttpPost]
        public async Task<IActionResult> CompletePomodoroSession(int id, double progressValue = 1)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var goal = await _goalRepo.GetByIdAsync(id, userId);
            if (goal != null)
            {
                goal.CurrentValue += progressValue;
                if (goal.CurrentValue > goal.TargetValue) goal.CurrentValue = goal.TargetValue;
                
                UpdateStreak(goal);

                _goalRepo.Update(goal);
                await _goalRepo.SaveAsync();
                
                var forecast = ForecastGoal(goal);
                return Json(new { success = true, newValue = goal.CurrentValue, forecastStatus = forecast, streak = goal.CurrentStreak });
            }
            return Json(new { success = false, message = "Goal not found" });
        }

        private void UpdateStreak(PersonalGoal goal)
        {
            var today = DateTime.Now.Date;
            if (goal.LastUpdated.HasValue)
            {
                var lastDate = goal.LastUpdated.Value.Date;
                if (lastDate == today.AddDays(-1))
                {
                    goal.CurrentStreak++;
                }
                else if (lastDate < today.AddDays(-1))
                {
                    goal.CurrentStreak = 0;
                }
            }
            goal.LastUpdated = DateTime.Now;
        }

        private string ForecastGoal(PersonalGoal goal)
        {
            if (goal.CurrentValue >= goal.TargetValue) return "Achieved";
            
            var daysPassed = (DateTime.Now - goal.StartDate).TotalDays;
            if (daysPassed < 1) return "On Track"; // Chưa đủ dữ liệu
            
            double velocity = goal.CurrentValue / daysPassed;
            if (velocity <= 0) return "At Risk"; 
            
            var daysRemaining = (goal.TargetValue - goal.CurrentValue) / velocity;
            var estimatedFinishDate = DateTime.Now.AddDays(daysRemaining);
            
            return estimatedFinishDate > goal.TargetDate ? "At Risk" : "On Track";
        }

        public async Task<IActionResult> Delete(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var goal = await _goalRepo.GetByIdAsync(id, userId);
            
            if (goal == null) return NotFound();
            return View(goal);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var goal = await _goalRepo.GetByIdAsync(id, userId);
            
            if (goal != null)
            {
                _goalRepo.Delete(goal);
                await _goalRepo.SaveAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
