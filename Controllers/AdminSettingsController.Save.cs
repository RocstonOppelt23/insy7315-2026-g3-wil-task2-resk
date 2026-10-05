using System.Globalization;
using System.Text.RegularExpressions;
using RESK.WIL.Models;
using RESK.WIL.Services;

namespace RESK.WIL.Controllers
{
    // Checks what was typed on each tab and copies it into the settings.
    // Each method returns the problems found (an empty list = fine to save).
    public partial class AdminSettingsController
    {
        private static List<string> ApplyGeneral(ReskSettingsForm form, ReskSettings settings)
        {
            var errors = new List<string>();

            settings.OrganisationName = Text(form.OrganisationName, 80);
            settings.SystemName = Text(form.SystemName, 80);
            settings.SupportEmail = Text(form.SupportEmail, 120);
            settings.ContactNumber = Text(form.ContactNumber, 30);

            if (settings.OrganisationName.Length == 0)
            {
                errors.Add("Enter the organisation name.");
            }

            if (settings.SystemName.Length == 0)
            {
                errors.Add("Enter the system display name.");
            }

            if (!Regex.IsMatch(settings.SupportEmail, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                errors.Add("Enter a valid support email address.");
            }

            if (!Regex.IsMatch(settings.ContactNumber, @"^[0-9+()\s-]*$"))
            {
                errors.Add("The contact number can only use numbers, spaces, + ( ) and -.");
            }

            settings.RequireApproval = form.RequireApproval;
            ApplyKey(form, settings, errors);
            ApplyDefaultRole(form, settings, errors);

            if (settings.Languages.Any(l => l.Active && l.Code == form.DefaultLanguage))
            {
                settings.DefaultLanguage = form.DefaultLanguage!;
            }
            else
            {
                errors.Add("Choose a default language.");
            }

            return errors;
        }


        private static List<string> ApplyRegistration(ReskSettingsForm form, ReskSettings settings)
        {
            var errors = new List<string>();

            settings.AllowRegistration = form.AllowRegistration;
            settings.RequireApproval = form.RequireApproval;
            settings.PasswordRequireNumber = form.PasswordRequireNumber;
            settings.PasswordRequireSpecial = form.PasswordRequireSpecial;

            ApplyDefaultRole(form, settings, errors);
            ApplyKey(form, settings, errors);

            settings.ApprovalWindowDays = Choice(form.ApprovalWindowDays, ReskSettingsStore.ApprovalWindows, "an account approval window", errors);
            settings.PasswordMinLength = Choice(form.PasswordMinLength, ReskSettingsStore.PasswordLengths, "a minimum password length", errors);

            return errors;
        }


        private List<string> ApplyWorkflow(ReskSettingsForm form, ReskSettings settings)
        {
            var errors = new List<string>();

            settings.RequireAssignment = form.RequireAssignment;
            settings.RequireComments = form.RequireComments;
            settings.TargetReviewDays = Choice(form.TargetReviewDays, ReskSettingsStore.ReviewTargets, "a target review time", errors);
            settings.EscalateAfterDays = Choice(form.EscalateAfterDays, ReskSettingsStore.EscalationDays, "an escalation time", errors);

            if (errors.Count == 0 && settings.EscalateAfterDays < settings.TargetReviewDays)
            {
                errors.Add("\"Escalate after\" can't be shorter than the target review time.");
            }

            ReskRole? role = ReskRoleStore.Get(Root, form.FinalDecisionRole);

            if (role != null && role.Area == "Admin" && role.IsActive && !role.IsAdministrator && role.Can("proposals", "approve"))
            {
                settings.FinalDecisionRole = role.Id;
            }
            else
            {
                errors.Add("Choose the role that records the final decision. It needs the \"Proposals - Approve\" permission.");
            }

            return errors;
        }


        private static List<string> ApplyLanguages(ReskSettingsForm form, ReskSettings settings)
        {
            var errors = new List<string>();
            var languages = new List<ReskLanguage>();

            foreach (ReskLanguageForm row in form.Languages)
            {
                string code = Text(row.Code, 5).ToLowerInvariant();
                string name = Text(row.Name, 40);
                string file = Text(row.File, 40).ToLowerInvariant();

                if (!Regex.IsMatch(code, "^[a-z]{2,5}$"))
                {
                    errors.Add($"\"{(name.Length > 0 ? name : code)}\": the language code must be 2 to 5 letters, for example \"zu\".");
                    continue;
                }

                if (name.Length == 0)
                {
                    errors.Add($"Enter a name for the \"{code}\" language.");
                    continue;
                }

                if (languages.Any(l => l.Code == code))
                {
                    errors.Add($"The language code \"{code}\" is used twice.");
                    continue;
                }

                languages.Add(new ReskLanguage
                {
                    Code = code,
                    Name = name,
                    File = Regex.IsMatch(file, @"^[a-z0-9_-]+\.json$") ? file : code + ".json",
                    Maintainer = Text(row.Maintainer, 40),
                    Active = row.Active
                });
            }

            settings.Languages = languages;
            settings.RememberLanguage = form.RememberLanguage;
            settings.DefaultLanguage = Text(form.DefaultLanguage, 5).ToLowerInvariant();
            settings.FallbackLanguage = Text(form.FallbackLanguage, 5).ToLowerInvariant();

            if (!languages.Any(l => l.Active))
            {
                errors.Add("At least one language must be active.");
            }
            else
            {
                if (!languages.Any(l => l.Active && l.Code == settings.DefaultLanguage))
                {
                    errors.Add("The default language must be one of the active languages.");
                }

                if (!languages.Any(l => l.Active && l.Code == settings.FallbackLanguage))
                {
                    errors.Add("The fallback language must be one of the active languages.");
                }
            }

            return errors;
        }


        private static List<string> ApplyNotifications(ReskSettingsForm form, ReskSettings settings)
        {
            var errors = new List<string>();

            foreach (var e in ReskSettingsStore.Events)
            {
                ReskNotifyRule rule = settings.Notify(e.Key);
                rule.InApp = form.InApp.Contains(e.Key);
                rule.Email = form.Email.Contains(e.Key);
            }

            settings.EmailSenderName = Text(form.EmailSenderName, 60);

            if (settings.EmailSenderName.Length == 0)
            {
                errors.Add("Enter the email sender name.");
            }

            if (DateTime.TryParseExact(form.DailySummaryTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime time))
            {
                settings.DailySummaryTime = time.ToString("HH:mm", CultureInfo.InvariantCulture);
            }
            else
            {
                errors.Add("Choose a time for the daily summary.");
            }

            return errors;
        }


        private static List<string> ApplySecurity(ReskSettingsForm form, ReskSettings settings)
        {
            var errors = new List<string>();

            settings.RequireMfa = form.RequireMfa;
            settings.LockoutEnabled = form.LockoutEnabled;
            settings.SessionTimeoutMinutes = Choice(form.SessionTimeoutMinutes, ReskSettingsStore.SessionTimeouts, "a session timeout", errors);
            settings.FailedLoginLimit = Choice(form.FailedLoginLimit, ReskSettingsStore.LoginLimits, "a failed login limit", errors);
            settings.LockoutMinutes = Choice(form.LockoutMinutes, ReskSettingsStore.LockoutDurations, "a lockout duration", errors);
            settings.AuditRetentionYears = Choice(form.AuditRetentionYears, ReskSettingsStore.RetentionChoices, "an audit log retention period", errors);
            settings.PasswordExpiryDays = Choice(form.PasswordExpiryDays, ReskSettingsStore.ExpiryChoices, "a password expiry period", errors);

            return errors;
        }
    }
}