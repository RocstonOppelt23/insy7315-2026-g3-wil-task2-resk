using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RESK.WIL.Models;

namespace RESK.WIL.Controllers
{
    /*
     * =========================================================
     * SAVE DRAFT + CANCEL (proposal wizard)
     * =========================================================
     *
     *   POST /Producer/SaveDraft        "Save draft" on any wizard step:
     *                                   saves what is on the page (even if
     *                                   unfinished) and opens the Drafts page.
     *
     *   GET  /Producer/CancelProposal   "Cancel": leaves the wizard and goes
     *                                   back to the dashboard. Steps that were
     *                                   already finished stay in Drafts.
     *
     *   "Log out" on a wizard page posts to SaveDraft with logout=true:
     *   the page is saved to Drafts first, then the producer is signed
     *   out and sent to the login page.
     *
     * "step" tells us which page the producer was on:
     *   1 Producer details, 2 Programme details, 3 Production details,
     *   4 Attachments, 5 Review
     */
    [Authorize(Roles = "Producer")]
    [Route("Producer")]
    public class ProducerDraftController : Controller
    {
        // Same Session keys as ProducerController.
        private const string GuidelinesKey = "ProposalGuidelinesAccepted";
        private const string ProducerDetailsKey = "ProducerDetails";
        private const string ProgrammeDetailsKey = "ProgrammeDetails";
        private const string ProductionDetailsKey = "ProductionDetails";
        private const string ShowreelKey = "PilotShowreelLink";
        private const string CurrentDraftKey = "CurrentDraftId";
        private const string ProposalNameKey = "ProposalDocumentName";
        private const string BudgetNameKey = "BudgetDocumentName";
        private const string AdditionalNameKey = "AdditionalFileName";
        private const string ProposalStoredKey = "ProposalDocumentStoredName";
        private const string BudgetStoredKey = "BudgetDocumentStoredName";
        private const string AdditionalStoredKey = "AdditionalFileStoredName";

        // Same upload rules as ProducerController.
        private const long MaxFileBytes = 20 * 1024 * 1024;

        private static readonly string[] ProposalExtensions = { ".pdf" };

        private static readonly string[] BudgetExtensions =
            { ".pdf", ".doc", ".docx", ".xls", ".xlsx" };

        private static readonly string[] AdditionalExtensions =
            { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".jpg", ".jpeg", ".png" };


        private readonly RESK.WIL.Data.ApplicationDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ProducerDraftController> _logger;

        public ProducerDraftController(
            RESK.WIL.Data.ApplicationDbContext db,
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            IWebHostEnvironment environment,
            ILogger<ProducerDraftController> logger)
        {
            _db = db;
            _userManager = userManager;
            _signInManager = signInManager;
            _environment = environment;
            _logger = logger;
        }


        // =========================================================
        // SAVE DRAFT
        // =========================================================

        [HttpPost("SaveDraft")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(70_000_000)]
        [RequestFormLimits(MultipartBodyLengthLimit = 70_000_000)]
        public async Task<IActionResult> SaveDraft(int step, bool logout = false)
        {
            // Keep what is on the current page, even if it is not
            // complete yet. Nothing is validated for a draft.
            switch (step)
            {
                case 1:
                    var producer = new ProducerDetailsViewModel();
                    await TryUpdateModelAsync(producer, string.Empty);
                    HttpContext.Session.SetString(
                        ProducerDetailsKey,
                        JsonSerializer.Serialize(producer));
                    break;

                case 2:
                    var programme = new ProgrammeDetailsViewModel();
                    await TryUpdateModelAsync(programme, string.Empty);
                    HttpContext.Session.SetString(
                        ProgrammeDetailsKey,
                        JsonSerializer.Serialize(programme));
                    break;

                case 3:
                    var production = new ProductionDetailsViewModel();
                    await TryUpdateModelAsync(production, string.Empty);
                    HttpContext.Session.SetString(
                        ProductionDetailsKey,
                        JsonSerializer.Serialize(production));
                    break;

                case 4:
                    var attachments = new AttachmentsViewModel();
                    await TryUpdateModelAsync(attachments, string.Empty);
                    await SaveAttachmentsAsync(attachments);
                    break;
            }


            bool hasAnything =
                HttpContext.Session.GetString(ProducerDetailsKey) != null ||
                HttpContext.Session.GetString(ProgrammeDetailsKey) != null ||
                HttpContext.Session.GetString(ProductionDetailsKey) != null ||
                HttpContext.Session.GetInt32(CurrentDraftKey) != null;

            if (!hasAnything)
            {
                ClearWizard();

                if (logout)
                {
                    return await SignOutToLoginAsync();
                }

                TempData["DraftsMessage"] =
                    "There was nothing to save yet, so no draft was created.";

                return Redirect("/Producer/Drafts");
            }


            // A draft saved on step 3 has finished steps 1 and 2.
            int finishedSteps = Math.Clamp(step - 1, 0, 4);

            ProducerProposal draft =
                await SaveToDatabaseAsync(finishedSteps);


            // Leave the wizard. "Continue editing" on the Drafts page
            // loads everything back in.
            ClearWizard();

            if (logout)
            {
                return await SignOutToLoginAsync();
            }

            TempData["DraftsMessage"] =
                $"\"{draft.DisplayTitle}\" was saved to your drafts.";

            return Redirect("/Producer/Drafts");
        }


