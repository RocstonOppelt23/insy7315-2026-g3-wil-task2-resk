using System.Globalization;
using System.Text;
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
     * ADMIN - ROLES & PERMISSIONS
     * =========================================================
     *
     *   GET  /Admin/Roles?role=ID              roles list + permission matrix
     *   POST /Admin/Roles/{id}/Save            save the matrix
     *   POST /Admin/Roles/{id}/Duplicate       copy a role
     *   POST /Admin/Roles/{id}/Delete          only custom roles nobody uses
     *   GET  /Admin/Roles/{id}/Edit            "Edit role" page      (AdminRolesController.Edit.cs)
     *   GET  /Admin/Roles/Create               3-step "Create role"  (AdminRolesController.Wizard.cs)
     *   GET  /Admin/NoAccess                   shown when a role doesn't allow a page
     *   GET  /Admin/Roles/MyAccess.js          hides sidebar links the role can't open
     */
    [Authorize(Roles = "Admin")]
    public partial class AdminRolesController : Controller
    {
        private const string ViewFolder = "~/Views/AdminRoles/";

        private readonly RESK.WIL.Data.ApplicationDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IWebHostEnvironment _environment;

        public AdminRolesController(
            RESK.WIL.Data.ApplicationDbContext db,
            UserManager<IdentityUser> userManager,
            IWebHostEnvironment environment)
        {
            _db = db;
            _userManager = userManager;
            _environment = environment;
        }

        private string Root => _environment.ContentRootPath;


        // =========================================================
        // LIST + MATRIX
        // =========================================================

        [HttpGet("Admin/Roles", Order = -1)]
        public async Task<IActionResult> Index(string? role, string? created)
        {
            await SetAdminInfoAsync();

            List<ReskRole> roles = ReskRoleStore.All(Root);
            Dictionary<string, List<string>> holders = await HoldersAsync(roles);

            ReskRole selected = roles.FirstOrDefault(r => r.Id == role) ?? roles.First();
            ReskRole? mine = await MyRoleAsync(roles);

            var model = new ReskRolesIndexViewModel
            {
                Roles = roles.Select(r => new ReskRoleListItem
                {
                    Id = r.Id,
                    Name = r.Name,
                    Initials = ReskRoleStore.Initials(r.Name),
                    Colour = ReskRoleStore.Colour(r),
                    Status = r.Status,
                    AreaLabel = ReskRoleStore.AreaLabel(r.Area),
                    Tagline = r.IsAdministrator ? "Full system access" : Shorten(r.Description, 34),
                    Users = holders.TryGetValue(r.Id, out List<string>? h) ? h.Count : 0,
                    IsSystem = r.IsSystem
                }).ToList(),
                ActiveCount = roles.Count(r => r.IsActive),
                Id = selected.Id,
                Name = selected.Name,
                Status = selected.Status,
                Description = selected.Description,
                Area = selected.Area,
                IsAdministrator = selected.IsAdministrator,
                IsSystem = selected.IsSystem,
                IsMine = mine?.Id == selected.Id,
                Permissions = selected.Permissions,
                UpdatedText = $"Last updated {SouthAfricaTime.ToLocal(selected.UpdatedAtUtc).ToString("d MMM yyyy 'at' HH:mm", CultureInfo.InvariantCulture)} by {selected.UpdatedBy ?? "System"}"
            };

            if (holders.TryGetValue(selected.Id, out List<string>? names))
            {
                model.UserCount = names.Count;
                model.UserNames = names.OrderBy(n => n).Take(12).ToList();
            }

            ReskRole? createdRole = roles.FirstOrDefault(r => r.Id == created);

            if (createdRole != null)
            {
                model.CreatedId = createdRole.Id;
                model.CreatedName = createdRole.Name;
            }

            return View(ViewFolder + "Index.cshtml", model);
        }


        [HttpPost("Admin/Roles/{id}/Save", Order = -1)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(string id, ReskRoleSaveForm form)
        {
            List<ReskRole> roles = ReskRoleStore.All(Root);
            ReskRole? role = roles.FirstOrDefault(r => r.Id == id);

            if (role == null)
            {
                return NotFoundRedirect();
            }

            string? error = await ValidateChangeAsync(role, roles, form.Name, form.Status, form.Description, form.Perms);

            if (error != null)
            {
                TempData["AdminError"] = error;
                return Redirect("/Admin/Roles?role=" + id);
            }

            ApplyChange(role, form.Name!, form.Status!, form.Description!, form.Perms);
            ReskRoleStore.SaveAll(Root, roles);

            TempData["AdminMessage"] = $"\"{role.Name}\" was saved. Users with this role get the new permissions straight away.";
            return Redirect("/Admin/Roles?role=" + id);
        }


        [HttpPost("Admin/Roles/{id}/Duplicate", Order = -1)]
        [ValidateAntiForgeryToken]
        public IActionResult Duplicate(string id)
        {
            List<ReskRole> roles = ReskRoleStore.All(Root);
            ReskRole? source = roles.FirstOrDefault(r => r.Id == id);

            if (source == null)
            {
                return NotFoundRedirect();
            }

            string name = "Copy of " + source.Name;
            int n = 2;

            while (roles.Any(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                name = $"Copy of {source.Name} ({n++})";
            }

            var copy = new ReskRole
            {
                Name = name.Length > 60 ? name.Substring(0, 60) : name,
                Description = source.Description,
                Area = source.Area,
                Template = source.Template,
                Status = ReskRoleStore.Inactive,
                Permissions = new List<string>(source.Permissions),
                CreatedBy = User.Identity?.Name,
                UpdatedBy = User.Identity?.Name
            };

            roles.Add(copy);
            ReskRoleStore.SaveAll(Root, roles);

            TempData["AdminMessage"] = $"\"{copy.Name}\" was created as an inactive copy. Rename it, adjust the permissions and set it to Active.";
            return Redirect("/Admin/Roles?role=" + copy.Id);
        }


        [HttpPost("Admin/Roles/{id}/Delete", Order = -1)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            List<ReskRole> roles = ReskRoleStore.All(Root);
            ReskRole? role = roles.FirstOrDefault(r => r.Id == id);

            if (role == null)
            {
                return NotFoundRedirect();
            }

            if (role.IsSystem)
            {
                TempData["AdminError"] = "Built-in roles can't be deleted. You can set them to Inactive instead.";
                return Redirect("/Admin/Roles?role=" + id);
            }

            Dictionary<string, List<string>> holders = await HoldersAsync(roles);
            int count = holders.TryGetValue(id, out List<string>? h) ? h.Count : 0;

            if (count > 0)
            {
                TempData["AdminError"] = $"{count} user(s) still have \"{role.Name}\". Give them another role first (Users → Edit user).";
                return Redirect("/Admin/Roles?role=" + id);
            }

            roles.Remove(role);
            ReskRoleStore.SaveAll(Root, roles);

            TempData["AdminMessage"] = $"The \"{role.Name}\" role was deleted.";
            return Redirect("/Admin/Roles");
        }
    }
}