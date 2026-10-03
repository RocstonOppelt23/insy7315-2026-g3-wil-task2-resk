using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RESK.WIL.Data;
using RESK.WIL.Models;

namespace RESK.WIL.Services
{
    // Central API permission gateway.
    // All controllers should ask this service before returning data so a user
    // cannot bypass disabled system features or role scope rules from Postman,
    // Swagger, or a changed front end.
    public class AccessControlService
    {
        private readonly ApplicationDbContext _db;
        private readonly bool _bypassServiceLogic;

        public AccessControlService(
            ApplicationDbContext db,
            IConfiguration configuration)
        {
            _db = db;
            _bypassServiceLogic = configuration.GetValue<bool>(
                "ApiTesting:BypassServiceLogic");
        }

        public bool BypassServiceLogic => _bypassServiceLogic;

        public async Task<User?> GetCurrentUserAsync(
            ClaimsPrincipal principal,
            CancellationToken cancellationToken)
        {
            // ASP Identity stores the logged-in user id in the auth cookie.
            // If the request has no valid session cookie, API testing tools will
            // reach this branch and the controller should return 401.
            // When ApiTesting:BypassServiceLogic is true, Swagger/Postman uses
            // the first database user as a local test user instead.
            if (!int.TryParse(
                    principal.FindFirstValue(ClaimTypes.NameIdentifier),
                    out int userId))
            {
                return _bypassServiceLogic
                    ? await GetFallbackTestUserAsync(cancellationToken)
                    : null;
            }

            var user = await _db.Users
                .Include(u => u.Role)
                .SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);

            return user ?? (_bypassServiceLogic
                ? await GetFallbackTestUserAsync(cancellationToken)
                : null);
        }

        public async Task<SystemFeatureSettings> GetSystemSettingsAsync(
            CancellationToken cancellationToken)
        {
            // The system uses one settings row as the global feature switch.
            // Creating a default row here keeps API calls from failing when the
            // database is new but migrations already created the table.
            var settings = await _db.SystemFeatureSettings
                .SingleOrDefaultAsync(s => s.Id == 1, cancellationToken);

            if (settings is not null)
                return settings;

            settings = new SystemFeatureSettings { Id = 1 };
            _db.SystemFeatureSettings.Add(settings);
            await _db.SaveChangesAsync(cancellationToken);

            return settings;
        }

        public static bool IsActive(User? user) =>
            user?.AccountStatus == UserAccountStatus.Active;

        // Proposal access requires every layer to pass:
        // 1. the proposal feature is enabled,
        // 2. the user account is active,
        // 3. the user has one assigned custom role,
        // 4. that role has at least one proposal permission.
        public async Task<bool> CanUseProposalsAsync(
            User? user,
            CancellationToken cancellationToken)
        {
            if (_bypassServiceLogic)
                return true;

            var settings = await GetSystemSettingsAsync(cancellationToken);

            return settings.ProposalsEnabled &&
                   IsActive(user) &&
                   user!.Role is not null &&
                   HasAnyProposalPermission(user.Role);
        }

        public async Task<bool> CanCreateProposalAsync(
            User? user,
            CancellationToken cancellationToken)
        {
            if (_bypassServiceLogic)
                return true;

            var settings = await GetSystemSettingsAsync(cancellationToken);

            return settings.ProposalsEnabled &&
                   IsActive(user) &&
                   user!.Role?.ProposalsCreate == true;
        }

        public async Task<bool> CanEditProposalAsync(
            User? user,
            CancellationToken cancellationToken)
        {
            if (_bypassServiceLogic)
                return true;

            var settings = await GetSystemSettingsAsync(cancellationToken);

            return settings.ProposalsEnabled &&
                   IsActive(user) &&
                   user!.Role is not null &&
                   (user.Role.ProposalsEdit || user.Role.ProposalsCreate);
        }

        public async Task<bool> CanReviewProposalAsync(
            User? user,
            CancellationToken cancellationToken)
        {
            if (_bypassServiceLogic)
                return true;

            var settings = await GetSystemSettingsAsync(cancellationToken);

            return settings.ProposalsEnabled &&
                   IsActive(user) &&
                   user!.Role is not null &&
                   (user.Role.ProposalsApprove ||
                    user.Role.ProposalsEdit ||
                    user.Role.ProposalsScope == AccessScope.All);
        }

