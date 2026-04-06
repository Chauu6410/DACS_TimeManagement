using DACS_TimeManagement.Models;
using DACS_TimeManagement.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using ClosedXML.Excel;
using System.IO;

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

        [HttpGet]
        public async Task<IActionResult> ExportToExcel()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            // Lấy toàn bộ Tasks của users
            var tasks = await _taskRepo.GetAllAsync(userId);
            
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("User Tasks Report");

                // Tạo Header
                var currentRow = 1;
                worksheet.Cell(currentRow, 1).Value = "Task ID";
                worksheet.Cell(currentRow, 2).Value = "Title";
                worksheet.Cell(currentRow, 3).Value = "Status";
                worksheet.Cell(currentRow, 4).Value = "Priority";
                worksheet.Cell(currentRow, 5).Value = "Start Date";
                worksheet.Cell(currentRow, 6).Value = "End Date";
                worksheet.Cell(currentRow, 7).Value = "Assignee Info";

                // Format Header
                var headerRange = worksheet.Range("A1:G1");
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.AirForceBlue;
                headerRange.Style.Font.FontColor = XLColor.White;

                // Thêm dữ liệu
                foreach (var task in tasks.OrderByDescending(t => t.Id))
                {
                    currentRow++;
                    worksheet.Cell(currentRow, 1).Value = task.Id;
                    worksheet.Cell(currentRow, 2).Value = task.Title;
                    worksheet.Cell(currentRow, 3).Value = task.Status.ToString();
                    worksheet.Cell(currentRow, 4).Value = task.Priority.ToString();
                    
                    // StartDate/EndDate là public DateTime không nullable nên gọi ToString() trực tiếp
                    worksheet.Cell(currentRow, 5).Value = task.StartDate.ToString("yyyy-MM-dd HH:mm");
                    worksheet.Cell(currentRow, 6).Value = task.EndDate.ToString("yyyy-MM-dd HH:mm");
                    
                    // Hiện người được phân công (nếu có id)
                    worksheet.Cell(currentRow, 7).Value = string.IsNullOrEmpty(task.AssigneeId) ? "Unassigned" : "Assigned";
                }

                // Tự canh chỉnh độ rộng cột
                worksheet.Columns().AdjustToContents();

                // Trả về file Excel dạng stream
                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"TimeMaster_Report_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
                }
            }
        }
    }
}
