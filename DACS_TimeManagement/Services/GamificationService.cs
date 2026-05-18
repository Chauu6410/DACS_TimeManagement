using DACS_TimeManagement.Models;
using DACS_TimeManagement.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DACS_TimeManagement.Services
{
    public class GamificationService : IGamificationService
    {
        private readonly ApplicationDbContext _context;

        public GamificationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task AwardPointsAsync(string userId, int points)
        {
            var profile = await _context.UserProfiles.FirstOrDefaultAsync(u => u.UserId == userId);
            if (profile != null)
            {
                profile.Points += points;
                // Calculate level: Level 1 starts at 0 points. Every 100 points = 1 level
                profile.Level = (profile.Points / 100) + 1;
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateStreakAsync(string userId)
        {
            var profile = await _context.UserProfiles.FirstOrDefaultAsync(u => u.UserId == userId);
            if (profile == null) return;

            var today = DateTime.UtcNow.Date;
            if (profile.LastTaskCompletedDate.HasValue)
            {
                var lastDate = profile.LastTaskCompletedDate.Value.Date;
                if (today > lastDate)
                {
                    if ((today - lastDate).TotalDays == 1)
                    {
                        // Consecutive day
                        profile.CurrentStreak++;
                    }
                    else
                    {
                        // Streak broken
                        profile.CurrentStreak = 1;
                    }
                }
            }
            else
            {
                profile.CurrentStreak = 1;
            }

            profile.LastTaskCompletedDate = DateTime.UtcNow;

            if (profile.CurrentStreak > profile.HighestStreak)
            {
                profile.HighestStreak = profile.CurrentStreak;
            }

            await _context.SaveChangesAsync();
        }

        public async Task<UserProfile?> GetUserProfileGamificationAsync(string userId)
        {
            return await _context.UserProfiles.FirstOrDefaultAsync(u => u.UserId == userId);
        }

        public async Task<List<UserProfile>> GetProjectLeaderboardAsync(int projectId)
        {
            var projectMembers = await _context.ProjectMembers
                .Where(pm => pm.ProjectId == projectId)
                .Select(pm => pm.UserId)
                .ToListAsync();
                
            // Include project owner as well
            var project = await _context.Projects.FindAsync(projectId);
            if (project != null && !projectMembers.Contains(project.UserId))
            {
                projectMembers.Add(project.UserId);
            }

            return await _context.UserProfiles
                .Where(u => projectMembers.Contains(u.UserId))
                .OrderByDescending(u => u.Points)
                .ThenByDescending(u => u.CurrentStreak)
                .ToListAsync();
        }

        public async Task<List<UserProfile>> GetGlobalLeaderboardAsync(int limit = 10)
        {
            return await _context.UserProfiles
                .OrderByDescending(u => u.Points)
                .ThenByDescending(u => u.CurrentStreak)
                .Take(limit)
                .ToListAsync();
        }
    }
}
