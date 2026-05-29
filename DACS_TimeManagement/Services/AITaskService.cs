using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DACS_TimeManagement.Controllers;
using DACS_TimeManagement.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DACS_TimeManagement.Services
{
    public interface IAITaskService
    {
        List<SuggestedTaskDTO> ExtractTasksFromMarkdown(string markdown);
        Task<(bool success, int count, string message)> ImportTasksToProject(int projectId, string userId, List<SuggestedTaskDTO> tasks);
    }

    public class AITaskService : IAITaskService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<AITaskService> _logger;

        public AITaskService(ApplicationDbContext db, ILogger<AITaskService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public List<SuggestedTaskDTO> ExtractTasksFromMarkdown(string markdown)
        {
            var tasks = new List<SuggestedTaskDTO>();

            try
            {
                // Find json-tasks code block
                var jsonTasksPattern = @"```json-tasks\s*([\s\S]*?)\s*```";
                var match = Regex.Match(markdown, jsonTasksPattern);

                if (match.Success)
                {
                    var jsonContent = match.Groups[1].Value.Trim();
                    _logger.LogInformation("Found json-tasks block with {Length} characters", jsonContent.Length);

                    var options = new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        AllowTrailingCommas = true
                    };

                    tasks = System.Text.Json.JsonSerializer.Deserialize<List<SuggestedTaskDTO>>(jsonContent, options)
                            ?? new List<SuggestedTaskDTO>();

                    _logger.LogInformation("Successfully parsed {Count} tasks from JSON", tasks.Count);
                }
                else
                {
                    _logger.LogWarning("No json-tasks block found in markdown");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing json-tasks block");
            }

            return tasks;
        }

        public async Task<(bool success, int count, string message)> ImportTasksToProject(
            int projectId, 
            string userId, 
            List<SuggestedTaskDTO> tasks)
        {
            try
            {
                var project = await _db.Set<Project>()
                    .Include(p => p.BoardLists)
                    .Include(p => p.Tasks)
                    .FirstOrDefaultAsync(p => p.Id == projectId && 
                        (p.UserId == userId || p.Members.Any(pm => pm.UserId == userId)));

                if (project == null)
                {
                    return (false, 0, "Project not found or access denied");
                }

                // Ensure board lists exist
                var boardLists = await EnsureBoardListsExist(projectId);

                var tasksToAdd = new List<WorkTask>();
                var existingTaskKeys = project.Tasks
                    .Where(t => t.AITaskKey != null)
                    .Select(t => t.AITaskKey)
                    .ToHashSet();

                var existingTaskTitles = project.Tasks
                    .Select(t => t.Title.ToLower().Trim())
                    .ToHashSet();

                // Get max position for proper ordering
                var maxPosition = project.Tasks.Any() 
                    ? project.Tasks.Max(t => t.Position) 
                    : 0;

                foreach (var taskDto in tasks)
                {
                    // Validate task
                    if (string.IsNullOrWhiteSpace(taskDto.Title))
                    {
                        _logger.LogWarning("Skipping task with empty title");
                        continue;
                    }

                    // Check by Key first (for cross-language sync)
                    if (!string.IsNullOrEmpty(taskDto.Key) && existingTaskKeys.Contains(taskDto.Key))
                    {
                        _logger.LogInformation("Skipping duplicate task by key: {Key}", taskDto.Key);
                        continue;
                    }

                    // Fallback to Title check
                    var normalizedTitle = taskDto.Title.ToLower().Trim();
                    if (existingTaskTitles.Contains(normalizedTitle))
                    {
                        _logger.LogInformation("Skipping duplicate task by title: {Title}", taskDto.Title);
                        continue;
                    }

                    // Determine board list based on category
                    var boardList = DetermineBoardList(boardLists, taskDto.Category);

                    // Parse priority
                    var priority = ParsePriority(taskDto.Priority);

                    // Calculate dates
                    var estimatedDays = taskDto.EstimatedDays > 0 ? taskDto.EstimatedDays : 7;
                    var startDate = DateTime.Now;
                    var endDate = startDate.AddDays(estimatedDays);

                    maxPosition++;
                    tasksToAdd.Add(new WorkTask
                    {
                        ProjectId = projectId,
                        BoardListId = boardList.Id,
                        UserId = userId,
                        Title = taskDto.Title,
                        Description = taskDto.Description ?? "",
                        AITaskKey = taskDto.Key,
                        Status = Models.TaskStatus.Todo,
                        Priority = priority,
                        StartDate = startDate,
                        EndDate = endDate,
                        Position = maxPosition,
                        Progress = 0
                    });

                    _logger.LogInformation("Prepared task for import: {Title} (Priority: {Priority}, Days: {Days})", 
                        taskDto.Title, priority, estimatedDays);
                }

                if (tasksToAdd.Any())
                {
                    await _db.Set<WorkTask>().AddRangeAsync(tasksToAdd);
                    await _db.SaveChangesAsync();
                    
                    var message = $"Successfully added {tasksToAdd.Count} task(s) to Kanban board";
                    _logger.LogInformation(message);
                    return (true, tasksToAdd.Count, message);
                }

                return (true, 0, "No new tasks to add (all tasks already exist)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing tasks to project {ProjectId}", projectId);
                return (false, 0, $"Error: {ex.Message}");
            }
        }

        private async Task<Dictionary<string, BoardList>> EnsureBoardListsExist(int projectId)
        {
            var existingLists = await _db.Set<BoardList>()
                .Where(bl => bl.ProjectId == projectId)
                .ToListAsync();

            var result = new Dictionary<string, BoardList>();

            // Define default board lists
            var defaultLists = new[] { "To Do", "In Progress", "Done" };

            for (int i = 0; i < defaultLists.Length; i++)
            {
                var listName = defaultLists[i];
                var existing = existingLists.FirstOrDefault(bl => bl.Name == listName);

                if (existing == null)
                {
                    existing = new BoardList
                    {
                        Name = listName,
                        ProjectId = projectId,
                        Position = i
                    };
                    _db.Set<BoardList>().Add(existing);
                    await _db.SaveChangesAsync();
                    _logger.LogInformation("Created board list: {Name} for project {ProjectId}", listName, projectId);
                }

                result[listName] = existing;
            }

            return result;
        }

        private BoardList DetermineBoardList(Dictionary<string, BoardList> boardLists, string category)
        {
            // All new AI-suggested tasks start in "To Do"
            // In the future, we could map categories to different lists
            return boardLists.GetValueOrDefault("To Do") ?? boardLists.Values.First();
        }

        private Priority ParsePriority(string priorityStr)
        {
            if (string.IsNullOrEmpty(priorityStr)) return Priority.Medium;

            return priorityStr.ToLower() switch
            {
                "low" => Priority.Low,
                "medium" => Priority.Medium,
                "high" => Priority.High,
                "urgent" => Priority.Urgent,
                _ => Priority.Medium
            };
        }
    }
}
