using DACS_TimeManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace DACS_TimeManagement.Repositories
{
    public class ProjectRepository : Repository<Project>, IProjectRepository
    {
        public ProjectRepository(ApplicationDbContext context) : base(context) { }

        public async Task<IEnumerable<Project>> GetProjectsWithStatsAsync(string userId)
        {
            // Lấy dự án kèm theo danh sách Task và BoardLists (với WorkTasks) để hiển thị Kanban
            // Include projects where user is owner (UserId) OR a ProjectMember
            return await _context.Projects
                .Include(p => p.Tasks)
                .Include(p => p.BoardLists)
                    .ThenInclude(b => b.WorkTasks)
                .Include(p => p.Members)
                .Where(p => p.UserId == userId || p.Members.Any(m => m.UserId == userId))
                .ToListAsync();
        }
    }
}
