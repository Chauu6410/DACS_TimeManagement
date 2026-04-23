using System.ComponentModel.DataAnnotations;

namespace DACS_TimeManagement.Models
{
    public class ScheduledEvent
    {
        public int Id { get; set; }
        public int TaskId { get; set; }
        public WorkTask? Task { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string? Color { get; set; }
    }
}
