using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace RESK.WIL.Services
{
    // ReskAuditRecorder: sign-in attempts and CSV downloads.
    public static partial class ReskAuditRecorder
    {
        // =========================================================
        // SIGN-IN
        // =========================================================

        private static async Task LoginAsync(HttpContext context, string root, RESK.WIL.Data.ApplicationDbContext? db, bool redirected)
        {
            string email = FormValue(context, "Email", "Input.Email", "email", "UserName", "Username", "Input.UserName") ?? "";
            bool signedIn = redirected && SetsLoginCookie(context);

            if (!signedIn && email.Length == 0)
            {
                return;
            }

            IdentityUser? account = null;

            if (db != null && email.Length > 0)
            {
                string normalised = email.ToUpperInvariant();

                account = await db.Set<IdentityUser>().AsNoTracking()
                    .FirstOrDefaultAsync(u => u.NormalizedEmail == normalised || u.NormalizedUserName == normalised);
            }

            ReskAuditEvent item = ReskAuditMiddleware.NewEvent(context, root, "Security", "Signed in", "signin");
            item.Record = email.Length > 0 ? ReskAuditMiddleware.Shorten(email, 100) : "Unknown account";

            if (signedIn)
            {
                if (account != null && db != null)
                {
                    List<string> roles = await ReskAuditSnapshots.RoleNamesAsync(db, account.Id);
                    ReskAuditMiddleware.FillUser(item, root, account.Id, account.Email ?? email, roles);
                }

                item.RecordId = "Session started";
                item.Summary = "The user signed in with the correct password.";
            }
            else
            {
                item.UserId = null;
                item.UserName = "Unknown user";
                item.UserEmail = null;
                item.UserRole = null;
                item.Action = "Failed login attempt";
                item.Type = "failed";
                item.Result = ReskAuditLog.Failed;
                item.RecordId = account == null ? "No matching account" : "Existing account";
                item.Summary = account == null
                    ? "No account uses this email address."
                    : "Wrong password, or the account is locked, suspended or waiting for approval.";
            }

            ReskAuditLog.Add(root, item);
        }


        // True when the response carries a new sign-in cookie.
        private static bool SetsLoginCookie(HttpContext context)
        {
            foreach (string? cookie in context.Response.Headers["Set-Cookie"])
            {
                if (cookie == null)
                {
                    continue;
                }

                int equals = cookie.IndexOf('=');
                int end = cookie.IndexOf(';');

                if (equals <= 0)
                {
                    continue;
                }

                string name = cookie.Substring(0, equals);
                int length = (end < 0 ? cookie.Length : end) - equals - 1;

                bool other = name.Contains("Antiforgery", StringComparison.OrdinalIgnoreCase) ||
                             name.Contains("Session", StringComparison.OrdinalIgnoreCase) ||
                             name.Contains("TempData", StringComparison.OrdinalIgnoreCase);

                if (!other && length > 100)
                {
                    return true;
                }
            }

            return false;
        }


        // =========================================================
        // CSV DOWNLOADS
        // =========================================================

        private static void Export(HttpContext context, string root, ReskAuditRule rule, int code)
        {
            string header = context.Response.Headers["Content-Disposition"].ToString();

            if (code != 200 || header.Length == 0)
            {
                return;
            }

            string file = "CSV file";

            if (ContentDispositionHeaderValue.TryParse(header, out ContentDispositionHeaderValue? parsed))
            {
                string name = (parsed.FileNameStar.HasValue ? parsed.FileNameStar.Value : parsed.FileName.Value) ?? "";
                file = name.Trim('"').Length > 0 ? name.Trim('"') : file;
            }

            ReskAuditEvent item = ReskAuditMiddleware.NewEvent(context, root, rule.Module, rule.Action, rule.Type);
            item.Record = file;
            item.RecordId = "CSV download";
            item.Summary = "A CSV file was downloaded. It may contain personal information.";
            ReskAuditLog.Add(root, item);
        }
    }
}