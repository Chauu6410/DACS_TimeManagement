using DACS_TimeManagement.Models;
using DACS_TimeManagement.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DACS_TimeManagement.Controllers
{
    [Authorize]
    public class ProjectController : Controller
    {
        private readonly IProjectRepository _projectRepo;

        public ProjectController(IProjectRepository projectRepo) => _projectRepo = projectRepo;

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var projects = await _projectRepo.GetProjectsWithStatsAsync(userId);
            return View(projects);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Project project)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            ModelState.Remove("UserId");
            ModelState.Remove("Tasks");

            if (ModelState.IsValid)
            {
                project.UserId = userId;
                project.CreatedDate = DateTime.Now;
                await _projectRepo.AddAsync(project);
                await _projectRepo.SaveAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(project);
        }

        public async Task<IActionResult> Details(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var projects = await _projectRepo.FindAsync(p => p.Id == id && p.UserId == userId, p => p.Tasks);
            var project = projects.FirstOrDefault();
            
            if (project == null) return NotFound();
            return View(project);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var project = await _projectRepo.GetByIdAsync(id, userId);
            
            if (project == null) return NotFound();
            return View(project);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Project project)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            ModelState.Remove("Tasks"); // UserId is posted from hidden field, but removed Tasks.
            if (ModelState.IsValid)
            {
                project.UserId = userId;
                _projectRepo.Update(project);
                await _projectRepo.SaveAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(project);
        }

        public async Task<IActionResult> Delete(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var project = await _projectRepo.GetByIdAsync(id, userId);
            
            if (project == null) return NotFound();
            return View(project);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var project = await _projectRepo.GetByIdAsync(id, userId);
            
            if (project != null)
            {
                _projectRepo.Delete(project);
                await _projectRepo.SaveAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
