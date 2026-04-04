using DACS_TimeManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DACS_TimeManagement.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public AdminController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.ToListAsync();
            var totalProjects = await _context.Projects.CountAsync();
            var totalTasks = await _context.WorkTasks.CountAsync();

            var model = new AdminViewModel
            {
                TotalUsers = users.Count,
                TotalProjects = totalProjects,
                TotalTasks = totalTasks,
                Users = users
            };

            return View(model);
        }
    }
}
