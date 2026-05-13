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
    }
}