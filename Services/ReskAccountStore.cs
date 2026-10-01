using System.Text.Json;
using System.Text.RegularExpressions;

namespace RESK.WIL.Services
{
    /*
     * =========================================================
     * USER ACCOUNT STORE
     * =========================================================
     *
     * Extra account information the admin Users screens need,
     * saved as JSON in App_Data (no database change needed):
     *
     *   App_Data/Accounts/{userId}.json
     */
    public class ReskActivity
    {
        public DateTime AtUtc { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Detail { get; set; } = string.Empty;

        // info, success, warning, danger
        public string Kind { get; set; } = "info";
    }


    public class ReskAccount
    {
        public string UserId { get; set; } = string.Empty;

        // "" (work it out from roles), Pending, Active, Suspended, Banned, Rejected
        public string Status { get; set; } = string.Empty;

        // Temporary suspension end (null = until an admin lifts it).
        public DateTime? SuspendedUntilUtc { get; set; }

        // Why the account was suspended or banned, and by whom.
        public string? RestrictionReason { get; set; }

        public string? RestrictedBy { get; set; }

        public DateTime? JoinedAtUtc { get; set; }

        // Role to give the user when the registration is approved.
        public string? RequestedRole { get; set; }

        public string? RegistrationReason { get; set; }

        public string? AdminNote { get; set; }

        public bool RequirePasswordChange { get; set; }

        public DateTime? PasswordChangedAtUtc { get; set; }

        public DateTime? StatusChangedAtUtc { get; set; }

        public List<ReskActivity> Activity { get; set; } = new();
    }


    public static class ReskAccountStore
    {
        public const string Pending = "Pending";
        public const string Active = "Active";
        public const string Suspended = "Suspended";
        public const string Rejected = "Rejected";
        public const string Banned = "Banned";

        private static readonly JsonSerializerOptions JsonOptions =
            new JsonSerializerOptions { WriteIndented = true };

        private static readonly object FileLock = new object();


        public static ReskAccount Load(string contentRoot, string userId)
        {
            string path = FilePath(contentRoot, userId);

            try
            {
                if (File.Exists(path))
                {
                    ReskAccount? account =
                        JsonSerializer.Deserialize<ReskAccount>(File.ReadAllText(path));

                    if (account != null)
                    {
                        account.UserId = userId;
                        ExpireSuspension(contentRoot, account);
                        return account;
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (JsonException)
            {
            }

            return new ReskAccount { UserId = userId };
        }


        public static void Save(string contentRoot, ReskAccount account)
        {
            Directory.CreateDirectory(Folder(contentRoot));

            lock (FileLock)
            {
                File.WriteAllText(
                    FilePath(contentRoot, account.UserId),
                    JsonSerializer.Serialize(account, JsonOptions));
            }
        }


        public static void Delete(string contentRoot, string userId)
        {
            try
            {
                string path = FilePath(contentRoot, userId);

                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
            }
        }


        // True while the account is suspended or banned.
        public static bool IsRestricted(ReskAccount account)
        {
            return account.Status == Suspended || account.Status == Banned;
        }


        // Removes the suspension / ban details (the caller sets the new status).
        public static void ClearRestriction(ReskAccount account)
        {
            account.SuspendedUntilUtc = null;
            account.RestrictionReason = null;
            account.RestrictedBy = null;
        }


        // A temporary suspension that has run out ends by itself.
        private static void ExpireSuspension(string contentRoot, ReskAccount account)
        {
            if (account.Status != Suspended ||
                !account.SuspendedUntilUtc.HasValue ||
                account.SuspendedUntilUtc.Value > DateTime.UtcNow)
            {
                return;
            }

            account.Status = string.Empty;
            account.StatusChangedAtUtc = account.SuspendedUntilUtc;
            ClearRestriction(account);
            Log(account, "Suspension ended", "The temporary suspension period finished", "success");

            try
            {
                Save(contentRoot, account);
            }
            catch (IOException)
            {
            }
        }


        // Adds an entry to the user's activity history (newest first, last 200 kept).
        public static void Log(ReskAccount account, string title, string detail, string kind = "info")
        {
            account.Activity.Insert(0, new ReskActivity
            {
                AtUtc = DateTime.UtcNow,
                Title = title,
                Detail = detail,
                Kind = kind
            });

            if (account.Activity.Count > 200)
            {
                account.Activity.RemoveRange(200, account.Activity.Count - 200);
            }
        }


        private static string Folder(string contentRoot)
        {
            return Path.Combine(contentRoot, "App_Data", "Accounts");
        }


        private static string FilePath(string contentRoot, string userId)
        {
            string safe = Regex.Replace(userId ?? string.Empty, "[^A-Za-z0-9-]", string.Empty);

            return Path.Combine(Folder(contentRoot), (safe.Length == 0 ? "unknown" : safe) + ".json");
        }
    }
}