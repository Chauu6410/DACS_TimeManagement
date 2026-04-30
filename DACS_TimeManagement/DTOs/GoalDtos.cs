using System.ComponentModel.DataAnnotations;
using DACS_TimeManagement.Models;

namespace DACS_TimeManagement.DTOs
{
    public class CreateGoalDto
    {
        // Minimal Create DTO: link to Project, description and target date
        public int? ProjectId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên mục tiêu")]
        public string Title { get; set; }

        public string? Description { get; set; }

        [Required]
        public DateTime TargetDate { get; set; }

        public double? TargetHours { get; set; }

        public GoalType Type { get; set; } = GoalType.TaskBased;
    }

    public class UpdateGoalDto : CreateGoalDto
    {
        [Required]
        public int Id { get; set; }
    }

    public class LinkTasksDto
    {
        [Required]
        public int GoalId { get; set; }
        [Required]
        public List<int> TaskIds { get; set; }
    }

    public class GoalDetailDto
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string? Description { get; set; }
        public GoalType Type { get; set; }
        public double? TargetHours { get; set; }
        public int? TargetTasks { get; set; }
        public double CompletedHours { get; set; }
        public int CompletedTasks { get; set; }
        public GoalStatus Status { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime TargetDate { get; set; }
    }
}
