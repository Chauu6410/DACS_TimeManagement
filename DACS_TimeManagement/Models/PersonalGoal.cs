using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DACS_TimeManagement.Models
{
    public class PersonalGoal
    {
        public int Id { get; set; }
        public string GoalName { get; set; }
        public DateTime TargetDate { get; set; }
        public double TargetValue { get; set; } // Ví dụ: Học 100 giờ
        public double CurrentValue { get; set; }
        public string UserId { get; set; }
    }
}

