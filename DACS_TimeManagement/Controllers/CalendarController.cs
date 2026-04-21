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

        // Unified save (create or update) with ownership check
        [HttpPost]
        public async Task<IActionResult> SaveEvent([FromBody] CalendarEvent model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            try
            {
                if (model.Id > 0)
                {
                    // update - ensure ownership
                    var existing = await _calendarRepo.GetByIdAsync(model.Id, userId);
                    if (existing == null)
                        return Forbid(); // not owner or not found

                    existing.Subject = model.Subject ?? "No Title";
                    existing.Description = model.Description;
                    existing.StartTime = model.StartTime;
                    existing.EndTime = model.EndTime;
                    if (!string.IsNullOrEmpty(model.ThemeColor)) 
                        existing.ThemeColor = model.ThemeColor.Length > 7 ? model.ThemeColor.Substring(0, 7) : model.ThemeColor;
                    existing.IsFullDay = model.IsFullDay;

                    _calendarRepo.Update(existing);
                    await _calendarRepo.SaveAsync();
                    return Ok(new { success = true, eventId = existing.Id });
                }
                else
                {
                    // create new - gán UserId của người đang đăng nhập
                    if (string.IsNullOrWhiteSpace(model.Subject))
                        return BadRequest(new { success = false, message = "Missing subject" });

                    model.UserId = userId;
                    if (string.IsNullOrEmpty(model.ThemeColor))
                        model.ThemeColor = "#6366f1"; // default color
                    else if (model.ThemeColor.Length > 7)
                        model.ThemeColor = model.ThemeColor.Substring(0, 7);

                    await _calendarRepo.AddAsync(model);
                    await _calendarRepo.SaveAsync();
                    return Ok(new { success = true, eventId = model.Id });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = "Save failed", detail = ex.Message });
            }
        }

        // Trang xem lịch
        public async Task<IActionResult> Index()
        {
            var events = await _context.CalendarEvents.ToListAsync();
            var tasks = await _context.WorkTasks
                .Include(t => t.Project)
                .OrderBy(t => t.EndDate)
                .ToListAsync();
            ViewBag.UserTasks = tasks;
            // expose current user id to the view so client-side can determine ownership
            ViewBag.CurrentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return View(events);
        }

        // GET: /Calendar/Create (dùng cho form HTML nếu cần)
        public IActionResult Create() => View();

        // API: Lấy danh sách sự kiện cho FullCalendar
        [HttpGet]
        public async Task<JsonResult> GetEvents(DateTime start, DateTime end)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var events = await _calendarRepo.GetEventsInRangeAsync(userId, start, end);
            var worktasks = await _context.WorkTasks
                .Include(p => p.Project)
                .Where(p => p.UserId == userId && p.EndDate >= start && p.EndDate <= end)
                .ToListAsync();

            var eventList = new List<object>();
            eventList.AddRange(events.Select(e => new {
                id = "evt_" + e.Id,
                title = "📅 " + e.Subject,
                start = e.StartTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                end = e.EndTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                description = e.Description,
                ownerId = e.UserId,
                color = MapColorToHex(e.ThemeColor),  // Map tên màu sang Hex
                className = new[] { "evt-event" },
                projectName = string.Empty,
                allDay = e.IsFullDay
            }));

            foreach (var p in worktasks)
            {
                eventList.Add(new
                {
                    id = "prj_" + p.Id,
                    title = "🗓️ " + p.Title + (p.Project != null ? " (" + p.Project.Name + ")" : ""),
                    start = p.EndDate.ToString("yyyy-MM-dd"),
                    end = p.EndDate.ToString("yyyy-MM-dd"),
                    description = p.Description ?? string.Empty,
                    ownerId = p.UserId,
                    projectName = p.Project != null ? p.Project.Name : string.Empty,
                    color = "#fecaca", // Màu mặc định cho Project Task
                    className = new[] { "prj-event" },
                    allDay = true
                });
            }
            return Json(eventList);
        }

        // Helper: Convert color names to Hex (in case old data stores "Orange" instead of "#f97316")
        private string MapColorToHex(string color)
        {
            if (string.IsNullOrEmpty(color)) return "#6366f1"; // Default Indigo
            if (color.StartsWith("#")) return color; // Already hex
            
            return color.ToLowerInvariant() switch
            {
                "indigo" => "#6366f1",
                "cyan" => "#06b6d4",
                "amber" => "#f59e0b",
                "pink" => "#ec4899",
                "emerald" => "#10b981",
                "red" => "#ef4444",
                "violet" => "#8b5cf6",
                "orange" => "#f97316",
                "teal" => "#14b8a6",
                "fuchsia" => "#d946ef",
                _ => "#6366f1" // Mặc định
            };
        }

        public class ColorUpdatePayload
        {
            public string themeColor { get; set; }
        }

        // Cho phép cập nhật riêng màu của event ngay cả khi không phải owner
        [HttpPost]
        public async Task<IActionResult> UpdateEventColor(int id, [FromBody] ColorUpdatePayload payload)
        {
            var evt = await _context.CalendarEvents.FindAsync(id);
            if (evt == null)
                return NotFound(new { success = false, message = "Event not found" });

            try
            {
                string? color = payload?.themeColor;
                if (string.IsNullOrEmpty(color))
                    return BadRequest(new { success = false, message = "Invalid color" });

                if (color.Length > 7) color = color.Substring(0, 7);

                evt.ThemeColor = color;
                _calendarRepo.Update(evt);
                await _calendarRepo.SaveAsync();
                return Ok(new { success = true });
            }
            catch
            {
                return BadRequest(new { success = false });
            }
        }

        // API TẠO SỰ KIỆN (dùng cho AJAX JSON)
        [HttpPost("Api/CreateEvent")]
        public async Task<IActionResult> ApiCreateEvent([FromBody] CalendarEvent model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            try
            {
                if (string.IsNullOrWhiteSpace(model.Subject))
                    return BadRequest(new { success = false, message = "Missing subject" });

                if (string.IsNullOrEmpty(model.ThemeColor))
                    model.ThemeColor = "#6366f1";

                model.UserId = userId;

                await _calendarRepo.AddAsync(model);
                await _calendarRepo.SaveAsync();

                return Ok(new { success = true, eventId = model.Id });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = "Failed to create event", detail = ex.Message });
            }
        }

        // API XÓA SỰ KIỆN
        [HttpPost]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var evt = await _calendarRepo.GetByIdAsync(id, userId);
            if (evt == null)
                return Json(new { success = false, message = "Event not found" });

            _calendarRepo.Delete(evt);
            await _calendarRepo.SaveAsync();
            return Json(new { success = true });
        }

        // (Tùy chọn) API CẬP NHẬT SỰ KIỆN (nếu muốn chỉnh sửa màu hoặc nội dung)
        [HttpPost]
        public async Task<IActionResult> UpdateEvent(int id, [FromBody] CalendarEvent updatedModel)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var existing = await _calendarRepo.GetByIdAsync(id, userId);
            if (existing == null)
                return NotFound(new { success = false, message = "Event not found" });

            existing.Subject = updatedModel.Subject;
            existing.Description = updatedModel.Description;
            existing.StartTime = updatedModel.StartTime;
            existing.EndTime = updatedModel.EndTime;
            existing.ThemeColor = updatedModel.ThemeColor ?? existing.ThemeColor;
            existing.IsFullDay = updatedModel.IsFullDay;

            _calendarRepo.Update(existing);
            await _calendarRepo.SaveAsync();
            return Ok(new { success = true });
        }

        // Action cũ CreateEvent (dùng cho form HTML, nếu không dùng thì có thể bỏ)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateEvent(CalendarEvent model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            if (ModelState.IsValid)
            {
                model.UserId = userId;
                await _calendarRepo.AddAsync(model);
                await _calendarRepo.SaveAsync();
                return RedirectToAction("Index", "Home");
            }
            return View("Create", model);
        }

        public class AutoPlanTaskModel
        {
            public string Title { get; set; }
            public string Difficulty { get; set; }
            public int DurationMinutes { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> AutoPlanTomorrow([FromBody] List<AutoPlanTaskModel> tasks)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            if (tasks == null || !tasks.Any()) return BadRequest(new { success = false, message = "No tasks provided" });

            DateTime targetDate = DateTime.Today.AddDays(1);
            DateTime currentTime = new DateTime(targetDate.Year, targetDate.Month, targetDate.Day, 8, 30, 0);

            var sortedTasks = tasks
                .OrderBy(t => t.Difficulty == "Hard" ? 1 : (t.Difficulty == "Medium" ? 2 : 3))
                .ToList();

            foreach (var t in sortedTasks)
            {
                // Tránh giờ nghỉ trưa (11:30 - 13:00)
                if (currentTime.TimeOfDay >= new TimeSpan(11, 30, 0) && currentTime.TimeOfDay < new TimeSpan(13, 0, 0))
                {
                    currentTime = new DateTime(targetDate.Year, targetDate.Month, targetDate.Day, 13, 0, 0);
                }

                var end = currentTime.AddMinutes(t.DurationMinutes);
                
                // Nếu task lấn sâu sang giờ nghỉ trưa, đẩy hẳn sang buổi chiều
                if (currentTime.TimeOfDay < new TimeSpan(11, 30, 0) && end.TimeOfDay > new TimeSpan(12, 0, 0))
                {
                    currentTime = new DateTime(targetDate.Year, targetDate.Month, targetDate.Day, 13, 0, 0);
                    end = currentTime.AddMinutes(t.DurationMinutes);
                }

                string color = t.Difficulty == "Hard" ? "#ef4444" : (t.Difficulty == "Medium" ? "#f59e0b" : "#10b981");

                var evt = new CalendarEvent
                {
                    Subject = t.Title,
                    UserId = userId,
                    StartTime = currentTime,
                    EndTime = end,
                    ThemeColor = color,
                    IsFullDay = false,
                    Description = "🤖 Auto-scheduled - Difficulty: " + t.Difficulty
                };

                await _calendarRepo.AddAsync(evt);
                
                // Trễ thêm 5 phút Pomodoro nghỉ ngắn sau mỗi Task
                currentTime = end.AddMinutes(5);
            }

            await _calendarRepo.SaveAsync();
            return Ok(new { success = true });
        }
    }
}