using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DACS_TimeManagement.Models
{
    public class ApplicationDbContext : IdentityDbContext<IdentityUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Khai báo các DbSet cho các thực thể đã tạo
        public DbSet<Project> Projects { get; set; }
        public DbSet<WorkTask> WorkTasks { get; set; }
        public DbSet<CalendarEvent> CalendarEvents { get; set; }
        public DbSet<PersonalGoal> PersonalGoals { get; set; }
        public DbSet<TimeLog> TimeLogs { get; set; }
        public DbSet<UserProfile> UserProfiles { get; set; }
        public DbSet<SharedTask> SharedTasks { get; set; }
        public DbSet<SharedEvent> SharedEvents { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        // Cấu hình quan hệ và ràng buộc dữ liệu
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // 1. Cấu hình Quan hệ Project - WorkTask (1 - n)
            builder.Entity<WorkTask>()
                .HasOne(t => t.Project)
                .WithMany(p => p.Tasks)
                .HasForeignKey(t => t.ProjectId)
                .OnDelete(DeleteBehavior.Cascade); // Xóa dự án thì xóa luôn task liên quan

            // 2. Cấu hình Quan hệ WorkTask - TimeLog (1 - n)
            builder.Entity<TimeLog>()
                .HasOne(tl => tl.WorkTask)
                .WithMany(t => t.TimeLogs) // Phải khớp với ICollection trong WorkTask
                .HasForeignKey(tl => tl.WorkTaskId)
                .OnDelete(DeleteBehavior.Cascade);

            // 3. Ràng buộc UserId (Mặc dù dùng Identity, ta vẫn nên khai báo độ dài hoặc Index nếu cần)
            builder.Entity<Project>().Property(p => p.UserId).IsRequired();
            builder.Entity<WorkTask>().Property(t => t.UserId).IsRequired();
            builder.Entity<CalendarEvent>().Property(e => e.UserId).IsRequired();
            builder.Entity<PersonalGoal>().Property(g => g.UserId).IsRequired();
            builder.Entity<Notification>().Property(n => n.UserId).IsRequired();

            // Cấu hình thêm cho Priority và Status (Lưu dưới dạng String trong DB cho dễ đọc)
            builder.Entity<WorkTask>()
                .Property(t => t.Priority)
                .HasConversion<string>();

            builder.Entity<WorkTask>()
                .Property(t => t.Status)
                .HasConversion<string>();
        }
    }
}

