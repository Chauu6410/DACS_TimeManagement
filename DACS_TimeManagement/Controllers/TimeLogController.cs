using DACS_TimeManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
            
            // Validate task exists and user has permission
            var task = await _context.WorkTasks.Include(t => t.Project).FirstOrDefaultAsync(t => t.Id == workTaskId);
            if (task == null) return NotFound();

            bool isOwner = task.UserId == userId;
            bool isAssignee = task.AssigneeId == userId;
            bool isProjectMember = false;
            
            if (task.ProjectId.HasValue)
            {
                isProjectMember = await _context.ProjectMembers.AnyAsync(pm => pm.ProjectId == task.ProjectId.Value && pm.UserId == userId);
            }

            if (!isOwner && !isAssignee && !isProjectMember)
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
