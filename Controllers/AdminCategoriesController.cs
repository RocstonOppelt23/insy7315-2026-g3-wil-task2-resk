using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
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
     * ADMIN - PROGRAMME CATEGORIES
     * =========================================================
     *
     *   GET  /Admin/Categories                      list, stats, search
     *   GET  /Admin/Categories/Create               "Add category"
     *   POST /Admin/Categories/Create
     *   GET  /Admin/Categories/{id}/Edit            "Edit category"
     *   POST /Admin/Categories/{id}/Edit
     *   POST /Admin/Categories/{id}/Status          activate / deactivate
     *   POST /Admin/Categories/{id}/Visibility      show / hide on the producer form
     *   POST /Admin/Categories/{id}/Delete          only when no proposal uses it
     *
     * Producers see categories that are Active AND "Show on the
     * producer proposal form" (see ReskCategoryOptionsViewComponent).
     */
    [Authorize(Roles = "Admin")]
    public class AdminCategoriesController : Controller
    {
        private const string ViewFolder = "~/Views/AdminCategories/";

        private readonly RESK.WIL.Data.ApplicationDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IWebHostEnvironment _environment;

        public AdminCategoriesController(
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
        // LIST
        // =========================================================

        [HttpGet("Admin/Categories", Order = -1)]
        public async Task<IActionResult> Index(string? search, string? status)
        {
            await SetAdminInfoAsync();

            Dictionary<string, int> usage = await UsageAsync();
            List<ReskCategory> all = ReskCategoryStore.All(Root, usage.Keys);

            List<AdminCategoryRow> rows = all.Select(c => ToRow(c, usage)).ToList();
            AdminCategoryRow? mostUsed = rows.Where(r => r.ProposalCount > 0).OrderByDescending(r => r.ProposalCount).FirstOrDefault();

            IEnumerable<AdminCategoryRow> filtered = rows;

            if (!string.IsNullOrWhiteSpace(search))
            {
                string term = search.Trim();
                filtered = filtered.Where(r =>
                    r.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    r.Description.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    r.Code.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            if (status == "active")
            {
                filtered = filtered.Where(r => r.IsActive);
            }
            else if (status == "inactive")
            {
                filtered = filtered.Where(r => !r.IsActive);
            }
            else if (status == "hidden")
            {
                filtered = filtered.Where(r => r.IsActive && !r.ShowOnForm);
            }

            var model = new AdminCategoriesIndexViewModel
            {
                Total = rows.Count,
                Active = rows.Count(r => r.IsActive),
                Inactive = rows.Count(r => !r.IsActive),
                MostUsedName = mostUsed?.Name ?? "—",
                MostUsedCount = mostUsed?.ProposalCount ?? 0,
                Search = search,
                Status = status,
                Rows = filtered.ToList()
            };

            return View(ViewFolder + "Index.cshtml", model);
        }


        // =========================================================
        // ADD
        // =========================================================

        [HttpGet("Admin/Categories/Create", Order = -1)]
        public async Task<IActionResult> Create()
        {
            await SetAdminInfoAsync();

            List<ReskCategory> all = ReskCategoryStore.All(Root, (await UsageAsync()).Keys);

            var form = new AdminCategoryForm
            {
                DisplayOrder = all.Count == 0 ? 1 : all.Max(c => c.DisplayOrder) + 1
            };

            return View(ViewFolder + "Create.cshtml", form);
        }


        [HttpPost("Admin/Categories/Create", Order = -1)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AdminCategoryForm form)
        {
            List<ReskCategory> all = ReskCategoryStore.All(Root, (await UsageAsync()).Keys);

            Clean(form);
            Validate(form, all, null);

            if (!ModelState.IsValid)
            {
                await SetAdminInfoAsync();
                return View(ViewFolder + "Create.cshtml", form);
            }

            string by = User.Identity?.Name ?? "Admin";

            var category = new ReskCategory
            {
                Id = all.Count == 0 ? 1 : all.Max(c => c.Id) + 1,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = by
            };

            Apply(form, category, all, by);
            all.Add(category);
            ReskCategoryStore.SaveAll(Root, all);

            TempData["AdminMessage"] = $"The \"{category.Name}\" category was created.";

            return Redirect("/Admin/Categories");
        }


        // =========================================================
        // EDIT
        // =========================================================

        [HttpGet("Admin/Categories/{id:int}/Edit", Order = -1)]
        public async Task<IActionResult> Edit(int id)
        {
            Dictionary<string, int> usage = await UsageAsync();
            ReskCategory? category = ReskCategoryStore.All(Root, usage.Keys).FirstOrDefault(c => c.Id == id);

            if (category == null)
            {
                return NotFoundRedirect();
            }

            await SetAdminInfoAsync();
            SetEditInfo(category, usage);

            var form = new AdminCategoryForm
            {
                Name = category.Name,
                Description = category.Description,
                Code = category.Code,
                Status = category.Status,
                DisplayOrder = category.DisplayOrder,
                IconColour = category.IconColour,
                ShowOnForm = category.ShowOnForm,
                RequireSupportingDocuments = category.RequireSupportingDocuments
            };

            return View(ViewFolder + "Edit.cshtml", form);
        }


        [HttpPost("Admin/Categories/{id:int}/Edit", Order = -1)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, AdminCategoryForm form)
        {
            Dictionary<string, int> usage = await UsageAsync();
            List<ReskCategory> all = ReskCategoryStore.All(Root, usage.Keys);
            ReskCategory? category = all.FirstOrDefault(c => c.Id == id);

            if (category == null)
            {
                return NotFoundRedirect();
            }

            Clean(form);
            Validate(form, all, category);

            if (!ModelState.IsValid)
            {
                await SetAdminInfoAsync();
                SetEditInfo(category, usage);
                return View(ViewFolder + "Edit.cshtml", form);
            }

            string oldName = category.Name;

            Apply(form, category, all, User.Identity?.Name ?? "Admin");
            ReskCategoryStore.SaveAll(Root, all);

            // Renamed: move existing proposals to the new name.
            int moved = 0;

            if (!string.Equals(oldName, category.Name, StringComparison.Ordinal))
            {
                moved = await RenameOnProposalsAsync(oldName, category.Name);
            }

            TempData["AdminMessage"] = moved > 0
                ? $"Changes saved. {moved} proposal(s) were moved to the new name \"{category.Name}\"."
                : "Changes saved.";

            return Redirect("/Admin/Categories");
        }


        // =========================================================
        // QUICK ACTIONS
        // =========================================================

        [HttpPost("Admin/Categories/{id:int}/Status", Order = -1)]
        [ValidateAntiForgeryToken]
        public IActionResult Status(int id, string? to, string? returnUrl)
        {
            List<ReskCategory> all = ReskCategoryStore.All(Root);
            ReskCategory? category = all.FirstOrDefault(c => c.Id == id);

            if (category == null)
            {
                return NotFoundRedirect();
            }

            bool activate = to == "active";

            category.Status = activate ? ReskCategoryStore.Active : ReskCategoryStore.Inactive;
            category.UpdatedAtUtc = DateTime.UtcNow;
            category.UpdatedBy = User.Identity?.Name ?? "Admin";
            ReskCategoryStore.SaveAll(Root, all);

            TempData["AdminMessage"] = activate
                ? $"\"{category.Name}\" is active again."
                : $"\"{category.Name}\" was deactivated. Producers can no longer choose it; existing proposals keep it.";

            return LocalRedirect(SafeReturn(returnUrl));
        }


        [HttpPost("Admin/Categories/{id:int}/Visibility", Order = -1)]
        [ValidateAntiForgeryToken]
        public IActionResult Visibility(int id, string? returnUrl)
        {
            List<ReskCategory> all = ReskCategoryStore.All(Root);
            ReskCategory? category = all.FirstOrDefault(c => c.Id == id);

            if (category == null)
            {
                return NotFoundRedirect();
            }

            category.ShowOnForm = !category.ShowOnForm;
            category.UpdatedAtUtc = DateTime.UtcNow;
            category.UpdatedBy = User.Identity?.Name ?? "Admin";
            ReskCategoryStore.SaveAll(Root, all);

            TempData["AdminMessage"] = category.ShowOnForm
                ? $"\"{category.Name}\" is shown on the producer proposal form."
                : $"\"{category.Name}\" is hidden from the producer proposal form.";

            return LocalRedirect(SafeReturn(returnUrl));
        }


        [HttpPost("Admin/Categories/{id:int}/Delete", Order = -1)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            Dictionary<string, int> usage = await UsageAsync();
            List<ReskCategory> all = ReskCategoryStore.All(Root, usage.Keys);
            ReskCategory? category = all.FirstOrDefault(c => c.Id == id);

            if (category == null)
            {
                return NotFoundRedirect();
            }

            int count = CountFor(category.Name, usage);

            if (count > 0)
            {
                TempData["AdminError"] =
                    $"\"{category.Name}\" is used by {count} proposal(s), so it can't be deleted. Deactivate it instead.";
                return Redirect($"/Admin/Categories/{id}/Edit");
            }

            all.Remove(category);
            ReskCategoryStore.SaveAll(Root, all);

            TempData["AdminMessage"] = $"The \"{category.Name}\" category was deleted.";

            return Redirect("/Admin/Categories");
        }


        // =========================================================
        // HELPERS
        // =========================================================

        // Category name (trimmed, any case) -> number of proposals.
        private async Task<Dictionary<string, int>> UsageAsync()
        {
            List<string> names = await _db.ProducerProposals
                .AsNoTracking()
                .Where(p => p.Category != null && p.Category != "")
                .Select(p => p.Category)
                .ToListAsync();

            return names
                .Select(n => n.Trim())
                .Where(n => n.Length > 0)
                .GroupBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.First(), g => g.Count(), StringComparer.OrdinalIgnoreCase);
        }


        private static int CountFor(string name, Dictionary<string, int> usage)
        {
            return usage.TryGetValue(name.Trim(), out int count) ? count : 0;
        }


        private static AdminCategoryRow ToRow(ReskCategory c, Dictionary<string, int> usage)
        {
            return new AdminCategoryRow
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                Code = c.Code,
                IsActive = c.IsActive,
                ShowOnForm = c.ShowOnForm,
                RequireSupportingDocuments = c.RequireSupportingDocuments,
                ColourHex = ReskCategoryStore.Hex(c.IconColour),
                DisplayOrder = c.DisplayOrder,
                ProposalCount = CountFor(c.Name, usage)
            };
        }


        private void SetEditInfo(ReskCategory category, Dictionary<string, int> usage)
        {
            ViewData["CategoryId"] = category.Id;
            ViewData["CategoryName"] = category.Name;
            ViewData["IsActive"] = category.IsActive;
            ViewData["ProposalCount"] = CountFor(category.Name, usage);
            ViewData["UpdatedText"] =
                $"Last updated {SouthAfricaTime.ToLocal(category.UpdatedAtUtc).ToString("d MMM yyyy 'at' HH:mm", CultureInfo.InvariantCulture)}" +
                (string.IsNullOrWhiteSpace(category.UpdatedBy) ? "" : $" by {category.UpdatedBy}");
        }


        private static void Clean(AdminCategoryForm form)
        {
            form.Name = Regex.Replace((form.Name ?? "").Trim(), @"\s+", " ");
            form.Description = (form.Description ?? "").Trim();
            form.Code = string.IsNullOrWhiteSpace(form.Code) ? null : form.Code.Trim().ToUpperInvariant();

            if (form.Status != ReskCategoryStore.Active && form.Status != ReskCategoryStore.Inactive)
            {
                form.Status = ReskCategoryStore.Active;
            }

            if (form.IconColour == null || !ReskCategoryStore.Colours.ContainsKey(form.IconColour))
            {
                form.IconColour = "Cyan";
            }
        }


        private void Validate(AdminCategoryForm form, List<ReskCategory> all, ReskCategory? editing)
        {
            if (form.Name.Length > 0 &&
                all.Any(c => c != editing && ReskCategoryStore.SameName(c.Name, form.Name)))
            {
                ModelState.AddModelError(nameof(form.Name), "A category with this name already exists.");
            }

            if (form.Code != null)
            {
                if (!Regex.IsMatch(form.Code, "^[A-Z0-9]{2,10}$"))
                {
                    ModelState.AddModelError(nameof(form.Code), "Use 2 to 10 letters or numbers, e.g. EDU.");
                }
                else if (all.Any(c => c != editing && string.Equals(c.Code, form.Code, StringComparison.OrdinalIgnoreCase)))
                {
                    ModelState.AddModelError(nameof(form.Code), "Another category already uses this code.");
                }
            }
        }


        private static void Apply(AdminCategoryForm form, ReskCategory category, List<ReskCategory> all, string by)
        {
            category.Name = form.Name;
            category.Description = form.Description;
            category.Status = form.Status;
            category.DisplayOrder = form.DisplayOrder;
            category.IconColour = form.IconColour ?? "Cyan";
            category.ShowOnForm = form.ShowOnForm;
            category.RequireSupportingDocuments = form.RequireSupportingDocuments;
            category.UpdatedAtUtc = DateTime.UtcNow;
            category.UpdatedBy = by;

            // No code given: make one from the name (DOC, DOC2, ...).
            string code = form.Code ?? ReskCategoryStore.MakeCode(form.Name);
            string unique = code;
            int n = 2;

            while (all.Any(c => c != category && string.Equals(c.Code, unique, StringComparison.OrdinalIgnoreCase)))
            {
                unique = code + n++;
            }

            category.Code = unique;
        }


        // Updates the category name on proposals (summary column + saved wizard answers).
        private async Task<int> RenameOnProposalsAsync(string oldName, string newName)
        {
            List<ProducerProposal> proposals = await _db.ProducerProposals
                .Where(p => p.Category != null && p.Category != "")
                .ToListAsync();

            int changed = 0;

            foreach (ProducerProposal p in proposals.Where(p => ReskCategoryStore.SameName(p.Category, oldName)))
            {
                p.Category = newName;

                if (!string.IsNullOrWhiteSpace(p.ProgrammeDetailsJson))
                {
                    try
                    {
                        if (JsonNode.Parse(p.ProgrammeDetailsJson) is JsonObject json &&
                            json["Category"] is JsonValue value &&
                            value.TryGetValue(out string? current) &&
                            ReskCategoryStore.SameName(current, oldName))
                        {
                            json["Category"] = newName;
                            p.ProgrammeDetailsJson = json.ToJsonString();
                        }
                    }
                    catch (System.Text.Json.JsonException)
                    {
                        // Leave unreadable JSON alone.
                    }
                }

                changed++;
            }

            if (changed > 0)
            {
                await _db.SaveChangesAsync();
            }

            return changed;
        }


        private static string SafeReturn(string? returnUrl)
        {
            return !string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith("/Admin/Categories", StringComparison.OrdinalIgnoreCase)
                ? returnUrl
                : "/Admin/Categories";
        }


        private IActionResult NotFoundRedirect()
        {
            TempData["AdminError"] = "That category could not be found.";
            return Redirect("/Admin/Categories");
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


namespace RESK.WIL.Models
{
    public class AdminCategoryRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Code { get; set; } = "";
        public bool IsActive { get; set; }
        public bool ShowOnForm { get; set; }
        public bool RequireSupportingDocuments { get; set; }
        public string ColourHex { get; set; } = "#12b8ce";
        public int DisplayOrder { get; set; }
        public int ProposalCount { get; set; }
    }


    public class AdminCategoriesIndexViewModel
    {
        public int Total { get; set; }
        public int Active { get; set; }
        public int Inactive { get; set; }
        public string MostUsedName { get; set; } = "—";
        public int MostUsedCount { get; set; }
        public string? Search { get; set; }
        public string? Status { get; set; }
        public List<AdminCategoryRow> Rows { get; set; } = new();
    }


    public class AdminCategoryForm
    {
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Enter a category name.")]
        [System.ComponentModel.DataAnnotations.StringLength(60, MinimumLength = 2, ErrorMessage = "Use 2 to 60 characters.")]
        public string Name { get; set; } = "";

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Enter a short description.")]
        [System.ComponentModel.DataAnnotations.StringLength(250, ErrorMessage = "Keep the description under 250 characters.")]
        public string Description { get; set; } = "";

        public string? Code { get; set; }

        public string Status { get; set; } = "Active";

        [System.ComponentModel.DataAnnotations.Range(1, 999, ErrorMessage = "Use a number from 1 to 999.")]
        public int DisplayOrder { get; set; } = 1;

        public string? IconColour { get; set; } = "Cyan";

        public bool ShowOnForm { get; set; } = true;

        public bool RequireSupportingDocuments { get; set; }
    }
}