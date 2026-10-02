using Microsoft.AspNetCore.Mvc;
using RESK.WIL.Models;
using RESK.WIL.Services;

namespace RESK.WIL.Controllers
{
    public partial class AdminRolesController
    {
        // =========================================================
        // EDIT ROLE (module rows with an "Edit" link each)
        // =========================================================

        [HttpGet("Admin/Roles/{id}/Edit", Order = -1)]
        public async Task<IActionResult> Edit(string id)
        {
            ReskRole? role = ReskRoleStore.Get(Root, id);

            if (role == null)
            {
                return NotFoundRedirect();
            }

            if (role.Status == ReskRoleStore.Draft)
            {
                return Redirect("/Admin/Roles/Create?draft=" + id);
            }

            await SetAdminInfoAsync();

            ViewData["RoleId"] = role.Id;
            ViewData["IsAdministrator"] = role.IsAdministrator;
            ViewData["AreaLabel"] = ReskRoleStore.AreaLabel(role.Area);

            return View(ViewFolder + "Edit.cshtml", new ReskRoleSaveForm
            {
                Name = role.Name,
                Status = role.Status,
                Description = role.Description,
                Perms = new List<string>(role.Permissions)
            });
        }


        [HttpPost("Admin/Roles/{id}/Edit", Order = -1)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, ReskRoleSaveForm form)
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
                await SetAdminInfoAsync();
                ModelState.AddModelError("", error);
                ViewData["RoleId"] = role.Id;
                ViewData["IsAdministrator"] = role.IsAdministrator;
                ViewData["AreaLabel"] = ReskRoleStore.AreaLabel(role.Area);
                form.Perms = ReskRoleStore.Clean(form.Perms);
                return View(ViewFolder + "Edit.cshtml", form);
            }

            ApplyChange(role, form.Name!, form.Status!, form.Description ?? "", form.Perms);
            ReskRoleStore.SaveAll(Root, roles);

            TempData["AdminMessage"] = $"\"{role.Name}\" was updated. Users with this role get the new permissions straight away.";
            return Redirect("/Admin/Roles?role=" + id);
        }
    }
}