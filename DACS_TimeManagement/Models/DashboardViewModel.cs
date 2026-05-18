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

    public class DashboardProjectDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class DashboardViewModel
    {
        public List<DashboardEventDto> TodayEvents { get; set; } = new();
        public double[] WeeklyHours { get; set; } = new double[7];
        public int[] WeeklyTasks { get; set; } = new int[7];

        // Focus & Gamification
        public List<DashboardTaskDto> InProgressFocusTasks { get; set; } = new();
        public int Level { get; set; }
        public int Points { get; set; }
        public int CurrentStreak { get; set; }
        public string UserName { get; set; } = string.Empty;

        // User Projects for Quick Task Creation
        public List<DashboardProjectDto> UserProjects { get; set; } = new();
    }
}
