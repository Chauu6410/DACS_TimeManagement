using System;
using System.Threading.Tasks;
using DACS_TimeManagement.DTOs;
using DACS_TimeManagement.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DACS_TimeManagement.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AIController : ControllerBase
    {
        private readonly IGeminiService _geminiService;

        public AIController(IGeminiService geminiService)
        {
            _geminiService = geminiService;
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
                string context = "Bạn là một cố vấn chiến lược và chuyên gia hiệu suất.";
                string goalText = "Dựa trên dữ liệu dưới đây, hãy: 1. Phân tích mức độ khó của mục tiêu (Dễ/Trung bình/Khó). 2. Đưa ra chiến lược hành động chi tiết để hoàn thành đúng hạn. Trả lời bằng tiếng Việt, sử dụng Markdown nhẹ nhàng và phong cách quyết đoán.";
                
                string progressInfo = request.Goal.Type == "TaskBased" 
                    ? $"Tiến độ: {request.Goal.CompletedTasks}/{request.Goal.TargetTasks} task." 
                    : $"Tiến độ: {request.Goal.CompletedHours:F1}/{request.Goal.TargetHours:F1} giờ.";

                string userInput = $@"
Dữ liệu Mục tiêu:
- Tiêu đề: {request.Goal.Title}
- Mô tả: {request.Goal.Description}
- Trạng thái: {request.Goal.Status}
- {progressInfo}
- Hạn chót: {request.Goal.TargetDate:dd/MM/yyyy}

Dự án: {request.Project?.Name ?? "N/A"}
Chi tiết: {request.Project?.Detail ?? "N/A"}
";


                string prompt = _geminiService.BuildAdvancedPrompt(context, goalText, userInput);

                string aiResult = await _geminiService.GenerateContent(prompt);

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
    }
}