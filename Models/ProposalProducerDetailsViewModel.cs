using System.ComponentModel.DataAnnotations;

namespace RESK.WIL.Models
{
    public class ProposalProducerDetailsViewModel
    {
        [Required(ErrorMessage = "First name is required.")]
        [Display(Name = "First name")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required.")]
        [Display(Name = "Last name")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [Display(Name = "Email address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mobile number is required.")]
        [Display(Name = "Mobile number")]
        public string MobileNumber { get; set; } = string.Empty;

        [Display(Name = "Organisation")]
        public string? Organisation { get; set; }

        [Required(ErrorMessage = "Physical address is required.")]
        [Display(Name = "Physical address")]
        public string PhysicalAddress { get; set; } = string.Empty;

        [Required(ErrorMessage = "Choose a preferred contact method.")]
        [Display(Name = "Preferred contact method")]
        public string PreferredContactMethod { get; set; } = "Email";
    }
}
