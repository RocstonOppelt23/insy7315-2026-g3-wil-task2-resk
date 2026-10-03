using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace RESK.WIL.Models
{
    /*
     * =========================================================
     * PRODUCER PROPOSAL
     * =========================================================
     *
     * One row per proposal a producer starts in the
     * "New proposal" wizard.
     *
     * - While the producer is still filling in the wizard,
     *   Status = "Draft" and the row is saved after every step
     *   (this is what powers the Drafts page).
     *
     * - When the producer submits, Status becomes "InReview".
     *
     * - Reviewers/Admins can later set Status to "Approved",
     *   "ChangesRequested" or "Rejected".
     *
     * This table is linked to the logged-in ASP.NET Identity
     * user (AspNetUsers.Id) through OwnerUserId.
     */
    public class ProducerProposal
    {
        [Key]
        public int Id { get; set; }


        // AspNetUsers.Id of the producer who owns this proposal.
        [Required]
        [StringLength(450)]
        public string OwnerUserId { get; set; } = string.Empty;


        // Set when the proposal is submitted, e.g. CTV-2026-0042
        [StringLength(30)]
        public string? Reference { get; set; }


        // See ProposalStatuses below.
        [Required]
        [StringLength(30)]
        public string Status { get; set; } = ProposalStatuses.Draft;


        // How many wizard steps are finished (0 - 5).
        // 1 = Producer details, 2 = Programme details,
        // 3 = Production details, 4 = Attachments,
        // 5 = Submitted.
        public int CompletedSteps { get; set; }


        // =====================================================
        // SUMMARY FIELDS (copied from the wizard so lists and
        // the dashboard don't have to read the JSON below)
        // =====================================================

        [StringLength(200)]
        public string ProgrammeTitle { get; set; } = string.Empty;

        [StringLength(100)]
        public string Category { get; set; } = string.Empty;

        [StringLength(100)]
        public string? ProgrammeFormat { get; set; }

        [StringLength(50)]
        public string? EpisodeDuration { get; set; }

        [StringLength(100)]
        public string? PrimaryLanguage { get; set; }


        // =====================================================
        // FULL WIZARD ANSWERS (JSON of each step's view model)
        // =====================================================

        public string? ProducerDetailsJson { get; set; }

        public string? ProgrammeDetailsJson { get; set; }

        public string? ProductionDetailsJson { get; set; }


        // =====================================================
        // ATTACHMENTS
        // =====================================================

        // Files are stored in App_Data/ProposalUploads/{OwnerUserId}/
        // under a random "stored" name. The original name is kept
        // only for display.

        [StringLength(500)]
        public string? PilotShowreelLink { get; set; }

        [StringLength(260)]
        public string? ProposalDocumentName { get; set; }

        [StringLength(100)]
        public string? ProposalDocumentStoredName { get; set; }

        [StringLength(260)]
        public string? BudgetDocumentName { get; set; }

        [StringLength(100)]
        public string? BudgetDocumentStoredName { get; set; }

        [StringLength(260)]
        public string? AdditionalFileName { get; set; }

        [StringLength(100)]
        public string? AdditionalFileStoredName { get; set; }


        // =====================================================
        // DATES (always stored in UTC)
        // =====================================================

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime? SubmittedAtUtc { get; set; }


        // =====================================================
        // HELPERS (not stored in the database)
        // =====================================================

        public int ProgressPercent =>
            Math.Clamp(CompletedSteps, 0, 5) * 20;

        public string DisplayTitle =>
            string.IsNullOrWhiteSpace(ProgrammeTitle)
                ? "Untitled proposal"
                : ProgrammeTitle;
    }


    // =========================================================
    // STATUS VALUES
    // =========================================================

    public static class ProposalStatuses
    {
        public const string Draft = "Draft";

        public const string InReview = "InReview";

        public const string Approved = "Approved";

        public const string ChangesRequested = "ChangesRequested";

        public const string Rejected = "Rejected";


        // Text shown to the producer.
        public static string Label(string status) =>
            status switch
            {
                Draft => "Draft",
                InReview => "In review",
                Approved => "Approved",
                ChangesRequested => "Changes",
                Rejected => "Not approved",
                _ => status
            };


        // CSS class used for the coloured status pill.
        public static string CssClass(string status) =>
            status switch
            {
                Draft => "status-draft",
                InReview => "status-review",
                Approved => "status-approved",
                ChangesRequested => "status-changes",
                Rejected => "status-rejected",
                _ => "status-draft"
            };


        // Workflow stages shown on the dashboard:
        // 1 Draft, 2 Submitted, 3 In review, 4 Decision, 5 Complete
        public static int WorkflowStage(string status) =>
            status switch
            {
                Draft => 1,
                InReview => 3,
                ChangesRequested => 3,
                Approved => 5,
                Rejected => 5,
                _ => 1
            };
    }


    // =========================================================
    // SOUTH AFRICAN TIME
    // =========================================================

    /*
     * Dates are saved in UTC. This converts them to South
     * African time (UTC+2) for display, whatever time zone the
     * server itself is set to.
     */
    public static class SouthAfricaTime
    {
        private static readonly TimeZoneInfo Zone = FindZone();

        private static TimeZoneInfo FindZone()
        {
            // Windows id first, then the IANA id (Linux/macOS).
            foreach (string id in new[]
                     {
                         "South Africa Standard Time",
                         "Africa/Johannesburg"
                     })
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(id);
                }
                catch (TimeZoneNotFoundException)
                {
                }
                catch (InvalidTimeZoneException)
                {
                }
            }

            // South Africa has no daylight saving: always UTC+2.
            return TimeZoneInfo.CreateCustomTimeZone(
                "SAST",
                TimeSpan.FromHours(2),
                "South Africa Standard Time",
                "South Africa Standard Time");
        }


        public static DateTime ToLocal(DateTime utc)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(utc, DateTimeKind.Utc),
                Zone);
        }


        // "today at 10:36", "yesterday at 09:15" or "4 August 2026"
        public static string Friendly(DateTime utc)
        {
            DateTime local = ToLocal(utc);
            DateTime today = ToLocal(DateTime.UtcNow).Date;

            if (local.Date == today)
            {
                return "today at " +
                       local.ToString("HH:mm", CultureInfo.InvariantCulture);
            }

            if (local.Date == today.AddDays(-1))
            {
                return "yesterday at " +
                       local.ToString("HH:mm", CultureInfo.InvariantCulture);
            }

            return local.ToString(
                "d MMMM yyyy",
                CultureInfo.InvariantCulture);
        }


        // "Today", "Yesterday" or "4 Aug 2026"
        public static string DayLabel(DateTime utc)
        {
            DateTime local = ToLocal(utc);
            DateTime today = ToLocal(DateTime.UtcNow).Date;

            if (local.Date == today)
            {
                return "Today";
            }

            if (local.Date == today.AddDays(-1))
            {
                return "Yesterday";
            }

            return local.ToString(
                "d MMM yyyy",
                CultureInfo.InvariantCulture);
        }


        // "10:36"
        public static string Time(DateTime utc)
        {
            return ToLocal(utc).ToString(
                "HH:mm",
                CultureInfo.InvariantCulture);
        }


        // "05 Aug"
        public static string ShortDate(DateTime utc)
        {
            return ToLocal(utc).ToString(
                "dd MMM",
                CultureInfo.InvariantCulture);
        }


        // "5 August 2026"
        public static string LongDate(DateTime utc)
        {
            return ToLocal(utc).ToString(
                "d MMMM yyyy",
                CultureInfo.InvariantCulture);
        }
    }
}