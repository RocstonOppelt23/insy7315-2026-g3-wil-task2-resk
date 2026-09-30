using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RESK.WIL.Models;
using RESK.WIL.Services;

namespace RESK.WIL.Controllers
{
    /*
     *   GET  /Producer/Settings         settings page
     *   POST /Producer/Settings         save profile, language, notifications, photo
     *   POST /Producer/ChangePassword   Security tab
     *   POST /Producer/RequestPosition  "Request position change"
     *   GET  /Producer/ProfilePhoto     the producer's own photo
     */
    [Authorize(Roles = "Producer")]
    [Route("Producer")]
    public class ProducerSettingsController : Controller
    {
        private const string ViewPath = "~/Views/Producer/Settings.cshtml";

        private const long MaxPhotoBytes = 5 * 1024 * 1024;

        private static readonly string[] Tabs =
        {
            "profile", "language", "notifications", "security", "account"
        };

        private static readonly List<string> Positions = new() { "Reviewer", "Administrator" };

        private static readonly List<string> ContactMethods = new() { "Email", "Phone", "WhatsApp" };

        private static readonly List<string> Languages = new()
        {
            "English", "Afrikaans", "isiZulu", "isiXhosa", "Sesotho", "Setswana"
        };

        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly IWebHostEnvironment _environment;

        public ProducerSettingsController(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            IWebHostEnvironment environment)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _environment = environment;
        }

        private string Root => _environment.ContentRootPath;


        // ================= SETTINGS PAGE =================

        [HttpGet("Settings")]
        public async Task<IActionResult> Settings(string? tab)
        {
            IdentityUser? user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            ProducerProfile profile = ProducerProfileStore.Load(Root, user.Id);

            var form = new ProducerSettingsForm
            {
                FullName = profile.FullName,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber,
                Organisation = profile.Organisation,
                PreferredContactMethod = profile.PreferredContactMethod,
                PreferredLanguage = profile.PreferredLanguage,
                NotifyStatusChanges = profile.NotifyStatusChanges,
                NotifyReviewerComments = profile.NotifyReviewerComments,
                NotifyWeeklySummary = profile.NotifyWeeklySummary
            };

            ProducerSettingsViewModel model = BuildModel(user, profile, form, tab);

            model.Message = TempData["SettingsMessage"] as string;
            model.ErrorMessage = TempData["SettingsError"] as string;

            return View(ViewPath, model);
        }


