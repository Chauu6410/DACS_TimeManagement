using System.ComponentModel.DataAnnotations;

namespace DACS_TimeManagement.Models
{
    // Join entity for many-to-many between PersonalGoal and WorkTask
    public class GoalTask
    {
        [Required]
        public int GoalId { get; set; }
        public PersonalGoal Goal { get; set; }

        [Required]
        public int WorkTaskId { get; set; }
        public WorkTask WorkTask { get; set; }

        // Flag to mark the most relevant task for a goal
        public bool IsPrimary { get; set; } = false;
    }
}
