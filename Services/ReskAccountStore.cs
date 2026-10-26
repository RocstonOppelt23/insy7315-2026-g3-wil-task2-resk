using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using RESK.WIL.Data;

namespace RESK.WIL.Services
{
    /*
     * =========================================================
     * USER ACCOUNT STORE
     * =========================================================
     *
     * Extra account information the admin Users screens need,
     * saved in the database:
     *
     *   ReskAccounts           one row per user (status, restriction, notes)
     *   ReskAccountActivities  the user's activity history
     */
    public class ReskActivity
    {
        // Database key (not shown anywhere).
        [JsonIgnore]
        public int Id { get; set; }

        // ReskAccounts.UserId this entry belongs to.
        [JsonIgnore]
        public string AccountUserId { get; set; } = string.Empty;

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

        // Newest first.
        public List<ReskActivity> Activity { get; set; } = new();
    }


    public static class ReskAccountStore
    {
        public const string Pending = "Pending";
        public const string Active = "Active";
        public const string Suspended = "Suspended";
        public const string Rejected = "Rejected";
        public const string Banned = "Banned";

        // Every value the Status column accepts (also used by the check constraint).
        public static readonly string[] Statuses = { "", Pending, Active, Suspended, Rejected, Banned };

        public const int MaxActivity = 200;


        // contentRoot is kept so existing callers don't change; the data now lives in the database.
        public static ReskAccount Load(string contentRoot, string userId)
        {
            ReskAccount? account;

            using (ApplicationDbContext db = ReskDb.Open())
            {
                account = db.ReskAccounts
                    .AsNoTracking()
                    .Include(a => a.Activity
                        .OrderByDescending(x => x.AtUtc)
                        .ThenByDescending(x => x.Id))
                    .FirstOrDefault(a => a.UserId == userId);
            }

            if (account == null)
            {
                return new ReskAccount { UserId = userId };
            }

            ExpireSuspension(contentRoot, account);
            return account;
        }


        // Insert or update the account row. New activity entries are added and
        // entries that were trimmed from the list are removed.
        public static void Save(string contentRoot, ReskAccount account)
        {
            using ApplicationDbContext db = ReskDb.Open();

            ReskAccount? existing = db.ReskAccounts
                .Include(a => a.Activity)
                .FirstOrDefault(a => a.UserId == account.UserId);

            foreach (ReskActivity item in account.Activity)
            {
                item.AccountUserId = account.UserId;
            }

            if (existing == null)
            {
                foreach (ReskActivity item in account.Activity)
                {
                    item.Id = 0;
                }

                db.ReskAccounts.Add(account);
                db.SaveChanges();
                return;
            }

            db.Entry(existing).CurrentValues.SetValues(account);

            var kept = new HashSet<int>(account.Activity.Where(x => x.Id != 0).Select(x => x.Id));

            db.ReskAccountActivities.RemoveRange(existing.Activity.Where(x => !kept.Contains(x.Id)));
            db.ReskAccountActivities.AddRange(account.Activity.Where(x => x.Id == 0));

            db.SaveChanges();
        }


        public static void Delete(string contentRoot, string userId)
        {
            using ApplicationDbContext db = ReskDb.Open();

            ReskAccount? existing = db.ReskAccounts
                .Include(a => a.Activity)
                .FirstOrDefault(a => a.UserId == userId);

            if (existing != null)
            {
                db.ReskAccounts.Remove(existing);
                db.SaveChanges();
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

            Save(contentRoot, account);
        }


        // Adds an entry to the user's activity history (newest first, last 200 kept).
        public static void Log(ReskAccount account, string title, string detail, string kind = "info")
        {
            account.Activity.Insert(0, new ReskActivity
            {
                AccountUserId = account.UserId,
                AtUtc = DateTime.UtcNow,
                Title = title,
                Detail = detail,
                Kind = kind
            });

            if (account.Activity.Count > MaxActivity)
            {
                account.Activity.RemoveRange(MaxActivity, account.Activity.Count - MaxActivity);
            }
        }
    }
}
