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
                .Where(se => se.Task != null && (se.Task.AssigneeId == userId || (se.Task.UserId == userId && string.IsNullOrEmpty(se.Task.AssigneeId)))
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
                .Where(t => t.AssigneeId == userId || (t.UserId == userId && string.IsNullOrEmpty(t.AssigneeId)))
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

            var tasksQuery = await _context.WorkTasks
                .Include(t => t.Project)
                .Where(t => (t.AssigneeId == userId || (t.UserId == userId && string.IsNullOrEmpty(t.AssigneeId))) && !scheduledTaskIds.Contains(t.Id))
                .Select(t => new
                {
                    id = t.Id,
                    title = t.Title ?? "Unnamed Task",
                    projectName = t.Project != null ? t.Project.Name : "No Project",
                    description = t.Description ?? "",
                    deadlineDate = t.EndDate
                })
                .OrderBy(t => t.deadlineDate)
                .ToListAsync();

            var tasks = tasksQuery.Select(t => new
            {
                id = t.id,
                title = t.title,
                projectName = t.projectName,
                description = t.description,
                deadline = t.deadlineDate.ToString("yyyy-MM-dd"),
                isOverdue = t.deadlineDate < today
            });

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

            var scheduledEventsQuery = await _context.ScheduledEvents
                .Include(se => se.Task)!.ThenInclude(t => t.Project)
                .Where(se => se.Task != null && (se.Task.AssigneeId == userId || (se.Task.UserId == userId && string.IsNullOrEmpty(se.Task.AssigneeId)))
                             && se.StartTime >= startOfDay && se.StartTime < endOfDay)
                .OrderBy(se => se.StartTime)
                .Select(se => new
                {
                    id = se.Id,
                    title = (se.Task!.Project != null ? se.Task.Project.Name + " - " : "") + (se.Task.Title ?? "Unnamed Task"),
                    startDateTime = se.StartTime,
                    endDateTime = se.EndTime,
                    color = se.Color ?? "#818cf8"
                })
                .ToListAsync();

            var scheduledEvents = scheduledEventsQuery.Select(se => new
            {
                id = se.id,
                title = se.title,
                start = se.startDateTime.ToString("HH:mm"),
                end = se.endDateTime.ToString("HH:mm"),
                color = se.color
            });

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
                    .FirstOrDefaultAsync(t => t.Id == request.TaskId && (t.AssigneeId == userId || (t.UserId == userId && string.IsNullOrEmpty(t.AssigneeId))));

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
            if (se.Task == null || !(se.Task.AssigneeId == userId || (se.Task.UserId == userId && string.IsNullOrEmpty(se.Task.AssigneeId))))
                return Forbid();

            _context.ScheduledEvents.Remove(se);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        // POST: Calendar/UpdateScheduledEventColor
        [HttpPost]
        public async Task<IActionResult> UpdateScheduledEventColor(int id, string color)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            var se = await _context.ScheduledEvents.Include(s => s.Task).FirstOrDefaultAsync(s => s.Id == id);
            if (se == null)
                return Json(new { success = false, message = "Event not found" });

            // permission: only task owner or assignee can edit
            if (se.Task == null || !(se.Task.AssigneeId == userId || (se.Task.UserId == userId && string.IsNullOrEmpty(se.Task.AssigneeId))))
                return Forbid();

            se.Color = color;
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        public class AIPlanRequest
        {
            public List<int> TaskIds { get; set; } = new List<int>();
        }

        // POST: Calendar/AnalyzeTasksWithAI
        [HttpPost]
        public async Task<IActionResult> AnalyzeTasksWithAI([FromBody] AIPlanRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            if (request.TaskIds == null || !request.TaskIds.Any())
                return BadRequest(new { success = false, message = "No tasks selected" });

            var tasks = await _context.WorkTasks
                .Include(t => t.Project)
                .Where(t => request.TaskIds.Contains(t.Id) && (t.AssigneeId == userId || (t.UserId == userId && string.IsNullOrEmpty(t.AssigneeId))))
                .ToListAsync();

            var suggestions = new List<object>();
            var currentTime = DateTime.Today.AddDays(1).AddHours(8); // Start tomorrow 8 AM

            foreach (var task in tasks.OrderBy(t => t.EndDate)) // Prioritize earlier deadlines
            {
                // Skip lunch break
                if (currentTime.TimeOfDay >= new TimeSpan(12, 0, 0) && currentTime.TimeOfDay < new TimeSpan(13, 30, 0))
                {
                    currentTime = currentTime.Date.AddHours(13).AddMinutes(30);
                }

                // Move to next day if it's too late
                if (currentTime.TimeOfDay >= new TimeSpan(21, 0, 0))
                {
                    currentTime = currentTime.Date.AddDays(1).AddHours(8);
                }

                string importance;
                string color;
                int durationMinutes;
                
                var daysUntilDeadline = (task.EndDate - DateTime.Today).TotalDays;

                if (daysUntilDeadline < 0)
                {
                    importance = "Critical (Overdue)";
                    color = "#ef4444"; // Red
                    durationMinutes = 90;
                }
                else if (daysUntilDeadline <= 2)
                {
                    importance = "High (Approaching Deadline)";
                    color = "#f59e0b"; // Orange
                    durationMinutes = 60;
                }
                else
                {
                    importance = "Normal";
                    color = "#10b981"; // Green
                    durationMinutes = 45;
                }

                var startTime = currentTime;
                var endTime = currentTime.AddMinutes(durationMinutes);

                suggestions.Add(new
                {
                    taskId = task.Id,
                    title = (task.Project != null ? task.Project.Name + " - " : "") + task.Title,
                    startTime = startTime.ToString("yyyy-MM-ddTHH:mm"),
                    endTime = endTime.ToString("yyyy-MM-ddTHH:mm"),
                    color = color,
                    importance = importance
                });

                currentTime = endTime.AddMinutes(15); // 15 min break
            }

            return Ok(new { success = true, data = suggestions });
        }

        // POST: Calendar/SaveScheduledEvents
        [HttpPost]
        public async Task<IActionResult> SaveScheduledEvents([FromBody] List<ScheduledEventDto> requests)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            if (requests == null || !requests.Any())
                return BadRequest(new { success = false, message = "No events to save" });

            var taskIds = requests.Select(r => r.TaskId).ToList();
            var tasks = await _context.WorkTasks
                .Where(t => taskIds.Contains(t.Id) && (t.AssigneeId == userId || (t.UserId == userId && string.IsNullOrEmpty(t.AssigneeId))))
                .ToListAsync();

            var validTaskIds = tasks.Select(t => t.Id).ToHashSet();

            var eventsToSave = new List<ScheduledEvent>();
            foreach (var req in requests)
            {
                if (validTaskIds.Contains(req.TaskId))
                {
                    eventsToSave.Add(new ScheduledEvent
                    {
                        TaskId = req.TaskId,
                        StartTime = req.StartTime,
                        EndTime = req.EndTime,
                        Color = req.Color ?? GetRandomPastelColor()
                    });
                }
            }

            if (eventsToSave.Any())
            {
                _context.ScheduledEvents.AddRange(eventsToSave);
                await _context.SaveChangesAsync();
            }

            return Ok(new { success = true, message = "Saved successfully" });
        }

        private string GetRandomPastelColor()
        {
            var colors = new[] { "#818cf8", "#c084fc", "#f472b6", "#fb923c", "#fbbf24", "#4ade80", "#2dd4bf", "#60a5fa" };
            var random = new Random();
            return colors[random.Next(colors.Length)];
        }

    }
}