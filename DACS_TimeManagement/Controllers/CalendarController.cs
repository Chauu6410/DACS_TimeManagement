using DACS_TimeManagement.Models;
using DACS_TimeManagement.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DACS_TimeManagement.Controllers
{
    [Authorize]
    public class CalendarController : Controller
    {
        private readonly ICalendarRepository _calendarRepo;

        public CalendarController(ICalendarRepository calendarRepo) => _calendarRepo = calendarRepo;

        public IActionResult Index() => View();

        [HttpGet]
        public async Task<JsonResult> GetEvents(DateTime start, DateTime end)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var events = await _calendarRepo.GetEventsInRangeAsync(userId, start, end);

            var rows = events.Select(e => new {
                id = e.Id,
                title = e.Subject,
                start = e.StartTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                end = e.EndTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                color = e.ThemeColor,
                allDay = e.IsFullDay
            });
            return Json(rows);
        }

        [HttpPost]
        public async Task<IActionResult> CreateEvent([FromBody] CalendarEvent model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            model.UserId = userId;
            await _calendarRepo.AddAsync(model);
            await _calendarRepo.SaveAsync();
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var evt = await _calendarRepo.GetByIdAsync(id, userId);
            
            if (evt != null)
            {
                _calendarRepo.Delete(evt);
                await _calendarRepo.SaveAsync();
            }
            return Ok();
        }
    }
}
