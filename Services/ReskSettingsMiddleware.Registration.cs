using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace RESK.WIL.Services
{
    // ReskSettingsMiddleware: the Registration tab.
    public partial class ReskSettingsMiddleware
    {
        private static readonly string[] AdminKeyNames =
        {
            "adminregistrationkey", "adminregisterkey", "adminkey", "adminsignupkey", "adminregistrationcode",
            "admincode", "adminsecret", "adminsecretkey", "adminpasskey", "adminaccesskey"
        };


        private async Task RegisterAsync(HttpContext context, string root, ReskSettings settings, bool post)
        {
            // ---------- registration is closed ----------
            if (!settings.AllowRegistration)
            {
                context.Response.Redirect("/Account/RegistrationClosed");
                return;
            }

            RESK.WIL.Data.ApplicationDbContext? db = context.RequestServices.GetService<RESK.WIL.Data.ApplicationDbContext>();
            UserManager<IdentityUser>? users = context.RequestServices.GetService<UserManager<IdentityUser>>();
            IFormCollection? form = post ? await SimpleFormAsync(context) : null;
            string email = post ? await FormValueAsync(context, "Email", "Input.Email", "email", "UserName", "Username") : "";

            if (!post || db == null || users == null || email.Length == 0)
            {
                await _next(context);
                return;
            }

            string normalised = email.ToUpperInvariant();
            bool existed = await db.Set<IdentityUser>().AsNoTracking().AnyAsync(u => u.NormalizedEmail == normalised || u.NormalizedUserName == normalised);

            await _next(context);

            if (existed)
            {
                return;
            }

            IdentityUser? user = await users.FindByEmailAsync(email) ?? await users.FindByNameAsync(email);

            if (user == null)
            {
                // The registration page turned it down (validation error).
                return;
            }

            await NewAccountAsync(root, settings, users, user, form);
        }


        // Makes a brand-new account follow the Registration settings.
        private static async Task NewAccountAsync(string root, ReskSettings settings, UserManager<IdentityUser> users,
            IdentityUser user, IFormCollection? form)
        {
            List<string> roles = (await users.GetRolesAsync(user)).Where(r => SignInRoles.Contains(r)).ToList();
            ReskAccount account = ReskAccountStore.Load(root, user.Id);
            account.JoinedAtUtc ??= DateTime.UtcNow;

            // ---------- administrator registration key ----------
            bool wrongKey = false;

            if (roles.Contains("Admin") && settings.RequireAdminKey && settings.AdminKey.Length > 0)
            {
                bool typed = form != null && form.Any(field => field.Value.Any(v => v == settings.AdminKey));

                if (!typed)
                {
                    wrongKey = true;
                }
            }

            if (settings.RequireApproval || wrongKey)
            {
                // ---------- wait for an administrator ----------
                string requested = wrongKey || roles.Count == 0 ? settings.DefaultRole : roles[0];

                if (roles.Count > 0)
                {
                    await users.RemoveFromRolesAsync(user, roles);
                }

                account.Status = ReskAccountStore.Pending;
                account.RequestedRole = requested;
                account.RegistrationReason ??= wrongKey
                    ? "Tried to register as an administrator without the correct registration key."
                    : "Registered on the sign-up page.";

                ReskAccountStore.Log(account, "Account registered", "Waiting for an administrator to approve it", "info");
            }
            else
            {
                // ---------- active straight away ----------
                if (roles.Count == 0)
                {
                    await users.AddToRoleAsync(user, settings.DefaultRole);
                    roles.Add(settings.DefaultRole);
                }

                account.Status = ReskAccountStore.Active;
                ReskAccountStore.Log(account, "Account registered", "Active straight away as " + roles[0], "success");
            }

            account.StatusChangedAtUtc = DateTime.UtcNow;
            account.PasswordChangedAtUtc ??= DateTime.UtcNow;
            ReskAccountStore.Save(root, account);
        }


        // Keeps the key on the Registration tab and the key in the app's own
        // configuration (appsettings / user-secrets) the same, when one is found there.
        private static void SyncAdminKey(HttpContext context, string root, ReskSettings settings)
        {
            IConfiguration? configuration = context.RequestServices.GetService<IConfiguration>();

            if (configuration == null)
            {
                return;
            }

            string? name = configuration.AsEnumerable()
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
                .Select(pair => pair.Key)
                .FirstOrDefault(key =>
                {
                    string leaf = new string(key.Substring(key.LastIndexOf(':') + 1).Where(char.IsLetter).ToArray()).ToLowerInvariant();
                    return AdminKeyNames.Contains(leaf);
                });

            if (name == null)
            {
                return;
            }

            if (settings.AdminKey.Length == 0)
            {
                // First run: show the key the app already uses.
                ReskSettings copy = ReskSettingsStore.Editable(root);
                copy.AdminKey = configuration[name] ?? "";
                ReskSettingsStore.Save(root, copy, null);
            }
            else if (configuration[name] != settings.AdminKey)
            {
                configuration[name] = settings.AdminKey;
            }
        }
    }
}