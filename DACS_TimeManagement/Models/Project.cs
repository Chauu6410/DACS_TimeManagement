using System.ComponentModel.DataAnnotations;

namespace DACS_TimeManagement.Models
{
    public class Project
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        // Liên kết với Identity User sau này
        public string UserId { get; set; }
        public ICollection<WorkTask> Tasks { get; set; }
    }
}

