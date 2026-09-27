using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RESK.WIL.Controllers
{
    [Authorize(Roles = "Producer")]
    public class ProducerController : Controller
    {
        // =========================================================
        // PRODUCER DASHBOARD
        // =========================================================

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }
    }
}