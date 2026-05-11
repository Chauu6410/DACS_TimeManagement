using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DACS_TimeManagement.Services.Interfaces;
using DACS_TimeManagement.Services;
using DACS_TimeManagement.Models;
using TaskStatusModel = DACS_TimeManagement.Models.TaskStatus;

namespace DACS_TimeManagement.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/schedule")]
    public class AutoPlanController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IGeminiService _geminiService;
        private readonly IConfiguration _config;
        private readonly ILogger<AutoPlanController> _logger;

        public AutoPlanController(ApplicationDbContext db, IGeminiService geminiService, IConfiguration config, ILogger<AutoPlanController> logger)
        {
            _db = db;
            _geminiService = geminiService;
            _config = config;
            _logger = logger;
        }

        public class ScheduleItemDto
        {
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public string TaskName { get; set; } = string.Empty;
            public string Note { get; set; } = string.Empty;
        }

        // POST api/schedule/autoplan
        [HttpPost("autoplan")]
        public async Task<IActionResult> GenerateAutoPlan(CancellationToken cancellationToken)
        {
            var userId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            // 1) Query uncompleted tasks
            var tasks = await _db.WorkTasks
                .AsNoTracking()
                .Where(t => t.AssigneeId == userId && t.Status != TaskStatusModel.Completed)
                .OrderByDescending(t => t.Priority)
                .ThenBy(t => t.StartDate)
                .ToListAsync(cancellationToken);

            // Prepare a concise tasks payload for the AI
            var taskPayload = tasks.Select(t => new {
                id = t.Id,
                title = t.Title,
                description = t.Description ?? string.Empty,
                start = t.StartDate.ToString("o"),
                end = t.EndDate.ToString("o"),
                estMinutes = (int)Math.Max(15, (t.EndDate - t.StartDate).TotalMinutes)
            }).ToList();


            // 2) Build localized system/user instruction and prompt using the same approach as GoalService
            var profile = await _db.Set<UserProfile>().AsNoTracking().FirstOrDefaultAsync(up => up.UserId == userId);
            var lang = profile?.Language ?? System.Threading.Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName ?? "en";
            var isVi = lang.StartsWith("vi", StringComparison.OrdinalIgnoreCase);

            string context = isVi
                ? "Bạn là một chuyên gia quản lý thời gian, chuyên tối ưu hóa lịch trình dựa trên năng lượng người dùng."
                : "You are an expert time-management assistant who optimizes schedules based on user energy and priorities.";

            string goalText = isVi
                ? "Tạo kế hoạch Auto-Plan từ danh sách task đã cho. Ràng buộc: chèn nghỉ 5 phút sau mỗi 25 phút làm việc (Pomodoro). Không thêm task mới. Trả về JSON ngắn gọn (mảng đối tượng) với StartTime (ISO), EndTime (ISO), TaskName, Note (lý do)."
                : "Create an Auto-Plan from the provided list of tasks. Constraint: insert 5-minute short breaks after each 25 minutes of focused work (Pomodoro). Do not add new tasks. Return concise JSON array with StartTime (ISO), EndTime (ISO), TaskName, Note (reason).";

            var userInputText = JsonSerializer.Serialize(new { tasks = taskPayload, timezone = System.TimeZoneInfo.Local.Id });
            var prompt = _geminiService.BuildAdvancedPrompt(context, goalText, userInputText);

            // 3) Call Gemini with timeout and handle errors like GoalService
            int timeoutSeconds = _config.GetValue<int>("Gemini:TimeoutSeconds", 60);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            string aiRaw;
            try
            {
                // Use lower temperature for schedule generation to favor deterministic plans
                aiRaw = await _geminiService.GenerateContent(prompt, 0.2, cts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("AutoPlan: AI request timed out for user {UserId}", userId);
                var msg = isVi ? "Yêu cầu AI quá thời gian chờ" : "AI request timed out";
                return StatusCode(504, new { error = msg });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AutoPlan: AI call failed for user {UserId}", userId);
                var msg = isVi ? "Tạo plan AI thất bại" : "Failed to generate content";
                return StatusCode(502, new { error = msg, detail = ex.Message });
            }

            // Post-process: remove greetings if any, but do NOT truncate lines since it's JSON
            if (string.IsNullOrWhiteSpace(aiRaw)) aiRaw = isVi ? "AI không trả về nội dung." : "AI returned empty content.";
            try
            {
                var lines = aiRaw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).ToList();
                while (lines.Count > 0 && (lines[0].StartsWith("Chào", StringComparison.OrdinalIgnoreCase)
                                           || lines[0].StartsWith("Xin chào", StringComparison.OrdinalIgnoreCase)
                                           || lines[0].StartsWith("Hello", StringComparison.OrdinalIgnoreCase)
                                           || lines[0].StartsWith("Hi", StringComparison.OrdinalIgnoreCase)
                                           || lines[0].StartsWith("Dear", StringComparison.OrdinalIgnoreCase)
                                           || lines[0].StartsWith("Bạn", StringComparison.OrdinalIgnoreCase)
                                           || lines[0].StartsWith("Dưới đây", StringComparison.OrdinalIgnoreCase)
                                           || lines[0].StartsWith("Here is", StringComparison.OrdinalIgnoreCase)))
                {
                    lines.RemoveAt(0);
                }
                if (lines.Count > 0 && lines[0].Length < 120 && lines[0].EndsWith(".", StringComparison.Ordinal)) lines.RemoveAt(0);
                aiRaw = string.Join("\n", lines);
            }
            catch { }

            // 4) Extract JSON array from aiRaw (in case AI returns markdown or text)
            var json = ExtractJsonArray(aiRaw);
            if (json == null)
            {
                _logger.LogWarning("AutoPlan: AI returned non-JSON response: {Resp}", aiRaw);
                return StatusCode(502, new { error = "AI returned unexpected format", raw = aiRaw });
            }

            try
            {
                var doc = JsonDocument.Parse(json);
                var list = new List<ScheduleItemDto>();
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        var start = el.GetProperty("StartTime").GetDateTime();
                        var end = el.GetProperty("EndTime").GetDateTime();
                        var name = el.GetProperty("TaskName").GetString() ?? "";
                        var note = el.TryGetProperty("Note", out var n) ? n.GetString() ?? "" : "";

                        list.Add(new ScheduleItemDto { StartTime = start, EndTime = end, TaskName = name, Note = note });
                    }
                }

                return Ok(list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AutoPlan: Failed to parse AI JSON");
                return StatusCode(502, new { error = "Failed to parse AI response as JSON", raw = aiRaw });
            }
        }

        // Helper: tries to extract first JSON array from text
        private static string? ExtractJsonArray(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            // Quick path: trimmed starts with [
            var trimmed = text.Trim();
            if (trimmed.StartsWith("["))
            {
                // Try to find matching closing bracket - naive but effective for most AI outputs
                int depth = 0;
                for (int i = 0; i < trimmed.Length; i++)
                {
                    if (trimmed[i] == '[') depth++;
                    else if (trimmed[i] == ']')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            return trimmed.Substring(0, i + 1);
                        }
                    }
                }
            }

            // Fallback: regex extract between first [ and last ]
            var m = Regex.Match(text, "\\[.*\\]", RegexOptions.Singleline);
            if (m.Success) return m.Value;
            return null;
        }
    }
}
