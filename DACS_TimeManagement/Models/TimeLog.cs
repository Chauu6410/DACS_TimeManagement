using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DACS_TimeManagement.Models
{
    public class TimeLog
    {
        public int Id { get; set; }

        // Đảm bảo tên này chính xác và kiểu dữ liệu là int
        public int WorkTaskId { get; set; }

        // Navigation property
        public WorkTask WorkTask { get; set; }

        public DateTime LogDate { get; set; }
        public double DurationHours { get; set; }
        public string? Note { get; set; }
    }
}
