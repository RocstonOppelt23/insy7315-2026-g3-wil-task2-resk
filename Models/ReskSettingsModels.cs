using RESK.WIL.Services;

namespace RESK.WIL.Models
{
    // One language row posted from the Languages tab.
    public class ReskLanguageForm
    {
        public string? Code { get; set; }

        public string? Name { get; set; }

        public string? File { get; set; }

        public string? Maintainer { get; set; }

        public bool Active { get; set; }
    }


    // Everything the System settings tabs can post (each tab only uses its own fields).
    public class ReskSettingsForm
    {
        public string Tab { get; set; } = "general";

        // ---------- General ----------
        public string? OrganisationName { get; set; }
        public string? SystemName { get; set; }
        public string? SupportEmail { get; set; }
        public string? ContactNumber { get; set; }
        public string? DefaultLanguage { get; set; }

        // ---------- General + Registration ----------
        public bool RequireApproval { get; set; }
        public bool RequireAdminKey { get; set; }
        public string? AdminKey { get; set; }
        public string? DefaultRole { get; set; }

        // ---------- Registration ----------
        public bool AllowRegistration { get; set; }
        public int ApprovalWindowDays { get; set; }
        public int PasswordMinLength { get; set; }
        public bool PasswordRequireNumber { get; set; }
        public bool PasswordRequireSpecial { get; set; }

        // ---------- Workflow ----------
        public bool RequireAssignment { get; set; }
        public bool RequireComments { get; set; }
        public int TargetReviewDays { get; set; }
        public int EscalateAfterDays { get; set; }
        public string? FinalDecisionRole { get; set; }

        // ---------- Languages ----------
        public string? FallbackLanguage { get; set; }
        public bool RememberLanguage { get; set; }
        public List<ReskLanguageForm> Languages { get; set; } = new();

        // ---------- Notifications (keys of the switches that are on) ----------
        public List<string> InApp { get; set; } = new();
        public List<string> Email { get; set; } = new();
        public string? DailySummaryTime { get; set; }
        public string? EmailSenderName { get; set; }

        // ---------- Security ----------
        public bool RequireMfa { get; set; }
        public bool LockoutEnabled { get; set; }
        public int SessionTimeoutMinutes { get; set; }
        public int FailedLoginLimit { get; set; }
        public int LockoutMinutes { get; set; }
        public int AuditRetentionYears { get; set; }
        public int PasswordExpiryDays { get; set; }
    }


    public class ReskSettingsViewModel
    {
        // general, registration, workflow, languages, notifications, security
        public string Tab { get; set; } = "general";

        // The values to show (saved ones, or what was typed when there are errors).
        public ReskSettings Settings { get; set; } = new();

        public List<string> Errors { get; set; } = new();

        // False for roles that may only look at the settings.
        public bool CanEdit { get; set; } = true;

        // "Last saved 2 Oct 2026 at 14:05 by Admin User"
        public string? SavedText { get; set; }

        // ---------- Workflow tab ----------
        public List<(string Id, string Name)> DecisionRoles { get; set; } = new();

        public int InReview { get; set; }

        public int PastTarget { get; set; }

        public int NeedEscalation { get; set; }

        // ---------- Security tab ----------
        // False until one successful sign-in has been recognised (the lockout waits for that).
        public bool LockoutReady { get; set; }
    }
}