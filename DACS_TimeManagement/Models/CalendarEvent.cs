using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DACS_TimeManagement.Models
{
    public class CalendarEvent
    {
        public int Id { get; set; }
        public string Subject { get; set; }
        public string? Description { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool IsFullDay { get; set; }
        public string ThemeColor { get; set; } // Màu sắc hiển thị trên lịch

        public string UserId { get; set; }
    }
}
