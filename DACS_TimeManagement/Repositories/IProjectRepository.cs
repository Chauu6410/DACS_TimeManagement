using DACS_TimeManagement.Models;

namespace DACS_TimeManagement.Repositories
{
    public interface IProjectRepository : IRepository<Project>
    {
        Task<IEnumerable<Project>> GetProjectsWithStatsAsync(string userId);
    }
}
