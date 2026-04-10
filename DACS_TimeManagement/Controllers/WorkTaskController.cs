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

                // 3. CHỈ LOAD DỮ LIỆU CỦA 1 PROJECT ĐANG CHỌN (Tăng tốc độ 500%)
                var boardLists = await _context.BoardLists
                    .AsNoTracking()
                    .Where(bl => bl.ProjectId == selectedId) // Lọc chặt chẽ theo ID
                    .OrderBy(bl => bl.Position)
                    .ToListAsync();

                var tasks = await _context.WorkTasks
                    .AsNoTracking()
                    .Include(t => t.Assignee)
                    .Where(t => t.ProjectId == selectedId && (t.UserId == currentUserId || t.AssigneeId == currentUserId))
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
        public async Task<IActionResult> Create()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            // 1. Lấy danh sách Project của người đang đăng nhập
            var projects = await _projectRepo.GetAllAsync(userId);
            ViewBag.ProjectId = new SelectList(projects, "Id", "Name");
            // 2. Lấy danh sách tất cả User trong hệ thống để chọn Người thực hiện
            // (Cần inject UserManager vào Controller)
            var allUsers = await _context.Users.Select(u => new {
                Id = u.Id,
                DisplayName = u.Email // Hoặc u.UserName
            }).ToListAsync();
            ViewBag.AssigneeId = new SelectList(allUsers, "Id", "DisplayName");
            return View();
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
                ModelState.AddModelError("EndDate", "Ngày kết thúc phải lớn hơn ngày bắt đầu.");
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
                        .SendAsync("ReceiveNotification", "Bạn được giao việc mới!", task.Title);
                }

                return RedirectToAction(nameof(Index));
            }

            var projects = await _projectRepo.GetAllAsync(userId);
            ViewBag.ProjectId = new SelectList(projects, "Id", "Name", task.ProjectId);
            return View(task);
        }

        // --- 3. CẬP NHẬT VỊ TRÍ (Drag & Drop) ---
        [HttpPost]
        public async Task<IActionResult> UpdateTaskPosition(int taskId, int? newListId, int newPosition)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var task = await _taskRepo.GetByIdAsync(taskId, userId);

            if (task == null) return Json(new { success = false, message = "Không tìm thấy công việc" });

            try
            {
                // Cập nhật BoardListId và Status dựa trên tên cột
                task.BoardListId = newListId;
                var targetList = await _context.BoardLists.FindAsync(newListId);
                if (targetList != null)
                {
                    var name = targetList.Name.ToLower();
                    if (name.Contains("done") || name.Contains("hoàn thành")) task.Status = Models.TaskStatus.Completed;
                    else if (name.Contains("doing") || name.Contains("tiến độ")) task.Status = Models.TaskStatus.InProgress;
                    else task.Status = Models.TaskStatus.Todo;
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
                    task.Description = "Mô tả công việc này đã được bảo mật (Private).";
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
                ModelState.AddModelError("EndDate", "Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Mã hóa lại trước khi cập nhật
                    if (taskForm.IsPrivate && !string.IsNullOrEmpty(taskForm.Description))
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
                            .SendAsync("ReceiveNotification", "Một thẻ công việc của bạn vừa được cập nhật!", taskForm.Title);
                    }

                    TempData["SuccessMessage"] = "Cập nhật nhiệm vụ thành công!";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    ModelState.AddModelError(string.Empty, "Dữ liệu đã bị thay đổi bởi người khác. Vui lòng tải lại trang.");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, "Đã xảy ra lỗi khi cập nhật: " + ex.Message);
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

                TempData["SuccessMessage"] = "Đã xóa nhiệm vụ thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                // CẢI THIỆN: Thay vì kẹt ở trang Delete, trả về Index và báo lỗi
                TempData["ErrorMessage"] = "Không thể xóa nhiệm vụ này. Có thể nó đang chứa dữ liệu liên kết khác: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }
    }
}