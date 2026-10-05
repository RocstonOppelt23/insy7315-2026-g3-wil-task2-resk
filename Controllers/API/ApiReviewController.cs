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
    [ApiController]
    [Route("api/review")]
    [Authorize(Roles = "Reviewer,Admin")]
    public class ApiReviewController : ControllerBase
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

        // GET /api/review/queue
        [HttpGet("queue")]
        public async Task<ActionResult<List<object>>> GetQueue(CancellationToken ct)
        {
            var reviews = ProposalReviewStore.LoadAll(_environment.ContentRootPath);

            var items = await _db.ProducerProposals
                .AsNoTracking()
                .Where(p => p.Status == ProposalStatuses.InReview)
                .OrderBy(p => p.SubmittedAtUtc)
                .ToListAsync(ct);

            var result = items.Select(p => new
            {
                p.Id,
                p.Reference,
                ProgrammeTitle = p.DisplayTitle,
                p.Category,
                p.Status,
                StatusLabel = ProposalStatuses.Label(p.Status),
                p.SubmittedAtUtc,
                p.UpdatedAtUtc,
                ReviewerName = reviews.TryGetValue(p.Id, out var r) ? r.ReviewerName : null,
                ReviewDeadline = reviews.TryGetValue(p.Id, out var r2) ? r2.Deadline : null
            }).ToList();

            return Ok(result);
        }

        // GET /api/review/{id:int}
        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiProposalDetails>> GetById(int id)
        {
            var proposal = await _db.ProducerProposals
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id && p.Status != ProposalStatuses.Draft);

            if (proposal == null)
                return NotFound();

            var review = ProposalReviewStore.Load(_environment.ContentRootPath, id);
            return Ok(ApiProposalMapper.ToDetails(proposal, review));
        }

        public class ApiReviewDecisionRequest
        {
            public string? comments { get; set; }
        }

        // POST /api/review/{id:int}/approve
        [HttpPost("{id:int}/approve")]
        public async Task<ActionResult<object>> Approve(
            int id,
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ApiReviewDecisionRequest? request)
        {
            return await HandleDecision(id, ProposalReviewStore.Approve, ProposalStatuses.Approved, request);
        }

        // POST /api/review/{id:int}/reject
        [HttpPost("{id:int}/reject")]
        public async Task<ActionResult<object>> Reject(
            int id,
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ApiReviewDecisionRequest? request)
        {
            return await HandleDecision(id, ProposalReviewStore.Reject, ProposalStatuses.Rejected, request);
        }

        // POST /api/review/{id:int}/request-changes
        [HttpPost("{id:int}/request-changes")]
        public async Task<ActionResult<object>> RequestChanges(
            int id,
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ApiReviewDecisionRequest? request)
        {
            return await HandleDecision(id, ProposalReviewStore.RequestChanges, ProposalStatuses.ChangesRequested, request);
        }

        private async Task<ActionResult<object>> HandleDecision(int id, string reviewDecisionConstant, string targetStatus, ApiReviewDecisionRequest? request)
        {
            ProducerProposal? proposal = await _db.ProducerProposals.SingleOrDefaultAsync(p => p.Id == id);
            if (proposal == null)
                return NotFound();

            if (!ProposalWorkflow.TryMove(proposal, targetStatus, out string moveError))
                return Conflict(moveError);

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

            return Ok(new
            {
                Id = proposal.Id,
                Reference = proposal.Reference,
                Status = proposal.Status,
                StatusLabel = ProposalStatuses.Label(proposal.Status),
                Decision = review.Decision,
                UpdatedAtUtc = proposal.UpdatedAtUtc
            });
        }
    }
}
