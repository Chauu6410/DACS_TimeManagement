using DACS_TimeManagement.Models;
using DACS_TimeManagement.Repositories;
using DACS_TimeManagement.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace DACS_TimeManagement.Controllers
{
    [Authorize]
    public class GamificationController : Controller
    {
        private readonly IGamificationService _gamificationService;
        private readonly IProjectRepository _projectRepo;

        public GamificationController(IGamificationService gamificationService, IProjectRepository projectRepo)
        {
            _gamificationService = gamificationService;
            _projectRepo = projectRepo;
        }

        public async Task<IActionResult> Index(int? projectId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var projects = await _projectRepo.GetAllAsync(userId);
            ViewBag.Projects = projects;

            var userProfile = await _gamificationService.GetUserProfileGamificationAsync(userId);
            ViewBag.UserProfile = userProfile;

            List<Models.UserProfile> leaderboard = new List<Models.UserProfile>();
            
            if (projectId.HasValue)
            {
                ViewBag.SelectedProjectId = projectId.Value;
                leaderboard = await _gamificationService.GetProjectLeaderboardAsync(projectId.Value);
            }
            else
            {
                leaderboard = await _gamificationService.GetGlobalLeaderboardAsync(10);
                ViewBag.SelectedProjectId = null;
            }

            return View(leaderboard);
        }
    }
}
