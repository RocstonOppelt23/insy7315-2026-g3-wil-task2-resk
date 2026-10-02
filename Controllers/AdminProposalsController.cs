using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using RESK.WIL.Models;
using RESK.WIL.Services;

namespace RESK.WIL.Controllers
{
    /*
     * =========================================================
     * ADMIN - PROPOSALS
     * =========================================================
     *
     *   GET  /Admin/Proposals                 list, stats, filters
     *   GET  /Admin/Proposals/Export          download the filtered list (CSV)
     *   GET  /Admin/Proposals/{id}            proposal details + workflow
     *   GET  /Admin/Proposals/{id}/Download/{file}   attachment download
     *   GET  /Admin/Proposals/{id}/Assign     choose reviewer + deadline
     *   POST /Admin/Proposals/{id}/Assign
     *   GET  /Admin/Proposals/{id}/Review     comments, recommendation, checklist
     *   POST /Admin/Proposals/{id}/Review     "Save draft" or "Submit review"
     *   GET  /Admin/Proposals/{id}/Confirm    "Confirm proposal decision"
     *   POST /Admin/Proposals/{id}/Confirm    updates the proposal status
     *
     * Workflow:
     *   Submitted -> Needs assignment -> (Assign reviewer) -> In review
     *   -> (Review + Confirm) -> Approved / Changes needed / Not approved
     */
    [Authorize(Roles = "Admin")]
    public class AdminProposalsController : Controller
    {
        private const string ViewFolder = "~/Views/AdminProposals/";

        private static readonly FileExtensionContentTypeProvider ContentTypes = new();

        private readonly RESK.WIL.Data.ApplicationDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IWebHostEnvironment _environment;

        public AdminProposalsController(
            RESK.WIL.Data.ApplicationDbContext db,
            UserManager<IdentityUser> userManager,
            IWebHostEnvironment environment)
        {
            _db = db;
            _userManager = userManager;
            _environment = environment;
        }

        private string Root => _environment.ContentRootPath;


        // =========================================================
        // LIST
        // =========================================================

        [HttpGet("Admin/Proposals", Order = -1)]
        public async Task<IActionResult> Index(string? search, string? status, string? category, string? reviewer)
        {
            await SetAdminInfoAsync();

            List<AdminProposalRow> all = await LoadRowsAsync();
            List<AdminProposalRow> filtered = Filter(all, search, status, category, reviewer);

            DateTime monthStart = MonthStartLocal();

            var model = new AdminProposalsViewModel
            {
                Total = all.Count,
                AwaitingAssignment = all.Count(r => r.StatusKey == "assign"),
                InReview = all.Count(r => r.StatusKey == "review"),
                ApprovedThisMonth = all.Count(r =>
                    r.StatusKey == "approved" &&
                    SouthAfricaTime.ToLocal(r.UpdatedAtUtc) >= monthStart),

                Search = search,
                Status = status,
                Category = category,
                Reviewer = reviewer,

                Categories = all.Select(r => r.Category).Where(c => c != "—").Distinct().OrderBy(c => c).ToList(),
                Reviewers = all.Select(r => r.ReviewerName).Where(n => n != "Unassigned").Distinct().OrderBy(n => n).ToList(),

                Rows = filtered
            };

            return View(ViewFolder + "Index.cshtml", model);
        }


        [HttpGet("Admin/Proposals/Export", Order = -1)]
        public async Task<IActionResult> Export(string? search, string? status, string? category, string? reviewer)
        {
            List<AdminProposalRow> rows = Filter(await LoadRowsAsync(), search, status, category, reviewer);

            var csv = new StringBuilder();
            csv.AppendLine("Reference,Title,Producer,Category,Reviewer,Status,Submitted,Updated");

            foreach (AdminProposalRow row in rows)
            {
                csv.AppendLine(string.Join(",",
                    Csv(row.Reference), Csv(row.Title), Csv(row.Producer), Csv(row.Category),
                    Csv(row.ReviewerName), Csv(row.StatusLabel),
                    Csv(row.SubmittedAtUtc.HasValue ? SouthAfricaTime.ToLocal(row.SubmittedAtUtc.Value).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : ""),
                    Csv(SouthAfricaTime.ToLocal(row.UpdatedAtUtc).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))));
            }

