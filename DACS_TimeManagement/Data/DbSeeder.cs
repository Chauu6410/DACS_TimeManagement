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
    /// All content is in English. Each user gets at least one project and personal goal.
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

            var allUsers = new List<IdentityUser> { admin, u1, u2, u3 };

            // 3) Avoid duplicate seeding if projects already exist
            if (await context.Projects.AsNoTracking().AnyAsync()) return;

            // 4) Create projects for each user (so everyone has their own data)
            var projects = new List<Project>();

            // Admin projects
            projects.Add(new Project { Name = "TimeMaster Management System", Description = "Enterprise software with AES encryption integration.", CreatedDate = DateTime.Now.AddDays(-20), UserId = admin.Id });
            projects.Add(new Project { Name = "Cloud & AI Research", Description = "Infrastructure deployment and machine learning experiments.", CreatedDate = DateTime.Now.AddDays(-10), UserId = admin.Id });
            projects.Add(new Project { Name = "Marketing Campaign Q2", Description = "Promote new product features.", CreatedDate = DateTime.Now.AddDays(-5), UserId = admin.Id });

            // User1 (Huong) personal projects
            projects.Add(new Project { Name = "Personal Learning Plan", Description = "Upskilling in .NET and React", CreatedDate = DateTime.Now.AddDays(-15), UserId = u1.Id });
            projects.Add(new Project { Name = "Fitness Tracker App", Description = "Mobile app for daily workouts", CreatedDate = DateTime.Now.AddDays(-8), UserId = u1.Id });

            // User2 (Quynh) personal projects
            projects.Add(new Project { Name = "Freelance Portfolio", Description = "Build a professional portfolio website", CreatedDate = DateTime.Now.AddDays(-12), UserId = u2.Id });
            projects.Add(new Project { Name = "Digital Marketing Strategy", Description = "SEO and content plan", CreatedDate = DateTime.Now.AddDays(-3), UserId = u2.Id });

            // User3 (Ngoc) personal projects
            projects.Add(new Project { Name = "Reading Challenge 2025", Description = "Track 50 books this year", CreatedDate = DateTime.Now.AddDays(-7), UserId = u3.Id });
            projects.Add(new Project { Name = "Meditation Habit", Description = "Daily mindfulness practice", CreatedDate = DateTime.Now.AddDays(-1), UserId = u3.Id });

            foreach (var proj in projects)
            {
                context.Projects.Add(proj);
                await context.SaveChangesAsync(); // Save to get Project.Id

                // Ensure BoardLists exist for this project
                var lists = await context.BoardLists.Where(b => b.ProjectId == proj.Id).OrderBy(b => b.Position).ToListAsync();
                if (!lists.Any())
                {
                    lists = new List<BoardList>
                    {
                        new BoardList { Name = "To Do", Position = 0, ProjectId = proj.Id },
                        new BoardList { Name = "In Progress", Position = 1, ProjectId = proj.Id },
                        new BoardList { Name = "Testing", Position = 2, ProjectId = proj.Id },
                        new BoardList { Name = "Done", Position = 3, ProjectId = proj.Id }
                    };
                    context.BoardLists.AddRange(lists);
                    await context.SaveChangesAsync();

                    // Reload to get generated Ids
                    lists = await context.BoardLists.Where(b => b.ProjectId == proj.Id).OrderBy(b => b.Position).ToListAsync();
                }

                // Create 4-6 tasks per project, assign random assignees (including project owner and others)
                int taskCount = 5; // each project gets 5 tasks
                for (int i = 1; i <= taskCount; i++)
                {
                    // Assignee: sometimes the project owner, sometimes another user (for collaboration demo)
                    IdentityUser assignee = (i % 2 == 0) ? proj.UserId == admin.Id ? u1 : admin : allUsers.First(u => u.Id == proj.UserId);
                    var board = lists[i % lists.Count];

                    var task = new WorkTask
                    {
                        Title = (i == 1) ? $"Setup: {proj.Name}" : $"Task #{i} - {proj.Name}",
                        Description = (i == 3) ? "This task involves encrypted data handling (AES-256)." : $"Detailed description for task {i} of project '{proj.Name}'.",
                        StartDate = DateTime.Now.AddDays(-i),
                        EndDate = DateTime.Now.AddDays(i * 2),
                        Priority = (Priority)(i % 4),
                        Status = (i > 4) ? DACS_TimeManagement.Models.TaskStatus.Completed : DACS_TimeManagement.Models.TaskStatus.Todo,
                        Progress = (i > 4) ? 100 : (i * 15),
                        Position = i - 1,
                        ProjectId = proj.Id,
                        BoardListId = board.Id,
                        UserId = proj.UserId,   // Project owner
                        AssigneeId = assignee.Id,
                        IsPrivate = (i == 3)
                    };
                    context.WorkTasks.Add(task);
                }

                // Add a PersonalGoal for the project owner (each user gets at least one goal)
                var existingGoal = await context.PersonalGoals.FirstOrDefaultAsync(g => g.UserId == proj.UserId);
                if (existingGoal == null)
                {
                    context.PersonalGoals.Add(new PersonalGoal
                    {
                        GoalName = $"Master {proj.Name}",
                        TargetDate = DateTime.Now.AddDays(45),
                        TargetValue = 100,
                        CurrentValue = 10,
                        UserId = proj.UserId
                    });
                }

                await context.SaveChangesAsync();
            }

            // Add a sample notification for each user
            foreach (var user in allUsers)
            {
                if (!await context.Notifications.AnyAsync(n => n.UserId == user.Id))
                {
                    context.Notifications.Add(new Notification
                    {
                        Message = $"Welcome to TimeMaster! Start managing your time effectively.",
                        TriggerTime = DateTime.Now,
                        IsRead = false,
                        UserId = user.Id
                    });
                }
            }

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
                var existing = await userManager.FindByEmailAsync(email);
                if (existing != null) return existing;
                throw new Exception($"Failed to create user {email}: {string.Join(';', res.Errors.Select(e => e.Description))}");
            }
            await userManager.AddToRoleAsync(user, role);
            return user;
        }
    }
}