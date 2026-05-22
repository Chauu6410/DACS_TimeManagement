using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using DACS_TimeManagement.Models;
using Microsoft.EntityFrameworkCore;
using DACS_TimeManagement.Repositories;
using Microsoft.Extensions.Localization;

namespace DACS_TimeManagement.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationRepository _notifRepo;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IStringLocalizer<AccountController> _localizer;

        public AccountController(ApplicationDbContext context, INotificationRepository notifRepo, IWebHostEnvironment webHostEnvironment, IStringLocalizer<AccountController> localizer)
        {
            _context = context;
            _notifRepo = notifRepo;
            _webHostEnvironment = webHostEnvironment;
            _localizer = localizer;
        }

        // --- Settings Page ---
        public IActionResult Settings()
        {
            return RedirectToAction(nameof(Profile));
        }

        // --- Activity Log Page ---
        public async Task<IActionResult> Activity()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            // Fetch recent task history for the current user
            var activities = await _context.TaskHistories
                .Include(h => h.WorkTask)
                .Where(h => h.ChangedByUserId == userId)
                .OrderByDescending(h => h.ChangedAt)
                .Take(50)
                .ToListAsync();

            return View(activities);
        }


        [HttpPost]
        public IActionResult ChangePassword()
        {
            // Redirect to Identity Manage ChangePassword page
            return RedirectToPage("/Account/Manage/ChangePassword", new { area = "Identity" });
        }

        // --- Notifications Page ---
        public async Task<IActionResult> Notifications(int page = 1, int pageSize = 10)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var notifications = await _notifRepo.GetPagedAsync(userId, page, pageSize);
            int totalCount = await _notifRepo.CountAsync(userId);
            int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            return View(notifications);
        }

        // --- Profile Page (Modernized) ---
        public async Task<IActionResult> Profile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            
            if (profile == null)
            {
                profile = new UserProfile 
                { 
                    UserId = userId,
                    Email = User.FindFirstValue(ClaimTypes.Email),
                    JoinDate = DateTime.Now
                };
                _context.UserProfiles.Add(profile);
                await _context.SaveChangesAsync();
            }

            return View(profile);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(UserProfile model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var existingProfile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            
            if (existingProfile != null)
            {
                existingProfile.FullName = model.FullName;
                existingProfile.PhoneNumber = model.PhoneNumber;
                existingProfile.Department = model.Department;
                existingProfile.Position = model.Position;
                
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = _localizer["ProfileUpdated"].Value;
            }

            return RedirectToAction(nameof(Profile));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePreferences(UserProfile model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var existingProfile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            
            if (existingProfile != null)
            {
                existingProfile.Theme = model.Theme;
                existingProfile.DefaultView = model.DefaultView;
                existingProfile.EmailNotifications = model.EmailNotifications;
                existingProfile.PushNotifications = model.PushNotifications;
                existingProfile.WorkStartTime = model.WorkStartTime;
                existingProfile.WorkEndTime = model.WorkEndTime;
                existingProfile.Language = model.Language;
                
                await _context.SaveChangesAsync();

                // Set Culture Cookie for Localization
                var culture = model.Language == "vi" ? "vi-VN" : "en-US";
                Response.Cookies.Append(
                    Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.DefaultCookieName,
                    Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.MakeCookieValue(new Microsoft.AspNetCore.Localization.RequestCulture(culture)),
                    new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
                );

                TempData["SuccessMessage"] = _localizer["PreferencesSaved"].Value;
            }

            return RedirectToAction(nameof(Profile));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadAvatar(IFormFile avatarFile)
        {
            if (avatarFile == null || avatarFile.Length == 0)
            {
                TempData["ErrorMessage"] = _localizer["InvalidImageFile"].Value;
                return RedirectToAction(nameof(Profile));
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null) return NotFound();

            var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "avatars");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{userId}_{DateTime.Now.Ticks}{Path.GetExtension(avatarFile.FileName)}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await avatarFile.CopyToAsync(fileStream);
            }

            profile.AvatarUrl = $"/uploads/avatars/{fileName}";
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = _localizer["AvatarUpdated"].Value;
            return RedirectToAction(nameof(Profile));
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle2FA()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null) return NotFound();

            profile.TwoFactorEnabled = !profile.TwoFactorEnabled;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = profile.TwoFactorEnabled
                ? _localizer["TwoFactorEnabled"].Value
                : _localizer["TwoFactorDisabled"].Value;

            // Redirect về tab Security
            return RedirectToAction(nameof(Profile), null, "security");
        }
    }
}
