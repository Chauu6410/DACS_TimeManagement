using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace DACS_TimeManagement.Models
{
    public enum Priority { Low, Medium, High, Urgent }
    public enum TaskStatus { Todo, InProgress, Completed, Overdue }

    public class WorkTask
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Title is required")]
        [StringLength(100, ErrorMessage = "Title cannot exceed 100 characters")]
        public string Title { get; set; }

        public string? Description { get; set; }

        [Required(ErrorMessage = "Start date is required")]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "End date is required")]
        public DateTime EndDate { get; set; }

        public string? Color { get; set; }

        public Priority Priority { get; set; }
        public TaskStatus Status { get; set; }

        [Range(0, 100, ErrorMessage = "Progress must be between 0 and 100")]
        public int Progress { get; set; } // % hoàn thành (0-100)

        public int? ProjectId { get; set; }
        [JsonIgnore]
        public Project? Project { get; set; }

        // Position within a board column for ordering
        public int Position { get; set; }

        // Optional relation to a BoardList (column) so a task can belong to a column
        public int? BoardListId { get; set; }
        [JsonIgnore]
        public BoardList? BoardList { get; set; }

        public string? UserId { get; set; } // Người tạo task

        // Người được giao việc (Assignee)
        public string? AssigneeId { get; set; }

        [ForeignKey("UserId")]
        public virtual IdentityUser? Creator { get; set; }

        [ForeignKey("AssigneeId")]
        public virtual IdentityUser? Assignee { get; set; }

        public bool IsPrivate { get; set; } = false; // Đánh dấu thẻ là riêng tư

        public ICollection<TimeLog> TimeLogs { get; set; } = new List<TimeLog>();
        public ICollection<ScheduledEvent> ScheduledEvents { get; set; } = new List<ScheduledEvent>();

    }
}