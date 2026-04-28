using DACS_TimeManagement.Hubs;
using DACS_TimeManagement.Models;
using DACS_TimeManagement.Repositories;
using DACS_TimeManagement.Services;
using DACS_TimeManagement.Data; // Đảm bảo có namespace của DBContext
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace DACS_TimeManagement.Controllers
{
    [Authorize]
    public class WorkTaskController : Controller
    {
        private readonly IWorkTaskRepository _taskRepo;
        private readonly IProjectRepository _projectRepo;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ICryptoService _crypto;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<WorkTaskController> _logger;
        private readonly DACS_TimeManagement.Services.Interfaces.IGoalService _goalService;

        public WorkTaskController(IWorkTaskRepository taskRepo, IProjectRepository projectRepo, IHubContext<NotificationHub> hubContext, ICryptoService crypto, ApplicationDbContext context, ILogger<WorkTaskController> logger, DACS_TimeManagement.Services.Interfaces.IGoalService goalService)
        {
            _taskRepo = taskRepo;
            _projectRepo = projectRepo;
            _hubContext = hubContext;
            _crypto = crypto;
            _context = context;
            _logger = logger;
            _goalService = goalService;
        }

        private async Task NotifyProjectUsersAboutNewTaskAsync(WorkTask task, string creatorUserId)
        {
            if (!task.ProjectId.HasValue)
            {
                return;
            }

            var project = await _context.Projects
                .Include(p => p.Members)
                .FirstOrDefaultAsync(p => p.Id == task.ProjectId.Value);

            if (project == null)
            {
                return;
            }

            var creator = await _context.Users.FirstOrDefaultAsync(u => u.Id == creatorUserId);
            var creatorName = creator?.Email ?? creator?.UserName ?? "A member";

            var recipientIds = project.Members
                .Select(m => m.UserId)
                .Append(project.UserId)
                .Where(id => !string.IsNullOrEmpty(id) && id != creatorUserId)
                .Distinct()
                .ToList();

            if (!recipientIds.Any())
            {
                return;
            }

            var title = "Project Update";
            var mainMessage = $"{creatorName} added a new task in project {project.Name}.";
            var notifications = recipientIds.Select(recipientId => new Notification
            {
                Title = title,
                Message = $"{mainMessage}||Task: {task.Title}",
                TriggerTime = DateTime.Now,
                CreatedAt = DateTime.Now,
                IsRead = false,
                UserId = recipientId
            }).ToList();

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();

            foreach (var recipientId in recipientIds)
            {
                await _hubContext.Clients.User(recipientId)
                    .SendAsync("ReceiveNotification", title, mainMessage, task.Title);
            }
        }

        // --- 1. TRANG KANBAN (Tối ưu tốc độ cực cao) ---
        public async Task<IActionResult> Index(int? projectId)
        {
            try
            {
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // 1. Lấy danh sách Project để đổ vào Dropdown chọn (Chỉ lấy Name và Id cho nhẹ)
                var userProjects = (await _projectRepo.GetProjectsWithStatsAsync(currentUserId)).ToList();

                if (!userProjects.Any())
                {
                    return View(new Models.KanbanViewModel { Projects = new List<Project>(), BoardLists = new List<BoardList>() });
                }

                // 2. QUAN TRỌNG: Xác định Project nào sẽ được hiển thị
                // Nếu người dùng chưa chọn (projectId == null), mặc định lấy Project đầu tiên
                int selectedId = projectId ?? userProjects.First().Id;

                var isProjectOwner = userProjects.Any(p => p.Id == selectedId && p.UserId == currentUserId);

                // 3. CHỈ LOAD DỮ LIỆU CỦA 1 PROJECT ĐANG CHỌN (Tăng tốc độ 500%)
                var boardLists = await _context.BoardLists
                    .Where(bl => bl.ProjectId == selectedId) // Lọc chặt chẽ theo ID
                    .OrderBy(bl => bl.Position)
                    .ToListAsync();

                bool isDirty = false;
                foreach (var list in boardLists)
                {
                    if ((list.Name == "To Do" || list.Name == "Cần làm") && (list.Name != "To Do" || list.Position != 0)) { list.Name = "To Do"; list.Position = 0; isDirty = true; }
                    else if ((list.Name == "In Progress" || list.Name == "Đang làm" || list.Name == "Doing") && (list.Name != "In Progress" || list.Position != 1)) { list.Name = "In Progress"; list.Position = 1; isDirty = true; }
                    else if ((list.Name == "Done" || list.Name == "Hoàn tất") && (list.Name != "Done" || list.Position != 3)) { list.Name = "Done"; list.Position = 3; isDirty = true; }
                }
                if (!boardLists.Any(bl => bl.Name == "Testing"))
                {
                    var testingList = new BoardList { Name = "Testing", Position = 2, ProjectId = selectedId };
                    _context.BoardLists.Add(testingList);
                    boardLists.Add(testingList);
                    isDirty = true;
                }
                if (isDirty)
                {
                    await _context.SaveChangesAsync();
                    boardLists = boardLists.OrderBy(bl => bl.Position).ToList();
                }

                List<WorkTask> tasks;
                if (isProjectOwner)
                {
                    // Project owner can see all tasks in the project
                    tasks = await _context.WorkTasks
                        .AsNoTracking()
                        .Include(t => t.Assignee)
                        .Where(t => t.ProjectId == selectedId)
                        .ToListAsync();
                }
                else
                {
                    // Members may only see tasks they created or tasks assigned to them
                    tasks = await _context.WorkTasks
                        .AsNoTracking()
                        .Include(t => t.Assignee)
                        .Where(t => t.ProjectId == selectedId && (t.UserId == currentUserId || t.AssigneeId == currentUserId))
                        .ToListAsync();
                }

                // 4. Ghép Task vào List (Chỉ xử lý trong phạm vi 1 Project nên cực nhanh)
                foreach (var list in boardLists)
                {
                    list.WorkTasks = tasks
                        .Where(t => t.BoardListId == list.Id)
                        .OrderBy(t => t.Position)
                        .ToList();
                }

                // 5. Truyền dữ liệu sang View
                var viewModel = new Models.KanbanViewModel
                {
                    Projects = userProjects,
                    SelectedProjectId = selectedId,
                    BoardLists = boardLists
                };

                // Load pending change requests related to tasks in this project
                var taskIds = tasks.Select(t => t.Id).ToList();
                var pendingRequests = await _context.TaskChangeRequests
                    .Where(r => r.Status == TaskChangeStatus.Pending && taskIds.Contains(r.TaskId))
                    .ToListAsync();

                ViewBag.PendingRequests = pendingRequests; // List<TaskChangeRequest>
                ViewBag.IsProjectOwner = isProjectOwner;

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Lỗi load Kanban");
                return View(new Models.KanbanViewModel());
            }
        }

        // --- 2. TẠO MỚI TASK ---
        public async Task<IActionResult> Create(int? projectId, int? boardListId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            // 1. Lấy danh sách Project của người đang đăng nhập
            var projects = await _projectRepo.GetAllAsync(userId);
            ViewBag.ProjectId = new SelectList(projects, "Id", "Name", projectId);

            // Fetch members for the selected project initially, or return empty
            var assignees = new List<object>();
            if (projectId.HasValue)
            {
                var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
                if (project != null)
                {
                    if (project.UserId == userId) 
                    {
                        // Nếu là Owner dự án -> Được phép giao cho tất cả mọi người
                        var owner = await _context.Users.FirstOrDefaultAsync(u => u.Id == project.UserId);
                        if (owner != null) assignees.Add(new { Id = owner.Id, DisplayName = owner.Email + " (Owner)" });

                        var members = await _context.ProjectMembers
                            .Where(pm => pm.ProjectId == projectId.Value)
                            .Join(_context.Users, pm => pm.UserId, u => u.Id, (pm, u) => new { Id = u.Id, DisplayName = u.Email })
                            .ToListAsync();
                        
                        assignees.AddRange(members.Where(m => m.Id != owner.Id));
                    }
                    else
                    {
                        // Nếu chỉ là Member -> Chỉ được giao cho chính bản thân mình
                        var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                        if (currentUser != null) assignees.Add(new { Id = currentUser.Id, DisplayName = currentUser.Email + " (You)" });
                    }
                }
            }
            else
            {
                // Nếu chưa chọn project -> Chỉ giao cho mình
                var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (currentUser != null) assignees.Add(new { Id = currentUser.Id, DisplayName = currentUser.Email + " (You)" });
            }
            // Không gán allUsers nữa, load động hoặc để nguyên list rỗng lúc đầu
            ViewBag.AssigneeId = new SelectList(assignees, "Id", "DisplayName", userId);
            
            var task = new WorkTask { ProjectId = projectId, BoardListId = boardListId };
            return View(task);
        }
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(WorkTask task)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            ModelState.Remove("UserId");
            ModelState.Remove("Project");
            ModelState.Remove("TimeLogs");
            ModelState.Remove("BoardListId");

            if (task.StartDate > task.EndDate)
            {
                ModelState.AddModelError("EndDate", "End date must be greater than start date.");
            }

            if (ModelState.IsValid)
            {
                task.UserId = userId;

                // Tự động gán vào cột đầu tiên nếu trống
                if (task.BoardListId == null || task.BoardListId == 0)
                {
                    var firstList = await _context.BoardLists
                        .Where(bl => bl.ProjectId == task.ProjectId)
                        .OrderBy(bl => bl.Position)
                        .FirstOrDefaultAsync();
                    if (firstList != null) task.BoardListId = firstList.Id;
                }

                if (task.IsPrivate && !string.IsNullOrEmpty(task.Description))
                {
                    task.Description = _crypto.Encrypt(task.Description);
                }

                if (string.IsNullOrEmpty(task.AssigneeId)) task.AssigneeId = userId;

                var project = task.ProjectId.HasValue ? await _context.Projects.FirstOrDefaultAsync(p => p.Id == task.ProjectId.Value) : null;
                var isOwner = project != null && project.UserId == userId;

                if (isOwner)
                {
                    await _taskRepo.AddAsync(task);
                    await _taskRepo.SaveAsync();
                    
                    // Ghi nhận lịch sử tạo task
                    var history = new TaskHistory
                    {
                        WorkTaskId = task.Id,
                        OldBoardListId = null,
                        NewBoardListId = task.BoardListId,
                        ChangedAt = DateTime.Now,
                        ChangedByUserId = userId
                    };
                    _context.TaskHistories.Add(history);
                    await _context.SaveChangesAsync();
                    
                    await NotifyProjectUsersAboutNewTaskAsync(task, userId);
                }
                else
                {
                    // Create a change request for creation
                    var payload = new { Title = task.Title, ProjectId = task.ProjectId, BoardListId = task.BoardListId };
                    var tcr = new Models.TaskChangeRequest
                    {
                        TaskId = 0,
                        RequesterId = userId,
                        OwnerId = project?.UserId ?? string.Empty,
                        Action = Models.TaskChangeAction.Create,
                        Payload = JsonSerializer.Serialize(payload),
                        Status = Models.TaskChangeStatus.Pending
                    };
                    _context.TaskChangeRequests.Add(tcr);
                    await _context.SaveChangesAsync();
                    // Notify owner
                    if (!string.IsNullOrEmpty(tcr.OwnerId))
                    {
                        var notif = new Notification
                        {
                            Title = "Task Creation Request",
                            Message = $"{User.Identity?.Name} requested to create a task in project '{project?.Name}'.",
                            TriggerTime = DateTime.Now,
                            CreatedAt = DateTime.Now,
                            IsRead = false,
                            UserId = tcr.OwnerId
                        };
                        _context.Notifications.Add(notif);
                        await _context.SaveChangesAsync();
                        await _hubContext.Clients.User(tcr.OwnerId).SendAsync("ReceiveNotification", notif.Title, notif.Message, "task-request", tcr.Id);
                    }
                }
                if (task.AssigneeId != userId)
                {
                    var notif = new Notification
                    {
                        Title = "New Task",
                        Message = "You have been assigned a new task!",
                        TriggerTime = DateTime.Now,
                        CreatedAt = DateTime.Now,
                        IsRead = false,
                        UserId = task.AssigneeId
                    };
                    _context.Notifications.Add(notif);
                    await _context.SaveChangesAsync();

                    await _hubContext.Clients.User(task.AssigneeId)
                        .SendAsync("ReceiveNotification", notif.Title, notif.Message, task.Title);
                }

                return RedirectToAction(nameof(Index), new { projectId = task.ProjectId });
            }

            var projects = await _projectRepo.GetAllAsync(userId);
            ViewBag.ProjectId = new SelectList(projects, "Id", "Name", task.ProjectId);
            
            // Re-bind AssigneeId drop down just in case of validation failed
            var assignees = new List<object>();
            if (task.ProjectId.HasValue)
            {
               var proj = await _context.Projects.FirstOrDefaultAsync(p => p.Id == task.ProjectId);
               if (proj != null)
               {
                   var owner = await _context.Users.FirstOrDefaultAsync(u => u.Id == proj.UserId);
                   if (owner != null) assignees.Add(new { Id = owner.Id, DisplayName = owner.Email });

                   var members = await _context.ProjectMembers
                       .Where(pm => pm.ProjectId == task.ProjectId.Value)
                       .Join(_context.Users, pm => pm.UserId, u => u.Id, (pm, u) => new { Id = u.Id, DisplayName = u.Email })
                       .ToListAsync();
                   assignees.AddRange(members.Where(m => m.Id != owner.Id));
               }
            }
            ViewBag.AssigneeId = new SelectList(assignees, "Id", "DisplayName", task.AssigneeId);

            return View(task);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuickCreate(int projectId, int boardListId, string title)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(title))
            {
                return Json(new { success = false, message = "Title is required" });
            }

            try
            {
                // Xác định status dựa trên BoardListId
                var projectLists = await _context.BoardLists
                        .AsNoTracking()
                        .Where(bl => bl.ProjectId == projectId)
                        .OrderBy(bl => bl.Position)
                        .Select(bl => bl.Id)
                        .ToListAsync();

                Models.TaskStatus status = Models.TaskStatus.Todo;
                if (projectLists.Any())
                {
                    if (boardListId == projectLists.Last()) status = Models.TaskStatus.Completed;
                    else if (boardListId != projectLists.First()) status = Models.TaskStatus.InProgress;
                }

                // Tìm Position lớn nhất để chèn vào cuối cột
                var maxPosition = await _context.WorkTasks
                    .Where(t => t.BoardListId == boardListId)
                    .OrderByDescending(t => t.Position)
                    .Select(t => (int?)t.Position)
                    .FirstOrDefaultAsync() ?? -1;

                var task = new WorkTask
                {
                    Title = title,
                    ProjectId = projectId,
                    BoardListId = boardListId,
                    UserId = userId,
                    AssigneeId = userId, // Assign to self by default
                    StartDate = DateTime.Now,
                    EndDate = DateTime.Now.AddDays(1),
                    Priority = Priority.Medium,
                    Status = status,
                    Progress = status == Models.TaskStatus.Completed ? 100 : 0,
                    Position = maxPosition + 1
                };

                await _taskRepo.AddAsync(task);
                await _taskRepo.SaveAsync();

                await NotifyProjectUsersAboutNewTaskAsync(task, userId);

                var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                var userName = currentUser?.UserName ?? "U";

                return Json(new { 
                    success = true, 
                    task = new {
                        id = task.Id,
                        title = task.Title,
                        priorityClass = "pastel-warning",
                        priorityText = task.Priority.ToString(),
                        assigneeInitial = userName.Substring(0, 1).ToUpper(),
                        assigneeName = userName,
                        endDate = task.EndDate.ToString("MMM dd, yyyy"),
                        progress = task.Progress
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetProjectMembers(int projectId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            
            var assignees = new List<object>();

            if (project == null || projectId == 0) 
            {
                var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == currentUserId);
                if (currentUser != null) assignees.Add(new { Id = currentUser.Id, DisplayName = currentUser.Email + " (You)" });
                return Json(assignees);
            }

            if (project.UserId == currentUserId)
            {
                // Is Owner
                var owner = await _context.Users.FirstOrDefaultAsync(u => u.Id == project.UserId);
                if (owner != null) assignees.Add(new { Id = owner.Id, DisplayName = owner.Email + " (Owner)" });

                var members = await _context.ProjectMembers
                    .Where(pm => pm.ProjectId == projectId)
                    .Join(_context.Users, pm => pm.UserId, u => u.Id, (pm, u) => new { Id = u.Id, DisplayName = u.Email })
                    .ToListAsync();

                assignees.AddRange(members.Where(m => m.Id != owner.Id));
            }
            else
            {
                // Is merely a member
                var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == currentUserId);
                if (currentUser != null) assignees.Add(new { Id = currentUser.Id, DisplayName = currentUser.Email + " (You)" });
            }
            
            return Json(assignees);
        }

        // --- 3. CẬP NHẬT VỊ TRÍ (Drag & Drop) ---
        [HttpPost]
        public async Task<IActionResult> UpdateTaskPosition(int taskId, int? newListId, int newPosition)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            var isAdmin = User.IsInRole("Admin");
            // Check if user is creator, assignee, or project owner
            var task = await _context.WorkTasks
                .Include(t => t.Project)
                .ThenInclude(p => p.Members)
                .FirstOrDefaultAsync(t => t.Id == taskId && 
                    (isAdmin || t.UserId == userId || t.AssigneeId == userId || 
                    (t.Project != null && (t.Project.UserId == userId || (!t.IsPrivate && t.Project.Members.Any(m => m.UserId == userId))))));

            if (task == null) return Json(new { success = false, message = "Task not found or you do not have permission to update" });

            // If the user is not the owner and is attempting to modify a task assigned to them that they did not create,
            // create a TaskChangeRequest instead of applying the change immediately.
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isOwner = task.Project != null && task.Project.UserId == currentUserId;

            // If the user is not the owner and is attempting to modify a task assigned to them that they did not create,
            // create a TaskChangeRequest instead of applying the change immediately. The client that initiated the move
            // should still update its UI optimistically, but the DB update will wait for owner approval.
            if (!isOwner && task.AssigneeId == currentUserId && task.UserId != currentUserId)
            {
                var payload = new { NewListId = newListId, NewPosition = newPosition };
                var tcr = new Models.TaskChangeRequest
                {
                    TaskId = taskId,
                    RequesterId = currentUserId,
                    OwnerId = task.Project?.UserId ?? string.Empty,
                    Action = Models.TaskChangeAction.Edit,
                    Payload = JsonSerializer.Serialize(payload),
                    Status = Models.TaskChangeStatus.Pending
                };
                _context.TaskChangeRequests.Add(tcr);
                await _context.SaveChangesAsync();

                // Notify owner about the change request
                if (!string.IsNullOrEmpty(tcr.OwnerId) && tcr.OwnerId != userId)
                {
                    var notif = new Notification
                    {
                        Title = "Task Change Request",
                        Message = $"User has requested to modify task {task.Title}.",
                        TriggerTime = DateTime.Now,
                        CreatedAt = DateTime.Now,
                        IsRead = false,
                        UserId = tcr.OwnerId
                    };
                    _context.Notifications.Add(notif);
                    await _context.SaveChangesAsync();

                    await _hubContext.Clients.User(tcr.OwnerId).SendAsync("ReceiveNotification", notif.Title, notif.Message, task.Title);
                    // Also notify owner that a task change has been requested so they can mark pending in UI
                    await _hubContext.Clients.User(tcr.OwnerId).SendAsync("TaskChangeRequested", task.Id, tcr.Id);
                }

                return Json(new { success = true, pending = true, message = "Change request submitted and awaiting owner approval" });
            }

            try
            {
                // Cập nhật BoardListId và Status dựa trên thứ tự cột của dự án thay vì dựa vào tên cột
                int? oldBoardListId = task.BoardListId;
                task.BoardListId = newListId;
                if (newListId.HasValue && task.ProjectId.HasValue)
                {
                    // Lấy tất cả các cột của dự án này, sắp xếp theo Position
                    var projectLists = await _context.BoardLists
                        .AsNoTracking()
                        .Where(bl => bl.ProjectId == task.ProjectId.Value)
                        .OrderBy(bl => bl.Position)
                        .Select(bl => bl.Id)
                        .ToListAsync();

                    if (projectLists.Any())
                    {
                        if (newListId.Value == projectLists.First())
                        {
                            task.Status = Models.TaskStatus.Todo;
                            if (task.Progress >= 100) task.Progress = 0;
                        }
                        else if (newListId.Value == projectLists.Last())
                        {
                            task.Status = Models.TaskStatus.Completed;
                            task.Progress = 100;
                        }
                        else
                        {
                            task.Status = Models.TaskStatus.InProgress;
                            if (task.Progress >= 100) task.Progress = 90;
                        }
                    }
                }

                task.Position = newPosition;
                _taskRepo.Update(task);
                await _taskRepo.SaveAsync();

                // Ghi nhận lịch sử nếu cột thay đổi
                if (oldBoardListId != newListId)
                {
                    var history = new TaskHistory
                    {
                        WorkTaskId = task.Id,
                        OldBoardListId = oldBoardListId,
                        NewBoardListId = newListId,
                        ChangedAt = DateTime.Now,
                        ChangedByUserId = userId
                    };
                    _context.TaskHistories.Add(history);
                    await _context.SaveChangesAsync();
                }

                // Notify project members about the move so owners see it immediately
                try
                {
                    if (task.Project != null)
                    {
                        var recipientIds = task.Project.Members.Select(m => m.UserId)
                            .Append(task.Project.UserId)
                            .Where(id => !string.IsNullOrEmpty(id))
                            .Distinct()
                            .Where(id => id != userId) // exclude actor
                            .ToList();

                        if (recipientIds.Any())
                        {
                            var notif = new Notification
                            {
                                Title = "Task Updated",
                                Message = $"Task '{task.Title}' was moved by {User.Identity?.Name}.",
                                TriggerTime = DateTime.Now,
                                CreatedAt = DateTime.Now,
                                IsRead = false
                            };

                            // Create notifications for each recipient
                            var notifications = recipientIds.Select(rid => new Notification
                            {
                                Title = notif.Title,
                                Message = notif.Message + "||TaskMove",
                                TriggerTime = notif.TriggerTime,
                                CreatedAt = notif.CreatedAt,
                                IsRead = false,
                                UserId = rid
                            }).ToList();

                            _context.Notifications.AddRange(notifications);
                            await _context.SaveChangesAsync();

                            foreach (var rid in recipientIds)
                            {
                                await _hubContext.Clients.User(rid)
                                    .SendAsync("ReceiveNotification", notif.Title, notif.Message, task.Title);
                                // Also send a lightweight event so clients can update Kanban UI
                                await _hubContext.Clients.User(rid).SendAsync("TaskChangeApplied", task.Id);
                            }
                        }
                    }
                }
                catch { /* swallow notification exceptions */ }

                // Auto-sync: if task moved to Completed, recalculate related goals progress
                try
                {
                    if (task.Status == Models.TaskStatus.Completed)
                    {
                        var relatedGoalIds = await _context.GoalTasks
                            .Where(gt => gt.WorkTaskId == task.Id)
                            .Select(gt => gt.GoalId)
                            .Distinct()
                            .ToListAsync();

                        foreach (var gid in relatedGoalIds)
                        {
                            // Recalculate using GoalService to ensure history and status updated
                            try { await _goalService.RecalculateProgressForGoalAsync(gid, task.Project?.UserId ?? task.UserId); } catch { }
                        }
                    }
                }
                catch { /* non-critical */ }

                return Json(new { success = true, newProgress = task.Progress });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
        // GET: WorkTask/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            // Eager load Project and TimeLogs
            var task = await _context.WorkTasks
                .Include(t => t.Project)
                .ThenInclude(p => p.Members)
                .Include(t => t.TimeLogs)
                .FirstOrDefaultAsync(t => t.Id == id && 
                    (isAdmin || t.UserId == userId || t.AssigneeId == userId || 
                    (t.Project != null && (t.Project.UserId == userId || (!t.IsPrivate && t.Project.Members.Any(m => m.UserId == userId))))));
            
            if (task == null)
            {
                return NotFound();
            }

            // If current user is assignee but not creator and not project owner, editing requires owner approval.
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isProjectOwner = task.Project != null && task.Project.UserId == currentUserId;
            if (!isProjectOwner && task.AssigneeId == currentUserId && task.UserId != currentUserId)
            {
                // Present a read-only view with a warning and option to submit change request
                ViewBag.RequiresApproval = true;
            }

            // Giải mã nếu private (và nếu người dùng là Owner hoặc Assignee)
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

            return View(task);
        }

        // GET: WorkTask/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            var task = await _context.WorkTasks
                .Include(t => t.Project)
                .ThenInclude(p => p.Members)
                .FirstOrDefaultAsync(t => t.Id == id && 
                    (isAdmin || t.UserId == userId || t.AssigneeId == userId || 
                    (t.Project != null && (t.Project.UserId == userId || (!t.IsPrivate && t.Project.Members.Any(m => m.UserId == userId))))));

            if (task == null)
            {
                return NotFound();
            }

            // Phải giải mã nó ra thì lên form người dùng mới đọc đc và sửa lại đc
            if (task.IsPrivate && !string.IsNullOrEmpty(task.Description))
            {
                // Chỉ người tạo hoặc assignee mới đc sửa đúng data
                if (userId == task.UserId || userId == task.AssigneeId)
                {
                    task.Description = _crypto.Decrypt(task.Description);
                }
                else
                {
                    task.Description = ""; // Ko cho mk edit content của ng khác
                }
            }

            var projects = await _projectRepo.GetAllAsync(userId);
            ViewBag.ProjectId = new SelectList(projects, "Id", "Name", task.ProjectId);

            // Fetch assignees
            var assignees = new List<object>();
            if (task.ProjectId.HasValue)
            {
                var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == task.ProjectId);
                if (project != null)
                {
                    if (project.UserId == userId) 
                    {
                        var owner = await _context.Users.FirstOrDefaultAsync(u => u.Id == project.UserId);
                        if (owner != null) assignees.Add(new { Id = owner.Id, DisplayName = owner.Email + " (Owner)" });

                        var members = await _context.ProjectMembers
                            .Where(pm => pm.ProjectId == task.ProjectId.Value)
                            .Join(_context.Users, pm => pm.UserId, u => u.Id, (pm, u) => new { Id = u.Id, DisplayName = u.Email })
                            .ToListAsync();
                        
                        assignees.AddRange(members.Where(m => m.Id != owner.Id));
                    }
                    else
                    {
                        var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                        if (currentUser != null) assignees.Add(new { Id = currentUser.Id, DisplayName = currentUser.Email + " (You)" });
                    }
                }
            }
            else
            {
                var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (currentUser != null) assignees.Add(new { Id = currentUser.Id, DisplayName = currentUser.Email + " (You)" });
            }
            ViewBag.AssigneeId = new SelectList(assignees, "Id", "DisplayName", task.AssigneeId);

            return View(task);
        }

        // POST: WorkTask/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, WorkTask taskForm)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (id != taskForm.Id)
            {
                return NotFound();
            }

            ModelState.Remove("UserId");
            ModelState.Remove("Project");
            ModelState.Remove("TimeLogs");

            if (taskForm.StartDate > taskForm.EndDate)
            {
                ModelState.AddModelError("EndDate", "End date must be greater than or equal to start date.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // KHẮC PHỤC BUG (2): Lấy Data cũ để đối chiếu quyền và tránh ghi đè mô tả mật
                    var dbTask = await _context.WorkTasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
                    if (dbTask != null && dbTask.IsPrivate)
                    {
                        if (userId != dbTask.UserId && userId != dbTask.AssigneeId)
                        {
                            // Nếu không có quyền, giữ nguyên trạng thái mã hóa gốc từ DB
                            taskForm.Description = dbTask.Description;
                        }
                        else if (!string.IsNullOrEmpty(taskForm.Description))
                        {
                            taskForm.Description = _crypto.Encrypt(taskForm.Description);
                        }
                        else 
                        {
                            taskForm.Description = string.Empty;
                        }
                    }
                    else if (taskForm.IsPrivate && !string.IsNullOrEmpty(taskForm.Description))
                    {
                        taskForm.Description = _crypto.Encrypt(taskForm.Description);
                    }

                    var isAdmin = User.IsInRole("Admin");
                    // If requester is assignee but not creator/owner, create change request instead of direct update
                    var isOwner = dbTask != null && dbTask.ProjectId.HasValue && (await _context.Projects.FindAsync(dbTask.ProjectId.Value))?.UserId == userId;
                    if (dbTask != null && dbTask.AssigneeId == userId && dbTask.UserId != userId && !isOwner)
                    {
                        // Build payload with proposed changes
                        var payloadObj = new {
                            Title = taskForm.Title,
                            Description = taskForm.Description,
                            EndDate = taskForm.EndDate,
                            Priority = taskForm.Priority,
                            Progress = taskForm.Progress
                        };
                            var tcr = new Models.TaskChangeRequest
                        {
                            TaskId = id,
                            RequesterId = userId,
                                OwnerId = (await _context.Projects.FindAsync(dbTask.ProjectId.Value))?.UserId ?? string.Empty,
                            Action = Models.TaskChangeAction.Edit,
                            Payload = JsonSerializer.Serialize(payloadObj),
                            Status = Models.TaskChangeStatus.Pending
                        };
                        _context.TaskChangeRequests.Add(tcr);
                        await _context.SaveChangesAsync();

                        // Notify owner via Notifications and SignalR
                        if (!string.IsNullOrEmpty(tcr.OwnerId))
                        {
                            var notif = new Notification
                            {
                                Title = "Task Change Request",
                                Message = $"{User.Identity?.Name} requested changes to task '{dbTask.Title}'.",
                                TriggerTime = DateTime.Now,
                                CreatedAt = DateTime.Now,
                                IsRead = false,
                                UserId = tcr.OwnerId
                            };
                            _context.Notifications.Add(notif);
                            await _context.SaveChangesAsync();
                            await _hubContext.Clients.User(tcr.OwnerId).SendAsync("ReceiveNotification", notif.Title, notif.Message, dbTask.Title);
                        }

                        TempData["SuccessMessage"] = "Change request submitted to project owner for approval.";
                        return RedirectToAction(nameof(Index), new { projectId = taskForm.ProjectId });
                    }

                    var success = await _taskRepo.UpdateTaskDetailsAsync(id, userId, taskForm, isAdmin);
                    if (!success)
                    {
                        return NotFound();
                    }

                    // BẮN THÔNG BÁO SIGNALR NẾU ASSIGNEE KHÁC NGƯỜI SỬA
                    if (!string.IsNullOrEmpty(taskForm.AssigneeId) && taskForm.AssigneeId != userId)
                    {
                        var notif = new Notification
                        {
                            Title = "New Assignment",
                            Message = $"One of your tasks has just been updated!||Task: {taskForm.Title}",
                            TriggerTime = DateTime.Now,
                            CreatedAt = DateTime.Now,
                            IsRead = false,
                            UserId = taskForm.AssigneeId
                        };
                        _context.Notifications.Add(notif);
                        await _context.SaveChangesAsync();

                        await _hubContext.Clients.User(taskForm.AssigneeId)
                            .SendAsync("ReceiveNotification", notif.Title, "One of your tasks has just been updated!", taskForm.Title);
                    }

                    TempData["SuccessMessage"] = "Task updated successfully!";
                    return RedirectToAction(nameof(Index), new { projectId = taskForm.ProjectId });
                }
                catch (DbUpdateConcurrencyException)
                {
                    ModelState.AddModelError(string.Empty, "Data has been changed by someone else. Please reload the page.");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, "An error occurred during update: " + ex.Message);
                }
            }

            // Nếu form lỗi, load lại danh sách project để hiển thị lại View
            var projects = await _projectRepo.GetAllAsync(userId);
            ViewBag.ProjectId = new SelectList(projects, "Id", "Name", taskForm.ProjectId);

            // Re-bind AssigneeId
            var assignees = new List<object>();
            if (taskForm.ProjectId.HasValue)
            {
               var proj = await _context.Projects.FirstOrDefaultAsync(p => p.Id == taskForm.ProjectId);
               if (proj != null)
               {
                   var owner = await _context.Users.FirstOrDefaultAsync(u => u.Id == proj.UserId);
                   if (owner != null) assignees.Add(new { Id = owner.Id, DisplayName = owner.Email });

                   var members = await _context.ProjectMembers
                       .Where(pm => pm.ProjectId == taskForm.ProjectId.Value)
                       .Join(_context.Users, pm => pm.UserId, u => u.Id, (pm, u) => new { Id = u.Id, DisplayName = u.Email })
                       .ToListAsync();
                   assignees.AddRange(members.Where(m => m.Id != owner.Id));
               }
            }
            ViewBag.AssigneeId = new SelectList(assignees, "Id", "DisplayName", taskForm.AssigneeId);

            return View(taskForm);
        }

        // GET: WorkTask/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");

            // CẢI THIỆN: Load kèm Project để trang Delete hiện tên dự án thay vì hiện ID
            var task = await _context.WorkTasks
                .Include(t => t.Project)
                .ThenInclude(p => p.Members)
                .FirstOrDefaultAsync(t => t.Id == id && 
                    (isAdmin || t.UserId == userId || t.AssigneeId == userId || 
                    (t.Project != null && (t.Project.UserId == userId || (!t.IsPrivate && t.Project.Members.Any(m => m.UserId == userId))))));

            if (task == null)
            {
                return NotFound();
            }
            return View(task);
        }

        // POST: WorkTask/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            try
            {
                // Lấy thông tin task trước khi xóa để lấy projectId
                var task = await _context.WorkTasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
                int? projectId = task?.ProjectId;

                var isAdmin = User.IsInRole("Admin");
                var success = await _taskRepo.DeleteTaskAsync(id, userId, isAdmin);
                if (!success)
                {
                    return NotFound();
                }

                TempData["SuccessMessage"] = "Task deleted successfully!";
                return RedirectToAction(nameof(Index), new { projectId = projectId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Cannot delete this task. It may contain other linked data: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
