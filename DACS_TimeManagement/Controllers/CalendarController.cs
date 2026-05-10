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
        private readonly DACS_TimeManagement.Services.Interfaces.IGeminiService _geminiService;
        private readonly DACS_TimeManagement.Services.Interfaces.IUserWorkScheduleService _workScheduleService;

        public CalendarController(ICalendarRepository calendarRepo, ApplicationDbContext context, DACS_TimeManagement.Services.Interfaces.IGeminiService geminiService, DACS_TimeManagement.Services.Interfaces.IUserWorkScheduleService workScheduleService)
        {
            _calendarRepo = calendarRepo;
            _context = context;
            _geminiService = geminiService;
            _workScheduleService = workScheduleService;
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
                .Where(t => (t.AssigneeId == userId || (t.UserId == userId && string.IsNullOrEmpty(t.AssigneeId))) 
                         && !scheduledTaskIds.Contains(t.Id) 
                         && t.EndDate.Date >= today)

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

                // Check overlap
                var overlapError = await GetOverlapError(userId, request.StartTime, request.EndTime);
                if (overlapError != null)
                    return BadRequest(new { success = false, message = overlapError });

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

        private async Task<string?> GetOverlapError(string userId, DateTime start, DateTime end, int? excludeId = null)
        {
            var overlap = await _context.ScheduledEvents
                .Include(se => se.Task)
                .Where(se => se.Task != null && (se.Task.AssigneeId == userId || (se.Task.UserId == userId && string.IsNullOrEmpty(se.Task.AssigneeId))))
                .Where(se => se.Id != excludeId)
                .Where(se => (start >= se.StartTime && start < se.EndTime) || 
                             (end > se.StartTime && end <= se.EndTime) || 
                             (start <= se.StartTime && end >= se.EndTime))
                .FirstOrDefaultAsync();

            if (overlap != null)
            {
                return $"Trùng lịch với: {overlap.Task?.Title} ({overlap.StartTime:HH:mm} - {overlap.EndTime:HH:mm})";
            }
            return null;
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
            public DateTime? StartDate { get; set; }
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

            if (!tasks.Any())
                return BadRequest(new { success = false, message = "Tasks not found or you don't have access" });

            var schedule = await _workScheduleService.GetOrCreateDefaultAsync(userId);
            var startDate = request.StartDate?.ToString("yyyy-MM-dd") ?? DateTime.Today.AddDays(1).ToString("yyyy-MM-dd");
            var refDate = request.StartDate ?? DateTime.Today.AddDays(1);

            var busyEvents = await _context.ScheduledEvents
                .Include(se => se.Task)
                .Where(se => se.Task != null && (se.Task.AssigneeId == userId || (se.Task.UserId == userId && string.IsNullOrEmpty(se.Task.AssigneeId)))
                             && se.StartTime >= refDate.Date 
                             && se.StartTime <= refDate.Date.AddDays(14))
                .Select(se => new { Start = se.StartTime.ToString("yyyy-MM-ddTHH:mm"), End = se.EndTime.ToString("yyyy-MM-ddTHH:mm"), Title = se.Task.Title })
                .ToListAsync();

            var busyJson = System.Text.Json.JsonSerializer.Serialize(busyEvents);

            string allowedColors = "#818cf8, #f472b6, #fb923c, #fbbf24, #4ade80, #2dd4bf, #60a5fa";

            string context = $@"Bạn là một AI chuyên gia lập lịch trình TUYỆT ĐỐI KHÔNG TRÙNG LỊCH. 
CÁC QUY TẮC TỬ THỦ:
1. KHÔNG GHI ĐÈ: 'Busy Slots' là các khung giờ ĐÃ CÓ CHỦ. Tuyệt đối không xếp task mới vào đó.
2. KHÔNG TỰ TRÙNG: Các task mới bạn đang xếp cũng phải nối tiếp nhau, không được trùng giờ nhau.
3. GIỜ LÀM: {schedule.DefaultStartHour:HH:mm} - {schedule.DefaultEndHour:HH:mm}. Nghỉ trưa: {schedule.LunchStart:HH:mm} - {schedule.LunchEnd:HH:mm}. 
4. THỜI GIAN: Bắt đầu từ {startDate} lúc {schedule.DefaultStartHour:HH:mm}. Nếu hết giờ làm việc, hãy chuyển sang ngày tiếp theo.
5. MÀU SẮC: Chọn trong [{allowedColors}].
6. ĐỊNH DẠNG: Chỉ trả về JSON ARRAY.";

            var taskData = tasks.Select(t => new {
                Id = t.Id,
                Title = (t.Project != null ? t.Project.Name + " - " : "") + t.Title,
                Deadline = t.EndDate.ToString("yyyy-MM-dd")
            });
            var jsonInput = System.Text.Json.JsonSerializer.Serialize(taskData);

            var prompt = $@"{context}
DANH SÁCH LỊCH ĐÃ CÓ (BUSY SLOTS - CẤM XẾP TRÙNG):
{busyJson}

DANH SÁCH TASK MỚI CẦN XẾP LỊCH:
{jsonInput}

Yêu cầu đầu ra JSON Array mẫu:
[
  {{
    ""taskId"": id,
    ""title"": ""tên"",
    ""startTime"": ""yyyy-MM-ddTHH:mm"",
    ""endTime"": ""yyyy-MM-ddTHH:mm"",
    ""color"": ""chọn 1 mã màu từ danh sách cho sẵn"",
    ""importance"": ""Critical/High/Normal""
  }}
]";

            try 
            {
                string aiRaw = await _geminiService.GenerateContent(prompt);
                
                // 1. Check for explicit error messages from GeminiService
                if (aiRaw.StartsWith("Lỗi", StringComparison.OrdinalIgnoreCase) || 
                    aiRaw.StartsWith("Error", StringComparison.OrdinalIgnoreCase) ||
                    aiRaw.StartsWith("AI không", StringComparison.OrdinalIgnoreCase) ||
                    aiRaw.StartsWith("Đã xảy ra", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { success = false, message = aiRaw });
                }

                // 2. Try to extract JSON array
                string? cleanJson = ExtractJsonArray(aiRaw);
                if (string.IsNullOrEmpty(cleanJson))
                {
                    // If not a JSON array, it might be a conversational refusal/error
                    return BadRequest(new { success = false, message = "AI không trả về đúng định dạng lịch trình. Phản hồi: " + aiRaw });
                }
                
                var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var suggestions = System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.JsonElement>>(cleanJson, options);

                string? warning = null;
                if (suggestions != null && suggestions.Count < tasks.Count)
                {
                    warning = $"AI chỉ xếp được {suggestions.Count}/{tasks.Count} công việc. Một số công việc không thể tìm thấy khoảng trống phù hợp trong lịch trình hiện tại của bạn.";
                }

                return Ok(new { success = true, data = suggestions, warning = warning });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = "Lỗi khi xử lý phản hồi từ AI: " + ex.Message });
            }
        }

        // Helper: tries to extract first JSON array from text
        private static string? ExtractJsonArray(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var trimmed = text.Trim();
            
            // Handle markdown code blocks
            if (trimmed.Contains("```"))
            {
                int start = trimmed.IndexOf("[");
                int end = trimmed.LastIndexOf("]");
                if (start >= 0 && end > start)
                {
                    return trimmed.Substring(start, end - start + 1);
                }
            }

            if (trimmed.StartsWith("["))
            {
                int depth = 0;
                for (int i = 0; i < trimmed.Length; i++)
                {
                    if (trimmed[i] == '[') depth++;
                    else if (trimmed[i] == ']')
                    {
                        depth--;
                        if (depth == 0) return trimmed.Substring(0, i + 1);
                    }
                }
            }
            
            // Final fallback: try to find any [ ... ]
            int fStart = trimmed.IndexOf("[");
            int fEnd = trimmed.LastIndexOf("]");
            if (fStart >= 0 && fEnd > fStart) return trimmed.Substring(fStart, fEnd - fStart + 1);

            return null;
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
                    // Backend validation for each suggestion
                    var overlapError = await GetOverlapError(userId, req.StartTime, req.EndTime);
                    if (overlapError != null)
                    {
                        return BadRequest(new { success = false, message = $"Lỗi: '{req.StartTime:HH:mm}' {overlapError}" });
                    }

                    // Also check overlap with other items in this batch
                    if (eventsToSave.Any(e => (req.StartTime >= e.StartTime && req.StartTime < e.EndTime) || (req.EndTime > e.StartTime && req.EndTime <= e.EndTime)))
                    {
                         return BadRequest(new { success = false, message = $"Lỗi: AI đề xuất hai task mới trùng nhau tại {req.StartTime:HH:mm}" });
                    }

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