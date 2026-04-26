using DACS_TimeManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace DACS_TimeManagement.Repositories
{
    public class WorkTaskRepository : Repository<WorkTask>, IWorkTaskRepository
    {
        public WorkTaskRepository(ApplicationDbContext context) : base(context) { }

        public async Task<IEnumerable<WorkTask>> GetTasksByProjectAsync(int projectId, string userId)
        {
            return await _dbSet
                .Where(t => t.ProjectId == projectId && t.UserId == userId)
                .Include(t => t.Project)
                .ToListAsync();
        }

        public async Task<IEnumerable<WorkTask>> GetUpcomingTasksAsync(string userId, int days)
        {
            var deadline = DateTime.Now.AddDays(days);
            return await _dbSet
                .Where(t => t.UserId == userId
                         && t.EndDate <= deadline
                         && t.Status != DACS_TimeManagement.Models.TaskStatus.Completed)
                .OrderBy(t => t.EndDate)
                .ToListAsync();
        }

        public async Task<bool> UpdateTaskProgressAsync(int id, string userId, int progress)
        {
            var task = await GetByIdAsync(id, userId);
            if (task == null) return false;

            task.Progress = progress;
            if (progress >= 100) task.Status = DACS_TimeManagement.Models.TaskStatus.Completed;
            
            Update(task);
            return await SaveAsync();
        }

        public async Task<bool> UpdateTaskDetailsAsync(int id, string userId, WorkTask updatedTask, bool isAdmin = false)
        {
            var existingTask = await _context.WorkTasks
                .Include(t => t.Project)
                .ThenInclude(p => p.Members)
                .FirstOrDefaultAsync(t => t.Id == id && 
                    (isAdmin || t.UserId == userId || t.AssigneeId == userId || 
                    (t.Project != null && (t.Project.UserId == userId || (!t.IsPrivate && t.Project.Members.Any(m => m.UserId == userId))))));
                    
            if (existingTask == null) return false;

            // Đồng bộ hoá trạng thái (Status) & Tiến độ (Progress)
            if (updatedTask.Status == DACS_TimeManagement.Models.TaskStatus.Completed)
            {
                // Nếu người dùng chọn Completed thì gán Progress = 100
                updatedTask.Progress = 100;
            }
            else if (updatedTask.Progress == 100)
            {
                // Nếu người dùng kéo thanh trượt tới 100 thì Status = Completed
                updatedTask.Status = DACS_TimeManagement.Models.TaskStatus.Completed;
            }
            else if (updatedTask.Progress > 0 && updatedTask.Status == DACS_TimeManagement.Models.TaskStatus.Todo)
            {
                // Nếu tiến độ lớn hơn 0 mà Status vẫn đang Todo, chuyển sang InProgress
                updatedTask.Status = DACS_TimeManagement.Models.TaskStatus.InProgress;
            }
            else if (updatedTask.Progress == 0 && updatedTask.Status == DACS_TimeManagement.Models.TaskStatus.InProgress)
            {
                // Nếu tiến độ về 0 mà Status đang là InProgress, chuyển lại Todo
                updatedTask.Status = DACS_TimeManagement.Models.TaskStatus.Todo;
            }

            existingTask.Title = updatedTask.Title;
            existingTask.Description = updatedTask.Description;
            existingTask.StartDate = updatedTask.StartDate;
            existingTask.EndDate = updatedTask.EndDate;
            existingTask.Priority = updatedTask.Priority;
            existingTask.Status = updatedTask.Status;
            existingTask.Progress = updatedTask.Progress;
            // Ensure Assignee changes are persisted
            existingTask.AssigneeId = updatedTask.AssigneeId;
            existingTask.IsPrivate = updatedTask.IsPrivate;
            existingTask.ProjectId = updatedTask.ProjectId;

            Update(existingTask);
            return await SaveAsync();
        }

        public async Task<bool> DeleteTaskAsync(int id, string userId, bool isAdmin = false)
        {
            var task = await _context.WorkTasks
                .Include(t => t.TimeLogs)
                .Include(t => t.Project)
                .ThenInclude(p => p.Members)
                .FirstOrDefaultAsync(t => t.Id == id && 
                    (isAdmin || t.UserId == userId || t.AssigneeId == userId || 
                    (t.Project != null && (t.Project.UserId == userId || (!t.IsPrivate && t.Project.Members.Any(m => m.UserId == userId))))));
            if (task == null) return false;

            if (task.TimeLogs != null && task.TimeLogs.Any())
            {
                _context.RemoveRange(task.TimeLogs);
            }
            
            Delete(task);
            return await SaveAsync();
        }
    }
}