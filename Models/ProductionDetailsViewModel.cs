using System.ComponentModel.DataAnnotations;

namespace RESK.WIL.Models
{
    public class ProductionDetailsViewModel
    {
        [Required(ErrorMessage = "Number of episodes is required.")]
        [Range(1, 500, ErrorMessage = "Enter a valid number of episodes.")]
        [Display(Name = "Number of episodes")]
        public int? NumberOfEpisodes { get; set; }


        [Required(ErrorMessage = "Proposed start date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Proposed start date")]
        public DateTime? ProposedStartDate { get; set; }


        [Required(ErrorMessage = "Recording location is required.")]
        [Display(Name = "Recording location")]
        public string RecordingLocation { get; set; } = string.Empty;


        [Display(Name = "Production company / organisation")]
        public string? ProductionCompany { get; set; }


        [Required(ErrorMessage = "Producer / director is required.")]
        [Display(Name = "Producer / director")]
        public string ProducerDirector { get; set; } = string.Empty;


        [Required(ErrorMessage = "Please provide your crew or contributors.")]
        [Display(Name = "Crew / contributors")]
        public string CrewContributors { get; set; } = string.Empty;


        [Required(ErrorMessage = "Please describe the equipment or facilities required.")]
        [Display(Name = "Equipment / facilities")]
        public string EquipmentFacilities { get; set; } = string.Empty;


        [Required(ErrorMessage = "Estimated production budget is required.")]
        [Range(
            0,
            100000000,
            ErrorMessage = "Enter a valid production budget."
        )]
        [Display(Name = "Estimated production budget")]
        public decimal? EstimatedBudget { get; set; }


        [Required(ErrorMessage = "Please select a funding status.")]
        [Display(Name = "Funding status")]
        public string FundingStatus { get; set; } = string.Empty;
    }
}