using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RESK.WIL.Data;
using RESK.WIL.Models;
using RESK.WIL.Services;

namespace RESK.WIL.Controllers
{
    [Authorize(Roles = "Producer")]
    public class ProducerController : Controller
    {
        // =========================================================
        // UPLOAD SETTINGS
        // =========================================================

        // Each file may be up to 20 MB.
        private const long MaxFileBytes =
            20 * 1024 * 1024;

        // Three 20 MB files plus form data is about 63 MB,
        // so the whole request is allowed up to 70 MB.
        private const long MaxRequestBytes =
            70_000_000;

        private static readonly string[] ProposalExtensions =
        {
            ".pdf"
        };

        private static readonly string[] BudgetExtensions =
        {
            ".pdf",
            ".doc",
            ".docx",
            ".xls",
            ".xlsx"
        };

        private static readonly string[] AdditionalExtensions =
        {
            ".pdf",
            ".doc",
            ".docx",
            ".xls",
            ".xlsx",
            ".jpg",
            ".jpeg",
            ".png"
        };


        // =========================================================
        // SESSION KEYS
        // =========================================================

        private const string GuidelinesKey = "ProposalGuidelinesAccepted";
        private const string ProducerDetailsKey = "ProducerDetails";
        private const string ProgrammeDetailsKey = "ProgrammeDetails";
        private const string ProductionDetailsKey = "ProductionDetails";
        private const string ShowreelKey = "PilotShowreelLink";

        // Id of the ProducerProposals row being edited.
        private const string CurrentDraftKey = "CurrentDraftId";

        // Original file name shown to the producer.
        private const string ProposalNameKey = "ProposalDocumentName";
        private const string BudgetNameKey = "BudgetDocumentName";
        private const string AdditionalNameKey = "AdditionalFileName";

        // Random name of the file saved on the server.
        private const string ProposalStoredKey = "ProposalDocumentStoredName";
        private const string BudgetStoredKey = "BudgetDocumentStoredName";
        private const string AdditionalStoredKey = "AdditionalFileStoredName";


        // Works out a file's content type (e.g. application/pdf)
        // from its extension when a producer downloads it.
        private static readonly FileExtensionContentTypeProvider ContentTypes =
            new FileExtensionContentTypeProvider();


        private readonly UserManager<IdentityUser> _userManager;
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ProducerController> _logger;

        public ProducerController(
            UserManager<IdentityUser> userManager,
            ApplicationDbContext db,
            IWebHostEnvironment environment,
            ILogger<ProducerController> logger)
        {
            _userManager = userManager;
            _db = db;
            _environment = environment;
            _logger = logger;
        }


        // =========================================================
        // PRODUCER DASHBOARD
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            string userId = CurrentUserId();

            List<ProducerProposal> proposals =
                await _db.ProducerProposals
                    .AsNoTracking()
                    .Where(p => p.OwnerUserId == userId)
                    .OrderByDescending(p => p.UpdatedAtUtc)
                    .ToListAsync();


            List<ProducerProposal> submitted =
                proposals
                    .Where(p => p.Status != ProposalStatuses.Draft)
                    .ToList();

            List<ProducerProposal> drafts =
                proposals
                    .Where(p => p.Status == ProposalStatuses.Draft)
                    .ToList();


            var model =
                new ProducerDashboardViewModel
                {
                    ProducerName = await CurrentUserNameAsync(),

                    TotalSubmissions = submitted.Count,

                    InReview =
                        submitted.Count(p =>
                            p.Status == ProposalStatuses.InReview),

                    Approved =
                        submitted.Count(p =>
                            p.Status == ProposalStatuses.Approved),

                    Drafts = drafts.Count,

                    RecentProposals =
                        proposals
                            .Take(5)
                            .Select(p => new ProposalRowViewModel
                            {
                                Id = p.Id,
                                Title = p.DisplayTitle,
                                UpdatedText =
                                    SouthAfricaTime.ShortDate(p.UpdatedAtUtc),
                                Status = p.Status,
                                StatusLabel =
                                    ProposalStatuses.Label(p.Status),
                                StatusCss =
                                    ProposalStatuses.CssClass(p.Status)
                            })
                            .ToList()
                };


            // ---------------------------------------------
            // CURRENT PROPOSAL (latest submitted one)
            // ---------------------------------------------

            ProducerProposal? current =
                submitted.FirstOrDefault();

            if (current != null)
            {
                model.CurrentProposal =
                    new CurrentProposalViewModel
                    {
                        Id = current.Id,
                        Title = current.DisplayTitle,
                        StatusLabel =
                            current.Status == ProposalStatuses.InReview
                                ? "Under review"
                                : ProposalStatuses.Label(current.Status),
                        StatusCss =
                            ProposalStatuses.CssClass(current.Status),
                        MetaLine = BuildMetaLine(current),
                        SubmittedText =
                            current.SubmittedAtUtc.HasValue
                                ? "Submitted " +
                                  SouthAfricaTime.LongDate(
                                      current.SubmittedAtUtc.Value)
                                : string.Empty,
                        WorkflowStage =
                            ProposalStatuses.WorkflowStage(current.Status)
                    };
            }


