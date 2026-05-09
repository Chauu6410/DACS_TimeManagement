using DACS_TimeManagement.Models;
using DACS_TimeManagement.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DACS_TimeManagement.Services
{
    public class UserWorkScheduleService : IUserWorkScheduleService
    {
        private readonly ApplicationDbContext _context;

        public UserWorkScheduleService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<UserWorkSchedule?> GetByUserIdAsync(string userId)
        {
            return await _context.UserWorkSchedules.FirstOrDefaultAsync(u => u.UserId == userId);
        }

        public async Task<UserWorkSchedule> GetOrCreateDefaultAsync(string userId)
        {
            var schedule = await GetByUserIdAsync(userId);
            if (schedule == null)
            {
                schedule = new UserWorkSchedule
                {
                    UserId = userId,
                    DefaultStartHour = new TimeOnly(8, 0),
                    DefaultEndHour = new TimeOnly(17, 0),
                    LunchStart = new TimeOnly(12, 0),
                    LunchEnd = new TimeOnly(13, 30),
                    WorkingDays = "Monday,Tuesday,Wednesday,Thursday,Friday",
                    TimeZoneId = "SE Asia Standard Time"
                };
                _context.UserWorkSchedules.Add(schedule);
                await _context.SaveChangesAsync();
            }
            return schedule;
        }

        public async Task<bool> HasScheduleAsync(string userId)
        {
            return await _context.UserWorkSchedules.AnyAsync(u => u.UserId == userId);
        }

        public async Task UpdateAsync(UserWorkSchedule schedule)
        {
            _context.UserWorkSchedules.Update(schedule);
            await _context.SaveChangesAsync();
        }
    }
}
