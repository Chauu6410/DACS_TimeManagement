using DACS_TimeManagement.Models;
using DACS_TimeManagement.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace DACS_TimeManagement.Controllers
{
    [Authorize]
    public class ReportController : Controller
    {
        private readonly IWorkTaskRepository _taskRepo;
        private readonly IProjectRepository _projectRepo;
        private readonly ApplicationDbContext _context;

        public ReportController(IWorkTaskRepository taskRepo, IProjectRepository projectRepo, ApplicationDbContext context)
        {
            _taskRepo = taskRepo;
            _projectRepo = projectRepo;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var tasks = await _taskRepo.GetAllAsync(userId);
            var projects = await _projectRepo.GetProjectsWithStatsAsync(userId);

            // 1. Task Status Distribution (Pie Chart)
            var statusStats = tasks.GroupBy(t => t.Status)
                                   .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
                                   .ToList();

            // 2. Time Logging Analytics (Bar Chart)
            var thirtyDaysAgo = DateTime.Now.AddDays(-30);
            var timeLogs = await _context.TimeLogs
                .Include(t => t.WorkTask)
                .Where(t => t.WorkTask.UserId == userId && t.LogDate >= thirtyDaysAgo)
                .ToListAsync();

            // Aggregate hours by Project
            var projectTimeMap = new Dictionary<string, double>();
            var unassignedHours = 0.0;

            foreach(var log in timeLogs)
            {
                var pId = log.WorkTask.ProjectId;
                if(pId.HasValue) 
                {
                    var proj = projects.FirstOrDefault(p => p.Id == pId.Value);
                    string pName = proj?.Name ?? "Deleted Project";
                    if(projectTimeMap.ContainsKey(pName))
                        projectTimeMap[pName] += log.DurationHours;
                    else
                        projectTimeMap[pName] = log.DurationHours;
                }
                else
                {
                    unassignedHours += log.DurationHours;
                }
            }
            if (unassignedHours > 0) projectTimeMap["No Project"] = unassignedHours;

            // 3. Daily Hours Over Last 7 Days (Line Chart)
            var last7Days = Enumerable.Range(0, 7).Select(i => DateTime.Today.AddDays(-6 + i)).ToList();
            var dailyHours = new double[7];
            for(int i = 0; i < 7; i++)
            {
                var dt = last7Days[i].Date;
                dailyHours[i] = timeLogs.Where(t => t.LogDate.Date == dt).Sum(t => t.DurationHours);
            }

            // Overview Metrics
            ViewBag.TotalTimeLogged = timeLogs.Sum(t => t.DurationHours);
            ViewBag.TotalTasksCompleted = tasks.Count(t => t.Status == DACS_TimeManagement.Models.TaskStatus.Completed);
            ViewBag.TotalProjects = projects.Count();
            ViewBag.TaskCompletionRate = tasks.Any() ? (int)((double)ViewBag.TotalTasksCompleted / tasks.Count() * 100) : 0;

            // Chart Data
            ViewBag.StatusJson = JsonSerializer.Serialize(statusStats.Select(s => s.Count));
            ViewBag.StatusLabelsJson = JsonSerializer.Serialize(statusStats.Select(s => s.Status));
            
            ViewBag.ProjectTimeLabelsJson = JsonSerializer.Serialize(projectTimeMap.Keys);
            ViewBag.ProjectTimeDataJson = JsonSerializer.Serialize(projectTimeMap.Values);

            ViewBag.DailyTimeLabelsJson = JsonSerializer.Serialize(last7Days.Select(d => d.ToString("ddd")));
            ViewBag.DailyTimeDataJson = JsonSerializer.Serialize(dailyHours);

            var projectList = projects.ToList();
            return View(projectList);
        }
    }
}
