using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DACS_TimeManagement.Models
{
    public class PersonalGoal
    {
        public int Id { get; set; }
        public string GoalName { get; set; }
        public string? Description { get; set; }
        public DateTime TargetDate { get; set; }
        public double TargetValue { get; set; }
        public double CurrentValue { get; set; }
        public string UserId { get; set; }
        public DateTime StartDate { get; set; } = DateTime.Now;
        public DateTime? LastUpdated { get; set; }
        public int CurrentStreak { get; set; }
    }
}

