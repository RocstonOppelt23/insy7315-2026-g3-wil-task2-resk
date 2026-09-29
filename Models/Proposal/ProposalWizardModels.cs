using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace RESK.WIL.Models.Proposal
{
    public class ProducerDetailsVm
    {
        [Required]
        public string FirstName { get; set; } = "";

        [Required]
        public string LastName { get; set; } = "";

        [Required, EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        public string MobileNumber { get; set; } = "";

        public string? Organisation { get; set; }

        [Required]
        public string PhysicalAddress { get; set; } = "";

        [Required]
        public string PreferredContactMethod { get; set; } = "Email";
    }

    public class ProgrammeDetailsVm
    {
        [Required]
        public string ProgrammeTitle { get; set; } = "";

        [Required]
        public string Category { get; set; } = "";

        [Required]
        public string ProgrammeFormat { get; set; } = "";

        public string? PrimaryLanguage { get; set; }

        public string? EpisodeDuration { get; set; }

        [Required]
        public string Synopsis { get; set; } = "";

        public string? TargetAudience { get; set; }

        public string? BroadcastFrequency { get; set; }
    }

    public class ProductionDetailsVm
    {
        [Required]
        public int? NumberOfEpisodes { get; set; }

        [Required]
        public string ProposedStartDate { get; set; } = "";

        [Required]
        public string RecordingLocation { get; set; } = "";

        public string? ProductionCompany { get; set; }

        [Required]
        public string ProducerDirector { get; set; } = "";

        public string? CrewContributors { get; set; }

        public string? EquipmentFacilities { get; set; }

        public string? EstimatedBudget { get; set; }

        public string? FundingStatus { get; set; }
    }

    public class AttachmentsVm
    {
        public IFormFile? ProposalDocument { get; set; }
        public IFormFile? BudgetDocument { get; set; }
        public IFormFile? AdditionalFile { get; set; }
        public string? PilotShowreelLink { get; set; }

        public string? ProposalDocumentName { get; set; }
        public string? BudgetDocumentName { get; set; }
        public string? AdditionalFileName { get; set; }
    }

    public class ReviewVm
    {
        public ProducerDetailsVm Producer { get; set; } = new();
        public ProgrammeDetailsVm Programme { get; set; } = new();
        public ProductionDetailsVm Production { get; set; } = new();

        public string? ProposalDocumentName { get; set; }
        public string? BudgetDocumentName { get; set; }
        public string? AdditionalFileName { get; set; }
        public string? PilotShowreelLink { get; set; }

        public bool ConfirmSubmission { get; set; }
    }
}
