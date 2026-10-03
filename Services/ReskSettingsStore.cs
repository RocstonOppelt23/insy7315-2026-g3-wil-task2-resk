using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using RESK.WIL.Models;

namespace RESK.WIL.Services
{
    // Reads and saves System settings, and holds the option lists the screens use.
    public static class ReskSettingsStore
    {
        public static readonly (string Key, string Title, string Hint)[] Events =
        {
            ("submitted", "New proposal submitted", "Notify proposal managers and assigned reviewers"),
            ("status", "Proposal status changed", "Notify the producer when a decision or request is recorded"),
            ("registration", "User registration awaiting approval", "Notify administrators about pending accounts"),
            ("role", "Role or permission changed", "Notify affected users and administrators"),
            ("deadline", "Review deadline approaching", "Notify assigned reviewers before the target date")
        };

        public static readonly int[] ApprovalWindows = { 1, 2, 3, 5, 7, 10 };
        public static readonly int[] PasswordLengths = { 6, 8, 10, 12, 14 };
        public static readonly int[] ReviewTargets = { 3, 5, 7, 10, 14 };
        public static readonly int[] EscalationDays = { 5, 7, 10, 14, 21 };
        public static readonly int[] SessionTimeouts = { 15, 30, 60, 120, 240, 480 };
        public static readonly int[] LoginLimits = { 3, 5, 10 };
        public static readonly int[] LockoutDurations = { 5, 15, 30, 60 };
        public static readonly int[] RetentionChoices = { 1, 3, 5, 7, 10 };
        public static readonly int[] ExpiryChoices = { 0, 30, 60, 90, 180 };
        public static readonly string[] RegistrationRoles = { "Producer", "Reviewer" };

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };
        private static readonly object Gate = new object();
        private static ReskSettings? _current;
        private static string? _root;

        // Goes up by one each time settings are saved (so they can be applied again).
        public static int Version { get; private set; } = 1;

        // Used by the audit log before a request has loaded the settings.
        public static int AuditRetention => _current?.AuditRetentionYears ?? 10;


        // The saved settings (kept in memory after the first read). Treat as read-only.
        public static ReskSettings Current(string contentRoot)
        {
            lock (Gate)
            {
                if (_current != null && _root == contentRoot)
                {
                    return _current;
                }

                ReskSettings? settings = null;
                string path = FilePath(contentRoot);

                try
                {
                    if (File.Exists(path))
                    {
                        settings = JsonSerializer.Deserialize<ReskSettings>(File.ReadAllText(path));
                    }
                }
                catch (IOException)
                {
                }
                catch (JsonException)
                {
                    File.Copy(path, path + ".broken-" + DateTime.UtcNow.Ticks, true);
                }

                bool firstRun = settings == null;
                settings ??= new ReskSettings();

                foreach (var e in Events)
                {
                    settings.Notify(e.Key);
                }

                if (firstRun && !File.Exists(path))
                {
                    // Write the defaults once so they (and the start date for password age) are kept.
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                        File.WriteAllText(path, JsonSerializer.Serialize(settings, JsonOptions));
                    }
                    catch (IOException)
                    {
                    }
                }

                _current = settings;
                _root = contentRoot;
                return settings;
            }
        }


        // A separate copy that can be changed and then saved.
        public static ReskSettings Editable(string contentRoot)
        {
            return JsonSerializer.Deserialize<ReskSettings>(JsonSerializer.Serialize(Current(contentRoot))) ?? new ReskSettings();
        }


        public static void Save(string contentRoot, ReskSettings settings, string? by)
        {
            lock (Gate)
            {
                if (by != null)
                {
                    settings.UpdatedAtUtc = DateTime.UtcNow;
                    settings.UpdatedBy = by;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(FilePath(contentRoot))!);
                File.WriteAllText(FilePath(contentRoot), JsonSerializer.Serialize(settings, JsonOptions));

                _current = settings;
                _root = contentRoot;
                Version++;
            }
        }


        // =========================================================
        // USED BY OTHER SCREENS
        // =========================================================

        // Password rules and Identity's own lockout follow the settings.
        public static void Apply(IdentityOptions options, ReskSettings settings)
        {
            options.Password.RequiredLength = settings.PasswordMinLength;
            options.Password.RequireDigit = settings.PasswordRequireNumber;
            options.Password.RequireNonAlphanumeric = settings.PasswordRequireSpecial;

            options.Lockout.MaxFailedAccessAttempts = settings.LockoutEnabled ? settings.FailedLoginLimit : 100000;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(settings.LockoutMinutes);
        }


        // "Minimum 8 characters, with a number and a special character"
        public static string PasswordRule(ReskSettings settings)
        {
            var extra = new List<string>();

            if (settings.PasswordRequireNumber)
            {
                extra.Add("a number");
            }

            if (settings.PasswordRequireSpecial)
            {
                extra.Add("a special character");
            }

            return $"Minimum {settings.PasswordMinLength} characters" + (extra.Count == 0 ? "" : ", with " + string.Join(" and ", extra));
        }


        // Default review deadline when a reviewer is assigned.
        public static DateTime ReviewDeadline(string contentRoot)
        {
            return AddWorkingDays(SouthAfricaTime.ToLocal(DateTime.UtcNow).Date, Current(contentRoot).TargetReviewDays);
        }


        // True when the review can't be submitted yet because a reason is needed.
        public static bool CommentsMissing(string contentRoot, string? recommendation, string? comments)
        {
            if (!string.IsNullOrWhiteSpace(comments))
            {
                return false;
            }

            return Current(contentRoot).RequireComments && recommendation != ProposalReviewStore.Approve;
        }


        public static DateTime AddWorkingDays(DateTime date, int days)
        {
            DateTime result = date;

            while (days > 0)
            {
                result = result.AddDays(1);

                if (result.DayOfWeek != DayOfWeek.Saturday && result.DayOfWeek != DayOfWeek.Sunday)
                {
                    days--;
                }
            }

            return result;
        }


        // Monday to Friday days from "from" (not counted) up to "to".
        public static int WorkingDaysBetween(DateTime from, DateTime to)
        {
            int days = 0;

            for (DateTime day = from.Date.AddDays(1); day <= to.Date; day = day.AddDays(1))
            {
                if (day.DayOfWeek != DayOfWeek.Saturday && day.DayOfWeek != DayOfWeek.Sunday)
                {
                    days++;
                }
            }

            return days;
        }


        private static string FilePath(string contentRoot)
        {
            return Path.Combine(contentRoot, "App_Data", "SystemSettings.json");
        }
    }
}