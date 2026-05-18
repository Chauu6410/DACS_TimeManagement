using DACS_TimeManagement.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DACS_TimeManagement.Services.Interfaces
{
    public interface IGamificationService
    {
        Task AwardPointsAsync(string userId, int points);
        Task UpdateStreakAsync(string userId);
        Task<UserProfile?> GetUserProfileGamificationAsync(string userId);
        Task<List<UserProfile>> GetProjectLeaderboardAsync(int projectId);
        Task<List<UserProfile>> GetGlobalLeaderboardAsync(int limit = 10);
    }
}
