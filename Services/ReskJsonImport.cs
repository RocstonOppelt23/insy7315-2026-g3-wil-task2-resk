using System.Text.Json;
using RESK.WIL.Data;

namespace RESK.WIL.Services
{
    /*
     * =========================================================
     * ONE-TIME IMPORT OF THE OLD App_Data JSON FILES
     * =========================================================
     *
     * Reviews, accounts, roles and the audit log used to be saved
     * as JSON files in App_Data. They now live in the database.
     *
     * At startup, each file (or folder) that still exists is copied
     * into its table - but only if that table is still empty, so the
     * import can never overwrite or duplicate database data. After a
     * successful import the file is renamed with ".imported" on the
     * end, which keeps it as a backup and stops it being read again.
     */
    public static class ReskJsonImport
    {
        public const string ImportedSuffix = ".imported";


        public static void ImportAll(string contentRoot, ApplicationDbContext db, ILogger logger)
        {
            string dataFolder = Path.Combine(contentRoot, "App_Data");

            if (!Directory.Exists(dataFolder))
            {
                return;
            }

            Run(logger, "proposal reviews", () => ImportReviews(dataFolder, db));
            Run(logger, "user accounts", () => ImportAccounts(dataFolder, db));
            Run(logger, "roles", () => ImportRoles(dataFolder, db));
            Run(logger, "role assignments", () => ImportAssignments(dataFolder, db));
            Run(logger, "Administrator account", () => ImportAdministrator(dataFolder, db));
            Run(logger, "audit log", () => ImportAuditLog(dataFolder, db));
        }


        // A failed import is logged and skipped; the app still starts.
        private static void Run(ILogger logger, string name, Func<int> import)
        {
            try
            {
                int count = import();

                if (count > 0)
                {
                    logger.LogInformation("Imported {Count} {Name} from App_Data JSON into the database.", count, name);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not import {Name} from App_Data JSON.", name);
            }
        }


        // ---------- App_Data/Reviews/{proposalId}.json ----------
        public static int ImportReviews(string dataFolder, ApplicationDbContext db)
        {
            string folder = Path.Combine(dataFolder, "Reviews");

            if (!Directory.Exists(folder) || db.ReskProposalReviews.Any())
            {
                return 0;
            }

            int count = 0;

            foreach (string file in Directory.GetFiles(folder, "*.json"))
            {
                if (!int.TryParse(Path.GetFileNameWithoutExtension(file), out int proposalId))
                {
                    continue;
                }

                ProposalReview? review = Read<ProposalReview>(file);

                if (review == null)
                {
                    continue;
                }

                review.ProposalId = proposalId;
                db.ReskProposalReviews.Add(review);
                count++;
            }

            db.SaveChanges();
            MarkImported(folder);
            return count;
        }


        // ---------- App_Data/Accounts/{userId}.json ----------
        public static int ImportAccounts(string dataFolder, ApplicationDbContext db)
        {
            string folder = Path.Combine(dataFolder, "Accounts");

            if (!Directory.Exists(folder) || db.ReskAccounts.Any())
            {
                return 0;
            }

            int count = 0;

            foreach (string file in Directory.GetFiles(folder, "*.json"))
            {
                ReskAccount? account = Read<ReskAccount>(file);

                if (account == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(account.UserId))
                {
                    account.UserId = Path.GetFileNameWithoutExtension(file);
                }

                if (!ReskAccountStore.Statuses.Contains(account.Status))
                {
                    account.Status = string.Empty;
                }

                foreach (ReskActivity item in account.Activity)
                {
                    item.Id = 0;
                    item.AccountUserId = account.UserId;
                }

                db.ReskAccounts.Add(account);
                count++;
            }

            db.SaveChanges();
            MarkImported(folder);
            return count;
        }


        // ---------- App_Data/Roles.json ----------
        public static int ImportRoles(string dataFolder, ApplicationDbContext db)
        {
            string file = Path.Combine(dataFolder, "Roles.json");

            if (!File.Exists(file) || db.ReskRoles.Any())
            {
                return 0;
            }

            List<ReskRole> roles = Read<List<ReskRole>>(file) ?? new List<ReskRole>();

            for (int i = 0; i < roles.Count; i++)
            {
                roles[i].SortOrder = i;
            }

            db.ReskRoles.AddRange(roles);
            db.SaveChanges();
            MarkImported(file);
            return roles.Count;
        }


        // ---------- App_Data/RoleAssignments.json ----------
        public static int ImportAssignments(string dataFolder, ApplicationDbContext db)
        {
            string file = Path.Combine(dataFolder, "RoleAssignments.json");

            if (!File.Exists(file) || db.ReskRoleAssignments.Any())
            {
                return 0;
            }

            Dictionary<string, string> map = Read<Dictionary<string, string>>(file) ?? new();

            db.ReskRoleAssignments.AddRange(map.Select(pair =>
                new ReskRoleAssignment { UserId = pair.Key, RoleId = pair.Value }));

            db.SaveChanges();
            MarkImported(file);
            return map.Count;
        }


        // ---------- App_Data/Administrator.json ----------
        public static int ImportAdministrator(string dataFolder, ApplicationDbContext db)
        {
            string file = Path.Combine(dataFolder, "Administrator.json");

            if (!File.Exists(file) || db.ReskSystemValues.Find(ReskRoleStore.AdministratorKey) != null)
            {
                return 0;
            }

            Dictionary<string, string> saved = Read<Dictionary<string, string>>(file) ?? new();

            if (!saved.TryGetValue("UserId", out string? userId) || string.IsNullOrWhiteSpace(userId))
            {
                return 0;
            }

            db.ReskSystemValues.Add(new ReskSystemValue { Key = ReskRoleStore.AdministratorKey, Value = userId });

            if (saved.TryGetValue("Email", out string? email))
            {
                db.ReskSystemValues.Add(new ReskSystemValue { Key = ReskRoleStore.AdministratorEmailKey, Value = email });
            }

            db.SaveChanges();
            MarkImported(file);
            return 1;
        }


        // ---------- App_Data/AuditLog.jsonl (one event per line) ----------
        public static int ImportAuditLog(string dataFolder, ApplicationDbContext db)
        {
            string file = Path.Combine(dataFolder, "AuditLog.jsonl");

            if (!File.Exists(file) || db.ReskAuditEvents.Any())
            {
                return 0;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int count = 0;

            foreach (string line in File.ReadAllLines(file))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                ReskAuditEvent? item;

                try
                {
                    item = JsonSerializer.Deserialize<ReskAuditEvent>(line);
                }
                catch (JsonException)
                {
                    continue;
                }

                if (item == null || item.Id.Length == 0 || !seen.Add(item.Id))
                {
                    continue;
                }

                if (item.Result != ReskAuditLog.Failed)
                {
                    item.Result = ReskAuditLog.Successful;
                }

                foreach (ReskAuditChange change in item.Changes)
                {
                    change.Id = 0;
                    change.EventId = item.Id;
                }

                db.ReskAuditEvents.Add(item);
                count++;
            }

            db.SaveChanges();
            MarkImported(file);
            return count;
        }


        private static T? Read<T>(string file)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(File.ReadAllText(file));
            }
            catch (JsonException)
            {
                return default;
            }
        }


        // Keeps the old file as a backup but stops it being imported again.
        private static void MarkImported(string path)
        {
            string target = path + ImportedSuffix;

            if (Directory.Exists(path))
            {
                if (!Directory.Exists(target))
                {
                    Directory.Move(path, target);
                }
            }
            else if (File.Exists(path))
            {
                File.Move(path, target, overwrite: true);
            }
        }
    }
}
