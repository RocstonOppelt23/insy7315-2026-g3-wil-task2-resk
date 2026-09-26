using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RESK.WIL.Data;
using RESK.WIL.Models;

namespace RESK.WIL.Controllers
{
    //adding new line?
    [ApiController]
    [Route("api/system")]
    public class ApiSystemController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public ApiSystemController(ApplicationDbContext db)
        {
            _db = db;
        }

        // GET /api/system/features
        [HttpGet]
        public async Task<ActionResult<SystemResponse>> Get(
            CancellationToken cancellationToken)
        {
            var settings = await _db.SystemFeatureSettings
                .AsNoTracking()
                .SingleOrDefaultAsync(s => s.Id == 1, cancellationToken);

            if (settings is null)
                return NotFound("System feature settings cannot GET.");

            return Ok(ToResponse(settings));
        }

        // PUT /api/system/features
        [HttpPut]
        [Authorize(Policy = "ManageSystemSettings")]
        public async Task<ActionResult<SystemResponse>> Update(
            [FromBody] UpdateSystemRequest request,
            CancellationToken cancellationToken)
        {
            var settings = await _db.SystemFeatureSettings
                .SingleOrDefaultAsync(s => s.Id == 1, cancellationToken);

            if (settings is null)
                return NotFound("System feature settings cannot PUT.");

            settings.UsersEnabled = request.UsersEnabled;
            settings.ProposalsEnabled = request.ProposalsEnabled;
            settings.ReportsEnabled = request.ReportsEnabled;
            settings.AuditEnabled = request.AuditEnabled;
            settings.RolesEnabled = request.RolesEnabled;
            settings.SettingsEnabled = request.SettingsEnabled;

            await _db.SaveChangesAsync(cancellationToken);

            return Ok(ToResponse(settings));
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

    // The client sends only editable fields, not the database Id.
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
}
