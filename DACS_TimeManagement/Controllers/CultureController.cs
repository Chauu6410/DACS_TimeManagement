using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using DACS_TimeManagement.Models;

namespace DACS_TimeManagement.Controllers
{
    public class CultureController : Controller
    {
        private readonly ApplicationDbContext _db;

        public CultureController(ApplicationDbContext db)
        {
            _db = db;
        }

        // GET /setlanguage?lang=vi&returnUrl=/Project/Details/1
        [HttpGet("/setlanguage")]
        public async System.Threading.Tasks.Task<IActionResult> SetLanguage(string lang, string? returnUrl)
        {
            var culture = string.Equals(lang, "vi", StringComparison.OrdinalIgnoreCase) ? "vi-VN" : "en-US";

            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), HttpOnly = false }
            );

            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userId))
                {
                    var profile = await _db.UserProfiles.FirstOrDefaultAsync(u => u.UserId == userId);
                    if (profile != null)
                    {
                        profile.Language = lang;
                        _db.UserProfiles.Update(profile);
                        await _db.SaveChangesAsync();
                    }
                }
            }

            if (string.IsNullOrEmpty(returnUrl)) returnUrl = "/";

            return LocalRedirect(returnUrl);
        }
    }
}
