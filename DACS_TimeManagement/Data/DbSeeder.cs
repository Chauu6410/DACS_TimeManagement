using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DACS_TimeManagement.Models;

namespace DACS_TimeManagement.Data
{
    /// <summary>
    /// Database seeder to create sample roles, users, projects, board lists and tasks.
    /// Matches project structure in the solution (ApplicationDbContext, Project, BoardList, WorkTask, PersonalGoal, Notification).
    /// </summary>
    public static class DbSeeder
    {
        public static async Task SeedData(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // Ensure database is created/migrated
            await context.Database.EnsureCreatedAsync();

            // 1) Create roles
            if (!await roleManager.RoleExistsAsync("Admin")) await roleManager.CreateAsync(new IdentityRole("Admin"));
            if (!await roleManager.RoleExistsAsync("User")) await roleManager.CreateAsync(new IdentityRole("User"));

            // 2) Create users (admin + 3 sample users)
            var admin = await CreateUserIfNotExists(userManager, "admin@gmail.com", "Admin@123", "Admin");
            var u1 = await CreateUserIfNotExists(userManager, "huong.dev@gmail.com", "Dev@123", "User");
            var u2 = await CreateUserIfNotExists(userManager, "quynh.dev@gmail.com", "Dev@123", "User");
            var u3 = await CreateUserIfNotExists(userManager, "ngoc.test@gmail.com", "Dev@123", "User");

            var userList = new List<IdentityUser> { admin, u1, u2, u3 };

            // 3) Avoid duplicate seeding
            if (await context.Projects.AsNoTracking().AnyAsync()) return;

            // 4) Create 3 sample projects
            var projects = new List<Project>
            {
                new Project { Name = "Hệ thống Quản lý TimeMaster", Description = "Dự án CNTT tích hợp mã hóa AES.", CreatedDate = DateTime.Now.AddDays(-20), UserId = admin.Id },
                new Project { Name = "Nghiên cứu Cloud & AI", Description = "Triển khai hạ tầng và học máy.", CreatedDate = DateTime.Now.AddDays(-10), UserId = admin.Id },
                new Project { Name = "Chiến dịch Marketing Q2", Description = "Quảng bá sản phẩm mới.", CreatedDate = DateTime.Now.AddDays(-5), UserId = admin.Id }
            };

            foreach (var proj in projects)
            {
                context.Projects.Add(proj);
                await context.SaveChangesAsync();

                // Ensure BoardLists exist for this project (do not duplicate)
                var lists = await context.BoardLists.Where(b => b.ProjectId == proj.Id).OrderBy(b => b.Position).AsNoTracking().ToListAsync();
                if (!lists.Any())
                {
                    lists = new List<BoardList>
                    {
                        new BoardList { Name = "Cần làm", Position = 0, ProjectId = proj.Id },
                        new BoardList { Name = "Đang làm", Position = 1, ProjectId = proj.Id },
                        new BoardList { Name = "Hoàn tất", Position = 2, ProjectId = proj.Id }
                    };
                    context.BoardLists.AddRange(lists);
                    await context.SaveChangesAsync();

                    // Reload to get generated Ids
                    lists = await context.BoardLists.Where(b => b.ProjectId == proj.Id).OrderBy(b => b.Position).ToListAsync();
                }

                // Create exactly 6 tasks per project and distribute assignees round-robin
                for (int i = 1; i <= 6; i++)
                {
                    var assignee = userList[i % userList.Count];
                    var board = lists[i % lists.Count];

                    var task = new WorkTask
                    {
                        Title = $"Task #{i} - {proj.Name}",
                        Description = (i == 3) ? "Dữ liệu được mã hóa AES" : $"Mô tả cho công việc {i} - {proj.Name}",
                        StartDate = DateTime.Now.AddDays(-i),
                        EndDate = DateTime.Now.AddDays(i),
                        Priority = (Priority)(i % 4),
                        Status = (i > 4) ? DACS_TimeManagement.Models.TaskStatus.Completed : DACS_TimeManagement.Models.TaskStatus.Todo,
                        Progress = (i > 4) ? 100 : 0,
                        Position = i - 1,
                        ProjectId = proj.Id,
                        BoardListId = board.Id,
                        UserId = admin.Id,
                        AssigneeId = assignee.Id,
                        IsPrivate = (i == 3)
                    };

                    context.WorkTasks.Add(task);
                }

                // Add a sample PersonalGoal per project owner for demo
                context.PersonalGoals.Add(new PersonalGoal
                {
                    GoalName = $"Mục tiêu cho {proj.Name}",
                    TargetDate = DateTime.Now.AddDays(30),
                    TargetValue = 100,
                    CurrentValue = 10,
                    UserId = admin.Id
                });

                await context.SaveChangesAsync();
            }

            // Add a sample notification for admin
            context.Notifications.Add(new Notification
            {
                Message = "Chào mừng bạn đã sử dụng TimeMaster - thông báo mẫu.",
                TriggerTime = DateTime.Now,
                IsRead = false,
                UserId = admin.Id
            });

            await context.SaveChangesAsync();
        }

        private static async Task<IdentityUser> CreateUserIfNotExists(UserManager<IdentityUser> userManager, string email, string password, string role)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user != null) return user;

            user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            var res = await userManager.CreateAsync(user, password);
            if (!res.Succeeded)
            {
                // If creation failed, try to return existing user or throw
                var existing = await userManager.FindByEmailAsync(email);
                if (existing != null) return existing;
                throw new Exception($"Failed to create user {email}: {string.Join(';', res.Errors.Select(e => e.Description))}");
            }
            await userManager.AddToRoleAsync(user, role);
            return user;
        }
    }
}