            // ---------------------------------------------
            // NEXT ACTION
            // ---------------------------------------------

            ProducerProposal? needsChanges =
                submitted.FirstOrDefault(p =>
                    p.Status == ProposalStatuses.ChangesRequested);

            if (needsChanges != null)
            {
                model.NextActionTitle = "Changes requested";
                model.NextActionText =
                    $"A reviewer asked for changes to \"{needsChanges.DisplayTitle}\". " +
                    "Update it and send it back.";
                model.NextActionButton = "Review feedback";
                model.NextActionAction = nameof(Details);
                model.NextActionProposalId = needsChanges.Id;
            }
            else if (drafts.Count > 0)
            {
                model.NextActionTitle = "Finish your draft";
                model.NextActionText =
                    drafts.Count == 1
                        ? "You have 1 unsent proposal. Continue where you left off."
                        : $"You have {drafts.Count} unsent proposals. Continue where you left off.";
                model.NextActionButton = "Go to drafts";
                model.NextActionAction = nameof(Drafts);
            }
            else if (submitted.Any(p =>
                         p.Status == ProposalStatuses.InReview))
            {
                model.NextActionTitle = "No action required right now";
                model.NextActionText =
                    "Your proposal is currently being reviewed. " +
                    "You will be notified if changes are requested.";
                model.NextActionButton = "Check status details";
                model.NextActionAction = nameof(Details);
                model.NextActionProposalId = current?.Id;
            }
            else if (submitted.Count > 0)
            {
                model.NextActionTitle = "Start your next proposal";
                model.NextActionText =
                    "None of your proposals need action. " +
                    "Start a new programme submission when you are ready.";
                model.NextActionButton = "Start proposal";
                model.NextActionAction = nameof(Guidelines);
            }
            else
            {
                model.NextActionTitle = "Create your first proposal";
                model.NextActionText =
                    "You currently have no proposals. " +
                    "Start a new programme submission to begin.";
                model.NextActionButton = "Start proposal";
                model.NextActionAction = nameof(Guidelines);
            }


            return View(model);
        }


        // =========================================================
        // DRAFTS
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Drafts()
        {
            string userId = CurrentUserId();

            List<ProducerProposal> drafts =
                await _db.ProducerProposals
                    .AsNoTracking()
                    .Where(p =>
                        p.OwnerUserId == userId &&
                        p.Status == ProposalStatuses.Draft)
                    .OrderByDescending(p => p.UpdatedAtUtc)
                    .ToListAsync();


            var model =
                new ProducerDraftsViewModel
                {
                    ProducerName = await CurrentUserNameAsync(),

                    NearlyComplete =
                        drafts.Count(p => p.CompletedSteps >= 4),

                    Message =
                        TempData["DraftsMessage"] as string,

                    Drafts =
                        drafts
                            .Select(p => new DraftCardViewModel
                            {
                                Id = p.Id,
                                Title = p.DisplayTitle,
                                Initials = Initials(p.DisplayTitle),
                                Category =
                                    string.IsNullOrWhiteSpace(p.Category)
                                        ? "No category yet"
                                        : p.Category,
                                UpdatedText =
                                    "Last updated " +
                                    SouthAfricaTime.Friendly(p.UpdatedAtUtc),
                                UpdatedSortKey =
                                    new DateTimeOffset(
                                        DateTime.SpecifyKind(
                                            p.UpdatedAtUtc,
                                            DateTimeKind.Utc))
                                        .ToUnixTimeMilliseconds(),
                                ProgressPercent = p.ProgressPercent
                            })
                            .ToList()
                };


            ProducerProposal? latest =
                drafts.FirstOrDefault();

            if (latest != null)
            {
                model.LastUpdatedDay =
                    SouthAfricaTime.DayLabel(latest.UpdatedAtUtc);

                model.LastUpdatedTime =
                    SouthAfricaTime.Time(latest.UpdatedAtUtc);
            }


            return View(model);
        }


