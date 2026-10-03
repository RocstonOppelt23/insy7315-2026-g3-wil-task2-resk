namespace RESK.WIL.Models
{
    // =========================================================
    // DRAFTS PAGE
    // =========================================================

    public class ProducerDraftsViewModel
    {
        public string ProducerName { get; set; } = "Producer";

        public List<DraftCardViewModel> Drafts { get; set; } = new();

        public int TotalDrafts => Drafts.Count;

        // Drafts with the attachments step done (80% or more).
        public int NearlyComplete { get; set; }

        // "Today" / "Yesterday" / "4 Aug 2026", or "—" if none.
        public string LastUpdatedDay { get; set; } = "—";

        // "10:36", or empty if none.
        public string LastUpdatedTime { get; set; } = string.Empty;

        // Shown after a draft has been deleted.
        public string? Message { get; set; }
    }


    public class DraftCardViewModel
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Initials { get; set; } = "P";

        public string Category { get; set; } = string.Empty;

        // "today at 10:36" / "4 August 2026"
        public string UpdatedText { get; set; } = string.Empty;

        // Used by the page's sort, in milliseconds.
        public long UpdatedSortKey { get; set; }

        public int ProgressPercent { get; set; }
    }


    // =========================================================
    // PRODUCER DASHBOARD
    // =========================================================

    public class ProducerDashboardViewModel
    {
        public string ProducerName { get; set; } = "Producer";

        // Stat cards
        public int TotalSubmissions { get; set; }

        public int InReview { get; set; }

        public int Approved { get; set; }

        public int Drafts { get; set; }


        // Most recently updated submitted proposal (not a draft).
        public CurrentProposalViewModel? CurrentProposal { get; set; }


        // Latest proposals of any status, newest first.
        public List<ProposalRowViewModel> RecentProposals { get; set; } = new();


        // "Next action" panel
        public string NextActionTitle { get; set; } = string.Empty;

        public string NextActionText { get; set; } = string.Empty;

        public string NextActionButton { get; set; } = string.Empty;

        public string NextActionAction { get; set; } = "Guidelines";

        public int? NextActionProposalId { get; set; }
    }


    public class CurrentProposalViewModel
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string StatusLabel { get; set; } = string.Empty;

        public string StatusCss { get; set; } = string.Empty;

        // "Documentary • 30 minute episode • English"
        public string MetaLine { get; set; } = string.Empty;

        // "Submitted 5 August 2026"
        public string SubmittedText { get; set; } = string.Empty;

        public int WorkflowStage { get; set; }

        public int WorkflowPercent => WorkflowStage * 20;
    }


    public class ProposalRowViewModel
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        // "05 Aug"
        public string UpdatedText { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public string StatusLabel { get; set; } = string.Empty;

        public string StatusCss { get; set; } = string.Empty;
    }
}