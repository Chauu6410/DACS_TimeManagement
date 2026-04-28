using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DACS_TimeManagement.Models
{
    public class PersonalGoal
    {
        public int Id { get; set; }

        [Required]
        [StringLength(150)]
        public string Title { get; set; }

        public string? Description { get; set; }

        // Goal type: Time-based (hours) or Task-based (count)
        public GoalType Type { get; set; } = GoalType.TimeBased;

        // Time-based target (hours)
        public double? TargetHours { get; set; }

        // Task-based target (number of tasks)
        public int? TargetTasks { get; set; }

        // Aggregates maintained by services
        public double CompletedHours { get; set; }
        public int CompletedTasks { get; set; }

        public DateTime StartDate { get; set; } = DateTime.Now;
        public DateTime TargetDate { get; set; }

        // Status and metadata
        public GoalStatus Status { get; set; } = GoalStatus.Active;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Backwards-compatible fields used by existing UI/actions

        // Link to Project (optional)
        public int? ProjectId { get; set; }
        public string? AIActionPlan { get; set; }
        public Project? Project { get; set; }

        public string UserId { get; set; }

        public DateTime? LastUpdated { get; set; }
        public int CurrentStreak { get; set; }

        // Navigation: many-to-many with WorkTask via GoalTask
        public ICollection<GoalTask> GoalTasks { get; set; } = new List<GoalTask>();
        // History navigation
        public ICollection<GoalProgressHistory> GoalProgressHistories { get; set; } = new List<GoalProgressHistory>();

        // Backwards-compatible properties for existing UI
        // Original code used GoalName, TargetValue (double) and CurrentValue (double)
        [NotMapped]
        public string GoalName
        {
            get => Title;
            set => Title = value;
        }

        // Stored target value for backward compatibility. Keep in sync with TargetHours/TargetTasks
        private double _targetValue;
        public double TargetValue
        {
            get => _targetValue;
            set
            {
                _targetValue = value;
                if (Type == GoalType.TimeBased)
                    TargetHours = value;
                else
                    TargetTasks = (int)Math.Round(value);
            }
        }

        private double _currentValue;
        public double CurrentValue
        {
            get => _currentValue;
            set
            {
                _currentValue = value;
                if (Type == GoalType.TimeBased)
                    CompletedHours = value;
                else
                    CompletedTasks = (int)Math.Round(value);
            }
        }
    }
}

namespace DACS_TimeManagement.Models
{
    public enum GoalType
    {
        TimeBased = 0,
        TaskBased = 1
    }

    public enum GoalStatus
    {
        Active,
        OnTrack,
        Behind,
        Overdue,
        Completed
    }
}