            byte[] bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();

            return File(bytes, "text/csv", $"proposals-{SouthAfricaTime.ToLocal(DateTime.UtcNow):yyyyMMdd}.csv");
        }


        // =========================================================
        // DETAILS
        // =========================================================

        [HttpGet("Admin/Proposals/{id:int}", Order = -1)]
        public async Task<IActionResult> Details(int id)
        {
            ProducerProposal? proposal = await FindSubmittedAsync(id);

            if (proposal == null)
            {
                return NotFoundRedirect();
            }

            await SetAdminInfoAsync();

            RESK.WIL.Services.ProposalReview review = ProposalReviewStore.Load(Root, id);
            AdminProposalRow row = await BuildRowAsync(proposal, review);

            var model = new AdminProposalDetailsViewModel
            {
                Row = row,
                Review = review,
                Format = proposal.ProgrammeFormat ?? "—",
                Summary = JsonFields(proposal.ProgrammeDetailsJson, "ProgrammeTitle", "Category")
                    .Concat(JsonFields(proposal.ProductionDetailsJson))
                    .ToList(),
                ProducerFields = JsonFields(proposal.ProducerDetailsJson),
                ShowreelUrl = SafeWebLink(proposal.PilotShowreelLink)
            };

            AddAttachment(model, "proposal", "Proposal document", proposal.ProposalDocumentName);
            AddAttachment(model, "budget", "Budget document", proposal.BudgetDocumentName);
            AddAttachment(model, "additional", "Additional file", proposal.AdditionalFileName);

            // Workflow steps
            bool decided = row.StatusKey is "approved" or "changes" or "rejected";

            model.Steps.Add(new AdminWorkflowStep
            {
                Title = "Submitted",
                Detail = proposal.SubmittedAtUtc.HasValue ? SouthAfricaTime.ToLocal(proposal.SubmittedAtUtc.Value).ToString("dd MMM yyyy, HH:mm", CultureInfo.InvariantCulture) : "",
                Reached = true
            });

            model.Steps.Add(new AdminWorkflowStep
            {
                Title = "Initial review",
                Detail = review.HasReviewer
                    ? (decided ? "Completed by " : "With ") + review.ReviewerName
                    : "Waiting for a reviewer",
                Reached = review.HasReviewer || decided
            });

            model.Steps.Add(new AdminWorkflowStep
            {
                Title = "Management decision",
                Detail = decided && review.DecidedAtUtc.HasValue
                    ? SouthAfricaTime.ToLocal(review.DecidedAtUtc.Value).ToString("dd MMM yyyy, HH:mm", CultureInfo.InvariantCulture)
                    : review.ReviewSubmitted ? "Recommendation ready" : "Not started",
                Reached = decided
            });

            model.Steps.Add(new AdminWorkflowStep
            {
                Title = "Final outcome",
                Detail = decided ? row.StatusLabel : "Not started",
                Reached = decided
            });

            return View(ViewFolder + "Details.cshtml", model);
        }


        [HttpGet("Admin/Proposals/{id:int}/Download/{file}", Order = -1)]
        public async Task<IActionResult> Download(int id, string file)
        {
            ProducerProposal? proposal = await FindSubmittedAsync(id);

            if (proposal == null)
            {
                return NotFound();
            }

            (string? name, string? stored) = file switch
            {
                "proposal" => (proposal.ProposalDocumentName, proposal.ProposalDocumentStoredName),
                "budget" => (proposal.BudgetDocumentName, proposal.BudgetDocumentStoredName),
                "additional" => (proposal.AdditionalFileName, proposal.AdditionalFileStoredName),
                _ => (null, null)
            };

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(stored))
            {
                return NotFound();
            }

            string path = Path.Combine(
                Root, "App_Data", "ProposalUploads",
                SafeFileName(proposal.OwnerUserId),
                Path.GetFileName(stored));

