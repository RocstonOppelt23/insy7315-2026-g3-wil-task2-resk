using Microsoft.AspNetCore.Http;

namespace RESK.WIL.Services
{
    // ReskAuditRecorder: users, roles and categories.
    public static partial class ReskAuditRecorder
    {
        // =========================================================
        // USERS
        // =========================================================

        private static async Task UserAsync(HttpContext context, string root, RESK.WIL.Data.ApplicationDbContext db,
            ReskAuditRule rule, ReskAuditBefore before, bool ok)
        {
            ReskAuditState? was = before.Record;
            ReskAuditState? now = await ReskAuditSnapshots.UserAsync(db, root, rule.Key);

            if (was == null && now == null)
            {
                return;
            }

            ReskAuditEvent item = ReskAuditMiddleware.NewEvent(context, root, rule.Module, rule.Action, rule.Type);
            ReskAuditState shown = now ?? was!;
            item.Record = shown.Name;
            item.RecordId = shown.IdLabel;

            if (now == null)
            {
                item.Module = "Users";
                item.Action = "Deleted user account";
                item.Type = "delete";
                item.Summary = $"The account for {shown.Name} was permanently deleted.";
                ReskAuditLog.Add(root, item);
                return;
            }

            item.Changes = ReskAuditSnapshots.Diff(was, now);

            if (item.Changes.Count == 0)
            {
                // Nothing to compare for these: the password itself is never recorded.
                if (ok && rule.Last is "resetpassword" or "changepassword")
                {
                    item.Summary = "The password was changed. The password itself is never recorded.";
                    ReskAuditLog.Add(root, item);
                }
                else if (ok && rule.Last == "signoutall")
                {
                    item.Summary = "Every open session for this user was ended.";
                    ReskAuditLog.Add(root, item);
                }

                return;
            }

            if (was != null && was.Status != now.Status)
            {
                (item.Action, item.Type) = now.Status switch
                {
                    "Active" => was.Status == "Pending" ? ("Approved registration", "approve") : ("Activated user account", "update"),
                    "Suspended" => ("Suspended user account", "update"),
                    "Banned" => ("Banned user account", "update"),
                    "Locked" => ("Locked user account", "update"),
                    "Rejected" => ("Rejected registration", "reject"),
                    _ => (item.Action, item.Type)
                };

                item.Module = "Users";
            }
            else if (item.Changes.Any(c => c.Field == "Role"))
            {
                item.Action = "Changed user role";
            }

            item.Summary = $"Account changed for {now.Name}.";
            ReskAuditLog.Add(root, item);
        }


        // =========================================================
        // ROLES
        // =========================================================

        private static void Role(HttpContext context, string root, ReskAuditRule rule, ReskAuditBefore before)
        {
            ReskAuditState? was = before.Record;
            ReskAuditState? now = ReskAuditSnapshots.Role(root, rule.Key);

            if (was == null && now == null)
            {
                return;
            }

            ReskAuditEvent item = ReskAuditMiddleware.NewEvent(context, root, rule.Module, rule.Action, rule.Type);
            ReskAuditState shown = now ?? was!;
            item.Record = shown.Name;
            item.RecordId = shown.IdLabel;

            if (now == null)
            {
                item.Action = "Deleted role";
                item.Type = "delete";
                item.Summary = $"The {shown.Name} role was deleted.";
                ReskAuditLog.Add(root, item);
                return;
            }

            item.Changes = ReskAuditSnapshots.Diff(was, now);

            if (item.Changes.Count == 0)
            {
                return;
            }

            bool permissions = item.Changes.Any(c => c.Field.Contains(" \u2014 "));

            item.Action =
                permissions ? "Updated role permissions" :
                item.Changes.All(c => c.Field == "Status") ? (now.Status == ReskRoleStore.Active ? "Activated role" : "Deactivated role") :
                "Updated role details";

            item.Type = "update";
            item.Summary = (permissions ? "Permissions" : "Details") + $" changed for the {now.Name} role.";
            ReskAuditLog.Add(root, item);
        }


        private static void NewRoles(HttpContext context, string root, ReskAuditRule rule, ReskAuditBefore before)
        {
            foreach (ReskRole role in ReskRoleStore.All(root))
            {
                bool existed = before.Existing.TryGetValue(role.Id, out string? oldStatus);
                bool finished = existed && oldStatus == ReskRoleStore.Draft && role.Status != ReskRoleStore.Draft;

                if (existed && !finished)
                {
                    continue;
                }

                bool draft = role.Status == ReskRoleStore.Draft;
                ReskAuditState state = ReskAuditSnapshots.Role(role);

                ReskAuditEvent item = ReskAuditMiddleware.NewEvent(context, root, "Roles", draft ? "Saved role draft" : rule.Action, "create");
                item.Record = state.Name;
                item.RecordId = state.IdLabel;
                item.Changes = ReskAuditSnapshots.Diff(null, state);
                item.Summary = draft
                    ? $"The {role.Name} role was saved as a draft. It can't be given to users yet."
                    : $"The {role.Name} role was created with {role.Permissions.Count} permission(s) allowed.";
                ReskAuditLog.Add(root, item);
            }
        }


        // =========================================================
        // CATEGORIES
        // =========================================================

        private static void Category(HttpContext context, string root, ReskAuditRule rule, ReskAuditBefore before)
        {
            ReskAuditState? was = before.Record;
            ReskAuditState? now = CategoriesExist(root) ? ReskAuditSnapshots.Category(root, rule.Key) : null;

            if (was == null && now == null)
            {
                return;
            }

            ReskAuditEvent item = ReskAuditMiddleware.NewEvent(context, root, rule.Module, rule.Action, rule.Type);
            ReskAuditState shown = now ?? was!;
            item.Record = shown.Name;
            item.RecordId = shown.IdLabel;

            if (now == null)
            {
                item.Action = "Deleted category";
                item.Type = "delete";
                item.Summary = $"The {shown.Name} category was deleted.";
                ReskAuditLog.Add(root, item);
                return;
            }

            item.Changes = ReskAuditSnapshots.Diff(was, now);

            if (item.Changes.Count == 0)
            {
                return;
            }

            item.Action =
                item.Changes.All(c => c.Field == "Status") ? (now.Status == ReskCategoryStore.Active ? "Activated category" : "Deactivated category") :
                item.Changes.All(c => c.Field == "Shown on the proposal form") ? "Changed category visibility" :
                "Updated category";

            item.Type = "update";
            item.Summary = $"The {now.Name} category was changed.";
            ReskAuditLog.Add(root, item);
        }


        private static void NewCategories(HttpContext context, string root, ReskAuditRule rule, ReskAuditBefore before)
        {
            if (before.Unknown || !CategoriesExist(root))
            {
                return;
            }

            foreach (ReskCategory category in ReskCategoryStore.All(root).Where(c => !before.Existing.ContainsKey(c.Id.ToString())))
            {
                ReskAuditState state = ReskAuditSnapshots.Category(category);

                ReskAuditEvent item = ReskAuditMiddleware.NewEvent(context, root, "Categories", rule.Action, "create");
                item.Record = state.Name;
                item.RecordId = state.IdLabel;
                item.Changes = ReskAuditSnapshots.Diff(null, state);
                item.Summary = $"The {category.Name} category was created.";
                ReskAuditLog.Add(root, item);
            }
        }
    }
}