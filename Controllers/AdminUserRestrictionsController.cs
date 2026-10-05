using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RESK.WIL.Models;
using RESK.WIL.Services;

namespace RESK.WIL.Controllers
{
    /*
     * =========================================================
     * ADMIN - SUSPEND OR BAN USERS
     * =========================================================
     *
     *   GET  /Admin/Users/{id}/Restrict?mode=suspend|ban   the form
     *   POST /Admin/Users/{id}/Restrict                    apply it
     *   POST /Admin/Users/{id}/Lift                        end a suspension or ban
     *
     * Temporary suspension
     *   The user cannot sign in until the end date (1-90 days, a chosen
     *   date, or until an admin lifts it). It ends by itself on that date.
     *
     * Ban
     *   Permanent. The user cannot sign in until an admin lifts the ban.
     *
     * Both sign the user out of every session straight away.
     */
    [Authorize(Roles = "Admin")]
    public class AdminUserRestrictionsController : Controller
    {
        private const string ViewFolder = "~/Views/AdminUsers/";

        private static readonly int[] AllowedDays = { 0, 1, 3, 7, 14, 30, 90 };

        private readonly UserManager<IdentityUser> _userManager;
        private readonly IWebHostEnvironment _environment;

        public AdminUserRestrictionsController(UserManager<IdentityUser> userManager, IWebHostEnvironment environment)
        {
            _userManager = userManager;
            _environment = environment;
        }

        private string Root => _environment.ContentRootPath;


        // =========================================================
        // FORM
        // =========================================================

        [HttpGet("Admin/Users/{id}/Restrict", Order = -1)]
        public async Task<IActionResult> Restrict(string id, string? mode, string? returnTab)
        {
            IdentityUser? user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                TempData["AdminError"] = "That user could not be found.";
                return Redirect("/Admin/Users");
            }

            if (user.Id == _userManager.GetUserId(User))
            {
                TempData["AdminError"] = "You cannot suspend or ban your own account.";
                return Redirect($"/Admin/Users/{id}");
            }

            ReskAccount account = ReskAccountStore.Load(Root, user.Id);

            if (account.Status == ReskAccountStore.Banned)
            {
                TempData["AdminError"] = "This account is already banned. Lift the ban first if you want to change it.";
                return Redirect($"/Admin/Users/{id}");
            }

            await SetAdminInfoAsync();

            AdminRestrictViewModel model = await BuildModelAsync(user, account);
            model.Mode = mode == "ban" ? "ban" : "suspend";
            model.ReturnTab = CleanTab(returnTab);

            return View(ViewFolder + "Restrict.cshtml", model);
        }


        // =========================================================
        // APPLY
        // =========================================================

