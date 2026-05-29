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
using Microsoft.Extensions.Logging;

namespace DACS_TimeManagement.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AIController : ControllerBase
    {
        private readonly IGeminiService _geminiService;
        private readonly ApplicationDbContext _db;
        private readonly ILogger<AIController> _logger;
        private readonly IAITaskService _aiTaskService;

        public AIController(
            IGeminiService geminiService, 
            ApplicationDbContext db, 
            ILogger<AIController> logger,
            IAITaskService aiTaskService)
        {
            _geminiService = geminiService;
            _db = db;
            _logger = logger;
            _aiTaskService = aiTaskService;
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
                var lang = System.Threading.Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName;
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
                var lang = System.Threading.Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName;
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
                    if (isVi)
                    {
                        goal.AIActionPlanVi = fullResult.ToString();
                        goal.AIActionPlanEn = null; // Clear stale English version to force re-translation
                    }
                    else
                    {
                        goal.AIActionPlanEn = fullResult.ToString();
                        goal.AIActionPlanVi = null; // Clear stale Vietnamese version to force re-translation
                    }
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
                var userId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
                var project = await _db.Set<Project>()
                    .Include(p => p.Tasks)
                    .FirstOrDefaultAsync(p => p.Id == projectId && (p.UserId == userId || p.Members.Any(pm => pm.UserId == userId)), cancellationToken);

                if (project == null)
                {
                    await Response.WriteAsync("data: {\"error\": \"Project not found\"}\n\n", cancellationToken);
                    return;
                }

                var lang = System.Threading.Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName;
                var isVi = lang.StartsWith("vi", StringComparison.OrdinalIgnoreCase);

                string context = isVi
                    ? "Bạn là một Chuyên gia Cố vấn Chiến lược và Huấn luyện viên Hiệu suất cao cấp (Senior Performance Coach)."
                    : "You are a Senior Strategy Advisor and Performance Coach.";

                string goalText = isVi
                    ? @"Nhiệm vụ: Phân tích dự án và lập kế hoạch thực hiện tối ưu.
Yêu cầu:
1. Đánh giá độ phức tạp của dự án (Dễ/Trung bình/Khó/Rất khó).
2. Phân tích SWOT ngắn gọn (Điểm mạnh/Yếu/Cơ hội/Thách thức).
3. Đề xuất lộ trình thực hiện với 3-5 giai đoạn cụ thể (milestones).
4. Gợi ý 5-8 tác vụ quan trọng cần làm, được sắp xếp theo thứ tự ưu tiên.
5. Đưa ra 3 lời khuyên thực tế để quản lý dự án hiệu quả.

BẮT BUỘC: Cuối bản kế hoạch, bạn PHẢI thêm một khối mã JSON để hệ thống có thể tạo các tác vụ:
```json-tasks
[
  { 
    ""key"": ""task_1"", 
    ""title"": ""Tên task ngắn gọn"", 
    ""description"": ""Mô tả chi tiết task này"",
    ""priority"": ""High"",
    ""estimatedDays"": 3,
    ""category"": ""Planning""
  },
  { 
    ""key"": ""task_2"", 
    ""title"": ""Tên task 2"", 
    ""description"": ""Mô tả task 2"",
    ""priority"": ""Medium"",
    ""estimatedDays"": 5,
    ""category"": ""Development""
  }
]
```
Ghi chú quan trọng: 
- Tạo 5-8 tasks cụ thể, thực tế và có thể thực hiện được
- Priority: Low, Medium, High, hoặc Urgent
- Category: Planning, Design, Development, Testing, Deployment, Documentation
- estimatedDays: Số ngày ước tính để hoàn thành (1-30)
- Key phải unique: task_1, task_2, task_3...
- KHÔNG thêm giải thích ngoài lề. Chỉ tập trung vào kế hoạch và JSON.

Định dạng: Sử dụng Markdown chuyên nghiệp, emoji phù hợp. Trả lời bằng tiếng Việt."
                    : @"Task: Analyze the project and create an optimal implementation plan.
Requirements:
1. Assess project complexity (Easy/Medium/Hard/Very Hard).
2. Brief SWOT analysis (Strengths/Weaknesses/Opportunities/Threats).
3. Propose a roadmap with 3-5 specific milestones.
4. Suggest 5-8 critical tasks, prioritized by importance.
5. Give 3 practical project management tips.

MANDATORY: At the end of the plan, add a JSON code block for task templates:
```json-tasks
[
  { 
    ""key"": ""task_1"", 
    ""title"": ""Short task name"", 
    ""description"": ""Detailed task description"",
    ""priority"": ""High"",
    ""estimatedDays"": 3,
    ""category"": ""Planning""
  },
  { 
    ""key"": ""task_2"", 
    ""title"": ""Task name 2"", 
    ""description"": ""Task description 2"",
    ""priority"": ""Medium"",
    ""estimatedDays"": 5,
    ""category"": ""Development""
  }
]
```
Important notes:
- Create 5-8 specific, realistic, actionable tasks
- Priority: Low, Medium, High, or Urgent
- Category: Planning, Design, Development, Testing, Deployment, Documentation
- estimatedDays: Estimated days to complete (1-30)
- Key must be unique: task_1, task_2, task_3...
- DO NOT add meta-comments. Focus only on the strategy and JSON.

Format: Use professional Markdown with appropriate emojis. Answer in English.";

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
                var hasError = false;
                
                await foreach (var chunk in _geminiService.StreamGenerateContent(prompt, 0.5, cancellationToken))
                {
                    // Check if chunk contains error message
                    if (chunk.StartsWith("Lỗi") || chunk.StartsWith("Error"))
                    {
                        hasError = true;
                        var errorJson = System.Text.Json.JsonSerializer.Serialize(new { error = chunk });
                        await Response.WriteAsync($"data: {errorJson}\n\n", cancellationToken);
                        await Response.Body.FlushAsync(cancellationToken);
                        break;
                    }
                    
                    fullResult.Append(chunk);
                    var escapedChunk = System.Text.Json.JsonSerializer.Serialize(chunk);
                    await Response.WriteAsync($"data: {escapedChunk}\n\n", cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                }

                // Save to DB after successful stream (only if no error)
                if (!hasError && fullResult.Length > 0)
                {
                    if (isVi)
                    {
                        project.AIStrategyVi = fullResult.ToString();
                        project.AIStrategyEn = null;
                    }
                    else
                    {
                        project.AIStrategyEn = fullResult.ToString();
                        project.AIStrategyVi = null;
                    }
                    
                    await _db.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("Successfully saved AI strategy for project {ProjectId}", projectId);
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
                var userId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
                var isVi = request.TargetLang == "vi";
                string sourceText = null;
                Project project = null;
                PersonalGoal goal = null;

                if (request.ProjectId.HasValue)
                {
                    project = await _db.Set<Project>().FirstOrDefaultAsync(p => p.Id == request.ProjectId && (p.UserId == userId || p.Members.Any(pm => pm.UserId == userId)));
                    if (project == null) return NotFound();
                    sourceText = isVi ? project.AIStrategyEn : project.AIStrategyVi;
                }
                else if (request.GoalId.HasValue)
                {
                    goal = await _db.Set<PersonalGoal>().FirstOrDefaultAsync(g => g.Id == request.GoalId && g.UserId == userId);
                    if (goal == null) return NotFound();
                    sourceText = isVi ? goal.AIActionPlanEn : goal.AIActionPlanVi;
                }
                else
                {
                    return BadRequest("Must provide ProjectId or GoalId.");
                }

                if (string.IsNullOrEmpty(sourceText)) return BadRequest("No source strategy to translate.");

                string context = "Bạn là một chuyên gia dịch thuật chuyên nghiệp, chuyên ngành Quản trị Dự án và Mục tiêu cá nhân.";
                string promptGoal = isVi 
                    ? "Dịch bản kế hoạch chiến lược sau đây sang tiếng Việt. Giữ nguyên định dạng Markdown và các emoji. ĐẶC BIỆT: Trong khối mã json-tasks (nếu có), PHẢI giữ nguyên giá trị trường \"key\", chỉ dịch \"title\" và \"description\". KHÔNG thêm bất kỳ lời giải thích hay lưu ý nào về việc thiếu dữ liệu hoặc không thể thực hiện quy tắc."
                    : "Translate the following strategy plan into English. Maintain Markdown formatting and emojis. SPECIAL: In the json-tasks block (if any), MUST keep \"key\" values unchanged, only translate \"title\" and \"description\". DO NOT add any explanations or notes about missing data or inability to follow rules.";
                
                string prompt = _geminiService.BuildAdvancedPrompt(context, promptGoal, sourceText);
                string translatedText = await _geminiService.GenerateContent(prompt, 0.2, CancellationToken.None);

                if (project != null)
                {
                    if (isVi) project.AIStrategyVi = translatedText;
                    else project.AIStrategyEn = translatedText;
                }
                else if (goal != null)
                {
                    if (isVi) goal.AIActionPlanVi = translatedText;
                    else goal.AIActionPlanEn = translatedText;
                    goal.UpdatedAt = DateTime.UtcNow;
                }

                await _db.SaveChangesAsync();
                return Ok(new { result = translatedText });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error translating strategy");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("extract-tasks")]
        public IActionResult ExtractTasksFromStrategy([FromBody] ExtractTasksRequestDTO request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.StrategyText))
                {
                    return BadRequest(new { error = "Strategy text is required" });
                }

                var tasks = _aiTaskService.ExtractTasksFromMarkdown(request.StrategyText);
                
                return Ok(new { 
                    success = true, 
                    tasks = tasks,
                    count = tasks.Count 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting tasks from strategy");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        private List<SuggestedTaskDTO> ExtractTasksFromMarkdown(string markdown)
        {
            return _aiTaskService.ExtractTasksFromMarkdown(markdown);
        }

        [HttpPost("import-tasks")]
        public async Task<IActionResult> ImportSuggestedTasks([FromBody] ImportTasksRequestDTO request)
        {
            try
            {
                var userId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
                
                if (request.Tasks == null || !request.Tasks.Any())
                {
                    return BadRequest(new { error = "No tasks provided" });
                }

                var (success, count, message) = await _aiTaskService.ImportTasksToProject(
                    request.ProjectId, 
                    userId, 
                    request.Tasks);

                if (!success)
                {
                    return BadRequest(new { error = message });
                }

                return Ok(new { 
                    success = true, 
                    count = count,
                    message = message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing tasks for project {ProjectId}", request.ProjectId);
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }

    public class TranslateRequestDTO
    {
        public int? ProjectId { get; set; }
        public int? GoalId { get; set; }
        public string TargetLang { get; set; }
    }

    public class ExtractTasksRequestDTO
    {
        public string StrategyText { get; set; }
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
        public string Priority { get; set; } = "Medium";
        public int EstimatedDays { get; set; } = 7;
        public string Category { get; set; } = "General";
    }
}