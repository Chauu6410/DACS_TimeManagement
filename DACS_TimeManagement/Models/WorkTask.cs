using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DACS_TimeManagement.Models
{
    public enum Priority { Low, Medium, High, Urgent }
    public enum TaskStatus { Todo, InProgress, Completed, Overdue }

    public class WorkTask
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Tiêu đề không được để trống")]
        [StringLength(100, ErrorMessage = "Tiêu đề không được vượt quá 100 ký tự")]
        public string Title { get; set; }

        public string? Description { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày bắt đầu")]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày kết thúc")]
        public DateTime EndDate { get; set; }

        public Priority Priority { get; set; }
        public TaskStatus Status { get; set; }

        [Range(0, 100, ErrorMessage = "Tiến độ phải từ 0 đến 100")]
        public int Progress { get; set; } // % hoàn thành (0-100)

        public int? ProjectId { get; set; }
        public Project? Project { get; set; }

        // Position within a board column for ordering
        public int Position { get; set; }

        // Optional relation to a BoardList (column) so a task can belong to a column
        public int? BoardListId { get; set; }
        public BoardList? BoardList { get; set; }

        public string? UserId { get; set; }

        public ICollection<TimeLog> TimeLogs { get; set; } = new List<TimeLog>();
    }
}