using DACS_TimeManagement.Models;
using DACS_TimeManagement.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Security.Claims;

namespace DACS_TimeManagement.Controllers
{
    //[Authorize]
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

        // 1. Trang giới thiệu(Landing Page) - Cho phép mọi người truy cập
        [AllowAnonymous]
        public IActionResult Index()
        {
            // Nếu người dùng đã đăng nhập rồi thì chuyển thẳng vào Dashboard luôn cho tiện
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Dashboard");
            }
            return View();
        }

        public async Task<IActionResult> Dashboard()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Redirect("/Identity/Account/Login");
            }

            // 1. Core Task Metrics
            var tasks = await _taskRepo.GetAllAsync(userId);
            var today = DateTime.Today;

            var goals = await _context.PersonalGoals.Where(g => g.UserId == userId).ToListAsync();

            var model = new DashboardViewModel
            {
                TotalTasks = tasks.Count(),
                CompletedTasks = tasks.Count(t => t.Status == DACS_TimeManagement.Models.TaskStatus.Completed),
                InProgressTasks = tasks.Count(t => t.Status == DACS_TimeManagement.Models.TaskStatus.InProgress),
                AllTasks = tasks.ToList(),
                TotalGoals = goals.Count,
                CompletedGoals = goals.Count(g => g.Status == DACS_TimeManagement.Models.GoalStatus.Completed),
                OverallGoalProgress = goals.Count == 0 ? 0 : (goals.Count(g => g.Status == DACS_TimeManagement.Models.GoalStatus.Completed) * 100 / goals.Count)
            };

            // 2. Schedule Data
            var todayEvents = await _calendarRepo.GetEventsInRangeAsync(userId, today, today.AddDays(1).AddTicks(-1));
            model.TodayEvents = todayEvents.Select(e => new DashboardEventDto {
                Title = e.Subject,
                StartTime = e.StartTime,
                Description = e.Description ?? string.Empty,
                Status = "Scheduled"
            }).ToList();

            model.RecentTasks = tasks.OrderBy(t => t.EndDate).Take(5).Select(t => new DashboardTaskDto {
                Id = t.Id,
                Title = t.Title,
                IsCompleted = t.Status == DACS_TimeManagement.Models.TaskStatus.Completed,
                Priority = t.Priority.ToString(),
                DueDate = t.EndDate
            }).ToList();

            // 3. Time & Analytics
            var last7Days = Enumerable.Range(0, 7).Select(i => today.AddDays(-6 + i)).ToList();
            var recentLogs = await _context.TimeLogs
                .Include(t => t.WorkTask)
                .Where(t => t.WorkTask.UserId == userId && t.LogDate >= today.AddDays(-6) && t.LogDate <= today.AddDays(1))
                .ToListAsync();

            model.HoursWorked = recentLogs.Sum(l => l.DurationHours);
            for (int i = 0; i < 7; i++)
            {
                var date = last7Days[i].Date;
                model.WeeklyHours[i] = recentLogs.Where(l => l.LogDate.Date == date).Sum(l => l.DurationHours);
                model.WeeklyTasks[i] = tasks.Count(t => t.EndDate.Date == date);
            }

            return View(model);
        }


        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
