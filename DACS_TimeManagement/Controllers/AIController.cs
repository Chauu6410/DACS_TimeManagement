using System;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using DACS_TimeManagement.Models;
using DACS_TimeManagement.DTOs;
using DACS_TimeManagement.Services.Interfaces;
using DACS_TimeManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace DACS_TimeManagement.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AIController : ControllerBase
    {
        private readonly IGeminiService _geminiService;
        private readonly ApplicationDbContext _db;

        public AIController(IGeminiService geminiService, ApplicationDbContext db)
        {
            _geminiService = geminiService;
            _db = db;
        }

        [HttpPost("generate")]
        public async Task<IActionResult> GeneratePlan([FromBody] AIRequestDTO request)
        {
            if (request == null || request.Goal == null)
            {
                return BadRequest("Invalid request data.");
            }

            try
            {
                var userId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var profile = await _db.Set<UserProfile>().AsNoTracking().FirstOrDefaultAsync(up => up.UserId == userId);
                var lang = profile?.Language ?? System.Threading.Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName ?? "en";
                var isVi = lang.StartsWith("vi", StringComparison.OrdinalIgnoreCase);

                string context = isVi
                    ? "Bạn là một Chuyên gia Cố vấn Chiến lược và Huấn luyện viên Hiệu suất cao cấp (Senior Performance Coach)."
                    : "You are a Senior Strategy Advisor and Performance Coach.";

                string goalText = isVi
                    ? @"Nhiệm vụ: Phân tích mục tiêu người dùng và lập kế hoạch hành động tối ưu.
Yêu cầu:
1. Đánh giá độ khó (Dễ/Trung bình/Khó/Rất khó) dựa trên thời gian còn lại và khối lượng công việc.
2. Phân tích SWOT ngắn gọn (Điểm mạnh/Yếu/Cơ hội/Thách thức).
3. Đề xuất 3-5 cột mốc (Milestones) quan trọng.
4. Đưa ra 3 lời khuyên ngắn gọn thực tế để tránh trì hoãn.

Định dạng: Sử dụng Markdown chuyên nghiệp, trình bày thoáng đãng, ngôn ngữ truyền cảm hứng nhưng thực tế. Trả lời bằng tiếng Việt."
                    : @"Task: Analyze user goals and create an optimal action plan.
Requirements:
1. Assess difficulty (Easy/Medium/Hard/Very Hard) based on remaining time and workload.
2. Brief SWOT analysis (Strengths/Weaknesses/Opportunities/Threats).
3. Propose 3-5 key Milestones.
4. Give 3 practical tips to avoid procrastination.

Format: Use professional Markdown, clean layout, inspiring yet practical language. Answer in English.";

                string progressInfo = isVi
                    ? (request.Goal.Type == "TaskBased" ? $"Tiến độ: {request.Goal.CompletedTasks}/{request.Goal.TargetTasks} task." : $"Tiến độ: {request.Goal.CompletedHours:F1}/{request.Goal.TargetHours:F1} giờ.")
                    : (request.Goal.Type == "TaskBased" ? $"Progress: {request.Goal.CompletedTasks}/{request.Goal.TargetTasks} tasks." : $"Progress: {request.Goal.CompletedHours:F1}/{request.Goal.TargetHours:F1} hours.");

                string userInput = isVi 
                    ? $@"
Dữ liệu Mục tiêu:
- Tiêu đề: {request.Goal.Title}
- Mô tả: {request.Goal.Description}
- Trạng thái: {request.Goal.Status}
- {progressInfo}
- Hạn chót: {request.Goal.TargetDate:dd/MM/yyyy}

Dự án: {request.Project?.Name ?? "N/A"}
Chi tiết: {request.Project?.Detail ?? "N/A"}
"
                    : $@"
Goal Data:
- Title: {request.Goal.Title}
- Description: {request.Goal.Description}
- Status: {request.Goal.Status}
- {progressInfo}
- Deadline: {request.Goal.TargetDate:dd/MM/yyyy}

Project: {request.Project?.Name ?? "N/A"}
Details: {request.Project?.Detail ?? "N/A"}
";


                string prompt = _geminiService.BuildAdvancedPrompt(context, goalText, userInput);

                // Use a slightly higher temperature for goal strategy to allow creative suggestions
                string aiResult = await _geminiService.GenerateContent(prompt, 0.5, CancellationToken.None);

                if (string.IsNullOrWhiteSpace(aiResult))
                {
                    return StatusCode(500, new { error = "AI trả về kết quả rỗng. Vui lòng thử lại sau." });
                }

                return Ok(new { result = aiResult });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("stream-plan")]
        public async Task StreamPlan([FromQuery] int goalId, CancellationToken cancellationToken)
        {
            Response.ContentType = "text/event-stream";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["Connection"] = "keep-alive";

            try
            {
                var goal = await _db.Set<PersonalGoal>().Include(g => g.Project).FirstOrDefaultAsync(g => g.Id == goalId, cancellationToken);
                if (goal == null)
                {
                    await Response.WriteAsync("data: {\"error\": \"Goal not found\"}\n\n", cancellationToken);
                    return;
                }

                var userId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var profile = await _db.Set<UserProfile>().AsNoTracking().FirstOrDefaultAsync(up => up.UserId == userId, cancellationToken);
                var lang = profile?.Language ?? System.Threading.Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName ?? "en";
                var isVi = lang.StartsWith("vi", StringComparison.OrdinalIgnoreCase);

                string context = isVi
                    ? "Bạn là Chuyên gia Chiến lược Hiệu suất."
                    : "You are a Performance Strategy Expert.";

                string goalText = isVi
                    ? "Lập kế hoạch hành động: Độ khó, SWOT (ngắn), Milestones (3-5), Lời khuyên (3). Markdown, Tiếng Việt."
                    : "Create action plan: Difficulty, SWOT (brief), Milestones (3-5), Tips (3). Markdown, English.";

                string progressInfo = isVi
                    ? (goal.Type == GoalType.TaskBased ? $"Tiến độ: {goal.CompletedTasks}/{goal.TargetTasks} task." : $"Tiến độ: {goal.CompletedHours:F1}/{goal.TargetHours:F1} giờ.")
                    : (goal.Type == GoalType.TaskBased ? $"Progress: {goal.CompletedTasks}/{goal.TargetTasks} tasks." : $"Progress: {goal.CompletedHours:F1}/{goal.TargetHours:F1} hours.");

                string userInput = isVi 
                    ? $"Mục tiêu: {goal.Title}\nMô tả: {goal.Description}\n{progressInfo}\nHạn chót: {goal.TargetDate:dd/MM/yyyy}"
                    : $"Goal: {goal.Title}\nDescription: {goal.Description}\n{progressInfo}\nDeadline: {goal.TargetDate:dd/MM/yyyy}";

                string prompt = _geminiService.BuildAdvancedPrompt(context, goalText, userInput);

                var fullResult = new System.Text.StringBuilder();
                await foreach (var chunk in _geminiService.StreamGenerateContent(prompt, 0.5, cancellationToken))
                {
                    fullResult.Append(chunk);
                    // Escape data for SSE
                    var escapedChunk = System.Text.Json.JsonSerializer.Serialize(chunk);
                    await Response.WriteAsync($"data: {escapedChunk}\n\n", cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                }

                // Save to DB after successful stream
                if (fullResult.Length > 0)
                {
                    goal.AIActionPlan = fullResult.ToString();
                    goal.UpdatedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                var error = System.Text.Json.JsonSerializer.Serialize(new { error = ex.Message });
                await Response.WriteAsync($"data: {error}\n\n", cancellationToken);
            }
        }

        [HttpGet("stream-project-strategy")]
        public async Task StreamProjectStrategy([FromQuery] int projectId, CancellationToken cancellationToken)
        {
            Response.ContentType = "text/event-stream";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["Connection"] = "keep-alive";

            try
            {
                var project = await _db.Set<Project>()
                    .Include(p => p.Tasks)
                    .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);

                if (project == null)
                {
                    await Response.WriteAsync("data: {\"error\": \"Project not found\"}\n\n", cancellationToken);
                    return;
                }

                var userId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var profile = await _db.Set<UserProfile>().AsNoTracking().FirstOrDefaultAsync(up => up.UserId == userId, cancellationToken);
                var lang = profile?.Language ?? System.Threading.Thread.CurrentThread.CurrentUICulture.Name ?? "vi";
                var isVi = lang.StartsWith("vi", StringComparison.OrdinalIgnoreCase);

                string context = isVi
                    ? "Bạn là một Chuyên gia Cố vấn Chiến lược và Huấn luyện viên Hiệu suất cao cấp (Senior Performance Coach)."
                    : "You are a Senior Strategy Advisor and Performance Coach.";

                string goalText = isVi
                    ? @"Nhiệm vụ: Phân tích dự án và lập kế hoạch thực hiện tối ưu.
Yêu cầu:
1. Đánh giá độ phức tạp của dự án.
2. Phân tích SWOT (Điểm mạnh/Yếu/Cơ hội/Thách thức).
3. Đề xuất lộ trình thực hiện với các giai đoạn cụ thể.
4. Gợi ý 3-5 tác vụ quan trọng cần làm ngay.
5. Đưa ra 3 lời khuyên thực tế để quản lý dự án hiệu quả.

BẮT BUỘC: Cuối bản kế hoạch, bạn PHẢI thêm một khối mã JSON để hệ thống có thể tạo các tác vụ mẫu:
```json-tasks
[
  { ""key"": ""task_1"", ""title"": ""Tên task 1"", ""description"": ""Mô tả ngắn gọn 1"" },
  { ""key"": ""task_2"", ""title"": ""Tên task 2"", ""description"": ""Mô tả ngắn gọn 2"" }
]
```
Ghi chú: 
- Nếu đây là dự án mới, hãy tạo các task mới với key (task_1, task_2,...).
- KHÔNG thêm các lời giải thích hay lưu ý ngoài lề về việc thiếu dữ liệu json-tasks. Chỉ tập trung vào bản kế hoạch và khối mã JSON.

Định dạng: Sử dụng Markdown chuyên nghiệp. Trả lời bằng tiếng Việt."
                    : @"Task: Analyze the project and create an optimal implementation plan.
Requirements:
1. Assess project complexity.
2. Brief SWOT analysis.
3. Propose a roadmap.
4. Suggest 3-5 critical tasks.
5. Give 3 practical tips.

MANDATORY: At the end of the plan, add a JSON code block for task templates:
```json-tasks
[
  { ""key"": ""task_1"", ""title"": ""Task name 1"", ""description"": ""Description 1"" },
  { ""key"": ""task_2"", ""title"": ""Task name 2"", ""description"": ""Description 2"" }
]
```
Note:
- If this is a new project, generate new tasks with keys (task_1, task_2,...).
- DO NOT add meta-comments or notes about missing json-tasks blocks. Focus only on the strategy and the JSON block.

Format: Use professional Markdown. Answer in English.";

                string existingTasksInfo = isVi ? "Các tác vụ hiện có: " : "Existing tasks: ";
                if (project.Tasks != null && project.Tasks.Any())
                {
                    var taskSummaries = project.Tasks.Select(t => $"- {t.Title} (Key: {t.AITaskKey ?? "None"})");
                    existingTasksInfo += string.Join("\n", taskSummaries);
                }
                else
                {
                    existingTasksInfo += isVi ? "Chưa có tác vụ nào." : "No tasks yet.";
                }

                string userInput = isVi 
                    ? $"Dự án: {project.Name}\nChi tiết: {project.Description ?? "Không có mô tả"}\n{existingTasksInfo}"
                    : $"Project: {project.Name}\nDetails: {project.Description ?? "No description"}\n{existingTasksInfo}";

                string prompt = _geminiService.BuildAdvancedPrompt(context, goalText, userInput);

                var fullResult = new System.Text.StringBuilder();
                await foreach (var chunk in _geminiService.StreamGenerateContent(prompt, 0.5, cancellationToken))
                {
                    fullResult.Append(chunk);
                    var escapedChunk = System.Text.Json.JsonSerializer.Serialize(chunk);
                    await Response.WriteAsync($"data: {escapedChunk}\n\n", cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                }

                // Save to DB after successful stream
                if (fullResult.Length > 0)
                {
                    if (isVi)
                    {
                        project.AIStrategyVi = fullResult.ToString();
                        project.AIStrategyEn = null; // Clear stale English version to force re-translation
                    }
                    else
                    {
                        project.AIStrategyEn = fullResult.ToString();
                        project.AIStrategyVi = null; // Clear stale Vietnamese version to force re-translation
                    }
                    
                    await _db.SaveChangesAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                var error = System.Text.Json.JsonSerializer.Serialize(new { error = ex.Message });
                await Response.WriteAsync($"data: {error}\n\n", cancellationToken);
            }
        }


        [HttpPost("translate-strategy")]
        public async Task<IActionResult> TranslateStrategy([FromBody] TranslateRequestDTO request)
        {
            try
            {
                var project = await _db.Set<Project>().FirstOrDefaultAsync(p => p.Id == request.ProjectId);
                if (project == null) return NotFound();

                var isVi = request.TargetLang == "vi";
                string sourceText = isVi ? project.AIStrategyEn : project.AIStrategyVi;
                if (string.IsNullOrEmpty(sourceText)) return BadRequest("No source strategy to translate.");

                string context = "Bạn là một chuyên gia dịch thuật chuyên nghiệp, chuyên ngành Quản trị Dự án.";
                string goal = isVi 
                    ? "Dịch bản kế hoạch chiến lược sau đây sang tiếng Việt. Giữ nguyên định dạng Markdown và các emoji. ĐẶC BIỆT: Trong khối mã json-tasks (nếu có), PHẢI giữ nguyên giá trị trường \"key\", chỉ dịch \"title\" và \"description\". KHÔNG thêm bất kỳ lời giải thích hay lưu ý nào về việc thiếu dữ liệu hoặc không thể thực hiện quy tắc."
                    : "Translate the following strategy plan into English. Maintain Markdown formatting and emojis. SPECIAL: In the json-tasks block (if any), MUST keep \"key\" values unchanged, only translate \"title\" and \"description\". DO NOT add any explanations or notes about missing data or inability to follow rules.";
                
                string prompt = _geminiService.BuildAdvancedPrompt(context, goal, sourceText);
                string translatedText = await _geminiService.GenerateContent(prompt, 0.2, CancellationToken.None);

                if (request.TargetLang == "vi") project.AIStrategyVi = translatedText;
                else project.AIStrategyEn = translatedText;

                await _db.SaveChangesAsync();
                return Ok(new { result = translatedText });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("import-tasks")]
        public async Task<IActionResult> ImportSuggestedTasks([FromBody] ImportTasksRequestDTO request)
        {
            try
            {
                var project = await _db.Set<Project>().FirstOrDefaultAsync(p => p.Id == request.ProjectId);
                if (project == null) return NotFound();

                var userId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                
                // Find or create a default BoardList (column) for tasks
                var boardList = await _db.Set<BoardList>()
                    .Where(bl => bl.ProjectId == project.Id)
                    .OrderBy(bl => bl.Position)
                    .FirstOrDefaultAsync();

                if (boardList == null)
                {
                    boardList = new BoardList 
                    { 
                        Name = "To Do", 
                        ProjectId = project.Id, 
                        Position = 0 
                    };
                    _db.Set<BoardList>().Add(boardList);
                    await _db.SaveChangesAsync();
                }

                var tasksToAdd = new List<WorkTask>();
                var existingTaskKeys = await _db.Set<WorkTask>()
                    .Where(t => t.ProjectId == project.Id && t.AITaskKey != null)
                    .Select(t => t.AITaskKey)
                    .ToListAsync();
                
                var existingTaskTitles = await _db.Set<WorkTask>()
                    .Where(t => t.ProjectId == project.Id)
                    .Select(t => t.Title.ToLower().Trim())
                    .ToListAsync();

                foreach (var taskDto in request.Tasks)
                {
                    // Check by Key first (for cross-language sync)
                    if (!string.IsNullOrEmpty(taskDto.Key) && existingTaskKeys.Contains(taskDto.Key)) continue;

                    // Fallback to Title check
                    var normalizedTitle = taskDto.Title.ToLower().Trim();
                    if (existingTaskTitles.Contains(normalizedTitle)) continue; 

                    tasksToAdd.Add(new WorkTask
                    {
                        ProjectId = project.Id,
                        BoardListId = boardList.Id, 
                        UserId = userId,
                        Title = taskDto.Title,
                        Description = taskDto.Description,
                        AITaskKey = taskDto.Key, // Store the unique key
                        Status = Models.TaskStatus.Todo,
                        Priority = Models.Priority.Medium,
                        StartDate = DateTime.Now,
                        EndDate = DateTime.Now.AddDays(7),
                        Position = 0
                    });
                }

                await _db.Set<WorkTask>().AddRangeAsync(tasksToAdd);
                await _db.SaveChangesAsync();

                return Ok(new { success = true, count = tasksToAdd.Count });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }

    public class TranslateRequestDTO
    {
        public int ProjectId { get; set; }
        public string TargetLang { get; set; }
    }

    public class ImportTasksRequestDTO
    {
        public int ProjectId { get; set; }
        public List<SuggestedTaskDTO> Tasks { get; set; }
    }

    public class SuggestedTaskDTO
    {
        public string Key { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
    }
}