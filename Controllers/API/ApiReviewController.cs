using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RESK.WIL.Data;
using RESK.WIL.Models;
using RESK.WIL.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;

namespace RESK.WIL.Controllers.API
{
    /*
     * Endpoints:
     * GET  /api/review/queue
     * GET  /api/review/{id:int}
     * POST /api/review/{id:int}/approve
     * POST /api/review/{id:int}/reject
     * POST /api/review/{id:int}/request-changes
     */
    /// <summary>Reviewer queue and review decisions.</summary>
    [Route("api/review")]
    [Authorize(Roles = "Reviewer,Admin")]
    public class ApiReviewController : ApiControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IWebHostEnvironment _environment;

        public ApiReviewController(
            ApplicationDbContext db,
            UserManager<IdentityUser> userManager,
            IWebHostEnvironment environment)
        {
            _db = db;
            _userManager = userManager;
            _environment = environment;
        }

        /// <summary>Lists proposals waiting for review, oldest submission first.</summary>
        /// <response code="200">Proposals that are In review.</response>
        [HttpGet("queue")]
        [ProducesResponseType(typeof(List<ApiReviewQueueItem>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<ApiReviewQueueItem>>> GetQueue(CancellationToken ct)
        {
            var reviews = ProposalReviewStore.LoadAll(_environment.ContentRootPath);

            var items = await _db.ProducerProposals
                .AsNoTracking()
                .Where(p => p.Status == ProposalStatuses.InReview)
                .OrderBy(p => p.SubmittedAtUtc)
                .ToListAsync(ct);

            var result = items.Select(p =>
            {
                reviews.TryGetValue(p.Id, out var review);

                return new ApiReviewQueueItem(
                    p.Id,
                    p.Reference,
                    p.DisplayTitle,
                    p.Category,
                    p.Status,
                    ProposalStatuses.Label(p.Status),
                    p.SubmittedAtUtc,
                    p.UpdatedAtUtc,
                    review?.ReviewerName,
                    review?.Deadline);
            }).ToList();

            return Ok(result);
        }

        /// <summary>Gets a submitted proposal with its review details.</summary>
        /// <param name="id">The proposal id.</param>
        /// <response code="200">The proposal.</response>
        /// <response code="404">No submitted proposal has that id.</response>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(ApiProposalDetails), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiProposalDetails>> GetById(int id)
        {
            var proposal = await _db.ProducerProposals
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id && p.Status != ProposalStatuses.Draft);

            if (proposal == null)
                return NotFoundProblem($"Submitted proposal {id} was not found.");

            var review = ProposalReviewStore.Load(_environment.ContentRootPath, id);
            return Ok(ApiProposalMapper.ToDetails(proposal, review));
        }

        /// <summary>Optional reviewer comments sent with a decision.</summary>
        public class ApiReviewDecisionRequest
        {
            /// <summary>Feedback for the producer.</summary>
            [StringLength(4000)]
            public string? comments { get; set; }
        }

        /// <summary>Approves a proposal that is In review.</summary>
        /// <param name="id">The proposal id.</param>
        /// <param name="request">Optional comments for the producer.</param>
        [HttpPost("{id:int}/approve")]
        [ProducesResponseType(typeof(ApiReviewDecisionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<ApiReviewDecisionResponse>> Approve(
            int id,
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ApiReviewDecisionRequest? request)
        {
            return await HandleDecision(id, ProposalReviewStore.Approve, ProposalStatuses.Approved, request);
        }

        /// <summary>Rejects a proposal that is In review.</summary>
        /// <param name="id">The proposal id.</param>
        /// <param name="request">Optional comments for the producer.</param>
        [HttpPost("{id:int}/reject")]
        [ProducesResponseType(typeof(ApiReviewDecisionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<ApiReviewDecisionResponse>> Reject(
            int id,
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ApiReviewDecisionRequest? request)
        {
            return await HandleDecision(id, ProposalReviewStore.Reject, ProposalStatuses.Rejected, request);
        }

        /// <summary>Sends a proposal back to the producer for changes.</summary>
        /// <param name="id">The proposal id.</param>
        /// <param name="request">Optional comments for the producer.</param>
        [HttpPost("{id:int}/request-changes")]
        [ProducesResponseType(typeof(ApiReviewDecisionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<ApiReviewDecisionResponse>> RequestChanges(
            int id,
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ApiReviewDecisionRequest? request)
        {
            return await HandleDecision(id, ProposalReviewStore.RequestChanges, ProposalStatuses.ChangesRequested, request);
        }

        private async Task<ActionResult<ApiReviewDecisionResponse>> HandleDecision(int id, string reviewDecisionConstant, string targetStatus, ApiReviewDecisionRequest? request)
        {
            ProducerProposal? proposal = await _db.ProducerProposals.SingleOrDefaultAsync(p => p.Id == id);
            if (proposal == null)
                return NotFoundProblem($"Proposal {id} was not found.");

            if (!ProposalWorkflow.TryMove(proposal, targetStatus, out string moveError))
                return ConflictProblem(moveError);

            await _db.SaveChangesAsync();

            DateTime now = DateTime.UtcNow;

            var review = ProposalReviewStore.Load(_environment.ContentRootPath, id);
            review.Recommendation = reviewDecisionConstant;
            review.Decision = reviewDecisionConstant;
            review.DecidedAtUtc = now;
            review.DecidedBy = User.Identity?.Name;
            review.ReviewSubmitted = false;
            review.ReviewSavedAtUtc = now;

            if (request != null && !string.IsNullOrWhiteSpace(request.comments))
            {
                review.Comments = request.comments;
            }

            if (!review.HasReviewer)
            {
                IdentityUser? me = await _userManager.GetUserAsync(User);
                review.ReviewerUserId = me?.Id;
                review.ReviewerName = me?.Email;
                review.AssignedAtUtc ??= now;
            }

            ProposalReviewStore.Save(_environment.ContentRootPath, review);

            return Ok(new ApiReviewDecisionResponse(
                proposal.Id,
                proposal.Reference,
                proposal.Status,
                ProposalStatuses.Label(proposal.Status),
                review.Decision,
                proposal.UpdatedAtUtc));
        }
    }

    /// <summary>One proposal in the reviewer queue.</summary>
    public record ApiReviewQueueItem(
        int Id,
        string? Reference,
        string ProgrammeTitle,
        string Category,
        string Status,
        string StatusLabel,
        DateTime? SubmittedAtUtc,
        DateTime UpdatedAtUtc,
        string? ReviewerName,
        DateTime? ReviewDeadline);

    /// <summary>Result of an approve / reject / request-changes decision.</summary>
    public record ApiReviewDecisionResponse(
        int Id,
        string? Reference,
        string Status,
        string StatusLabel,
        string? Decision,
        DateTime UpdatedAtUtc);
}
