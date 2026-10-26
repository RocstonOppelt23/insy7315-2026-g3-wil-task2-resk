using Microsoft.EntityFrameworkCore;
using RESK.WIL.Data;

namespace RESK.WIL.Services
{
    /*
     * =========================================================
     * ROLES & PERMISSIONS
     * =========================================================
     *
     * Every role has:
     *   - an ACCESS AREA (the real sign-in role it uses):
     *       Admin    -> station management screens (/Admin/...)
     *       Reviewer -> reviewer screens
     *       Producer -> producer screens
     *   - a PERMISSION MATRIX: module x action (view, create, edit,
     *     delete, approve, export)
     *
     * For roles in the Admin area the matrix is ENFORCED on every
     * /Admin page by ReskRestrictionMiddleware. The built-in
     * "Administrator" role always has full access and can't be
     * changed, so the system can never lock everyone out.
     *
     * Saved in the database:
     *   ReskRoles            the roles and their permissions
     *   ReskRoleAssignments  userId -> roleId
     */
    public static partial class ReskRoleStore
    {
        public const string Active = "Active";
        public const string Inactive = "Inactive";
        public const string Draft = "Draft";

        public const string AdministratorId = "administrator";
        public const string ManagerId = "proposal-manager";
        public const string ReviewerId = "reviewer";
        public const string ProducerId = "producer";
        public const string ViewerId = "viewer";

        // Modules: key, name, short description, actions that make sense for it
        public static readonly (string Key, string Name, string Hint, string[] Actions)[] Modules =
        {
            ("users", "Users", "User accounts, approvals and access", new[] { "view", "create", "edit", "delete", "approve", "export" }),
            ("proposals", "Proposals", "Proposal records, reviewers and decisions", new[] { "view", "create", "edit", "delete", "approve", "export" }),
            ("categories", "Categories", "Programme categories", new[] { "view", "create", "edit", "delete" }),
            ("reports", "Reports", "Reports, summaries and CSV exports", new[] { "view", "export" }),
            ("roles", "Roles & permissions", "Roles and what they can do", new[] { "view", "create", "edit", "delete" }),
            ("audit", "Audit log", "History of changes", new[] { "view", "export" }),
            ("settings", "System settings", "Station-wide settings", new[] { "view", "edit" })
        };

        public static readonly (string Key, string Label)[] Actions =
        {
            ("view", "View"), ("create", "Create"), ("edit", "Edit"),
            ("delete", "Delete"), ("approve", "Approve"), ("export", "Export")
        };

        public static readonly (string Key, string Label, string Hint)[] Areas =
        {
            ("Admin", "Station management", "Signs in to the admin screens. The permissions below decide which admin pages and actions they can use."),
            ("Reviewer", "Reviewer workspace", "Signs in to the reviewer screens to review assigned proposals."),
            ("Producer", "Producer workspace", "Signs in to the producer screens to create and submit proposals.")
        };

        private static readonly object FileLock = new object();


        // =========================================================
        // ROLES
        // =========================================================

        // contentRoot is kept so existing callers don't change; the data now lives in the database.
        public static List<ReskRole> All(string contentRoot)
        {
            lock (FileLock)
            {
                using ApplicationDbContext db = ReskDb.Open();

                List<ReskRole> roles = db.ReskRoles
                    .AsNoTracking()
                    .OrderBy(r => r.SortOrder)
                    .ToList();

                if (roles.Count == 0)
                {
                    roles = Seed();
                    Write(db, roles);
                }

                // The Administrator role always exists, active, with full access.
                ReskRole? admin = roles.FirstOrDefault(r => r.Id == AdministratorId);

                if (admin == null)
                {
                    roles.Insert(0, Seed()[0]);
                    Write(db, roles);
                }
                else
                {
                    admin.Status = Active;
                    admin.Area = "Admin";
                    admin.Permissions = AllPermissions();
                }

                return roles;
            }
        }


