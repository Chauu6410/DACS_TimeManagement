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
        public async Task<IActionResult> UpdateProgress(int id, int value)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var goal = await _goalRepo.GetByIdAsync(id, userId);
            if (goal != null)
            {
                goal.CurrentValue = value;
                
                _goalRepo.Update(goal);
                await _goalRepo.SaveAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false });
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
