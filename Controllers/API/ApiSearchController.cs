using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RESK.WIL.Data;
using RESK.WIL.Models;
using RESK.WIL.Services;

namespace RESK.WIL.Controllers.API
{
    [ApiController]
    [Route("api/search")]
    [Authorize(Policy = "ApiAccess")]
    public class ApiSearchController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly AccessControlService _access;

        public ApiSearchController(
            ApplicationDbContext db,
            AccessControlService access)
        {
            _db = db;
            _access = access;
        }

        // GET /api/search/proposals?query=music&searchType=Title
        [HttpGet("proposals")]
        public async Task<ActionResult<SearchResponse<ProposalSearch>>> SearchProposals(
            [FromQuery] RESK.WIL.Models.ProposalSearchRequest request,
            CancellationToken cancellationToken)
        {
            var currentUser = await _access.GetCurrentUserAsync(
                User,
                cancellationToken);

            if (currentUser is null)
                return Unauthorized();

            if (!await _access.CanUseProposalsAsync(currentUser, cancellationToken))
                return Forbid();

            int page = Math.Max(request.Page, 1);
            int pageSize = Math.Clamp(request.PageSize, 1, 100);

            IQueryable<Proposal> query = _db.Proposals
                .AsNoTracking()
                .Include(p => p.Producer);

            // Server-side security order:
            // system feature + role permission first, role scope second,
            // user filters/sorting last. Do not move this logic to the front end.
            // When ApiTesting:BypassServiceLogic is true, this returns the
            // original query so API tools can test filters without login.
            query = _access.ApplyProposalScope(query, currentUser);
            query = ApplyProposalFilters(query, request);

            int totalCount = await query.CountAsync(cancellationToken);

            var items = await ApplyProposalSorting(query, request)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProposalSearch
                {
                    Id = p.Id,
                    Title = p.ProgrammeTitle,
                    Status = p.ProposalStatus,
                    Type = p.ProposalType,
                    CreatedAtUtc = p.CreatedAtUtc,
                    UpdatedAtUtc = p.UpdatedAtUtc,
                    SubmittedAtUtc = p.SubmittedAtUtc,
                    ProducerId = p.ProducerId,
                    ProducerName = (p.Producer.Name + " " + p.Producer.LastName).Trim()
                })
                .ToListAsync(cancellationToken);

