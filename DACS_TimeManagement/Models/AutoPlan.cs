using static DACS_TimeManagement.Controllers.CalendarController;

namespace DACS_TimeManagement.Models
{
    public class AutoPlan
    {
        public List<AutoPlanTaskItem> Tasks { get; set; } = new List<AutoPlanTaskItem>();
        public DateTime? TargetDate { get; set; }
    }
    public class AutoPlanTaskItem
    {
        public int TaskId { get; set; }
        public string Difficulty { get; set; } = "Medium";
        public int DurationMinutes { get; set; } = 60;
    }
}
