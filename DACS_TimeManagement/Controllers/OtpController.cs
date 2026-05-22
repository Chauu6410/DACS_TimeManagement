using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using DACS_TimeManagement.Services.Interfaces;
using Microsoft.Extensions.Localization;

namespace DACS_TimeManagement.Controllers
{
    public class OtpController : Controller
    {
        private readonly IOtpService _otpService;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<OtpController> _logger;
        private readonly IStringLocalizer<OtpController> _localizer;

        public OtpController(
            IOtpService otpService,
            SignInManager<IdentityUser> signInManager,
            UserManager<IdentityUser> userManager,
            ILogger<OtpController> logger,
            IStringLocalizer<OtpController> localizer)
        {
            _otpService = otpService;
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
            _localizer = localizer;
        }

        // GET: /Otp/VerifyOtp
        [HttpGet]
        public IActionResult VerifyOtp()
        {
            var userId = HttpContext.Session.GetString("2fa_userId");
            var email = HttpContext.Session.GetString("2fa_email");

            // Nếu không có session → về trang đăng nhập
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(email))
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            // Ẩn bớt email để bảo vệ quyền riêng tư (vd: us***@gmail.com)
            ViewBag.MaskedEmail = MaskEmail(email);
            return View();
        }

        // POST: /Otp/VerifyOtp
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyOtp(string otp)
        {
            var userId = HttpContext.Session.GetString("2fa_userId");
            var email = HttpContext.Session.GetString("2fa_email");
            var returnUrl = HttpContext.Session.GetString("2fa_returnUrl") ?? "/";
            var rememberMeStr = HttpContext.Session.GetString("2fa_rememberMe");
            bool rememberMe = bool.TryParse(rememberMeStr, out var rm) && rm;

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(email))
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            ViewBag.MaskedEmail = MaskEmail(email);

            if (string.IsNullOrWhiteSpace(otp) || otp.Length != 6)
            {
                ViewBag.Error = _localizer["EnterAll6Digits"].Value;
                return View();
            }

            var isValid = await _otpService.VerifyOtpAsync(userId, otp.Trim());

            if (!isValid)
            {
                ViewBag.Error = _localizer["InvalidOrExpiredOtp"].Value;
                return View();
            }

            // OTP hợp lệ → hoàn tất đăng nhập
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                ViewBag.Error = _localizer["AccountNotFound"].Value;
                return View();
            }

            await _signInManager.SignInAsync(user, rememberMe);

            // Xóa session 2FA
            HttpContext.Session.Remove("2fa_userId");
            HttpContext.Session.Remove("2fa_email");
            HttpContext.Session.Remove("2fa_returnUrl");
            HttpContext.Session.Remove("2fa_rememberMe");

            _logger.LogInformation("User {UserId} completed 2FA login successfully.", userId);
            return LocalRedirect(returnUrl);
        }

        // POST: /Otp/ResendOtp
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendOtp()
        {
            var userId = HttpContext.Session.GetString("2fa_userId");
            var email = HttpContext.Session.GetString("2fa_email");

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(email))
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            await _otpService.GenerateAndSendOtpAsync(userId, email);

            TempData["ResendSuccess"] = _localizer["OtpResentSuccess"].Value;
            return RedirectToAction(nameof(VerifyOtp));
        }

        private static string MaskEmail(string email)
        {
            if (string.IsNullOrEmpty(email)) return email;
            var parts = email.Split('@');
            if (parts.Length != 2) return email;
            var name = parts[0];
            var domain = parts[1];
            var maskedName = name.Length <= 2
                ? name
                : name[..2] + new string('*', name.Length - 2);
            return $"{maskedName}@{domain}";
        }
    }
}