        // Reopens a saved draft in the wizard at the next
        // unfinished step.
        [HttpGet]
        public async Task<IActionResult> ContinueDraft(int id)
        {
            ProducerProposal? draft =
                await FindOwnDraftAsync(id);

            if (draft == null)
            {
                TempData["DraftsMessage"] =
                    "That draft could not be found. It may have been deleted or submitted.";

                return RedirectToAction(nameof(Drafts));
            }


            // Replace whatever the wizard held before with
            // this draft's saved answers.
            ClearProposalWizard();

            HttpContext.Session.SetString(GuidelinesKey, "true");
            HttpContext.Session.SetInt32(CurrentDraftKey, draft.Id);

            SetOrRemove(ProducerDetailsKey, draft.ProducerDetailsJson);
            SetOrRemove(ProgrammeDetailsKey, draft.ProgrammeDetailsJson);
            SetOrRemove(ProductionDetailsKey, draft.ProductionDetailsJson);
            SetOrRemove(ShowreelKey, draft.PilotShowreelLink);

            SetOrRemove(ProposalNameKey, draft.ProposalDocumentName);
            SetOrRemove(ProposalStoredKey, draft.ProposalDocumentStoredName);
            SetOrRemove(BudgetNameKey, draft.BudgetDocumentName);
            SetOrRemove(BudgetStoredKey, draft.BudgetDocumentStoredName);
            SetOrRemove(AdditionalNameKey, draft.AdditionalFileName);
            SetOrRemove(AdditionalStoredKey, draft.AdditionalFileStoredName);


            // Go to the first step that is not finished yet.
            string nextStep =
                draft.CompletedSteps switch
                {
                    <= 0 => nameof(Create),
                    1 => nameof(ProgrammeDetails),
                    2 => nameof(ProductionDetails),
                    3 => nameof(Attachments),
                    _ => nameof(Review)
                };

            return RedirectToAction(nextStep);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDraft(int id)
        {
            ProducerProposal? draft =
                await FindOwnDraftAsync(id);

            if (draft == null)
            {
                TempData["DraftsMessage"] =
                    "That draft could not be found. It may already have been deleted.";

                return RedirectToAction(nameof(Drafts));
            }


            string title = draft.DisplayTitle;

            // Remove the draft's uploaded files from disk.
            DeleteUploadFile(draft.ProposalDocumentStoredName);
            DeleteUploadFile(draft.BudgetDocumentStoredName);
            DeleteUploadFile(draft.AdditionalFileStoredName);

            _db.ProducerProposals.Remove(draft);
            await _db.SaveChangesAsync();


            // If this draft was open in the wizard, close it.
            if (HttpContext.Session.GetInt32(CurrentDraftKey) == id)
            {
                ClearProposalWizard();
            }


            TempData["DraftsMessage"] =
                $"\"{title}\" was deleted.";

            return RedirectToAction(nameof(Drafts));
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

            // Start a brand-new proposal. Any earlier proposal
            // is already saved as a draft, so nothing is lost.
            ClearProposalWizard();

            HttpContext.Session.SetString(
                GuidelinesKey,
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

            ProducerDetailsViewModel? savedModel =
                ReadSession<ProducerDetailsViewModel>(
                    ProducerDetailsKey);

            if (savedModel != null)
            {
                return View(savedModel);
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
        public async Task<IActionResult> Create(
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
                ProducerDetailsKey,
                JsonSerializer.Serialize(model)
            );

            await SaveDraftAsync(completedStep: 1);

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

            if (!HasSessionValue(ProducerDetailsKey))
            {
                return RedirectToAction(nameof(Create));
            }

            ProgrammeDetailsViewModel? savedModel =
                ReadSession<ProgrammeDetailsViewModel>(
                    ProgrammeDetailsKey);

            return View(
                savedModel ?? new ProgrammeDetailsViewModel()
            );
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProgrammeDetails(
            ProgrammeDetailsViewModel model)
        {
            if (!HasAcceptedGuidelines())
            {
                return RedirectToAction(nameof(Guidelines));
            }

            if (!HasSessionValue(ProducerDetailsKey))
            {
                return RedirectToAction(nameof(Create));
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            HttpContext.Session.SetString(
                ProgrammeDetailsKey,
                JsonSerializer.Serialize(model)
            );

            await SaveDraftAsync(completedStep: 2);

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

            if (!HasSessionValue(ProducerDetailsKey))
            {
                return RedirectToAction(nameof(Create));
            }

            if (!HasSessionValue(ProgrammeDetailsKey))
            {
                return RedirectToAction(
                    nameof(ProgrammeDetails)
                );
            }

            ProductionDetailsViewModel? savedModel =
                ReadSession<ProductionDetailsViewModel>(
                    ProductionDetailsKey);

            return View(
                savedModel ?? new ProductionDetailsViewModel()
            );
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProductionDetails(
            ProductionDetailsViewModel model)
        {
            if (!HasAcceptedGuidelines())
            {
                return RedirectToAction(nameof(Guidelines));
            }

            if (!HasSessionValue(ProducerDetailsKey))
            {
                return RedirectToAction(nameof(Create));
            }

            if (!HasSessionValue(ProgrammeDetailsKey))
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
                ProductionDetailsKey,
                JsonSerializer.Serialize(model)
            );

            await SaveDraftAsync(completedStep: 3);

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
            IActionResult? redirect =
                RedirectIfEarlierStepsMissing();

            if (redirect != null)
            {
                return redirect;
            }

            var model =
                new AttachmentsViewModel
                {
                    PilotShowreelLink =
                        HttpContext.Session.GetString(
                            ShowreelKey
                        )
                };

            FillExistingFileNames(model);

            return View(model);
        }


        // =========================================================
        // STEP 4 - ATTACHMENTS POST
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
        public async Task<IActionResult> Attachments(
            AttachmentsViewModel model)
        {
            IActionResult? redirect =
                RedirectIfEarlierStepsMissing();

            if (redirect != null)
            {
                return redirect;
            }


            // Proposal PDF is required, unless one was already
            // saved earlier.
            if (model.ProposalDocument == null &&
                !HasStoredFile(ProposalStoredKey))
            {
                ModelState.AddModelError(
                    nameof(model.ProposalDocument),
                    "Please upload a proposal PDF."
                );
            }


            ValidateUpload(
                model.ProposalDocument,
                nameof(model.ProposalDocument),
                ProposalExtensions
            );

            ValidateUpload(
                model.BudgetDocument,
                nameof(model.BudgetDocument),
                BudgetExtensions
            );

            ValidateUpload(
                model.AdditionalFile,
                nameof(model.AdditionalFile),
                AdditionalExtensions
            );


            if (!ModelState.IsValid)
            {
                FillExistingFileNames(model);

                return View(model);
            }


            try
            {
                if (model.ProposalDocument != null)
                {
                    await SaveUploadAsync(
                        model.ProposalDocument,
                        ProposalNameKey,
                        ProposalStoredKey
                    );
                }

                if (model.BudgetDocument != null)
                {
                    await SaveUploadAsync(
                        model.BudgetDocument,
                        BudgetNameKey,
                        BudgetStoredKey
                    );
                }

                if (model.AdditionalFile != null)
                {
                    await SaveUploadAsync(
                        model.AdditionalFile,
                        AdditionalNameKey,
                        AdditionalStoredKey
                    );
                }
            }
            catch (Exception ex) when (
                ex is IOException ||
                ex is UnauthorizedAccessException)
            {
                _logger.LogError(
                    ex,
                    "Saving proposal attachments failed."
                );

                ModelState.AddModelError(
                    string.Empty,
                    "We couldn't save your files. Please try again."
                );

                FillExistingFileNames(model);

                return View(model);
            }


            if (!string.IsNullOrWhiteSpace(
                model.PilotShowreelLink))
            {
                HttpContext.Session.SetString(
                    ShowreelKey,
                    model.PilotShowreelLink
                );
            }
            else
            {
                HttpContext.Session.Remove(
                    ShowreelKey
                );
            }


            await SaveDraftAsync(completedStep: 4);


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
            IActionResult? redirect =
                RedirectIfEarlierStepsMissing();

            if (redirect != null)
            {
                return redirect;
            }


            if (!HasStoredFile(ProposalStoredKey))
            {
                return RedirectToAction(
                    nameof(Attachments)
                );
            }


            ProducerDetailsViewModel? producer =
                ReadSession<ProducerDetailsViewModel>(
                    ProducerDetailsKey);

            ProgrammeDetailsViewModel? programme =
                ReadSession<ProgrammeDetailsViewModel>(
                    ProgrammeDetailsKey);

            ProductionDetailsViewModel? production =
                ReadSession<ProductionDetailsViewModel>(
                    ProductionDetailsKey);


            if (producer == null)
            {
                return RedirectToAction(nameof(Create));
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
                            ProposalNameKey
                        ),

                    BudgetDocumentName =
                        HttpContext.Session.GetString(
                            BudgetNameKey
                        ),

                    AdditionalFileName =
                        HttpContext.Session.GetString(
                            AdditionalNameKey
                        ),

                    PilotShowreelLink =
                        HttpContext.Session.GetString(
                            ShowreelKey
                        )
                };


            return View(model);
        }


        // =========================================================
        // STEP 5 - SUBMIT PROPOSAL
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitProposal(
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


            IActionResult? redirect =
                RedirectIfEarlierStepsMissing();

            if (redirect != null)
            {
                return redirect;
            }


            if (!HasStoredFile(ProposalStoredKey))
            {
                return RedirectToAction(
                    nameof(Attachments)
                );
            }


            // Make sure the saved row has the latest answers,
            // then turn the draft into a submitted proposal.
            ProducerProposal proposal =
                await SaveDraftAsync(completedStep: 4);

            DateTime now = DateTime.UtcNow;

            if (!ProposalWorkflow.TryMove(proposal, ProposalStatuses.InReview, out string moveError))
            {
                TempData["SubmitError"] = moveError;
                return RedirectToAction(nameof(Review));
            }

            proposal.CompletedSteps = 5;
            proposal.SubmittedAtUtc = now;
            proposal.UpdatedAtUtc = now;

            // The row id is unique, so the reference is too.
            proposal.Reference =
                $"CTV-{SouthAfricaTime.ToLocal(now).Year}-{proposal.Id:D4}";

            await _db.SaveChangesAsync();


            TempData["SubmittedReference"] =
                proposal.Reference;

            TempData["SubmittedDate"] =
                SouthAfricaTime.ToLocal(now).ToString(
                    "dd MMMM yyyy 'at' HH:mm"
                );


            // The files stay on disk: they now belong to the
            // submitted proposal.
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
        // MY PROPOSALS (submitted proposals only - no drafts)
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> MyProposals()
        {
            string userId = CurrentUserId();

            List<ProducerProposal> submitted =
                await _db.ProducerProposals
                    .AsNoTracking()
                    .Where(p =>
                        p.OwnerUserId == userId &&
                        p.Status != ProposalStatuses.Draft)
                    .OrderByDescending(p => p.UpdatedAtUtc)
                    .ToListAsync();


            var model =
                new ProducerMyProposalsViewModel
                {
                    ProducerName = await CurrentUserNameAsync(),

                    Message =
                        TempData["ProposalsMessage"] as string,

                    Proposals =
                        submitted
                            .Select(p => new MyProposalRowViewModel
                            {
                                Id = p.Id,
                                Title = p.DisplayTitle,
                                Category =
                                    string.IsNullOrWhiteSpace(p.Category)
                                        ? "No category"
                                        : p.Category,
                                UpdatedText =
                                    "Updated " +
                                    SouthAfricaTime.ShortDate(p.UpdatedAtUtc),
                                Reference = p.Reference,
                                StatusLabel = StatusText(p.Status),
                                StatusCss =
                                    ProposalStatuses.CssClass(p.Status)
                            })
                            .ToList()
                };


            return View(model);
        }


        // =========================================================
        // VIEW ONE PROPOSAL
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return RedirectToAction(nameof(MyProposals));
            }


            ProducerProposal? proposal =
                await FindOwnProposalAsync(id.Value);

            if (proposal == null)
            {
                TempData["ProposalsMessage"] =
                    "That proposal could not be found.";

                return RedirectToAction(nameof(MyProposals));
            }


            // Drafts are opened in the wizard instead.
            if (proposal.Status == ProposalStatuses.Draft)
            {
                return RedirectToAction(
                    nameof(ContinueDraft),
                    new { id = proposal.Id });
            }


            var model =
                new ProducerProposalDetailsViewModel
                {
                    ProducerName = await CurrentUserNameAsync(),

                    Id = proposal.Id,
                    Title = proposal.DisplayTitle,
                    Reference = proposal.Reference ?? "—",
                    Category =
                        string.IsNullOrWhiteSpace(proposal.Category)
                            ? "—"
                            : proposal.Category,
                    MetaLine = BuildMetaLine(proposal),

                    StatusLabel = StatusText(proposal.Status),
                    StatusCss = ProposalStatuses.CssClass(proposal.Status),
                    WorkflowStage =
                        ProposalStatuses.WorkflowStage(proposal.Status),

                    SubmittedText =
                        proposal.SubmittedAtUtc.HasValue
                            ? SouthAfricaTime.LongDate(
                                proposal.SubmittedAtUtc.Value)
                            : "—",

                    UpdatedText =
                        SouthAfricaTime.Friendly(proposal.UpdatedAtUtc),

                    ShowreelUrl =
                        SafeWebLink(proposal.PilotShowreelLink)
                };


            // The producer's answers from each wizard step.
            AddSection(model, "Programme details", proposal.ProgrammeDetailsJson);
            AddSection(model, "Producer details", proposal.ProducerDetailsJson);
            AddSection(model, "Production details", proposal.ProductionDetailsJson);


            // Uploaded files.
            AddAttachment(model, "proposal", "Proposal document", proposal.ProposalDocumentName);
            AddAttachment(model, "budget", "Budget document", proposal.BudgetDocumentName);
            AddAttachment(model, "additional", "Additional file", proposal.AdditionalFileName);


            return View(model);
        }


        // Downloads one of a proposal's uploaded files.
        // file = "proposal", "budget" or "additional"
        [HttpGet]
        public async Task<IActionResult> Attachment(
            int id,
            string file)
        {
            ProducerProposal? proposal =
                await FindOwnProposalAsync(id);

            if (proposal == null)
            {
                return NotFound();
            }


            string? originalName = null;
            string? storedName = null;

            switch (file)
            {
                case "proposal":
                    originalName = proposal.ProposalDocumentName;
                    storedName = proposal.ProposalDocumentStoredName;
                    break;

                case "budget":
                    originalName = proposal.BudgetDocumentName;
                    storedName = proposal.BudgetDocumentStoredName;
                    break;

                case "additional":
                    originalName = proposal.AdditionalFileName;
                    storedName = proposal.AdditionalFileStoredName;
                    break;
            }


            string? path = UploadPath(storedName);

            if (string.IsNullOrWhiteSpace(originalName) ||
                path == null ||
                !System.IO.File.Exists(path))
            {
                return NotFound();
            }


            if (!ContentTypes.TryGetContentType(
                    originalName,
                    out string? contentType))
            {
                contentType = "application/octet-stream";
            }


            return PhysicalFile(
                path,
                contentType,
                originalName);
        }


        // =========================================================
        // EXISTING PLACEHOLDER PAGE
        // =========================================================

        [HttpGet]
        public IActionResult Edit(int? id)
        {
            return View();
        }


        // =========================================================
        // HELPERS - VIEWING PROPOSALS
        // =========================================================

        // Any proposal (draft or submitted) owned by the
        // signed-in producer.
        private async Task<ProducerProposal?> FindOwnProposalAsync(
            int id)
        {
            string userId = CurrentUserId();

            return await _db.ProducerProposals
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.Id == id &&
                    p.OwnerUserId == userId);
        }


        // Same as the dashboard labels, but spelled out in full
        // for "changes requested".
        private static string StatusText(
            string status)
        {
            return status == ProposalStatuses.ChangesRequested
                ? "Changes needed"
                : ProposalStatuses.Label(status);
        }


        // Turns one wizard step's saved JSON into a list of
        // "label: value" rows for the proposal page.
        private static void AddSection(
            ProducerProposalDetailsViewModel model,
            string title,
            string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            var section =
                new ProposalDetailSection
                {
                    Title = title
                };

            try
            {
                using JsonDocument document =
                    JsonDocument.Parse(json);

                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return;
                }

                foreach (JsonProperty property in
                         document.RootElement.EnumerateObject())
                {
                    string value =
                        FormatJsonValue(property.Value);

                    section.Fields.Add(
                        new ProposalDetailField
                        {
                            Label = Humanize(property.Name),
                            Value =
                                string.IsNullOrWhiteSpace(value)
                                    ? "Not provided"
                                    : value
                        });
                }
            }
            catch (JsonException)
            {
                return;
            }

            if (section.Fields.Count > 0)
            {
                model.Sections.Add(section);
            }
        }


        private static string FormatJsonValue(
            JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Number => element.GetRawText(),
                JsonValueKind.True => "Yes",
                JsonValueKind.False => "No",
                JsonValueKind.Array =>
                    string.Join(
                        ", ",
                        element.EnumerateArray()
                            .Select(FormatJsonValue)
                            .Where(v => !string.IsNullOrWhiteSpace(v))),
                JsonValueKind.Object => element.GetRawText(),
                _ => string.Empty
            };
        }


