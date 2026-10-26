using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RESK.WIL.Data;
using RESK.WIL.Controllers.API;
using RESK.WIL.Models;

namespace RESK.WIL.Controllers
{
    /// <summary>Feature switches for the admin modules.</summary>
    [Route("api/system")]
    public class ApiSystemController : ApiControllerBase
    {
        private readonly ApplicationDbContext _db;

        public ApiSystemController(ApplicationDbContext db)
        {
            _db = db;
        }

        /// <summary>Gets which admin modules are switched on.</summary>
        /// <response code="200">The current feature settings.</response>
        /// <response code="404">The settings row has not been created yet.</response>
        [HttpGet]
        [ProducesResponseType(typeof(SystemResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<SystemResponse>> Get(
            CancellationToken cancellationToken)
        {
            var settings = await _db.SystemFeatureSettings
                .AsNoTracking()
                .SingleOrDefaultAsync(s => s.Id == 1, cancellationToken);

            if (settings is null)
                return NotFoundProblem("System feature settings have not been set up yet.");

            return Ok(ToResponse(settings));
        }

        /// <summary>Switches admin modules on or off (Admin only).</summary>
        /// <response code="200">The updated feature settings.</response>
        /// <response code="400">The request body is invalid.</response>
        /// <response code="404">The settings row has not been created yet.</response>
        [HttpPut("update")]
        [ProducesResponseType(typeof(SystemResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [Authorize(Policy = "ManageSystemSettings")]
        public async Task<ActionResult<SystemResponse>> Update(
            [FromBody] UpdateSystemRequest request,
            CancellationToken cancellationToken)
        {
            var settings = await _db.SystemFeatureSettings
                .SingleOrDefaultAsync(s => s.Id == 1, cancellationToken);

            if (settings is null)
                return NotFoundProblem("System feature settings have not been set up yet.");

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
