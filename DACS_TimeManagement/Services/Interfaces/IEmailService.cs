using System.Threading.Tasks;

namespace DACS_TimeManagement.Services.Interfaces
{
    public interface IEmailService
    {
        Task SendEmailAsync(string email, string subject, string htmlMessage);
    }
}
