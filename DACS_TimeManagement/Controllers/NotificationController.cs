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

        public NotificationController(INotificationRepository notifRepo) => _notifRepo = notifRepo;

        // Hiển thị danh sách thông báo có phân trang
        public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var notifications = await _notifRepo.GetPagedAsync(userId, page, pageSize);
            int totalCount = await _notifRepo.CountAsync(userId);
            int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            return View(notifications);
        }

        // Route so the view under Views/Account/Notifications.cshtml can be reached at /Account/Notifications
        [HttpGet("/Account/Notifications")]
        public async Task<IActionResult> Notifications(int page = 1, int pageSize = 10)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var notifications = await _notifRepo.GetPagedAsync(userId, page, pageSize);
            int totalCount = await _notifRepo.CountAsync(userId);
            int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            // Explicitly return the view located in Views/Account/Notifications.cshtml
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
    }
}