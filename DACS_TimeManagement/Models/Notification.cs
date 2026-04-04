using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DACS_TimeManagement.Models
{
    public class Notification
    {
        public int Id { get; set; }
        public string Message { get; set; }
        public DateTime TriggerTime { get; set; }
        public bool IsRead { get; set; } = false;
        public string UserId { get; set; }
    }
}

