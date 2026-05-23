#nullable disable
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace DACS_TimeManagement.Areas.Identity.Pages.Account
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IEmailSender _emailSender;

        public ForgotPasswordModel(UserManager<IdentityUser> userManager, IEmailSender emailSender)
        {
            _userManager = userManager;
            _emailSender = emailSender;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Vui lòng nhập địa chỉ email.")]
            [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
            [Display(Name = "Email")]
            public string Email { get; set; }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var user = await _userManager.FindByEmailAsync(Input.Email);

            // Luôn trả về trang Confirmation dù email có tồn tại hay không (tránh lộ thông tin)
            if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
            {
                // Nếu app không require email confirmation, vẫn gửi bình thường
                if (user == null) return RedirectToPage("./ForgotPasswordConfirmation");
            }

            // Tạo reset token
            var code = await _userManager.GeneratePasswordResetTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

            var callbackUrl = Url.Page(
                "/Account/ResetPassword",
                pageHandler: null,
                values: new { area = "Identity", code },
                protocol: Request.Scheme);

            // Gửi email HTML đẹp
            var htmlBody = BuildResetPasswordEmail(Input.Email, HtmlEncoder.Default.Encode(callbackUrl));
            await _emailSender.SendEmailAsync(
                Input.Email,
                "Đặt lại mật khẩu - Time Master",
                htmlBody);

            return RedirectToPage("./ForgotPasswordConfirmation");
        }

        private static string BuildResetPasswordEmail(string email, string resetUrl)
        {
            return $@"
<!DOCTYPE html>
<html lang='vi'>
<head>
  <meta charset='UTF-8'>
  <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='margin:0;padding:0;background-color:#f1f5f9;font-family:Inter,Segoe UI,Arial,sans-serif;'>
  <table width='100%' cellpadding='0' cellspacing='0' style='background:#f1f5f9;padding:48px 0;'>
    <tr>
      <td align='center'>
        <table width='540' cellpadding='0' cellspacing='0' style='background:#ffffff;border-radius:24px;overflow:hidden;box-shadow:0 8px 32px rgba(0,0,0,0.08);'>

          <!-- Header -->
          <tr>
            <td style='background:linear-gradient(135deg,#6366f1 0%,#8b5cf6 100%);padding:44px 48px 36px;text-align:center;'>
              <div style='display:inline-block;background:rgba(255,255,255,0.18);border-radius:16px;padding:14px 20px;margin-bottom:18px;'>
                <span style='font-size:2.2rem;'>🔑</span>
              </div>
              <h1 style='color:#ffffff;margin:0;font-size:1.6rem;font-weight:800;letter-spacing:-0.5px;'>Đặt lại mật khẩu</h1>
              <p style='color:rgba(255,255,255,0.75);margin:10px 0 0;font-size:0.9rem;'>Time Master · Bảo mật tài khoản</p>
            </td>
          </tr>

          <!-- Body -->
          <tr>
            <td style='padding:44px 48px;'>
              <p style='color:#374151;font-size:1rem;margin:0 0 8px;font-weight:600;'>Xin chào,</p>
              <p style='color:#6b7280;font-size:0.95rem;margin:0 0 28px;line-height:1.7;'>
                Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản gắn với địa chỉ email
                <strong style='color:#4f46e5;'>{email}</strong>.
              </p>

              <!-- CTA Button -->
              <div style='text-align:center;margin:0 0 32px;'>
                <a href='{resetUrl}'
                   style='display:inline-block;background:linear-gradient(135deg,#6366f1,#8b5cf6);color:#ffffff;
                          text-decoration:none;padding:16px 40px;border-radius:50px;font-size:1rem;
                          font-weight:700;letter-spacing:0.3px;
                          box-shadow:0 8px 24px rgba(99,102,241,0.35);'>
                  Đặt lại mật khẩu ngay
                </a>
              </div>

              <!-- Expiry warning -->
              <div style='background:#fef3c7;border-left:4px solid #f59e0b;border-radius:10px;padding:14px 18px;margin-bottom:28px;'>
                <p style='color:#92400e;margin:0;font-size:0.875rem;line-height:1.6;'>
                  ⏰ <strong>Lưu ý:</strong> Đường dẫn này sẽ hết hạn sau <strong>24 giờ</strong>.
                  Nếu không sử dụng kịp, bạn cần yêu cầu đặt lại mật khẩu mới.
                </p>
              </div>

              <!-- Security notice -->
              <div style='background:#f0fdf4;border-left:4px solid #22c55e;border-radius:10px;padding:14px 18px;margin-bottom:28px;'>
                <p style='color:#166534;margin:0;font-size:0.875rem;line-height:1.6;'>
                  🛡️ <strong>Không phải bạn?</strong> Hãy bỏ qua email này.
                  Mật khẩu của bạn sẽ không thay đổi cho đến khi bạn nhấp vào đường dẫn trên và tạo mật khẩu mới.
                </p>
              </div>

              <!-- Fallback link -->
              <p style='color:#9ca3af;font-size:0.8rem;line-height:1.6;margin:0;'>
                Nếu nút không hoạt động, hãy sao chép và dán đường dẫn sau vào trình duyệt:<br>
                <span style='color:#6366f1;word-break:break-all;font-size:0.75rem;'>{resetUrl}</span>
              </p>
            </td>
          </tr>

          <!-- Divider -->
          <tr>
            <td style='padding:0 48px;'>
              <hr style='border:none;border-top:1px solid #e5e7eb;margin:0;'>
            </td>
          </tr>

          <!-- Footer -->
          <tr>
            <td style='padding:24px 48px;text-align:center;'>
              <p style='color:#9ca3af;font-size:0.78rem;margin:0 0 6px;'>
                Email này được gửi từ <strong>Time Master</strong> vì có yêu cầu đặt lại mật khẩu.
              </p>
              <p style='color:#c4b5fd;font-size:0.75rem;margin:0;'>
                © {DateTime.UtcNow.Year} Time Master · Tự động gửi, vui lòng không trả lời email này.
              </p>
            </td>
          </tr>

        </table>
      </td>
    </tr>
  </table>
</body>
</html>";
        }
    }
}
