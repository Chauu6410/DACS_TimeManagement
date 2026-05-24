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

            // ─── OPTIMIZATION: FAST-PATH GUARD ──────────────────────────────────────────
            // If the database is already fully initialized and seeded, we return immediately.
            // This bypasses context.Database.EnsureCreatedAsync(), UserManager, and RoleManager checks,
            // which saves several seconds on application startup.
            try
            {
                // We check if the database and the UserProfiles table exist and contain the admin's profile.
                if (await context.UserProfiles.AnyAsync(p => p.Email == "huongphanngocquynh@gmail.com"))
                {
                    Console.WriteLine(">>> Seed Data already exists. Skipping database seeding for ultra-fast startup.");
                    return;
                }
            }
            catch (Exception)
            {
                // If the database or tables do not exist yet, EF Core will throw an exception.
                // In this case, we proceed with database creation and seeding.
            }

            await context.Database.EnsureCreatedAsync();

            // ─── 1. Roles ───────────────────────────────────────────────────────────────
            if (!await roleManager.RoleExistsAsync("Admin")) await roleManager.CreateAsync(new IdentityRole("Admin"));
            if (!await roleManager.RoleExistsAsync("User"))  await roleManager.CreateAsync(new IdentityRole("User"));

            // ─── 2. Admin Users ─────────────────────────────────────────────────────────
            var admin1 = await CreateUserIfNotExists(userManager, "huongphanngocquynh@gmail.com", "Admin@123", "Admin");
            var admin2 = await CreateUserIfNotExists(userManager, "duongvobaochau.11a8@gmail.com", "Admin@123", "Admin");

            // ─── 3. Guard: skip if already seeded ───────────────────────────────────────
            // If the admin profile exists, we assume the seed data has already run.
            if (await context.UserProfiles.AnyAsync(p => p.UserId == admin1.Id)) return;

            // ─── 4. Member Users ────────────────────────────────────────────────────────
            var u1 = await CreateUserIfNotExists(userManager, "huongphan061005@gmail.com", "Member@123", "User");
            var u2 = await CreateUserIfNotExists(userManager, "member2@example.com", "Member@123", "User");
            var u3 = await CreateUserIfNotExists(userManager, "member3@example.com", "Member@123", "User");
            var u4 = await CreateUserIfNotExists(userManager, "member4@example.com", "Member@123", "User");

            var allUsers = new List<IdentityUser> { admin1, admin2, u1, u2, u3, u4 };

            // ─── 5. UserProfiles ────────────────────────────────────────────────────────
            var profileDefs = new[]
            {
                (admin1, "Phan Ngọc Quỳnh Hương", "IT",          "System Administrator", "dark",    "Dashboard"),
                (admin2, "Võ Bảo Châu Dương",     "IT",          "System Administrator", "dark",    "Dashboard"),
                (u1,     "Phan Hương",            "Engineering", "Senior Developer",     "light",   "Kanban"),
                (u2,     "Nguyễn Văn An",         "Marketing",   "Content Strategist",   "primary", "Dashboard"),
                (u3,     "Trần Thị Bình",         "QA",          "QA Engineer",          "light",   "Dashboard"),
                (u4,     "Lê Minh Tuấn",          "Management",  "Project Manager",      "dark",    "Dashboard"),
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

            // ─── 6. Projects ────────────────────────────────────────────────────────────
            var projectDefs = new[]
            {
                // Admin1 projects (huongphanngocquynh@gmail.com) - Reduced to 3 projects
                ("TimeMaster Management System",     "Enterprise time-management platform with AES-256 encryption.",               -30, admin1.Id),
                ("Cloud & AI Research Lab",          "Infrastructure deployment, MLOps pipelines and LLM experiments.",           -20, admin1.Id),
                ("Marketing Campaign Q3",            "Multi-channel product launch campaign for Q3 2025.",                        -10, admin1.Id),
                
                // Admin2 projects (duongvobaochau.11a8@gmail.com) - Reduced to 3 projects
                ("System Security Audit",            "Comprehensive security review and penetration testing.",                    -25, admin2.Id),
                ("Database Optimization Project",    "Performance tuning and query optimization for production databases.",       -15, admin2.Id),
                ("DevOps Pipeline Automation",       "Automated CI/CD pipeline with Docker and Kubernetes.",                      -20, admin2.Id),
                
                // Member u1 projects (huongphan061005@gmail.com) - Reduced to 3 projects
                ("Personal .NET Learning Plan",      "Structured upskilling in ASP.NET Core 8, EF Core and React.",               -25, u1.Id),
                ("Fitness Tracker App",              "Cross-platform mobile app for daily workouts and nutrition logging.",        -15, u1.Id),
                ("Blog Platform Development",        "Personal blog platform with CMS and comment system.",                       -21, u1.Id),
                
                // Other members - Reduced to 2 projects each
                ("Freelance Portfolio v2",           "Redesigned personal portfolio with Next.js and Framer Motion.",             -18, u2.Id),
                ("SEO & Content Strategy",           "Six-month SEO roadmap and blog content calendar.",                          -8,  u2.Id),
                ("Reading Challenge 2025",           "Track 52 books this year with reviews and rating scores.",                  -12, u3.Id),
                ("Daily Mindfulness Practice",       "Build a sustainable meditation habit with journaling.",                     -5,  u3.Id),
                ("Team Onboarding Playbook",         "Document and automate the onboarding process for new engineers.",           -22, u4.Id),
                ("Q2 Sprint Planning Dashboard",     "Centralised sprint planning and velocity tracking for three scrum teams.",  -14, u4.Id),
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
                // Admin1 projects (0-2)
                [0]  = new[]{"Design system architecture","Set up CI/CD pipeline","Implement AES-256 encryption module","Write unit tests for auth service","Deploy to staging environment","Conduct security audit","Fix JWT refresh-token bug","Optimise database query performance"},
                [1]  = new[]{"Provision AWS EKS cluster","Configure Terraform IaC","Train baseline NLP model","Integrate MLflow tracking","Build model serving API","Write GPU cost-optimisation report","Review LLM fine-tuning results","Deploy monitoring stack"},
                [2]  = new[]{"Craft brand messaging","Design social-media assets","Schedule LinkedIn posts","Run A/B test on landing page","Analyse campaign metrics","Coordinate with influencers","Produce promotional video","Publish Q3 retrospective"},
                
                // Admin2 projects (3-5)
                [3]  = new[]{"Conduct vulnerability assessment","Review access control policies","Implement security patches","Perform penetration testing","Document security findings","Update security protocols","Train staff on security","Generate audit report"},
                [4]  = new[]{"Analyse slow queries","Implement database indexing","Optimize table structures","Set up query caching","Monitor database performance","Review backup strategies","Implement partitioning","Document optimization results"},
                [5]  = new[]{"Set up Jenkins pipeline","Configure Docker containers","Deploy Kubernetes cluster","Implement automated testing","Add deployment rollback","Set up monitoring alerts","Document deployment process","Train development team"},
                
                // Member u1 projects (6-8)
                [6]  = new[]{"Study ASP.NET Core fundamentals","Build sample CRUD app","Learn EF Core migrations","Integrate SignalR real-time","Deploy app to Azure","Practice unit testing patterns","Read Clean Architecture book","Implement repository pattern"},
                [7]  = new[]{"Define app wireframes","Set up React Native project","Build workout logging screen","Implement nutrition API","Add progress chart component","Write integration tests","Publish to TestFlight","Collect beta feedback"},
                [8]  = new[]{"Design blog schema","Build article editor","Implement comment system","Add user authentication","Create admin dashboard","Optimize SEO","Deploy to hosting","Write first blog post"},
                
                // Other members (9-14)
                [9]  = new[]{"Create design system tokens","Build hero section","Add portfolio case studies","Implement dark mode","Optimise for Core Web Vitals","Add contact form backend","Write blog posts","Deploy to Vercel"},
                [10] = new[]{"Keyword research phase 1","Optimise existing pages","Create content calendar","Write 4 pillar articles","Build backlink outreach list","Analyse competitor gaps","Submit sitemap to GSC","Track monthly rankings"},
                [11] = new[]{"Set reading goal targets","Log January books","Log February books","Write mid-year review","Create Goodreads shelf","Design reading tracker UI","Share top-5 recommendations","Log Q3 reads"},
                [12] = new[]{"Install meditation app","Complete 7-day beginner course","Write daily journal entry","Try body-scan technique","Join mindfulness community","Log 30-day streak","Read 'The Miracle Morning'","Practice gratitude journaling"},
                [13] = new[]{"Draft onboarding checklist","Record tool walkthroughs","Set up Notion workspace","Automate account provisioning","Collect feedback from new hires","Review first-week survey results","Update handbook docs","Present playbook to HR"},
                [14] = new[]{"Set up Jira board","Define sprint ceremonies","Create velocity chart","Retrospective template","Backlog grooming guide","Integrate with Slack","Build burndown report","Train team leads"},
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
                // Admin1 events (huongphanngocquynh@gmail.com)
                (admin1.Id, "Sprint Planning – Q3",              "Plan tasks for the upcoming sprint.",                  DateTime.Now.AddDays(2).Date.AddHours(9),   DateTime.Now.AddDays(2).Date.AddHours(11),   false, "#4f46e5"),
                (admin1.Id, "Security Review Meeting",           "Monthly security posture review.",                     DateTime.Now.AddDays(5).Date.AddHours(14),  DateTime.Now.AddDays(5).Date.AddHours(15),   false, "#ef4444"),
                (admin1.Id, "Mobile App Design Review",          "Review mobile app UI/UX designs with team.",           DateTime.Now.AddDays(3).Date.AddHours(10),  DateTime.Now.AddDays(3).Date.AddHours(12),   false, "#0ea5e9"),
                (admin1.Id, "E-commerce Platform Demo",          "Demo new e-commerce features to stakeholders.",        DateTime.Now.AddDays(7).Date.AddHours(15),  DateTime.Now.AddDays(7).Date.AddHours(16),   false, "#10b981"),
                (admin1.Id, "API Gateway Architecture Meeting",  "Discuss API gateway implementation strategy.",         DateTime.Now.AddDays(4).Date.AddHours(13),  DateTime.Now.AddDays(4).Date.AddHours(14),   false, "#8b5cf6"),
                (admin1.Id, "Customer Analytics Workshop",       "Workshop on analytics dashboard requirements.",        DateTime.Now.AddDays(6).Date.AddHours(9),   DateTime.Now.AddDays(6).Date.AddHours(12),   false, "#f59e0b"),
                (admin1.Id, "Team Standup - Morning",            "Daily standup with development team.",                 DateTime.Now.Date.AddHours(9, 0, 0),        DateTime.Now.Date.AddHours(9, 15, 0),        false, "#10b981"),
                (admin1.Id, "Marketing Campaign Review",         "Review Q3 marketing campaign progress.",               DateTime.Now.AddDays(8).Date.AddHours(14),  DateTime.Now.AddDays(8).Date.AddHours(15),   false, "#ec4899"),
                (admin1.Id, "Company All-Hands Meeting",         "Quarterly all-hands meeting for all staff.",           DateTime.Now.AddDays(10).Date.AddHours(10), DateTime.Now.AddDays(10).Date.AddHours(12),  false, "#f59e0b"),
                (admin1.Id, "Technical Leadership Summit",       "Annual technical leadership conference.",              DateTime.Now.AddDays(30).Date,              DateTime.Now.AddDays(32).Date.AddHours(23, 59, 0), true, "#4f46e5"),
                (admin1.Id, "Code Review Session",               "Weekly code review with senior developers.",           DateTime.Now.AddDays(1).Date.AddHours(16),  DateTime.Now.AddDays(1).Date.AddHours(17),   false, "#0ea5e9"),
                (admin1.Id, "Product Roadmap Planning",          "Plan product roadmap for next quarter.",               DateTime.Now.AddDays(12).Date.AddHours(9),  DateTime.Now.AddDays(12).Date.AddHours(11),  false, "#8b5cf6"),
                
                // Admin2 events (duongvobaochau.11a8@gmail.com)
                (admin2.Id, "Database Performance Review",       "Review database optimization progress.",               DateTime.Now.AddDays(3).Date.AddHours(10),  DateTime.Now.AddDays(3).Date.AddHours(11),   false, "#0ea5e9"),
                (admin2.Id, "Security Audit Kickoff",            "Initial meeting for security audit project.",          DateTime.Now.AddDays(1).Date.AddHours(14),  DateTime.Now.AddDays(1).Date.AddHours(15),   false, "#ef4444"),
                (admin2.Id, "DevOps Pipeline Demo",              "Demonstrate new CI/CD pipeline features.",             DateTime.Now.AddDays(4).Date.AddHours(11),  DateTime.Now.AddDays(4).Date.AddHours(12),   false, "#10b981"),
                (admin2.Id, "Disaster Recovery Drill",           "Quarterly disaster recovery testing exercise.",        DateTime.Now.AddDays(15).Date.AddHours(8),  DateTime.Now.AddDays(15).Date.AddHours(17),  false, "#ef4444"),
                (admin2.Id, "Network Infrastructure Planning",   "Plan network upgrade implementation.",                 DateTime.Now.AddDays(5).Date.AddHours(13),  DateTime.Now.AddDays(5).Date.AddHours(15),   false, "#8b5cf6"),
                (admin2.Id, "Cloud Migration Workshop",          "Workshop on AWS cloud migration strategy.",            DateTime.Now.AddDays(7).Date.AddHours(9),   DateTime.Now.AddDays(7).Date.AddHours(12),   false, "#0ea5e9"),
                (admin2.Id, "Monitoring System Review",          "Review Prometheus and Grafana setup.",                 DateTime.Now.AddDays(6).Date.AddHours(14),  DateTime.Now.AddDays(6).Date.AddHours(15),   false, "#f59e0b"),
                (admin2.Id, "IT Team Standup",                   "Daily IT operations standup meeting.",                 DateTime.Now.Date.AddHours(9, 30, 0),       DateTime.Now.Date.AddHours(9, 45, 0),        false, "#10b981"),
                (admin2.Id, "Security Training Session",         "Security awareness training for IT staff.",            DateTime.Now.AddDays(9).Date.AddHours(10),  DateTime.Now.AddDays(9).Date.AddHours(12),   false, "#ef4444"),
                (admin2.Id, "Infrastructure Review Meeting",     "Monthly infrastructure health review.",                DateTime.Now.AddDays(11).Date.AddHours(15), DateTime.Now.AddDays(11).Date.AddHours(16),  false, "#8b5cf6"),
                (admin2.Id, "Backup System Maintenance",         "Scheduled maintenance for backup systems.",            DateTime.Now.AddDays(20).Date.AddHours(22), DateTime.Now.AddDays(21).Date.AddHours(2),   false, "#ef4444"),
                
                // Member u1 events (huongphan061005@gmail.com)
                (u1.Id,    "ASP.NET Core Workshop",             "Hands-on workshop with mentor.",                       DateTime.Now.AddDays(3).Date.AddHours(10),  DateTime.Now.AddDays(3).Date.AddHours(13),   false, "#0ea5e9"),
                (u1.Id,    "Team Standup",                      "Daily sync with the engineering team.",                DateTime.Now.Date.AddHours(9, 30, 0),       DateTime.Now.Date.AddHours(9, 45, 0),        false, "#10b981"),
                (u1.Id,    "Fitness App User Testing",          "Conduct user testing for fitness tracker app.",        DateTime.Now.AddDays(5).Date.AddHours(14),  DateTime.Now.AddDays(5).Date.AddHours(16),   false, "#ec4899"),
                (u1.Id,    "React Native Study Group",          "Weekly study group for React Native learning.",        DateTime.Now.AddDays(2).Date.AddHours(19),  DateTime.Now.AddDays(2).Date.AddHours(21),   false, "#8b5cf6"),
                (u1.Id,    "Blog Platform Design Review",       "Review blog platform design with peers.",              DateTime.Now.AddDays(4).Date.AddHours(15),  DateTime.Now.AddDays(4).Date.AddHours(16),   false, "#f59e0b"),
                (u1.Id,    "Task Management Demo",              "Demo task management tool to team.",                   DateTime.Now.AddDays(8).Date.AddHours(11),  DateTime.Now.AddDays(8).Date.AddHours(12),   false, "#10b981"),
                (u1.Id,    "Code Review Session",               "Peer code review for recent projects.",                DateTime.Now.AddDays(1).Date.AddHours(16),  DateTime.Now.AddDays(1).Date.AddHours(17),   false, "#0ea5e9"),
                (u1.Id,    "Weather App API Integration",       "Work session for weather API integration.",            DateTime.Now.AddDays(6).Date.AddHours(10),  DateTime.Now.AddDays(6).Date.AddHours(12),   false, "#14b8a6"),
                (u1.Id,    "Recipe Platform Launch Meeting",    "Planning meeting for recipe platform launch.",         DateTime.Now.AddDays(9).Date.AddHours(13),  DateTime.Now.AddDays(9).Date.AddHours(14),   false, "#ec4899"),
                (u1.Id,    "Budget Tracker Development",        "Development session for budget tracker.",              DateTime.Now.AddDays(7).Date.AddHours(14),  DateTime.Now.AddDays(7).Date.AddHours(17),   false, "#f59e0b"),
                (u1.Id,    "Tech Meetup - .NET Community",      "Local .NET developer community meetup.",               DateTime.Now.AddDays(12).Date.AddHours(18), DateTime.Now.AddDays(12).Date.AddHours(21),  false, "#4f46e5"),
                (u1.Id,    "Personal Project Review",           "Monthly review of personal project progress.",         DateTime.Now.AddDays(14).Date.AddHours(10), DateTime.Now.AddDays(14).Date.AddHours(11),  false, "#8b5cf6"),
                (u1.Id,    "Hackathon Weekend",                 "48-hour hackathon to build MVP features.",             DateTime.Now.AddDays(20).Date,              DateTime.Now.AddDays(21).Date.AddHours(23, 59, 0), true, "#ef4444"),
                
                // Other members
                (u2.Id,    "Content Calendar Review",           "Review and schedule blog posts.",                      DateTime.Now.AddDays(1).Date.AddHours(11),  DateTime.Now.AddDays(1).Date.AddHours(12),   false, "#f59e0b"),
                (u2.Id,    "SEO Audit Presentation",            "Present findings to the stakeholders.",                DateTime.Now.AddDays(7).Date.AddHours(15),  DateTime.Now.AddDays(7).Date.AddHours(16),   false, "#8b5cf6"),
                (u3.Id,    "QA Regression Testing",             "Full regression run before release.",                  DateTime.Now.AddDays(4).Date.AddHours(9),   DateTime.Now.AddDays(4).Date.AddHours(17),   false, "#14b8a6"),
                (u3.Id,    "Book Club Meeting",                 "Discuss this month's reading selection.",              DateTime.Now.AddDays(6).Date.AddHours(19),  DateTime.Now.AddDays(6).Date.AddHours(20),   false, "#ec4899"),
                (u4.Id,    "Q3 Roadmap Planning",               "Define product roadmap for Q3 2025.",                  DateTime.Now.AddDays(1).Date.AddHours(14),  DateTime.Now.AddDays(1).Date.AddHours(16),   false, "#4f46e5"),
                (u4.Id,    "Team Building Day",                 "Annual team outing and activities.",                   DateTime.Now.AddDays(14).Date,              DateTime.Now.AddDays(14).Date.AddHours(23, 59, 0), true, "#10b981"),
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
                // Admin1 notifications (huongphanngocquynh@gmail.com)
                (admin1.Id, "System Ready",                "Welcome to TimeMaster! The platform is fully configured.",       false),
                (admin1.Id, "Security Alert",              "New login detected from an unrecognised device. Please verify.", true),
                (admin1.Id, "Project Milestone",           "TimeMaster Management System reached 75% completion!",           false),
                (admin1.Id, "Task Due Soon",               "'Deploy to staging environment' is due in 2 days.",              false),
                (admin1.Id, "Team Update",                 "3 new tasks assigned to you in Mobile App Development.",         false),
                (admin1.Id, "Meeting Reminder",            "Sprint Planning meeting starts in 1 hour.",                      false),
                (admin1.Id, "Code Review Request",         "New pull request requires your review.",                         true),
                (admin1.Id, "API Gateway Update",          "API Gateway Implementation project updated by team member.",     false),
                (admin1.Id, "Performance Alert",           "Customer Analytics Dashboard showing high load.",                true),
                (admin1.Id, "Deployment Success",          "E-commerce Platform successfully deployed to production.",       false),
                
                // Admin2 notifications (duongvobaochau.11a8@gmail.com)
                (admin2.Id, "Welcome Admin",               "Welcome to TimeMaster! Your admin account is ready.",            false),
                (admin2.Id, "Database Alert",              "Database performance metrics require attention.",                false),
                (admin2.Id, "Security Scan Complete",      "Security audit scan completed. 3 issues found.",                 true),
                (admin2.Id, "Backup Status",               "Daily backup completed successfully.",                           false),
                (admin2.Id, "Network Upgrade",             "Network Infrastructure Upgrade project at 60% completion.",      false),
                (admin2.Id, "Cloud Migration",             "AWS migration for 2 applications completed.",                    false),
                (admin2.Id, "Monitoring Alert",            "CPU usage exceeded 80% on production server.",                   true),
                (admin2.Id, "DevOps Pipeline",             "CI/CD pipeline deployed successfully.",                          false),
                (admin2.Id, "Disaster Recovery Test",      "DR drill scheduled for next week. Please review procedures.",    false),
                (admin2.Id, "System Maintenance",          "Scheduled maintenance window approved for this weekend.",        false),
                
                // Member u1 notifications (huongphan061005@gmail.com)
                (u1.Id,    "Welcome!",                     "Welcome to TimeMaster, Hương! Start with your first project.",   false),
                (u1.Id,    "Task Due Soon",                "'Build workout logging screen' is due in 2 days.",               false),
                (u1.Id,    "Learning Progress",            "You've completed 5 out of 8 tasks in .NET Learning Plan!",       false),
                (u1.Id,    "New Member Joined",            "A team member joined your 'Fitness Tracker App' project.",       true),
                (u1.Id,    "Goal Achievement",             "Congratulations! You've reached your weekly coding goal.",       false),
                (u1.Id,    "Workshop Reminder",            "ASP.NET Core Workshop starts tomorrow at 10 AM.",                false),
                (u1.Id,    "Code Review Feedback",         "Your code review received positive feedback from mentor.",       true),
                (u1.Id,    "Blog Post Published",          "Your first blog post is now live on the platform!",              false),
                (u1.Id,    "App Store Submission",         "Fitness Tracker App submitted to TestFlight successfully.",      false),
                (u1.Id,    "Hackathon Registration",       "You're registered for the React Native Hackathon!",              false),
                (u1.Id,    "Task Completed",               "Great job! 'Implement nutrition API' marked as complete.",       false),
                (u1.Id,    "Project Update",               "Weather Forecast App project updated with new features.",        false),
                
                // Other members
                (u2.Id,    "Welcome!",                     "Welcome to TimeMaster! Your portfolio project is ready.",        false),
                (u2.Id,    "Milestone Reached",            "Your SEO project has reached 50% progress. Great work!",         false),
                (u3.Id,    "Welcome!",                     "Welcome to TimeMaster! Start tracking your goals today.",        false),
                (u3.Id,    "Goal Reminder",                "You haven't logged any progress on 'Reading Challenge' today.",   false),
                (u4.Id,    "Welcome!",                     "Welcome to TimeMaster! Your team projects are ready.",           false),
                (u4.Id,    "Sprint Review Due",            "Sprint review for 'Q2 Sprint Planning' is due tomorrow.",        false),
                (u2.Id,    "Comment on Task",              "Admin left a comment on 'Optimise existing pages'.",             true),
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

            // Share an event from admin1 to u1
            var admin1Event = await context.CalendarEvents.FirstOrDefaultAsync(e => e.UserId == admin1.Id);
            if (admin1Event != null)
            {
                context.SharedEvents.Add(new SharedEvent
                {
                    EventId          = admin1Event.Id,
                    OwnerId          = admin1.Id,
                    SharedWithUserId = u1.Id,
                    PermissionLevel  = "View"
                });
            }
            await context.SaveChangesAsync();

            // ─── 11. TaskChangeRequests ─────────────────────────────────────────────────
            // A request from u2 to edit a task owned by admin1
            var admin1Task = await context.WorkTasks.FirstOrDefaultAsync(t => t.UserId == admin1.Id);
            if (admin1Task != null)
            {
                context.TaskChangeRequests.Add(new TaskChangeRequest
                {
                    TaskId      = admin1Task.Id,
                    RequesterId = u2.Id,
                    OwnerId     = admin1.Id,
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