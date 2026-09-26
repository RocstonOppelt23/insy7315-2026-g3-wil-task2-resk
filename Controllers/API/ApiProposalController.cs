using Microsoft.AspNetCore.Mvc;

namespace RESK.WIL.Controllers
{
    public class ApiProposalController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
