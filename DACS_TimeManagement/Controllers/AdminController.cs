using DACS_TimeManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.Localization;
 
namespace DACS_TimeManagement.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IStringLocalizer<AdminController> _localizer;
 
        public AdminController(ApplicationDbContext context, UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager, IStringLocalizer<AdminController> localizer)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _localizer = localizer;
        }

        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.ToListAsync();
            var totalProjects = await _context.Projects.CountAsync();
            var totalTasks = await _context.WorkTasks.CountAsync();

            var userDetails = new List<UserDetailViewModel>();
            foreach (var user in users)
            {
                userDetails.Add(new UserDetailViewModel
                {
                    User = user,
                    IsAdmin = await _userManager.IsInRoleAsync(user, "Admin"),
                    IsLocked = await _userManager.IsLockedOutAsync(user),
                    LastAccess = null // Hoặc lấy từ bảng log nếu có
                });
            }

            var model = new AdminViewModel
            {
                TotalUsers = users.Count,
                TotalProjects = totalProjects,
                TotalTasks = totalTasks,
                Users = users,
                UserDetails = userDetails
            };

            return View(model);
        }

        // 1. Xác thực email nhanh
        [HttpPost]
        public async Task<IActionResult> ConfirmEmail(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return Json(new { success = false, message = _localizer["UserNotFound"].Value });

            user.EmailConfirmed = true;
            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
                return Json(new { success = true, message = string.Format(_localizer["EmailConfirmedFor"].Value, user.Email) });

            return Json(new { success = false, message = _localizer["FailedToConfirmEmail"].Value });
        }

        // 2. Gán quyền Admin / Gỡ quyền Admin
        [HttpPost]
        public async Task<IActionResult> ToggleAdminRole(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null || user.Email == "admin@gmail.com")
                return Json(new { success = false, message = _localizer["CannotChangeRole"].Value });

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            if (isAdmin)
            {
                await _userManager.RemoveFromRoleAsync(user, "Admin");
                if (!await _userManager.IsInRoleAsync(user, "User"))
                {
                    await _userManager.AddToRoleAsync(user, "User");
                }
                return Json(new { success = true, message = string.Format(_localizer["DemotedUser"].Value, user.Email), isAdmin = false });
            }
            else
            {
                await _userManager.AddToRoleAsync(user, "Admin");
                return Json(new { success = true, message = string.Format(_localizer["PromotedUser"].Value, user.Email), isAdmin = true });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ToggleLockout(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null || user.Email == "admin@gmail.com")
                return Json(new { success = false, message = _localizer["CannotLockUser"].Value });

            var isLocked = await _userManager.IsLockedOutAsync(user);
            if (isLocked)
            {
                await _userManager.SetLockoutEndDateAsync(user, null);
                return Json(new { success = true, message = string.Format(_localizer["UnlockedUser"].Value, user.Email), isLocked = false });
            }
            else
            {
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
                return Json(new { success = true, message = string.Format(_localizer["LockedUser"].Value, user.Email), isLocked = true });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null || user.Email == "admin@gmail.com")
                return Json(new { success = false, message = _localizer["UserNotFoundOrProtected"].Value });

            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
                return Json(new { success = true, message = string.Format(_localizer["UserDeleted"].Value, user.Email) });

            return Json(new { success = false, message = _localizer["FailedToDeleteUser"].Value });
        }

        [HttpPost]
        public async Task<IActionResult> BulkDelete([FromBody] List<string> userIds)
        {
            if (userIds == null || !userIds.Any()) return Json(new { success = false, message = _localizer["NoUsersSelected"].Value });

            int count = 0;
            foreach (var id in userIds)
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user != null && user.Email != "admin@gmail.com")
                {
                    var result = await _userManager.DeleteAsync(user);
                    if (result.Succeeded) count++;
                }
            }
            return Json(new { success = true, message = string.Format(_localizer["BulkDeleted"].Value, count) });
        }

        [HttpPost]
        public async Task<IActionResult> BulkToggleAdmin([FromBody] List<string> userIds)
        {
            if (userIds == null || !userIds.Any()) return Json(new { success = false, message = _localizer["NoUsersSelected"].Value });

            int promoted = 0;
            int demoted = 0;
            foreach (var id in userIds)
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user != null && user.Email != "admin@gmail.com")
                {
                    var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
                    if (isAdmin)
                    {
                        await _userManager.RemoveFromRoleAsync(user, "Admin");
                        demoted++;
                    }
                    else
                    {
                        await _userManager.AddToRoleAsync(user, "Admin");
                        promoted++;
                    }
                }
            }
            return Json(new { success = true, message = string.Format(_localizer["BulkAdminToggled"].Value, promoted, demoted) });
        }

        [HttpPost]
        public async Task<IActionResult> BulkToggleLockout([FromBody] List<string> userIds)
        {
            if (userIds == null || !userIds.Any()) return Json(new { success = false, message = _localizer["NoUsersSelected"].Value });

            int locked = 0;
            int unlocked = 0;
            foreach (var id in userIds)
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user != null && user.Email != "admin@gmail.com")
                {
                    var isLocked = await _userManager.IsLockedOutAsync(user);
                    if (isLocked)
                    {
                        await _userManager.SetLockoutEndDateAsync(user, null);
                        unlocked++;
                    }
                    else
                    {
                        await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
                        locked++;
                    }
                }
            }
            return Json(new { success = true, message = string.Format(_localizer["BulkLockToggled"].Value, locked, unlocked) });
        }

        [HttpPost]
        public async Task<IActionResult> BulkConfirmEmail([FromBody] List<string> userIds)
        {
            if (userIds == null || !userIds.Any()) return Json(new { success = false, message = _localizer["NoUsersSelected"].Value });

            int count = 0;
            foreach (var id in userIds)
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user != null && !user.EmailConfirmed)
                {
                    user.EmailConfirmed = true;
                    var result = await _userManager.UpdateAsync(user);
                    if (result.Succeeded) count++;
                }
            }
            return Json(new { success = true, message = string.Format(_localizer["EmailsConfirmed"].Value, count) });
        }
    }
}