using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace RESK.WIL.Models
{
    public class AttachmentsViewModel
    {
        [Display(Name = "Proposal document")]
        public IFormFile? ProposalDocument { get; set; }

        [Display(Name = "Pilot / showreel link")]
        [Url(ErrorMessage = "Please enter a valid URL.")]
        public string? PilotShowreelLink { get; set; }

        [Display(Name = "Budget document")]
        public IFormFile? BudgetDocument { get; set; }

        [Display(Name = "Additional file")]
        public IFormFile? AdditionalFile { get; set; }

        public string? ExistingProposalDocument { get; set; }

        public string? ExistingBudgetDocument { get; set; }

        public string? ExistingAdditionalFile { get; set; }
    }
}