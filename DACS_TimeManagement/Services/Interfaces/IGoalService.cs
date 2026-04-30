using DACS_TimeManagement.DTOs;
using DACS_TimeManagement.Models;

namespace DACS_TimeManagement.Services.Interfaces
{
    public interface IGoalService
    {
        Task<GoalDetailDto> CreateAsync(CreateGoalDto dto, string userId);
        Task<GoalDetailDto?> UpdateAsync(UpdateGoalDto dto, string userId);
        Task<bool> LinkTasksAsync(int goalId, List<int> taskIds, string userId);
        Task RecalculateProgressForGoalAsync(int goalId, string userId, string? note = null);
        Task HandleTimeLogAsync(TimeLog timeLog);
        Task SyncProjectGoalsAsync(int projectId);
        Task SyncTaskGoalsAsync(int taskId);
        string GetAIPrediction(PersonalGoal goal);
        string GetAIShortStatus(PersonalGoal goal);

        Task<string> RegenerateSmartAIStrategyAsync(int goalId, string userId);
    }
}
