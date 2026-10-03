using Microsoft.AspNetCore.Identity;
using RESK.WIL.Models;
using RESK.WIL.Services;

namespace RESK.WIL.Controllers
{
    public partial class AdminUserAccessController
    {
        private async Task<ReskUserAccessViewModel> BuildAsync(IdentityUser user)
        {
            await SetAdminInfoAsync();

            List<ReskRole> roles = ReskRoleStore.All(Root);
            IList<string> identityRoles = await _userManager.GetRolesAsync(user);
            ReskAccount account = ReskAccountStore.Load(Root, user.Id);
            bool pending = !identityRoles.Any(r => SignInRoles.Contains(r));

            ReskRole? current = pending
                ? (ReskRoleStore.Get(Root, ReskRoleStore.Assignments(Root).GetValueOrDefault(user.Id))
                   ?? ReskRoleStore.Get(Root, ReskRoleStore.DefaultRoleId(account.RequestedRole ?? "Producer")))
                : ReskRoleStore.RoleFor(Root, user.Id, identityRoles, roles);

            string name = ProducerProfileStore.DisplayName(ProducerProfileStore.Load(Root, user.Id), user);

            var model = new ReskUserAccessViewModel
            {
                UserId = user.Id,
                DisplayName = name,
                IsSelf = user.Id == _userManager.GetUserId(User),
                IsPending = pending,
                CurrentRoleId = current?.Id ?? ReskRoleStore.ProducerId,
                CurrentStatus = pending ? "Pending" :
                    account.Status == ReskAccountStore.Banned ? "Banned" :
                    account.Status == ReskAccountStore.Suspended ? "Suspended" : "Active",
                Roles = roles
                    .Where(r => r.Status != ReskRoleStore.Draft && (!r.IsAdministrator || ReskRoleStore.IsAdministratorAccount(Root, user.Id)))
                    .OrderBy(r => r.Area == "Admin" ? 0 : r.Area == "Reviewer" ? 1 : 2)
                    .ThenBy(r => r.Name)
                    .Select(r => (r.Id, r.Name, ReskRoleStore.AreaLabel(r.Area), r.IsActive))
                    .ToList()
            };

            foreach (ReskRole r in roles)
            {
                model.Previews[r.Id] = PreviewLines(r);
            }

            return model;
        }


        // Plain-English lines for "Role permissions preview".
        private static List<string> PreviewLines(ReskRole role)
        {
            if (role.Area == "Producer")
            {
                return new List<string>
                {
                    "Create and edit own proposals",
                    "View own proposal status",
                    "Manage drafts",
                    "Update personal profile"
                };
            }

            var lines = new List<string>();

            if (role.Area == "Reviewer")
            {
                lines.Add("Signs in to the reviewer workspace");
            }

            if (role.IsAdministrator)
            {
                lines.Add("Full access to every admin screen and action");
                return lines;
            }

            foreach (var module in ReskRoleStore.Modules)
            {
                string summary = ReskRoleStore.Summary(role, module.Key);

                if (summary != "No access")
                {
                    lines.Add($"{module.Name}: {summary}");
                }
            }

            if (lines.Count == 0 || (role.Area == "Reviewer" && lines.Count == 1))
            {
                lines.Add("No permissions selected yet");
            }

            return lines;
        }


        private async Task<int> ActiveAdministratorsAsync(List<ReskRole> roles)
        {
            IList<IdentityUser> admins = await _userManager.GetUsersInRoleAsync("Admin");
            Dictionary<string, string> assignments = ReskRoleStore.Assignments(Root);

            return admins.Count(a =>
                ReskRoleStore.RoleFor(Root, a.Id, new[] { "Admin" }, roles, assignments)?.Id == ReskRoleStore.AdministratorId &&
                !ReskAccountStore.IsRestricted(ReskAccountStore.Load(Root, a.Id)));
        }


        // Role name shown in the Users list (custom roles included).
        public static string RoleLabel(string contentRoot, string userId, string identityRole)
        {
            return ReskRoleStore.RoleFor(contentRoot, userId, new[] { identityRole })?.Name ?? identityRole;
        }


        // Rows for the "Permissions" tab on the user's page. Null when the user has no role yet.
        public static List<AdminPermissionRow>? PermissionRows(string contentRoot, string userId, string? identityRole)
        {
            if (string.IsNullOrEmpty(identityRole))
            {
                return null;
            }

            ReskRole? role = ReskRoleStore.RoleFor(contentRoot, userId, new[] { identityRole });

            if (role == null)
            {
                return null;
            }

            return ReskRoleStore.Modules
                .Select(m => new AdminPermissionRow(
                    m.Name,
                    role.Can(m.Key, "view") && m.Actions.Contains("view"),
                    role.Can(m.Key, "create") && m.Actions.Contains("create"),
                    role.Can(m.Key, "edit") && m.Actions.Contains("edit"),
                    role.Can(m.Key, "delete") && m.Actions.Contains("delete"),
                    role.Can(m.Key, "approve") && m.Actions.Contains("approve"),
                    role.Can(m.Key, "export") && m.Actions.Contains("export")))
                .ToList();
        }


        private static string RoleName(List<ReskRole> roles, string id)
        {
            return roles.FirstOrDefault(r => r.Id == id)?.Name ?? "No role";
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