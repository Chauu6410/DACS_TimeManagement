using DACS_TimeManagement.DTOs;
using DACS_TimeManagement.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DACS_TimeManagement.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class GoalsController : ControllerBase
    {
        private readonly IGoalService _goalService;

        public GoalsController(IGoalService goalService)
        {
            _goalService = goalService;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateGoalDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var result = await _goalService.CreateAsync(dto, userId);
            return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateGoalDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var updated = await _goalService.UpdateAsync(dto, userId);
            if (updated == null) return NotFound();
            return Ok(updated);
        }

        [HttpPost("{id}/tasks")]
        public async Task<IActionResult> LinkTasks(int id, [FromBody] LinkTasksDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var ok = await _goalService.LinkTasksAsync(id, dto.TaskIds, userId);
            if (!ok) return NotFound();
            return NoContent();
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            // For demo keep simple: return placeholder
            return Ok(new { message = "Use MVC controllers for rich UI; this API supports integrations." });
        }

        [HttpPost("update-progress")]
        public async Task<IActionResult> UpdateProgress([FromBody] int goalId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            await _goalService.RecalculateProgressForGoalAsync(goalId, userId);
            return Ok();
        }
    }
}
