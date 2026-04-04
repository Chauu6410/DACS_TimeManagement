using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DACS_TimeManagement.Models
{
    public class SharedEvent
    {
        public int Id { get; set; }

        public int EventId { get; set; }
        [ForeignKey("EventId")]
        public CalendarEvent Event { get; set; }

        public string OwnerId { get; set; }
        
        public string SharedWithUserId { get; set; }

        public string PermissionLevel { get; set; } // "View" or "Edit"

        public DateTime SharedDate { get; set; } = DateTime.Now;
    }
}
