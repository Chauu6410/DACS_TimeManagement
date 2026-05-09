using DACS_TimeManagement.Models;

namespace DACS_TimeManagement.Services.Interfaces
{
    public interface IUserWorkScheduleService
    {
        Task<UserWorkSchedule?> GetByUserIdAsync(string userId);
        Task<UserWorkSchedule> GetOrCreateDefaultAsync(string userId);
        Task UpdateAsync(UserWorkSchedule schedule);
        Task<bool> HasScheduleAsync(string userId);
    }
}
