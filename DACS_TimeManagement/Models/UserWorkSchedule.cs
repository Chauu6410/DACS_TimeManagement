using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DACS_TimeManagement.Models
{
    public class UserWorkSchedule
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; }

        [Required]
        public TimeOnly DefaultStartHour { get; set; }

        [Required]
        public TimeOnly DefaultEndHour { get; set; }

        [Required]
        public TimeOnly LunchStart { get; set; }

        [Required]
        public TimeOnly LunchEnd { get; set; }

        public string WorkingDays { get; set; } = "Monday,Tuesday,Wednesday,Thursday,Friday";

        public string TimeZoneId { get; set; } = "SE Asia Standard Time";
    }
}
