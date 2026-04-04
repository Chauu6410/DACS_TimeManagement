using System.ComponentModel.DataAnnotations;

namespace DACS_TimeManagement.Models
{
    public class BoardList
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        // Position for ordering columns within a project
        public int Position { get; set; }

        public int ProjectId { get; set; }
        public Project? Project { get; set; }

        public ICollection<WorkTask> WorkTasks { get; set; } = new List<WorkTask>();
    }
}