        public static ReskRole? Get(string contentRoot, string? id)
        {
            return string.IsNullOrEmpty(id) ? null : All(contentRoot).FirstOrDefault(r => r.Id == id);
        }


        // Saves the full list: new roles are added, changed roles updated,
        // and roles missing from the list are deleted.
        public static void SaveAll(string contentRoot, List<ReskRole> roles)
        {
            lock (FileLock)
            {
                using ApplicationDbContext db = ReskDb.Open();
                Write(db, roles);
            }
        }


        // =========================================================
        // ASSIGNMENTS (userId -> roleId)
        // =========================================================

        public static Dictionary<string, string> Assignments(string contentRoot)
        {
            using ApplicationDbContext db = ReskDb.Open();

            return db.ReskRoleAssignments
                .AsNoTracking()
                .ToDictionary(a => a.UserId, a => a.RoleId);
        }


        public static void Assign(string contentRoot, string userId, string? roleId)
        {
            lock (FileLock)
            {
                using ApplicationDbContext db = ReskDb.Open();

                ReskRoleAssignment? existing = db.ReskRoleAssignments.Find(userId);

                if (string.IsNullOrEmpty(roleId))
                {
                    if (existing != null)
                    {
                        db.ReskRoleAssignments.Remove(existing);
                    }
                }
                else if (existing == null)
                {
                    db.ReskRoleAssignments.Add(new ReskRoleAssignment { UserId = userId, RoleId = roleId });
                }
                else
                {
                    existing.RoleId = roleId;
                }

                db.SaveChanges();
            }
        }


        // The role a user has: their assigned role if it matches their sign-in
        // role, otherwise the built-in role for that sign-in role.
        public static ReskRole? RoleFor(string contentRoot, string userId, IEnumerable<string> identityRoles,
            List<ReskRole>? roles = null, Dictionary<string, string>? assignments = null)
        {
            roles ??= All(contentRoot);
            assignments ??= Assignments(contentRoot);

            string? area =
                identityRoles.Contains("Admin") ? "Admin" :
                identityRoles.Contains("Reviewer") ? "Reviewer" :
                identityRoles.Contains("Producer") ? "Producer" : null;

            if (area == null)
            {
                return null;
            }

            if (assignments.TryGetValue(userId, out string? roleId))
            {
                ReskRole? assigned = roles.FirstOrDefault(r => r.Id == roleId);

                if (assigned != null && assigned.Area == area)
                {
                    return SingleAdministrator(contentRoot, userId, assigned, roles);
                }
            }

            return SingleAdministrator(contentRoot, userId, roles.FirstOrDefault(r => r.Id == DefaultRoleId(area)), roles);
        }


        public static string DefaultRoleId(string area)
        {
            return area switch
            {
                "Admin" => AdministratorId,
                "Reviewer" => ReviewerId,
                _ => ProducerId
            };
        }


        // Makes the ReskRoles table match the list (order is kept in SortOrder).
        private static void Write(ApplicationDbContext db, List<ReskRole> roles)
        {
            Dictionary<string, ReskRole> existing = db.ReskRoles.ToDictionary(r => r.Id);
            var ids = new HashSet<string>(roles.Select(r => r.Id));

            db.ReskRoles.RemoveRange(existing.Values.Where(r => !ids.Contains(r.Id)));

            for (int i = 0; i < roles.Count; i++)
            {
                ReskRole role = roles[i];
                role.SortOrder = i;

                if (existing.TryGetValue(role.Id, out ReskRole? row))
                {
                    db.Entry(row).CurrentValues.SetValues(role);
                    row.Permissions = new List<string>(role.Permissions);
                }
                else
                {
                    db.ReskRoles.Add(role);
                }
            }

            db.SaveChanges();
        }
    }


    // One row per user who has a custom role (userId -> roleId).
    public class ReskRoleAssignment
    {
        public string UserId { get; set; } = string.Empty;

        public string RoleId { get; set; } = string.Empty;
    }
}
