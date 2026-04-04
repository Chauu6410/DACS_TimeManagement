using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DACS_TimeManagement.Models
{
    public class SharedTask
    {
        public int Id { get; set; }

        public int WorkTaskId { get; set; }
        [ForeignKey("WorkTaskId")]
        public WorkTask WorkTask { get; set; }

        public string OwnerId { get; set; } // Identity UserId of creator
        
        public string SharedWithUserId { get; set; } // Identity UserId of invited team member

        public string PermissionLevel { get; set; } // e.g., "View", "Edit"

        public DateTime SharedDate { get; set; } = DateTime.Now;
    }
}
