using DACS_TimeManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace DACS_TimeManagement.Repositories
{
    public class ProjectRepository : Repository<Project>, IProjectRepository
    {
        public ProjectRepository(ApplicationDbContext context) : base(context) { }

        public async Task<IEnumerable<Project>> GetProjectsWithStatsAsync(string userId)
        {
            // Tối ưu hoá cực độ: Chỉ lấy Tasks để đếm tiến độ, dùng AsNoTracking để đọc nhanh
            return await _context.Projects
                .AsNoTracking()
                .Include(p => p.Tasks)
                .Where(p => p.UserId == userId || p.Members.Any(m => m.UserId == userId))
                .ToListAsync();
        }
    }
}
