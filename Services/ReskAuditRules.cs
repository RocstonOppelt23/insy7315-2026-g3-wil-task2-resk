using Microsoft.AspNetCore.Http;

namespace RESK.WIL.Services
{
    // What the audit middleware should record for one request.
    public class ReskAuditRule
    {
        // user, user-create, role, roles, category, categories, proposals,
        // login, logout, register, export, generic
        public string Kind { get; set; } = "generic";

        public string Module { get; set; } = "Settings";

        public string Action { get; set; } = string.Empty;

        public string Type { get; set; } = "update";

        // Record id taken from the URL (user id, role id, category id).
        public string Key { get; set; } = string.Empty;

        // Last part of the URL, lower case ("edit", "delete", ...).
        public string Last { get; set; } = string.Empty;

        // Producer pages only look at the signed-in producer's own proposals.
        public bool OwnOnly { get; set; }
    }


    /*
     * =========================================================
     * AUDIT RULES: which requests are recorded, and as what
     * =========================================================
     *
     * Pages that only show information are not recorded.
     * Recorded: sign-in / sign-out, every change made on the admin
     * screens, proposal workflow steps and CSV downloads.
     */
    public static partial class ReskAuditRules
    {
        public static ReskAuditRule? Match(HttpContext context)
        {
            string path = (context.Request.Path.Value ?? "").TrimEnd('/');
            string[] raw = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (raw.Length == 0)
            {
                return null;
            }

            string[] parts = raw.Select(p => p.ToLowerInvariant()).ToArray();
            string first = parts[0];
            string last = parts[^1];
            bool post = HttpMethods.IsPost(context.Request.Method);
            bool signedIn = context.User.Identity?.IsAuthenticated == true;

            // ---------- sign in / out / register (any controller) ----------
            if (post && (last == "login" || last == "signin"))
            {
                return Rule("login", "Security", "Signed in", "signin", last);
            }

            if (post && (last == "logout" || last == "signout" || last == "logoff"))
            {
                return signedIn ? Rule("logout", "Security", "Signed out", "signin", last) : null;
            }

            if (post && last == "register")
            {
                return Rule("register", "Users", "Registered a new account", "create", last);
            }

            if (first == "admin")
            {
                return Admin(parts, raw, post);
            }

            if (!post)
            {
                return null;
            }

            if (last.Contains("forgotpassword"))
            {
                return Rule("generic", "Security", "Requested a password reset", "update", last);
            }

            if (last.Contains("resetpassword"))
            {
                return Rule("generic", "Security", "Reset password", "update", last);
            }

            if (!signedIn)
            {
                return null;
            }

            if (first == "producer")
            {
                string userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";

                switch (last)
                {
                    case "settings":
                        return Rule("user", "Users", "Updated own profile", "update", last, userId);
                    case "changepassword":
                        return Rule("user", "Security", "Changed own password", "update", last, userId);
                    case "requestposition":
                        return Rule("user", "Users", "Requested a position change", "update", last, userId);
                }

                // Everything else on the producer side: only proposal changes are recorded.
                ReskAuditRule own = Rule("proposals", "Proposals", "", "update", last);
                own.OwnOnly = true;
                return own;
            }

            // Reviewer screens (and anything else that saves): proposal changes,
            // or the name of the action when no proposal changed.
            string fallback = first == "reviewer" || first == "account" ? Humanise(raw[^1]) : "";

            return Rule("proposals", first == "account" ? "Security" : "Proposals", fallback, "update", last);
        }


        private static ReskAuditRule? Admin(string[] parts, string[] raw, bool post)
        {
            string section = parts.Length > 1 ? parts[1] : "";
            string last = parts[^1];
            string key = parts.Length > 3 ? raw[2] : "";

            if (!post)
            {
                // CSV downloads
                if (last == "export" && parts.Length == 3)
                {
                    string what = section switch
                    {
                        "users" => "Exported users list",
                        "proposals" => "Exported proposals list",
                        "audit" or "auditlog" => "Exported audit log",
                        _ => "Exported " + section
                    };

                    return Rule("export", "Reports", what, "export", last);
                }

                return null;
            }

            switch (section)
            {
                case "users":
                    if (last == "create")
                    {
                        return Rule("user-create", "Users", "Created user account", "create", last);
                    }

                    (string module, string action, string type) = last switch
                    {
                        "edit" => ("Users", "Updated user account", "update"),
                        "pending" => ("Users", "Reviewed registration", "approve"),
                        "resetpassword" => ("Security", "Reset user password", "update"),
                        "signoutall" => ("Security", "Signed user out of all devices", "update"),
                        "lock" => ("Users", "Locked user account", "update"),
                        "unlock" => ("Users", "Unlocked user account", "update"),
                        "suspend" or "restrict" => ("Users", "Suspended user account", "update"),
                        "reactivate" or "lift" => ("Users", "Activated user account", "update"),
                        "delete" => ("Users", "Deleted user account", "delete"),
                        _ => ("Users", Humanise(raw[^1]) + " (user)", "update")
                    };

                    return Rule("user", module, action, type, last, key);

                case "proposals":
                    return Rule("proposals", "Proposals", last == "review" ? "Saved proposal review notes" : "", "update", last, key);

                case "categories":
                    if (last == "create")
                    {
                        return Rule("categories", "Categories", "Created category", "create", last);
                    }

                    return Rule("category", "Categories", last == "delete" ? "Deleted category" : "Updated category",
                        last == "delete" ? "delete" : "update", last, key);

                case "roles":
                    if (last == "create" || last == "duplicate")
                    {
                        return Rule("roles", "Roles", last == "duplicate" ? "Duplicated role" : "Created role", "create", last);
                    }

                    return Rule("role", "Roles", last == "delete" ? "Deleted role" : "Updated role",
                        last == "delete" ? "delete" : "update", last, key);

                case "reports":
                    return last == "export" ? Rule("export", "Reports", "Exported report", "export", last) : null;

                case "settings":
                case "systemsettings":
                    return Rule("generic", "Settings", "Updated system settings", "update", last);

                default:
                    return Rule("generic", "Settings", Humanise(raw[^1]), "update", last);
            }
        }


        private static ReskAuditRule Rule(string kind, string module, string action, string type, string last, string key = "")
        {
            return new ReskAuditRule { Kind = kind, Module = module, Action = action, Type = type, Last = last, Key = key };
        }
    }
}