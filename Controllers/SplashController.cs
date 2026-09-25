using Microsoft.AspNetCore.Mvc;

namespace RESK.WIL.Controllers
{
    public class SplashController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}