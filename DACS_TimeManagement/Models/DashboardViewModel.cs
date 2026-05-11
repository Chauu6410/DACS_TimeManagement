using System;
using System.Collections.Generic;

namespace DACS_TimeManagement.Models
{
    public class DashboardEventDto
    {
        public string Title { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class DashboardTaskDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
        public string Priority { get; set; } = string.Empty;
        public DateTime DueDate { get; set; }
    }

    public class DashboardViewModel
    {
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int InProgressTasks { get; set; }
        public double HoursWorked { get; set; }
        public List<DashboardEventDto> TodayEvents { get; set; } = new();
        public List<DashboardTaskDto> RecentTasks { get; set; } = new();
        public double[] WeeklyHours { get; set; } = new double[7];
        public int[] WeeklyTasks { get; set; } = new int[7];
        public List<DACS_TimeManagement.Models.WorkTask> AllTasks { get; set; } = new();
    }
}
