using DACS_TimeManagement.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace DACS_TimeManagement.Repositories
{
    public class Repository<T> : IRepository<T> where T : class
    {
        protected readonly ApplicationDbContext _context;
        protected readonly DbSet<T> _dbSet;

        public Repository(ApplicationDbContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        // Lấy tất cả theo UserId (Sử dụng Reflection để tìm thuộc tính UserId)
        public virtual async Task<IEnumerable<T>> GetAllAsync(string userId)
        {
            return await _dbSet
                .Where(e => EF.Property<string>(e, "UserId") == userId)
                .ToListAsync();
        }

        // Tìm kiếm linh hoạt với Include các bảng liên quan
        public virtual async Task<IEnumerable<T>> FindAsync(
            Expression<Func<T, bool>> predicate,
            params Expression<Func<T, object>>[] includes)
        {
            IQueryable<T> query = _dbSet;

            // Nạp các bảng liên quan (Eager Loading)
            foreach (var include in includes)
            {
                query = query.Include(include);
            }

            return await query.Where(predicate).ToListAsync();
        }

        // Lấy 1 bản ghi cụ thể theo ID và UserId
        public virtual async Task<T?> GetByIdAsync(int id, string userId)
        {
            var entity = await _dbSet.FindAsync(id);
            if (entity != null)
            {
                var actualUserId = _context.Entry(entity).Property("UserId").CurrentValue as string;
                if (actualUserId == userId)
                {
                    return entity;
                }
            }
            return null;
        }

        public virtual async Task AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
        }

        public virtual void Update(T entity)
        {
            _dbSet.Update(entity);
        }

        public virtual void Delete(T entity)
        {
            _dbSet.Remove(entity);
        }

        public virtual async Task<bool> SaveAsync()
        {
            return await _context.SaveChangesAsync() > 0;
        }
    }
}
