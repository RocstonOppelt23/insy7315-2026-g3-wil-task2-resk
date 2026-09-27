using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESK.WIL.Models;

namespace RESK.WIL.Controllers
{
    [Authorize]
    public class ProposalController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        [Authorize(Roles = "Producer")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Producer")]
        public IActionResult Create(Proposal proposal)
        {
            if (!ModelState.IsValid)
            {
                return View(proposal);
            }

            proposal.Title =
            SecurityHelper.Sanitize(proposal.Title);

            proposal.Description =
            SecurityHelper.Sanitize(proposal.Description);

            proposal.Category =
            SecurityHelper.Sanitize(proposal.Category);

            TempData["Success"] =
            "Proposal submitted successfully.";

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Reviewer")]
        public IActionResult Review()
        {
            return View();
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Dashboard()
        {
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Reviewer")]
        [ValidateAntiForgeryToken]
        public IActionResult Approve(int id)
        {
            TempData["Success"] =
            "Proposal approved successfully.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = "Reviewer")]
        [ValidateAntiForgeryToken]
        public IActionResult Reject(int id)
        {
            TempData["Success"] =
            "Proposal rejected successfully.";

            return RedirectToAction(nameof(Index));
        }
    }
}
