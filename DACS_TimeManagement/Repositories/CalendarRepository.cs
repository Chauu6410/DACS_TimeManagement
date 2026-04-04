using DACS_TimeManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace DACS_TimeManagement.Repositories
{
    public class CalendarRepository : Repository<CalendarEvent>, ICalendarRepository
    {
        public CalendarRepository(ApplicationDbContext context) : base(context) { }

        public async Task<IEnumerable<CalendarEvent>> GetEventsInRangeAsync(string userId, DateTime start, DateTime end)
        {
            return await _context.CalendarEvents
                .Where(e => e.UserId == userId
                         && e.StartTime >= start
                         && e.EndTime <= end)
                .OrderBy(e => e.StartTime)
                .ToListAsync();
        }
    }
}
