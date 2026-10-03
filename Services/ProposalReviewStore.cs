using System.Text.Json;
using System.Text.Json.Serialization;

namespace RESK.WIL.Services
{
    /*
     * =========================================================
     * PROPOSAL REVIEW STORE
     * =========================================================
     *
     * Reviewer assignment and review details for each proposal,
     * saved as JSON in App_Data (no database change needed):
     *
     *   App_Data/Reviews/{proposalId}.json
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
        private static readonly JsonSerializerOptions JsonOptions =
            new JsonSerializerOptions { WriteIndented = true };

        private static readonly object FileLock = new object();

        public const string Approve = "Approve";
        public const string RequestChanges = "RequestChanges";
        public const string Reject = "Reject";


        public static ProposalReview Load(string contentRoot, int proposalId)
        {
            string path = FilePath(contentRoot, proposalId);

            try
            {
                if (File.Exists(path))
                {
                    ProposalReview? review =
                        JsonSerializer.Deserialize<ProposalReview>(File.ReadAllText(path));

                    if (review != null)
                    {
                        review.ProposalId = proposalId;
                        return review;
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (JsonException)
            {
            }

            return new ProposalReview { ProposalId = proposalId };
        }


        public static Dictionary<int, ProposalReview> LoadAll(string contentRoot)
        {
            var result = new Dictionary<int, ProposalReview>();
            string folder = Folder(contentRoot);

            if (!Directory.Exists(folder))
            {
                return result;
            }

            foreach (string file in Directory.GetFiles(folder, "*.json"))
            {
                if (int.TryParse(Path.GetFileNameWithoutExtension(file), out int id))
                {
                    result[id] = Load(contentRoot, id);
                }
            }

            return result;
        }


        public static void Save(string contentRoot, ProposalReview review)
        {
            Directory.CreateDirectory(Folder(contentRoot));

            lock (FileLock)
            {
                File.WriteAllText(
                    FilePath(contentRoot, review.ProposalId),
                    JsonSerializer.Serialize(review, JsonOptions));
            }
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


        private static string Folder(string contentRoot)
        {
            return Path.Combine(contentRoot, "App_Data", "Reviews");
        }


        private static string FilePath(string contentRoot, int proposalId)
        {
            return Path.Combine(Folder(contentRoot), proposalId + ".json");
        }
    }
}