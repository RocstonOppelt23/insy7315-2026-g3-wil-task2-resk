using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESK.WIL.Models;

namespace RESK.WIL.Controllers
{
    [Authorize(Roles = "Producer")]
    public class ProducerController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Proposal proposal)
        {
            if (!ModelState.IsValid)
            {
                return View(proposal);
            }

            TempData["Success"] =
                "Proposal submitted successfully.";

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public IActionResult Details()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Edit()
        {
            return View();
        }

     

      
    }
}