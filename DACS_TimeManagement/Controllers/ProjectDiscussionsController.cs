using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DACS_TimeManagement.Models;
using System.Security.Claims;

namespace DACS_TimeManagement.Controllers
{
    [Authorize]
    public class ProjectDiscussionsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public ProjectDiscussionsController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> GetComments(int projectId)
        {
            try
            {
                var comments = await _context.ProjectDiscussion
                    .Include(c => c.User)
                    .Where(c => c.ProjectId == projectId)
                    .OrderBy(c => c.CreatedAt)
                    .Select(c => new
                    {
                        c.Id,
                        c.UserId,
                        c.Content,
                        c.CreatedAt,
                        UserName = c.User != null ? c.User.UserName : "Unknown",
                        UserEmail = c.User != null ? c.User.Email : "",
                        AttachmentOriginalName = c.AttachmentOriginalName,
                        AttachmentFileName = c.AttachmentFileName,
                        AttachmentContentType = c.AttachmentContentType,
                        AttachmentSize = c.AttachmentSize
                    })
                    .ToListAsync();

                return Json(new { success = true, comments });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment([FromForm] AddCommentModel model)
        {
            try
            {
                if (model == null || string.IsNullOrWhiteSpace(model.Content))
                    return Json(new { success = false, message = "Comment content is required." });

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userId == null)
                    return Json(new { success = false, message = "User not authenticated." });

                var project = await _context.Projects.FindAsync(model.ProjectId);
                if (project == null)
                    return Json(new { success = false, message = "Project not found." });

                var comment = new ProjectDiscussion
                {
                    ProjectId = model.ProjectId,
                    UserId = userId,
                    Content = model.Content.Trim(),
                    CreatedAt = DateTime.UtcNow
                };

                // Handle file upload (optional)
                if (model.Attachment != null && model.Attachment.Length > 0)
                {
                    // ensure uploads folder exists
                    var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "project_discussions");
                    if (!Directory.Exists(uploadsRoot)) Directory.CreateDirectory(uploadsRoot);

                    var uniqueName = Guid.NewGuid().ToString() + Path.GetExtension(model.Attachment.FileName);
                    var filePath = Path.Combine(uploadsRoot, uniqueName);
                    using (var fs = new FileStream(filePath, FileMode.Create))
                    {
                        await model.Attachment.CopyToAsync(fs);
                    }

                    comment.AttachmentFileName = uniqueName;
                    comment.AttachmentOriginalName = model.Attachment.FileName;
                    comment.AttachmentContentType = model.Attachment.ContentType;
                    comment.AttachmentSize = model.Attachment.Length;
                }

                _context.ProjectDiscussion.Add(comment);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Comment added successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteComment(int id)
        {
            var comment = await _context.ProjectDiscussion.FindAsync(id);
            if (comment == null) return Json(new { success = false, message = "Comment not found." });
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (comment.UserId != userId) return Json(new { success = false, message = "Unauthorized." });

            // remove attachment file if exists
            try
            {
                if (!string.IsNullOrEmpty(comment.AttachmentFileName))
                {
                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "project_discussions", comment.AttachmentFileName);
                    if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
                }
            }
            catch { /* ignore */ }
            _context.ProjectDiscussion.Remove(comment);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Deleted." });
        }
    }

    public class AddCommentModel
    {
        public int ProjectId { get; set; }
        public string Content { get; set; }
        public IFormFile? Attachment { get; set; }
    }

}