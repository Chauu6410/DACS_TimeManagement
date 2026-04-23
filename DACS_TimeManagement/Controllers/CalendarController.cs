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
        private readonly ApplicationDbContext _context;

        public CalendarController(ICalendarRepository calendarRepo, ApplicationDbContext context)
        {
            _calendarRepo = calendarRepo;
            _context = context;
        }

        // GET: Calendar/Index
        public async Task<IActionResult> Index()
        {
            ViewBag.CurrentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            return View();
        }

        // GET: Calendar/GetScheduledEvents - Chỉ lấy dữ liệu từ ScheduledEvents
        [HttpGet]
        public async Task<JsonResult> GetScheduledEvents(DateTime start, DateTime end)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

            var events = await _context.ScheduledEvents
                .Include(se => se.Task)!.ThenInclude(t => t.Project)
                .Where(se => se.Task != null && (se.Task.UserId == userId || se.Task.AssigneeId == userId)
                             && ((se.StartTime >= start && se.StartTime <= end) || (se.EndTime >= start && se.EndTime <= end)))
                .OrderBy(se => se.StartTime)
                .ToListAsync();

            var list = events.Select(se => new
            {
                id = se.Id,
                title = (se.Task?.Project != null ? se.Task.Project.Name + " - " : "") + (se.Task?.Title ?? "Scheduled Task"),
                start = se.StartTime.ToString("o"),
                end = se.EndTime.ToString("o"),
                description = se.Task?.Description ?? string.Empty,
                backgroundColor = se.Color ?? "#818cf8",
                borderColor = "transparent",
                textColor = "#1e293b",
                extendedProps = new
                {
                    taskId = se.TaskId,
                    projectName = se.Task?.Project?.Name ?? string.Empty,
                    ownerId = se.Task?.UserId ?? string.Empty
                }
            }).ToList();

            return Json(list);
        }

        // GET: Calendar/GetMyTasks - Lấy danh sách Task của user
        [HttpGet]
        public async Task<IActionResult> GetMyTasks()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            var tasks = await _context.WorkTasks
                .Include(t => t.Project)
                .Where(t => t.UserId == userId)
                .Select(t => new
                {
                    id = t.Id,
                    title = t.Title ?? "Unnamed Task",
                    projectName = t.Project != null ? t.Project.Name : "No Project",
                    description = t.Description ?? ""
                })
                .OrderBy(t => t.projectName)
                .ThenBy(t => t.title)
                .ToListAsync();

            return Ok(tasks);
        }

        // GET: Calendar/GetAllTasks - Lấy tất cả task cho Auto Plan (có phân biệt đã scheduled hay chưa)
        [HttpGet]
        public async Task<IActionResult> GetAllTasks()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            // Lấy danh sách task đã được scheduled cho ngày mai
            var scheduledTaskIds = await _context.ScheduledEvents
                .Where(se => se.StartTime.Date == tomorrow)
                .Select(se => se.TaskId)
                .ToListAsync();

            var tasks = await _context.WorkTasks
                .Include(t => t.Project)
                .Where(t => t.UserId == userId && !scheduledTaskIds.Contains(t.Id))
                .Select(t => new
                {
                    id = t.Id,
                    title = t.Title ?? "Unnamed Task",
                    projectName = t.Project != null ? t.Project.Name : "No Project",
                    description = t.Description ?? "",
                    deadline = t.EndDate.ToString("yyyy-MM-dd"),
                    isOverdue = t.EndDate < today
                })
                .OrderBy(t => t.deadline)
                .ToListAsync();

            return Ok(tasks);
        }

        // GET: Calendar/GetScheduledTasksByDate - Lấy scheduled tasks theo ngày cho Todo List
        [HttpGet]
        public async Task<IActionResult> GetScheduledTasksByDate(DateTime date)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            var startOfDay = date.Date;
            var endOfDay = date.Date.AddDays(1);

            var scheduledEvents = await _context.ScheduledEvents
                .Include(se => se.Task)!.ThenInclude(t => t.Project)
                .Where(se => se.Task != null && (se.Task.UserId == userId || se.Task.AssigneeId == userId)
                             && se.StartTime >= startOfDay && se.StartTime < endOfDay)
                .OrderBy(se => se.StartTime)
                .Select(se => new
                {
                    id = se.Id,
                    title = (se.Task!.Project != null ? se.Task.Project.Name + " - " : "") + (se.Task.Title ?? "Unnamed Task"),
                    start = se.StartTime.ToString("HH:mm"),
                    end = se.EndTime.ToString("HH:mm"),
                    color = se.Color ?? "#818cf8"
                })
                .ToListAsync();

            return Ok(scheduledEvents);
        }

        // POST: Calendar/CreateScheduledEvent - Tạo scheduled event từ Task
        [HttpPost]
        public async Task<IActionResult> CreateScheduledEvent([FromBody] ScheduledEventDto request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            try
            {

                // validate task exists and user owns or assigned
                var task = await _context.WorkTasks
                    .Include(t => t.Project)
                    .FirstOrDefaultAsync(t => t.Id == request.TaskId && (t.UserId == userId || t.AssigneeId == userId));

                if (task == null)
                    return BadRequest(new { success = false, message = "Task not found or not allowed" });

                var se = new ScheduledEvent
                {
                    TaskId = request.TaskId,
                    StartTime = request.StartTime,
                    EndTime = request.EndTime,
                    Color = string.IsNullOrEmpty(request.Color) ? GetRandomPastelColor() : request.Color
                };

                _context.ScheduledEvents.Add(se);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, eventId = se.Id });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

