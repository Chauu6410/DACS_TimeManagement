using DACS_TimeManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace DACS_TimeManagement.Repositories
{
    public class GoalRepository : Repository<PersonalGoal>, IGoalRepository
    {
        public GoalRepository(ApplicationDbContext context) : base(context) { }

        public async Task<IEnumerable<PersonalGoal>> GetIncompleteGoalsAsync(string userId)
        {
            return await _context.PersonalGoals
                .Where(g => g.UserId == userId && g.CurrentValue < g.TargetValue)
                .OrderBy(g => g.TargetDate)
                .ToListAsync();
        }
    }
}
