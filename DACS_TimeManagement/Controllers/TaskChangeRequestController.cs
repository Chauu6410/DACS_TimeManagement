using DACS_TimeManagement.Models;
using DACS_TimeManagement.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Security.Claims;

namespace DACS_TimeManagement.Controllers
{
    [Authorize]
    public class TaskChangeRequestController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;

        public TaskChangeRequestController(ApplicationDbContext context, IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        // Owner view: list pending requests for projects they own
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var pending = await _context.TaskChangeRequests
                .Where(r => r.OwnerId == userId && r.Status == TaskChangeStatus.Pending)
                .OrderBy(r => r.CreatedAt)
                .ToListAsync();

            return View(pending);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var req = await _context.TaskChangeRequests.FirstOrDefaultAsync(r => r.Id == id && r.OwnerId == userId);
            if (req == null) return NotFound();

            // apply based on action
            if (req.Action == TaskChangeAction.Create)
            {
                try
                {
                    var payload = JsonSerializer.Deserialize<JsonElement>(req.Payload ?? "{}");
                    var title = payload.GetProperty("Title").GetString();
                    var projectId = payload.GetProperty("ProjectId").GetInt32();
                    var boardListId = payload.GetProperty("BoardListId").GetInt32();

                    var task = new WorkTask
                    {
                        Title = title,
                        ProjectId = projectId,
                        BoardListId = boardListId,
                        UserId = req.RequesterId,
                        AssigneeId = req.RequesterId,
                        StartDate = DateTime.Now,
                        EndDate = DateTime.Now.AddDays(1),
                        Priority = Models.Priority.Medium,
                        Status = Models.TaskStatus.Todo,
                        Progress = 0
                    };
                    _context.WorkTasks.Add(task);
                    await _context.SaveChangesAsync();

                    // update request to link created task
                    req.TaskId = task.Id;
                }
                catch { /* ignore malformed */ }
            }
            else if (req.Action == TaskChangeAction.Edit)
            {
                var task = await _context.WorkTasks.FirstOrDefaultAsync(t => t.Id == req.TaskId);
                if (task != null)
                {
                    try
                    {
                        var payload = JsonSerializer.Deserialize<JsonElement>(req.Payload ?? "{}");
                        if (payload.TryGetProperty("Title", out var p)) task.Title = p.GetString() ?? task.Title;
                        if (payload.TryGetProperty("Description", out p)) task.Description = p.GetString() ?? task.Description;
                        if (payload.TryGetProperty("EndDate", out p) && p.ValueKind == JsonValueKind.String)
                        {
                            if (DateTime.TryParse(p.GetString(), out var d)) task.EndDate = d;
                        }
                        if (payload.TryGetProperty("Priority", out p))
                        {
                            // Priority may be serialized as number or string
                            var raw = p.GetRawText().Trim('"');
                            if (int.TryParse(raw, out var prVal)) task.Priority = (Priority)prVal;
                            else if (Enum.TryParse<Priority>(raw, out var pr)) task.Priority = pr;
                        }
                        // Handle assignee
                        if (payload.TryGetProperty("AssigneeId", out p) && p.ValueKind == JsonValueKind.String)
                        {
                            task.AssigneeId = p.GetString();
                        }
                        // Handle progress/status
                        if (payload.TryGetProperty("Progress", out p) && p.ValueKind == JsonValueKind.Number)
                        {
                            if (p.TryGetInt32(out var prog)) { task.Progress = prog; }
                        }
                        if (payload.TryGetProperty("Status", out p) && p.ValueKind == JsonValueKind.String)
                        {
                            var sraw = p.GetString();
                            if (!string.IsNullOrEmpty(sraw))
                            {
                                try
                                {
                                    var parsed = Enum.Parse(typeof(DACS_TimeManagement.Models.TaskStatus), sraw, true);
                                    if (parsed is DACS_TimeManagement.Models.TaskStatus parsedStatus) task.Status = parsedStatus;
                                }
                                catch { }
                            }
                        }
                        // Handle move (NewListId/NewPosition)
                        if (payload.TryGetProperty("NewListId", out p) && p.ValueKind == JsonValueKind.Number)
                        {
                            if (p.TryGetInt32(out var nl)) task.BoardListId = nl;
                        }
                        if (payload.TryGetProperty("NewPosition", out p) && p.ValueKind == JsonValueKind.Number)
                        {
                            if (p.TryGetInt32(out var np)) task.Position = np;
                        }
                        _context.WorkTasks.Update(task);
                        await _context.SaveChangesAsync();
                    }
                    catch { }
                }
            }
            else if (req.Action == TaskChangeAction.Delete)
            {
                var task = await _context.WorkTasks.FirstOrDefaultAsync(t => t.Id == req.TaskId);
                if (task != null)
                {
                    _context.WorkTasks.Remove(task);
                    await _context.SaveChangesAsync();
                }
            }

            req.Status = TaskChangeStatus.Approved;
            req.ReviewedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // notify requester
            if (!string.IsNullOrEmpty(req.RequesterId))
            {
                var notif = new Notification
                {
                    Title = "Task Change Approved",
                    Message = $"Your requested change (#{req.Id}) was approved by the owner.",
                    TriggerTime = DateTime.Now,
                    CreatedAt = DateTime.Now,
                    IsRead = false,
                    UserId = req.RequesterId
                };
                _context.Notifications.Add(notif);
                await _context.SaveChangesAsync();
                await _hubContext.Clients.User(req.RequesterId).SendAsync("ReceiveNotification", notif.Title, notif.Message, "task-change-approved");
            }

            // Notify all project members that the task was updated/created/deleted so they can refresh UI
            if (req.TaskId != 0)
            {
                var workTask = await _context.WorkTasks.FindAsync(req.TaskId);
                Project project = null;
                if (workTask != null)
                {
                    project = await _context.Projects.Include(p => p.Members).FirstOrDefaultAsync(p => p.Id == workTask.ProjectId);
                }

                if (project != null)
                {
                    var recipientIds = project.Members.Select(m => m.UserId).Append(project.UserId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
                    foreach (var rid in recipientIds)
                    {
                        await _hubContext.Clients.User(rid).SendAsync("TaskChangeApplied", req.TaskId);
                    }
                }
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var req = await _context.TaskChangeRequests.FirstOrDefaultAsync(r => r.Id == id && r.OwnerId == userId);
            if (req == null) return NotFound();

            req.Status = TaskChangeStatus.Rejected;
            req.ReviewedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(req.RequesterId))
            {
                var notif = new Notification
                {
                    Title = "Task Change Rejected",
                    Message = $"Your requested change (#{req.Id}) was rejected by the owner.",
                    TriggerTime = DateTime.Now,
                    CreatedAt = DateTime.Now,
                    IsRead = false,
                    UserId = req.RequesterId
                };
                _context.Notifications.Add(notif);
                await _context.SaveChangesAsync();
                await _hubContext.Clients.User(req.RequesterId).SendAsync("ReceiveNotification", notif.Title, notif.Message, "task-change-rejected");
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