        public async Task<bool> CanUseUsersAsync(
            User? user,
            CancellationToken cancellationToken)
        {
            if (_bypassServiceLogic)
                return true;

            var settings = await GetSystemSettingsAsync(cancellationToken);

            return settings.UsersEnabled &&
                   IsActive(user) &&
                   user!.Role?.UsersView == true;
        }

        public async Task<bool> CanManageUsersAsync(
            User? user,
            CancellationToken cancellationToken)
        {
            if (_bypassServiceLogic)
                return true;

            var settings = await GetSystemSettingsAsync(cancellationToken);

            return settings.UsersEnabled &&
                   IsActive(user) &&
                   user!.Role is not null &&
                   (user.Role.UsersEdit ||
                    user.Role.UsersApprove ||
                    user.Role.UsersDelete);
        }

        public bool CanManageSystemSettings(User? user) =>
            _bypassServiceLogic ||
            (IsActive(user) &&
             user!.Role?.SettingsEdit == true);

        // Scope is applied after permission is accepted but before filters,
        // sorting, and projection. This keeps hidden records out of dashboard
        // counts and search results even if the front end sends custom queries.
        public IQueryable<Proposal> ApplyProposalScope(
            IQueryable<Proposal> query,
            User user)
        {
            if (_bypassServiceLogic)
                return query;

            return user.Role?.ProposalsScope switch
            {
                AccessScope.All => query,
                AccessScope.SelectedRoles when user.RoleId.HasValue =>
                    query.Where(p =>
                        _db.RoleScopeTargets.Any(t =>
                            t.RoleId == user.RoleId.Value &&
                            t.Area == RoleScopeArea.Proposals &&
                            t.TargetRoleId == p.Producer.RoleId)),
                _ => query.Where(p => p.ProducerId == user.Id)
            };
        }

        // User search never exposes passwords, ID numbers, security answers, or
        // MFA values. This scope only decides which user rows may be summarized.
        public IQueryable<User> ApplyUserScope(
            IQueryable<User> query,
            User user)
        {
            if (_bypassServiceLogic)
                return query;

            return user.Role?.UsersScope switch
            {
                AccessScope.All => query,
                AccessScope.SelectedRoles when user.RoleId.HasValue =>
                    query.Where(u =>
                        u.RoleId.HasValue &&
                        _db.RoleScopeTargets.Any(t =>
                            t.RoleId == user.RoleId.Value &&
                            t.Area == RoleScopeArea.Users &&
                            t.TargetRoleId == u.RoleId.Value)),
                _ => query.Where(u => u.Id == user.Id)
            };
        }

        private static bool HasAnyProposalPermission(AccessRole role) =>
            role.ProposalsView ||
            role.ProposalsCreate ||
            role.ProposalsEdit ||
            role.ProposalsDelete ||
            role.ProposalsAssign ||
            role.ProposalsApprove ||
            role.ProposalsExport;

        private async Task<User?> GetFallbackTestUserAsync(
            CancellationToken cancellationToken)
        {
            var testUser = await _db.Users
                .Include(u => u.Role)
                .OrderBy(u => u.Id)
                .FirstOrDefaultAsync(cancellationToken);

            return testUser ?? new User
            {
                Id = 0,
                AccountStatus = UserAccountStatus.Active,
                Role = new AccessRole
                {
                    Title = "Local API Test Role",
                    UsersView = true,
                    UsersEdit = true,
                    UsersDelete = true,
                    UsersApprove = true,
                    UsersScope = AccessScope.All,
                    ProposalsView = true,
                    ProposalsCreate = true,
                    ProposalsEdit = true,
                    ProposalsDelete = true,
                    ProposalsAssign = true,
                    ProposalsApprove = true,
                    ProposalsExport = true,
                    ProposalsScope = AccessScope.All,
                    SettingsEdit = true,
                    SettingsScope = AccessScope.All
                }
            };
        }
    }
}
