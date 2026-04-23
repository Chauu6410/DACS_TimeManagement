using DACS_TimeManagement.Models;

namespace DACS_TimeManagement.Repositories
{
    public interface IWorkTaskRepository : IRepository<WorkTask>
    {
        Task<IEnumerable<WorkTask>> GetTasksByProjectAsync(int projectId, string userId);
        Task<IEnumerable<WorkTask>> GetUpcomingTasksAsync(string userId, int days);
        Task<bool> UpdateTaskProgressAsync(int id, string userId, int progress);
        Task<bool> UpdateTaskDetailsAsync(int id, string userId, WorkTask updatedTask, bool isAdmin = false);
        Task<bool> DeleteTaskAsync(int id, string userId, bool isAdmin = false);
    }
}
