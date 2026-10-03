using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RESK.WIL.Data;
using RESK.WIL.Models;
using RESK.WIL.Services;

namespace RESK.WIL.Controllers.API
{
    [ApiController]
    [Route("api/system")]
    public class ApiSystemController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly AccessControlService _access;

        public ApiSystemController(
            ApplicationDbContext db,
            AccessControlService access)
        {
            _db = db;
            _access = access;
        }

        // GET /api/system
        [HttpGet]
        public async Task<ActionResult<SystemResponse>> Get(
            CancellationToken cancellationToken)
        {
            // the front end can check whether a feature is enabled
            // before showing menu items. Protected API endpoints still enforce
            // these switches again on the server.
            var settings = await GetSettings(cancellationToken);

            return Ok(ToResponse(settings));
        }

        // PUT /api/system/update
        [HttpPut("update")]
        [Authorize(Policy = "ManageSystemSettings")]
        public async Task<ActionResult<SystemResponse>> Update(
            [FromBody] UpdateSystemRequest request,
            CancellationToken cancellationToken)
        {
            var currentUser = await _access.GetCurrentUserAsync(
                User,
                cancellationToken);

            if (currentUser is null)
                return Unauthorized();

            if (!_access.CanManageSystemSettings(currentUser))
                return Forbid();

            // These feature switches are the first layer checked by
            // AccessControlService before role permissions and scopes.
            var settings = await GetSettings(cancellationToken);

            settings.UsersEnabled = request.UsersEnabled;
            settings.ProposalsEnabled = request.ProposalsEnabled;
            settings.ReportsEnabled = request.ReportsEnabled;
            settings.AuditEnabled = request.AuditEnabled;
            settings.RolesEnabled = request.RolesEnabled;
            settings.SettingsEnabled = request.SettingsEnabled;

            await _db.SaveChangesAsync(cancellationToken);

            return Ok(ToResponse(settings));
        }

        private async Task<SystemFeatureSettings> GetSettings(
            CancellationToken cancellationToken)
        {
            var settings = await _db.SystemFeatureSettings
                .SingleOrDefaultAsync(s => s.Id == 1, cancellationToken);

            if (settings is not null)
                return settings;

            settings = new SystemFeatureSettings { Id = 1 };
            _db.SystemFeatureSettings.Add(settings);
            await _db.SaveChangesAsync(cancellationToken);

            return settings;
        }

        private static SystemResponse ToResponse(
            SystemFeatureSettings settings) =>
            new(
                settings.UsersEnabled,
                settings.ProposalsEnabled,
                settings.ReportsEnabled,
                settings.AuditEnabled,
                settings.RolesEnabled,
                settings.SettingsEnabled);
    }

    public record UpdateSystemRequest(
        bool UsersEnabled,
        bool ProposalsEnabled,
        bool ReportsEnabled,
        bool AuditEnabled,
        bool RolesEnabled,
        bool SettingsEnabled);

    public record SystemResponse(
        bool UsersEnabled,
        bool ProposalsEnabled,
        bool ReportsEnabled,
        bool AuditEnabled,
        bool RolesEnabled,
        bool SettingsEnabled);

    //----------Constructor----------//
}
