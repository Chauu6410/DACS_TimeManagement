using DACS_TimeManagement.Models;
using DACS_TimeManagement.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;

namespace DACS_TimeManagement.Services
{
    public class EmailNotificationWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<EmailNotificationWorker> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5);

        public EmailNotificationWorker(IServiceProvider serviceProvider, ILogger<EmailNotificationWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Email Notification Worker is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndSendRemindersAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while checking for reminders.");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("Email Notification Worker is stopping.");
        }

        private async Task CheckAndSendRemindersAsync()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

                var now = DateTime.UtcNow;
                var upcomingLimit = now.AddMinutes(30); // Check for events in the next 30 minutes

                var events = await context.CalendarEvents
                    .Where(e => e.IsImportant && e.IsEmailNotification && !e.NotificationSent && e.StartTime <= upcomingLimit && e.StartTime > now)
                    .ToListAsync();

                foreach (var ev in events)
                {
                    var user = await userManager.FindByIdAsync(ev.UserId);
                    if (user != null && !string.IsNullOrEmpty(user.Email))
                    {
                        var subject = $"[Nhắc nhở] Sự kiện quan trọng: {ev.Subject}";
                        var body = $@"
                            <h2>Thông báo sự kiện sắp diễn ra</h2>
                            <p>Xin chào,</p>
                            <p>Bạn có một sự kiện quan trọng sắp diễn ra vào lúc <strong>{ev.StartTime:HH:mm dd/MM/yyyy}</strong>.</p>
                            <p><strong>Tên sự kiện:</strong> {ev.Subject}</p>
                            <p><strong>Mô tả:</strong> {ev.Description}</p>
                            <p>Hãy chuẩn bị sẵn sàng nhé!</p>
                            <br/>
                            <p>Trân trọng,<br/>DACS Time Management System</p>";

                        await emailService.SendEmailAsync(user.Email, subject, body);
                        
                        ev.NotificationSent = true;
                        _logger.LogInformation("Reminder sent for event {EventId} to {Email}", ev.Id, user.Email);
                    }
                }

                if (events.Any())
                {
                    await context.SaveChangesAsync();
                }
            }
        }
    }
}
