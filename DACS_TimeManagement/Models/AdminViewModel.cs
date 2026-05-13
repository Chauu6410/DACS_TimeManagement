using Microsoft.AspNetCore.Identity;

namespace DACS_TimeManagement.Models
{
    public class AdminViewModel
    {
        public int TotalUsers { get; set; }
        public int TotalProjects { get; set; }
        public int TotalTasks { get; set; }
        public List<IdentityUser> Users { get; set; } = new List<IdentityUser>();
        public List<UserDetailViewModel> UserDetails { get; set; } = new List<UserDetailViewModel>();
    }

    public class UserDetailViewModel
    {
        public IdentityUser User { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsLocked { get; set; }
        public DateTime? LastAccess { get; set; }
    }
}
