using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Http;

namespace DACS_TimeManagement.Controllers
{
    public class CultureController : Controller
    {
        // GET /setlanguage?lang=vi&returnUrl=/Project/Details/1
        [HttpGet("/setlanguage")]
        public IActionResult SetLanguage(string lang, string? returnUrl)
        {
            var culture = string.Equals(lang, "vi", StringComparison.OrdinalIgnoreCase) ? "vi-VN" : "en-US";

            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), HttpOnly = false }
            );

            if (string.IsNullOrEmpty(returnUrl)) returnUrl = "/";

            return LocalRedirect(returnUrl);
        }
    }
}
