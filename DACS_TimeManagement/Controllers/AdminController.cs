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
        private readonly RoleManager<IdentityRole> _roleManager;

        public AdminController(ApplicationDbContext context, UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
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

        // 1. Xác thực email nhanh
        [HttpPost]
        public async Task<IActionResult> ConfirmEmail(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                user.EmailConfirmed = true;
                await _userManager.UpdateAsync(user);
            }
            return RedirectToAction(nameof(Index));
        }

        // 2. Gán quyền Admin / Gỡ quyền Admin
        [HttpPost]
        public async Task<IActionResult> ToggleAdminRole(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null || user.Email == "admin@gmail.com")
                return Json(new { success = false, message = "Cannot change the role of this user." });

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            if (isAdmin)
            {
                await _userManager.RemoveFromRoleAsync(user, "Admin");
                if (!await _userManager.IsInRoleAsync(user, "User"))
                {
                    await _userManager.AddToRoleAsync(user, "User");
                }
                return Json(new { success = true, message = $"Demoted {user.Email} to Member.", isAdmin = false });
            }
            else
            {
                await _userManager.AddToRoleAsync(user, "Admin");
                return Json(new { success = true, message = $"Promoted {user.Email} to Admin.", isAdmin = true });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ToggleLockout(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null || user.Email == "admin@gmail.com")
                return Json(new { success = false, message = "Cannot lock/unlock this user." });

            var isLocked = await _userManager.IsLockedOutAsync(user);
            if (isLocked)
            {
                await _userManager.SetLockoutEndDateAsync(user, null);
                return Json(new { success = true, message = $"Unlocked account {user.Email}.", isLocked = false });
            }
            else
            {
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
                return Json(new { success = true, message = $"Locked account {user.Email}.", isLocked = true });
            }
        }
    }
}