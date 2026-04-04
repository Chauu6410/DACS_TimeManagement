using DACS_TimeManagement.Models;
using DACS_TimeManagement.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DACS_TimeManagement.Controllers
{
    [Authorize]
    public class WorkTaskController : Controller
    {
        private readonly IWorkTaskRepository _taskRepo;
        private readonly IProjectRepository _projectRepo;

        public WorkTaskController(IWorkTaskRepository taskRepo, IProjectRepository projectRepo)
        {
            _taskRepo = taskRepo;
            _projectRepo = projectRepo;
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            // Sử dụng FindAsync để lấy kèm Project nhằm hiển thị tên dự án ở View
            var tasks = await _taskRepo.FindAsync(t => t.UserId == userId, t => t.Project);
            return View(tasks);
        }

        public async Task<IActionResult> Create()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var projects = await _projectRepo.GetAllAsync(userId);
            ViewBag.ProjectId = new SelectList(projects, "Id", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(WorkTask task)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Xóa validate UserId do trường này không nằm trên Form (sẽ gán thủ công ở dưới)
            ModelState.Remove("UserId");
            ModelState.Remove("Project");
            ModelState.Remove("TimeLogs");

            if (task.StartDate > task.EndDate)
            {
                ModelState.AddModelError("EndDate", "Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.");
            }

            if (ModelState.IsValid)
            {
                task.UserId = userId;
                await _taskRepo.AddAsync(task);
                await _taskRepo.SaveAsync();
                return RedirectToAction(nameof(Index));
            }
            var projects = await _projectRepo.GetAllAsync(userId);
            ViewBag.ProjectId = new SelectList(projects, "Id", "Name", task.ProjectId);
            return View(task);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProgress(int id, int progress)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var success = await _taskRepo.UpdateTaskProgressAsync(id, userId, progress);
            return Json(new { success });
        }

        // POST: /WorkTask/UpdateTaskPosition
        // Called when a task is dragged to a new list or reordered within the same list.
        [HttpPost]
        public async Task<IActionResult> UpdateTaskPosition(int taskId, int? newListId, int newPosition)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Load the task ensuring it belongs to the current user
            var task = await _taskRepo.GetByIdAsync(taskId, userId);
            if (task == null)
            {
                return Json(new { success = false, message = "Task not found" });
            }

            var oldListId = task.BoardListId;

            try
            {
                if (oldListId == newListId)
                {
                    // Reorder within the same list
                    var tasksInList = (await _taskRepo.FindAsync(t => t.BoardListId == newListId && t.UserId == userId))
                        .OrderBy(t => t.Position)
                        .ToList();

                    // Remove the moving task from the sequence
                    tasksInList.RemoveAll(t => t.Id == taskId);

                    // Clamp the insert position
                    var insertPos = Math.Max(0, Math.Min(newPosition, tasksInList.Count));

                    // Insert the task object at the new position
                    tasksInList.Insert(insertPos, task);

                    // Reassign positions
                    for (int i = 0; i < tasksInList.Count; i++)
                    {
                        tasksInList[i].Position = i;
                        _taskRepo.Update(tasksInList[i]);
                    }
                }
                else
                {
                    // Remove from old list (if any) and reindex
                    if (oldListId != null)
                    {
                        var oldTasks = (await _taskRepo.FindAsync(t => t.BoardListId == oldListId && t.UserId == userId))
                            .OrderBy(t => t.Position)
                            .ToList();

                        oldTasks.RemoveAll(t => t.Id == taskId);
                        for (int i = 0; i < oldTasks.Count; i++)
                        {
                            oldTasks[i].Position = i;
                            _taskRepo.Update(oldTasks[i]);
                        }
                    }

                    // Insert into new list and reindex
                    var newTasks = (await _taskRepo.FindAsync(t => t.BoardListId == newListId && t.UserId == userId))
                        .OrderBy(t => t.Position)
                        .ToList();

                    var insertPos = Math.Max(0, Math.Min(newPosition, newTasks.Count));

                    // Set task's new list id before inserting so updates reflect correctly
                    task.BoardListId = newListId;

                    newTasks.Insert(insertPos, task);
                    for (int i = 0; i < newTasks.Count; i++)
                    {
                        newTasks[i].Position = i;
                        _taskRepo.Update(newTasks[i]);
                    }
                }

                // Persist all changes
                var saved = await _taskRepo.SaveAsync();
                return Json(new { success = saved });
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
                    var success = await _taskRepo.UpdateTaskDetailsAsync(id, userId, taskForm);
                    if (!success)
                    {
                        return NotFound();
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