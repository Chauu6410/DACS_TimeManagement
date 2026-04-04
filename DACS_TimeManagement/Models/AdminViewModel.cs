using Microsoft.AspNetCore.Identity;

namespace DACS_TimeManagement.Models
{
    public class AdminViewModel
    {
        public int TotalUsers { get; set; }
        public int TotalProjects { get; set; }
        public int TotalTasks { get; set; }
        public List<IdentityUser> Users { get; set; } = new List<IdentityUser>();
    }
}
