using DACS_TimeManagement.Services.Interfaces;
using DACS_TimeManagement.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DACS_TimeManagement.Controllers
{
    [Authorize]
    public class WorkScheduleController : Controller
    {
        private readonly IUserWorkScheduleService _scheduleService;

        public WorkScheduleController(IUserWorkScheduleService scheduleService)
        {
            _scheduleService = scheduleService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var schedule = await _scheduleService.GetOrCreateDefaultAsync(userId);

            var viewModel = new WorkScheduleViewModel
            {
                DefaultStartHour = schedule.DefaultStartHour.ToString("HH:mm"),
                DefaultEndHour = schedule.DefaultEndHour.ToString("HH:mm"),
                LunchStart = schedule.LunchStart.ToString("HH:mm"),
                LunchEnd = schedule.LunchEnd.ToString("HH:mm"),
                SelectedWorkingDays = schedule.WorkingDays.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(WorkScheduleViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            if (ModelState.IsValid)
            {
                var schedule = await _scheduleService.GetOrCreateDefaultAsync(userId);

                if (TimeOnly.TryParse(model.DefaultStartHour, out var startHour)) schedule.DefaultStartHour = startHour;
                if (TimeOnly.TryParse(model.DefaultEndHour, out var endHour)) schedule.DefaultEndHour = endHour;
                if (TimeOnly.TryParse(model.LunchStart, out var lunchStart)) schedule.LunchStart = lunchStart;
                if (TimeOnly.TryParse(model.LunchEnd, out var lunchEnd)) schedule.LunchEnd = lunchEnd;
                
                schedule.WorkingDays = string.Join(",", model.SelectedWorkingDays);

                await _scheduleService.UpdateAsync(schedule);

                TempData["SuccessMessage"] = "Cập nhật cấu hình lịch làm việc thành công!";
                return RedirectToAction(nameof(Index));
            }
            // If we got this far something failed; redisplay the form with validation messages
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> GetMySchedule()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();
            var s = await _scheduleService.GetOrCreateDefaultAsync(userId);
            return Json(new { 
                success = true, 
                data = new {
                    DefaultStartHour = s.DefaultStartHour.ToString("HH:mm"),
                    DefaultEndHour = s.DefaultEndHour.ToString("HH:mm"),
                    LunchStart = s.LunchStart.ToString("HH:mm"),
                    LunchEnd = s.LunchEnd.ToString("HH:mm"), 
                    WorkingDays = s.WorkingDays.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateMySchedule([FromBody] WorkScheduleViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            if (ModelState.IsValid)
            {
                var s = await _scheduleService.GetOrCreateDefaultAsync(userId);
                
                if (TimeOnly.TryParse(model.DefaultStartHour, out var startHour)) s.DefaultStartHour = startHour;
                if (TimeOnly.TryParse(model.DefaultEndHour, out var endHour)) s.DefaultEndHour = endHour;
                if (TimeOnly.TryParse(model.LunchStart, out var lunchStart)) s.LunchStart = lunchStart;
                if (TimeOnly.TryParse(model.LunchEnd, out var lunchEnd)) s.LunchEnd = lunchEnd;
                
                s.WorkingDays = string.Join(",", model.SelectedWorkingDays ?? new List<string>());
                await _scheduleService.UpdateAsync(s);
                return Json(new { success = true });
            }
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return Json(new { success = false, message = "Dữ liệu không hợp lệ: " + string.Join("; ", errors) });
        }
    }
}
