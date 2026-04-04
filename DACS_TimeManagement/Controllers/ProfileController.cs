using DACS_TimeManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DACS_TimeManagement.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProfileController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login", "Account", new { area = "Identity" });

            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            
            if (profile == null)
            {
                // Create a default profile if it doesn't exist
                profile = new UserProfile 
                { 
                    UserId = userId,
                    Email = User.FindFirstValue(ClaimTypes.Email)
                };
                _context.UserProfiles.Add(profile);
                await _context.SaveChangesAsync();
            }

            return View(profile);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(UserProfile model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            if (ModelState.IsValid)
            {
                var existingProfile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
                
                if (existingProfile != null)
                {
                    existingProfile.FullName = model.FullName;
                    existingProfile.PhoneNumber = model.PhoneNumber;
                    existingProfile.Department = model.Department;
                    existingProfile.Position = model.Position;
                    existingProfile.Theme = model.Theme;
                    existingProfile.DefaultView = model.DefaultView;
                    existingProfile.EmailNotifications = model.EmailNotifications;
                    existingProfile.PushNotifications = model.PushNotifications;
                    existingProfile.WorkStartTime = model.WorkStartTime;
                    existingProfile.WorkEndTime = model.WorkEndTime;

                    _context.UserProfiles.Update(existingProfile);
                    await _context.SaveChangesAsync();
                    
                    TempData["SuccessMessage"] = "Profile successfully updated!";
                    return RedirectToAction(nameof(Index));
                }
            }

            return View("Index", model);
        }
    }
}