// DTO để binding từ client
public class ScheduledEventDto
{
    public int TaskId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string? Color { get; set; }
}

        // POST: Calendar/DeleteScheduledEvent
        [HttpPost]
        public async Task<IActionResult> DeleteScheduledEvent(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            var se = await _context.ScheduledEvents.Include(s => s.Task).FirstOrDefaultAsync(s => s.Id == id);
            if (se == null)
                return Json(new { success = false, message = "Event not found" });

            // permission: only task owner or assignee can delete
            if (se.Task == null || !(se.Task.UserId == userId || se.Task.AssigneeId == userId))
                return Forbid();

            _context.ScheduledEvents.Remove(se);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        // POST: Calendar/AutoPlanTasks - Auto plan lấy task từ database
        [HttpPost]
        public async Task<IActionResult> AutoPlanTasks([FromBody] AutoPlan request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            if (request.Tasks == null || !request.Tasks.Any())
                return BadRequest(new { success = false, message = "No tasks selected" });

            try
            {
                var targetDate = request.TargetDate ?? DateTime.Today.AddDays(1);
                var currentTime = new DateTime(targetDate.Year, targetDate.Month, targetDate.Day, 8, 0, 0);

                // Sắp xếp: Hard làm buổi sáng, Medium/Easy buổi chiều
                var morningTasks = request.Tasks.Where(t => t.Difficulty == "Hard").ToList();
                var afternoonTasks = request.Tasks.Where(t => t.Difficulty != "Hard").ToList();

                var scheduledEvents = new List<ScheduledEvent>();

                // Xử lý tasks buổi sáng
                foreach (var t in morningTasks)
                {
                    // Nghỉ trưa từ 12:00 đến 13:30
                    if (currentTime.TimeOfDay >= new TimeSpan(12, 0, 0) && currentTime.TimeOfDay < new TimeSpan(13, 30, 0))
                    {
                        currentTime = new DateTime(targetDate.Year, targetDate.Month, targetDate.Day, 13, 30, 0);
                    }

                    if (currentTime.TimeOfDay >= new TimeSpan(21, 0, 0)) break;

                    var endTime = currentTime.AddMinutes(t.DurationMinutes);

                    var se = new ScheduledEvent
                    {
                        TaskId = t.TaskId,
                        StartTime = currentTime,
                        EndTime = endTime,
                        Color = "#ef4444" // Màu đỏ cho Hard
                    };

                    scheduledEvents.Add(se);
                    currentTime = endTime.AddMinutes(5);
                }

                // Reset time cho buổi chiều
                currentTime = new DateTime(targetDate.Year, targetDate.Month, targetDate.Day, 13, 30, 0);

                // Xử lý tasks buổi chiều
                foreach (var t in afternoonTasks)
                {
                    if (currentTime.TimeOfDay >= new TimeSpan(21, 0, 0)) break;

                    var endTime = currentTime.AddMinutes(t.DurationMinutes);
                    var color = t.Difficulty == "Medium" ? "#f59e0b" : "#10b981";

                    var se = new ScheduledEvent
                    {
                        TaskId = t.TaskId,
                        StartTime = currentTime,
                        EndTime = endTime,
                        Color = color
                    };

                    scheduledEvents.Add(se);
                    currentTime = endTime.AddMinutes(5);
                }

                if (scheduledEvents.Any())
                {
                    _context.ScheduledEvents.AddRange(scheduledEvents);
                    await _context.SaveChangesAsync();
                }

                return Ok(new { success = true, message = $"Scheduled {scheduledEvents.Count} tasks for {targetDate:dd/MM/yyyy}", count = scheduledEvents.Count });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        private string GetRandomPastelColor()
        {
            var colors = new[] { "#818cf8", "#c084fc", "#f472b6", "#fb923c", "#fbbf24", "#4ade80", "#2dd4bf", "#60a5fa" };
            var random = new Random();
            return colors[random.Next(colors.Length)];
        }

    }
}