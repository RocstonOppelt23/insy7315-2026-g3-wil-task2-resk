namespace RESK.WIL.Services
{
    // One interface language on the Languages tab.
    public class ReskLanguage
    {
        // "en"
        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        // "en.json"
        public string File { get; set; } = string.Empty;

        public string Maintainer { get; set; } = string.Empty;

        public bool Active { get; set; } = true;
    }


    // One event on the Notifications tab.
    public class ReskNotifyRule
    {
        public string Key { get; set; } = string.Empty;

        public bool InApp { get; set; } = true;

        public bool Email { get; set; } = true;
    }


    /*
     * =========================================================
     * SYSTEM SETTINGS (Admin > System settings)
     * =========================================================
     *
     * Saved as JSON (no database change needed):
     *   App_Data/SystemSettings.json
     */
    public class ReskSettings
    {
        // ---------- General ----------
        public string OrganisationName { get; set; } = "Cape Town TV";

        public string SystemName { get; set; } = "Proposal Workflow Management System";

        public string SupportEmail { get; set; } = "support@capetowntv.org";

        public string ContactNumber { get; set; } = "+27 21 000 0000";

        // ---------- Registration ----------
        public bool AllowRegistration { get; set; } = true;

        public bool RequireApproval { get; set; } = true;

        // Producer or Reviewer
        public string DefaultRole { get; set; } = "Producer";

        public int ApprovalWindowDays { get; set; } = 3;

        public bool RequireAdminKey { get; set; } = true;

        // Empty = no key has been set here yet (nothing is checked).
        public string AdminKey { get; set; } = string.Empty;

        public int PasswordMinLength { get; set; } = 8;

        public bool PasswordRequireNumber { get; set; } = true;

        public bool PasswordRequireSpecial { get; set; } = true;

        // ---------- Workflow ----------
        public bool RequireAssignment { get; set; } = true;

        public bool RequireComments { get; set; } = true;

        public int TargetReviewDays { get; set; } = 5;

        public int EscalateAfterDays { get; set; } = 7;

        // Role id (Roles & permissions) that may record the final decision.
        public string FinalDecisionRole { get; set; } = ReskRoleStore.ManagerId;

        // ---------- Languages ----------
        public string DefaultLanguage { get; set; } = "en";

        public string FallbackLanguage { get; set; } = "en";

        public bool RememberLanguage { get; set; } = true;

        public List<ReskLanguage> Languages { get; set; } = new()
        {
            new ReskLanguage { Code = "en", Name = "English", File = "en.json", Maintainer = "Zayeed" },
            new ReskLanguage { Code = "zh", Name = "isiZulu/isiXhosa", File = "zh.json", Maintainer = "Kuan-Chi" }
        };

        // ---------- Notifications ----------
        public List<ReskNotifyRule> Notifications { get; set; } = new();

        // HH:mm
        public string DailySummaryTime { get; set; } = "08:00";

        public string EmailSenderName { get; set; } = "Cape Town TV Workflow";

        // ---------- Security ----------
        // Saved only: the sign-in page has no second step yet.
        public bool RequireMfa { get; set; }

        public bool LockoutEnabled { get; set; } = true;

        public int SessionTimeoutMinutes { get; set; } = 30;

        public int FailedLoginLimit { get; set; } = 5;

        public int LockoutMinutes { get; set; } = 15;

        public int AuditRetentionYears { get; set; } = 7;

        // 0 = passwords never expire
        public int PasswordExpiryDays { get; set; } = 90;

        // Passwords with no recorded change date are counted from this day.
        public DateTime PasswordAgeFromUtc { get; set; } = DateTime.UtcNow;

        // ---------- Saved by ----------
        public DateTime? UpdatedAtUtc { get; set; }

        public string? UpdatedBy { get; set; }


        public ReskNotifyRule Notify(string key)
        {
            ReskNotifyRule? rule = Notifications.FirstOrDefault(n => n.Key == key);

            if (rule == null)
            {
                // "User registration awaiting approval" has email off in the design.
                rule = new ReskNotifyRule { Key = key, Email = key != "registration" };
                Notifications.Add(rule);
            }

            return rule;
        }
    }
}