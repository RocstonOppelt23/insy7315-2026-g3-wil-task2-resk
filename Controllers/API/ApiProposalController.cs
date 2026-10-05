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

namespace RESK.WIL.Controllers.API
{
    /*
     * Endpoints:
     * GET  /api/proposals
     * GET  /api/proposals/{id:int}
     * POST /api/proposals/{id:int}/submit
     */
    [ApiController]
    [Route("api/proposals")]
    [Authorize(Policy = "ManageProposals")]
    public class ApiProposalController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IWebHostEnvironment _environment;

        public ApiProposalController(
            ApplicationDbContext db,
            UserManager<IdentityUser> userManager,
            IWebHostEnvironment environment)
        {
            _db = db;
            _userManager = userManager;
            _environment = environment;
        }

        // GET /api/proposals
        [HttpGet]
        public async Task<ActionResult<List<ApiProposalSummary>>> GetMine(CancellationToken cancellationToken)
        {
            string? userId = _userManager.GetUserId(User);
            if (userId == null)
                return Unauthorized();

            var proposals = await _db.ProducerProposals
                .AsNoTracking()
                .Where(p => p.OwnerUserId == userId)
                .OrderByDescending(p => p.UpdatedAtUtc)
                .ToListAsync(cancellationToken);

            var reviews = ProposalReviewStore.LoadAll(_environment.ContentRootPath);

            var result = proposals
                .Select(p => ApiProposalMapper.ToSummary(p, reviews.TryGetValue(p.Id, out var r) ? r : null))
                .ToList();

            return Ok(result);
        }

        // GET /api/proposals/{id:int}
        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiProposalDetails>> GetById(int id, CancellationToken cancellationToken)
        {
            string? userId = _userManager.GetUserId(User);
            if (userId == null)
                return Unauthorized();

            var proposal = await _db.ProducerProposals
                .AsNoTracking()
                .SingleOrDefaultAsync(p => p.Id == id && p.OwnerUserId == userId, cancellationToken);

            if (proposal == null)
                return NotFound();

            var review = ProposalReviewStore.Load(_environment.ContentRootPath, id);
            return Ok(ApiProposalMapper.ToDetails(proposal, review));
        }

        // POST /api/proposals/{id:int}/submit
        [HttpPost("{id:int}/submit")]
        public async Task<ActionResult<ApiProposalDetails>> Submit(int id)
        {
            string? userId = _userManager.GetUserId(User);
            if (userId == null)
                return Unauthorized();

            var proposal = await _db.ProducerProposals
                .SingleOrDefaultAsync(p => p.Id == id && p.OwnerUserId == userId);

            if (proposal == null)
                return NotFound();

            if (proposal.CompletedSteps < 4 || string.IsNullOrWhiteSpace(proposal.ProposalDocumentStoredName))
            {
                return BadRequest("Complete all proposal steps and upload the proposal document before submitting.");
            }

            if (!ProposalWorkflow.TryMove(proposal, ProposalStatuses.InReview, out string moveError))
            {
                return Conflict(moveError);
            }

            DateTime now = DateTime.UtcNow;
            proposal.CompletedSteps = 5;
            proposal.SubmittedAtUtc = now;
            proposal.Reference ??= $"CTV-{SouthAfricaTime.ToLocal(now).Year}-{proposal.Id:D4}";

            await _db.SaveChangesAsync();

            var review = ProposalReviewStore.Load(_environment.ContentRootPath, id);
            return Ok(ApiProposalMapper.ToDetails(proposal, review));
        }
    }

    // Mapping and response shapes used by the API controllers
    public record ApiProposalSummary(
        int Id,
        string? Reference,
        string ProgrammeTitle,
        string Category,
        string Status,
        string StatusLabel,
        int ProgressPercent,
        DateTime CreatedAtUtc,
        DateTime? SubmittedAtUtc,
        DateTime UpdatedAtUtc);

    public record ApiProposalDetails(
        int Id,
        string? Reference,
        string ProgrammeTitle,
        string Category,
        string Status,
        string StatusLabel,
        int ProgressPercent,
        DateTime CreatedAtUtc,
        DateTime? SubmittedAtUtc,
        DateTime UpdatedAtUtc,
        string ProgrammeFormat,
        string EpisodeDuration,
        string PrimaryLanguage,
        int CompletedSteps,
        string? ReviewerName,
        DateTime? ReviewDeadline,
        string? Decision,
        string? ReviewerComments,
        DateTime? DecidedAtUtc);

    public static class ApiProposalMapper
    {
        public static ApiProposalSummary ToSummary(ProducerProposal p, RESK.WIL.Services.ProposalReview? review)
        {
            return new ApiProposalSummary(
                p.Id,
                p.Reference,
                p.DisplayTitle,
                p.Category ?? string.Empty,
                p.Status,
                ProposalStatuses.Label(p.Status),
                p.ProgressPercent,
                p.CreatedAtUtc,
                p.SubmittedAtUtc,
                p.UpdatedAtUtc);
        }

        public static ApiProposalDetails ToDetails(ProducerProposal p, RESK.WIL.Services.ProposalReview? review)
        {
            return new ApiProposalDetails(
                p.Id,
                p.Reference,
                p.DisplayTitle,
                p.Category ?? string.Empty,
                p.Status,
                ProposalStatuses.Label(p.Status),
                p.ProgressPercent,
                p.CreatedAtUtc,
                p.SubmittedAtUtc,
                p.UpdatedAtUtc,
                p.ProgrammeFormat ?? string.Empty,
                p.EpisodeDuration ?? string.Empty,
                p.PrimaryLanguage ?? string.Empty,
                p.CompletedSteps,
                review?.ReviewerName,
                review?.Deadline,
                review?.Decision,
                review?.Comments,
                review?.DecidedAtUtc);
        }
    }
}
