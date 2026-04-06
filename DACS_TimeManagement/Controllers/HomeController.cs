using DACS_TimeManagement.Models;
using DACS_TimeManagement.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Security.Claims;

namespace DACS_TimeManagement.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IWorkTaskRepository _taskRepo;
        private readonly ICalendarRepository _calendarRepo;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, IWorkTaskRepository taskRepo, ICalendarRepository calendarRepo, ApplicationDbContext context)
        {
            _logger = logger;
            _taskRepo = taskRepo;
            _calendarRepo = calendarRepo;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Redirect("/Identity/Account/Login");
            }

            // 1. Core Task Metrics
            var tasks = await _taskRepo.GetAllAsync(userId);
            ViewBag.TotalTasks = tasks.Count();
            ViewBag.CompletedTasks = tasks.Count(t => t.Status == DACS_TimeManagement.Models.TaskStatus.Completed);
            ViewBag.InProgressTasks = tasks.Count(t => t.Status == DACS_TimeManagement.Models.TaskStatus.InProgress);
            
            // 2. Schedule Data
            var today = DateTime.Today;
            var todayEvents = await _calendarRepo.GetEventsInRangeAsync(userId, today, today.AddDays(1).AddTicks(-1));
            ViewBag.TodayEvents = todayEvents.Select(e => new {
                Title = e.Subject,
                StartTime = e.StartTime,
                Description = e.Description ?? "No description",
                Status = "Scheduled"
            }).ToList();

            ViewBag.RecentTasks = tasks.OrderBy(t => t.EndDate).Take(5).Select(t => new {
                Id = t.Id,
                Title = t.Title,
                IsCompleted = t.Status == DACS_TimeManagement.Models.TaskStatus.Completed,
                Priority = t.Priority.ToString(),
                DueDate = t.EndDate
            }).ToList();

            // 3. Authentic Time & Analytics Data (Requirement 8)
            var last7Days = Enumerable.Range(0, 7).Select(i => today.AddDays(-6 + i)).ToList();
            var weeklyHours = new double[7];
            var weeklyTasks = new int[7];

            // Get all time logs for user in last 7 days
            var recentLogs = await _context.TimeLogs
                .Include(t => t.WorkTask)
                .Where(t => t.WorkTask.UserId == userId && t.LogDate >= today.AddDays(-6) && t.LogDate <= today.AddDays(1))
                .ToListAsync();

            var totalHours = recentLogs.Sum(l => l.DurationHours);
            ViewBag.HoursWorked = totalHours;

            // Fill weekly hours array
            for (int i = 0; i < 7; i++)
            {
                var date = last7Days[i].Date;
                weeklyHours[i] = recentLogs.Where(l => l.LogDate.Date == date).Sum(l => l.DurationHours);
                
                // For tasks, let's track tasks that were actually completed on those days.
                // Or simply tasks they have due on those days
                weeklyTasks[i] = tasks.Count(t => t.EndDate.Date == date);
            }

            ViewBag.WeeklyHours = weeklyHours;
            ViewBag.WeeklyTasks = weeklyTasks;

            return View();
        }


        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
