using System;
using System.ComponentModel.DataAnnotations;

namespace DACS_TimeManagement.Models
{
    public class OtpRecord
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; }

        [Required]
        [StringLength(6)]
        public string Code { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime ExpiresAt { get; set; }

        public bool IsUsed { get; set; } = false;
    }
}
