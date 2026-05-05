using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DACS_TimeManagement.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        public IActionResult Index() => RedirectToAction("Profile", "Account");
        public IActionResult Update() => RedirectToAction("Profile", "Account");
    }
}
