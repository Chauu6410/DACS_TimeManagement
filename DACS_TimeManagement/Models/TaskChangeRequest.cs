using System.ComponentModel.DataAnnotations;

namespace DACS_TimeManagement.Models
{
    public enum TaskChangeStatus { Pending, Approved, Rejected }
    public enum TaskChangeAction { Create, Edit, Delete }

    public class TaskChangeRequest
    {
        public int Id { get; set; }
        public int TaskId { get; set; }
        public string RequesterId { get; set; }
        public string OwnerId { get; set; }
        public TaskChangeAction Action { get; set; }
        // Payload contains JSON representation of proposed changes (for Edit)
        public string? Payload { get; set; }
        public TaskChangeStatus Status { get; set; } = TaskChangeStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReviewedAt { get; set; }
    }
}
