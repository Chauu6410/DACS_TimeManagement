using DACS_TimeManagement.Models;
using DACS_TimeManagement.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace DACS_TimeManagement.Controllers
{
    [Authorize]
    public class ProjectController : Controller
    {
        private readonly IProjectRepository _projectRepo;
        private readonly DACS_TimeManagement.Services.ICryptoService _crypto;
        private readonly Microsoft.AspNetCore.Identity.UserManager<Microsoft.AspNetCore.Identity.IdentityUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly Microsoft.AspNetCore.SignalR.IHubContext<DACS_TimeManagement.Hubs.NotificationHub> _hubContext;
        private readonly DACS_TimeManagement.Services.Interfaces.IGoalService _goalService;

        public ProjectController(
            IProjectRepository projectRepo, 
            DACS_TimeManagement.Services.ICryptoService crypto,
            Microsoft.AspNetCore.Identity.UserManager<Microsoft.AspNetCore.Identity.IdentityUser> userManager,
            ApplicationDbContext context,
            Microsoft.AspNetCore.SignalR.IHubContext<DACS_TimeManagement.Hubs.NotificationHub> hubContext,
            DACS_TimeManagement.Services.Interfaces.IGoalService goalService)
        {
            _projectRepo = projectRepo;
            _crypto = crypto;
            _userManager = userManager;
            _context = context;
            _hubContext = hubContext;
            _goalService = goalService;
        }

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
            // Xóa các lỗi validate không cần thiết
            ModelState.Remove("UserId");
            ModelState.Remove("Tasks");

            if (ModelState.IsValid)
            {
                project.UserId = userId;
                project.CreatedDate = DateTime.Now;

                // BƯỚC 1: CHỈ LƯU PROJECT 1 LẦN DUY NHẤT
                _context.Projects.Add(project);
                await _context.SaveChangesAsync();

                // Tự động tạo 3 cột cơ bản cho Kanban Board
                var defaultLists = new List<BoardList>
                {
                    new BoardList { Name = "To Do", Position = 0, ProjectId = project.Id },
                    new BoardList { Name = "In Progress", Position = 1, ProjectId = project.Id },
                    new BoardList { Name = "Testing", Position = 2, ProjectId = project.Id },
                    new BoardList { Name = "Done", Position = 3, ProjectId = project.Id }
                };
                _context.BoardLists.AddRange(defaultLists);
                await _context.SaveChangesAsync();  // Lưu các cột xong là xong

                
                return RedirectToAction(nameof(Index));
            }
            return View(project);
        }

        [HttpGet]
        public async Task<IActionResult> GetGoalContributions(int projectId)
        {
            // Find goal-task pairs for tasks in this project
            var pairs = await _context.GoalTasks
                .Where(gt => gt.WorkTask.ProjectId == projectId)
                .Select(gt => new { gt.GoalId, TaskStatus = gt.WorkTask.Status })
                .ToListAsync();

            var grouped = pairs.GroupBy(p => p.GoalId).ToList();
            var result = new List<object>();
            foreach (var g in grouped)
            {
                var goalId = g.Key;
                var total = g.Count();
                var completed = g.Count(x => x.TaskStatus == Models.TaskStatus.Completed);
                var pct = total == 0 ? 0 : (double)completed / total * 100.0;
                var goal = await _context.PersonalGoals.FirstOrDefaultAsync(pg => pg.Id == goalId);
                var title = goal?.Title ?? "Goal";

                // Get AI prediction text (may be long) and derive a short status for color coding
                string aiDetail = _goalService != null && goal != null ? _goalService.GetAIPrediction(goal) : "";
                string aiStatus = "unknown";
                if (!string.IsNullOrEmpty(aiDetail))
                {
                    var lower = aiDetail.ToLower();
                    if (lower.Contains("trễ") || lower.Contains("trễ hạn") || lower.Contains("⚠️") || lower.Contains("at risk") || lower.Contains("risk")) aiStatus = "at-risk";
                    else if (lower.Contains("sớm") || lower.Contains("✅") || lower.Contains("achieve") || lower.Contains("hoàn thành sớm")) aiStatus = "on-track";
                    else aiStatus = "info";
                }

                result.Add(new { goalId = goalId, goalName = title, contributionPct = pct, progress = Math.Round(pct, 1), aiStatus = aiStatus, aiDetail = aiDetail });
            }

            return Json(result);
        }
        public async Task<IActionResult> Details(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            
            // Lấy Project kèm theo Tasks, BoardLists và Owner
            var projects = await _projectRepo.FindAsync(
                p => p.Id == id && p.UserId == userId, 
                p => p.Tasks, 
                p => p.BoardLists,
                p => p.Owner);
            
            var project = projects.FirstOrDefault();
            
            // Nếu không tìm thấy bằng UserId với quyền Owner, thử tìm với quyền Member
            if (project == null)
            {
                var targetProject = await _context.Projects
                    .Include(p => p.Tasks)
                    .Include(p => p.BoardLists)
                    .Include(p => p.Owner)
                    .FirstOrDefaultAsync(p => p.Id == id);
                if (targetProject != null)
                {
                    var isMember = await _context.ProjectMembers.AnyAsync(pm => pm.ProjectId == id && pm.UserId == userId);
                    if (isMember) project = targetProject;
                }
            }

            if (project == null) return NotFound();

            // Load danh sách Members để mang ra View
            ViewBag.Members = await _context.ProjectMembers
                 .Include(pm => pm.User)
                 .Where(pm => pm.ProjectId == id)
                 .ToListAsync();

            // Nếu dự án cũ chưa có cột nào, tự động tạo 3 cột cơ bản
            if (project.BoardLists == null || !project.BoardLists.Any())
            {
                var newBoards = new List<BoardList>
                {
                    new BoardList { Name = "To Do", Position = 0, ProjectId = project.Id },
                    new BoardList { Name = "In Progress", Position = 1, ProjectId = project.Id },
                    new BoardList { Name = "Testing", Position = 2, ProjectId = project.Id },
                    new BoardList { Name = "Done", Position = 3, ProjectId = project.Id }
                };
                
                project.BoardLists = newBoards;
                _projectRepo.Update(project);
                await _projectRepo.SaveAsync();
            }

            // Giải mã data cho hiển thị Kanban
            if (project.Tasks != null)
            {
                foreach(var task in project.Tasks)
                {
                    if (task.IsPrivate && !string.IsNullOrEmpty(task.Description))
                    {
                        if (userId == task.UserId || userId == task.AssigneeId)
                        {
                            task.Description = _crypto.Decrypt(task.Description);
                        }
                        else
                        {
                            task.Description = "This task description is private.";
                        }
                    }
                }
            }

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMember(int projectId, string email)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            // 1. Kiểm tra Project có thuộc quyền sở hữu của người dùng hiện tại không
            var project = await _projectRepo.GetByIdAsync(projectId, currentUserId ?? "");
            if (project == null)
            {
                TempData["ErrorMessage"] = "Project not found or you do not have permission to add members.";
                return RedirectToAction(nameof(Details), new { id = projectId });
            }

            // 2. Kiểm tra Email có tồn tại trên hệ thống không
            var targetUser = await _userManager.FindByEmailAsync(email);
            if (targetUser == null)
            {
                TempData["ErrorMessage"] = $"User not found with email: {email}";
                return RedirectToAction(nameof(Details), new { id = projectId });
            }

            if (targetUser.Id == currentUserId)
            {
                TempData["ErrorMessage"] = "You cannot invite yourself.";
                return RedirectToAction(nameof(Details), new { id = projectId });
            }

            // 3. Kiểm tra xem người này đã là member chưa
            var isAlreadyMember = await _context.ProjectMembers.AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == targetUser.Id);
            if (isAlreadyMember)
            {
                TempData["ErrorMessage"] = "This user is already in the project.";
                return RedirectToAction(nameof(Details), new { id = projectId });
            }

            // 4. Thêm Member
            var newMember = new ProjectMember
            {
                ProjectId = projectId,
                UserId = targetUser.Id,
                Role = "Member",
                JoinedDate = DateTime.Now
            };

            _context.ProjectMembers.Add(newMember);
            await _context.SaveChangesAsync();

            // 4.5 Lưu Notification vào DB
            var notification = new Notification
            {
                Title = "New Assignment",
                Message = $"You have been added to the project: {project.Name}!",
                TriggerTime = DateTime.Now,
                CreatedAt = DateTime.Now,
                IsRead = false,
                UserId = targetUser.Id
            };
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            // 5. Bắn thông báo Realtime SignalR cho người vừa được Invite
            await _hubContext.Clients.User(targetUser.Id)
                .SendAsync("ReceiveNotification", notification.Title, notification.Message, "System");

            TempData["SuccessMessage"] = $"Successfully invited {email} to the project!";
            return RedirectToAction(nameof(Details), new { id = projectId });
        }
        public async Task<IActionResult> Kanban(int id)
        {
            var project = await _context.Projects
                .Include(p => p.BoardLists)
                .Include(p => p.Tasks)
                    .ThenInclude(t => t.Assignee)
                .FirstOrDefaultAsync(p => p.Id == id);
            return View(project);
        }
    }
}
