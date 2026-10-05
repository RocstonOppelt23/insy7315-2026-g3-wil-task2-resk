using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using RESK.WIL.Models;

namespace RESK.WIL.Services
{
    /*
     * =========================================================
     * AUDIT MIDDLEWARE (runs on every request)
     * =========================================================
     *
     * 1) Works out whether the request is one the audit log records
     *    (ReskAuditRules) and reads the record BEFORE the change.
     * 2) Lets the request run as normal.
     * 3) Reads the record AFTER the change and saves one audit event
     *    with what changed (ReskAuditRecorder).
     *
     * Nothing here can stop a page from working: any problem while
     * recording is ignored and the request carries on.
     */
    public class ReskAuditMiddleware
    {
        private const string CookieName = "resk.sid";
        private const string SessionItem = "ReskAuditSession";

        private readonly RequestDelegate _next;

        public ReskAuditMiddleware(RequestDelegate next)
        {
            _next = next;
        }


        public async Task InvokeAsync(HttpContext context, IWebHostEnvironment environment)
        {
            string root = environment.ContentRootPath;
            ReskAuditRule? rule = null;
            ReskAuditBefore? before = null;

            try
            {
                EnsureSession(context);
                rule = ReskAuditRules.Match(context);

                if (rule != null)
                {
                    before = await ReskAuditRecorder.BeforeAsync(context, root, rule);
                }
            }
            catch (Exception)
            {
                // The audit log must never break a page.
                rule = null;
            }

            await _next(context);

            try
            {
                if (Blocked(context, root))
                {
                    return;
                }

                if (rule != null && before != null)
                {
                    await ReskAuditRecorder.FinishAsync(context, root, rule, before);
                }
            }
            catch (Exception)
            {
                // Same as above: recording problems are ignored.
            }
        }


        // =========================================================
        // BLOCKED REQUESTS (security events)
        // =========================================================

        // Records a request that was stopped by a role or by a suspension / ban.
        private static bool Blocked(HttpContext context, string root)
        {
            int code = context.Response.StatusCode;
            string location = context.Response.Headers["Location"].ToString();
            string path = context.Request.Path.Value ?? "";
            bool redirected = code >= 300 && code < 400;

            if (redirected && location.StartsWith("/Admin/NoAccess", StringComparison.OrdinalIgnoreCase))
            {
                ReskAuditEvent item = NewEvent(context, root, "Security", "Blocked: role does not allow this", "failed");
                item.Result = ReskAuditLog.Failed;
                item.Record = context.Request.Method + " " + Shorten(path, 90);
                item.RecordId = "Permission needed: " + Query(location, "module") + " / " + Query(location, "need");
                item.Summary = "The request was stopped because the user's role does not have this permission.";
                ReskAuditLog.Add(root, item);
                return true;
            }

            if (redirected && location.StartsWith("/Account/Restricted", StringComparison.OrdinalIgnoreCase))
            {
                ReskAuditEvent item = NewEvent(context, root, "Security", "Blocked: account suspended or banned", "failed");
                item.Result = ReskAuditLog.Failed;
                item.Record = item.UserEmail ?? item.UserName;
                item.RecordId = "Account " + Query(location, "type");
                item.Summary = "A suspended or banned account tried to use the system and was signed out.";
                ReskAuditLog.Add(root, item);
                return true;
            }

            return false;
        }


        private static string Query(string url, string name)
        {
            int start = url.IndexOf(name + "=", StringComparison.OrdinalIgnoreCase);

            if (start < 0)
            {
                return "\u2014";
            }

            start += name.Length + 1;
            int end = url.IndexOf('&', start);
            string value = Uri.UnescapeDataString(end < 0 ? url.Substring(start) : url.Substring(start, end - start));

            return value.Length == 0 ? "\u2014" : value;
        }


        // =========================================================
        // SHARED BY THE RECORDER
        // =========================================================

        // A new event with who / where / device already filled in.
        public static ReskAuditEvent NewEvent(HttpContext context, string root, string module, string action, string type)
        {
            var item = new ReskAuditEvent
            {
                AtUtc = DateTime.UtcNow,
                Module = module,
                Action = action,
                Type = type,
                Source = ReskAuditRules.Source(context),
                SessionId = context.Items[SessionItem] as string,
                Ip = ReskAuditRules.Ip(context),
                Device = ReskAuditRules.Device(context),
                Request = context.Request.Method + " " + Shorten(context.Request.Path.Value ?? "", 150)
            };

            ClaimsPrincipal user = context.User;

            if (user.Identity?.IsAuthenticated == true)
            {
                var roles = new[] { "Admin", "Reviewer", "Producer" }.Where(user.IsInRole).ToList();

                FillUser(item, root,
                    user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "",
                    user.FindFirstValue(ClaimTypes.Email) ?? user.Identity.Name ?? "",
                    roles);
            }

            return item;
        }


        public static void FillUser(ReskAuditEvent item, string root, string userId, string email, List<string> roles)
        {
            ProducerProfile profile = userId.Length == 0 ? new ProducerProfile() : ProducerProfileStore.Load(root, userId);

            item.UserId = userId;
            item.UserEmail = email;

            item.UserName =
                !string.IsNullOrWhiteSpace(profile.FullName) ? profile.FullName.Trim() :
                roles.Contains("Admin") ? "Admin User" :
                email.Length > 0 ? email : "User";

            item.UserRole = userId.Length == 0
                ? null
                : ReskRoleStore.RoleFor(root, userId, roles)?.Name ?? roles.FirstOrDefault() ?? "No role yet";
        }


        public static string Shorten(string text, int max)
        {
            return text.Length <= max ? text : text.Substring(0, max - 1) + "\u2026";
        }


        // A short id for the browser session, shown as "Session ID" on the details page.
        private static void EnsureSession(HttpContext context)
        {
            string? value = context.Request.Cookies[CookieName];

            if (value == null || value.Length != 6 || !value.All(char.IsDigit))
            {
                value = Random.Shared.Next(100000, 1000000).ToString();

                context.Response.Cookies.Append(CookieName, value, new CookieOptions
                {
                    HttpOnly = true,
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    Path = "/"
                });
            }

            context.Items[SessionItem] = "SES-" + value;
        }
    }
}