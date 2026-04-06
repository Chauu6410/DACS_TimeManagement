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

        // Board lists (columns) for this project (e.g., To Do, Doing, Done)
        public ICollection<BoardList> BoardLists { get; set; } = new List<BoardList>();

        // Danh sách thành viên trong dự án
        public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();
    }
}

