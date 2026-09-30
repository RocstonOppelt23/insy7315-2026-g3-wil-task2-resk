using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace RESK.WIL.Models
{
    // =========================================================
    // SAVED PROFILE (stored as JSON in App_Data/Profiles)
    // =========================================================

    public class ProducerProfile
    {
        public string FullName { get; set; } = string.Empty;

        public string Organisation { get; set; } = string.Empty;

        public string PreferredContactMethod { get; set; } = "Email";

        // Set by an administrator only.
        public string ProducerCategory { get; set; } = "Community Producer";

        public string PreferredLanguage { get; set; } = "English";

        public bool NotifyStatusChanges { get; set; } = true;

        public bool NotifyReviewerComments { get; set; } = true;

        public bool NotifyWeeklySummary { get; set; }

        // "Reviewer" or "Administrator" while a request is pending.
        public string? RequestedPosition { get; set; }

        public DateTime? PositionRequestedAtUtc { get; set; }
    }


    // =========================================================
    // WHAT THE SETTINGS FORM POSTS
    // =========================================================

    public class ProducerSettingsForm
    {
        [Required(ErrorMessage = "Enter your full name.")]
        [StringLength(100, ErrorMessage = "Full name can be up to 100 characters.")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter your email address.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [StringLength(256)]
        public string Email { get; set; } = string.Empty;

        [StringLength(30, ErrorMessage = "Phone number can be up to 30 characters.")]
        [RegularExpression(@"^[0-9+()\s-]*$", ErrorMessage = "Use numbers, spaces, + and - only.")]
        public string? PhoneNumber { get; set; }

        [StringLength(150, ErrorMessage = "Organisation can be up to 150 characters.")]
        public string? Organisation { get; set; }

        public string? PreferredContactMethod { get; set; } = "Email";

        public string? PreferredLanguage { get; set; } = "English";

        public bool NotifyStatusChanges { get; set; } = true;

        public bool NotifyReviewerComments { get; set; } = true;

        public bool NotifyWeeklySummary { get; set; }

        // New profile photo (JPG or PNG, up to 5 MB).
        [ValidateNever]
        public IFormFile? Photo { get; set; }

        // True when the producer clicked "Remove photo".
        public bool RemovePhoto { get; set; }

        // Tab that was open when the form was saved.
        public string? ActiveTab { get; set; }
    }


    // =========================================================
    // SETTINGS PAGE
    // =========================================================

    public class ProducerSettingsViewModel
    {
        public ProducerSettingsForm Form { get; set; } = new();

        public string ProducerName { get; set; } = "Producer";

        // "KC"
        public string Initials { get; set; } = "P";

        // Null when the producer has no photo yet.
        public string? PhotoUrl { get; set; }

        public string ProducerCategory { get; set; } = "Community Producer";

        public string AccountEmail { get; set; } = string.Empty;

        // Pending position request, if any.
        public string? RequestedPosition { get; set; }

        public string? PositionRequestedText { get; set; }

        // profile, language, notifications, security or account
        public string ActiveTab { get; set; } = "profile";

        public string? Message { get; set; }

        public string? ErrorMessage { get; set; }

        public List<string> Positions { get; set; } = new();

        public List<string> ContactMethods { get; set; } = new();

        public List<string> Languages { get; set; } = new();
    }
}