            return Ok(new SearchResponse<ProposalSearch>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            });
        }

        // GET /api/search/users?query=reviewer&searchType=Role
        [HttpGet("users")]
        public async Task<ActionResult<SearchResponse<UserSearch>>> SearchUsers(
            [FromQuery] UserSearchRequest request,
            CancellationToken cancellationToken)
        {
            var currentUser = await _access.GetCurrentUserAsync(
                User,
                cancellationToken);

            if (currentUser is null)
                return Unauthorized();

            if (!await _access.CanUseUsersAsync(currentUser, cancellationToken))
                return Forbid();

            int page = Math.Max(request.Page, 1);
            int pageSize = Math.Clamp(request.PageSize, 1, 100);

            IQueryable<User> query = _db.Users
                .AsNoTracking()
                .Include(u => u.Role)
                .Include(u => u.Proposals);

            // User search deliberately returns only public/admin-safe summary
            // fields. Personal details such as phone, address, password hash,
            // MFA data and ID number are not projected into UserSearch.
            // When ApiTesting:BypassServiceLogic is true, this returns the
            // original query so API tools can test filters without login.
            query = _access.ApplyUserScope(query, currentUser);
            query = ApplyUserFilters(query, request);

            int totalCount = await query.CountAsync(cancellationToken);

            var items = await ApplyUserSorting(query, request)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new UserSearch
                {
                    Id = u.Id,
                    DisplayName = (u.Name + " " + u.LastName).Trim(),
                    AccountStatus = u.AccountStatus,
                    RoleId = u.RoleId,
                    RoleTitle = u.Role != null ? u.Role.Title : null,
                    ProposalCount = u.Proposals.Count,
                    CreatedAtUtc = u.CreatedAtUtc
                })
                .ToListAsync(cancellationToken);

            return Ok(new SearchResponse<UserSearch>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            });
        }

        private static IQueryable<Proposal> ApplyProposalFilters(
            IQueryable<Proposal> query,
            RESK.WIL.Models.ProposalSearchRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.ProposalType))
            {
                string type = request.ProposalType.Trim().ToUpperInvariant();
                query = query.Where(p => p.ProposalType == type);
            }

            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                string status = request.Status.Trim();
                query = query.Where(p => p.ProposalStatus == status);
            }

            if (request.FromDateUtc.HasValue)
                query = query.Where(p => p.CreatedAtUtc >= request.FromDateUtc.Value);

            if (request.ToDateUtc.HasValue)
                query = query.Where(p => p.CreatedAtUtc <= request.ToDateUtc.Value);

            if (string.IsNullOrWhiteSpace(request.Query))
                return query;

            string value = request.Query.Trim();

            return request.SearchType?.Trim().ToLowerInvariant() switch
            {
                "producer" => query.Where(p =>
                    p.Producer.Name.Contains(value) ||
                    p.Producer.LastName.Contains(value)),
                "type" => query.Where(p => p.ProposalType.Contains(value)),
                "status" => query.Where(p => p.ProposalStatus.Contains(value)),
                "date" when DateTime.TryParse(value, out DateTime date) =>
                    query.Where(p => p.CreatedAtUtc.Date == date.Date ||
                                     (p.SubmittedAtUtc.HasValue &&
                                      p.SubmittedAtUtc.Value.Date == date.Date) ||
                                     p.UpdatedAtUtc.Date == date.Date),
                _ => query.Where(p => p.ProgrammeTitle.Contains(value))
            };
        }

        private static IQueryable<User> ApplyUserFilters(
            IQueryable<User> query,
            UserSearchRequest request)
        {
            if (request.RoleId.HasValue)
                query = query.Where(u => u.RoleId == request.RoleId.Value);

            if (request.AccountStatus.HasValue)
                query = query.Where(u => u.AccountStatus == request.AccountStatus.Value);

            if (string.IsNullOrWhiteSpace(request.Query))
                return query;

            string value = request.Query.Trim();

            return request.SearchType?.Trim().ToLowerInvariant() switch
            {
                "role" => query.Where(u =>
                    u.Role != null && u.Role.Title.Contains(value)),
                "status" => query.Where(u =>
                    u.AccountStatus.ToString().Contains(value)),
                _ => query.Where(u =>
                    u.Name.Contains(value) ||
                    u.LastName.Contains(value))
            };
        }

        private static IQueryable<Proposal> ApplyProposalSorting(
            IQueryable<Proposal> query,
            RESK.WIL.Models.ProposalSearchRequest request)
        {
            bool desc = IsDescending(request.SortDirection);

            return request.SortBy?.Trim().ToLowerInvariant() switch
            {
                "title" => desc
                    ? query.OrderByDescending(p => p.ProgrammeTitle)
                    : query.OrderBy(p => p.ProgrammeTitle),
                "type" => desc
                    ? query.OrderByDescending(p => p.ProposalType)
                    : query.OrderBy(p => p.ProposalType),
                "status" => desc
                    ? query.OrderByDescending(p => p.ProposalStatus)
                    : query.OrderBy(p => p.ProposalStatus),
                "createdat" => desc
                    ? query.OrderByDescending(p => p.CreatedAtUtc)
                    : query.OrderBy(p => p.CreatedAtUtc),
                "submittedat" => desc
                    ? query.OrderByDescending(p => p.SubmittedAtUtc)
                    : query.OrderBy(p => p.SubmittedAtUtc),
                _ => desc
                    ? query.OrderByDescending(p => p.UpdatedAtUtc)
                    : query.OrderBy(p => p.UpdatedAtUtc)
            };
        }

        private static IQueryable<User> ApplyUserSorting(
            IQueryable<User> query,
            UserSearchRequest request)
        {
            bool desc = IsDescending(request.SortDirection);

            return request.SortBy?.Trim().ToLowerInvariant() switch
            {
                "role" => desc
                    ? query.OrderByDescending(u => u.Role != null ? u.Role.Title : "")
                    : query.OrderBy(u => u.Role != null ? u.Role.Title : ""),
                "status" => desc
                    ? query.OrderByDescending(u => u.AccountStatus)
                    : query.OrderBy(u => u.AccountStatus),
                "proposalcount" => desc
                    ? query.OrderByDescending(u => u.Proposals.Count)
                    : query.OrderBy(u => u.Proposals.Count),
                "createdat" => desc
                    ? query.OrderByDescending(u => u.CreatedAtUtc)
                    : query.OrderBy(u => u.CreatedAtUtc),
                _ => desc
                    ? query.OrderByDescending(u => u.Name)
                    : query.OrderBy(u => u.Name)
            };
        }
        // sorting research results by descending order if the sortDirection is "desc" or "z-a"
        private static bool IsDescending(string? sortDirection) =>
            sortDirection?.Trim().Equals(
                "desc",
                StringComparison.OrdinalIgnoreCase) == true ||
            sortDirection?.Trim().Equals(
                "z-a",
                StringComparison.OrdinalIgnoreCase) == true;
    }

    //----------Constructor----------//
}
