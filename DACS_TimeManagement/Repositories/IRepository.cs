using System.Linq.Expressions;

namespace DACS_TimeManagement.Repositories
{
    public interface IRepository<T> where T : class
    {
        // Lấy tất cả dữ liệu thuộc về một User
        Task<IEnumerable<T>> GetAllAsync(string userId);

        // Tìm kiếm nâng cao (Lọc, Sắp xếp, Include bảng liên quan)
        Task<IEnumerable<T>> FindAsync(
            Expression<Func<T, bool>> predicate,
            params Expression<Func<T, object>>[] includes);

        // Lấy 1 bản ghi cụ thể theo ID và UserId
        Task<T?> GetByIdAsync(int id, string userId);

        // Thêm, Sửa, Xóa
        Task AddAsync(T entity);
        void Update(T entity);
        void Delete(T entity);

        // Lưu thay đổi vào Database
        Task<bool> SaveAsync();
    }
}
