using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace WIL.Models
{
    public class Proposal : IValidatableObject
    {
        //----------instruction----------//

        // IPPF: Independent Producer Proposal Form
        // MVSF: Music Video Submission Form
        // CPAF: Co-Production Application Form
        // PAF: Programme Acquisitions Form
        // TDLA: Television Distribution Licence Agreement

        //----------Text area requirement----------//

        // Long string:
        //     require larger text area

        // ?string optional:
        //     show this field, but allow the user to leave it blank.

        //----------Core Layer----------//
        [Key]
        public int Id { get; set; }
        public int ProducerId { get; set; }
        public User Producer { get; set; } = null!;
        public int? LastChangerId { get; set; }
        public string LastAction { get; set; } = string.Empty;
        public string ProposalStatus { get; set; } = "Draft"; // Draft, Pending, Approved, Rejected
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow; // Draft 
        public DateTime? SubmittedAtUtc { get; set; } // first sumitted date to CTTV
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;// after submission, when the reviewer / producer / admin updates the proposal

        //----------Programme----------//

        // System Details

        public string ProposalType { get; set;} = string.Empty;
        public string PhysicalAddress { get; set; } = string.Empty;

        // General Programme Details
        public string ProgrammeTitle { get; set; } = string.Empty;
        // Partner {Long string / ?string optional}
        public string? Partner { get; set; }
        public string? TdlaFileUrl { get; set; }


        //----------Production Details----------//

        // IPPF /MVSF {?string optional}
        public string? Publisher { get; set; }
        //-----------//

        // IPPF [unit min: 26-52, episodes < 13]
        public int Duration { get; set; }
        //-----------//

        // IPPF / PAF {?string optional - [IPPF < 13]}
        public int? Episodes  { get; set; } // int? to avoid default 0 value
        //-----------//

        // IPPF / CPAF / PAF {Long string}
        public string Introduction { get; set; } = string.Empty;
        //-----------//

        // IPPF {Long string}
        public string Background { get; set; } = string.Empty;
        //-----------//

        // CPAF {Long string}
        public string Motivation { get; set; } = string.Empty;
        //-----------//

        // IPPF {Long string}
        public string Synopsis { get; set; } = string.Empty;
        //-----------//

        // IPPF / CPAF {Long string}
        public string Treatment { get; set; } = string.Empty;
        //-----------//

        // IPPF / CPAF / PAF
        public string Language { get; set; } = string.Empty;
        //-----------//

        // IPPF {Long string}
        public string FinancePlan { get; set; } = string.Empty;
        //-----------//

        // CPAF [resource and skills]
        public string ResourceSkills { get; set; } = string.Empty;
        //-----------//

        // CPAF  {Long string}
        public string Content { get; set; } = string.Empty;
        //-----------//

        // IPPF / CPAF {Long string} [Target Audience]
        public string TargetAudience { get; set; } = string.Empty;
        //-----------//

        // IPPF {Long string / ?string optional} [social media handles]
        public string? MediaHandles { get; set; }
        //-----------//

        // MVSF / CPAF / PAF {?string optional}
        public string? Genre { get; set; }
        //-----------//

        // MVSF [check box to select the type of copy right]
        public string Copyright { get; set; } = string.Empty;
        //-----------//

        // CPAF {?string optional}
        public string? WebsiteUrl { get; set; }
        //-----------//

        // CPAF {Long string / ?string optional} [Support From CTTV]
        public string? CTTVSupport { get; set; }
        //-----------//

        // CPAF {Long string / ?string optional} [Sponsors or Donors]
        public string? Sponsors { get; set; }
        //-----------//

        // PAF [check box to select the type of licence Duration]
        public string LicenceDuration { get; set; } = string.Empty;
        //-----------//


        //-----------End of Programme----------//

        // Verification 2 Digits [shows when create, strings because the 01 also 2 digits]
        public string VerificationCode { get; set; } = string.Empty;
        // Check if user read the TDLA file
        public bool IsTdlaRead { get; set; }

        //----------Validation----------//
        public IEnumerable<ValidationResult> Validate(
    ValidationContext validationContext)
        {
            string type = ProposalType?.Trim().ToUpperInvariant() ?? "";

            if (type is not ("IPPF" or "MVSF" or "CPAF" or "PAF"))
            {
                yield return Required(nameof(ProposalType));
                yield break;
            }

            // Required on all four CTTV forms.
            if (string.IsNullOrWhiteSpace(ProgrammeTitle))
                yield return Required(nameof(ProgrammeTitle));

            // Required on IPPF, MVSF, and CPAF; optional on PAF.
            if (type is "IPPF" or "MVSF" or "CPAF")
            {
                if (Duration <= 0)
                    yield return Required(nameof(Duration));
            }

            if (type == "IPPF")
            {
                if (string.IsNullOrWhiteSpace(Introduction))
                    yield return Required(nameof(Introduction));

                if (string.IsNullOrWhiteSpace(Background))
                    yield return Required(nameof(Background));

                if (string.IsNullOrWhiteSpace(Synopsis))
                    yield return Required(nameof(Synopsis));

                if (string.IsNullOrWhiteSpace(Treatment))
                    yield return Required(nameof(Treatment));

                if (string.IsNullOrWhiteSpace(Language))
                    yield return Required(nameof(Language));

                if (string.IsNullOrWhiteSpace(FinancePlan))
                    yield return Required(nameof(FinancePlan));

                if (string.IsNullOrWhiteSpace(TargetAudience))
                    yield return Required(nameof(TargetAudience));

                // IPPF programme duration must be 26 or 52 minutes.
                if (Duration != 26 && Duration != 52)
                {
                    yield return new ValidationResult(
                        "Select a duration of 26 or 52 minutes.",
                        new[] { nameof(Duration) });
                }

                // Episodes may be blank. If entered, allow 1 to 13.
                if (Episodes.HasValue &&
                    (Episodes.Value < 1 || Episodes.Value > 13))
                {
                    yield return new ValidationResult(
                        "Enter a number of episodes from 1 to 13.",
                        new[] { nameof(Episodes) });
                }
            }

            if (type == "CPAF")
            {
                if (string.IsNullOrWhiteSpace(Genre))
                    yield return Required(nameof(Genre));

                if (string.IsNullOrWhiteSpace(TargetAudience))
                    yield return Required(nameof(TargetAudience));

                if (string.IsNullOrWhiteSpace(Motivation))
                    yield return Required(nameof(Motivation));

                if (string.IsNullOrWhiteSpace(Content))
                    yield return Required(nameof(Content));

                if (string.IsNullOrWhiteSpace(Treatment))
                    yield return Required(nameof(Treatment));

                if (string.IsNullOrWhiteSpace(Language))
                    yield return Required(nameof(Language));

                if (string.IsNullOrWhiteSpace(ResourceSkills))
                    yield return Required(nameof(ResourceSkills));
            }

            if (type == "PAF")
            {
                if (string.IsNullOrWhiteSpace(Language))
                    yield return Required(nameof(Language));

                if (string.IsNullOrWhiteSpace(LicenceDuration))
                    yield return Required(nameof(LicenceDuration));

                if (!IsTdlaRead)
                {
                    yield return new ValidationResult(
                        "You must accept the licence agreement.",
                        new[] { nameof(IsTdlaRead) });
                }
                if (Episodes.HasValue && Episodes.Value < 1)
                {
                    yield return new ValidationResult(
                        "The number of episodes must be at least 1.",
                        new[] { nameof(Episodes) });
                }
            }

            // Website is optional, but check its format if supplied.
            if (!string.IsNullOrWhiteSpace(WebsiteUrl) &&
                !Uri.TryCreate(WebsiteUrl, UriKind.Absolute, out Uri? websiteUri))
            {
                yield return new ValidationResult(
                    "Enter a valid website URL.",
                    new[] { nameof(WebsiteUrl) });
            }
        }

        private static ValidationResult Required(string propertyName) =>
            new(
                $"{propertyName} is required.",
                new[] { propertyName });
    }

    public class ProposalComment
    {
        [Key]
        public int Id { get; set; }

        public int ProposalId { get; set; }
        public int AuthorUserId { get; set; }

        public string Message { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public class ProposalReview
    {
        [Key]
        public int Id { get; set; }

        public int ProposalId { get; set; }
        public int ReviewerUserId { get; set; }

        // For example: Approved, ChangesRequested, Rejected
        public string Action { get; set; } = string.Empty;
        public string? Feedback { get; set; }
        public DateTime ReviewedAtUtc { get; set; } = DateTime.UtcNow;
    }
    //----------Constructor----------//
}