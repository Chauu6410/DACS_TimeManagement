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
    /// Database seeder to populate all tables with realistic sample data.
    /// Covers: Users, Roles, UserProfiles, Projects, BoardLists, WorkTasks,
    ///         ProjectMembers, ProjectDiscussions, PersonalGoals, GoalTasks,
    ///         GoalProgressHistories, TimeLogs, TaskHistories,
    ///         CalendarEvents, Notifications.
    /// </summary>
    public static class DbSeeder
    {
        public static async Task SeedData(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            await context.Database.EnsureCreatedAsync();

            // ─── 1. Roles ───────────────────────────────────────────────────────────────
            if (!await roleManager.RoleExistsAsync("Admin")) await roleManager.CreateAsync(new IdentityRole("Admin"));
            if (!await roleManager.RoleExistsAsync("User"))  await roleManager.CreateAsync(new IdentityRole("User"));

            // ─── 2. Ensure Admin Exists ─────────────────────────────────────────────────
            var admin = await CreateUserIfNotExists(userManager, "admin@gmail.com",       "Admin@123", "Admin");

            // ─── 3. Guard: skip if already seeded ───────────────────────────────────────
            // If the admin profile exists, we assume the seed data has already run.
            if (await context.UserProfiles.AnyAsync(p => p.UserId == admin.Id)) return;

            // ─── 4. Sample Users ────────────────────────────────────────────────────────
            var u1    = await CreateUserIfNotExists(userManager, "huong@gmail.com",   "Huong@123",   "User");
            var u2    = await CreateUserIfNotExists(userManager, "quynh@gmail.com",   "Quynh@123",   "User");
            var u3    = await CreateUserIfNotExists(userManager, "ngoc@gmail.com",   "Ngoc@123",   "User");
            var u4    = await CreateUserIfNotExists(userManager, "minh@gmail.com",     "Minh@123",   "User");

            var allUsers = new List<IdentityUser> { admin, u1, u2, u3, u4 };

            // ─── 4. UserProfiles ────────────────────────────────────────────────────────
            var profileDefs = new[]
            {
                (admin, "System Administrator", "IT",          "Admin",            "dark",    "Dashboard"),
                (u1,    "Trần Thị Hương",        "Engineering", "Senior Developer", "light",   "Kanban"),
                (u2,    "Lê Thị Quỳnh",          "Marketing",   "Content Strategist","primary","Dashboard"),
                (u3,    "Nguyễn Thị Ngọc",       "QA",          "QA Engineer",      "light",   "Dashboard"),
                (u4,    "Phạm Văn Minh",          "Management",  "Project Manager",  "dark",    "Dashboard"),
            };

            foreach (var (user, name, dept, pos, theme, view) in profileDefs)
            {
                if (!await context.UserProfiles.AnyAsync(p => p.UserId == user.Id))
                {
                    context.UserProfiles.Add(new UserProfile
                    {
                        UserId             = user.Id,
                        FullName           = name,
                        Email              = user.Email,
                        Department         = dept,
                        Position           = pos,
                        Theme              = theme,
                        DefaultView        = view,
                        EmailNotifications = true,
                        PushNotifications  = true,
                        JoinDate           = DateTime.Now.AddMonths(-6),
                        WorkStartTime      = new TimeSpan(8, 30, 0),
                        WorkEndTime        = new TimeSpan(17, 30, 0)
                    });
                }
            }
            await context.SaveChangesAsync();

            // ─── 5. Projects ────────────────────────────────────────────────────────────
            var projectDefs = new[]
            {
                // Admin projects
                ("TimeMaster Management System",  "Enterprise time-management platform with AES-256 encryption.",               -30, admin.Id),
                ("Cloud & AI Research Lab",        "Infrastructure deployment, MLOps pipelines and LLM experiments.",           -20, admin.Id),
                ("Marketing Campaign Q3",          "Multi-channel product launch campaign for Q3 2025.",                        -10, admin.Id),
                // Huong
                ("Personal .NET Learning Plan",    "Structured upskilling in ASP.NET Core 8, EF Core and React.",               -25, u1.Id),
                ("Fitness Tracker App",            "Cross-platform mobile app for daily workouts and nutrition logging.",        -15, u1.Id),
                // Quynh
                ("Freelance Portfolio v2",         "Redesigned personal portfolio with Next.js and Framer Motion.",             -18, u2.Id),
                ("SEO & Content Strategy",         "Six-month SEO roadmap and blog content calendar.",                          -8,  u2.Id),
                // Ngoc
                ("Reading Challenge 2025",         "Track 52 books this year with reviews and rating scores.",                  -12, u3.Id),
                ("Daily Mindfulness Practice",     "Build a sustainable meditation habit with journaling.",                     -5,  u3.Id),
                // Minh
                ("Team Onboarding Playbook",       "Document and automate the onboarding process for new engineers.",           -22, u4.Id),
                ("Q2 Sprint Planning Dashboard",   "Centralised sprint planning and velocity tracking for three scrum teams.",  -14, u4.Id),
            };

            var projects = new List<Project>();
            foreach (var (name, desc, daysAgo, ownerId) in projectDefs)
            {
                var p = new Project { Name = name, Description = desc, CreatedDate = DateTime.Now.AddDays(daysAgo), UserId = ownerId };
                context.Projects.Add(p);
                await context.SaveChangesAsync();
                projects.Add(p);
            }

            // ─── 6. BoardLists + WorkTasks + ProjectMembers + TimeLogs + TaskHistories ─
            var rng = new Random(42);

            // Realistic task titles per project index
            var taskTitles = new Dictionary<int, string[]>
            {
                [0]  = new[]{"Design system architecture","Set up CI/CD pipeline","Implement AES-256 encryption module","Write unit tests for auth service","Deploy to staging environment","Conduct security audit","Fix JWT refresh-token bug","Optimise database query performance"},
                [1]  = new[]{"Provision AWS EKS cluster","Configure Terraform IaC","Train baseline NLP model","Integrate MLflow tracking","Build model serving API","Write GPU cost-optimisation report","Review LLM fine-tuning results","Deploy monitoring stack"},
                [2]  = new[]{"Craft brand messaging","Design social-media assets","Schedule LinkedIn posts","Run A/B test on landing page","Analyse campaign metrics","Coordinate with influencers","Produce promotional video","Publish Q3 retrospective"},
                [3]  = new[]{"Study ASP.NET Core fundamentals","Build sample CRUD app","Learn EF Core migrations","Integrate SignalR real-time","Deploy app to Azure","Practice unit testing patterns","Read Clean Architecture book","Implement repository pattern"},
                [4]  = new[]{"Define app wireframes","Set up React Native project","Build workout logging screen","Implement nutrition API","Add progress chart component","Write integration tests","Publish to TestFlight","Collect beta feedback"},
                [5]  = new[]{"Create design system tokens","Build hero section","Add portfolio case studies","Implement dark mode","Optimise for Core Web Vitals","Add contact form backend","Write blog posts","Deploy to Vercel"},
                [6]  = new[]{"Keyword research phase 1","Optimise existing pages","Create content calendar","Write 4 pillar articles","Build backlink outreach list","Analyse competitor gaps","Submit sitemap to GSC","Track monthly rankings"},
                [7]  = new[]{"Set reading goal targets","Log January books","Log February books","Write mid-year review","Create Goodreads shelf","Design reading tracker UI","Share top-5 recommendations","Log Q3 reads"},
                [8]  = new[]{"Install meditation app","Complete 7-day beginner course","Write daily journal entry","Try body-scan technique","Join mindfulness community","Log 30-day streak","Read 'The Miracle Morning'","Practice gratitude journaling"},
                [9]  = new[]{"Draft onboarding checklist","Record tool walkthroughs","Set up Notion workspace","Automate account provisioning","Collect feedback from new hires","Review first-week survey results","Update handbook docs","Present playbook to HR"},
                [10] = new[]{"Set up Jira board","Define sprint ceremonies","Create velocity chart","Retrospective template","Backlog grooming guide","Integrate with Slack","Build burndown report","Train team leads"},
            };

            var colors = new[] { "#4f46e5", "#0ea5e9", "#10b981", "#f59e0b", "#ef4444", "#8b5cf6", "#ec4899", "#14b8a6" };

            for (int pi = 0; pi < projects.Count; pi++)
            {
                var proj = projects[pi];

                // BoardLists
                var listNames = new[] { "To Do", "In Progress", "Testing", "Done" };
                var lists = new List<BoardList>();
                for (int li = 0; li < listNames.Length; li++)
                {
                    var bl = new BoardList { Name = listNames[li], Position = li, ProjectId = proj.Id };
                    context.BoardLists.Add(bl);
                    lists.Add(bl);
                }
                await context.SaveChangesAsync();
                lists = await context.BoardLists.Where(b => b.ProjectId == proj.Id).OrderBy(b => b.Position).ToListAsync();

                // ProjectMembers: owner + 1-2 collaborators
                context.ProjectMembers.Add(new ProjectMember { ProjectId = proj.Id, UserId = proj.UserId, Role = "Owner", JoinedDate = proj.CreatedDate });
                var collaborators = allUsers.Where(u => u.Id != proj.UserId).Take(2).ToList();
                foreach (var collab in collaborators)
                    context.ProjectMembers.Add(new ProjectMember { ProjectId = proj.Id, UserId = collab.Id, Role = "Member", JoinedDate = proj.CreatedDate.AddDays(rng.Next(1, 5)) });
                await context.SaveChangesAsync();

                // WorkTasks
                var titles = taskTitles.ContainsKey(pi) ? taskTitles[pi] : new[] { "General Task" };
                var createdTasks = new List<WorkTask>();

                for (int ti = 0; ti < titles.Length; ti++)
                {
                    int listIdx  = ti < 2 ? 3 : (ti < 4 ? 2 : (ti < 6 ? 1 : 0)); // distribute realistically
                    bool isDone  = listIdx == 3;
                    bool isInProg = listIdx == 1;

                    var assignee = collaborators.Count > 0 && ti % 3 == 0 ? collaborators[0] : allUsers.First(u => u.Id == proj.UserId);

                    var task = new WorkTask
                    {
                        Title       = titles[ti],
                        Description = $"Detailed breakdown and acceptance criteria for '{titles[ti]}'. Review with the team before starting.",
                        StartDate   = proj.CreatedDate.AddDays(ti * 2),
                        EndDate     = proj.CreatedDate.AddDays(ti * 2 + rng.Next(3, 10)),
                        Color       = colors[ti % colors.Length],
                        Priority    = (Priority)(ti % 4),
                        Status      = isDone ? DACS_TimeManagement.Models.TaskStatus.Completed
                                             : (isInProg ? DACS_TimeManagement.Models.TaskStatus.InProgress
                                                         : DACS_TimeManagement.Models.TaskStatus.Todo),
                        Progress    = isDone ? 100 : (isInProg ? rng.Next(30, 80) : rng.Next(0, 25)),
                        Position    = ti,
                        ProjectId   = proj.Id,
                        BoardListId = lists[listIdx].Id,
                        UserId      = proj.UserId,
                        AssigneeId  = assignee.Id,
                        IsPrivate   = (ti == 2)
                    };
                    context.WorkTasks.Add(task);
                    await context.SaveChangesAsync();
                    createdTasks.Add(task);

                    // TimeLogs seeding removed to provide a clean history

                    // TaskHistory: moved from To Do → In Progress (or In Progress → Done)
                    if (isDone)
                    {
                        context.TaskHistories.Add(new TaskHistory
                        {
                            WorkTaskId      = task.Id,
                            OldBoardListId  = lists[0].Id,
                            NewBoardListId  = lists[1].Id,
                            ChangedAt       = task.StartDate.AddDays(1),
                            ChangedByUserId = proj.UserId
                        });
                        context.TaskHistories.Add(new TaskHistory
                        {
                            WorkTaskId      = task.Id,
                            OldBoardListId  = lists[1].Id,
                            NewBoardListId  = lists[3].Id,
                            ChangedAt       = task.EndDate.AddDays(-1),
                            ChangedByUserId = proj.UserId
                        });
                    }
                    else if (isInProg)
                    {
                        context.TaskHistories.Add(new TaskHistory
                        {
                            WorkTaskId      = task.Id,
                            OldBoardListId  = lists[0].Id,
                            NewBoardListId  = lists[1].Id,
                            ChangedAt       = task.StartDate.AddDays(1),
                            ChangedByUserId = proj.UserId
                        });
                    }
                }
                await context.SaveChangesAsync();

                // ProjectDiscussions: 2-3 messages per project
                var discussionMessages = new[]
                {
                    ($"Kickoff meeting notes for **{proj.Name}** are now available in the shared drive.", proj.UserId, proj.CreatedDate.AddDays(1)),
                    ("Great progress this week! Keep the momentum going.", collaborators.Count > 0 ? collaborators[0].Id : proj.UserId, proj.CreatedDate.AddDays(5)),
                    ("Reminder: sprint review is scheduled for Friday at 3 PM.", proj.UserId, proj.CreatedDate.AddDays(10)),
                };
                foreach (var (content, userId, createdAt) in discussionMessages)
                {
                    context.ProjectDiscussion.Add(new ProjectDiscussion
                    {
                        ProjectId = proj.Id,
                        UserId    = userId,
                        Content   = content,
                        CreatedAt = createdAt
                    });
                }
                await context.SaveChangesAsync();

                // PersonalGoal per project owner (one per user at minimum)
                var existingGoal = await context.PersonalGoals.FirstOrDefaultAsync(g => g.UserId == proj.UserId && g.ProjectId == proj.Id);
                if (existingGoal == null && createdTasks.Any())
                {
                    bool useTimeBased = (pi % 2 == 0);
                    
                    // Calculate actual hours logged for this goal's tasks
                    var linkedTaskIds = createdTasks.Take(3).Select(t => t.Id).ToList();
                    var totalHoursLogged = await context.TimeLogs
                        .Where(tl => linkedTaskIds.Contains(tl.WorkTaskId))
                        .SumAsync(tl => tl.DurationHours);

                    var goal = new PersonalGoal
                    {
                        Title          = $"Complete: {proj.Name}",
                        Description    = $"Achieve all milestones for the '{proj.Name}' project on time.",
                        Type           = useTimeBased ? GoalType.TimeBased : GoalType.TaskBased,
                        TargetHours    = useTimeBased ? (double?)40 : null,
                        TargetTasks    = !useTimeBased ? (int?)createdTasks.Count : null,
                        CompletedHours = 0,
                        CompletedTasks = 0,
                        StartDate      = proj.CreatedDate,
                        TargetDate     = proj.CreatedDate.AddDays(60),
                        Status         = GoalStatus.OnTrack,
                        CreatedAt      = proj.CreatedDate,
                        ProjectId      = proj.Id,
                        UserId         = proj.UserId,
                        LastUpdated    = DateTime.Now.AddDays(-rng.Next(0, 5))
                    };
                    
                    if (useTimeBased && goal.CompletedHours > (goal.TargetHours ?? 40)) 
                        goal.TargetHours = Math.Ceiling(goal.CompletedHours / 10) * 10 + 10;

                    context.PersonalGoals.Add(goal);
                    await context.SaveChangesAsync();

                    // GoalTasks: link first 3 tasks to the goal
                    foreach (var t in createdTasks.Take(3))
                    {
                        context.GoalTasks.Add(new GoalTask
                        {
                            GoalId     = goal.Id,
                            WorkTaskId = t.Id,
                            IsPrimary  = t == createdTasks.First()
                        });
                    }

                    // Progress history snapshots removed to provide a clean history
                    context.GoalProgressHistories.Add(new GoalProgressHistory
                    {
                        GoalId     = goal.Id,
                        Progress   = 0,
                        RecordedAt = proj.CreatedDate,
                        Note       = "Initial Commitment Established"
                    });
                    await context.SaveChangesAsync();
                }
            }

            // ─── 7. CalendarEvents ──────────────────────────────────────────────────────
            var calendarDefs = new[]
            {
                (admin.Id, "Sprint Planning – Q3",          "Plan tasks for the upcoming sprint.",          DateTime.Now.AddDays(2).Date.AddHours(9),  DateTime.Now.AddDays(2).Date.AddHours(11),  false, "#4f46e5"),
                (admin.Id, "Security Review Meeting",       "Monthly security posture review.",             DateTime.Now.AddDays(5).Date.AddHours(14), DateTime.Now.AddDays(5).Date.AddHours(15),  false, "#ef4444"),
                (u1.Id,    "ASP.NET Core Workshop",         "Hands-on workshop with mentor.",               DateTime.Now.AddDays(3).Date.AddHours(10), DateTime.Now.AddDays(3).Date.AddHours(13),  false, "#0ea5e9"),
                (u1.Id,    "Team Standup",                  "Daily sync with the engineering team.",        DateTime.Now.Date.AddHours(9, 30, 0),      DateTime.Now.Date.AddHours(9, 45, 0),       false, "#10b981"),
                (u2.Id,    "Content Calendar Review",       "Review and schedule blog posts.",              DateTime.Now.AddDays(1).Date.AddHours(11), DateTime.Now.AddDays(1).Date.AddHours(12),  false, "#f59e0b"),
                (u2.Id,    "SEO Audit Presentation",        "Present findings to the stakeholders.",        DateTime.Now.AddDays(7).Date.AddHours(15), DateTime.Now.AddDays(7).Date.AddHours(16),  false, "#8b5cf6"),
                (u3.Id,    "QA Regression Testing",         "Full regression run before release.",          DateTime.Now.AddDays(4).Date.AddHours(9),  DateTime.Now.AddDays(4).Date.AddHours(17),  false, "#14b8a6"),
                (u3.Id,    "Book Club Meeting",             "Discuss this month's reading selection.",      DateTime.Now.AddDays(6).Date.AddHours(19), DateTime.Now.AddDays(6).Date.AddHours(20),  false, "#ec4899"),
                (u4.Id,    "Q3 Roadmap Planning",           "Define product roadmap for Q3 2025.",          DateTime.Now.AddDays(1).Date.AddHours(14), DateTime.Now.AddDays(1).Date.AddHours(16),  false, "#4f46e5"),
                (u4.Id,    "Team Building Day",             "Annual team outing and activities.",           DateTime.Now.AddDays(14).Date,             DateTime.Now.AddDays(14).Date.AddHours(23, 59, 0), true, "#10b981"),
                (admin.Id, "Company All-Hands",             "Quarterly all-hands meeting for all staff.",   DateTime.Now.AddDays(10).Date.AddHours(10),DateTime.Now.AddDays(10).Date.AddHours(12),  false, "#f59e0b"),
                (u1.Id,    "React Native Hackathon",        "48-hour hackathon to build MVP features.",     DateTime.Now.AddDays(20).Date,             DateTime.Now.AddDays(21).Date.AddHours(23, 59, 0), true, "#ef4444"),
            };

            foreach (var (uid, subj, desc, start, end, fullDay, color) in calendarDefs)
            {
                context.CalendarEvents.Add(new CalendarEvent
                {
                    Subject     = subj,
                    Description = desc,
                    StartTime   = start,
                    EndTime     = end,
                    IsFullDay   = fullDay,
                    ThemeColor  = color,
                    UserId      = uid
                });
            }
            await context.SaveChangesAsync();

            // ─── 8. Notifications ────────────────────────────────────────────────────────
            var notifDefs = new[]
            {
                (admin.Id, "System Ready",         "Welcome to TimeMaster! The platform is fully configured.",       false),
                (admin.Id, "Security Alert",       "New login detected from an unrecognised device. Please verify.", true),
                (u1.Id,    "Welcome!",             "Welcome to TimeMaster, Hương! Start with your first project.",   false),
                (u1.Id,    "Task Due Soon",        "'Setup CI/CD pipeline' is due in 2 days.",                       false),
                (u2.Id,    "Welcome!",             "Welcome to TimeMaster, Quỳnh! Your portfolio project is ready.", false),
                (u2.Id,    "Milestone Reached",    "Your SEO project has reached 50% progress. Great work!",         false),
                (u3.Id,    "Welcome!",             "Welcome to TimeMaster, Ngọc! Start tracking your goals today.",  false),
                (u3.Id,    "Goal Reminder",        "You haven't logged any progress on 'Reading Challenge' today.",   false),
                (u4.Id,    "Welcome!",             "Welcome to TimeMaster, Minh! Your team projects are ready.",     false),
                (u4.Id,    "Sprint Review Due",    "Sprint review for 'Q2 Sprint Planning' is due tomorrow.",        false),
                (u1.Id,    "New Member Joined",    "Minh Phạm joined your 'Fitness Tracker App' project.",           true),
                (u2.Id,    "Comment on Task",      "Admin left a comment on 'Optimise existing pages'.",             true),
            };

            foreach (var (uid, title, msg, isRead) in notifDefs)
            {
                if (!await context.Notifications.AnyAsync(n => n.UserId == uid && n.Title == title))
                {
                    context.Notifications.Add(new Notification
                    {
                        Title       = title,
                        Message     = msg,
                        TriggerTime = DateTime.Now.AddHours(-rng.Next(1, 72)),
                        CreatedAt   = DateTime.UtcNow.AddHours(-rng.Next(1, 72)),
                        IsRead      = isRead,
                        UserId      = uid
                    });
                }
            }
            await context.SaveChangesAsync();

            // ─── 9. ScheduledEvents ─────────────────────────────────────────────────────
            var someTasks = await context.WorkTasks.Take(10).ToListAsync();
            foreach (var task in someTasks)
            {
                if (rng.Next(0, 2) == 0) // 50% chance to have a scheduled slot
                {
                    context.ScheduledEvents.Add(new ScheduledEvent
                    {
                        TaskId    = task.Id,
                        StartTime = task.StartDate.AddHours(rng.Next(1, 4)),
                        EndTime   = task.StartDate.AddHours(rng.Next(5, 8)),
                        Color     = task.Color
                    });
                }
            }
            await context.SaveChangesAsync();

            // ─── 10. SharedTasks & SharedEvents ─────────────────────────────────────────
            // Share a task from u1 to u2
            var u1Task = await context.WorkTasks.FirstOrDefaultAsync(t => t.UserId == u1.Id);
            if (u1Task != null)
            {
                context.SharedTasks.Add(new SharedTask
                {
                    WorkTaskId       = u1Task.Id,
                    OwnerId          = u1.Id,
                    SharedWithUserId = u2.Id,
                    PermissionLevel  = "Edit"
                });
            }

            // Share an event from admin to u1
            var adminEvent = await context.CalendarEvents.FirstOrDefaultAsync(e => e.UserId == admin.Id);
            if (adminEvent != null)
            {
                context.SharedEvents.Add(new SharedEvent
                {
                    EventId          = adminEvent.Id,
                    OwnerId          = admin.Id,
                    SharedWithUserId = u1.Id,
                    PermissionLevel  = "View"
                });
            }
            await context.SaveChangesAsync();

            // ─── 11. TaskChangeRequests ─────────────────────────────────────────────────
            // A request from u2 to edit a task owned by admin
            var adminTask = await context.WorkTasks.FirstOrDefaultAsync(t => t.UserId == admin.Id);
            if (adminTask != null)
            {
                context.TaskChangeRequests.Add(new TaskChangeRequest
                {
                    TaskId      = adminTask.Id,
                    RequesterId = u2.Id,
                    OwnerId     = admin.Id,
                    Action      = TaskChangeAction.Edit,
                    Payload     = "{\"Title\": \"Updated Title by Seeder\", \"Description\": \"Collaborative edit request example.\"}",
                    Status      = TaskChangeStatus.Pending,
                    CreatedAt   = DateTime.Now.AddDays(-1)
                });
            }
            await context.SaveChangesAsync();
        }

        // ─── Helper ─────────────────────────────────────────────────────────────────────
        private static async Task<IdentityUser> CreateUserIfNotExists(
            UserManager<IdentityUser> userManager, string email, string password, string role)
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

        private static DateTime AddHours(this DateTime dt, int h, int m = 0, int s = 0)
            => dt.AddHours(h).AddMinutes(m).AddSeconds(s);
    }
}