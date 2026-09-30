using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RESK.WIL.Data;
using RESK.WIL.Services;

namespace RESK.WIL.Controllers.API
{
    [ApiController]
    [Route("api/review")]
    [Authorize(Roles = "Reviewer,Admin")]
    public class ApiReviewController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public ApiReviewController(ApplicationDbContext db)
        {
            _db = db;
        }

        // GET /api/review/queue
        [HttpGet("queue")]
        public async Task<ActionResult<List<ReviewQueueItem>>> GetQueue(
            CancellationToken ct)
        {
            var items = await _db.Proposals
                .AsNoTracking()
                .Where(p => p.ProposalStatus == ProposalWorkflow.Submitted
                         || p.ProposalStatus == ProposalWorkflow.UnderReview)
                .OrderBy(p => p.SubmittedAtUtc)
                .Select(p => new ReviewQueueItem(
                    p.Id,
                    p.ProgrammeTitle,
                    p.ProposalType,
                    p.ProposalStatus,
                    p.SubmittedAtUtc,
                    p.UpdatedAtUtc))
                .ToListAsync(ct);

            return Ok(items);
        }

        [HttpPost("{id:int}/start")]
        public Task<ActionResult<ReviewActionResponse>> Start(
            int id, CancellationToken ct) =>
            Move(id, ProposalWorkflow.UnderReview, "Review started", ct);

        [HttpPost("{id:int}/approve")]
        public Task<ActionResult<ReviewActionResponse>> Approve(
            int id, CancellationToken ct) =>
            Move(id, ProposalWorkflow.Approved, "Approved", ct);

        [HttpPost("{id:int}/decline")]
        public Task<ActionResult<ReviewActionResponse>> Decline(
            int id, CancellationToken ct) =>
            Move(id, ProposalWorkflow.Declined, "Declined", ct);

        [HttpPost("{id:int}/request-changes")]
        public Task<ActionResult<ReviewActionResponse>> RequestChanges(
            int id, CancellationToken ct) =>
            Move(id, ProposalWorkflow.Draft, "Changes requested", ct);

        private async Task<ActionResult<ReviewActionResponse>> Move(
            int id, string to, string action, CancellationToken ct)
        {
            if (!int.TryParse(
                    User.FindFirstValue(ClaimTypes.NameIdentifier),
                    out int userId))
                return Unauthorized();

            var proposal = await _db.Proposals
                .SingleOrDefaultAsync(p => p.Id == id, ct);

            if (proposal is null)
                return NotFound();

            if (!ProposalWorkflow.TryMove(proposal, to, out string error))
                return Conflict(error);

            proposal.LastChangerId = userId;
            proposal.LastAction = action;

            await _db.SaveChangesAsync(ct);

            return Ok(new ReviewActionResponse(
                proposal.Id,
                proposal.ProposalStatus,
                action,
                proposal.UpdatedAtUtc));
        }
    }

    public record ReviewQueueItem(
        int Id,
        string ProgrammeTitle,
        string ProposalType,
        string ProposalStatus,
        DateTime? SubmittedAtUtc,
        DateTime UpdatedAtUtc);

    public record ReviewActionResponse(
        int Id,
        string ProposalStatus,
        string Action,
        DateTime UpdatedAtUtc);
}