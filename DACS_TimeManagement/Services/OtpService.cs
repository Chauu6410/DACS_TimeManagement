using DACS_TimeManagement.Models;
using DACS_TimeManagement.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DACS_TimeManagement.Services
{
    public class OtpService : IOtpService
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<OtpService> _logger;

        private const int OtpExpiryMinutes = 10;

        public OtpService(ApplicationDbContext context, IEmailService emailService, ILogger<OtpService> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task GenerateAndSendOtpAsync(string userId, string email)
        {
            // Xóa các OTP cũ chưa dùng của user này
            var oldOtps = _context.OtpRecords
                .Where(o => o.UserId == userId && !o.IsUsed);
            _context.OtpRecords.RemoveRange(oldOtps);

            // Sinh mã 6 số ngẫu nhiên bảo mật
            var code = GenerateSecureCode();

            var otp = new OtpRecord
            {
                UserId = userId,
                Code = code,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(OtpExpiryMinutes),
                IsUsed = false
            };

            _context.OtpRecords.Add(otp);
            await _context.SaveChangesAsync();

            // Gửi email
            var subject = "Mã xác thực đăng nhập (OTP) - Time Master";
            var html = BuildOtpEmailHtml(code, OtpExpiryMinutes);

            try
            {
                await _emailService.SendEmailAsync(email, subject, html);
                _logger.LogInformation("OTP sent to {Email} for user {UserId}", email, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send OTP email to {Email} for user {UserId}", email, userId);
                throw; // Bubble up để controller biết
            }
        }

        public async Task<bool> VerifyOtpAsync(string userId, string code)
        {
            var otp = await _context.OtpRecords
                .Where(o => o.UserId == userId
                         && o.Code == code
                         && !o.IsUsed
                         && o.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();

            if (otp == null)
            {
                _logger.LogWarning("Invalid or expired OTP attempt for user {UserId}", userId);
                return false;
            }

            // Đánh dấu đã dùng
            otp.IsUsed = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation("OTP verified successfully for user {UserId}", userId);
            return true;
        }

        private static string GenerateSecureCode()
        {
            // Dùng Random.Shared với bảo mật cao hơn
            var random = System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, 1000000);
            return random.ToString("D6"); // Đảm bảo luôn đủ 6 chữ số (vd: 000123)
        }

        private static string BuildOtpEmailHtml(string code, int expiryMinutes)
        {
            return $@"
<!DOCTYPE html>
<html lang='vi'>
<head>
  <meta charset='UTF-8'>
  <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='margin:0;padding:0;background-color:#f8fafc;font-family:Inter,Segoe UI,Arial,sans-serif;'>
  <table width='100%' cellpadding='0' cellspacing='0' style='background:#f8fafc;padding:40px 0;'>
    <tr>
      <td align='center'>
        <table width='520' cellpadding='0' cellspacing='0' style='background:#ffffff;border-radius:20px;overflow:hidden;box-shadow:0 4px 24px rgba(0,0,0,0.08);'>
          <!-- Header -->
          <tr>
            <td style='background:linear-gradient(135deg,#6366f1,#8b5cf6);padding:36px 40px;text-align:center;'>
              <div style='font-size:2rem;margin-bottom:8px;'>🔐</div>
              <h1 style='color:#ffffff;margin:0;font-size:1.5rem;font-weight:700;letter-spacing:-0.5px;'>Xác thực hai yếu tố</h1>
              <p style='color:rgba(255,255,255,0.8);margin:8px 0 0;font-size:0.9rem;'>Time Master Security</p>
            </td>
          </tr>
          <!-- Body -->
          <tr>
            <td style='padding:40px;'>
              <p style='color:#374151;font-size:1rem;margin:0 0 24px;line-height:1.6;'>
                Xin chào! Bạn vừa yêu cầu đăng nhập vào <strong>Time Master</strong>.<br>
                Sử dụng mã OTP bên dưới để hoàn tất xác thực:
              </p>

              <!-- OTP Code Box -->
              <div style='background:linear-gradient(135deg,#eef2ff,#f5f3ff);border:2px dashed #a5b4fc;border-radius:16px;padding:32px;text-align:center;margin:0 0 28px;'>
                <div style='letter-spacing:16px;font-size:2.5rem;font-weight:800;color:#4f46e5;font-family:monospace;'>{code}</div>
                <p style='color:#6b7280;font-size:0.82rem;margin:12px 0 0;'>Mã có hiệu lực trong <strong>{expiryMinutes} phút</strong></p>
              </div>

              <!-- Warning -->
              <div style='background:#fff7ed;border-left:4px solid #f97316;border-radius:8px;padding:14px 18px;margin-bottom:28px;'>
                <p style='color:#9a3412;margin:0;font-size:0.875rem;line-height:1.5;'>
                  ⚠️ <strong>Lưu ý bảo mật:</strong> Không chia sẻ mã này với bất kỳ ai.
                  Đội ngũ Time Master sẽ không bao giờ yêu cầu mã OTP của bạn.
                </p>
              </div>

              <p style='color:#9ca3af;font-size:0.82rem;line-height:1.6;margin:0;'>
                Nếu bạn không thực hiện đăng nhập này, hãy bỏ qua email này và kiểm tra lại bảo mật tài khoản của bạn.
              </p>
            </td>
          </tr>
          <!-- Footer -->
          <tr>
            <td style='background:#f9fafb;padding:20px 40px;text-align:center;border-top:1px solid #e5e7eb;'>
              <p style='color:#9ca3af;font-size:0.78rem;margin:0;'>© {DateTime.UtcNow.Year} Time Master · Email này được gửi tự động, vui lòng không trả lời.</p>
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