        [HttpPost("Settings")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(6_000_000)]
        [RequestFormLimits(MultipartBodyLengthLimit = 6_000_000)]
        public async Task<IActionResult> Settings(ProducerSettingsForm form)
        {
            IdentityUser? user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            ProducerProfile profile = ProducerProfileStore.Load(Root, user.Id);

            form.FullName = (form.FullName ?? string.Empty).Trim();
            form.Email = (form.Email ?? string.Empty).Trim();
            form.PhoneNumber = string.IsNullOrWhiteSpace(form.PhoneNumber) ? null : form.PhoneNumber.Trim();
            form.Organisation = string.IsNullOrWhiteSpace(form.Organisation) ? null : form.Organisation.Trim();

            if (!ContactMethods.Contains(form.PreferredContactMethod ?? string.Empty))
            {
                ModelState.AddModelError(nameof(form.PreferredContactMethod), "Choose a contact method.");
            }

            if (!Languages.Contains(form.PreferredLanguage ?? string.Empty))
            {
                ModelState.AddModelError(nameof(form.PreferredLanguage), "Choose a language.");
            }

            string? photoExtension = null;

            if (form.Photo != null && form.Photo.Length > 0)
            {
                photoExtension = await CheckPhotoAsync(form.Photo);
            }

            bool emailChanged = !string.Equals(form.Email, user.Email, StringComparison.OrdinalIgnoreCase);

            if (emailChanged && ModelState.IsValid)
            {
                IdentityUser? other = await _userManager.FindByEmailAsync(form.Email);

                if (other != null && other.Id != user.Id)
                {
                    ModelState.AddModelError(nameof(form.Email), "Another account already uses this email address.");
                }
            }

            if (!ModelState.IsValid)
            {
                return ShowWithErrors(user, profile, form);
            }

            // ---------- Email (also the login name) ----------
            if (emailChanged)
            {
                string oldEmail = user.Email ?? string.Empty;

                IdentityResult emailResult = await _userManager.SetEmailAsync(user, form.Email);

                if (emailResult.Succeeded)
                {
                    // Changing the email marks it unconfirmed; confirm it again so login still works.
                    string token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    await _userManager.ConfirmEmailAsync(user, token);

                    if (string.Equals(user.UserName, oldEmail, StringComparison.OrdinalIgnoreCase))
                    {
                        emailResult = await _userManager.SetUserNameAsync(user, form.Email);
                    }
                }

                if (!emailResult.Succeeded)
                {
                    foreach (IdentityError error in emailResult.Errors)
                    {
                        ModelState.AddModelError(nameof(form.Email), error.Description);
                    }

                    return ShowWithErrors(user, profile, form);
                }
            }

            // ---------- Phone number ----------
            if (!string.Equals(form.PhoneNumber ?? string.Empty, user.PhoneNumber ?? string.Empty, StringComparison.Ordinal))
            {
                IdentityResult phoneResult = await _userManager.SetPhoneNumberAsync(user, form.PhoneNumber);

                if (!phoneResult.Succeeded)
                {
                    foreach (IdentityError error in phoneResult.Errors)
                    {
                        ModelState.AddModelError(nameof(form.PhoneNumber), error.Description);
                    }

                    return ShowWithErrors(user, profile, form);
                }
            }

            // ---------- Profile ----------
            profile.FullName = form.FullName;
            profile.Organisation = form.Organisation ?? string.Empty;
            profile.PreferredContactMethod = form.PreferredContactMethod!;
            profile.PreferredLanguage = form.PreferredLanguage!;
            profile.NotifyStatusChanges = form.NotifyStatusChanges;
            profile.NotifyReviewerComments = form.NotifyReviewerComments;
            profile.NotifyWeeklySummary = form.NotifyWeeklySummary;

            ProducerProfileStore.Save(Root, user.Id, profile);

            // ---------- Photo ----------
            if (form.Photo != null && photoExtension != null)
            {
                await ProducerProfileStore.SavePhotoAsync(Root, user.Id, form.Photo, photoExtension);
            }
            else if (form.RemovePhoto)
            {
                ProducerProfileStore.DeletePhoto(Root, user.Id);
            }

            if (emailChanged)
            {
                await _signInManager.RefreshSignInAsync(user);
            }

            TempData["SettingsMessage"] = "Your changes have been saved.";

            return RedirectToAction(nameof(Settings), new { tab = CleanTab(form.ActiveTab) });
        }


        // ================= SECURITY - CHANGE PASSWORD =================

        [HttpPost("ChangePassword")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string? currentPassword, string? newPassword, string? confirmPassword)
        {
            IdentityUser? user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            if (string.IsNullOrEmpty(currentPassword) || string.IsNullOrEmpty(newPassword))
            {
                TempData["SettingsError"] = "Enter your current password and a new password.";
                return RedirectToAction(nameof(Settings), new { tab = "security" });
            }

            if (newPassword != confirmPassword)
            {
                TempData["SettingsError"] = "The new passwords do not match.";
                return RedirectToAction(nameof(Settings), new { tab = "security" });
            }

            IdentityResult result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);

