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

        public WorkTaskController(IWorkTaskRepository taskRepo, IProjectRepository projectRepo, IHubContext<NotificationHub> hubContext, ICryptoService crypto, ApplicationDbContext context, ILogger<WorkTaskController> logger)
        {
            _taskRepo = taskRepo;
            _projectRepo = projectRepo;
            _hubContext = hubContext;
            _crypto = crypto;
            _context = context;
            _logger = logger;
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

                var tasks = await _context.WorkTasks
                    .AsNoTracking()
                    .Include(t => t.Assignee)
                    .Where(t => t.ProjectId == selectedId && (isProjectOwner || t.UserId == currentUserId || t.AssigneeId == currentUserId))
                    .ToListAsync();

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

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Lỗi load Kanban");
                return View(new Models.KanbanViewModel());
            }
        }

        // --- 2. TẠO MỚI TASK ---
        public async Task<IActionResult> Create(int? projectId)
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
            
            var task = new WorkTask { ProjectId = projectId };
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

                await _taskRepo.AddAsync(task);
                await _taskRepo.SaveAsync();

                if (task.AssigneeId != userId)
                {
                    await _hubContext.Clients.User(task.AssigneeId)
                        .SendAsync("ReceiveNotification", "You have been assigned a new task!", task.Title);
                }

                return RedirectToAction(nameof(Index));
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
            
            // Check if user is creator, assignee, or project owner
            var task = await _context.WorkTasks
                .Include(t => t.Project)
                .FirstOrDefaultAsync(t => t.Id == taskId && 
                    (t.UserId == userId || t.AssigneeId == userId || (t.Project != null && t.Project.UserId == userId)));

            if (task == null) return Json(new { success = false, message = "Task not found or you do not have permission to update" });

            try
            {
                // Cập nhật BoardListId và Status dựa trên thứ tự cột của dự án thay vì dựa vào tên cột
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
                        }
                        else if (newListId.Value == projectLists.Last())
                        {
                            task.Status = Models.TaskStatus.Completed;
                        }
                        else
                        {
                            task.Status = Models.TaskStatus.InProgress;
                        }
                    }
                }

                task.Position = newPosition;
                _taskRepo.Update(task);
                await _taskRepo.SaveAsync();

                return Json(new { success = true });
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
            // Eager load Project and TimeLogs
            var tasks = await _taskRepo.FindAsync(
                t => t.Id == id && t.UserId == userId,
                t => t.Project,
                t => t.TimeLogs);
            var task = tasks.FirstOrDefault();
            
            if (task == null)
            {
                return NotFound();
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
            var task = await _taskRepo.GetByIdAsync(id, userId);

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

                    var success = await _taskRepo.UpdateTaskDetailsAsync(id, userId, taskForm);
                    if (!success)
                    {
                        return NotFound();
                    }

                    // BẮN THÔNG BÁO SIGNALR NẾU ASSIGNEE KHÁC NGƯỜI SỬA
                    if (!string.IsNullOrEmpty(taskForm.AssigneeId) && taskForm.AssigneeId != userId)
                    {
                        await _hubContext.Clients.User(taskForm.AssigneeId)
                            .SendAsync("ReceiveNotification", "One of your tasks has just been updated!", taskForm.Title);
                    }

                    TempData["SuccessMessage"] = "Task updated successfully!";
                    return RedirectToAction(nameof(Index));
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
            return View(taskForm);
        }

        // GET: WorkTask/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // CẢI THIỆN: Load kèm Project để trang Delete hiện tên dự án thay vì hiện ID
            var tasks = await _taskRepo.FindAsync(t => t.Id == id && t.UserId == userId, t => t.Project);
            var task = tasks.FirstOrDefault();

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
                var success = await _taskRepo.DeleteTaskAsync(id, userId);
                if (!success)
                {
                    return NotFound();
                }

                TempData["SuccessMessage"] = "Task deleted successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                // CẢI THIỆN: Thay vì kẹt ở trang Delete, trả về Index và báo lỗi
                TempData["ErrorMessage"] = "Cannot delete this task. It may contain other linked data: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }
    }
}