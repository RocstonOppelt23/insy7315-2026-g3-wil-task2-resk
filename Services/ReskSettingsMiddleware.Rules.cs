using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;

namespace RESK.WIL.Services
{
    // ReskSettingsMiddleware: rules for the admin screens, and small helpers.
    public partial class ReskSettingsMiddleware
    {
        // Things nobody may do to the Administrator account.
        private static readonly string[] Protected =
        {
            "edit", "restrict", "suspend", "lock", "unlock", "reactivate", "lift", "delete", "pending"
        };


        // True = the request was answered here.
        private static async Task<bool> AdminRulesAsync(HttpContext context, string root, ReskSettings settings, string userId, string path, bool post)
        {
            await ChooseAdministratorAsync(context, root, userId);

            string[] parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            string section = parts.Length > 1 ? parts[1] : "";
            string last = parts[^1];

            // ---------- the Administrator account is fixed ----------
            if (section == "users" && parts.Length == 4 && Protected.Contains(last))
            {
                string? owner = ReskRoleStore.AdministratorUserId(root);

                if (owner != null && string.Equals(parts[2], owner, StringComparison.OrdinalIgnoreCase))
                {
                    Flash(context, "AdminError", "The Administrator account is fixed. Its profile, role and status can't be changed.");
                    context.Response.Redirect("/Admin/Users/" + owner);
                    return true;
                }
            }

            // ---------- workflow rules ----------
            if (section == "proposals" && parts.Length == 4 && int.TryParse(parts[2], out int proposalId) && (last == "review" || last == "confirm"))
            {
                if (settings.RequireAssignment && !ProposalReviewStore.Load(root, proposalId).HasReviewer)
                {
                    Flash(context, "AdminMessage", "Assign a reviewer first. Reviews can only start once a reviewer is assigned (System settings, Workflow).");
                    context.Response.Redirect($"/Admin/Proposals/{proposalId}/Assign");
                    return true;
                }

                if (last == "confirm")
                {
                    ReskRole? role = ReskRoleStore.RoleFor(root, userId, new[] { "Admin" });

                    if (role != null && !role.IsAdministrator && role.Id != settings.FinalDecisionRole)
                    {
                        string needed = ReskRoleStore.Get(root, settings.FinalDecisionRole)?.Name ?? "the Administrator";

                        Flash(context, "AdminError", $"Only the {needed} role can record the final decision (System settings, Workflow).");
                        context.Response.Redirect($"/Admin/Proposals/{proposalId}");
                        return true;
                    }
                }
            }

            return false;
        }


        // Runs once: picks the single account that keeps the Administrator role.
        private static async Task ChooseAdministratorAsync(HttpContext context, string root, string userId)
        {
            if (ReskRoleStore.AdministratorUserId(root) != null || !context.User.IsInRole("Admin"))
            {
                return;
            }

            RESK.WIL.Data.ApplicationDbContext? db = context.RequestServices.GetService<RESK.WIL.Data.ApplicationDbContext>();

            if (db == null)
            {
                return;
            }

            var admins = await (
                from link in db.Set<IdentityUserRole<string>>().AsNoTracking()
                join role in db.Set<IdentityRole>().AsNoTracking() on link.RoleId equals role.Id
                join user in db.Set<IdentityUser>().AsNoTracking() on link.UserId equals user.Id
                where role.Name == "Admin"
                select new { user.Id, Email = user.Email ?? user.UserName ?? "" }).ToListAsync();

            // Only accounts that are Administrators today (not Proposal Managers, Viewers, ...).
            Dictionary<string, string> assigned = ReskRoleStore.Assignments(root);
            var candidates = admins
                .Where(a => !assigned.TryGetValue(a.Id, out string? roleId) || roleId == ReskRoleStore.AdministratorId)
                .OrderBy(a => a.Email)
                .ToList();

            if (candidates.Count == 0)
            {
                return;
            }

            // 1) the admin account named in the app's configuration (appsettings / user-secrets)
            IConfiguration? configuration = context.RequestServices.GetService<IConfiguration>();
            var values = new HashSet<string>(
                configuration?.AsEnumerable().Where(p => !string.IsNullOrWhiteSpace(p.Value)).Select(p => p.Value!) ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);

            var chosen = candidates.FirstOrDefault(a => values.Contains(a.Email))
                         // 2) the administrator who is using the system right now
                         ?? candidates.FirstOrDefault(a => a.Id == userId)
                         // 3) the first one
                         ?? candidates[0];

            ReskRoleStore.SetAdministrator(root, chosen.Id, chosen.Email);
        }


        // =========================================================
        // SMALL HELPERS (used by every part)
        // =========================================================

        // A message for the next admin page (the green / red bar at the top).
        private static void Flash(HttpContext context, string key, string message)
        {
            ITempDataDictionaryFactory? factory = context.RequestServices.GetService<ITempDataDictionaryFactory>();

            if (factory == null)
            {
                return;
            }

            ITempDataDictionary data = factory.GetTempData(context);
            data[key] = message;
            data.Save();
        }


        // A value from a simple posted form (never used for file uploads).
        private static async Task<string> FormValueAsync(HttpContext context, params string[] keys)
        {
            IFormCollection? form = await SimpleFormAsync(context);

            if (form == null)
            {
                return "";
            }

            foreach (string key in keys)
            {
                if (form.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    return value.ToString().Trim();
                }
            }

            return "";
        }


        private static async Task<IFormCollection?> SimpleFormAsync(HttpContext context)
        {
            string type = context.Request.ContentType ?? "";

            if (!type.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            try
            {
                return await context.Request.ReadFormAsync();
            }
            catch (InvalidDataException)
            {
                return null;
            }
        }


        // True when the response carries a new sign-in cookie.
        private static bool SetsLoginCookie(HttpContext context)
        {
            int code = context.Response.StatusCode;

            if (code < 300 || code >= 400)
            {
                return false;
            }

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
    }
}