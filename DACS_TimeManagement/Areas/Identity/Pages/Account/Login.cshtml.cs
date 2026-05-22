// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using DACS_TimeManagement.Services.Interfaces;
using DACS_TimeManagement.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace DACS_TimeManagement.Areas.Identity.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IOtpService _otpService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<LoginModel> _logger;
        private readonly IStringLocalizer _localizer;

        public LoginModel(
            SignInManager<IdentityUser> signInManager,
            UserManager<IdentityUser> userManager,
            IOtpService otpService,
            ApplicationDbContext context,
            ILogger<LoginModel> logger,
            IStringLocalizerFactory localizerFactory)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _otpService = otpService;
            _context = context;
            _logger = logger;
            _localizer = localizerFactory.Create("Areas.Identity.Pages.Account.Login", typeof(LoginModel).Assembly.GetName().Name);
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string ReturnUrl { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [TempData]
        public string ErrorMessage { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public class InputModel
        {
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl ??= Url.Content("~/");

            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (ModelState.IsValid)
            {
                // This doesn't count login failures towards account lockout
                // To enable password failures to trigger account lockout, set lockoutOnFailure: true
                var result = await _signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    // Kiểm tra xem user có bật 2FA không
                    var user = await _userManager.FindByEmailAsync(Input.Email);
                    var profile = user != null
                        ? await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id)
                        : null;

                    if (profile != null && profile.TwoFactorEnabled)
                    {
                        // Đăng xuất session vừa tạo, chờ xác thực OTP
                        await _signInManager.SignOutAsync();

                        // Lưu thông tin vào Session để dùng sau khi nhập OTP
                        HttpContext.Session.SetString("2fa_userId", user.Id);
                        HttpContext.Session.SetString("2fa_email", Input.Email);
                        HttpContext.Session.SetString("2fa_returnUrl", returnUrl);
                        HttpContext.Session.SetString("2fa_rememberMe", Input.RememberMe.ToString());

                        // Gửi OTP qua email
                        try
                        {
                            await _otpService.GenerateAndSendOtpAsync(user.Id, Input.Email);
                            _logger.LogInformation("2FA OTP sent to {Email}", Input.Email);
                            return RedirectToAction("VerifyOtp", "Otp");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to send OTP to {Email}", Input.Email);
                            // Đăng nhập luôn nếu không gửi được email (tránh bị khóa)
                            await _signInManager.SignInAsync(user, Input.RememberMe);
                            ModelState.AddModelError(string.Empty,
                                _localizer["Otp Send Failed Message"].Value);
                            return Page();
                        }
                    }

                    _logger.LogInformation("User logged in.");
                    return LocalRedirect(returnUrl);
                }
                if (result.RequiresTwoFactor)
                {
                    return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
                }
                if (result.IsLockedOut)
                {
                    _logger.LogWarning("User account locked out.");
                    return RedirectToPage("./Lockout");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, _localizer["Invalid login attempt."].Value);
                    return Page();
                }
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }
    }
}
