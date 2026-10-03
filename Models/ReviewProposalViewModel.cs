namespace RESK.WIL.Models
{
    public class ReviewProposalViewModel
    {
        public ProducerDetailsViewModel ProducerDetails { get; set; }
            = new ProducerDetailsViewModel();

        public ProgrammeDetailsViewModel ProgrammeDetails { get; set; }
            = new ProgrammeDetailsViewModel();

        public ProductionDetailsViewModel ProductionDetails { get; set; }
            = new ProductionDetailsViewModel();

        public string? ProposalDocumentName { get; set; }

        public string? BudgetDocumentName { get; set; }

        public string? AdditionalFileName { get; set; }

        public string? PilotShowreelLink { get; set; }

        public bool ConfirmSubmission { get; set; }
    }
}