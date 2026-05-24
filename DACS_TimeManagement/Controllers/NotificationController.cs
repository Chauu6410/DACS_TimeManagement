using DACS_TimeManagement.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DACS_TimeManagement.Controllers
{
    [Authorize]
    public class NotificationController : Controller
    {
        private readonly INotificationRepository _notifRepo;
        private readonly Microsoft.Extensions.Localization.IStringLocalizer _localizer;

        public NotificationController(INotificationRepository notifRepo, Microsoft.Extensions.Localization.IStringLocalizerFactory factory)
        {
            _notifRepo = notifRepo;
            _localizer = factory.Create("Views.Account.Notifications", typeof(NotificationController).Assembly.GetName().Name);
        }

        private string TranslateMessage(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return msg;

            // Direct exact lookup first
            var localized = _localizer[msg];
            if (localized != null && !localized.ResourceNotFound)
                return localized.Value;

            // Simple pattern matching for common messages
            // Task added in project: "{creatorName} added a new task in project {projectName}."
            var matchAdded = System.Text.RegularExpressions.Regex.Match(msg, @"(.*) added a new task in project (.*)\.");
            if (matchAdded.Success)
                return string.Format(_localizer["TaskAddedInProject"].Value, matchAdded.Groups[1].Value, matchAdded.Groups[2].Value);

            // Task moved: "Task '{title}' was moved by {name}."
            var matchMoved = System.Text.RegularExpressions.Regex.Match(msg, @"Task '(.*)' was moved by (.*)\.");
            if (matchMoved.Success)
                return string.Format(_localizer["TaskMove"].Value, matchMoved.Groups[1].Value, matchMoved.Groups[2].Value);

            // Task assigned: "You have been assigned a new task!"
            if (msg.Contains("assigned a new task"))
                return _localizer["TaskAssigned"].Value;

            // Request modify: "User has requested to modify task {title}."
            var matchReqMod = System.Text.RegularExpressions.Regex.Match(msg, @"User has requested to modify task (.*)\.");
            if (matchReqMod.Success)
                return string.Format(_localizer["TaskRequestModify"].Value, matchReqMod.Groups[1].Value);

            // Request create: "{name} requested to create a task in project '{projectName}'."
            var matchReqCreate = System.Text.RegularExpressions.Regex.Match(msg, @"(.*) requested to create a task in project '(.*)'\.");
            if (matchReqCreate.Success)
                return string.Format(_localizer["TaskRequestCreate"].Value, matchReqCreate.Groups[1].Value, matchReqCreate.Groups[2].Value);

            return msg;
        }


        // Hiển thị danh sách thông báo có phân trang
        public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var notifications = await _notifRepo.GetPagedAsync(userId, page, pageSize);
            int totalCount = await _notifRepo.CountAsync(userId);
            int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            return View("~/Views/Account/Notifications.cshtml", notifications);
        }


        // Lấy số lượng chưa đọc để hiển thị Badge trên Navbar
        [HttpGet]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int count = await _notifRepo.CountUnreadAsync(userId);
            return Json(count);
        }

        // Lấy 5 thông báo mới nhất cho Dropdown
        [HttpGet]
        public async Task<IActionResult> GetRecentNotifications()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var notifications = await _notifRepo.GetPagedAsync(userId, 1, 5); // Lấy 5 thông báo trang 1
            return Json(notifications.Select(n => new {
                id = n.Id,
                title = _localizer[n.Title].Value, // Localize title if it's a key
                message = TranslateMessage((n.Message ?? string.Empty).Split("||", StringSplitOptions.None)[0]),
                isRead = n.IsRead,
                time = n.CreatedAt.ToString("g"),
                createdAt = n.CreatedAt
            }));
        }


        // Đánh dấu một thông báo là đã đọc
        [HttpPost]
        public async Task<IActionResult> MarkRead(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            await _notifRepo.MarkReadAsync(id, userId);
            return Ok();
        }

        // Compatibility wrapper for client-side code that calls /Notification/MarkAsRead
        [HttpPost]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            return await MarkRead(id);
        }

        // Đánh dấu tất cả thông báo của user là đã đọc
        [HttpPost]
        public async Task<IActionResult> MarkAllRead()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            await _notifRepo.MarkAllAsReadAsync(userId);
            return Ok();
        }

        // Compatibility wrapper for client-side code that calls /Notification/MarkAllAsRead
        [HttpPost]
        public async Task<IActionResult> MarkAllAsRead()
        {
            return await MarkAllRead();
        }

        // Xóa một thông báo
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            await _notifRepo.DeleteAsync(id, userId);
            return Ok();
        }

        // Xóa tất cả thông báo
        [HttpPost]
        public async Task<IActionResult> DeleteAll()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            await _notifRepo.DeleteAllAsync(userId);
            return Ok();
        }
    }
}
