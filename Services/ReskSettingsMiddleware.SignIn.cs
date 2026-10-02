using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace RESK.WIL.Services
{
    // ReskSettingsMiddleware: lockout after failed sign-ins, inactivity and due password changes.
    public partial class ReskSettingsMiddleware
    {
        // email -> times of the recent failed sign-ins
        private static readonly Dictionary<string, List<DateTime>> Failures = new(StringComparer.OrdinalIgnoreCase);

        // user + sign-in -> last request
        private static readonly ConcurrentDictionary<string, DateTime> LastSeen = new();

        private static readonly object GuardLock = new object();
        private static bool? _detected;


        // =========================================================
        // LOCKOUT AFTER FAILED SIGN-INS
        // =========================================================

        private async Task LoginAsync(HttpContext context, string root, ReskSettings settings)
        {
            string email = await FormValueAsync(context, "Email", "Input.Email", "email", "UserName", "Username", "Input.UserName");

            if (settings.LockoutEnabled && email.Length > 0 && SignInDetected(root))
            {
                int minutes = MinutesLocked(email, settings);

                if (minutes > 0)
                {
                    context.Response.Redirect("/Account/Locked?minutes=" + minutes);
                    return;
                }
            }

            await _next(context);

            if (email.Length == 0)
            {
                return;
            }

            if (SetsLoginCookie(context))
            {
                lock (GuardLock)
                {
                    Failures.Remove(email);
                }

                MarkSignInDetected(root);
                return;
            }

            lock (GuardLock)
            {
                if (!Failures.TryGetValue(email, out List<DateTime>? times))
                {
                    times = new List<DateTime>();
                    Failures[email] = times;
                }

                times.Add(DateTime.UtcNow);

                // Forget attempts that are older than an hour.
                times.RemoveAll(t => t < DateTime.UtcNow.AddHours(-1));
            }
        }


        // Minutes until this email may try again (0 = not locked).
        private static int MinutesLocked(string email, ReskSettings settings)
        {
            lock (GuardLock)
            {
                if (!Failures.TryGetValue(email, out List<DateTime>? times))
                {
                    return 0;
                }

                DateTime now = DateTime.UtcNow;
                List<DateTime> recent = times.Where(t => t > now.AddMinutes(-settings.LockoutMinutes)).OrderBy(t => t).ToList();

                if (recent.Count < settings.FailedLoginLimit)
                {
                    return 0;
                }

                // Unlocks when the attempt that reached the limit is old enough.
                DateTime until = recent[recent.Count - settings.FailedLoginLimit].AddMinutes(settings.LockoutMinutes);

                return Math.Max(1, (int)Math.Ceiling((until - now).TotalMinutes));
            }
        }


        // The lockout only switches on after one successful sign-in has been recognised
        // on this system, so it can never lock everyone out by mistake.
        private static bool SignInDetected(string root)
        {
            lock (GuardLock)
            {
                _detected ??= File.Exists(GuardPath(root));
                return _detected.Value;
            }
        }


        private static void MarkSignInDetected(string root)
        {
            lock (GuardLock)
            {
                if (_detected == true)
                {
                    return;
                }

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(GuardPath(root))!);
                    File.WriteAllText(GuardPath(root), JsonSerializer.Serialize(new { Detected = true, AtUtc = DateTime.UtcNow }));
                }
                catch (IOException)
                {
                }

                _detected = true;
            }
        }


        private static string GuardPath(string root) => Path.Combine(root, "App_Data", "SignInGuard.json");


        // =========================================================
        // SIGNED OUT AFTER INACTIVITY
        // =========================================================

        private static bool IdleTooLong(HttpContext context, string userId, ReskSettings settings)
        {
            // The sign-in cookie changes at every sign-in, so a new sign-in always starts fresh.
            string? ticket = context.Request.Cookies
                .Where(c => c.Key.Contains("Identity.Application") || c.Key.Contains(".AspNetCore.Cookies"))
                .Select(c => c.Value)
                .FirstOrDefault();

            if (string.IsNullOrEmpty(ticket))
            {
                return false;
            }

            string key = userId + "|" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ticket))).Substring(0, 24);
            DateTime now = DateTime.UtcNow;

            if (LastSeen.TryGetValue(key, out DateTime seen) && now - seen > TimeSpan.FromMinutes(settings.SessionTimeoutMinutes))
            {
                LastSeen.TryRemove(key, out _);
                return true;
            }

            LastSeen[key] = now;

            if (LastSeen.Count > 5000)
            {
                foreach (var old in LastSeen.Where(p => now - p.Value > TimeSpan.FromHours(12)).ToList())
                {
                    LastSeen.TryRemove(old.Key, out _);
                }
            }

            return false;
        }


        // =========================================================
        // PASSWORD CHANGES THAT ARE DUE
        // =========================================================

        // True when the user must choose a new password before carrying on.
        public static bool PasswordChangeDue(string root, ReskAccount account, ReskSettings settings)
        {
            if (account.RequirePasswordChange)
            {
                return true;
            }

            if (settings.PasswordExpiryDays <= 0)
            {
                return false;
            }

            // Unknown age: counted from the day password expiry was first switched on.
            DateTime changed = account.PasswordChangedAtUtc ?? settings.PasswordAgeFromUtc;

            return DateTime.UtcNow - changed > TimeSpan.FromDays(settings.PasswordExpiryDays);
        }
    }
}