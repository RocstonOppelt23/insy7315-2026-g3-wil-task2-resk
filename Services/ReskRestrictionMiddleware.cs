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
     * SUSPENDED / BANNED USERS
     * =========================================================
     *
     * Runs on every request. If the signed-in user is suspended or
     * banned they are signed out at once and sent to
     * /Account/Restricted, which explains why they can't continue.
     *
     * (Identity lockout already stops them signing in again.)
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