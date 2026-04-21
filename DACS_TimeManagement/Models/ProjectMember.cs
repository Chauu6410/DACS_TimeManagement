using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace DACS_TimeManagement.Models
{
    public class ProjectMember
    {
        public int Id { get; set; }

        [Required]
        public int ProjectId { get; set; }
        public Project? Project { get; set; }

        [Required]
        public string UserId { get; set; } // Liên kết đến AspNetUsers (IdentityUser)

        [ForeignKey("UserId")]
        public virtual IdentityUser? User { get; set; }

        public string Role { get; set; } = "Member"; // Ví dụ: "Owner", "Admin", "Member"
        
        public DateTime JoinedDate { get; set; } = DateTime.Now;
    }
}
