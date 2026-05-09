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
                DefaultStartHour = schedule.DefaultStartHour,
                DefaultEndHour = schedule.DefaultEndHour,
                LunchStart = schedule.LunchStart,
                LunchEnd = schedule.LunchEnd,
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

                schedule.DefaultStartHour = model.DefaultStartHour;
                schedule.DefaultEndHour = model.DefaultEndHour;
                schedule.LunchStart = model.LunchStart;
                schedule.LunchEnd = model.LunchEnd;
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
                    s.DefaultStartHour, s.DefaultEndHour, s.LunchStart, s.LunchEnd, 
                    WorkingDays = s.WorkingDays.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
                }
            });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateMySchedule([FromBody] WorkScheduleViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            if (ModelState.IsValid)
            {
                var s = await _scheduleService.GetOrCreateDefaultAsync(userId);
                s.DefaultStartHour = model.DefaultStartHour;
                s.DefaultEndHour = model.DefaultEndHour;
                s.LunchStart = model.LunchStart;
                s.LunchEnd = model.LunchEnd;
                s.WorkingDays = string.Join(",", model.SelectedWorkingDays);
                await _scheduleService.UpdateAsync(s);
                return Json(new { success = true });
            }
            return Json(new { success = false, message = "Dữ liệu không hợp lệ" });
        }
    }
}
