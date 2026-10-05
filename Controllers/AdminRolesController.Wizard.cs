using Microsoft.AspNetCore.Mvc;
using RESK.WIL.Models;
using RESK.WIL.Services;

namespace RESK.WIL.Controllers
{
    public partial class AdminRolesController
    {
        /*
         * CREATE ROLE (3 steps)
         *   Step 1  Role details + permission template
         *   Step 2  Permission matrix
         *   Step 3  Review -> Create role
         *
         * Everything typed so far travels with the form (hidden fields),
         * so Back / Next never lose anything. "Save draft" stores the role
         * as a Draft (not assignable) that can be continued later.
         */

        [HttpGet("Admin/Roles/Create", Order = -1)]
        public async Task<IActionResult> Create(string? draft, string? template)
        {
            await SetAdminInfoAsync();

            var form = new ReskRoleWizardForm();
            ReskRole? saved = ReskRoleStore.Get(Root, draft);

            if (saved != null && saved.Status == ReskRoleStore.Draft)
            {
                form.DraftId = saved.Id;
                form.Name = saved.Name;
                form.Description = saved.Description;
                form.Area = saved.Area;
                form.Template = saved.Template;
                form.AppliedTemplate = saved.Template;
                form.Perms = new List<string>(saved.Permissions);
            }
            else if (template is "manager" or "reviewer" or "viewer")
            {
                form.Template = template;
                form.Area = ReskRoleStore.TemplateArea(template);
            }

            return View(ViewFolder + "Create.cshtml", form);
        }


        [HttpPost("Admin/Roles/Create", Order = -1)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ReskRoleWizardForm form)
        {
            await SetAdminInfoAsync();

            List<ReskRole> roles = ReskRoleStore.All(Root);

            form.Name = (form.Name ?? "").Trim();
            form.Description = (form.Description ?? "").Trim();
            form.Perms = ReskRoleStore.Clean(form.Perms);
            form.Template = form.Template is "manager" or "reviewer" or "viewer" ? form.Template : "custom";
            form.Area = ReskRoleStore.Areas.Any(a => a.Key == form.Area) ? form.Area : "Admin";
            form.Status = form.AssignNow ? ReskRoleStore.Active : ReskRoleStore.Inactive;

            // ---------- Save draft (from any step) ----------
            if (form.Go == "draft")
            {
                if (form.Name.Length < 2)
                {
                    ModelState.AddModelError(nameof(form.Name), "Give the draft a name (at least 2 characters).");
                    form.Step = 1;
                    return View(ViewFolder + "Create.cshtml", form);
                }

                ReskRole draft = SaveRole(roles, form, ReskRoleStore.Draft);
                TempData["AdminMessage"] = $"\"{draft.Name}\" was saved as a draft. Open it from the roles list to continue.";
                return Redirect("/Admin/Roles?role=" + draft.Id);
            }

            // ---------- Jump back to a step ----------
            if (form.Go is "back" or "step1" or "step2")
            {
                form.Step = form.Go == "step1" ? 1 : form.Go == "step2" ? 2 : Math.Max(1, form.Step - 1);
                ModelState.Clear();
                return View(ViewFolder + "Create.cshtml", form);
            }

            // ---------- Step 1 -> 2 ----------
            if (form.Step == 1)
            {
                ValidateDetails(form, roles);

                if (!ModelState.IsValid)
                {
                    return View(ViewFolder + "Create.cshtml", form);
                }

                // A newly picked template fills in its permissions.
                if (form.AppliedTemplate != form.Template)
                {
                    form.Perms = ReskRoleStore.TemplatePermissions(form.Template);
                    form.AppliedTemplate = form.Template;
                }

                ModelState.Clear();
                form.Step = 2;
                return View(ViewFolder + "Create.cshtml", form);
            }

            // ---------- Step 2 -> 3 ----------
            if (form.Step == 2)
            {
                ModelState.Clear();
                form.Step = 3;
                return View(ViewFolder + "Create.cshtml", form);
            }

            // ---------- Step 3: create ----------
            ValidateDetails(form, roles);

            if (!ModelState.IsValid)
            {
                form.Step = 1;
                return View(ViewFolder + "Create.cshtml", form);
            }

            ReskRole role = SaveRole(roles, form, form.Status);

            return Redirect("/Admin/Roles?role=" + role.Id + "&created=" + role.Id);
        }


        private void ValidateDetails(ReskRoleWizardForm form, List<ReskRole> roles)
        {
            if (form.Name!.Length < 2 || form.Name.Length > 60)
            {
                ModelState.AddModelError(nameof(form.Name), "Enter a role name of 2 to 60 characters.");
            }
            else if (roles.Any(r => r.Id != form.DraftId && string.Equals(r.Name, form.Name, StringComparison.OrdinalIgnoreCase)))
            {
                ModelState.AddModelError(nameof(form.Name), "A role with this name already exists.");
            }

            if (form.Description!.Length < 5)
            {
                ModelState.AddModelError(nameof(form.Description), "Describe what this role is for (at least 5 characters).");
            }
            else if (form.Description.Length > 200)
            {
                ModelState.AddModelError(nameof(form.Description), "Keep the description under 200 characters.");
            }
        }


        // Creates the role, or updates the draft it came from.
        private ReskRole SaveRole(List<ReskRole> roles, ReskRoleWizardForm form, string status)
        {
            ReskRole? role = roles.FirstOrDefault(r => r.Id == form.DraftId && r.Status == ReskRoleStore.Draft);

            if (role == null)
            {
                role = new ReskRole { CreatedBy = User.Identity?.Name ?? "Admin", CreatedAtUtc = DateTime.UtcNow };
                roles.Add(role);
            }

            role.Name = form.Name!.Length > 60 ? form.Name.Substring(0, 60) : form.Name;
            role.Description = form.Description!.Length > 200 ? form.Description.Substring(0, 200) : form.Description;
            role.Area = form.Area;
            role.Template = form.Template;
            role.Status = status;
            role.Permissions = ReskRoleStore.Clean(form.Perms);
            role.UpdatedAtUtc = DateTime.UtcNow;
            role.UpdatedBy = User.Identity?.Name ?? "Admin";

            ReskRoleStore.SaveAll(Root, roles);
            return role;
        }
    }
}