        // =========================================================
        // CANCEL
        // =========================================================

        [HttpGet("CancelProposal")]
        public IActionResult CancelProposal()
        {
            ClearWizard();

            return Redirect("/Producer/Index");
        }


        // =========================================================
        // HELPERS
        // =========================================================

        // Signs the producer out and opens the login page.
        private async Task<IActionResult> SignOutToLoginAsync()
        {
            await _signInManager.SignOutAsync();

            HttpContext.Session.Clear();

            return Redirect("/Account/Login");
        }


        private string CurrentUserId()
        {
            return _userManager.GetUserId(User) ?? string.Empty;
        }


        // Creates or updates the producer's draft row from Session.
        private async Task<ProducerProposal> SaveToDatabaseAsync(
            int finishedSteps)
        {
            string userId = CurrentUserId();
            DateTime now = DateTime.UtcNow;

            ProducerProposal? draft = null;

            int? draftId = HttpContext.Session.GetInt32(CurrentDraftKey);

            if (draftId.HasValue)
            {
                draft = await _db.ProducerProposals
                    .FirstOrDefaultAsync(p =>
                        p.Id == draftId.Value &&
                        p.OwnerUserId == userId &&
                        p.Status == ProposalStatuses.Draft);
            }

            if (draft == null)
            {
                draft = new ProducerProposal
                {
                    OwnerUserId = userId,
                    Status = ProposalStatuses.Draft,
                    CreatedAtUtc = now
                };

                _db.ProducerProposals.Add(draft);
            }


            draft.ProducerDetailsJson = HttpContext.Session.GetString(ProducerDetailsKey);
            draft.ProgrammeDetailsJson = HttpContext.Session.GetString(ProgrammeDetailsKey);
            draft.ProductionDetailsJson = HttpContext.Session.GetString(ProductionDetailsKey);


            // Summary fields used by the Drafts and dashboard lists.
            string? programmeJson = HttpContext.Session.GetString(ProgrammeDetailsKey);

            if (!string.IsNullOrWhiteSpace(programmeJson))
            {
                try
                {
                    ProgrammeDetailsViewModel? programme =
                        JsonSerializer.Deserialize<ProgrammeDetailsViewModel>(programmeJson);

                    if (programme != null)
                    {
                        draft.ProgrammeTitle = Limit(programme.ProgrammeTitle, 200) ?? string.Empty;
                        draft.Category = Limit(programme.Category, 100) ?? string.Empty;
                        draft.ProgrammeFormat = Limit(programme.ProgrammeFormat, 100);
                        draft.EpisodeDuration = Limit(programme.EpisodeDuration, 50);
                        draft.PrimaryLanguage = Limit(programme.PrimaryLanguage, 100);
                    }
                }
                catch (JsonException)
                {
                }
            }


            draft.PilotShowreelLink = Limit(HttpContext.Session.GetString(ShowreelKey), 500);
            draft.ProposalDocumentName = Limit(HttpContext.Session.GetString(ProposalNameKey), 260);
            draft.ProposalDocumentStoredName = HttpContext.Session.GetString(ProposalStoredKey);
            draft.BudgetDocumentName = Limit(HttpContext.Session.GetString(BudgetNameKey), 260);
            draft.BudgetDocumentStoredName = HttpContext.Session.GetString(BudgetStoredKey);
            draft.AdditionalFileName = Limit(HttpContext.Session.GetString(AdditionalNameKey), 260);
            draft.AdditionalFileStoredName = HttpContext.Session.GetString(AdditionalStoredKey);


            // Never lower progress that was already made.
            draft.CompletedSteps = Math.Max(draft.CompletedSteps, finishedSteps);
            draft.UpdatedAtUtc = now;

            await _db.SaveChangesAsync();

            HttpContext.Session.SetInt32(CurrentDraftKey, draft.Id);

            return draft;
        }