            if (!result.Succeeded)
            {
                TempData["SettingsError"] = string.Join(" ", result.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Settings), new { tab = "security" });
            }

            await _signInManager.RefreshSignInAsync(user);

            TempData["SettingsMessage"] = "Your password has been changed.";

            return RedirectToAction(nameof(Settings), new { tab = "security" });
        }


        // ================= REQUEST POSITION CHANGE =================

        [HttpPost("RequestPosition")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestPosition(string? requestedPosition)
        {
            IdentityUser? user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            if (string.IsNullOrWhiteSpace(requestedPosition) || !Positions.Contains(requestedPosition))
            {
                TempData["SettingsError"] = "Choose the position you would like to request.";
                return RedirectToAction(nameof(Settings), new { tab = "profile" });
            }

            ProducerProfile profile = ProducerProfileStore.Load(Root, user.Id);

            profile.RequestedPosition = requestedPosition;
            profile.PositionRequestedAtUtc = DateTime.UtcNow;

            ProducerProfileStore.Save(Root, user.Id, profile);

            TempData["SettingsMessage"] = $"Your request to become a {requestedPosition} was sent to an administrator.";

            return RedirectToAction(nameof(Settings), new { tab = "profile" });
        }


        // ================= PROFILE PHOTO (own photo only) =================

        [HttpGet("ProfilePhoto")]
        public async Task<IActionResult> ProfilePhoto()
        {
            IdentityUser? user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return NotFound();
            }

            string? path = ProducerProfileStore.PhotoPath(Root, user.Id);

            if (path == null)
            {
                return NotFound();
            }

            string contentType = path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg";

            return PhysicalFile(path, contentType);
        }


        // ================= HELPERS =================

        private ProducerSettingsViewModel BuildModel(IdentityUser user, ProducerProfile profile, ProducerSettingsForm form, string? tab)
        {
            string name = ProducerProfileStore.DisplayName(profile, user);

            var model = new ProducerSettingsViewModel
            {
                Form = form,
                ProducerName = name,
                Initials = ProducerProfileStore.Initials(name),
                PhotoUrl = ProducerProfileStore.PhotoUrl(Root, user.Id),
                ProducerCategory = string.IsNullOrWhiteSpace(profile.ProducerCategory) ? "Community Producer" : profile.ProducerCategory,
                AccountEmail = user.Email ?? user.UserName ?? string.Empty,
                RequestedPosition = profile.RequestedPosition,
                ActiveTab = CleanTab(tab),
                Positions = Positions,
                ContactMethods = ContactMethods,
                Languages = Languages
            };

            if (profile.PositionRequestedAtUtc.HasValue)
            {
                model.PositionRequestedText = "Sent " + SouthAfricaTime.LongDate(profile.PositionRequestedAtUtc.Value);
            }

            return model;
        }

        private IActionResult ShowWithErrors(IdentityUser user, ProducerProfile profile, ProducerSettingsForm form)
        {
            ProducerSettingsViewModel model = BuildModel(user, profile, form, form.ActiveTab);

            model.ErrorMessage = "Please fix the highlighted fields and save again.";

            return View(ViewPath, model);
        }

        private static string CleanTab(string? tab)
        {
            string value = (tab ?? string.Empty).Trim().ToLowerInvariant();

            return Tabs.Contains(value) ? value : "profile";
        }

        // Returns ".jpg" or ".png" for a real JPG/PNG up to 5 MB, otherwise adds an error.
        private async Task<string?> CheckPhotoAsync(IFormFile photo)
        {
            if (photo.Length > MaxPhotoBytes)
            {
                ModelState.AddModelError(nameof(ProducerSettingsForm.Photo), "The photo must be 5 MB or smaller.");
                return null;
            }

            string extension = Path.GetExtension(photo.FileName ?? string.Empty).ToLowerInvariant();

            var header = new byte[8];
            int read;

            await using (Stream stream = photo.OpenReadStream())
            {
                read = await stream.ReadAsync(header.AsMemory(0, header.Length));
            }

            bool isJpeg = read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
            bool isPng = read >= 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47;

            if ((extension == ".jpg" || extension == ".jpeg") && isJpeg)
            {
                return ".jpg";
            }

            if (extension == ".png" && isPng)
            {
                return ".png";
            }

            ModelState.AddModelError(nameof(ProducerSettingsForm.Photo), "Upload a JPG or PNG image.");
            return null;
        }
    }
}