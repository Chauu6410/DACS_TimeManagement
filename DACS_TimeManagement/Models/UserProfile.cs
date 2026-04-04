using System;
using System.ComponentModel.DataAnnotations;

namespace DACS_TimeManagement.Models
{
    public class UserProfile
    {
        public int Id { get; set; }
        
        [Required]
        public string UserId { get; set; } // Links to AspNetUsers table
        
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Department { get; set; }
        public string? Position { get; set; }
        public DateTime JoinDate { get; set; } = DateTime.Now;
        
        // Preferences
        public string Theme { get; set; } = "light"; // "light", "dark", "primary", etc.
        public string DefaultView { get; set; } = "Dashboard";
        public bool EmailNotifications { get; set; } = true;
        public bool PushNotifications { get; set; } = true;
        
        public TimeSpan WorkStartTime { get; set; } = new TimeSpan(9, 0, 0); // 9:00 AM
        public TimeSpan WorkEndTime { get; set; } = new TimeSpan(17, 0, 0); // 5:00 PM
    }
}