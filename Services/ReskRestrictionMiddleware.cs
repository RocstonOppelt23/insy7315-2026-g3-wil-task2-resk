using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace RESK.WIL.Services
{
    /*
     * =========================================================
     * ACCESS CHECKS (runs on every request)
     * =========================================================
     *
     * 1) Suspended / banned users are signed out at once and sent to
     *    /Account/Restricted.
     *
     * 2) Admin-area users: every /Admin page is checked against the
     *    permission matrix of their role (Roles & permissions).
     *    Not allowed -> /Admin/NoAccess (or 403 for background calls).
     */
    public class ReskRestrictionMiddleware
    {
        private readonly RequestDelegate _next;

        public ReskRestrictionMiddleware(RequestDelegate next)
        {
            _next = next;
        }


        public async Task InvokeAsync(HttpContext context, IWebHostEnvironment environment)
        {
            if (context.User.Identity?.IsAuthenticated == true &&
                !context.Request.Path.StartsWithSegments("/Account/Restricted"))
            {
                string? userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (!string.IsNullOrEmpty(userId))
                {
                    // ---------- 1) suspended / banned ----------
                    ReskAccount account = ReskAccountStore.Load(environment.ContentRootPath, userId);

                    if (ReskAccountStore.IsRestricted(account))
                    {
                        await context.SignOutAsync(IdentityConstants.ApplicationScheme);

                        try
                        {
                            context.Session.Clear();
                        }
                        catch (InvalidOperationException)
                        {
                            // Session isn't configured - nothing to clear.
                        }

                        string type = account.Status == ReskAccountStore.Banned ? "banned" : "suspended";
                        context.Response.Redirect("/Account/Restricted?type=" + type);
                        return;
                    }

                    // ---------- 2) role permissions on /Admin ----------
                    if (context.User.IsInRole("Admin") &&
                        context.Request.Path.StartsWithSegments("/Admin"))
                    {
                        // Moved to a non-admin role but still holding an old admin login:
                        // sign out so the new role applies at the next sign-in.
                        if (ReskRoleStore.Assignments(environment.ContentRootPath).TryGetValue(userId, out string? assignedId))
                        {
                            ReskRole? assigned = ReskRoleStore.Get(environment.ContentRootPath, assignedId);

                            if (assigned != null && assigned.Area != "Admin")
                            {
                                await context.SignOutAsync(IdentityConstants.ApplicationScheme);
                                context.Response.Redirect("/Account/Login");
                                return;
                            }
                        }

                        var need = ReskRoleStore.Requirement(context.Request.Path.Value ?? "", context.Request.Method);

                        if (need.HasValue)
                        {
                            ReskRole? role = ReskRoleStore.RoleFor(environment.ContentRootPath, userId, new[] { "Admin" });

                            if (role != null && !role.Can(need.Value.Module, need.Value.Action))
                            {
                                bool background =
                                    context.Request.Path.Value!.EndsWith("/Preview", StringComparison.OrdinalIgnoreCase) ||
                                    context.Request.Headers["Accept"].ToString().Contains("application/json");

                                if (background)
                                {
                                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                                    return;
                                }

                                context.Response.Redirect(
                                    "/Admin/NoAccess?module=" + Uri.EscapeDataString(need.Value.Module) +
                                    "&need=" + Uri.EscapeDataString(need.Value.Action));
                                return;
                            }
                        }
                    }
                }
            }

            await _next(context);
        }
    }


    public static class ReskRestrictionMiddlewareExtensions
    {
        public static IApplicationBuilder UseReskRestrictions(this IApplicationBuilder app)
        {
            return app.UseMiddleware<ReskRestrictionMiddleware>();
        }
    }
}