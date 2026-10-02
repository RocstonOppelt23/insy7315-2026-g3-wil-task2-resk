namespace RESK.WIL.Services
{
    // Second half of ReskRoleStore: which permission each admin page needs, and text helpers.
    public static partial class ReskRoleStore
    {
        // =========================================================
        // WHICH PERMISSION A PAGE NEEDS
        // =========================================================

        // Returns (module, action) for an /Admin URL, or null when every admin may open it.
        public static (string Module, string Action)? Requirement(string path, string method)
        {
            string p = path.TrimEnd('/').ToLowerInvariant();
            bool post = string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase);

            if (!p.StartsWith("/admin/") || p.StartsWith("/admin/noaccess") || p == "/admin/roles/myaccess.js")
            {
                return null;
            }

            string[] parts = p.Split('/', StringSplitOptions.RemoveEmptyEntries);
            string section = parts.Length > 1 ? parts[1] : "";
            string last = parts[^1];

            switch (section)
            {
                case "users":
                    if (last == "export") return ("users", "export");
                    if (last == "create") return ("users", "create");
                    if (last == "pending") return ("users", post ? "approve" : "view");
                    if (last == "delete") return ("users", "delete");
                    if (last == "edit" || last == "restrict" || post) return ("users", "edit");
                    return ("users", "view");

                case "proposals":
                    if (last == "export") return ("proposals", "export");
                    if (last == "confirm") return ("proposals", post ? "approve" : "view");
                    if (last == "assign" || last == "review" || post) return ("proposals", "edit");
                    return ("proposals", "view");

                case "categories":
                    if (last == "create") return ("categories", "create");
                    if (last == "delete") return ("categories", "delete");
                    if (last == "edit" || post) return ("categories", "edit");
                    return ("categories", "view");

                case "reports":
                    if (parts.Length > 2 && parts[2] == "export") return ("reports", "export");
                    return ("reports", "view");

                case "roles":
                    if (last == "create" || last == "duplicate") return ("roles", "create");
                    if (last == "delete") return ("roles", "delete");
                    if (last == "edit" || post) return ("roles", "edit");
                    return ("roles", "view");

                case "audit":
                case "auditlog":
                    return ("audit", "view");

                case "settings":
                case "systemsettings":
                    return ("settings", post ? "edit" : "view");

                default:
                    return null;
            }
        }


        // =========================================================
        // TEXT HELPERS
        // =========================================================

        public static List<string> AllPermissions()
        {
            return Modules.SelectMany(m => m.Actions.Select(a => m.Key + ":" + a)).ToList();
        }


        public static List<string> Clean(IEnumerable<string>? permissions)
        {
            var valid = new HashSet<string>(AllPermissions());
            return (permissions ?? Enumerable.Empty<string>()).Where(valid.Contains).Distinct().ToList();
        }


        // "View, Edit, Approve" or "No access"
        public static string Summary(ReskRole role, string module)
        {
            var labels = Actions.Where(a => role.Can(module, a.Key) && Modules.First(m => m.Key == module).Actions.Contains(a.Key))
                                .Select(a => a.Label)
                                .ToList();

            return labels.Count == 0 ? "No access" : string.Join(", ", labels);
        }


        public static string Initials(string name)
        {
            string[] words = (name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string initials = words.Length >= 2 ? $"{words[0][0]}{words[1][0]}" : words.Length == 1 ? words[0].Substring(0, Math.Min(2, words[0].Length)) : "R";
            return initials.ToUpperInvariant();
        }


        public static string Colour(ReskRole role)
        {
            if (role.IsAdministrator) return "#12b8ce";
            if (role.Status == Draft) return "#9aa7ba";
            return role.Area switch
            {
                "Admin" => role.Template == "viewer" ? "#8a97ad" : "#1d3557",
                "Reviewer" => "#5b6b85",
                _ => "#7a8aa8"
            };
        }


        public static string AreaLabel(string area)
        {
            return Areas.FirstOrDefault(a => a.Key == area).Label ?? area;
        }


        // Permissions a template starts with.
        public static List<string> TemplatePermissions(string template)
        {
            return template switch
            {
                "manager" => new List<string>
                {
                    "users:view",
                    "proposals:view", "proposals:create", "proposals:edit", "proposals:delete", "proposals:approve", "proposals:export",
                    "categories:view", "categories:create", "categories:edit", "categories:delete",
                    "reports:view", "reports:export"
                },
                "reviewer" => new List<string> { "proposals:view", "proposals:edit", "proposals:approve", "categories:view" },
                "viewer" => new List<string> { "users:view", "proposals:view", "categories:view", "reports:view", "roles:view" },
                _ => new List<string>()
            };
        }


        public static string TemplateArea(string template)
        {
            return template == "reviewer" ? "Reviewer" : "Admin";
        }


        // The built-in roles (written the first time).
        private static List<ReskRole> Seed()
        {
            return new List<ReskRole>
            {
                new ReskRole
                {
                    Id = AdministratorId, Name = "Administrator", Area = "Admin", IsSystem = true, Template = "custom",
                    Description = "Full control of users, proposals, categories, reports, roles, audit records and system settings.",
                    Permissions = AllPermissions(), CreatedBy = "System", UpdatedBy = "System"
                },
                new ReskRole
                {
                    Id = ManagerId, Name = "Proposal Manager", Area = "Admin", IsSystem = true, Template = "manager",
                    Description = "Manages proposals, categories, reports and workflow decisions.",
                    Permissions = TemplatePermissions("manager"), CreatedBy = "System", UpdatedBy = "System"
                },
                new ReskRole
                {
                    Id = ReviewerId, Name = "Reviewer", Area = "Reviewer", IsSystem = true, Template = "reviewer",
                    Description = "Reviews assigned proposals and submits recommendations.",
                    Permissions = TemplatePermissions("reviewer"), CreatedBy = "System", UpdatedBy = "System"
                },
                new ReskRole
                {
                    Id = ProducerId, Name = "Producer", Area = "Producer", IsSystem = true, Template = "custom",
                    Description = "Creates, saves and submits their own programme proposals.",
                    Permissions = new List<string> { "proposals:view", "proposals:create", "proposals:edit", "categories:view" },
                    CreatedBy = "System", UpdatedBy = "System"
                },
                new ReskRole
                {
                    Id = ViewerId, Name = "Viewer", Area = "Admin", IsSystem = true, Template = "viewer",
                    Description = "Read-only access to authorised information without creating or editing data.",
                    Permissions = TemplatePermissions("viewer"), CreatedBy = "System", UpdatedBy = "System"
                }
            };
        }
    }
}