            if (!System.IO.File.Exists(path))
            {
                return NotFound();
            }

            if (!ContentTypes.TryGetContentType(name, out string? contentType))
            {
                contentType = "application/octet-stream";
            }

            return PhysicalFile(path, contentType, name);
        }


        // =========================================================
        // ASSIGN REVIEWER
        // =========================================================

        [HttpGet("Admin/Proposals/{id:int}/Assign", Order = -1)]
        public async Task<IActionResult> Assign(int id)
        {
            ProducerProposal? proposal = await FindSubmittedAsync(id);

            if (proposal == null)
            {
                return NotFoundRedirect();
            }

            if (proposal.Status != ProposalStatuses.InReview)
            {
                TempData["AdminMessage"] = "A reviewer can only be assigned while the proposal is in review.";
                return Redirect($"/Admin/Proposals/{id}");
            }

            await SetAdminInfoAsync();

            RESK.WIL.Services.ProposalReview review = ProposalReviewStore.Load(Root, id);

            var model = new AdminAssignViewModel
            {
                Row = await BuildRowAsync(proposal, review),
                Reviewers = await LoadReviewersAsync(),
                SelectedReviewerId = review.ReviewerUserId,
                Deadline = (review.Deadline ?? ReskSettingsStore.ReviewDeadline(Root)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Note = review.AssignmentNote
            };

            return View(ViewFolder + "Assign.cshtml", model);
        }


        [HttpPost("Admin/Proposals/{id:int}/Assign", Order = -1)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(int id, string? reviewerId, string? deadline, string? note)
        {
            ProducerProposal? proposal = await FindSubmittedAsync(id);

            if (proposal == null)
            {
                return NotFoundRedirect();
            }

            List<AdminReviewerOption> reviewers = await LoadReviewersAsync();
            AdminReviewerOption? chosen = reviewers.FirstOrDefault(r => r.UserId == reviewerId);

            if (chosen == null)
            {
                TempData["AdminMessage"] = "Please choose a reviewer.";
                return Redirect($"/Admin/Proposals/{id}/Assign");
            }

            RESK.WIL.Services.ProposalReview review = ProposalReviewStore.Load(Root, id);

            review.ReviewerUserId = chosen.UserId;
            review.ReviewerName = chosen.Name;
            review.AssignedAtUtc = DateTime.UtcNow;
            review.AssignedBy = User.Identity?.Name;
            review.AssignmentNote = Limit(note, 1000);
            review.Deadline =
                DateTime.TryParseExact(deadline, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime d)
                    ? d.Date
                    : null;

            ProposalReviewStore.Save(Root, review);

            proposal.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            TempData["AdminMessage"] = $"{chosen.Name} was assigned to review \"{proposal.DisplayTitle}\".";

            return Redirect($"/Admin/Proposals/{id}");
        }


        // =========================================================
        // REVIEW
        // =========================================================

        [HttpGet("Admin/Proposals/{id:int}/Review", Order = -1)]
        public async Task<IActionResult> Review(int id)
        {
            ProducerProposal? proposal = await FindSubmittedAsync(id);

            if (proposal == null)
            {
                return NotFoundRedirect();
            }

            if (proposal.Status != ProposalStatuses.InReview)
            {
                TempData["AdminMessage"] = "A decision has already been made on this proposal.";
                return Redirect($"/Admin/Proposals/{id}");
            }

            await SetAdminInfoAsync();

            RESK.WIL.Services.ProposalReview review = ProposalReviewStore.Load(Root, id);

            return View(ViewFolder + "Review.cshtml", new AdminReviewViewModel
            {
                Row = await BuildRowAsync(proposal, review),
                Review = review
            });
        }


        [HttpPost("Admin/Proposals/{id:int}/Review", Order = -1)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Review(
            int id,
            string? submitAction,
            string? recommendation,
            string? comments,
            bool purposeIsClear,
            bool audienceIsSuitable,
            bool planIsRealistic,
            bool budgetIsComplete)
        {
            ProducerProposal? proposal = await FindSubmittedAsync(id);

            if (proposal == null)
            {
                return NotFoundRedirect();
            }

            if (proposal.Status != ProposalStatuses.InReview)
            {
                TempData["AdminMessage"] = "A decision has already been made on this proposal.";
                return Redirect($"/Admin/Proposals/{id}");
            }

            RESK.WIL.Services.ProposalReview review = ProposalReviewStore.Load(Root, id);

            review.Recommendation =
                recommendation is ProposalReviewStore.Approve or ProposalReviewStore.RequestChanges or ProposalReviewStore.Reject
                    ? recommendation
                    : null;
            review.Comments = Limit(comments, 4000);
            review.PurposeIsClear = purposeIsClear;
            review.AudienceIsSuitable = audienceIsSuitable;
            review.PlanIsRealistic = planIsRealistic;
            review.BudgetIsComplete = budgetIsComplete;
            review.ReviewSavedAtUtc = DateTime.UtcNow;

            bool submitting = submitAction == "submit";

            if (submitting)
            {
                if (review.Recommendation == null || ReskSettingsStore.CommentsMissing(Root, review.Recommendation, review.Comments))
                {
                    review.ReviewSubmitted = false;
                    ProposalReviewStore.Save(Root, review);

                    TempData["AdminError"] = review.Recommendation == null ? "Choose a recommendation before submitting." : "Write a reason in the comments before rejecting or requesting changes.";
                    return Redirect($"/Admin/Proposals/{id}/Review");
                }

                review.ReviewSubmitted = true;
                ProposalReviewStore.Save(Root, review);

                return Redirect($"/Admin/Proposals/{id}/Confirm");
            }

            review.ReviewSubmitted = false;
            ProposalReviewStore.Save(Root, review);

            TempData["AdminMessage"] = "Your review draft was saved.";
            return Redirect($"/Admin/Proposals/{id}/Review");
        }


        // =========================================================
        // CONFIRM DECISION
        // =========================================================

        [HttpGet("Admin/Proposals/{id:int}/Confirm", Order = -1)]
        public async Task<IActionResult> Confirm(int id)
        {
            ProducerProposal? proposal = await FindSubmittedAsync(id);

            if (proposal == null)
            {
                return NotFoundRedirect();
            }

            RESK.WIL.Services.ProposalReview review = ProposalReviewStore.Load(Root, id);

            if (proposal.Status != ProposalStatuses.InReview || !review.ReviewSubmitted || review.Recommendation == null)
            {
                return Redirect($"/Admin/Proposals/{id}/Review");
            }

            await SetAdminInfoAsync();

            return View(ViewFolder + "Confirm.cshtml", new AdminReviewViewModel
            {
                Row = await BuildRowAsync(proposal, review),
                Review = review
            });
        }


        [HttpPost("Admin/Proposals/{id:int}/Confirm", Order = -1)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmDecision(int id)
        {
            ProducerProposal? proposal = await FindSubmittedAsync(id);

            if (proposal == null)
            {
                return NotFoundRedirect();
            }

            RESK.WIL.Services.ProposalReview review = ProposalReviewStore.Load(Root, id);

            if (proposal.Status != ProposalStatuses.InReview || !review.ReviewSubmitted || review.Recommendation == null)
            {
                return Redirect($"/Admin/Proposals/{id}/Review");
            }

            DateTime now = DateTime.UtcNow;

            proposal.Status = review.Recommendation switch
            {
                ProposalReviewStore.Approve => ProposalStatuses.Approved,
                ProposalReviewStore.Reject => ProposalStatuses.Rejected,
                _ => ProposalStatuses.ChangesRequested
            };
            proposal.UpdatedAtUtc = now;

            await _db.SaveChangesAsync();

            review.Decision = review.Recommendation;
            review.DecidedAtUtc = now;
            review.DecidedBy = User.Identity?.Name;
            review.ReviewSubmitted = false;

            // A reviewer's name is needed for the producer's status page.
            if (!review.HasReviewer)
            {
                IdentityUser? me = await _userManager.GetUserAsync(User);
                review.ReviewerUserId = me?.Id;
                review.ReviewerName = await DisplayNameAsync(me);
                review.AssignedAtUtc ??= now;
            }

            ProposalReviewStore.Save(Root, review);

            TempData["AdminMessage"] =
                $"Decision saved: \"{proposal.DisplayTitle}\" is now {StatusLabel(proposal.Status, true)}.";

            return Redirect($"/Admin/Proposals/{id}");
        }


        // =========================================================
        // HELPERS - DATA
        // =========================================================

        private async Task<ProducerProposal?> FindSubmittedAsync(int id)
        {
            return await _db.ProducerProposals
                .FirstOrDefaultAsync(p => p.Id == id && p.Status != ProposalStatuses.Draft);
        }


        private IActionResult NotFoundRedirect()
        {
            TempData["AdminMessage"] = "That proposal could not be found.";
            return Redirect("/Admin/Proposals");
        }


        private async Task<List<AdminProposalRow>> LoadRowsAsync()
        {
            List<ProducerProposal> proposals =
                await _db.ProducerProposals
                    .AsNoTracking()
                    .Where(p => p.Status != ProposalStatuses.Draft)
                    .OrderByDescending(p => p.UpdatedAtUtc)
                    .ToListAsync();

            Dictionary<string, IdentityUser> users =
                (await _db.Set<IdentityUser>().AsNoTracking().ToListAsync()).ToDictionary(u => u.Id);

            Dictionary<int, RESK.WIL.Services.ProposalReview> reviews = ProposalReviewStore.LoadAll(Root);

            return proposals
                .Select(p => MakeRow(p, reviews.TryGetValue(p.Id, out RESK.WIL.Services.ProposalReview? r) ? r : new RESK.WIL.Services.ProposalReview { ProposalId = p.Id }, users))
                .ToList();
        }


        private async Task<AdminProposalRow> BuildRowAsync(ProducerProposal proposal, RESK.WIL.Services.ProposalReview review)
        {
            Dictionary<string, IdentityUser> users = new();
            IdentityUser? owner = await _db.Set<IdentityUser>().AsNoTracking().FirstOrDefaultAsync(u => u.Id == proposal.OwnerUserId);

            if (owner != null)
            {
                users[owner.Id] = owner;
            }

            return MakeRow(proposal, review, users);
        }


        private AdminProposalRow MakeRow(ProducerProposal p, RESK.WIL.Services.ProposalReview review, Dictionary<string, IdentityUser> users)
        {
            users.TryGetValue(p.OwnerUserId, out IdentityUser? owner);

            string key = StatusKey(p.Status, review);

            return new AdminProposalRow
            {
                Id = p.Id,
                Title = p.DisplayTitle,
                Reference = string.IsNullOrWhiteSpace(p.Reference) ? $"#{p.Id}" : p.Reference,
                Producer = ProducerProfileStore.DisplayName(ProducerProfileStore.Load(Root, p.OwnerUserId), owner),
                Category = string.IsNullOrWhiteSpace(p.Category) ? "—" : p.Category,
                ReviewerName = review.HasReviewer ? review.ReviewerName ?? "Reviewer" : "Unassigned",
                StatusKey = key,
                StatusLabel = StatusLabel(p.Status, review.HasReviewer),
                StatusCss = "pill-" + key,
                SubmittedAtUtc = p.SubmittedAtUtc,
                UpdatedAtUtc = p.UpdatedAtUtc,
                UpdatedText = SouthAfricaTime.ShortDate(p.UpdatedAtUtc)
            };
        }


        private static List<AdminProposalRow> Filter(List<AdminProposalRow> rows, string? search, string? status, string? category, string? reviewer)
        {
            IEnumerable<AdminProposalRow> query = rows;

            if (!string.IsNullOrWhiteSpace(search))
            {
                string term = search.Trim();
                query = query.Where(r =>
                    r.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    r.Producer.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    r.Reference.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(r => r.StatusKey == status);
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(r => r.Category == category);
            }

            if (!string.IsNullOrWhiteSpace(reviewer))
            {
                query = query.Where(r => r.ReviewerName == reviewer);
            }

            return query.ToList();
        }


        // Users with the Reviewer or Admin role who can review proposals.
        private async Task<List<AdminReviewerOption>> LoadReviewersAsync()
        {
            var result = new Dictionary<string, AdminReviewerOption>();

            foreach ((string role, string label) in new[] { ("Reviewer", "Reviewer"), ("Admin", "Proposal Manager") })
            {
                foreach (IdentityUser user in await _userManager.GetUsersInRoleAsync(role))
                {
                    if (!result.ContainsKey(user.Id))
                    {
                        string name = await DisplayNameAsync(user);

                        result[user.Id] = new AdminReviewerOption
                        {
                            UserId = user.Id,
                            Name = name,
                            Initials = ProducerProfileStore.Initials(name),
                            RoleLabel = label
                        };
                    }
                }
            }

            // Active reviews = proposals still in review assigned to that person.
            List<int> inReviewIds =
                await _db.ProducerProposals
                    .AsNoTracking()
                    .Where(p => p.Status == ProposalStatuses.InReview)
                    .Select(p => p.Id)
                    .ToListAsync();

            Dictionary<int, RESK.WIL.Services.ProposalReview> reviews = ProposalReviewStore.LoadAll(Root);

            foreach (int pid in inReviewIds)
            {
                if (reviews.TryGetValue(pid, out RESK.WIL.Services.ProposalReview? r) &&
                    r.ReviewerUserId != null &&
                    result.TryGetValue(r.ReviewerUserId, out AdminReviewerOption? option))
                {
                    option.ActiveReviews++;
                }
            }

            return result.Values.OrderBy(r => r.Name).ToList();
        }


        private Task<string> DisplayNameAsync(IdentityUser? user)
        {
            if (user == null)
            {
                return Task.FromResult("Admin User");
            }

            return Task.FromResult(
                ProducerProfileStore.DisplayName(ProducerProfileStore.Load(Root, user.Id), user));
        }


        private async Task SetAdminInfoAsync()
        {
            IdentityUser? me = await _userManager.GetUserAsync(User);
            ProducerProfile profile = me == null ? new ProducerProfile() : ProducerProfileStore.Load(Root, me.Id);

            bool hasName = !string.IsNullOrWhiteSpace(profile.FullName);

            ViewData["AdminName"] = hasName ? profile.FullName.Trim() : "Admin User";
            ViewData["AdminInitials"] = hasName ? ProducerProfileStore.Initials(profile.FullName) : "AD";
        }


        // =========================================================
        // HELPERS - TEXT
        // =========================================================

        // assign, review, approved, changes, rejected
        private static string StatusKey(string status, RESK.WIL.Services.ProposalReview review)
        {
            return status switch
            {
                ProposalStatuses.InReview => review.HasReviewer ? "review" : "assign",
                ProposalStatuses.Approved => "approved",
                ProposalStatuses.ChangesRequested => "changes",
                ProposalStatuses.Rejected => "rejected",
                _ => "submitted"
            };
        }


        private static string StatusLabel(string status, bool hasReviewer)
        {
            return status switch
            {
                ProposalStatuses.InReview => hasReviewer ? "In review" : "Needs assignment",
                ProposalStatuses.Approved => "Approved",
                ProposalStatuses.ChangesRequested => "Changes needed",
                ProposalStatuses.Rejected => "Not approved",
                _ => "Submitted"
            };
        }


        private static DateTime MonthStartLocal()
        {
            DateTime local = SouthAfricaTime.ToLocal(DateTime.UtcNow);
            return new DateTime(local.Year, local.Month, 1);
        }


        // Turns a wizard step's saved JSON into "Label: value" rows.
        private static List<AdminField> JsonFields(string? json, params string[] skip)
        {
            var fields = new List<AdminField>();

            if (string.IsNullOrWhiteSpace(json))
            {
                return fields;
            }

            try
            {
                using JsonDocument doc = JsonDocument.Parse(json);

                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return fields;
                }

                foreach (JsonProperty property in doc.RootElement.EnumerateObject())
                {
                    if (skip.Contains(property.Name))
                    {
                        continue;
                    }

                    string value = FormatValue(property.Value);

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        fields.Add(new AdminField { Label = Humanize(property.Name), Value = value });
                    }
                }
            }
            catch (JsonException)
            {
            }

            return fields;
        }


        private static string FormatValue(JsonElement value)
        {
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? "",
                JsonValueKind.Number => value.ToString(),
                JsonValueKind.True => "Yes",
                JsonValueKind.False => "No",
                JsonValueKind.Array => string.Join(", ", value.EnumerateArray().Select(FormatValue).Where(v => v.Length > 0)),
                _ => ""
            };
        }


        // "TargetAudience" -> "Target audience"
        private static string Humanize(string name)
        {
            string spaced = Regex.Replace(name, "(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", " ");
            return spaced.Length == 0 ? name : char.ToUpper(spaced[0]) + spaced.Substring(1).ToLowerInvariant();
        }


        private static void AddAttachment(AdminProposalDetailsViewModel model, string key, string label, string? fileName)
        {
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                model.Attachments.Add(new AdminAttachment { Key = key, Label = label, FileName = fileName });
            }
        }


        private static string? SafeWebLink(string? link)
        {
            if (Uri.TryCreate(link?.Trim(), UriKind.Absolute, out Uri? uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                return uri.ToString();
            }

            return null;
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


        private static string Csv(string? value)
        {
            value ??= "";
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}


namespace RESK.WIL.Models
{
    public class AdminProposalRow
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Reference { get; set; } = "";
        public string Producer { get; set; } = "";
        public string Category { get; set; } = "";
        public string ReviewerName { get; set; } = "";
        public string StatusKey { get; set; } = "";
        public string StatusLabel { get; set; } = "";
        public string StatusCss { get; set; } = "";
        public DateTime? SubmittedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public string UpdatedText { get; set; } = "";
    }

    public class AdminProposalsViewModel
    {
        public int Total { get; set; }
        public int AwaitingAssignment { get; set; }
        public int InReview { get; set; }
        public int ApprovedThisMonth { get; set; }
        public string? Search { get; set; }
        public string? Status { get; set; }
        public string? Category { get; set; }
        public string? Reviewer { get; set; }
        public List<string> Categories { get; set; } = new();
        public List<string> Reviewers { get; set; } = new();
        public List<AdminProposalRow> Rows { get; set; } = new();
    }

    public class AdminField
    {
        public string Label { get; set; } = "";
        public string Value { get; set; } = "";
    }

    public class AdminAttachment
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public string FileName { get; set; } = "";
    }

    public class AdminWorkflowStep
    {
        public string Title { get; set; } = "";
        public string Detail { get; set; } = "";
        public bool Reached { get; set; }
    }

    public class AdminProposalDetailsViewModel
    {
        public AdminProposalRow Row { get; set; } = new();
        public RESK.WIL.Services.ProposalReview Review { get; set; } = new();
        public string Format { get; set; } = "—";
        public List<AdminField> Summary { get; set; } = new();
        public List<AdminField> ProducerFields { get; set; } = new();
        public List<AdminAttachment> Attachments { get; set; } = new();
        public List<AdminWorkflowStep> Steps { get; set; } = new();
        public string? ShowreelUrl { get; set; }
    }

    public class AdminReviewerOption
    {
        public string UserId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Initials { get; set; } = "";
        public string RoleLabel { get; set; } = "";
        public int ActiveReviews { get; set; }
    }

    public class AdminAssignViewModel
    {
        public AdminProposalRow Row { get; set; } = new();
        public List<AdminReviewerOption> Reviewers { get; set; } = new();
        public string? SelectedReviewerId { get; set; }
        public string Deadline { get; set; } = "";
        public string? Note { get; set; }
    }

    public class AdminReviewViewModel
    {
        public AdminProposalRow Row { get; set; } = new();
        public RESK.WIL.Services.ProposalReview Review { get; set; } = new();
    }
}