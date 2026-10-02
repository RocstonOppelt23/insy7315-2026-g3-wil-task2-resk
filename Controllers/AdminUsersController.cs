using System.Globalization;
using System.Security.Cryptography;
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
     * ADMIN - USERS
     * =========================================================
     *
     *   GET  /Admin/Users                    list, stats, filters
     *   GET  /Admin/Users/Export             download the filtered list (CSV)
     *   GET  /Admin/Users/Create             "Add user"
     *   POST /Admin/Users/Create
     *   GET  /Admin/Users/{id}?tab=...       user details (overview, permissions,
     *                                        proposals, activity, security)
     *   GET  /Admin/Users/{id}/Edit          edit details + change role
     *   POST /Admin/Users/{id}/Edit
     *   GET  /Admin/Users/{id}/Pending       "Pending registration"
     *   POST /Admin/Users/{id}/Pending       approve or reject
     *   POST /Admin/Users/{id}/ResetPassword     new temporary password
     *   POST /Admin/Users/{id}/SignOutAll        ends every signed-in session
     *   POST /Admin/Users/{id}/Lock              locks for 24 hours
     *   POST /Admin/Users/{id}/Unlock
     *   POST /Admin/Users/{id}/Suspend
     *   POST /Admin/Users/{id}/Reactivate
     *   POST /Admin/Users/{id}/Delete
     *
     * Status:
     *   Pending    registered but has no role yet (waiting for approval)
     *   Active     has a role and can sign in
     *   Locked     temporarily locked (24 hours, or too many wrong passwords)
     *   Suspended  cannot sign in until reactivated
     *   Rejected   registration was rejected (cannot sign in)
     */
    [Authorize(Roles = "Admin")]
    public class AdminUsersController : Controller
    {
        private const string ViewFolder = "~/Views/AdminUsers/";

        public static readonly string[] SystemRoles = { "Producer", "Reviewer", "Admin" };

        private readonly RESK.WIL.Data.ApplicationDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IWebHostEnvironment _environment;

        public AdminUsersController(
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

        [HttpGet("Admin/Users", Order = -1)]
        public async Task<IActionResult> Index(string? search, string? role, string? status, string? joined)
        {
            await SetAdminInfoAsync();

            List<AdminUserRow> all = await LoadRowsAsync();

            var model = new AdminUsersIndexViewModel
            {
                Total = all.Count,
                Pending = all.Count(r => r.StatusKey == "pending"),
                Active = all.Count(r => r.StatusKey == "active"),
                Suspended = all.Count(r => r.StatusKey is "suspended" or "banned" or "locked" or "rejected"),
                Search = search,
                Role = role,
                Status = status,
                Joined = joined,
                Rows = Filter(all, search, role, status, joined)
            };

            return View(ViewFolder + "Index.cshtml", model);
        }


        [HttpGet("Admin/Users/Export", Order = -1)]
        public async Task<IActionResult> Export(string? search, string? role, string? status, string? joined)
        {
            List<AdminUserRow> rows = Filter(await LoadRowsAsync(), search, role, status, joined);

            var csv = new StringBuilder();
            csv.AppendLine("Name,Email,Phone,Role,Organisation,Status,Joined");

            foreach (AdminUserRow row in rows)
            {
                csv.AppendLine(string.Join(",",
                    Csv(row.Name), Csv(row.Email), Csv(row.Phone), Csv(row.RoleLabel),
                    Csv(row.Organisation), Csv(row.StatusLabel),
                    Csv(row.JoinedAtUtc.HasValue ? SouthAfricaTime.ToLocal(row.JoinedAtUtc.Value).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "")));
            }

            byte[] bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();

            return File(bytes, "text/csv", $"users-{SouthAfricaTime.ToLocal(DateTime.UtcNow):yyyyMMdd}.csv");
        }


        // =========================================================
        // ADD USER
        // =========================================================

        [HttpGet("Admin/Users/Create", Order = -1)]
        public async Task<IActionResult> Create()
        {
            await SetAdminInfoAsync();

            return View(ViewFolder + "Create.cshtml", new AdminUserForm
            {
                Role = "Producer",
                Status = ReskAccountStore.Pending,
                RequirePasswordChange = true
            });
        }


        [HttpPost("Admin/Users/Create", Order = -1)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AdminUserForm form)
        {
            CleanForm(form);
            ValidateForm(form);

            if (ModelState.IsValid && await _userManager.FindByEmailAsync(form.Email) != null)
            {
                ModelState.AddModelError(nameof(form.Email), "An account with this email address already exists.");
            }

            if (!ModelState.IsValid)
            {
                await SetAdminInfoAsync();
                return View(ViewFolder + "Create.cshtml", form);
            }

            string temporaryPassword = MakeTemporaryPassword();

            var user = new IdentityUser
            {
                UserName = form.Email,
                Email = form.Email,
                EmailConfirmed = true,
                PhoneNumber = form.Phone
            };

            IdentityResult result = await _userManager.CreateAsync(user, temporaryPassword);

            if (!result.Succeeded)
            {
                foreach (IdentityError error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                await SetAdminInfoAsync();
                return View(ViewFolder + "Create.cshtml", form);
            }

            bool activate = form.Status == ReskAccountStore.Active;

            if (activate)
            {
                await _userManager.AddToRoleAsync(user, form.Role);
            }

            // Name and organisation live in the profile (also used by Settings).
            ProducerProfile profile = ProducerProfileStore.Load(Root, user.Id);
            profile.FullName = form.FullName;
            profile.Organisation = form.Organisation ?? string.Empty;
            ProducerProfileStore.Save(Root, user.Id, profile);

            ReskAccount account = ReskAccountStore.Load(Root, user.Id);
            account.JoinedAtUtc = DateTime.UtcNow;
            account.Status = activate ? ReskAccountStore.Active : ReskAccountStore.Pending;
            account.RequestedRole = form.Role;
            account.RegistrationReason = "Account created by an administrator";
            account.RequirePasswordChange = form.RequirePasswordChange;
            account.PasswordChangedAtUtc = DateTime.UtcNow;
            ReskAccountStore.Log(account, "Account created", $"By {User.Identity?.Name} • {(activate ? "Active as " + form.Role : "Pending approval")}", "success");
            ReskAccountStore.Save(Root, account);

            TempData["AdminMessage"] =
                $"{form.FullName}'s account was created. Temporary password: {temporaryPassword} — share it with them securely; it is only shown once.";

            return Redirect($"/Admin/Users/{user.Id}");
        }


        // =========================================================
        // USER DETAILS
        // =========================================================

        [HttpGet("Admin/Users/{id}", Order = -1)]
        public async Task<IActionResult> Details(string id, string? tab)
        {
            IdentityUser? user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFoundRedirect();
            }

            await SetAdminInfoAsync();

            AdminUserRow row = await BuildRowAsync(user);
            ProducerProfile profile = ProducerProfileStore.Load(Root, user.Id);
            ReskAccount account = ReskAccountStore.Load(Root, user.Id);

            List<ProducerProposal> proposals =
                await _db.ProducerProposals
                    .AsNoTracking()
                    .Where(p => p.OwnerUserId == user.Id)
                    .OrderByDescending(p => p.UpdatedAtUtc)
                    .ToListAsync();

            var model = new AdminUserDetailsViewModel
            {
                Row = row,
                Tab = tab is "permissions" or "proposals" or "activity" or "security" ? tab : "overview",
                PreferredContact = string.IsNullOrWhiteSpace(profile.PreferredContactMethod) ? "—" : profile.PreferredContactMethod,
                Language = string.IsNullOrWhiteSpace(profile.PreferredLanguage) ? "—" : profile.PreferredLanguage,
                Category = string.IsNullOrWhiteSpace(profile.ProducerCategory) ? "Community Producer" : profile.ProducerCategory,
                IsSelf = user.Id == _userManager.GetUserId(User),
                TwoFactorEnabled = user.TwoFactorEnabled,
                FailedAttempts = user.AccessFailedCount,
                LockedUntilText =
                    user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow && user.LockoutEnd.Value.Year < 9000
                        ? SouthAfricaTime.ToLocal(user.LockoutEnd.Value.UtcDateTime).ToString("d MMM yyyy 'at' HH:mm", CultureInfo.InvariantCulture)
                        : null,
                PasswordChangedText =
                    account.PasswordChangedAtUtc.HasValue
                        ? SouthAfricaTime.LongDate(account.PasswordChangedAtUtc.Value)
                        : "Not recorded",
                PasswordChangedAgo =
                    account.PasswordChangedAtUtc.HasValue
                        ? DaysAgo(account.PasswordChangedAtUtc.Value)
                        : "Changes made before this screen existed are not recorded",
                RequirePasswordChange = account.RequirePasswordChange,
                Permissions = AdminUserAccessController.PermissionRows(Root, user.Id, row.Role) ?? PermissionsFor(row.Role)
            };

            // Proposals tab
            foreach (ProducerProposal p in proposals)
            {
                bool isDraft = p.Status == ProposalStatuses.Draft;

                model.Proposals.Add(new AdminUserProposalRow
                {
                    Id = p.Id,
                    Title = p.DisplayTitle,
                    Reference = !string.IsNullOrWhiteSpace(p.Reference) ? p.Reference : $"DRAFT-{p.Id:D4}",
                    Category = string.IsNullOrWhiteSpace(p.Category) ? "—" : p.Category,
                    StatusKey = p.Status switch
                    {
                        ProposalStatuses.Draft => "draft",
                        ProposalStatuses.InReview => "review",
                        ProposalStatuses.Approved => "approved",
                        ProposalStatuses.ChangesRequested => "changes",
                        ProposalStatuses.Rejected => "rejected",
                        _ => "draft"
                    },
                    StatusLabel = p.Status switch
                    {
                        ProposalStatuses.Draft => "Draft",
                        ProposalStatuses.InReview => "In review",
                        ProposalStatuses.Approved => "Approved",
                        ProposalStatuses.ChangesRequested => "Changes",
                        ProposalStatuses.Rejected => "Not approved",
                        _ => p.Status
                    },
                    UpdatedText = SouthAfricaTime.DayLabel(p.UpdatedAtUtc),
                    IsDraft = isDraft
                });
            }

            // Activity tab: account history + proposal events
            var events = new List<ReskActivity>(account.Activity);

            foreach (ProducerProposal p in proposals)
            {
                if (p.Status == ProposalStatuses.Draft)
                {
                    events.Add(new ReskActivity { AtUtc = p.UpdatedAtUtc, Title = $"Saved changes to {p.DisplayTitle}", Detail = "Draft proposal", Kind = "info" });
                }
                else
                {
                    if (p.SubmittedAtUtc.HasValue)
                    {
                        events.Add(new ReskActivity { AtUtc = p.SubmittedAtUtc.Value, Title = $"Submitted {p.DisplayTitle}", Detail = p.Reference ?? "", Kind = "success" });
                    }

                    if (p.Status != ProposalStatuses.InReview)
                    {
                        events.Add(new ReskActivity
                        {
                            AtUtc = p.UpdatedAtUtc,
                            Title = $"{p.DisplayTitle} marked {model.Proposals.First(x => x.Id == p.Id).StatusLabel.ToLowerInvariant()}",
                            Detail = "Workflow decision",
                            Kind = p.Status == ProposalStatuses.Approved ? "success" : "warning"
                        });
                    }
                }
            }

            DateTime weekAgo = DateTime.UtcNow.AddDays(-7);

            model.Activity =
                events
                    .OrderByDescending(e => e.AtUtc)
                    .Take(30)
                    .Select(e => new AdminUserActivityRow
                    {
                        Title = e.Title,
                        Detail = Capitalise(SouthAfricaTime.Friendly(e.AtUtc)) +
                                 (string.IsNullOrWhiteSpace(e.Detail) ? "" : " • " + e.Detail),
                        Kind = e.Kind
                    })
                    .ToList();

            model.ProposalUpdatesThisWeek = proposals.Count(p => p.UpdatedAtUtc >= weekAgo);
            model.SubmittedCount = proposals.Count(p => p.Status != ProposalStatuses.Draft);
            model.AdminActionsThisWeek = account.Activity.Count(a => a.AtUtc >= weekAgo);

            DateTime? lastActivity = events.Count == 0 ? null : events.Max(e => e.AtUtc);
            model.LastActivityText =
                lastActivity.HasValue
                    ? SouthAfricaTime.ToLocal(lastActivity.Value).ToString("dd MMM yyyy 'at' HH:mm", CultureInfo.InvariantCulture)
                    : "No activity yet";

            return View(ViewFolder + "Details.cshtml", model);
        }


        // =========================================================
        // EDIT
        // =========================================================

        [HttpGet("Admin/Users/{id}/Edit", Order = -1)]
        public async Task<IActionResult> Edit(string id)
        {
            IdentityUser? user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFoundRedirect();
            }

            await SetAdminInfoAsync();

            AdminUserRow row = await BuildRowAsync(user);
            ReskAccount account = ReskAccountStore.Load(Root, user.Id);

            ViewData["UserId"] = user.Id;

            return View(ViewFolder + "Edit.cshtml", new AdminUserForm
            {
                FullName = row.Name == row.Email ? "" : row.Name,
                Email = user.Email ?? "",
                Phone = user.PhoneNumber,
                Organisation = row.Organisation == "—" ? null : row.Organisation,
                Role = row.Role ?? account.RequestedRole ?? "Producer",
                Status = row.StatusKey == "pending" ? ReskAccountStore.Pending : ReskAccountStore.Active
            });
        }


        [HttpPost("Admin/Users/{id}/Edit", Order = -1)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, AdminUserForm form)
        {
            IdentityUser? user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFoundRedirect();
            }

            CleanForm(form);
            ValidateForm(form);

            bool isSelf = user.Id == _userManager.GetUserId(User);

            if (isSelf && form.Role != "Admin")
            {
                ModelState.AddModelError(nameof(form.Role), "You cannot remove the Admin role from your own account.");
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
                await SetAdminInfoAsync();
                ViewData["UserId"] = user.Id;
                return View(ViewFolder + "Edit.cshtml", form);
            }

            var changes = new List<string>();

            // Email (also the login name)
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

            if (profile.FullName != form.FullName || profile.Organisation != (form.Organisation ?? ""))
            {
                profile.FullName = form.FullName;
                profile.Organisation = form.Organisation ?? "";
                ProducerProfileStore.Save(Root, user.Id, profile);
                changes.Add("name / organisation");
            }

            // Role
            ReskAccount account = ReskAccountStore.Load(Root, user.Id);
            IList<string> roles = await _userManager.GetRolesAsync(user);
            bool hasRole = roles.Any(r => SystemRoles.Contains(r));

            if (hasRole && !roles.Contains(form.Role))
            {
                await _userManager.RemoveFromRolesAsync(user, roles.Where(r => SystemRoles.Contains(r)));
                await _userManager.AddToRoleAsync(user, form.Role);
                await _userManager.UpdateSecurityStampAsync(user);
                changes.Add("role → " + form.Role);
            }
            else if (!hasRole)
            {
                account.RequestedRole = form.Role;
            }

            if (changes.Count > 0)
            {
                ReskAccountStore.Log(account, "Account details updated", $"By {User.Identity?.Name} • {string.Join(", ", changes)}", "info");
            }

            ReskAccountStore.Save(Root, account);

            TempData["AdminMessage"] = changes.Count > 0 ? "The account was updated." : "No changes were made.";

            return Redirect($"/Admin/Users/{user.Id}");
        }


        // =========================================================
        // PENDING REGISTRATION
        // =========================================================

        [HttpGet("Admin/Users/{id}/Pending", Order = -1)]
        public async Task<IActionResult> Pending(string id)
        {
            IdentityUser? user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFoundRedirect();
            }

            AdminUserRow row = await BuildRowAsync(user);

            if (row.StatusKey != "pending")
            {
                return Redirect($"/Admin/Users/{user.Id}");
            }

            await SetAdminInfoAsync();

            ReskAccount account = ReskAccountStore.Load(Root, user.Id);

            return View(ViewFolder + "Pending.cshtml", new AdminPendingViewModel
            {
                Row = row,
                RequestedRole = account.RequestedRole ?? "Producer",
                Reason = string.IsNullOrWhiteSpace(account.RegistrationReason) ? "Not provided" : account.RegistrationReason,
                EmailVerified = user.EmailConfirmed,
                PendingSince = row.JoinedAtUtc.HasValue ? SouthAfricaTime.LongDate(row.JoinedAtUtc.Value) : "—",
                Note = account.AdminNote
            });
        }


        [HttpPost("Admin/Users/{id}/Pending", Order = -1)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Pending(string id, string? decision, string? note)
        {
            IdentityUser? user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFoundRedirect();
            }

            if (user.Id == _userManager.GetUserId(User))
            {
                TempData["AdminError"] = "You cannot approve or reject your own account.";
                return Redirect($"/Admin/Users/{user.Id}");
            }

            AdminUserRow row = await BuildRowAsync(user);
            ReskAccount account = ReskAccountStore.Load(Root, user.Id);
            account.AdminNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

            if (decision == "approve")
            {
                string role = SystemRoles.Contains(account.RequestedRole) ? account.RequestedRole! : "Producer";

                await _userManager.AddToRoleAsync(user, role);
                await _userManager.SetLockoutEndDateAsync(user, null);

                account.Status = ReskAccountStore.Active;
                account.StatusChangedAtUtc = DateTime.UtcNow;
                ReskAccountStore.Log(account, "Registration approved", $"By {User.Identity?.Name} • Role: {role}", "success");
                ReskAccountStore.Save(Root, account);

                TempData["AdminMessage"] = $"{row.Name}'s account was approved as {role}.";
            }
            else if (decision == "reject")
            {
                await LockForeverAsync(user);

                account.Status = ReskAccountStore.Rejected;
                account.StatusChangedAtUtc = DateTime.UtcNow;
                ReskAccountStore.Log(account, "Registration rejected", $"By {User.Identity?.Name}" + (account.AdminNote == null ? "" : " • " + account.AdminNote), "danger");
                ReskAccountStore.Save(Root, account);

                TempData["AdminMessage"] = $"{row.Name}'s registration was rejected.";
            }

            return Redirect($"/Admin/Users/{user.Id}");
        }


        // =========================================================
        // ACCOUNT + SECURITY ACTIONS
        // =========================================================

        [HttpPost("Admin/Users/{id}/ResetPassword", Order = -1)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string id)
        {
            IdentityUser? user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFoundRedirect();
            }

            string temporaryPassword = MakeTemporaryPassword();
            string token = await _userManager.GeneratePasswordResetTokenAsync(user);
            IdentityResult result = await _userManager.ResetPasswordAsync(user, token, temporaryPassword);

            if (!result.Succeeded)
            {
                TempData["AdminError"] = string.Join(" ", result.Errors.Select(e => e.Description));
                return Redirect($"/Admin/Users/{id}?tab=security");
            }

            ReskAccount account = ReskAccountStore.Load(Root, user.Id);
            account.RequirePasswordChange = true;
            account.PasswordChangedAtUtc = DateTime.UtcNow;
            ReskAccountStore.Log(account, "Password reset by administrator", $"By {User.Identity?.Name}", "warning");
            ReskAccountStore.Save(Root, account);

            TempData["AdminMessage"] =
                $"New temporary password: {temporaryPassword} — share it with the user securely; it is only shown once. They have been signed out everywhere.";

            return Redirect($"/Admin/Users/{id}?tab=security");
        }


        [HttpPost("Admin/Users/{id}/SignOutAll", Order = -1)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SignOutAll(string id)
        {
            IdentityUser? user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFoundRedirect();
            }

            await _userManager.UpdateSecurityStampAsync(user);

            await LogAsync(user, "Signed out of all sessions", $"By {User.Identity?.Name}", "warning");

            TempData["AdminMessage"] = "All of this user's sessions will be signed out (within about 30 minutes, or straight away on their next sign-in check).";

            return Redirect($"/Admin/Users/{id}?tab=security");
        }


        [HttpPost("Admin/Users/{id}/Lock", Order = -1)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Lock(string id)
        {
            IdentityUser? user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFoundRedirect();
            }

            if (IsSelf(user))
            {
                TempData["AdminError"] = "You cannot lock your own account.";
                return Redirect($"/Admin/Users/{id}?tab=security");
            }

            await _userManager.SetLockoutEnabledAsync(user, true);
            if (ReskAccountStore.IsRestricted(ReskAccountStore.Load(Root, user.Id))) { TempData["AdminError"] = "This account is already suspended or banned."; return Redirect($"/Admin/Users/{id}?tab=security"); } await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddHours(24));
            await _userManager.UpdateSecurityStampAsync(user);

            await LogAsync(user, "Account temporarily locked", $"By {User.Identity?.Name} • 24 hours", "warning");

            TempData["AdminMessage"] = "The account is locked for 24 hours.";

            return Redirect($"/Admin/Users/{id}?tab=security");
        }


        [HttpPost("Admin/Users/{id}/Unlock", Order = -1)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unlock(string id)
        {
            IdentityUser? user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFoundRedirect();
            }

            ReskAccount account = ReskAccountStore.Load(Root, user.Id);

            if (account.Status is ReskAccountStore.Suspended or ReskAccountStore.Banned or ReskAccountStore.Rejected)
            {
                TempData["AdminError"] = "This account is suspended. Use Reactivate account instead.";
                return Redirect($"/Admin/Users/{id}?tab=security");
            }

            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.ResetAccessFailedCountAsync(user);

            await LogAsync(user, "Account unlocked", $"By {User.Identity?.Name}", "success");

            TempData["AdminMessage"] = "The account was unlocked.";

            return Redirect($"/Admin/Users/{id}?tab=security");
        }


        [HttpPost("Admin/Users/{id}/Suspend", Order = -1)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Suspend(string id, string? returnTab)
        {
            IdentityUser? user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFoundRedirect();
            }

            if (IsSelf(user))
            {
                TempData["AdminError"] = "You cannot suspend your own account.";
                return Redirect($"/Admin/Users/{id}");
            }

            await LockForeverAsync(user);

            ReskAccount account = ReskAccountStore.Load(Root, user.Id);
            account.Status = ReskAccountStore.Suspended;
            account.StatusChangedAtUtc = DateTime.UtcNow;
            ReskAccountStore.Log(account, "Account suspended", $"By {User.Identity?.Name}", "danger");
            ReskAccountStore.Save(Root, account);

            TempData["AdminMessage"] = "The account was suspended. The user can no longer sign in.";

            return Redirect($"/Admin/Users/{id}" + TabQuery(returnTab));
        }


        [HttpPost("Admin/Users/{id}/Reactivate", Order = -1)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reactivate(string id, string? returnTab)
        {
            IdentityUser? user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFoundRedirect();
            }

            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.ResetAccessFailedCountAsync(user);

            ReskAccount account = ReskAccountStore.Load(Root, user.Id);
            IList<string> roles = await _userManager.GetRolesAsync(user);

            account.Status = roles.Any(r => SystemRoles.Contains(r)) ? ReskAccountStore.Active : ReskAccountStore.Pending; ReskAccountStore.ClearRestriction(account);
            account.StatusChangedAtUtc = DateTime.UtcNow;
            ReskAccountStore.Log(account, "Account reactivated", $"By {User.Identity?.Name}", "success");
            ReskAccountStore.Save(Root, account);

            TempData["AdminMessage"] = "The account was reactivated.";

            return Redirect($"/Admin/Users/{id}" + TabQuery(returnTab));
        }


        [HttpPost("Admin/Users/{id}/Delete", Order = -1)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            IdentityUser? user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFoundRedirect();
            }

            if (IsSelf(user))
            {
                TempData["AdminError"] = "You cannot delete your own account.";
                return Redirect($"/Admin/Users/{id}");
            }

            int proposalCount = await _db.ProducerProposals.CountAsync(p => p.OwnerUserId == user.Id);

            if (proposalCount > 0)
            {
                TempData["AdminError"] =
                    $"This user has {proposalCount} proposal(s), so the account can't be deleted. Suspend the account instead.";
                return Redirect($"/Admin/Users/{id}");
            }

            string name = (await BuildRowAsync(user)).Name;

            IdentityResult result = await _userManager.DeleteAsync(user);

            if (!result.Succeeded)
            {
                TempData["AdminError"] = string.Join(" ", result.Errors.Select(e => e.Description));
                return Redirect($"/Admin/Users/{id}");
            }

            ReskAccountStore.Delete(Root, id);

            TempData["AdminMessage"] = $"{name}'s account was deleted.";

            return Redirect("/Admin/Users");
        }


        // =========================================================
        // HELPERS - DATA
        // =========================================================

        private async Task<List<AdminUserRow>> LoadRowsAsync()
        {
            List<IdentityUser> users = await _db.Set<IdentityUser>().AsNoTracking().ToListAsync();
            Dictionary<string, List<string>> roles = await LoadRolesAsync();
            Dictionary<string, DateTime> firstProposal = await FirstProposalDatesAsync();

            return users
                .Select(u => MakeRow(u, roles.TryGetValue(u.Id, out List<string>? r) ? r : new List<string>(), firstProposal))
                .OrderBy(r => r.StatusKey == "pending" ? 0 : 1)
                .ThenBy(r => r.Name)
                .ToList();
        }


        private async Task<AdminUserRow> BuildRowAsync(IdentityUser user)
        {
            Dictionary<string, List<string>> roles = await LoadRolesAsync();
            Dictionary<string, DateTime> firstProposal = await FirstProposalDatesAsync();

            return MakeRow(user, roles.TryGetValue(user.Id, out List<string>? r) ? r : new List<string>(), firstProposal);
        }


        // userId -> role names (Set<> is used because this DbContext has its own Roles/UserRoles tables).
        private async Task<Dictionary<string, List<string>>> LoadRolesAsync()
        {
            Dictionary<string, string> roleNames =
                (await _db.Set<IdentityRole>().AsNoTracking().ToListAsync())
                    .ToDictionary(r => r.Id, r => r.Name ?? "");

            var result = new Dictionary<string, List<string>>();

            foreach (IdentityUserRole<string> link in await _db.Set<IdentityUserRole<string>>().AsNoTracking().ToListAsync())
            {
                if (!roleNames.TryGetValue(link.RoleId, out string? name))
                {
                    continue;
                }

                if (!result.TryGetValue(link.UserId, out List<string>? list))
                {
                    list = new List<string>();
                    result[link.UserId] = list;
                }

                list.Add(name);
            }

            return result;
        }


        private async Task<Dictionary<string, DateTime>> FirstProposalDatesAsync()
        {
            return (await _db.ProducerProposals
                    .AsNoTracking()
                    .Select(p => new { p.OwnerUserId, p.CreatedAtUtc })
                    .ToListAsync())
                .GroupBy(p => p.OwnerUserId)
                .ToDictionary(g => g.Key, g => g.Min(p => p.CreatedAtUtc));
        }


        private AdminUserRow MakeRow(IdentityUser user, List<string> roles, Dictionary<string, DateTime> firstProposal)
        {
            ProducerProfile profile = ProducerProfileStore.Load(Root, user.Id);
            ReskAccount account = ReskAccountStore.Load(Root, user.Id);

            // Remember when we first saw this account (older accounts have no join date).
            DateTime? joined = account.JoinedAtUtc;

            if (firstProposal.TryGetValue(user.Id, out DateTime first) && (!joined.HasValue || first < joined.Value))
            {
                joined = first;
            }

            if (!account.JoinedAtUtc.HasValue)
            {
                account.JoinedAtUtc = joined ?? DateTime.UtcNow;
                ReskAccountStore.Save(Root, account);
                joined = account.JoinedAtUtc;
            }

            string? role =
                roles.Contains("Admin") ? "Admin" :
                roles.Contains("Reviewer") ? "Reviewer" :
                roles.Contains("Producer") ? "Producer" :
                roles.FirstOrDefault();

            string statusKey;

            if (account.Status == ReskAccountStore.Banned) { statusKey = "banned"; } else if (account.Status == ReskAccountStore.Rejected)
            {
                statusKey = "rejected";
            }
            else if (account.Status == ReskAccountStore.Suspended)
            {
                statusKey = "suspended";
            }
            else if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow)
            {
                statusKey = "locked";
            }
            else if (role == null)
            {
                statusKey = "pending";
            }
            else
            {
                statusKey = "active";
            }

            string name = ProducerProfileStore.DisplayName(profile, user);

            return new AdminUserRow
            {
                Id = user.Id,
                Name = name,
                Initials = ProducerProfileStore.Initials(name),
                Email = user.Email ?? user.UserName ?? "",
                Phone = string.IsNullOrWhiteSpace(user.PhoneNumber) ? "—" : user.PhoneNumber,
                Role = role,
                RoleLabel = role == null ? $"{account.RequestedRole ?? "Producer"} (requested)" : AdminUserAccessController.RoleLabel(Root, user.Id, role),
                Organisation = string.IsNullOrWhiteSpace(profile.Organisation) ? "—" : profile.Organisation,
                StatusKey = statusKey,
                StatusLabel = statusKey switch
                {
                    "active" => "Active",
                    "pending" => "Pending",
                    "locked" => "Locked",
                    "suspended" => "Suspended", "banned" => "Banned",
                    "rejected" => "Rejected",
                    _ => statusKey
                },
                JoinedAtUtc = joined,
                JoinedText = joined.HasValue ? SouthAfricaTime.ToLocal(joined.Value).ToString("dd MMM yyyy", CultureInfo.InvariantCulture) : "—",
                UserCode = "USR-" + (user.Id.Length >= 8 ? user.Id.Substring(0, 8) : user.Id).ToUpperInvariant(),
                AvatarColour = AvatarColour(user.Id)
            };
        }


        private static List<AdminUserRow> Filter(List<AdminUserRow> rows, string? search, string? role, string? status, string? joined)
        {
            IEnumerable<AdminUserRow> query = rows;

            if (!string.IsNullOrWhiteSpace(search))
            {
                string term = search.Trim();
                query = query.Where(r =>
                    r.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    r.Email.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    r.Organisation.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(role))
            {
                query = role == "none"
                    ? query.Where(r => r.Role == null)
                    : query.Where(r => r.Role == role);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = status == "suspended"
                    ? query.Where(r => r.StatusKey is "suspended" or "banned" or "locked" or "rejected")
                    : query.Where(r => r.StatusKey == status);
            }

            if (!string.IsNullOrWhiteSpace(joined))
            {
                DateTime now = DateTime.UtcNow;
                DateTime from = joined switch
                {
                    "7" => now.AddDays(-7),
                    "30" => now.AddDays(-30),
                    "year" => new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    _ => DateTime.MinValue
                };

                query = query.Where(r => r.JoinedAtUtc.HasValue && r.JoinedAtUtc.Value >= from);
            }

            return query.ToList();
        }


        private async Task LockForeverAsync(IdentityUser user)
        {
            await _userManager.SetLockoutEnabledAsync(user, true);
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
            await _userManager.UpdateSecurityStampAsync(user);
        }


        private async Task LogAsync(IdentityUser user, string title, string detail, string kind)
        {
            ReskAccount account = ReskAccountStore.Load(Root, user.Id);
            ReskAccountStore.Log(account, title, detail, kind);
            ReskAccountStore.Save(Root, account);
            await Task.CompletedTask;
        }


        private bool IsSelf(IdentityUser user)
        {
            return user.Id == _userManager.GetUserId(User);
        }


        private IActionResult NotFoundRedirect()
        {
            TempData["AdminError"] = "That user could not be found.";
            return Redirect("/Admin/Users");
        }


        private async Task SetAdminInfoAsync()
        {
            IdentityUser? me = await _userManager.GetUserAsync(User);
            ProducerProfile profile = me == null ? new ProducerProfile() : ProducerProfileStore.Load(Root, me.Id);

            bool hasName = !string.IsNullOrWhiteSpace(profile.FullName);

            ViewData["AdminName"] = hasName ? profile.FullName.Trim() : "Admin User";
            ViewData["AdminInitials"] = hasName ? ProducerProfileStore.Initials(profile.FullName) : "AD";
        }


        // =========================================================
        // HELPERS - FORMS + TEXT
        // =========================================================

        private static void CleanForm(AdminUserForm form)
        {
            form.FullName = (form.FullName ?? "").Trim();
            form.Email = (form.Email ?? "").Trim();
            form.Phone = string.IsNullOrWhiteSpace(form.Phone) ? null : form.Phone.Trim();
            form.Organisation = string.IsNullOrWhiteSpace(form.Organisation) ? null : form.Organisation.Trim();
        }


        private void ValidateForm(AdminUserForm form)
        {
            if (!SystemRoles.Contains(form.Role))
            {
                ModelState.AddModelError(nameof(form.Role), "Choose a role.");
            }

            if (form.Status != ReskAccountStore.Active && form.Status != ReskAccountStore.Pending)
            {
                form.Status = ReskAccountStore.Pending;
            }
        }


        // 14 characters with upper, lower, digit and symbol (meets Identity's default rules).
        private static string MakeTemporaryPassword()
        {
            const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string lower = "abcdefghijkmnpqrstuvwxyz";
            const string digits = "23456789";
            const string symbols = "!@#$%*?";
            string all = upper + lower + digits + symbols;

            var chars = new List<char>
            {
                upper[RandomNumberGenerator.GetInt32(upper.Length)],
                lower[RandomNumberGenerator.GetInt32(lower.Length)],
                digits[RandomNumberGenerator.GetInt32(digits.Length)],
                symbols[RandomNumberGenerator.GetInt32(symbols.Length)]
            };

            while (chars.Count < 14)
            {
                chars.Add(all[RandomNumberGenerator.GetInt32(all.Length)]);
            }

            return new string(chars.OrderBy(_ => RandomNumberGenerator.GetInt32(1000)).ToArray());
        }


        private static string Capitalise(string text)
        {
            return string.IsNullOrEmpty(text) ? text : char.ToUpper(text[0]) + text.Substring(1);
        }


        private static string DaysAgo(DateTime utc)
        {
            int days = (int)Math.Floor((DateTime.UtcNow - utc).TotalDays);

            return days <= 0 ? "Today" : days == 1 ? "1 day ago" : $"{days} days ago";
        }


        private static string TabQuery(string? tab)
        {
            return tab is "permissions" or "proposals" or "activity" or "security" ? "?tab=" + tab : "";
        }


        private static string AvatarColour(string id)
        {
            string[] colours = { "#12b8ce", "#1d3557", "#5b6b85", "#7a8aa8", "#c9444d", "#1f9a66", "#d39a12" };
            int sum = id.Sum(c => c);
            return colours[sum % colours.Length];
        }


        private static string Csv(string? value)
        {
            value ??= "";
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }


        // What each role can do (shown on the Permissions tab).
        private static List<AdminPermissionRow> PermissionsFor(string? role)
        {
            // View, Create, Edit, Delete, Approve, Export
            return role switch
            {
                "Admin" => new List<AdminPermissionRow>
                {
                    new("All proposals", true, false, true, false, true, true),
                    new("Users", true, true, true, true, true, true),
                    new("Reviews & decisions", true, true, true, false, true, false),
                    new("Reports", true, true, false, false, false, true),
                    new("System settings", true, false, true, false, false, false)
                },
                "Reviewer" => new List<AdminPermissionRow>
                {
                    new("Assigned proposals", true, false, false, false, false, false),
                    new("Reviews", true, true, true, false, false, false),
                    new("Guidelines", true, false, false, false, false, false),
                    new("Profile settings", true, false, true, false, false, false),
                    new("Reports", false, false, false, false, false, false)
                },
                _ => new List<AdminPermissionRow>
                {
                    new("My proposals", true, true, true, false, false, false),
                    new("Drafts", true, true, true, true, false, false),
                    new("Guidelines", true, false, false, false, false, false),
                    new("Profile settings", true, false, true, false, false, false),
                    new("Reports", false, false, false, false, false, false)
                }
            };
        }
    }
}