        [HttpPost("Admin/Users/{id}/Restrict", Order = -1)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restrict(string id, AdminRestrictForm form)
        {
            IdentityUser? user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                TempData["AdminError"] = "That user could not be found.";
                return Redirect("/Admin/Users");
            }

            if (user.Id == _userManager.GetUserId(User))
            {
                TempData["AdminError"] = "You cannot suspend or ban your own account.";
                return Redirect($"/Admin/Users/{id}");
            }

            ReskAccount account = ReskAccountStore.Load(Root, user.Id);

            string mode = form.Mode == "ban" ? "ban" : "suspend";
            string reason = (form.Reason ?? "").Trim();
            DateTime? untilUtc = null;

            // ---- validate ----
            if (mode == "ban" && reason.Length < 5)
            {
                ModelState.AddModelError(nameof(form.Reason), "Give a reason for the ban (at least 5 characters).");
            }

            if (reason.Length > 500)
            {
                ModelState.AddModelError(nameof(form.Reason), "Keep the reason under 500 characters.");
            }

            if (mode == "suspend")
            {
                if (form.Duration == "custom")
                {
                    if (!DateTime.TryParseExact(form.Until, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime localDate))
                    {
                        ModelState.AddModelError(nameof(form.Until), "Choose the date the suspension ends.");
                    }
                    else
                    {
                        // Ends at 00:00 South African time (UTC+2) on the chosen date.
                        DateTime endUtc = DateTime.SpecifyKind(localDate.Date.AddHours(-2), DateTimeKind.Utc);

                        if (endUtc <= DateTime.UtcNow)
                        {
                            ModelState.AddModelError(nameof(form.Until), "The end date must be in the future.");
                        }
                        else if (endUtc > DateTime.UtcNow.AddYears(1))
                        {
                            ModelState.AddModelError(nameof(form.Until), "A suspension can be at most one year. Use a ban instead.");
                        }
                        else
                        {
                            untilUtc = endUtc;
                        }
                    }
                }
                else if (int.TryParse(form.Duration, out int days) && AllowedDays.Contains(days))
                {
                    untilUtc = days == 0 ? null : DateTime.UtcNow.AddDays(days);
                }
                else
                {
                    ModelState.AddModelError(nameof(form.Duration), "Choose how long the suspension lasts.");
                }
            }

            if (!ModelState.IsValid)
            {
                await SetAdminInfoAsync();

                AdminRestrictViewModel model = await BuildModelAsync(user, account);
                model.Mode = mode;
                model.Duration = form.Duration ?? "7";
                model.Until = form.Until;
                model.Reason = reason;
                model.ReturnTab = CleanTab(form.ReturnTab);

                return View(ViewFolder + "Restrict.cshtml", model);
            }

            // ---- apply ----
            await _userManager.SetLockoutEnabledAsync(user, true);
            await _userManager.SetLockoutEndDateAsync(
                user,
                mode == "suspend" && untilUtc.HasValue
                    ? new DateTimeOffset(untilUtc.Value, TimeSpan.Zero)
                    : DateTimeOffset.MaxValue);

            // Ends every signed-in session for this user.
            await _userManager.UpdateSecurityStampAsync(user);

            string by = User.Identity?.Name ?? "an admin";

            account.Status = mode == "ban" ? ReskAccountStore.Banned : ReskAccountStore.Suspended;
            account.SuspendedUntilUtc = mode == "suspend" ? untilUtc : null;
            account.RestrictionReason = reason.Length == 0 ? null : reason;
            account.RestrictedBy = by;
            account.StatusChangedAtUtc = DateTime.UtcNow;

            string detail = $"By {by}";

            if (mode == "suspend")
            {
                detail += untilUtc.HasValue ? $" • until {LocalText(untilUtc.Value)}" : " • until lifted by an admin";
            }

            if (reason.Length > 0)
            {
                detail += $" • Reason: {reason}";
            }

            ReskAccountStore.Log(
                account,
                mode == "ban" ? "Account banned" : "Account suspended",
                detail,
                "danger");

            ReskAccountStore.Save(Root, account);

            TempData["AdminMessage"] = mode == "ban"
                ? "The user has been banned and signed out. They can no longer sign in."
                : untilUtc.HasValue
                    ? $"The user is suspended until {LocalText(untilUtc.Value)} and has been signed out."
                    : "The user is suspended until you lift it and has been signed out.";

            return Redirect($"/Admin/Users/{id}" + TabQuery(form.ReturnTab));
        }


        // =========================================================
        // LIFT
        // =========================================================

        [HttpPost("Admin/Users/{id}/Lift", Order = -1)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Lift(string id, string? returnTab)
        {
            IdentityUser? user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                TempData["AdminError"] = "That user could not be found.";
                return Redirect("/Admin/Users");
            }

            ReskAccount account = ReskAccountStore.Load(Root, user.Id);

            if (!ReskAccountStore.IsRestricted(account))
            {
                TempData["AdminError"] = "This account is not suspended or banned.";
                return Redirect($"/Admin/Users/{id}" + TabQuery(returnTab));
            }

            bool wasBanned = account.Status == ReskAccountStore.Banned;

            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.ResetAccessFailedCountAsync(user);

            IList<string> roles = await _userManager.GetRolesAsync(user);

            account.Status = roles.Any(r => AdminUsersController.SystemRoles.Contains(r))
                ? ReskAccountStore.Active
                : ReskAccountStore.Pending;
            account.StatusChangedAtUtc = DateTime.UtcNow;
            ReskAccountStore.ClearRestriction(account);
            ReskAccountStore.Log(
                account,
                wasBanned ? "Ban lifted" : "Suspension lifted",
                $"By {User.Identity?.Name}",
                "success");
            ReskAccountStore.Save(Root, account);

            TempData["AdminMessage"] = wasBanned
                ? "The ban was lifted. The user can sign in again."
                : "The suspension was lifted. The user can sign in again.";

            return Redirect($"/Admin/Users/{id}" + TabQuery(returnTab));
        }


