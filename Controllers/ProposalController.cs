using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RESK.WIL.Controllers
{
    [Authorize(Roles = "Producer")]
    public class ProposalController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            return RedirectToAction(nameof(ProducerDetails));
        }

        [HttpGet]
        public IActionResult ProducerDetails()
        {
            return View();
        }

        [HttpGet]
        public IActionResult ProgrammeDetails()
        {
            return View();
        }

        [HttpGet]
        public IActionResult ProductionDetails()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Attachments()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Review()
        {
            return View();
        }

        [HttpGet]
        public IActionResult MyProposals()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Status(int id)
        {
            ViewBag.ProposalId = id;
            return View();
        }

        [HttpGet]
        public IActionResult RequestedChanges(int id)
        {
            ViewBag.ProposalId = id;
            return View();
        }
    }
}
