using Microsoft.AspNetCore.Identity;

namespace DACS_TimeManagement.Models
{
    public class ProjectDiscussion
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string UserId { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }

        public Project Project { get; set; }
        public IdentityUser User { get; set; }
        // Attachment info
        public string? AttachmentFileName { get; set; } // stored filename on server
        public string? AttachmentOriginalName { get; set; } // original filename for display
        public string? AttachmentContentType { get; set; }
        public long? AttachmentSize { get; set; }
    }
}