        // =========================================================
        // HELPERS
        // =========================================================

        private async Task<AdminRestrictViewModel> BuildModelAsync(IdentityUser user, ReskAccount account)
        {
            ProducerProfile profile = ProducerProfileStore.Load(Root, user.Id);
            IList<string> roles = await _userManager.GetRolesAsync(user);

            string name = ProducerProfileStore.DisplayName(profile, user);
            string? role =
                roles.Contains("Admin") ? "Admin" :
                roles.Contains("Reviewer") ? "Reviewer" :
                roles.Contains("Producer") ? "Producer" : null;

            return new AdminRestrictViewModel
            {
                UserId = user.Id,
                Name = name,
                Email = user.Email ?? user.UserName ?? "",
                Initials = ProducerProfileStore.Initials(name),
                AvatarColour = AvatarColour(user.Id),
                RoleLabel = role == null ? "No role yet" : role == "Admin" ? "Proposal Manager" : role,
                IsAdmin = role == "Admin",
                CurrentRestriction =
                    account.Status == ReskAccountStore.Suspended
                        ? account.SuspendedUntilUtc.HasValue
                            ? $"Currently suspended until {LocalText(account.SuspendedUntilUtc.Value)}. Saving replaces that suspension."
                            : "Currently suspended until lifted. Saving replaces that suspension."
                        : null,
                MinDate = SouthAfricaTime.ToLocal(DateTime.UtcNow).AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                MaxDate = SouthAfricaTime.ToLocal(DateTime.UtcNow).AddYears(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            };
        }


        private async Task SetAdminInfoAsync()
        {
            IdentityUser? me = await _userManager.GetUserAsync(User);
            ProducerProfile profile = me == null ? new ProducerProfile() : ProducerProfileStore.Load(Root, me.Id);

            bool hasName = !string.IsNullOrWhiteSpace(profile.FullName);

            ViewData["AdminName"] = hasName ? profile.FullName.Trim() : "Admin User";
            ViewData["AdminInitials"] = hasName ? ProducerProfileStore.Initials(profile.FullName) : "AD";
        }


        public static string LocalText(DateTime utc)
        {
            return SouthAfricaTime.ToLocal(utc).ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture);
        }


        private static string? CleanTab(string? tab)
        {
            return tab is "permissions" or "proposals" or "activity" or "security" ? tab : null;
        }


        private static string TabQuery(string? tab)
        {
            string? clean = CleanTab(tab);
            return clean == null ? "" : "?tab=" + clean;
        }


        // Same colours as the Users list.
        private static string AvatarColour(string id)
        {
            string[] colours = { "#12b8ce", "#1d3557", "#5b6b85", "#7a8aa8", "#c9444d", "#1f9a66", "#d39a12" };
            int sum = id.Sum(c => c);
            return colours[sum % colours.Length];
        }
    }
}


namespace RESK.WIL.Models
{
    public class AdminRestrictForm
    {
        // "suspend" or "ban"
        public string? Mode { get; set; }

        // "1", "3", "7", "14", "30", "90", "0" (until lifted) or "custom"
        public string? Duration { get; set; }

        // yyyy-MM-dd when Duration is "custom"
        public string? Until { get; set; }

        public string? Reason { get; set; }

        public string? ReturnTab { get; set; }
    }


    public class AdminRestrictViewModel
    {
        public string UserId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string Initials { get; set; } = "";
        public string AvatarColour { get; set; } = "#12b8ce";
        public string RoleLabel { get; set; } = "";
        public bool IsAdmin { get; set; }
        public string? CurrentRestriction { get; set; }

        public string Mode { get; set; } = "suspend";
        public string Duration { get; set; } = "7";
        public string? Until { get; set; }
        public string? Reason { get; set; }
        public string? ReturnTab { get; set; }

        public string MinDate { get; set; } = "";
        public string MaxDate { get; set; } = "";
    }
}