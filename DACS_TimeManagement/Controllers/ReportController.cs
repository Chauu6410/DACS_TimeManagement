using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using DACS_TimeManagement.Models;

namespace DACS_TimeManagement.Controllers
{
    [Authorize]
    public class ReportController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportController(ApplicationDbContext context)
        {
            _context = context;
        }

        // View chính cho trang báo cáo
        public IActionResult Index()
        {
            return View();
        }

        // 1. Task Completion Chart (7 days)
        [HttpGet]
        public async Task<IActionResult> GetPerformanceData()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var sevenDaysAgo = DateTime.Now.Date.AddDays(-6); 

            var completedTasks = await _context.TaskHistories
                .Include(h => h.WorkTask)
                .Where(h => (h.WorkTask.UserId == userId || h.WorkTask.AssigneeId == userId) 
                            && h.ChangedAt >= sevenDaysAgo)
                .ToListAsync();

            var boardLists = await _context.BoardLists.ToDictionaryAsync(b => b.Id, b => b.Name);
            
            var data = completedTasks
                .Where(h => h.NewBoardListId.HasValue && boardLists.ContainsKey(h.NewBoardListId.Value) 
                            && (boardLists[h.NewBoardListId.Value].ToLower().Contains("done") || boardLists[h.NewBoardListId.Value].ToLower().Contains("hoàn tất")))
                .GroupBy(h => h.ChangedAt.Date)
                .Select(g => new { Date = g.Key.ToString("yyyy-MM-dd"), Count = g.Count() })
                .ToList();

            var result = new List<object>();
            for (int i = 0; i <= 6; i++)
            {
                var d = sevenDaysAgo.AddDays(i).ToString("yyyy-MM-dd");
                var item = data.FirstOrDefault(x => x.Date == d);
                result.Add(new { Date = d, Count = item != null ? item.Count : 0 });
            }

            return Json(result);
        }

        // 2. Hours Worked Tracking (7 days)
        [HttpGet]
        public async Task<IActionResult> GetHoursWorkedData()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var sevenDaysAgo = DateTime.Now.Date.AddDays(-6);

            var timeLogs = await _context.TimeLogs
                .Include(tl => tl.WorkTask)
                .Where(tl => (tl.WorkTask.UserId == userId || tl.WorkTask.AssigneeId == userId) 
                            && tl.LogDate >= sevenDaysAgo)
                .GroupBy(tl => tl.LogDate.Date)
                .Select(g => new { Date = g.Key.ToString("yyyy-MM-dd"), Hours = g.Sum(tl => tl.DurationHours) })
                .ToListAsync();

            var result = new List<object>();
            for (int i = 0; i <= 6; i++)
            {
                var d = sevenDaysAgo.AddDays(i).ToString("yyyy-MM-dd");
                var item = timeLogs.FirstOrDefault(x => x.Date == d);
                result.Add(new { Date = d, Hours = item != null ? Math.Round(item.Hours, 1) : 0 });
            }

            return Json(result);
        }

        // 3. Productivity Heatmap (365 days)
        [HttpGet]
        public async Task<IActionResult> GetHeatmapData()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var oneYearAgo = DateTime.Now.Date.AddDays(-365);

            var timeLogs = await _context.TimeLogs
                .Include(tl => tl.WorkTask)
                .Where(tl => (tl.WorkTask.UserId == userId || tl.WorkTask.AssigneeId == userId) && tl.LogDate >= oneYearAgo)
                .GroupBy(tl => tl.LogDate.Date)
                .Select(g => new { date = g.Key.ToString("yyyy-MM-dd"), count = g.Sum(tl => tl.DurationHours) })
                .ToListAsync();

            return Json(timeLogs);
        }

        // 4. Task Distribution Analysis (Current)
        [HttpGet]
        public async Task<IActionResult> GetTaskDistribution(int? projectId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            var query = _context.WorkTasks
                .Include(t => t.BoardList)
                .Where(t => t.UserId == userId || t.AssigneeId == userId);
                
            if (projectId.HasValue && projectId.Value > 0)
            {
                query = query.Where(t => t.ProjectId == projectId.Value);
            }

            var distribution = await query
                .Where(t => t.BoardList != null)
                .GroupBy(t => t.BoardList.Name)
                .Select(g => new { Stage = g.Key, Count = g.Count() })
                .ToListAsync();

            return Json(distribution);
        }

        // 5. Bottleneck Detection (Dwell Time per Stage)
        [HttpGet]
        public async Task<IActionResult> GetBottleneckData(int? projectId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            var query = _context.TaskHistories
                .Include(h => h.WorkTask)
                .Where(h => h.WorkTask != null && (h.WorkTask.UserId == userId || h.WorkTask.AssigneeId == userId));

            if (projectId.HasValue && projectId.Value > 0)
            {
                query = query.Where(h => h.WorkTask.ProjectId == projectId.Value);
            }

            var histories = await query.OrderBy(h => h.WorkTaskId).ThenBy(h => h.ChangedAt).ToListAsync();
            var boardLists = await _context.BoardLists.ToDictionaryAsync(b => b.Id, b => b.Name);

            var dwellTimes = new Dictionary<string, double>();
            var counts = new Dictionary<string, int>();

            var groupedByTask = histories.GroupBy(h => h.WorkTaskId);
            foreach (var taskGroup in groupedByTask)
            {
                var historyList = taskGroup.ToList();
                for (int i = 0; i < historyList.Count - 1; i++)
                {
                    var current = historyList[i];
                    var next = historyList[i + 1];
                    
                    if (current.NewBoardListId.HasValue && boardLists.ContainsKey(current.NewBoardListId.Value))
                    {
                        var listName = boardLists[current.NewBoardListId.Value];
                        var duration = (next.ChangedAt - current.ChangedAt).TotalHours;

                        if (!dwellTimes.ContainsKey(listName)) { dwellTimes[listName] = 0; counts[listName] = 0; }
                        dwellTimes[listName] += duration;
                        counts[listName]++;
                    }
                }
                
                var last = historyList.Last();
                if (last.NewBoardListId.HasValue && boardLists.ContainsKey(last.NewBoardListId.Value))
                {
                    var listName = boardLists[last.NewBoardListId.Value];
                    var duration = (DateTime.Now - last.ChangedAt).TotalHours;
                    
                    if (!listName.ToLower().Contains("done") && !listName.ToLower().Contains("hoàn tất"))
                    {
                        if (!dwellTimes.ContainsKey(listName)) { dwellTimes[listName] = 0; counts[listName] = 0; }
                        dwellTimes[listName] += duration;
                        counts[listName]++;
                    }
                }
            }

            var result = dwellTimes.Select(kvp => new 
            { 
                Stage = kvp.Key, 
                AverageHours = Math.Round(kvp.Value / (counts[kvp.Key] > 0 ? counts[kvp.Key] : 1), 1) 
            }).ToList();

            return Json(result);
        }

    }
}