        // "PreferredContactMethod" -> "Preferred contact method"
        // "CTTVSupport"            -> "CTTV support"
        private static string Humanize(
            string name)
        {
            string spaced =
                Regex.Replace(
                    name,
                    "(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])",
                    " ");

            string[] words =
                spaced.Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries);

            for (int i = 1; i < words.Length; i++)
            {
                // Keep acronyms like "CTTV" in capitals.
                if (words[i].Length > 1 &&
                    words[i].All(char.IsUpper))
                {
                    continue;
                }

                words[i] = words[i].ToLowerInvariant();
            }

            return string.Join(' ', words);
        }


        private static void AddAttachment(
            ProducerProposalDetailsViewModel model,
            string key,
            string label,
            string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return;
            }

            model.Attachments.Add(
                new ProposalAttachmentLink
                {
                    Key = key,
                    Label = label,
                    FileName = fileName
                });
        }


        // Only real http/https links are shown as clickable,
        // so a saved value can never run script in the page.
        private static string? SafeWebLink(
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                !Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri? uri) ||
                (uri.Scheme != Uri.UriSchemeHttp &&
                 uri.Scheme != Uri.UriSchemeHttps))
            {
                return null;
            }

            return uri.ToString();
        }


        // =========================================================
        // HELPERS - CURRENT USER
        // =========================================================

        private string CurrentUserId()
        {
            // [Authorize] guarantees a signed-in user.
            return _userManager.GetUserId(User) ?? string.Empty;
        }


        private async Task<string> CurrentUserNameAsync()
        {
            IdentityUser? user =
                await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return "Producer";
            }

            return RESK.WIL.Services.ProducerProfileStore.DisplayName(
                RESK.WIL.Services.ProducerProfileStore.Load(
                    _environment.ContentRootPath,
                    user.Id),
                user);
        }


        // =========================================================
        // HELPERS - DRAFTS
        // =========================================================

        private async Task<ProducerProposal?> FindOwnDraftAsync(
            int id)
        {
            string userId = CurrentUserId();

            return await _db.ProducerProposals
                .FirstOrDefaultAsync(p =>
                    p.Id == id &&
                    p.OwnerUserId == userId &&
                    p.Status == ProposalStatuses.Draft);
        }


        /*
         * Saves everything the wizard currently holds in Session
         * to the ProducerProposals table.
         *
         * The first call creates the draft row; later calls
         * update the same row (its id is kept in Session).
         */
        private async Task<ProducerProposal> SaveDraftAsync(
            int completedStep)
        {
            string userId = CurrentUserId();
            DateTime now = DateTime.UtcNow;

            ProducerProposal? draft = null;

            int? draftId =
                HttpContext.Session.GetInt32(CurrentDraftKey);

            if (draftId.HasValue)
            {
                draft = await FindOwnDraftAsync(draftId.Value);
            }

            if (draft == null)
            {
                draft =
                    new ProducerProposal
                    {
                        OwnerUserId = userId,
                        Status = ProposalStatuses.Draft,
                        CreatedAtUtc = now
                    };

                _db.ProducerProposals.Add(draft);
            }


            // Full answers for each step.
            draft.ProducerDetailsJson =
                HttpContext.Session.GetString(ProducerDetailsKey);

            draft.ProgrammeDetailsJson =
                HttpContext.Session.GetString(ProgrammeDetailsKey);

            draft.ProductionDetailsJson =
                HttpContext.Session.GetString(ProductionDetailsKey);


            // Summary fields for lists and the dashboard.
            ProgrammeDetailsViewModel? programme =
                ReadSession<ProgrammeDetailsViewModel>(
                    ProgrammeDetailsKey);

            if (programme != null)
            {
                draft.ProgrammeTitle = Limit(programme.ProgrammeTitle, 200);
                draft.Category = Limit(programme.Category, 100);
                draft.ProgrammeFormat = Limit(programme.ProgrammeFormat, 100);
                draft.EpisodeDuration = Limit(programme.EpisodeDuration, 50);
                draft.PrimaryLanguage = Limit(programme.PrimaryLanguage, 100);
            }


            // Attachments.
            draft.PilotShowreelLink =
                LimitOrNull(
                    HttpContext.Session.GetString(ShowreelKey), 500);

            draft.ProposalDocumentName =
                LimitOrNull(
                    HttpContext.Session.GetString(ProposalNameKey), 260);

            draft.ProposalDocumentStoredName =
                HttpContext.Session.GetString(ProposalStoredKey);

            draft.BudgetDocumentName =
                LimitOrNull(
                    HttpContext.Session.GetString(BudgetNameKey), 260);

            draft.BudgetDocumentStoredName =
                HttpContext.Session.GetString(BudgetStoredKey);

            draft.AdditionalFileName =
                LimitOrNull(
                    HttpContext.Session.GetString(AdditionalNameKey), 260);

            draft.AdditionalFileStoredName =
                HttpContext.Session.GetString(AdditionalStoredKey);


            // Going back to an earlier step never lowers progress.
            draft.CompletedSteps =
                Math.Max(draft.CompletedSteps, completedStep);

            draft.UpdatedAtUtc = now;


            await _db.SaveChangesAsync();

            HttpContext.Session.SetInt32(CurrentDraftKey, draft.Id);

            return draft;
        }


        private static string BuildMetaLine(
            ProducerProposal proposal)
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(proposal.ProgrammeFormat))
            {
                parts.Add(proposal.ProgrammeFormat);
            }

            if (!string.IsNullOrWhiteSpace(proposal.EpisodeDuration))
            {
                string duration = proposal.EpisodeDuration.Trim();

                // "30" becomes "30 minute episode".
                parts.Add(
                    duration.All(char.IsDigit)
                        ? $"{duration} minute episode"
                        : duration);
            }

            if (!string.IsNullOrWhiteSpace(proposal.PrimaryLanguage))
            {
                parts.Add(proposal.PrimaryLanguage);
            }

            if (parts.Count == 0 &&
                !string.IsNullOrWhiteSpace(proposal.Category))
            {
                parts.Add(proposal.Category);
            }

            return string.Join(" • ", parts);
        }


        // "Street Stories" -> "SS"
        private static string Initials(
            string title)
        {
            string[] words =
                title.Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries);

            string letters =
                string.Concat(
                    words
                        .Where(w => char.IsLetterOrDigit(w[0]))
                        .Take(2)
                        .Select(w => char.ToUpperInvariant(w[0])));

            return string.IsNullOrEmpty(letters)
                ? "P"
                : letters;
        }


        private static string Limit(
            string? value,
            int maxLength)
        {
            value = value?.Trim() ?? string.Empty;

            return value.Length <= maxLength
                ? value
                : value.Substring(0, maxLength);
        }


        private static string? LimitOrNull(
            string? value,
            int maxLength)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : Limit(value, maxLength);
        }


        // =========================================================
        // HELPERS - WIZARD
        // =========================================================

        private bool HasAcceptedGuidelines()
        {
            return HttpContext.Session.GetString(
                GuidelinesKey
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


        private void SetOrRemove(
            string key,
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                HttpContext.Session.Remove(key);
            }
            else
            {
                HttpContext.Session.SetString(key, value);
            }
        }


        // Reads a step's saved answers from Session.
        // Returns null (and forgets the value) if it is
        // missing or can't be read.
        private T? ReadSession<T>(
            string key)
            where T : class
        {
            string? json =
                HttpContext.Session.GetString(key);

            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<T>(json);
            }
            catch (JsonException)
            {
                HttpContext.Session.Remove(key);

                return null;
            }
        }


        private IActionResult? RedirectIfEarlierStepsMissing()
        {
            if (!HasAcceptedGuidelines())
            {
                return RedirectToAction(nameof(Guidelines));
            }

            if (!HasSessionValue(ProducerDetailsKey))
            {
                return RedirectToAction(nameof(Create));
            }

            if (!HasSessionValue(ProgrammeDetailsKey))
            {
                return RedirectToAction(
                    nameof(ProgrammeDetails)
                );
            }

            if (!HasSessionValue(ProductionDetailsKey))
            {
                return RedirectToAction(
                    nameof(ProductionDetails)
                );
            }

            return null;
        }


        private void FillExistingFileNames(
            AttachmentsViewModel model)
        {
            model.ExistingProposalDocument =
                HasStoredFile(ProposalStoredKey)
                    ? HttpContext.Session.GetString(ProposalNameKey)
                    : null;

            model.ExistingBudgetDocument =
                HasStoredFile(BudgetStoredKey)
                    ? HttpContext.Session.GetString(BudgetNameKey)
                    : null;

            model.ExistingAdditionalFile =
                HasStoredFile(AdditionalStoredKey)
                    ? HttpContext.Session.GetString(AdditionalNameKey)
                    : null;
        }


        // Forgets the wizard's Session data. Uploaded files are
        // kept, because they belong to a saved draft or a
        // submitted proposal.
        private void ClearProposalWizard()
        {
            string[] keys =
            {
                GuidelinesKey,
                CurrentDraftKey,
                ProducerDetailsKey,
                ProgrammeDetailsKey,
                ProductionDetailsKey,
                ProposalNameKey,
                BudgetNameKey,
                AdditionalNameKey,
                ProposalStoredKey,
                BudgetStoredKey,
                AdditionalStoredKey,
                ShowreelKey
            };

            foreach (string key in keys)
            {
                HttpContext.Session.Remove(key);
            }
        }


        // =========================================================
        // HELPERS - UPLOADS
        // =========================================================

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


            if (file.Length > MaxFileBytes)
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


        /*
         * Files are saved OUTSIDE wwwroot, in:
         *
         * App_Data/ProposalUploads/{userId}/
         *
         * so they can never be downloaded directly by URL.
         * Each file gets a random name; the original name is
         * only kept for display.
         */
        private string GetUserUploadFolder()
        {
            return Path.Combine(
                _environment.ContentRootPath,
                "App_Data",
                "ProposalUploads",
                SafeFileName(CurrentUserId())
            );
        }


        private async Task SaveUploadAsync(
            IFormFile file,
            string nameKey,
            string storedKey)
        {
            string folder =
                GetUserUploadFolder();

            Directory.CreateDirectory(folder);


            // Replace any file uploaded earlier for this slot.
            DeleteUploadFile(
                HttpContext.Session.GetString(storedKey));


            string extension =
                Path.GetExtension(file.FileName)
                    .ToLowerInvariant();

            string storedName =
                $"{Guid.NewGuid():N}{extension}";

            string fullPath =
                Path.Combine(folder, storedName);


            await using (var stream =
                new FileStream(
                    fullPath,
                    FileMode.CreateNew))
            {
                await file.CopyToAsync(stream);
            }


            HttpContext.Session.SetString(
                nameKey,
                SafeFileName(
                    Path.GetFileName(file.FileName)
                )
            );

            HttpContext.Session.SetString(
                storedKey,
                storedName
            );
        }


        private string? UploadPath(
            string? storedName)
        {
            if (string.IsNullOrWhiteSpace(storedName))
            {
                return null;
            }

            // GetFileName stops "..\" tricks in the stored value.
            return Path.Combine(
                GetUserUploadFolder(),
                Path.GetFileName(storedName)
            );
        }


        private bool HasStoredFile(
            string storedKey)
        {
            string? path =
                UploadPath(
                    HttpContext.Session.GetString(storedKey));

            return path != null &&
                   System.IO.File.Exists(path);
        }


        private void DeleteUploadFile(
            string? storedName)
        {
            string? path =
                UploadPath(storedName);

            if (path == null ||
                !System.IO.File.Exists(path))
            {
                return;
            }

            try
            {
                System.IO.File.Delete(path);
            }
            catch (Exception ex) when (
                ex is IOException ||
                ex is UnauthorizedAccessException)
            {
                // A leftover file is not worth failing the
                // request for; log it and carry on.
                _logger.LogWarning(
                    ex,
                    "Could not delete upload {Path}.",
                    path
                );
            }
        }


        private static string SafeFileName(
            string name)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalid, '_');
            }

            return string.IsNullOrWhiteSpace(name)
                ? "file"
                : name;
        }
    }
}