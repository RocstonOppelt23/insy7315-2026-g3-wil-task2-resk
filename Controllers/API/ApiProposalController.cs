using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RESK.WIL.Data;
using RESK.WIL.Models;

namespace RESK.WIL.Controllers.API
{
    [ApiController]
    [Route("api/proposals")]
    [Authorize(Policy = "ManageProposals")]
    public class ApiProposalController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public ApiProposalController(ApplicationDbContext db)
        {
            _db = db;
        }

        // GET /api/proposals
        [HttpGet]
        public async Task<ActionResult<List<ProposalResponse>>> GetMine(
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out int userId))
                return Unauthorized();

            var proposals = await _db.Proposals
                .AsNoTracking()
                .Where(p => p.ProducerId == userId)
                .OrderByDescending(p => p.UpdatedAtUtc)
                .Select(p => new ProposalResponse(
                    p.Id,
                    p.ProducerId,
                    p.ProposalType,
                    p.ProgrammeTitle,
                    p.ProposalStatus,
                    p.CreatedAtUtc,
                    p.SubmittedAtUtc,
                    p.UpdatedAtUtc))
                .ToListAsync(cancellationToken);

            return Ok(proposals);
        }

        // GET /api/proposals/7
        [HttpGet("{id:int}")]
        public async Task<ActionResult<ProposalDetailsResponse>> GetById(
            int id,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out int userId))
                return Unauthorized();

            var proposal = await _db.Proposals
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    p => p.Id == id && p.ProducerId == userId,
                    cancellationToken);

            if (proposal is null)
                return NotFound();

            return Ok(ToDetailsResponse(proposal));
        }

        // POST /api/proposals
        // An incomplete proposal can be saved as a draft.
        [HttpPost]
        public async Task<ActionResult<ProposalDetailsResponse>> CreateDraft(
            [FromBody] SaveProposalRequest request,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out int userId))
                return Unauthorized();

            var now = DateTime.UtcNow;

            var proposal = new Proposal
            {
                ProducerId = userId,
                ProposalStatus = "Draft",
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                LastChangerId = userId,
                LastAction = "Created draft"
            };

            ApplyFields(proposal, request);

            _db.Proposals.Add(proposal);
            await _db.SaveChangesAsync(cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { id = proposal.Id },
                ToDetailsResponse(proposal));
        }

        // PUT /api/proposals/7
        [HttpPut("{id:int}")]
        public async Task<ActionResult<ProposalDetailsResponse>> UpdateDraft(
            int id,
            [FromBody] SaveProposalRequest request,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out int userId))
                return Unauthorized();

            var proposal = await _db.Proposals.SingleOrDefaultAsync(
                p => p.Id == id && p.ProducerId == userId,
                cancellationToken);

            if (proposal is null)
                return NotFound();

            if (proposal.ProposalStatus != "Draft")
                return Conflict("Only drafts can be edited through this endpoint.");

            ApplyFields(proposal, request);
            proposal.LastChangerId = userId;
            proposal.LastAction = "Updated draft";
            proposal.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);
            return Ok(ToDetailsResponse(proposal));
        }

        // POST /api/proposals/7/submit
        [HttpPost("{id:int}/submit")]
        public async Task<ActionResult<ProposalDetailsResponse>> Submit(
            int id,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out int userId))
                return Unauthorized();

            var proposal = await _db.Proposals.SingleOrDefaultAsync(
                p => p.Id == id && p.ProducerId == userId,
                cancellationToken);

            if (proposal is null)
                return NotFound();

            if (proposal.ProposalStatus != "Draft")
                return Conflict("Only drafts can be submitted.");

            var errors = new List<ValidationResult>();
            var context = new ValidationContext(proposal);

            bool valid = Validator.TryValidateObject(
                proposal,
                context,
                errors,
                validateAllProperties: true);

            if (!valid)
            {
                foreach (var error in errors)
                {
                    foreach (var field in error.MemberNames.DefaultIfEmpty(""))
                        ModelState.AddModelError(
                            field,
                            error.ErrorMessage ?? "Invalid value.");
                }

                return ValidationProblem(ModelState);
            }

            var now = DateTime.UtcNow;
            proposal.ProposalStatus = "Pending";
            proposal.SubmittedAtUtc ??= now;
            proposal.UpdatedAtUtc = now;
            proposal.LastChangerId = userId;
            proposal.LastAction = "Submitted";

            await _db.SaveChangesAsync(cancellationToken);
            return Ok(ToDetailsResponse(proposal));
        }

        private bool TryGetUserId(out int userId) =>
            int.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out userId);

        private static void ApplyFields(
            Proposal proposal,
            SaveProposalRequest request)
        {
            proposal.ProposalType = request.ProposalType.Trim().ToUpperInvariant();
            proposal.PhysicalAddress = request.PhysicalAddress;
            proposal.ProgrammeTitle = request.ProgrammeTitle;
            proposal.Partner = request.Partner;
            proposal.TdlaFileUrl = request.TdlaFileUrl;
            proposal.Publisher = request.Publisher;
            proposal.Duration = request.Duration;
            proposal.Episodes = request.Episodes;
            proposal.Introduction = request.Introduction;
            proposal.Background = request.Background;
            proposal.Motivation = request.Motivation;
            proposal.Synopsis = request.Synopsis;
            proposal.Treatment = request.Treatment;
            proposal.Language = request.Language;
            proposal.FinancePlan = request.FinancePlan;
            proposal.ResourceSkills = request.ResourceSkills;
            proposal.Content = request.Content;
            proposal.TargetAudience = request.TargetAudience;
            proposal.MediaHandles = request.MediaHandles;
            proposal.Genre = request.Genre;
            proposal.Copyright = request.Copyright;
            proposal.WebsiteUrl = request.WebsiteUrl;
            proposal.CTTVSupport = request.CTTVSupport;
            proposal.Sponsors = request.Sponsors;
            proposal.LicenceDuration = request.LicenceDuration;
            proposal.IsTdlaRead = request.IsTdlaRead;
        }

        private static ProposalDetailsResponse ToDetailsResponse(
            Proposal proposal) =>
            new(
                proposal.Id,
                proposal.ProducerId,
                proposal.ProposalStatus,
                proposal.CreatedAtUtc,
                proposal.SubmittedAtUtc,
                proposal.UpdatedAtUtc,
                new SaveProposalRequest
                {
                    ProposalType = proposal.ProposalType,
                    PhysicalAddress = proposal.PhysicalAddress,
                    ProgrammeTitle = proposal.ProgrammeTitle,
                    Partner = proposal.Partner,
                    TdlaFileUrl = proposal.TdlaFileUrl,
                    Publisher = proposal.Publisher,
                    Duration = proposal.Duration,
                    Episodes = proposal.Episodes,
                    Introduction = proposal.Introduction,
                    Background = proposal.Background,
                    Motivation = proposal.Motivation,
                    Synopsis = proposal.Synopsis,
                    Treatment = proposal.Treatment,
                    Language = proposal.Language,
                    FinancePlan = proposal.FinancePlan,
                    ResourceSkills = proposal.ResourceSkills,
                    Content = proposal.Content,
                    TargetAudience = proposal.TargetAudience,
                    MediaHandles = proposal.MediaHandles,
                    Genre = proposal.Genre,
                    Copyright = proposal.Copyright,
                    WebsiteUrl = proposal.WebsiteUrl,
                    CTTVSupport = proposal.CTTVSupport,
                    Sponsors = proposal.Sponsors,
                    LicenceDuration = proposal.LicenceDuration,
                    IsTdlaRead = proposal.IsTdlaRead
                });
    }

    public record ProposalResponse(
        int Id,
        int ProducerId,
        string ProposalType,
        string ProgrammeTitle,
        string ProposalStatus,
        DateTime CreatedAtUtc,
        DateTime? SubmittedAtUtc,
        DateTime UpdatedAtUtc);

    public record ProposalDetailsResponse(
        int Id,
        int ProducerId,
        string ProposalStatus,
        DateTime CreatedAtUtc,
        DateTime? SubmittedAtUtc,
        DateTime UpdatedAtUtc,
        SaveProposalRequest Fields);

    public class SaveProposalRequest
    {
        public string ProposalType { get; set; } = string.Empty;
        public string PhysicalAddress { get; set; } = string.Empty;
        public string ProgrammeTitle { get; set; } = string.Empty;
        public string? Partner { get; set; }
        public string? TdlaFileUrl { get; set; }
        public string? Publisher { get; set; }
        public int Duration { get; set; }
        public int? Episodes { get; set; }
        public string Introduction { get; set; } = string.Empty;
        public string Background { get; set; } = string.Empty;
        public string Motivation { get; set; } = string.Empty;
        public string Synopsis { get; set; } = string.Empty;
        public string Treatment { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public string FinancePlan { get; set; } = string.Empty;
        public string ResourceSkills { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string TargetAudience { get; set; } = string.Empty;
        public string? MediaHandles { get; set; }
        public string? Genre { get; set; }
        public string Copyright { get; set; } = string.Empty;
        public string? WebsiteUrl { get; set; }
        public string? CTTVSupport { get; set; }
        public string? Sponsors { get; set; }
        public string LicenceDuration { get; set; } = string.Empty;
        public bool IsTdlaRead { get; set; }
    }
}