namespace RESK.WIL.Models
{
    public class AdminUserRow
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Initials { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string? Role { get; set; }
        public string RoleLabel { get; set; } = "";
        public string Organisation { get; set; } = "";
        public string StatusKey { get; set; } = "";
        public string StatusLabel { get; set; } = "";
        public DateTime? JoinedAtUtc { get; set; }
        public string JoinedText { get; set; } = "";
        public string UserCode { get; set; } = "";
        public string AvatarColour { get; set; } = "#12b8ce";
    }

    public class AdminUsersIndexViewModel
    {
        public int Total { get; set; }
        public int Pending { get; set; }
        public int Active { get; set; }
        public int Suspended { get; set; }
        public string? Search { get; set; }
        public string? Role { get; set; }
        public string? Status { get; set; }
        public string? Joined { get; set; }
        public List<AdminUserRow> Rows { get; set; } = new();
    }

    public class AdminUserForm
    {
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Enter the user's full name.")]
        [System.ComponentModel.DataAnnotations.StringLength(100)]
        public string FullName { get; set; } = "";

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Enter an email address.")]
        [System.ComponentModel.DataAnnotations.EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public string Email { get; set; } = "";

        [System.ComponentModel.DataAnnotations.StringLength(30)]
        [System.ComponentModel.DataAnnotations.RegularExpression(@"^[0-9+()\s-]*$", ErrorMessage = "Use numbers, spaces, + and - only.")]
        public string? Phone { get; set; }

        [System.ComponentModel.DataAnnotations.StringLength(150)]
        public string? Organisation { get; set; }

        public string Role { get; set; } = "Producer";

        // "Pending" or "Active"
        public string Status { get; set; } = "Pending";

        public bool RequirePasswordChange { get; set; } = true;
    }

