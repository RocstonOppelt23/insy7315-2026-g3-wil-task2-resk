using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RESK.WIL.Models;
using RESK.WIL.Services;

namespace RESK.WIL.Controllers
{
    /*
     * =========================================================
     * ADMIN - EDIT USER (details, assigned role, account status)
     * =========================================================
     *
     *   GET/POST /Admin/Users/{id}/Edit
     *
     * Order = -2 so this page is used instead of the older Edit user page.
     * The role list comes from Roles & permissions (including custom roles).
     */
    [Authorize(Roles = "Admin")]
    public partial class AdminUserAccessController : Controller
    {
        private static readonly string[] SignInRoles = { "Producer", "Reviewer", "Admin" };

        private readonly RESK.WIL.Data.ApplicationDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IWebHostEnvironment _environment;

        public AdminUserAccessController(
            RESK.WIL.Data.ApplicationDbContext db,
            UserManager<IdentityUser> userManager,
            IWebHostEnvironment environment)
        {
            _db = db;
            _userManager = userManager;
            _environment = environment;
        }

        private string Root => _environment.ContentRootPath;


        [HttpGet("Admin/Users/{id}/Edit", Order = -2)]
        public async Task<IActionResult> Edit(string id)
        {
            IdentityUser? user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                TempData["AdminError"] = "That user could not be found.";
                return Redirect("/Admin/Users");
            }

            ReskUserAccessViewModel model = await BuildAsync(user);
            ProducerProfile profile = ProducerProfileStore.Load(Root, user.Id);

            model.Form = new ReskUserAccessForm
            {
                FullName = string.IsNullOrWhiteSpace(profile.FullName) ? "" : profile.FullName,
                Email = user.Email ?? "",
                Phone = user.PhoneNumber,
                Organisation = profile.Organisation,
                RoleId = model.CurrentRoleId,
                AccountStatus = model.CurrentStatus
            };

            return View("~/Views/AdminUsers/EditAccess.cshtml", model);
        }


        [HttpPost("Admin/Users/{id}/Edit", Order = -2)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, ReskUserAccessForm form)
        {
            IdentityUser? user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                TempData["AdminError"] = "That user could not be found.";
                return Redirect("/Admin/Users");
            }

            ReskUserAccessViewModel model = await BuildAsync(user);
            List<ReskRole> roles = ReskRoleStore.All(Root);

            form.FullName = (form.FullName ?? "").Trim();
            form.Email = (form.Email ?? "").Trim();
            form.Phone = string.IsNullOrWhiteSpace(form.Phone) ? null : form.Phone.Trim();
            form.Organisation = string.IsNullOrWhiteSpace(form.Organisation) ? null : form.Organisation.Trim();

            ReskRole? newRole = roles.FirstOrDefault(r => r.Id == form.RoleId);

            // ---------- validation ----------
            if (newRole == null)
            {
                ModelState.AddModelError(nameof(form.RoleId), "Choose a role.");
            }
            else if (newRole.Id != model.CurrentRoleId && !newRole.IsActive)
            {
                ModelState.AddModelError(nameof(form.RoleId), "This role is not active yet. Activate it in Roles & permissions first.");
            }

            if (model.IsSelf && form.RoleId != model.CurrentRoleId)
            {
                ModelState.AddModelError(nameof(form.RoleId), "You can't change your own role. Ask another administrator.");
            }

            if (model.IsSelf && form.AccountStatus != model.CurrentStatus)
            {
                ModelState.AddModelError(nameof(form.AccountStatus), "You can't suspend or ban your own account.");
            }

            if (!model.IsPending && form.AccountStatus is not ("Active" or "Suspended" or "Banned"))
            {
                ModelState.AddModelError(nameof(form.AccountStatus), "Choose an account status.");
            }

            // Keep at least one active Administrator.
            if (model.CurrentRoleId == ReskRoleStore.AdministratorId &&
                (form.RoleId != ReskRoleStore.AdministratorId || form.AccountStatus != "Active") &&
                await ActiveAdministratorsAsync(roles) <= 1)
            {
                ModelState.AddModelError(nameof(form.RoleId), "This is the only active Administrator. Make someone else an Administrator first.");
            }

            if (ModelState.IsValid && !string.Equals(form.Email, user.Email, StringComparison.OrdinalIgnoreCase))
            {
                IdentityUser? other = await _userManager.FindByEmailAsync(form.Email);

                if (other != null && other.Id != user.Id)
                {
                    ModelState.AddModelError(nameof(form.Email), "Another account already uses this email address.");
                }
            }

