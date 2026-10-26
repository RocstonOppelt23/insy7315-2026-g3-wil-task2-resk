using Microsoft.EntityFrameworkCore;
using RESK.WIL.Data;
using RESK.WIL.Models;

namespace RESK.WIL.Services
{
    /*
     * =========================================================
     * AUDIT LOG
     * =========================================================
     *
     * A read-only history of important actions: sign-ins, user and
     * role changes, category changes, proposal workflow steps and
     * CSV exports.
     *
     * Events are written by ReskAuditMiddleware. There is no code
     * anywhere that edits or deletes a single event, so the log can
     * only grow. Events older than the retention period are dropped.
     *
     * Saved in the database:
     *   ReskAuditEvents   one row per event
     *   ReskAuditChanges  the before/after values of each event
     */
    public static class ReskAuditLog
    {
        public const string Successful = "Successful";
        public const string Failed = "Failed";

        public static int RetentionYears => ReskSettingsStore.AuditRetention;

        public static readonly string[] Modules =
        {
            "Proposals", "Users", "Roles", "Categories", "Reports", "Settings", "Security"
        };

        public static readonly (string Key, string Label)[] Types =
        {
            ("create", "Created"), ("update", "Updated"), ("delete", "Deleted"),
            ("submit", "Submitted"), ("approve", "Approved"), ("reject", "Rejected / changes requested"),
            ("assign", "Assigned"), ("export", "Exported"), ("signin", "Sign-in and sign-out"),
            ("failed", "Failed or blocked")
        };

        private static readonly object Gate = new object();

        // Old events are removed once per run of the application.
        private static bool _retentionApplied;


        // Adds one event. Never throws: the audit log must not break the page it records.
        // contentRoot is kept so existing callers don't change; the data now lives in the database.
        public static ReskAuditEvent Add(string contentRoot, ReskAuditEvent item)
        {
            lock (Gate)
            {
                try
                {
                    using ApplicationDbContext db = ReskDb.Open();

                    item.Id = NextId(db, item.AtUtc);

                    foreach (ReskAuditChange change in item.Changes)
                    {
                        change.Id = 0;
                        change.EventId = item.Id;
                    }

                    db.ReskAuditEvents.Add(item);
                    db.SaveChanges();
                }
                catch (Exception)
                {
                    // Keep the request working even if the database is unavailable.
                }

                return item;
            }
        }


        // Every event, newest first.
        public static List<ReskAuditEvent> All(string contentRoot)
        {
            lock (Gate)
            {
                using ApplicationDbContext db = ReskDb.Open();

                ApplyRetention(db);

                return db.ReskAuditEvents
                    .AsNoTracking()
                    .Include(e => e.Changes.OrderBy(c => c.Id))
                    .OrderByDescending(e => e.AtUtc)
                    .ThenByDescending(e => e.Id)
                    .ToList();
            }
        }


        public static ReskAuditEvent? Get(string contentRoot, string? id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            string key = id.Trim().ToUpperInvariant();

            using ApplicationDbContext db = ReskDb.Open();

            return db.ReskAuditEvents
                .AsNoTracking()
                .Include(e => e.Changes.OrderBy(c => c.Id))
                .FirstOrDefault(e => e.Id == key);
        }


        // Retention: events older than the retention period are removed.
        private static void ApplyRetention(ApplicationDbContext db)
        {
            if (_retentionApplied)
            {
                return;
            }

            DateTime cutoff = DateTime.UtcNow.AddYears(-RetentionYears);

            List<ReskAuditEvent> old = db.ReskAuditEvents
                .Include(e => e.Changes)
                .Where(e => e.AtUtc < cutoff)
                .ToList();

            if (old.Count > 0)
            {
                db.ReskAuditEvents.RemoveRange(old);
                db.SaveChanges();
            }

            _retentionApplied = true;
        }


        // AUD-{year}-{number}: the number restarts at 00001 each year.
        private static string NextId(ApplicationDbContext db, DateTime atUtc)
        {
            string prefix = "AUD-" + SouthAfricaTime.ToLocal(atUtc).Year + "-";

            string? lastId = db.ReskAuditEvents
                .Where(e => e.Id.StartsWith(prefix))
                .OrderByDescending(e => e.Id)
                .Select(e => e.Id)
                .FirstOrDefault();

            int last = 0;

            if (lastId != null)
            {
                int.TryParse(lastId.Substring(prefix.Length), out last);
            }

            return prefix + (last + 1).ToString("00000");
        }


        // Lets tests start from a clean state.
        internal static void ResetCache()
        {
            lock (Gate)
            {
                _retentionApplied = false;
            }
        }
    }
}
