using System.Text.Json;

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
     * Saved as JSON (no database change needed):
     *   App_Data/Roles.json            the roles
     *   App_Data/RoleAssignments.json  userId -> roleId
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

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };
        private static readonly object FileLock = new object();


        // =========================================================
        // ROLES
        // =========================================================

        public static List<ReskRole> All(string contentRoot)
        {
            lock (FileLock)
            {
                string path = RolesPath(contentRoot);
                List<ReskRole>? roles = null;

                try
                {
                    if (File.Exists(path))
                    {
                        roles = JsonSerializer.Deserialize<List<ReskRole>>(File.ReadAllText(path));
                    }
                }
                catch (IOException)
                {
                }
                catch (JsonException)
                {
                    File.Copy(path, path + ".broken-" + DateTime.UtcNow.Ticks, true);
                }

                if (roles == null || roles.Count == 0)
                {
                    roles = Seed();
                    Write(path, roles);
                }

                // The Administrator role always exists, active, with full access.
                ReskRole? admin = roles.FirstOrDefault(r => r.Id == AdministratorId);

                if (admin == null)
                {
                    roles.Insert(0, Seed()[0]);
                    Write(path, roles);
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


        public static void SaveAll(string contentRoot, List<ReskRole> roles)
        {
            lock (FileLock)
            {
                Write(RolesPath(contentRoot), roles);
            }
        }


        // =========================================================
        // ASSIGNMENTS (userId -> roleId)
        // =========================================================

        public static Dictionary<string, string> Assignments(string contentRoot)
        {
            lock (FileLock)
            {
                try
                {
                    string path = AssignmentsPath(contentRoot);

                    if (File.Exists(path))
                    {
                        return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))
                               ?? new Dictionary<string, string>();
                    }
                }
                catch (IOException)
                {
                }
                catch (JsonException)
                {
                }

                return new Dictionary<string, string>();
            }
        }


        public static void Assign(string contentRoot, string userId, string? roleId)
        {
            Dictionary<string, string> map = Assignments(contentRoot);

            if (string.IsNullOrEmpty(roleId))
            {
                map.Remove(userId);
            }
            else
            {
                map[userId] = roleId;
            }

            lock (FileLock)
            {
                Write(AssignmentsPath(contentRoot), map);
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


        private static void Write<T>(string path, T value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));
        }


        private static string RolesPath(string contentRoot) => Path.Combine(contentRoot, "App_Data", "Roles.json");

        private static string AssignmentsPath(string contentRoot) => Path.Combine(contentRoot, "App_Data", "RoleAssignments.json");
    }
}