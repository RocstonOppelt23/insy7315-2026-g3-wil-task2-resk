using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace RESK.WIL.Services
{
    /*
     * =========================================================
     * SYSTEM SETTINGS MIDDLEWARE (runs on every request)
     * =========================================================
     *
     * Makes the System settings screens take effect:
     *
     *   Registration  closed registration, approval of new accounts,
     *                 administrator registration key, password rules
     *   Security      lockout after failed sign-ins, sign-out after
     *                 inactivity, password changes that are due
     *   Workflow      reviewer needed before a review, who may record
     *                 the final decision
     *   Roles         one Administrator account, which can't be edited
     */
    public partial class ReskSettingsMiddleware
    {
        private static readonly string[] OwnPages =
        {
            "/account/registrationclosed", "/account/awaitingapproval", "/account/locked", "/account/signedout",
            "/account/restricted", "/account/updatepassword", "/account/myphoto", "/account/me.json"
        };

        private static readonly string[] SignInRoles = { "Admin", "Reviewer", "Producer" };

        // Settings version that was last applied to the password / lockout options.
        private static int _applied;

        private readonly RequestDelegate _next;

        public ReskSettingsMiddleware(RequestDelegate next)
        {
            _next = next;
        }


        public async Task InvokeAsync(HttpContext context, IWebHostEnvironment environment)
        {
            string root = environment.ContentRootPath;
            ReskSettings settings = ReskSettingsStore.Current(root);

            string path = (context.Request.Path.Value ?? "").TrimEnd('/').ToLowerInvariant();
            string last = path.Substring(path.LastIndexOf('/') + 1);
            bool post = HttpMethods.IsPost(context.Request.Method);

            ApplyOptions(context, root, settings);

            bool leaving = last == "logout" || last == "signout" || last == "logoff";

            if (OwnPages.Contains(path) || leaving)
            {
                await _next(context);
                return;
            }

            if (context.User.Identity?.IsAuthenticated == true &&
                await SignedInChecksAsync(context, root, settings, path, post))
            {
                return;
            }

            if (last == "register")
            {
                await RegisterAsync(context, root, settings, post);
                return;
            }

            if (post && (last == "login" || last == "signin"))
            {
                await LoginAsync(context, root, settings);
                return;
            }

            await _next(context);
        }


        // Checks for someone who is signed in. True = the request was answered here.
        private static async Task<bool> SignedInChecksAsync(HttpContext context, string root, ReskSettings settings, string path, bool post)
        {
            string userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

            if (userId.Length == 0)
            {
                return false;
            }

            // ---------- signed out after inactivity ----------
            if (IdleTooLong(context, userId, settings))
            {
                await context.SignOutAsync(IdentityConstants.ApplicationScheme);
                context.Response.Redirect("/Account/SignedOut?minutes=" + settings.SessionTimeoutMinutes);
                return true;
            }

            ReskAccount account = ReskAccountStore.Load(root, userId);

            // ---------- waiting for approval / rejected ----------
            if (account.Status == ReskAccountStore.Pending || account.Status == ReskAccountStore.Rejected)
            {
                await context.SignOutAsync(IdentityConstants.ApplicationScheme);
                context.Response.Redirect("/Account/AwaitingApproval" + (account.Status == ReskAccountStore.Rejected ? "?state=rejected" : ""));
                return true;
            }

            // ---------- password must be changed ----------
            if (PasswordChangeDue(root, account, settings) && !path.EndsWith("/myaccess.js"))
            {
                context.Response.Redirect("/Account/UpdatePassword");
                return true;
            }

            if (path == "/admin" || path.StartsWith("/admin/"))
            {
                return await AdminRulesAsync(context, root, settings, userId, path, post);
            }

            return false;
        }


        // =========================================================
        // SHARED HELPERS
        // =========================================================

        // Password rules and lockout follow the saved settings (again after every save).
        private static void ApplyOptions(HttpContext context, string root, ReskSettings settings)
        {
            int version = ReskSettingsStore.Version;

            if (_applied == version)
            {
                return;
            }

            IOptions<IdentityOptions>? identity = context.RequestServices.GetService<IOptions<IdentityOptions>>();

            if (identity != null)
            {
                ReskSettingsStore.Apply(identity.Value, settings);
            }

            SyncAdminKey(context, root, settings);
            _applied = version;
        }
    }
}