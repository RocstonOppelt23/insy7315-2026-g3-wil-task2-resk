using System.ComponentModel.DataAnnotations;

namespace RESK.WIL.Models
{
    public class ProgrammeDetailsViewModel
    {
        [Required(ErrorMessage = "Programme title is required.")]
        [Display(Name = "Programme title")]
        public string ProgrammeTitle { get; set; } = string.Empty;


        [Required(ErrorMessage = "Please select a category.")]
        [Display(Name = "Category")]
        public string Category { get; set; } = string.Empty;


        [Required(
            ErrorMessage = "Please select a programme format."
        )]
        [Display(Name = "Programme format")]
        public string ProgrammeFormat { get; set; } = string.Empty;


        [Required(
            ErrorMessage = "Please select a primary language."
        )]
        [Display(Name = "Primary language")]
        public string PrimaryLanguage { get; set; } = string.Empty;


        [Required(
            ErrorMessage = "Episode duration is required."
        )]
        [Display(Name = "Episode duration")]
        public string EpisodeDuration { get; set; } = string.Empty;


        [Required(
            ErrorMessage = "Programme synopsis is required."
        )]
        [StringLength(
            1000,
            ErrorMessage = "Synopsis cannot exceed 1000 characters."
        )]
        [Display(Name = "Programme synopsis")]
        public string Synopsis { get; set; } = string.Empty;


        [Required(
            ErrorMessage = "Target audience is required."
        )]
        [Display(Name = "Target audience")]
        public string TargetAudience { get; set; } = string.Empty;


        [Required(
            ErrorMessage = "Please select a broadcast frequency."
        )]
        [Display(Name = "Broadcast frequency")]
        public string BroadcastFrequency { get; set; } = string.Empty;
    }
}