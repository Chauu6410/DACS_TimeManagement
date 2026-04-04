using DACS_TimeManagement.Models;

namespace DACS_TimeManagement.Repositories
{
    public interface IGoalRepository : IRepository<PersonalGoal>
    {
        Task<IEnumerable<PersonalGoal>> GetIncompleteGoalsAsync(string userId);
    }
}
