using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace RESK.WIL.Models
{
    public class Proposal : IValidatableObject
    {
        // =====================================================
        // FORM TYPES
        // =====================================================

        // IPPF: Independent Producer Proposal Form
        // MVSF: Music Video Submission Form
        // CPAF: Co-Production Application Form
        // PAF: Programme Acquisitions Form
        // TDLA: Television Distribution Licence Agreement


        // =====================================================
        // CORE LAYER
        // =====================================================

        [Key]
        public int Id { get; set; }

        public int ProducerId { get; set; }

        public User Producer { get; set; } = null!;

        public int? LastChangerId { get; set; }

        public string LastAction { get; set; } = string.Empty;

        // Draft, Pending, Approved, Rejected
        public string ProposalStatus { get; set; } = "Draft";

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        // First submitted date to CTTV
        public DateTime? SubmittedAtUtc { get; set; }

        // Updated after submission when reviewer,
        // producer or admin updates the proposal
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;


        // =====================================================
        // NEW PROPOSAL INFORMATION
        // =====================================================

        /*
         * These fields have been added from the new proposal
         * structure while keeping the existing CTTV fields.
         */

        [StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [StringLength(1000)]
        public string Description { get; set; } = string.Empty;

        [StringLength(100)]
        public string Category { get; set; } = string.Empty;


        // =====================================================
        // PROGRAMME
        // =====================================================

        // System Details

        public string ProposalType { get; set; } = string.Empty;

        public string PhysicalAddress { get; set; } = string.Empty;


        // General Programme Details

        public string ProgrammeTitle { get; set; } = string.Empty;

        // Partner - optional
        public string? Partner { get; set; }

        public string? TdlaFileUrl { get; set; }


        // =====================================================
        // PRODUCTION DETAILS
        // =====================================================

        // IPPF / MVSF - optional
        public string? Publisher { get; set; }


        // IPPF
        // Unit minimum: 26-52
        // Episodes < 13

        public int Duration { get; set; }


        // IPPF / PAF
        // Optional

        public int? Episodes { get; set; }


        // IPPF / CPAF / PAF

        public string Introduction { get; set; } = string.Empty;


        // IPPF

        public string Background { get; set; } = string.Empty;


        // CPAF

        public string Motivation { get; set; } = string.Empty;


        // IPPF

        public string Synopsis { get; set; } = string.Empty;


        // IPPF / CPAF

        public string Treatment { get; set; } = string.Empty;


        // IPPF / CPAF / PAF

        public string Language { get; set; } = string.Empty;


        // IPPF

        public string FinancePlan { get; set; } = string.Empty;


        // CPAF

        public string ResourceSkills { get; set; } = string.Empty;


        // CPAF

        public string Content { get; set; } = string.Empty;

        public string Status { get; set; } = "Submitted";


        // IPPF / CPAF
        // Target Audience

        public string TargetAudience { get; set; } = string.Empty;


        // IPPF
        // Social media handles - optional

        public string? MediaHandles { get; set; }


        // MVSF / CPAF / PAF - optional

        public string? Genre { get; set; }


        // MVSF
        // Copyright selection

        public string Copyright { get; set; } = string.Empty;


        // CPAF - optional

        public string? WebsiteUrl { get; set; }


        // CPAF - optional

        public string? CTTVSupport { get; set; }


        // CPAF - optional

        public string? Sponsors { get; set; }


        // PAF
        // Licence duration selection

        public string LicenceDuration { get; set; } = string.Empty;


        // =====================================================
        // VERIFICATION
        // =====================================================

        // Two-digit verification code

        public string VerificationCode { get; set; } = string.Empty;

        // Check whether user has read the TDLA file

        public bool IsTdlaRead { get; set; }


        // =====================================================
        // VALIDATION
        // =====================================================

        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            string type =
                ProposalType?.Trim().ToUpperInvariant() ?? "";


            // -------------------------------------------------
            // PROPOSAL TYPE
            // -------------------------------------------------

            if (type is not ("IPPF" or "MVSF" or "CPAF" or "PAF"))
            {
                yield return Required(nameof(ProposalType));
                yield break;
            }


            // -------------------------------------------------
            // NEW PROPOSAL FIELDS
            // -------------------------------------------------

            // Title is required when supplied as part of the
            // general proposal information.

            if (string.IsNullOrWhiteSpace(Title))
            {
                yield return Required(nameof(Title));
            }


            if (string.IsNullOrWhiteSpace(Description))
            {
                yield return Required(nameof(Description));
            }


            if (string.IsNullOrWhiteSpace(Category))
            {
                yield return Required(nameof(Category));
            }


            // -------------------------------------------------
            // PROGRAMME TITLE
            // -------------------------------------------------

            // Required on all four CTTV forms.

            if (string.IsNullOrWhiteSpace(ProgrammeTitle))
            {
                yield return Required(nameof(ProgrammeTitle));
            }


            // -------------------------------------------------
            // DURATION
            // -------------------------------------------------

            // Required on IPPF, MVSF and CPAF.
            // Optional on PAF.

            if (type is "IPPF" or "MVSF" or "CPAF")
            {
                if (Duration <= 0)
                {
                    yield return Required(nameof(Duration));
                }
            }


            // =================================================
            // IPPF VALIDATION
            // =================================================

            if (type == "IPPF")
            {
                if (string.IsNullOrWhiteSpace(Introduction))
                {
                    yield return Required(nameof(Introduction));
                }

                if (string.IsNullOrWhiteSpace(Background))
                {
                    yield return Required(nameof(Background));
                }

                if (string.IsNullOrWhiteSpace(Synopsis))
                {
                    yield return Required(nameof(Synopsis));
                }

                if (string.IsNullOrWhiteSpace(Treatment))
                {
                    yield return Required(nameof(Treatment));
                }

                if (string.IsNullOrWhiteSpace(Language))
                {
                    yield return Required(nameof(Language));
                }

                if (string.IsNullOrWhiteSpace(FinancePlan))
                {
                    yield return Required(nameof(FinancePlan));
                }

                if (string.IsNullOrWhiteSpace(TargetAudience))
                {
                    yield return Required(nameof(TargetAudience));
                }


                // IPPF programme duration must be
                // 26 or 52 minutes.

                if (Duration != 26 && Duration != 52)
                {
                    yield return new ValidationResult(
                        "Select a duration of 26 or 52 minutes.",
                        new[] { nameof(Duration) });
                }


                // Episodes may be blank.
                // If entered, allow 1 to 13.

                if (Episodes.HasValue &&
                    (Episodes.Value < 1 ||
                     Episodes.Value > 13))
                {
                    yield return new ValidationResult(
                        "Enter a number of episodes from 1 to 13.",
                        new[] { nameof(Episodes) });
                }
            }


            // =================================================
            // CPAF VALIDATION
            // =================================================

            if (type == "CPAF")
            {
                if (string.IsNullOrWhiteSpace(Genre))
                {
                    yield return Required(nameof(Genre));
                }

                if (string.IsNullOrWhiteSpace(TargetAudience))
                {
                    yield return Required(nameof(TargetAudience));
                }

                if (string.IsNullOrWhiteSpace(Motivation))
                {
                    yield return Required(nameof(Motivation));
                }

                if (string.IsNullOrWhiteSpace(Content))
                {
                    yield return Required(nameof(Content));
                }

                if (string.IsNullOrWhiteSpace(Treatment))
                {
                    yield return Required(nameof(Treatment));
                }

                if (string.IsNullOrWhiteSpace(Language))
                {
                    yield return Required(nameof(Language));
                }

                if (string.IsNullOrWhiteSpace(ResourceSkills))
                {
                    yield return Required(nameof(ResourceSkills));
                }
            }


            // =================================================
            // PAF VALIDATION
            // =================================================

            if (type == "PAF")
            {
                if (string.IsNullOrWhiteSpace(Language))
                {
                    yield return Required(nameof(Language));
                }

                if (string.IsNullOrWhiteSpace(LicenceDuration))
                {
                    yield return Required(nameof(LicenceDuration));
                }


                // User must accept the licence agreement.

                if (!IsTdlaRead)
                {
                    yield return new ValidationResult(
                        "You must accept the licence agreement.",
                        new[] { nameof(IsTdlaRead) });
                }


                if (Episodes.HasValue &&
                    Episodes.Value < 1)
                {
                    yield return new ValidationResult(
                        "The number of episodes must be at least 1.",
                        new[] { nameof(Episodes) });
                }
            }


            // =================================================
            // WEBSITE URL VALIDATION
            // =================================================

            // Website is optional, but validate the format
            // when supplied.

            if (!string.IsNullOrWhiteSpace(WebsiteUrl) &&
                !Uri.TryCreate(
                    WebsiteUrl,
                    UriKind.Absolute,
                    out Uri? websiteUri))
            {
                yield return new ValidationResult(
                    "Enter a valid website URL.",
                    new[] { nameof(WebsiteUrl) });
            }
        }


        // =====================================================
        // VALIDATION HELPER
        // =====================================================

        private static ValidationResult Required(
            string propertyName) =>
            new(
                $"{propertyName} is required.",
                new[] { propertyName });
    }


    // =========================================================
    // PROPOSAL COMMENT
    // =========================================================

    public class ProposalComment
    {
        [Key]
        public int Id { get; set; }

        public int ProposalId { get; set; }

        public int AuthorUserId { get; set; }

        public string Message { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; } =
            DateTime.UtcNow;
    }


    // =========================================================
    // PROPOSAL REVIEW
    // =========================================================

    public class ProposalReview
    {
        [Key]
        public int Id { get; set; }

        public int ProposalId { get; set; }

        public int ReviewerUserId { get; set; }

        // For example:
        // Approved, ChangesRequested, Rejected

        public string Action { get; set; } = string.Empty;

        public string? Feedback { get; set; }

        public DateTime ReviewedAtUtc { get; set; } =
            DateTime.UtcNow;
    }
}