        // Attachments step: keep the showreel link and any valid
        // files the producer picked. Invalid files are skipped.
        private async Task SaveAttachmentsAsync(AttachmentsViewModel model)
        {
            if (!string.IsNullOrWhiteSpace(model.PilotShowreelLink))
            {
                HttpContext.Session.SetString(ShowreelKey, model.PilotShowreelLink.Trim());
            }

            try
            {
                await SaveFileIfValidAsync(model.ProposalDocument, ProposalExtensions, ProposalNameKey, ProposalStoredKey);
                await SaveFileIfValidAsync(model.BudgetDocument, BudgetExtensions, BudgetNameKey, BudgetStoredKey);
                await SaveFileIfValidAsync(model.AdditionalFile, AdditionalExtensions, AdditionalNameKey, AdditionalStoredKey);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                _logger.LogError(ex, "Saving draft attachments failed.");
            }
        }


        private async Task SaveFileIfValidAsync(
            IFormFile? file,
            string[] permittedExtensions,
            string nameKey,
            string storedKey)
        {
            if (file == null || file.Length <= 0 || file.Length > MaxFileBytes)
            {
                return;
            }

            string extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (Array.IndexOf(permittedExtensions, extension) < 0)
            {
                return;
            }

            string folder = Path.Combine(
                _environment.ContentRootPath,
                "App_Data",
                "ProposalUploads",
                SafeFileName(CurrentUserId()));

            Directory.CreateDirectory(folder);

            // Replace the file saved earlier for this slot.
            string? oldStored = HttpContext.Session.GetString(storedKey);

            if (!string.IsNullOrWhiteSpace(oldStored))
            {
                string oldPath = Path.Combine(folder, Path.GetFileName(oldStored));

                try
                {
                    if (System.IO.File.Exists(oldPath))
                    {
                        System.IO.File.Delete(oldPath);
                    }
                }
                catch (IOException)
                {
                }
            }

            string storedName = $"{Guid.NewGuid():N}{extension}";

            await using (var stream = new FileStream(
                             Path.Combine(folder, storedName),
                             FileMode.CreateNew))
            {
                await file.CopyToAsync(stream);
            }

            HttpContext.Session.SetString(nameKey, SafeFileName(Path.GetFileName(file.FileName)));
            HttpContext.Session.SetString(storedKey, storedName);
        }


        // Forgets the wizard's Session data. Saved drafts and their
        // files are kept.
        private void ClearWizard()
        {
            string[] keys =
            {
                GuidelinesKey, CurrentDraftKey,
                ProducerDetailsKey, ProgrammeDetailsKey, ProductionDetailsKey,
                ProposalNameKey, BudgetNameKey, AdditionalNameKey,
                ProposalStoredKey, BudgetStoredKey, AdditionalStoredKey,
                ShowreelKey
            };

            foreach (string key in keys)
            {
                HttpContext.Session.Remove(key);
            }
        }


        private static string? Limit(string? value, int max)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            value = value.Trim();

            return value.Length <= max ? value : value.Substring(0, max);
        }


        private static string SafeFileName(string name)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalid, '_');
            }

            return string.IsNullOrWhiteSpace(name) ? "file" : name;
        }
    }
}