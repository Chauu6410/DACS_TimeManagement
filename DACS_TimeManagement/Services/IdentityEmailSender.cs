using Microsoft.AspNetCore.Identity.UI.Services;
using DACS_TimeManagement.Services.Interfaces;

namespace DACS_TimeManagement.Services
{
    /// <summary>
    /// Adapter kết nối IEmailSender (ASP.NET Core Identity) với IEmailService của dự án.
    /// Cho phép chức năng Forgot Password, Email Confirmation của Identity gửi email thực.
    /// </summary>
    public class IdentityEmailSender : IEmailSender
    {
        private readonly IEmailService _emailService;

        public IdentityEmailSender(IEmailService emailService)
        {
            _emailService = emailService;
        }

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            return _emailService.SendEmailAsync(email, subject, htmlMessage);
        }
    }
}
