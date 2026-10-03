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
     * MY PROFILE (profile picture)
     * =========================================================
     *
     *   GET/POST /Account/MyProfile   change or remove the profile picture
     *   GET      /Account/MyPhoto     the signed-in user's picture
     *   GET      /Account/Me.json     used by the admin sidebar
     *
     * Every role can change its picture, except the Administrator:
     * that account belongs to one person and its profile is fixed.
     */
    [Authorize]
    public class ReskProfileController : Controller
    {
        private const string ProfileView = "~/Views/Account/ReskProfile.cshtml";
        private const long MaxPhotoBytes = 5_000_000;

        private readonly UserManager<IdentityUser> _userManager;
        private readonly IWebHostEnvironment _environment;

        public ReskProfileController(UserManager<IdentityUser> userManager, IWebHostEnvironment environment)
        {
            _userManager = userManager;
            _environment = environment;
        }

        private string Root => _environment.ContentRootPath;


        [HttpGet("Account/MyProfile", Order = -1)]
        public async Task<IActionResult> MyProfile()
        {
            IdentityUser? user = await _userManager.GetUserAsync(User);

            return user == null ? Redirect("/Account/Login") : View(ProfileView, await BuildAsync(user));
        }


        [HttpPost("Account/MyProfile", Order = -1)]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(6_000_000)]
        [RequestFormLimits(MultipartBodyLengthLimit = 6_000_000)]
        public async Task<IActionResult> MyProfile(IFormFile? photo, bool removePhoto)
        {
            IdentityUser? user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Redirect("/Account/Login");
            }

            ReskProfileViewModel model = await BuildAsync(user);

            if (model.IsAdministrator)
            {
                model.Error = "The Administrator profile is fixed and can't be changed.";
                return View(ProfileView, model);
            }

            if (photo != null && photo.Length > 0)
            {
                string? extension = await PhotoTypeAsync(photo);

                if (photo.Length > MaxPhotoBytes)
                {
                    model.Error = "The picture must be 5 MB or smaller.";
                }
                else if (extension == null)
                {
                    model.Error = "Upload a JPG or PNG picture.";
                }
                else
                {
                    await ProducerProfileStore.SavePhotoAsync(Root, user.Id, photo, extension);
                    TempData["ProfileMessage"] = "Your profile picture was updated.";
                    return Redirect("/Account/MyProfile");
                }

                return View(ProfileView, model);
            }

            if (removePhoto)
            {
                ProducerProfileStore.DeletePhoto(Root, user.Id);
                TempData["ProfileMessage"] = "Your profile picture was removed.";
                return Redirect("/Account/MyProfile");
            }

            model.Error = "Choose a picture first.";
            return View(ProfileView, model);
        }


        [HttpGet("Account/MyPhoto", Order = -1)]
        public IActionResult MyPhoto()
        {
            string? path = ProducerProfileStore.PhotoPath(Root, _userManager.GetUserId(User) ?? "");

            if (path == null)
            {
                return NotFound();
            }

            return PhysicalFile(path, path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg");
        }


        [HttpGet("Account/Me.json", Order = -1)]
        [ResponseCache(NoStore = true)]
        public async Task<IActionResult> Me()
        {
            IdentityUser? user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Json(new { canEdit = false, photo = (string?)null });
            }

            ReskProfileViewModel model = await BuildAsync(user);

            return Json(new { canEdit = !model.IsAdministrator, photo = model.IsAdministrator ? null : model.PhotoUrl });
        }


        // =========================================================
        // HELPERS
        // =========================================================

        private async Task<ReskProfileViewModel> BuildAsync(IdentityUser user)
        {
            IList<string> roles = await _userManager.GetRolesAsync(user);
            ReskRole? role = ReskRoleStore.RoleFor(Root, user.Id, roles);
            ProducerProfile profile = ProducerProfileStore.Load(Root, user.Id);
            string name = ProducerProfileStore.DisplayName(profile, user);
            string? path = ProducerProfileStore.PhotoPath(Root, user.Id);

            bool administrator = role?.IsAdministrator == true;
            bool unnamed = administrator && string.IsNullOrWhiteSpace(profile.FullName);

            // The admin sidebar shows these.
            ViewData["AdminName"] = unnamed ? "Admin User" : name;
            ViewData["AdminInitials"] = unnamed ? "AD" : ProducerProfileStore.Initials(name);

            return new ReskProfileViewModel
            {
                Name = unnamed ? "Admin User" : name,
                Email = user.Email ?? user.UserName ?? "",
                RoleName = role?.Name ?? roles.FirstOrDefault() ?? "No role yet",
                Initials = unnamed ? "AD" : ProducerProfileStore.Initials(name),
                PhotoUrl = path == null ? null : "/Account/MyPhoto?v=" + System.IO.File.GetLastWriteTimeUtc(path).Ticks,
                IsAdministrator = administrator,
                InAdminArea = roles.Contains("Admin"),
                IsProducer = roles.Contains("Producer"),
                Message = TempData["ProfileMessage"] as string
            };
        }


        // ".jpg" or ".png" when the file really is that kind of picture.
        private static async Task<string?> PhotoTypeAsync(IFormFile photo)
        {
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

            return extension == ".png" && isPng ? ".png" : null;
        }
    }
}