using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RESK.WIL.Models;
using RESK.WIL.Services;

namespace RESK.WIL.Controllers
{
    public partial class AdminRolesController
    {
        // =========================================================
        // NO ACCESS + SIDEBAR SCRIPT
        // =========================================================

        [HttpGet("Admin/NoAccess", Order = -1)]
        public async Task<IActionResult> NoAccess(string? module, string? need)
        {
            await SetAdminInfoAsync();

            ReskRole? mine = await MyRoleAsync(ReskRoleStore.All(Root));
            string moduleName = ReskRoleStore.Modules.FirstOrDefault(m => m.Key == module).Name ?? "this page";
            string actionName = ReskRoleStore.Actions.FirstOrDefault(a => a.Key == need).Label ?? "open";

            ViewData["ModuleName"] = moduleName;
            ViewData["ActionName"] = actionName.ToLowerInvariant();
            ViewData["RoleName"] = mine?.Name ?? "your role";

            return View(ViewFolder + "NoAccess.cshtml");
        }


        [HttpGet("Admin/Roles/MyAccess.js", Order = -1)]
        [ResponseCache(NoStore = true)]
        public async Task<IActionResult> MyAccess()
        {
            ReskRole? mine = await MyRoleAsync(ReskRoleStore.All(Root));
            var hidden = new List<string>();

            if (mine != null && !mine.IsAdministrator)
            {
                var labels = new Dictionary<string, string[]>
                {
                    ["users"] = new[] { "users" },
                    ["proposals"] = new[] { "proposals" },
                    ["categories"] = new[] { "categories" },
                    ["reports"] = new[] { "reports" },
                    ["roles"] = new[] { "roles & permissions", "roles and permissions", "roles" },
                    ["audit"] = new[] { "audit log" },
                    ["settings"] = new[] { "system settings", "settings" }
                };

                foreach (var pair in labels.Where(p => !mine.Can(p.Key, "view")))
                {
                    hidden.AddRange(pair.Value);
                }
            }

            var js = new StringBuilder();
            js.Append("(function(){var hide=[");
            js.Append(string.Join(",", hidden.Select(h => "\"" + h + "\"")));
            js.Append("];if(!hide.length)return;document.querySelectorAll('.sidebar a, aside a, nav a').forEach(function(a){");
            js.Append("var t=(a.textContent||'').replace(/\\s+/g,' ').trim().toLowerCase();if(hide.indexOf(t)>=0){a.style.display='none';}});})();");

            return Content(js.ToString(), "application/javascript");
        }


        // =========================================================
        // SHARED HELPERS
        // =========================================================

        // roleId -> names of the users who have it
        private async Task<Dictionary<string, List<string>>> HoldersAsync(List<ReskRole> roles)
        {
            List<IdentityUser> users = await _db.Set<IdentityUser>().AsNoTracking().ToListAsync();

            Dictionary<string, string> roleNames =
                (await _db.Set<IdentityRole>().AsNoTracking().ToListAsync()).ToDictionary(r => r.Id, r => r.Name ?? "");

            ILookup<string, string> userRoles =
                (await _db.Set<IdentityUserRole<string>>().AsNoTracking().ToListAsync())
                    .ToLookup(l => l.UserId, l => roleNames.TryGetValue(l.RoleId, out string? n) ? n : "");

            Dictionary<string, string> assignments = ReskRoleStore.Assignments(Root);
            var result = new Dictionary<string, List<string>>();

            foreach (IdentityUser user in users)
            {
                ReskRole? role = ReskRoleStore.RoleFor(Root, user.Id, userRoles[user.Id], roles, assignments);

                if (role == null)
                {
                    continue;
                }

                if (!result.TryGetValue(role.Id, out List<string>? list))
                {
                    list = new List<string>();
                    result[role.Id] = list;
                }

                list.Add(ProducerProfileStore.DisplayName(ProducerProfileStore.Load(Root, user.Id), user));
            }

            return result;
        }


        private async Task<ReskRole?> MyRoleAsync(List<ReskRole> roles)
        {
            IdentityUser? me = await _userManager.GetUserAsync(User);

            if (me == null)
            {
                return null;
            }

            IList<string> identityRoles = await _userManager.GetRolesAsync(me);
            return ReskRoleStore.RoleFor(Root, me.Id, identityRoles, roles);
        }


        // Checks a change to an existing role. Returns an error message or null.
        private async Task<string?> ValidateChangeAsync(ReskRole role, List<ReskRole> roles, string? name, string? status, string? description, List<string>? perms)
        {
            if (role.IsAdministrator)
            {
                return "The Administrator role always has full access and can't be changed. Duplicate it to make a variation.";
            }

            name = (name ?? "").Trim();
            description = (description ?? "").Trim();

            if (name.Length < 2 || name.Length > 60)
            {
                return "The role name must be 2 to 60 characters.";
            }

            if (roles.Any(r => r != role && string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                return "Another role already uses this name.";
            }

            if (description.Length > 200)
            {
                return "Keep the description under 200 characters.";
            }

            if (status != ReskRoleStore.Active && status != ReskRoleStore.Inactive && status != ReskRoleStore.Draft)
            {
                return "Choose a valid status.";
            }

            if (role.IsSystem && status != ReskRoleStore.Active && role.Id is ReskRoleStore.ReviewerId or ReskRoleStore.ProducerId)
            {
                return $"The built-in {role.Name} role must stay active.";
            }

            // Don't let an admin lock themselves out of this page.
            ReskRole? mine = await MyRoleAsync(roles);
            List<string> clean = ReskRoleStore.Clean(perms);

            if (mine?.Id == role.Id && (!clean.Contains("roles:view") || !clean.Contains("roles:edit") || status != ReskRoleStore.Active))
            {
                return "This is your own role. Keep it active with Roles & permissions View and Edit, or you would lock yourself out.";
            }

            return null;
        }


        private void ApplyChange(ReskRole role, string name, string status, string description, List<string>? perms)
        {
            role.Name = name.Trim();
            role.Status = status;
            role.Description = description.Trim();
            role.Permissions = ReskRoleStore.Clean(perms);
            role.UpdatedAtUtc = DateTime.UtcNow;
            role.UpdatedBy = User.Identity?.Name ?? "Admin";
        }


        private static string Shorten(string text, int max)
        {
            text = (text ?? "").Trim();
            return text.Length <= max ? text : text.Substring(0, max - 1).TrimEnd() + "…";
        }


        private IActionResult NotFoundRedirect()
        {
            TempData["AdminError"] = "That role could not be found.";
            return Redirect("/Admin/Roles");
        }


        private async Task SetAdminInfoAsync()
        {
            IdentityUser? me = await _userManager.GetUserAsync(User);
            ProducerProfile profile = me == null ? new ProducerProfile() : ProducerProfileStore.Load(Root, me.Id);

            bool hasName = !string.IsNullOrWhiteSpace(profile.FullName);

            ViewData["AdminName"] = hasName ? profile.FullName.Trim() : "Admin User";
            ViewData["AdminInitials"] = hasName ? ProducerProfileStore.Initials(profile.FullName) : "AD";
        }
    }
}