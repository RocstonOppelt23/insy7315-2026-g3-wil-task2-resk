using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RESK.WIL.Models;
using RESK.WIL.Services;

namespace RESK.WIL.Controllers
{
    /*
     * =========================================================
     * ADMIN - SYSTEM SETTINGS
     * =========================================================
     *
     *   GET  /Admin/Settings?tab=general        one tab of the settings
     *   POST /Admin/Settings/Save               saves the tab that was open
     *
     * Tabs: general, registration, workflow, languages, notifications, security.
     * The settings are saved in App_Data/SystemSettings.json and are applied
     * by ReskSettingsMiddleware.
     */
    [Authorize(Roles = "Admin")]
    public partial class AdminSettingsController : Controller
    {
        private const string ViewPath = "~/Views/AdminSettings/Index.cshtml";

        public static readonly (string Key, string Label)[] Tabs =
        {
            ("general", "General"), ("registration", "Registration"), ("workflow", "Workflow"),
            ("languages", "Languages"), ("notifications", "Notifications"), ("security", "Security")
        };

        private readonly RESK.WIL.Data.ApplicationDbContext _db;
        private readonly IWebHostEnvironment _environment;

        public AdminSettingsController(RESK.WIL.Data.ApplicationDbContext db, IWebHostEnvironment environment)
        {
            _db = db;
            _environment = environment;
        }

        private string Root => _environment.ContentRootPath;


        [HttpGet("Admin/Settings", Order = -1)]
        [HttpGet("Admin/SystemSettings", Order = -1)]
        public async Task<IActionResult> Index(string? tab)
        {
            return View(ViewPath, await BuildAsync(TabKey(tab), ReskSettingsStore.Current(Root)));
        }


        [HttpPost("Admin/Settings/Save", Order = -1)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(ReskSettingsForm form)
        {
            string tab = TabKey(form.Tab);
            ReskSettings settings = ReskSettingsStore.Editable(Root);

            List<string> errors = tab switch
            {
                "registration" => ApplyRegistration(form, settings),
                "workflow" => ApplyWorkflow(form, settings),
                "languages" => ApplyLanguages(form, settings),
                "notifications" => ApplyNotifications(form, settings),
                "security" => ApplySecurity(form, settings),
                _ => ApplyGeneral(form, settings)
            };

            if (errors.Count > 0)
            {
                ReskSettingsViewModel model = await BuildAsync(tab, settings);
                model.Errors = errors;
                return View(ViewPath, model);
            }

            ReskSettingsStore.Save(Root, settings, AdminName());

            TempData["AdminMessage"] = $"{Tabs.First(t => t.Key == tab).Label} settings were saved.";
            return Redirect("/Admin/Settings?tab=" + tab);
        }


        // =========================================================
        // HELPERS
        // =========================================================

        private static string TabKey(string? tab)
        {
            string key = (tab ?? "").Trim().ToLowerInvariant();
            return Tabs.Any(t => t.Key == key) ? key : "general";
        }


        private async Task<ReskSettingsViewModel> BuildAsync(string tab, ReskSettings settings)
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            ReskRole? mine = userId.Length == 0 ? null : ReskRoleStore.RoleFor(Root, userId, new[] { "Admin" });
            ReskSettings saved = ReskSettingsStore.Current(Root);

            ViewData["AdminName"] = AdminName();
            ViewData["AdminInitials"] = AdminName() == "Admin User" ? "AD" : ProducerProfileStore.Initials(AdminName());

            var model = new ReskSettingsViewModel
            {
                Tab = tab,
                Settings = settings,
                CanEdit = mine == null || mine.Can("settings", "edit"),
                LockoutReady = System.IO.File.Exists(Path.Combine(Root, "App_Data", "SignInGuard.json")),
                SavedText = saved.UpdatedAtUtc.HasValue
                    ? $"Last saved {SouthAfricaTime.ToLocal(saved.UpdatedAtUtc.Value).ToString("d MMM yyyy 'at' HH:mm", CultureInfo.InvariantCulture)} by {saved.UpdatedBy ?? "an administrator"}"
                    : null
            };

            if (tab == "workflow")
            {
                // Roles that sign in to the admin screens and may approve proposals.
                model.DecisionRoles = ReskRoleStore.All(Root)
                    .Where(r => r.Area == "Admin" && r.IsActive && !r.IsAdministrator && r.Can("proposals", "approve"))
                    .OrderBy(r => r.Name)
                    .Select(r => (r.Id, r.Name))
                    .ToList();

                // How the timing rules look right now.
                DateTime today = SouthAfricaTime.ToLocal(DateTime.UtcNow).Date;

                List<DateTime> waiting = (await _db.ProducerProposals.AsNoTracking()
                        .Where(p => p.Status == ProposalStatuses.InReview)
                        .Select(p => p.SubmittedAtUtc ?? p.UpdatedAtUtc)
                        .ToListAsync())
                    .Select(utc => SouthAfricaTime.ToLocal(utc).Date)
                    .ToList();

                model.InReview = waiting.Count;
                model.PastTarget = waiting.Count(day => ReskSettingsStore.WorkingDaysBetween(day, today) > settings.TargetReviewDays);
                model.NeedEscalation = waiting.Count(day => ReskSettingsStore.WorkingDaysBetween(day, today) > settings.EscalateAfterDays);
            }

            return model;
        }


        private string AdminName()
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            ProducerProfile profile = userId.Length == 0 ? new ProducerProfile() : ProducerProfileStore.Load(Root, userId);

            return string.IsNullOrWhiteSpace(profile.FullName) ? "Admin User" : profile.FullName.Trim();
        }


        // =========================================================
        // SHARED BY THE TABS
        // =========================================================

        private static void ApplyKey(ReskSettingsForm form, ReskSettings settings, List<string> errors)
        {
            string key = (form.AdminKey ?? "").Trim();

            settings.RequireAdminKey = form.RequireAdminKey;

            if (key.Length == 0 && !form.RequireAdminKey)
            {
                // Switched off and left empty: keep the key that was saved before.
                return;
            }

            settings.AdminKey = key.Length > 40 ? key.Substring(0, 40) : key;

            if (!Regex.IsMatch(key, @"^\S{8,40}$"))
            {
                errors.Add("The administrator registration key must be 8 to 40 characters with no spaces.");
            }
        }


        private static void ApplyDefaultRole(ReskSettingsForm form, ReskSettings settings, List<string> errors)
        {
            if (ReskSettingsStore.RegistrationRoles.Contains(form.DefaultRole))
            {
                settings.DefaultRole = form.DefaultRole!;
            }
            else
            {
                errors.Add("Choose the default role for new accounts.");
            }
        }


        private static int Choice(int value, int[] allowed, string what, List<string> errors)
        {
            if (!allowed.Contains(value))
            {
                errors.Add($"Choose {what}.");
                return allowed[0];
            }

            return value;
        }


        private static string Text(string? value, int max)
        {
            string text = (value ?? "").Trim();
            return text.Length > max ? text.Substring(0, max) : text;
        }
    }
}