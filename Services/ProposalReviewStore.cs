using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using RESK.WIL.Data;

namespace RESK.WIL.Services
{
    /*
     * =========================================================
     * PROPOSAL REVIEW STORE
     * =========================================================
     *
     * Reviewer assignment and review details for each proposal,
     * saved in the ReskProposalReviews table (one row per proposal).
     *
     * The proposal's status itself (In review, Approved, Changes
     * needed, Not approved) stays on the ProducerProposals row, so
     * producers see the decision on their My proposals page.
     */
    public class ProposalReview
    {
        public int ProposalId { get; set; }

        // ---------- Assignment ----------
        public string? ReviewerUserId { get; set; }

        public string? ReviewerName { get; set; }

        public DateTime? AssignedAtUtc { get; set; }

        public DateTime? Deadline { get; set; }

        public string? AssignmentNote { get; set; }

        public string? AssignedBy { get; set; }

        // ---------- Review ----------
        // "Approve", "RequestChanges" or "Reject"
        public string? Recommendation { get; set; }

        public string? Comments { get; set; }

        public bool PurposeIsClear { get; set; }

        public bool AudienceIsSuitable { get; set; }

        public bool PlanIsRealistic { get; set; }

        public bool BudgetIsComplete { get; set; }

        public DateTime? ReviewSavedAtUtc { get; set; }

        // True once "Submit review" was clicked (waiting for confirmation).
        public bool ReviewSubmitted { get; set; }

        // ---------- Decision ----------
        public string? Decision { get; set; }

        public DateTime? DecidedAtUtc { get; set; }

        public string? DecidedBy { get; set; }

        [JsonIgnore]
        public bool HasReviewer => !string.IsNullOrWhiteSpace(ReviewerUserId);
    }


    public static class ProposalReviewStore
    {
        public const string Approve = "Approve";
        public const string RequestChanges = "RequestChanges";
        public const string Reject = "Reject";


        // contentRoot is kept so existing callers don't change; the data now lives in the database.
        public static ProposalReview Load(string contentRoot, int proposalId)
        {
            using ApplicationDbContext db = ReskDb.Open();

            return db.ReskProposalReviews
                       .AsNoTracking()
                       .FirstOrDefault(r => r.ProposalId == proposalId)
                   ?? new ProposalReview { ProposalId = proposalId };
        }


        public static Dictionary<int, ProposalReview> LoadAll(string contentRoot)
        {
            using ApplicationDbContext db = ReskDb.Open();

            return db.ReskProposalReviews
                .AsNoTracking()
                .ToDictionary(r => r.ProposalId);
        }


        // Insert or update the review row for this proposal.
        public static void Save(string contentRoot, ProposalReview review)
        {
            using ApplicationDbContext db = ReskDb.Open();

            ProposalReview? existing = db.ReskProposalReviews.Find(review.ProposalId);

            if (existing == null)
            {
                db.ReskProposalReviews.Add(review);
            }
            else
            {
                db.Entry(existing).CurrentValues.SetValues(review);
            }

            db.SaveChanges();
        }


        // "Approve" -> "Approve", "RequestChanges" -> "Request changes"
        public static string RecommendationText(string? recommendation)
        {
            return recommendation switch
            {
                Approve => "Approve",
                RequestChanges => "Request changes",
                Reject => "Reject",
                _ => "—"
            };
        }
    }
}