            if (!ModelState.IsValid)
            {
                model.Form = form;
                return View("~/Views/AdminUsers/EditAccess.cshtml", model);
            }

            var changes = new List<string>();
            ReskAccount account = ReskAccountStore.Load(Root, user.Id);
            string by = User.Identity?.Name ?? "Admin";

            // ---------- details ----------
            if (!string.Equals(form.Email, user.Email, StringComparison.OrdinalIgnoreCase))
            {
                string oldEmail = user.Email ?? "";
                await _userManager.SetEmailAsync(user, form.Email);
                string token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                await _userManager.ConfirmEmailAsync(user, token);

                if (string.Equals(user.UserName, oldEmail, StringComparison.OrdinalIgnoreCase))
                {
                    await _userManager.SetUserNameAsync(user, form.Email);
                }

                changes.Add("email");
            }

            if ((form.Phone ?? "") != (user.PhoneNumber ?? ""))
            {
                await _userManager.SetPhoneNumberAsync(user, form.Phone);
                changes.Add("phone number");
            }

            ProducerProfile profile = ProducerProfileStore.Load(Root, user.Id);

            if (profile.FullName != form.FullName || (profile.Organisation ?? "") != (form.Organisation ?? ""))
            {
                profile.FullName = form.FullName;
                profile.Organisation = form.Organisation ?? "";
                ProducerProfileStore.Save(Root, user.Id, profile);
                changes.Add("name / organisation");
            }

            // ---------- role ----------
            if (newRole!.Id != model.CurrentRoleId)
            {
                IList<string> current = await _userManager.GetRolesAsync(user);

                if (model.IsPending)
                {
                    // Given when the registration is approved.
                    account.RequestedRole = newRole.Area;
                }
                else
                {
                    // Exactly one sign-in role: the new role's access area.
                    List<string> remove = current.Where(r => SignInRoles.Contains(r) && r != newRole.Area).ToList();

                    if (remove.Count > 0)
                    {
                        await _userManager.RemoveFromRolesAsync(user, remove);
                    }

                    if (!current.Contains(newRole.Area))
                    {
                        await _userManager.AddToRoleAsync(user, newRole.Area);
                    }
                }

                ReskRoleStore.Assign(Root, user.Id, newRole.Id);
                await _userManager.UpdateSecurityStampAsync(user);

                ReskAccountStore.Log(account, "Role changed", $"By {by} • {RoleName(roles, model.CurrentRoleId)} → {newRole.Name}", "warning");
                changes.Add("role → " + newRole.Name);
            }

            // ---------- account status ----------
            if (!model.IsPending && form.AccountStatus != model.CurrentStatus)
            {
                if (form.AccountStatus == "Active")
                {
                    await _userManager.SetLockoutEndDateAsync(user, null);
                    await _userManager.ResetAccessFailedCountAsync(user);
                    account.Status = ReskAccountStore.Active;
                    ReskAccountStore.ClearRestriction(account);
                    ReskAccountStore.Log(account, "Account reactivated", $"By {by} • from Edit user", "success");
                }
                else
                {
                    await _userManager.SetLockoutEnabledAsync(user, true);
                    await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
                    await _userManager.UpdateSecurityStampAsync(user);

                    account.Status = form.AccountStatus == "Banned" ? ReskAccountStore.Banned : ReskAccountStore.Suspended;
                    account.SuspendedUntilUtc = null;
                    account.RestrictionReason = "Set from Edit user";
                    account.RestrictedBy = by;
                    ReskAccountStore.Log(account, form.AccountStatus == "Banned" ? "Account banned" : "Account suspended",
                        $"By {by} • from Edit user • until lifted", "danger");
                }

                account.StatusChangedAtUtc = DateTime.UtcNow;
                changes.Add("status → " + form.AccountStatus.ToLowerInvariant());
            }

            if (changes.Any(c => !c.StartsWith("role") && !c.StartsWith("status")))
            {
                ReskAccountStore.Log(account, "Account details updated",
                    $"By {by} • {string.Join(", ", changes.Where(c => !c.StartsWith("role") && !c.StartsWith("status")))}", "info");
            }

            ReskAccountStore.Save(Root, account);

            TempData["AdminMessage"] = changes.Count == 0
                ? "No changes were made."
                : "Saved: " + string.Join(", ", changes) + ".";

            return Redirect($"/Admin/Users/{user.Id}");
        }
    }
}