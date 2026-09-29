using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RESK.WIL.Models;

namespace RESK.WIL.Controllers
{
    [Authorize(Roles = "Producer")]
    public class ProducerController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;

        public ProducerController(
            UserManager<IdentityUser> userManager)
        {
            _userManager = userManager;
        }


        // =========================================================
        // PRODUCER DASHBOARD
        // =========================================================

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }


        // =========================================================
        // GUIDELINES
        // =========================================================

        [HttpGet]
        public IActionResult Guidelines()
        {
            return View();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AcceptGuidelines(
            bool agreeToGuidelines)
        {
            if (!agreeToGuidelines)
            {
                TempData["GuidelinesError"] =
                    "You must read and agree to the proposal guidelines before starting a proposal.";

                return RedirectToAction(nameof(Guidelines));
            }

            HttpContext.Session.SetString(
                "ProposalGuidelinesAccepted",
                "true"
            );

            return RedirectToAction(nameof(Create));
        }


        // =========================================================
        // STEP 1 - PRODUCER DETAILS
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            if (!HasAcceptedGuidelines())
            {
                return RedirectToAction(nameof(Guidelines));
            }

            string? savedJson =
                HttpContext.Session.GetString(
                    "ProducerDetails"
                );

            if (!string.IsNullOrWhiteSpace(savedJson))
            {
                try
                {
                    ProducerDetailsViewModel? savedModel =
                        JsonSerializer.Deserialize
                        <ProducerDetailsViewModel>(
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
                        "ProducerDetails"
                    );
                }
            }

            IdentityUser? currentUser =
                await _userManager.GetUserAsync(User);

            var model =
                new ProducerDetailsViewModel
                {
                    Email =
                        currentUser?.Email
                        ?? currentUser?.UserName
                        ?? string.Empty,

                    PreferredContactMethod =
                        "Email"
                };

            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(
            ProducerDetailsViewModel model)
        {
            if (!HasAcceptedGuidelines())
            {
                return RedirectToAction(nameof(Guidelines));
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            HttpContext.Session.SetString(
                "ProducerDetails",
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
            if (!HasAcceptedGuidelines())
            {
                return RedirectToAction(nameof(Guidelines));
            }

            if (!HasSessionValue("ProducerDetails"))
            {
                return RedirectToAction(nameof(Create));
            }

            string? savedJson =
                HttpContext.Session.GetString(
                    "ProgrammeDetails"
                );

            if (!string.IsNullOrWhiteSpace(savedJson))
            {
                try
                {
                    ProgrammeDetailsViewModel? savedModel =
                        JsonSerializer.Deserialize
                        <ProgrammeDetailsViewModel>(
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
                        "ProgrammeDetails"
                    );
                }
            }

            return View(
                new ProgrammeDetailsViewModel()
            );
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ProgrammeDetails(
            ProgrammeDetailsViewModel model)
        {
            if (!HasAcceptedGuidelines())
            {
                return RedirectToAction(nameof(Guidelines));
            }

            if (!HasSessionValue("ProducerDetails"))
            {
                return RedirectToAction(nameof(Create));
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            HttpContext.Session.SetString(
                "ProgrammeDetails",
                JsonSerializer.Serialize(model)
            );

            return RedirectToAction(
                nameof(ProductionDetails)
            );
        }


        // =========================================================
        // STEP 3 - PRODUCTION DETAILS
        // =========================================================

        [HttpGet]
        public IActionResult ProductionDetails()
        {
            if (!HasAcceptedGuidelines())
            {
                return RedirectToAction(nameof(Guidelines));
            }

            if (!HasSessionValue("ProducerDetails"))
            {
                return RedirectToAction(nameof(Create));
            }

            if (!HasSessionValue("ProgrammeDetails"))
            {
                return RedirectToAction(
                    nameof(ProgrammeDetails)
                );
            }

            string? savedJson =
                HttpContext.Session.GetString(
                    "ProductionDetails"
                );

            if (!string.IsNullOrWhiteSpace(savedJson))
            {
                try
                {
                    ProductionDetailsViewModel? savedModel =
                        JsonSerializer.Deserialize
                        <ProductionDetailsViewModel>(
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
                        "ProductionDetails"
                    );
                }
            }

            return View(
                new ProductionDetailsViewModel()
            );
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ProductionDetails(
            ProductionDetailsViewModel model)
        {
            if (!HasAcceptedGuidelines())
            {
                return RedirectToAction(nameof(Guidelines));
            }

            if (!HasSessionValue("ProducerDetails"))
            {
                return RedirectToAction(nameof(Create));
            }

            if (!HasSessionValue("ProgrammeDetails"))
            {
                return RedirectToAction(
                    nameof(ProgrammeDetails)
                );
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            HttpContext.Session.SetString(
                "ProductionDetails",
                JsonSerializer.Serialize(model)
            );

            return RedirectToAction(
                nameof(Attachments)
            );
        }


        // =========================================================
        // STEP 4 - ATTACHMENTS GET
        // =========================================================

        [HttpGet]
        public IActionResult Attachments()
        {
            if (!HasAcceptedGuidelines())
            {
                return RedirectToAction(nameof(Guidelines));
            }

            if (!HasSessionValue("ProducerDetails"))
            {
                return RedirectToAction(nameof(Create));
            }

            if (!HasSessionValue("ProgrammeDetails"))
            {
                return RedirectToAction(
                    nameof(ProgrammeDetails)
                );
            }

            if (!HasSessionValue("ProductionDetails"))
            {
                return RedirectToAction(
                    nameof(ProductionDetails)
                );
            }

            var model =
                new AttachmentsViewModel
                {
                    ExistingProposalDocument =
                        HttpContext.Session.GetString(
                            "ProposalDocumentName"
                        ),

                    ExistingBudgetDocument =
                        HttpContext.Session.GetString(
                            "BudgetDocumentName"
                        ),

                    ExistingAdditionalFile =
                        HttpContext.Session.GetString(
                            "AdditionalFileName"
                        ),

                    PilotShowreelLink =
                        HttpContext.Session.GetString(
                            "PilotShowreelLink"
                        )
                };

            return View(model);
        }


        // =========================================================
        // STEP 4 - ATTACHMENTS POST
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(60_000_000)]
        public IActionResult Attachments(
            AttachmentsViewModel model)
        {
            if (!HasAcceptedGuidelines())
            {
                return RedirectToAction(nameof(Guidelines));
            }

            if (!HasSessionValue("ProducerDetails"))
            {
                return RedirectToAction(nameof(Create));
            }

            if (!HasSessionValue("ProgrammeDetails"))
            {
                return RedirectToAction(
                    nameof(ProgrammeDetails)
                );
            }

            if (!HasSessionValue("ProductionDetails"))
            {
                return RedirectToAction(
                    nameof(ProductionDetails)
                );
            }

            string? existingProposal =
                HttpContext.Session.GetString(
                    "ProposalDocumentName"
                );


            // Proposal PDF required
            if (model.ProposalDocument == null &&
                string.IsNullOrWhiteSpace(
                    existingProposal))
            {
                ModelState.AddModelError(
                    nameof(model.ProposalDocument),
                    "Please upload a proposal PDF."
                );
            }


            ValidateUpload(
                model.ProposalDocument,
                nameof(model.ProposalDocument),
                new[]
                {
                    ".pdf"
                }
            );


            ValidateUpload(
                model.BudgetDocument,
                nameof(model.BudgetDocument),
                new[]
                {
                    ".pdf",
                    ".doc",
                    ".docx",
                    ".xls",
                    ".xlsx"
                }
            );


            ValidateUpload(
                model.AdditionalFile,
                nameof(model.AdditionalFile),
                new[]
                {
                    ".pdf",
                    ".doc",
                    ".docx",
                    ".xls",
                    ".xlsx",
                    ".jpg",
                    ".jpeg",
                    ".png"
                }
            );


            if (!ModelState.IsValid)
            {
                model.ExistingProposalDocument =
                    existingProposal;

                model.ExistingBudgetDocument =
                    HttpContext.Session.GetString(
                        "BudgetDocumentName"
                    );

                model.ExistingAdditionalFile =
                    HttpContext.Session.GetString(
                        "AdditionalFileName"
                    );

                return View(model);
            }


            // Store safe original names in Session.
            if (model.ProposalDocument != null)
            {
                HttpContext.Session.SetString(
                    "ProposalDocumentName",
                    Path.GetFileName(
                        model.ProposalDocument.FileName
                    )
                );
            }


            if (model.BudgetDocument != null)
            {
                HttpContext.Session.SetString(
                    "BudgetDocumentName",
                    Path.GetFileName(
                        model.BudgetDocument.FileName
                    )
                );
            }


            if (model.AdditionalFile != null)
            {
                HttpContext.Session.SetString(
                    "AdditionalFileName",
                    Path.GetFileName(
                        model.AdditionalFile.FileName
                    )
                );
            }


            if (!string.IsNullOrWhiteSpace(
                model.PilotShowreelLink))
            {
                HttpContext.Session.SetString(
                    "PilotShowreelLink",
                    model.PilotShowreelLink
                );
            }
            else
            {
                HttpContext.Session.Remove(
                    "PilotShowreelLink"
                );
            }


            // Go directly to Review & Submit.
            return RedirectToAction(
                nameof(Review)
            );
        }


        // =========================================================
        // STEP 5 - REVIEW & SUBMIT GET
        // =========================================================

        [HttpGet]
        public IActionResult Review()
        {
            if (!HasAcceptedGuidelines())
            {
                return RedirectToAction(nameof(Guidelines));
            }

            string? producerJson =
                HttpContext.Session.GetString(
                    "ProducerDetails"
                );

            string? programmeJson =
                HttpContext.Session.GetString(
                    "ProgrammeDetails"
                );

            string? productionJson =
                HttpContext.Session.GetString(
                    "ProductionDetails"
                );


            if (string.IsNullOrWhiteSpace(
                producerJson))
            {
                return RedirectToAction(nameof(Create));
            }


            if (string.IsNullOrWhiteSpace(
                programmeJson))
            {
                return RedirectToAction(
                    nameof(ProgrammeDetails)
                );
            }


            if (string.IsNullOrWhiteSpace(
                productionJson))
            {
                return RedirectToAction(
                    nameof(ProductionDetails)
                );
            }


            if (!HasSessionValue(
                "ProposalDocumentName"))
            {
                return RedirectToAction(
                    nameof(Attachments)
                );
            }


            try
            {
                ProducerDetailsViewModel? producer =
                    JsonSerializer.Deserialize
                    <ProducerDetailsViewModel>(
                        producerJson
                    );

                ProgrammeDetailsViewModel? programme =
                    JsonSerializer.Deserialize
                    <ProgrammeDetailsViewModel>(
                        programmeJson
                    );

                ProductionDetailsViewModel? production =
                    JsonSerializer.Deserialize
                    <ProductionDetailsViewModel>(
                        productionJson
                    );


                if (producer == null)
                {
                    return RedirectToAction(
                        nameof(Create)
                    );
                }


                if (programme == null)
                {
                    return RedirectToAction(
                        nameof(ProgrammeDetails)
                    );
                }


                if (production == null)
                {
                    return RedirectToAction(
                        nameof(ProductionDetails)
                    );
                }


                var model =
                    new ReviewProposalViewModel
                    {
                        ProducerDetails =
                            producer,

                        ProgrammeDetails =
                            programme,

                        ProductionDetails =
                            production,

                        ProposalDocumentName =
                            HttpContext.Session.GetString(
                                "ProposalDocumentName"
                            ),

                        BudgetDocumentName =
                            HttpContext.Session.GetString(
                                "BudgetDocumentName"
                            ),

                        AdditionalFileName =
                            HttpContext.Session.GetString(
                                "AdditionalFileName"
                            ),

                        PilotShowreelLink =
                            HttpContext.Session.GetString(
                                "PilotShowreelLink"
                            )
                    };


                return View(model);
            }
            catch (JsonException)
            {
                ClearProposalWizard();

                TempData["GuidelinesError"] =
                    "Your proposal session could not be restored. Please start again.";

                return RedirectToAction(
                    nameof(Guidelines)
                );
            }
        }


        // =========================================================
        // STEP 5 - SUBMIT PROPOSAL
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SubmitProposal(
            ReviewProposalViewModel model)
        {
            if (!HasAcceptedGuidelines())
            {
                return RedirectToAction(nameof(Guidelines));
            }


            if (!model.ConfirmSubmission)
            {
                TempData["SubmitError"] =
                    "Please confirm the declaration before submitting.";

                return RedirectToAction(
                    nameof(Review)
                );
            }


            if (!HasSessionValue(
                "ProducerDetails"))
            {
                return RedirectToAction(nameof(Create));
            }


            if (!HasSessionValue(
                "ProgrammeDetails"))
            {
                return RedirectToAction(
                    nameof(ProgrammeDetails)
                );
            }


            if (!HasSessionValue(
                "ProductionDetails"))
            {
                return RedirectToAction(
                    nameof(ProductionDetails)
                );
            }


            if (!HasSessionValue(
                "ProposalDocumentName"))
            {
                return RedirectToAction(
                    nameof(Attachments)
                );
            }


            string reference =
                $"CTV-{DateTime.Now.Year}-" +
                $"{Random.Shared.Next(1000, 9999)}";


            TempData["SubmittedReference"] =
                reference;

            TempData["SubmittedDate"] =
                DateTime.Now.ToString(
                    "dd MMMM yyyy 'at' HH:mm"
                );


            ClearProposalWizard();


            return RedirectToAction(
                nameof(Submitted)
            );
        }


        // =========================================================
        // SUBMITTED
        // =========================================================

        [HttpGet]
        public IActionResult Submitted()
        {
            if (TempData[
                "SubmittedReference"] == null)
            {
                return RedirectToAction(
                    nameof(Index)
                );
            }


            TempData.Keep(
                "SubmittedReference"
            );

            TempData.Keep(
                "SubmittedDate"
            );


            return View();
        }


        // =========================================================
        // EXISTING PLACEHOLDER PAGES
        // =========================================================

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


        // =========================================================
        // HELPERS
        // =========================================================

        private bool HasAcceptedGuidelines()
        {
            return HttpContext.Session.GetString(
                "ProposalGuidelinesAccepted"
            ) == "true";
        }


        private bool HasSessionValue(
            string key)
        {
            return !string.IsNullOrWhiteSpace(
                HttpContext.Session.GetString(
                    key
                )
            );
        }


        private void ValidateUpload(
            IFormFile? file,
            string propertyName,
            string[] permittedExtensions)
        {
            if (file == null)
            {
                return;
            }


            if (file.Length <= 0)
            {
                ModelState.AddModelError(
                    propertyName,
                    "The selected file is empty."
                );

                return;
            }


            if (file.Length >
                20 * 1024 * 1024)
            {
                ModelState.AddModelError(
                    propertyName,
                    "The file cannot exceed 20 MB."
                );
            }


            string extension =
                Path.GetExtension(
                    file.FileName
                )
                .ToLowerInvariant();


            if (Array.IndexOf(
                    permittedExtensions,
                    extension) < 0)
            {
                ModelState.AddModelError(
                    propertyName,
                    "This file type is not allowed."
                );
            }
        }


        private void ClearProposalWizard()
        {
            HttpContext.Session.Remove(
                "ProposalGuidelinesAccepted"
            );

            HttpContext.Session.Remove(
                "ProducerDetails"
            );

            HttpContext.Session.Remove(
                "ProgrammeDetails"
            );

            HttpContext.Session.Remove(
                "ProductionDetails"
            );

            HttpContext.Session.Remove(
                "ProposalDocumentName"
            );

            HttpContext.Session.Remove(
                "BudgetDocumentName"
            );

            HttpContext.Session.Remove(
                "AdditionalFileName"
            );

            HttpContext.Session.Remove(
                "PilotShowreelLink"
            );
        }
    }
}