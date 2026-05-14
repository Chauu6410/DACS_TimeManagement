using System.ComponentModel.DataAnnotations;

namespace DACS_TimeManagement.ViewModels
{
    public class WorkScheduleViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập giờ bắt đầu làm việc")]
        [Display(Name = "Giờ bắt đầu")]
        public string DefaultStartHour { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập giờ kết thúc làm việc")]
        [Display(Name = "Giờ kết thúc")]
        public string DefaultEndHour { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập giờ bắt đầu nghỉ trưa")]
        [Display(Name = "Bắt đầu nghỉ trưa")]
        public string LunchStart { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập giờ kết thúc nghỉ trưa")]
        [Display(Name = "Kết thúc nghỉ trưa")]
        public string LunchEnd { get; set; }

        [Display(Name = "Ngày làm việc trong tuần")]
        public List<string> SelectedWorkingDays { get; set; } = new List<string>();

        public List<string> AllDays { get; set; } = new List<string> 
        { 
            "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" 
        };
    }
}
