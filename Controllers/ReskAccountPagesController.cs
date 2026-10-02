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
     * ACCOUNT PAGES THAT THE SYSTEM SETTINGS NEED
     * =========================================================
     *
     *   GET  /Account/RegistrationClosed   registration is switched off
     *   GET  /Account/AwaitingApproval     the account is not approved yet
     *   GET  /Account/Locked               too many failed sign-ins
     *   GET  /Account/SignedOut            signed out after inactivity
     *   GET/POST /Account/UpdatePassword   choose a new password
     */
    public class ReskAccountPagesController : Controller
    {
        private const string NoticeView = "~/Views/Account/ReskNotice.cshtml";
        private const string PasswordView = "~/Views/Account/ReskPassword.cshtml";

        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly IWebHostEnvironment _environment;

        public ReskAccountPagesController(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            IWebHostEnvironment environment)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _environment = environment;
        }

        private string Root => _environment.ContentRootPath;


        [HttpGet("Account/RegistrationClosed", Order = -1)]
        public IActionResult RegistrationClosed()
        {
            return Notice("Registration is closed",
                "New accounts can't be created at the moment. If you need access, please ask the station to create an account for you.",
                "amber");
        }


        [HttpGet("Account/AwaitingApproval", Order = -1)]
        public IActionResult AwaitingApproval(string? state)
        {
            ReskSettings settings = ReskSettingsStore.Current(Root);

            if (state == "rejected")
            {
                return Notice("Your registration was not approved",
                    "This account can't be used to sign in. If you think this is a mistake, please contact the station.", "red");
            }

            string days = settings.ApprovalWindowDays == 1 ? "1 working day" : settings.ApprovalWindowDays + " working days";

            return Notice("Your account is waiting for approval",
                $"An administrator reviews every new account, usually within {days}. You'll be able to sign in as soon as yours is approved.",
                "teal");
        }


        [HttpGet("Account/Locked", Order = -1)]
        public IActionResult Locked(int minutes = 15)
        {
            minutes = Math.Clamp(minutes, 1, 120);

            return Notice("Too many failed sign-in attempts",
                $"To protect this account, signing in is paused. Please try again in about {minutes} {(minutes == 1 ? "minute" : "minutes")}.",
                "red");
        }


        [HttpGet("Account/SignedOut", Order = -1)]
        public IActionResult SignedOut(int minutes = 30)
        {
            minutes = Math.Clamp(minutes, 1, 1440);
            string time = minutes % 60 == 0 ? (minutes / 60 == 1 ? "1 hour" : minutes / 60 + " hours") : minutes == 1 ? "1 minute" : minutes + " minutes";

            return Notice("You were signed out",
                $"For security, you are signed out after {time} without activity. Sign in again to carry on.", "teal");
        }


        private IActionResult Notice(string title, string message, string tone)
        {
            ReskSettings settings = ReskSettingsStore.Current(Root);

            return View(NoticeView, new ReskNoticeViewModel
            {
                Title = title,
                Message = message,
                Tone = tone,
                Organisation = settings.OrganisationName,
                SupportEmail = settings.SupportEmail,
                ContactNumber = settings.ContactNumber
            });
        }


        // =========================================================
        // CHOOSE A NEW PASSWORD
        // =========================================================

        [Authorize]
        [HttpGet("Account/UpdatePassword", Order = -1)]
        public async Task<IActionResult> UpdatePassword()
        {
            IdentityUser? user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Redirect("/Account/Login");
            }

            return View(PasswordView, PasswordModel(user));
        }


        [Authorize]
        [HttpPost("Account/UpdatePassword", Order = -1)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePassword(string? currentPassword, string? newPassword, string? confirmPassword)
        {
            IdentityUser? user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Redirect("/Account/Login");
            }

            ReskPasswordViewModel model = PasswordModel(user);

            if (string.IsNullOrEmpty(currentPassword) || string.IsNullOrEmpty(newPassword))
            {
                model.Errors.Add("Enter your current password and a new password.");
            }
            else if (newPassword != confirmPassword)
            {
                model.Errors.Add("The new password and the confirmation don't match.");
            }
            else if (newPassword == currentPassword)
            {
                model.Errors.Add("The new password must be different from the current one.");
            }
            else
            {
                IdentityResult result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);

                if (result.Succeeded)
                {
                    ReskAccount account = ReskAccountStore.Load(Root, user.Id);
                    account.RequirePasswordChange = false;
                    account.PasswordChangedAtUtc = DateTime.UtcNow;
                    ReskAccountStore.Log(account, "Password changed", "By the user", "success");
                    ReskAccountStore.Save(Root, account);

                    await _signInManager.RefreshSignInAsync(user);

                    return Redirect(User.IsInRole("Admin") ? "/Admin" : User.IsInRole("Producer") ? "/Producer" : "/");
                }

                model.Errors.AddRange(result.Errors.Select(e => e.Code == "PasswordMismatch" ? "The current password is not correct." : e.Description));
            }

            return View(PasswordView, model);
        }


        private ReskPasswordViewModel PasswordModel(IdentityUser user)
        {
            ReskSettings settings = ReskSettingsStore.Current(Root);
            ReskAccount account = ReskAccountStore.Load(Root, user.Id);

            return new ReskPasswordViewModel
            {
                Email = user.Email ?? user.UserName ?? "",
                Rule = ReskSettingsStore.PasswordRule(settings),
                MinLength = settings.PasswordMinLength,
                Required = ReskSettingsMiddleware.PasswordChangeDue(Root, account, settings),
                Reason = account.RequirePasswordChange
                    ? "Your password was set by an administrator. Choose your own password to carry on."
                    : $"Passwords must be changed every {settings.PasswordExpiryDays} days. Choose a new password to carry on."
            };
        }
    }
}