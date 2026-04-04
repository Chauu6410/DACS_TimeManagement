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

        // Lấy số lượng để hiển thị Badge trên Navbar
        [HttpGet]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int count = await _notifRepo.CountUnreadAsync(userId);
            return Json(count);
        }

        [HttpPost]
        public async Task<IActionResult> MarkRead(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            await _notifRepo.MarkReadAsync(id, userId);
            return Ok();
        }
    }
}
