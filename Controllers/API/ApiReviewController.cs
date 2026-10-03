using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RESK.WIL.Data;
using RESK.WIL.Models;
using RESK.WIL.Services;

namespace RESK.WIL.Controllers.API
{
    [ApiController]
    [Route("api/review")]
    [Authorize(Policy = "ManageProposals")]
    public class ApiReviewController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly AccessControlService _access;

        public ApiReviewController(
            ApplicationDbContext db,
            AccessControlService access)
        {
            _db = db;
            _access = access;
        }

        // Rocston's code: review queue endpoint for submitted proposals.
        // Changed for integration with Kuan-Chi's code: authorization uses AccessRole permissions
        // instead Identity role names, because each user has one selected AccessRole.
        // GET /api/review/queue
        [HttpGet("queue")]
        public async Task<ActionResult<List<ReviewQueueItem>>> GetQueue(
            CancellationToken cancellationToken)
        {
            var currentUser = await _access.GetCurrentUserAsync(
                User,
                cancellationToken);

            if (!await _access.CanReviewProposalAsync(currentUser, cancellationToken))
                return Forbid();

            var items = await _db.Proposals
                .AsNoTracking()
                .Where(p => p.ProposalStatus == ProposalWorkflow.Pending
                         || p.ProposalStatus == ProposalWorkflow.UnderReview)
                .OrderBy(p => p.SubmittedAtUtc)
                .Select(p => new ReviewQueueItem(
                    p.Id,
                    p.ProgrammeTitle,
                    p.ProposalType,
                    p.ProposalStatus,
                    p.SubmittedAtUtc,
                    p.UpdatedAtUtc))
                .ToListAsync(cancellationToken);

            return Ok(items);
        }

        [HttpPost("{id:int}/start")]
        public Task<ActionResult<ReviewActionResponse>> Start(
            int id,
            CancellationToken cancellationToken) =>
            Move(id, ProposalWorkflow.UnderReview, "Review started", cancellationToken);

        [HttpPost("{id:int}/approve")]
        public Task<ActionResult<ReviewActionResponse>> Approve(
            int id,
            CancellationToken cancellationToken) =>
            Move(id, ProposalWorkflow.Approved, "Approved", cancellationToken);

        [HttpPost("{id:int}/decline")]
        public Task<ActionResult<ReviewActionResponse>> Decline(
            int id,
            CancellationToken cancellationToken) =>
            Move(id, ProposalWorkflow.Rejected, "Declined", cancellationToken);

        [HttpPost("{id:int}/request-changes")]
        public Task<ActionResult<ReviewActionResponse>> RequestChanges(
            int id,
            CancellationToken cancellationToken) =>
            Move(id, ProposalWorkflow.Draft, "Changes requested", cancellationToken);

        private async Task<ActionResult<ReviewActionResponse>> Move(
            int id,
            string to,
            string action,
            CancellationToken cancellationToken)
        {
            var currentUser = await _access.GetCurrentUserAsync(
                User,
                cancellationToken);

            if (!await _access.CanReviewProposalAsync(currentUser, cancellationToken))
                return Forbid();

            int? currentUserId = currentUser!.Id == 0 ? null : currentUser.Id;

            var proposal = await _db.Proposals
                .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

            if (proposal is null)
                return NotFound();

            if (!ProposalWorkflow.TryMove(proposal, to, out string error))
                return Conflict(error);

            proposal.LastChangerId = currentUserId;
            proposal.LastAction = action;

            await _db.SaveChangesAsync(cancellationToken);

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

    //----------Constructor----------//
}
