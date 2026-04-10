using DACS_TimeManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace DACS_TimeManagement.Repositories
{
    public class NotificationRepository : Repository<Notification>, INotificationRepository
    {
        public NotificationRepository(ApplicationDbContext context) : base(context) { }

        // Lấy danh sách thông báo phân trang (sắp xếp mới nhất trước)
        public async Task<IEnumerable<Notification>> GetPagedAsync(string userId, int page, int pageSize)
        {
            return await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.TriggerTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        // Đếm tổng số thông báo của user
        public async Task<int> CountAsync(string userId)
        {
            return await _context.Notifications
                .CountAsync(n => n.UserId == userId);
        }

        // Lấy danh sách thông báo chưa đọc (không phân trang)
        public async Task<IEnumerable<Notification>> GetUnreadAsync(string userId)
        {
            return await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .OrderByDescending(n => n.TriggerTime)
                .ToListAsync();
        }

        // Đánh dấu một thông báo đã đọc
        public async Task MarkReadAsync(int id, string userId)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

            if (notification != null)
            {
                notification.IsRead = true;
                _context.Update(notification);
                await _context.SaveChangesAsync();
            }
        }

        // Đánh dấu tất cả thông báo của user là đã đọc
        public async Task MarkAllAsReadAsync(string userId)
        {
            var unreadNotifications = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            foreach (var n in unreadNotifications)
            {
                n.IsRead = true;
            }

            await _context.SaveChangesAsync();
        }

        // Đếm số thông báo chưa đọc
        public async Task<int> CountUnreadAsync(string userId)
        {
            return await _context.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);
        }
    }
}