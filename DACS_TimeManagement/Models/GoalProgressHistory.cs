using System.ComponentModel.DataAnnotations;

namespace DACS_TimeManagement.Models
{
    public class GoalProgressHistory
    {
        public int Id { get; set; }
        [Required]
        public int GoalId { get; set; }
        public PersonalGoal Goal { get; set; }

        public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

        // Stores progress percent (0-100)
        public double Progress { get; set; }

        // Optional notes
        public string? Note { get; set; }
    }
}
