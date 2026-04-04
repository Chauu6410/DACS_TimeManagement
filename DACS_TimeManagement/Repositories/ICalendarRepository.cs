using DACS_TimeManagement.Models;

namespace DACS_TimeManagement.Repositories
{
    public interface ICalendarRepository : IRepository<CalendarEvent>
    {
        Task<IEnumerable<CalendarEvent>> GetEventsInRangeAsync(string userId, DateTime start, DateTime end);
    }
}