    public class AdminPermissionRow
    {
        public AdminPermissionRow(string module, bool view, bool create, bool edit, bool delete, bool approve, bool export)
        {
            Module = module; View = view; Create = create; Edit = edit; Delete = delete; Approve = approve; Export = export;
        }

        public string Module { get; }
        public bool View { get; }
        public bool Create { get; }
        public bool Edit { get; }
        public bool Delete { get; }
        public bool Approve { get; }
        public bool Export { get; }
    }

    public class AdminUserProposalRow
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Reference { get; set; } = "";
        public string Category { get; set; } = "";
        public string StatusKey { get; set; } = "";
        public string StatusLabel { get; set; } = "";
        public string UpdatedText { get; set; } = "";
        public bool IsDraft { get; set; }
    }

    public class AdminUserActivityRow
    {
        public string Title { get; set; } = "";
        public string Detail { get; set; } = "";
        public string Kind { get; set; } = "info";
    }

    public class AdminUserDetailsViewModel
    {
        public AdminUserRow Row { get; set; } = new();
        public string Tab { get; set; } = "overview";
        public string PreferredContact { get; set; } = "—";
        public string Language { get; set; } = "—";
        public string Category { get; set; } = "Community Producer";
        public bool IsSelf { get; set; }
        public bool TwoFactorEnabled { get; set; }
        public int FailedAttempts { get; set; }
        public string? LockedUntilText { get; set; }
        public string PasswordChangedText { get; set; } = "Not recorded";
        public string PasswordChangedAgo { get; set; } = "";
        public bool RequirePasswordChange { get; set; }
        public string LastActivityText { get; set; } = "No activity yet";
        public int ProposalUpdatesThisWeek { get; set; }
        public int SubmittedCount { get; set; }
        public int AdminActionsThisWeek { get; set; }
        public List<AdminPermissionRow> Permissions { get; set; } = new();
        public List<AdminUserProposalRow> Proposals { get; set; } = new();
        public List<AdminUserActivityRow> Activity { get; set; } = new();
    }

    public class AdminPendingViewModel
    {
        public AdminUserRow Row { get; set; } = new();
        public string RequestedRole { get; set; } = "Producer";
        public string Reason { get; set; } = "";
        public bool EmailVerified { get; set; }
        public string PendingSince { get; set; } = "";
        public string? Note { get; set; }
    }
}