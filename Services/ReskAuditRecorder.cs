using Microsoft.AspNetCore.Http;

namespace RESK.WIL.Services
{
    // What the records looked like before the request ran.
    public class ReskAuditBefore
    {
        // One user, role or category.
        public ReskAuditState? Record { get; set; }

        // Role / category ids (and their status) that already existed.
        public Dictionary<string, string> Existing { get; set; } = new();

        // True when that list could not be read (so nothing is reported as new).
        public bool Unknown { get; set; }

        public Dictionary<int, ReskAuditState>? Proposals { get; set; }
    }


    /*
     * =========================================================
     * AUDIT RECORDER: turns a finished request into audit events
     * =========================================================
     *
     * Passwords and other secrets are never written to the log:
     * only the names of fields and their before / after values.
     */
    public static partial class ReskAuditRecorder
    {
        public static async Task<ReskAuditBefore> BeforeAsync(HttpContext context, string root, ReskAuditRule rule)
        {
            var before = new ReskAuditBefore();
            RESK.WIL.Data.ApplicationDbContext? db = context.RequestServices.GetService<RESK.WIL.Data.ApplicationDbContext>();

            switch (rule.Kind)
            {
                case "role":
                    before.Record = ReskAuditSnapshots.Role(root, rule.Key);
                    break;

                case "roles":
                    before.Existing = ReskRoleStore.All(root).ToDictionary(r => r.Id, r => r.Status);
                    break;

                case "category":
                    before.Record = CategoriesExist(root) ? ReskAuditSnapshots.Category(root, rule.Key) : null;
                    break;

                case "categories":
                    before.Unknown = !CategoriesExist(root);

                    if (!before.Unknown)
                    {
                        before.Existing = ReskCategoryStore.All(root).ToDictionary(c => c.Id.ToString(), c => c.Status);
                    }

                    break;

                case "user":
                    before.Record = db == null ? null : await ReskAuditSnapshots.UserAsync(db, root, rule.Key);
                    break;

                case "proposals":
                    if (db != null)
                    {
                        before.Proposals = await ReskAuditSnapshots.ProposalsAsync(db, root, rule.OwnOnly ? UserId(context) : null);
                    }

                    break;
            }

            return before;
        }


        public static async Task FinishAsync(HttpContext context, string root, ReskAuditRule rule, ReskAuditBefore before)
        {
            int code = context.Response.StatusCode;
            bool redirected = code >= 300 && code < 400;
            bool failed = code >= 400 || HasTempError(context);
            RESK.WIL.Data.ApplicationDbContext? db = context.RequestServices.GetService<RESK.WIL.Data.ApplicationDbContext>();

            switch (rule.Kind)
            {
                case "login":
                    await LoginAsync(context, root, db, redirected);
                    break;

                case "logout":
                    if (code < 400)
                    {
                        ReskAuditEvent item = ReskAuditMiddleware.NewEvent(context, root, rule.Module, rule.Action, rule.Type);
                        item.Record = item.UserEmail ?? item.UserName;
                        item.RecordId = "Session ended";
                        item.Summary = "The user signed out.";
                        ReskAuditLog.Add(root, item);
                    }

                    break;

                case "register":
                    if (redirected && !failed)
                    {
                        ReskAuditEvent item = ReskAuditMiddleware.NewEvent(context, root, rule.Module, rule.Action, rule.Type);
                        string email = FormValue(context, "Email", "Input.Email", "email") ?? "New account";
                        item.UserName = FormValue(context, "FullName", "Input.FullName", "Name") ?? email;
                        item.UserEmail = email;
                        item.Record = email;
                        item.RecordId = "Self-registration";
                        item.Summary = "A new account was registered from the sign-up page.";
                        ReskAuditLog.Add(root, item);
                    }

                    break;

                case "export":
                    Export(context, root, rule, code);
                    break;

                case "generic":
                    if (redirected && !failed)
                    {
                        ReskAuditEvent item = ReskAuditMiddleware.NewEvent(context, root, rule.Module, rule.Action, rule.Type);
                        item.Record = item.Source;
                        ReskAuditLog.Add(root, item);
                    }

                    break;

                case "user-create":
                    if (redirected && !failed)
                    {
                        ReskAuditEvent item = ReskAuditMiddleware.NewEvent(context, root, rule.Module, rule.Action, rule.Type);
                        string email = FormValue(context, "Email") ?? "";
                        item.Record = FormValue(context, "FullName") ?? (email.Length > 0 ? email : "New user");
                        item.RecordId = "Email: " + (email.Length > 0 ? email : "\u2014");
                        item.Summary = "A new user account was created by an administrator.";
                        AddFormChange(item, context, "Full name", "FullName");
                        AddFormChange(item, context, "Email", "Email");
                        AddFormChange(item, context, "Organisation", "Organisation");
                        AddFormChange(item, context, "Role", "Role");
                        AddFormChange(item, context, "Account status", "Status");
                        ReskAuditLog.Add(root, item);
                    }

                    break;

                case "user":
                    if (db != null)
                    {
                        await UserAsync(context, root, db, rule, before, redirected && !failed);
                    }

                    break;

                case "role":
                    Role(context, root, rule, before);
                    break;

                case "roles":
                    NewRoles(context, root, rule, before);
                    break;

                case "category":
                    Category(context, root, rule, before);
                    break;

                case "categories":
                    NewCategories(context, root, rule, before);
                    break;

                case "proposals":
                    if (db != null && before.Proposals != null)
                    {
                        await ProposalsAsync(context, root, db, rule, before.Proposals, redirected && !failed);
                    }

                    break;
            }
        }
    }
}