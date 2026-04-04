using DACS_TimeManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DACS_TimeManagement.Controllers
{
    [Authorize]
    public class ShareController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ShareController(ApplicationDbContext context)
        {
            _context = context;
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
    }
}
