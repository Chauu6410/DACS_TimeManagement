using System.Collections.Generic;

namespace DACS_TimeManagement.Models
{
    public class KanbanViewModel
    {
        // Chuyển sang List để dữ liệu được tải hoàn tất trước khi đưa ra View
        public List<Project> Projects { get; set; } = new List<Project>();
        public int? SelectedProjectId { get; set; }
        public List<BoardList> BoardLists { get; set; } = new List<BoardList>();
    }
}