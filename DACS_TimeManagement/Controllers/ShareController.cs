using DACS_TimeManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DACS_TimeManagement.Controllers
{
    [Authorize]
    public class ShareController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public ShareController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            // Get Tasks shared WITH this user
            var sharedTasks = await _context.SharedTasks
                .Include(st => st.WorkTask)
                .Where(st => st.SharedWithUserId == userId)
                .ToListAsync();

            // Get Events shared WITH this user
            var sharedEvents = await _context.SharedEvents
                .Include(se => se.Event)
                .Where(se => se.SharedWithUserId == userId)
                .ToListAsync();
            
            ViewBag.SharedEvents = sharedEvents;

            return View(sharedTasks);
        }

        [HttpGet]
        public async Task<IActionResult> GetMyItems()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            var tasks = await _context.WorkTasks
                .Where(t => t.UserId == userId || t.AssigneeId == userId)
                .Select(t => new { id = t.Id, title = t.Title })
                .ToListAsync();

            var events = await _context.CalendarEvents
                .Where(e => e.UserId == userId)
                .Select(e => new { id = e.Id, title = e.Subject })
                .ToListAsync();

            return Json(new { success = true, tasks, events });
        }

        public class CreateShareDto
        {
            public string ItemType { get; set; } = "";
            public int ItemId { get; set; }
            public string Email { get; set; } = "";
            public string PermissionLevel { get; set; } = "";
        }

        [HttpPost]
        public async Task<IActionResult> CreateShare([FromBody] CreateShareDto request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, message = "Not authenticated" });

            if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.ItemType) || request.ItemId <= 0)
                return BadRequest(new { success = false, message = "Invalid input" });

            var targetUser = await _userManager.FindByEmailAsync(request.Email);
            if (targetUser == null)
                return NotFound(new { success = false, message = "User with this email not found" });

            if (targetUser.Id == userId)
                return BadRequest(new { success = false, message = "Cannot share with yourself" });

            if (request.ItemType == "Task")
            {
                var task = await _context.WorkTasks.FirstOrDefaultAsync(t => t.Id == request.ItemId && (t.UserId == userId || t.AssigneeId == userId));
                if (task == null) return NotFound(new { success = false, message = "Task not found or you don't have permission" });

                var exists = await _context.SharedTasks.AnyAsync(st => st.WorkTaskId == request.ItemId && st.SharedWithUserId == targetUser.Id);
                if (exists) return BadRequest(new { success = false, message = "Already shared with this user" });

                var st = new SharedTask
                {
                    WorkTaskId = request.ItemId,
                    OwnerId = userId,
                    SharedWithUserId = targetUser.Id,
                    PermissionLevel = request.PermissionLevel ?? "View",
                    SharedDate = DateTime.Now
                };
                _context.SharedTasks.Add(st);
            }
            else if (request.ItemType == "Event")
            {
                var ev = await _context.CalendarEvents.FirstOrDefaultAsync(e => e.Id == request.ItemId && e.UserId == userId);
                if (ev == null) return NotFound(new { success = false, message = "Event not found or you don't have permission" });

                var exists = await _context.SharedEvents.AnyAsync(se => se.EventId == request.ItemId && se.SharedWithUserId == targetUser.Id);
                if (exists) return BadRequest(new { success = false, message = "Already shared with this user" });

                var se = new SharedEvent
                {
                    EventId = request.ItemId,
                    OwnerId = userId,
                    SharedWithUserId = targetUser.Id,
                    PermissionLevel = request.PermissionLevel ?? "View",
                    SharedDate = DateTime.Now
                };
                _context.SharedEvents.Add(se);
            }
            else
            {
                return BadRequest(new { success = false, message = "Invalid Item Type" });
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Shared successfully" });
        }
    }
}
