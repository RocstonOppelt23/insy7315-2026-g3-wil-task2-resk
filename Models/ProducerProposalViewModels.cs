namespace RESK.WIL.Models
{
    // =========================================================
    // MY PROPOSALS PAGE
    // =========================================================

    public class ProducerMyProposalsViewModel
    {
        public string ProducerName { get; set; } = "Producer";

        // Submitted proposals only (never drafts), newest first.
        public List<MyProposalRowViewModel> Proposals { get; set; } = new();

        // Shown when a proposal could not be opened.
        public string? Message { get; set; }
    }


    public class MyProposalRowViewModel
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        // "Updated 06 Aug"
        public string UpdatedText { get; set; } = string.Empty;

        // e.g. CTV-2026-0042
        public string? Reference { get; set; }

        public string StatusLabel { get; set; } = string.Empty;

        public string StatusCss { get; set; } = string.Empty;
    }


    // =========================================================
    // VIEW ONE PROPOSAL PAGE
    // =========================================================

    public class ProducerProposalDetailsViewModel
    {
        public string ProducerName { get; set; } = "Producer";

        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Reference { get; set; } = "—";

        public string Category { get; set; } = "—";

        // "Documentary • 30 minute episode • English"
        public string MetaLine { get; set; } = string.Empty;

        public string StatusLabel { get; set; } = string.Empty;

        public string StatusCss { get; set; } = string.Empty;

        // 1 Draft, 2 Submitted, 3 In review, 4 Decision, 5 Complete
        public int WorkflowStage { get; set; }

        // "5 August 2026"
        public string SubmittedText { get; set; } = "—";

        // "today at 10:36"
        public string UpdatedText { get; set; } = string.Empty;

        // Only set when the saved link is a real http/https address.
        public string? ShowreelUrl { get; set; }

        public List<ProposalDetailSection> Sections { get; set; } = new();

        public List<ProposalAttachmentLink> Attachments { get; set; } = new();
    }


    public class ProposalDetailSection
    {
        public string Title { get; set; } = string.Empty;

        public List<ProposalDetailField> Fields { get; set; } = new();
    }


    public class ProposalDetailField
    {
        public string Label { get; set; } = string.Empty;

        public string Value { get; set; } = string.Empty;
    }


    public class ProposalAttachmentLink
    {
        // "proposal", "budget" or "additional"
        public string Key { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;
    }
}