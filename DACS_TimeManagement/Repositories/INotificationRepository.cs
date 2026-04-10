using DACS_TimeManagement.Models;

namespace DACS_TimeManagement.Repositories
{
    public interface INotificationRepository : IRepository<Notification>
    {
        // Lấy danh sách thông báo chưa đọc của người dùng
        Task<IEnumerable<Notification>> GetPagedAsync(string userId, int page, int pageSize);
        Task<int> CountAsync(string userId);
        Task<IEnumerable<Notification>> GetUnreadAsync(string userId);

        // Đánh dấu một thông báo cụ thể là đã đọc
        Task MarkReadAsync(int id, string userId);

        // Đánh dấu tất cả thông báo của người dùng là đã đọc
        Task MarkAllAsReadAsync(string userId);

        // Đếm số lượng thông báo chưa đọc (để hiển thị icon số nhỏ trên menu)
        Task<int> CountUnreadAsync(string userId);
    }
}
