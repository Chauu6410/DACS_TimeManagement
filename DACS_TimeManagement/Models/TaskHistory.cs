using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DACS_TimeManagement.Models
{
    public class TaskHistory
    {
        public int Id { get; set; }
        
        public int WorkTaskId { get; set; }
        [ForeignKey("WorkTaskId")]
        public WorkTask? WorkTask { get; set; }
        
        public int? OldBoardListId { get; set; }
        public int? NewBoardListId { get; set; }
        
        public DateTime ChangedAt { get; set; }
        public string? ChangedByUserId { get; set; }
    }
}
