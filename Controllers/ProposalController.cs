using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESK.WIL.Models;

namespace RESK.WIL.Controllers
{
    [Authorize(Roles = "Producer")]
    public class ProposalController : Controller
    {
        private const string ProducerDetailsKey =
            "Proposal.ProducerDetails";


        // =========================================================
        // STEP 1 - PRODUCER DETAILS
        // =========================================================

        [HttpGet]
        public IActionResult ProducerDetails()
        {
            string? savedJson =
                HttpContext.Session.GetString(
                    ProducerDetailsKey
                );

            if (!string.IsNullOrWhiteSpace(savedJson))
            {
                try
                {
                    ProposalProducerDetailsViewModel? savedModel =
                        JsonSerializer.Deserialize
                        <ProposalProducerDetailsViewModel>(
                            savedJson
                        );

                    if (savedModel != null)
                    {
                        return View(savedModel);
                    }
                }
                catch (JsonException)
                {
                    HttpContext.Session.Remove(
                        ProducerDetailsKey
                    );
                }
            }

            return View(
                new ProposalProducerDetailsViewModel()
            );
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ProducerDetails(
            ProposalProducerDetailsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            HttpContext.Session.SetString(
                ProducerDetailsKey,
                JsonSerializer.Serialize(model)
            );

            return RedirectToAction(
                nameof(ProgrammeDetails)
            );
        }


        // =========================================================
        // STEP 2 - PROGRAMME DETAILS
        // =========================================================

        [HttpGet]
        public IActionResult ProgrammeDetails()
        {
            if (!HasProducerDetails())
            {
                return RedirectToAction(
                    nameof(ProducerDetails)
                );
            }

            return View();
        }


        // =========================================================
        // STEP 3 - PRODUCTION DETAILS
        // =========================================================

        [HttpGet]
        public IActionResult ProductionDetails()
        {
            return View();
        }


        // =========================================================
        // STEP 4 - ATTACHMENTS
        // =========================================================

        [HttpGet]
        public IActionResult Attachments()
        {
            return View();
        }


        // =========================================================
        // STEP 5 - REVIEW
        // =========================================================

        [HttpGet]
        public IActionResult Review()
        {
            return View();
        }


        // =========================================================
        // SUBMITTED
        // =========================================================

        [HttpGet]
        public IActionResult Submitted()
        {
            return View();
        }


        // =========================================================
        // HELPERS
        // =========================================================

        private bool HasProducerDetails()
        {
            return !string.IsNullOrWhiteSpace(
                HttpContext.Session.GetString(
                    ProducerDetailsKey
                )
            );
        }
    }
}
