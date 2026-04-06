using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;
using DACS_TimeManagement.Models;

namespace DACS_TimeManagement.Data
{
    public static class DbSeeder
    {
        public static async Task SeedRolesAndAdminAsync(IServiceProvider serviceProvider)
        {
            // Resolve RoleManager and UserManager from DI Container
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();

            // Define Roles
            string[] roles = { "Admin", "User" };
            foreach (var role in roles)
            {
                var roleExist = await roleManager.RoleExistsAsync(role);
                if (!roleExist)
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // Create Default Admin User
            string adminEmail = "admin@gmail.com";
            string adminPassword = "Admin@123";

            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                var newAdmin = new IdentityUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true
                };

                var createPowerUser = await userManager.CreateAsync(newAdmin, adminPassword);
                if (createPowerUser.Succeeded)
                {
                    // Assign Admin role to the newly created user
                    await userManager.AddToRoleAsync(newAdmin, "Admin");
                }
                adminUser = newAdmin;
            }

            // --- SEED SAMPLE DATA ---
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            if (adminUser != null && !context.Projects.Any(p => p.UserId == adminUser.Id))
            {
                var sampleProject = new Project
                {
                    Name = "Dự án mẫu - TimeMaster",
                    Description = "Dự án mẫu tự động tạo để bạn làm quen với hệ thống.",
                    CreatedDate = DateTime.Now,
                    UserId = adminUser.Id
                };
                context.Projects.Add(sampleProject);
                await context.SaveChangesAsync();

                var todoList = new BoardList { Name = "To Do", Position = 0, ProjectId = sampleProject.Id };
                var doingList = new BoardList { Name = "Doing", Position = 1, ProjectId = sampleProject.Id };
                var doneList = new BoardList { Name = "Done", Position = 2, ProjectId = sampleProject.Id };
                context.BoardLists.AddRange(todoList, doingList, doneList);
                await context.SaveChangesAsync();

                var task1 = new WorkTask
                {
                    Title = "Nhiệm vụ 1: Khám phá TimeMaster",
                    Description = "Tìm hiểu các tính năng của trang web",
                    StartDate = DateTime.Now,
                    EndDate = DateTime.Now.AddDays(2),
                    Priority = Priority.High,
                    Status = DACS_TimeManagement.Models.TaskStatus.Todo,
                    Progress = 0,
                    ProjectId = sampleProject.Id,
                    BoardListId = todoList.Id,
                    Position = 0,
                    UserId = adminUser.Id,
                    AssigneeId = adminUser.Id
                };

                var task2 = new WorkTask
                {
                    Title = "Nhiệm vụ 2: Tạo mục tiêu cá nhân",
                    Description = "Lên mục tiêu và theo dõi tiến độ",
                    StartDate = DateTime.Now,
                    EndDate = DateTime.Now.AddDays(5),
                    Priority = Priority.Medium,
                    Status = DACS_TimeManagement.Models.TaskStatus.InProgress,
                    Progress = 50,
                    ProjectId = sampleProject.Id,
                    BoardListId = doingList.Id,
                    Position = 0,
                    UserId = adminUser.Id,
                    AssigneeId = adminUser.Id
                };
                
                context.WorkTasks.AddRange(task1, task2);

                // Add admin as a project member (owner) so membership UI can show
                var adminMember = new ProjectMember
                {
                    ProjectId = sampleProject.Id,
                    UserId = adminUser.Id,
                    Role = "Owner",
                    JoinedDate = DateTime.Now
                };
                context.ProjectMembers.Add(adminMember);

                // Seed a sample notification for the admin user so the notification UI isn't empty
                var notif = new Notification
                {
                    Message = "Chào mừng bạn đến với TimeMaster! Đây là thông báo mẫu.",
                    TriggerTime = DateTime.Now,
                    IsRead = false,
                    UserId = adminUser.Id
                };
                context.Notifications.Add(notif);

                var goal = new PersonalGoal
                {
                    GoalName = "Hoàn thành 100 giờ lập trình",
                    TargetDate = DateTime.Now.AddDays(30),
                    TargetValue = 100,
                    CurrentValue = 10,
                    UserId = adminUser.Id
                };
                context.PersonalGoals.Add(goal);

                await context.SaveChangesAsync();
            }
        }
    }
}
