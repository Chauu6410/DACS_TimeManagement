using DACS_TimeManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DACS_TimeManagement.Controllers
{
    [Authorize]
    public class TimeLogController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TimeLogController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LogTime(int workTaskId, double durationHours, string? note)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            // Validate task belongs to user
            var task = await _context.WorkTasks.FindAsync(workTaskId);
            if (task == null || task.UserId != userId)
            {
                return Unauthorized();
            }

            if (durationHours <= 0)
            {
                return BadRequest("Duration must be positive.");
            }

            var timeLog = new TimeLog
            {
                WorkTaskId = workTaskId,
                DurationHours = durationHours,
                Note = note,
                LogDate = DateTime.Now
            };

            _context.TimeLogs.Add(timeLog);
            await _context.SaveChangesAsync();

            return RedirectToAction("Details", "WorkTask", new { id = workTaskId });
        }
    }
}
