using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RESK.WIL.Models;
using RESK.WIL.Services;

namespace RESK.WIL.Controllers
{
    public partial class AdminReportsController
    {
        // =========================================================
        // DATA
        // =========================================================

        private async Task<List<ReportRow>> LoadRowsAsync()
        {
            List<ProducerProposal> proposals = await _db.ProducerProposals
                .AsNoTracking()
                .Where(p => p.Status != ProposalStatuses.Draft)
                .ToListAsync();

            Dictionary<string, IdentityUser> users =
                (await _db.Set<IdentityUser>().AsNoTracking().ToListAsync()).ToDictionary(u => u.Id);

            Dictionary<int, RESK.WIL.Services.ProposalReview> reviews = ProposalReviewStore.LoadAll(Root);
            var names = new Dictionary<string, string>();
            var rows = new List<ReportRow>();

            foreach (ProducerProposal p in proposals)
            {
                users.TryGetValue(p.OwnerUserId, out IdentityUser? owner);

                if (!names.TryGetValue(p.OwnerUserId, out string? producer))
                {
                    producer = ProducerProfileStore.DisplayName(ProducerProfileStore.Load(Root, p.OwnerUserId), owner);
                    names[p.OwnerUserId] = producer;
                }

                RESK.WIL.Services.ProposalReview review = reviews.TryGetValue(p.Id, out RESK.WIL.Services.ProposalReview? r) ? r : new RESK.WIL.Services.ProposalReview { ProposalId = p.Id };

                bool decided = p.Status is ProposalStatuses.Approved or ProposalStatuses.Rejected or ProposalStatuses.ChangesRequested;
                DateTime submittedUtc = p.SubmittedAtUtc ?? p.CreatedAtUtc;
                DateTime? decisionUtc = decided ? review.DecidedAtUtc ?? p.UpdatedAtUtc : null;

                string reviewer =
                    review.HasReviewer ? review.ReviewerName ?? "Reviewer" :
                    
                    "Unassigned";

                string key = p.Status switch
                {
                    ProposalStatuses.InReview => review.HasReviewer ? "review" : "assign",
                    ProposalStatuses.Approved => "approved",
                    ProposalStatuses.ChangesRequested => "changes",
                    ProposalStatuses.Rejected => "rejected",
                    _ => "review"
                };

                rows.Add(new ReportRow
                {
                    Id = p.Id,
                    Reference = string.IsNullOrWhiteSpace(p.Reference) ? $"#{p.Id}" : p.Reference!,
                    Title = p.DisplayTitle,
                    ProducerId = p.OwnerUserId,
                    Producer = producer,
                    ProducerEmail = owner?.Email ?? "",
                    Category = string.IsNullOrWhiteSpace(p.Category) ? "—" : p.Category.Trim(),
                    Format = p.ProgrammeFormat ?? "",
                    Duration = p.EpisodeDuration ?? "",
                    Language = p.PrimaryLanguage ?? "",
                    StatusKey = key,
                    StatusLabel = key switch
                    {
                        "assign" => "Needs assignment",
                        "review" => "In review",
                        "changes" => "Changes needed",
                        "approved" => "Approved",
                        "rejected" => "Not approved",
                        _ => key
                    },
                    Reviewer = reviewer,
                    HasReviewer = review.HasReviewer,
                    DeadlineLocal = review.Deadline,
                    SubmittedUtc = submittedUtc,
                    SubmittedLocal = SouthAfricaTime.ToLocal(submittedUtc),
                    DecisionUtc = decisionUtc,
                    DecidedBy = review.DecidedBy ?? (decided ? reviewer : ""),
                    UpdatedUtc = p.UpdatedAtUtc,
                    ReviewDays = decisionUtc.HasValue && decisionUtc.Value > submittedUtc
                        ? (decisionUtc.Value - submittedUtc).TotalDays
                        : null
                });
            }

            return rows;
        }


        private static List<ReportRow> ApplyFilters(List<ReportRow> all, ReskExportForm form, ReportPeriod period)
        {
            IEnumerable<ReportRow> query = all.Where(r => period.Contains(r.SubmittedLocal));

            switch (form.Status)
            {
                case "open":
                    query = query.Where(r => r.StatusKey is "assign" or "review");
                    break;
                case "decided":
                    query = query.Where(r => r.StatusKey is "approved" or "rejected");
                    break;
                case "assign":
                case "review":
                case "changes":
                case "approved":
                case "rejected":
                    query = query.Where(r => r.StatusKey == form.Status);
                    break;
            }

            if (!string.IsNullOrWhiteSpace(form.Category))
            {
                query = query.Where(r => ReskCategoryStore.SameName(r.Category, form.Category));
            }

            if (!string.IsNullOrWhiteSpace(form.Producer))
            {
                query = query.Where(r => r.ProducerId == form.Producer);
            }

            return query.OrderBy(r => r.SubmittedLocal).ThenBy(r => r.Id).ToList();
        }


        private static List<(string Key, string Label, bool Default, bool More)> SelectedColumns(ReskExportForm form)
        {
            var chosen = new HashSet<string>(form.Columns ?? new List<string>());
            return ExportColumns.Where(c => chosen.Contains(c.Key)).ToList();
        }


        private static string Cell(ReportRow r, string key, string? dateFormat)
        {
            string format = dateFormat == "iso" ? "yyyy-MM-dd" : "dd MMM yyyy";

            return key switch
            {
                "reference" => r.Reference,
                "title" => r.Title,
                "producer" => r.Producer,
                "email" => r.ProducerEmail,
                "category" => r.Category,
                "format" => r.Format,
                "duration" => r.Duration,
                "language" => r.Language,
                "submitted" => r.SubmittedLocal.ToString(format, CultureInfo.InvariantCulture),
                "status" => r.StatusLabel,
                "reviewer" => r.Reviewer,
                "deadline" => r.DeadlineLocal.HasValue ? r.DeadlineLocal.Value.ToString(format, CultureInfo.InvariantCulture) : "",
                "decision" => r.DecisionUtc.HasValue ? SouthAfricaTime.ToLocal(r.DecisionUtc.Value).ToString(format, CultureInfo.InvariantCulture) : "",
                "decidedBy" => r.DecidedBy,
                "reviewDays" => r.ReviewDays.HasValue ? r.ReviewDays.Value.ToString("0.0", CultureInfo.InvariantCulture) : "",
                "updated" => SouthAfricaTime.ToLocal(r.UpdatedUtc).ToString(format, CultureInfo.InvariantCulture),
                _ => ""
            };
        }


        // Quotes a CSV value. Values starting with = + - @ are prefixed with '
        // so a spreadsheet never runs them as a formula.
        private static string Csv(string? value)
        {
            value ??= "";

            if (value.Length > 0 && "=+-@\t\r".IndexOf(value[0]) >= 0)
            {
                value = "'" + value;
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}