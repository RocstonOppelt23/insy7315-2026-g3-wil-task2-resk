using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RESK.WIL.Data;
using RESK.WIL.Models;
using RESK.WIL.Services;

namespace RESK.WIL.Controllers.API
{
    [ApiController]
    [Route("api/dashboard")]
    [Authorize(Policy = "ApiAccess")]
    public class ApiDashboardController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly AccessControlService _access;

        public ApiDashboardController(
            ApplicationDbContext db,
            AccessControlService access)
        {
            _db = db;
            _access = access;
        }

        // GET /api/dashboard/summary
        [HttpGet("summary")]
        public async Task<ActionResult<DashboardSummaryResponse>> GetSummary(
            CancellationToken cancellationToken)
        {
            var currentUser = await _access.GetCurrentUserAsync(
                User,
                cancellationToken);

            if (currentUser is null)
                return Unauthorized();

            if (!await _access.CanUseProposalsAsync(currentUser, cancellationToken))
                return Forbid();

            IQueryable<Proposal> proposalQuery = _db.Proposals.AsNoTracking();

            // Dashboard proposal counts follow the same access model as search.
            // When ApiTesting:BypassServiceLogic is true, the service returns
            // the original query for local Swagger/Postman testing.
            proposalQuery = _access.ApplyProposalScope(proposalQuery, currentUser);

            var proposalSummary = new ProposalDashboardSummary
            {
                Total = await proposalQuery.CountAsync(cancellationToken),
                Draft = await proposalQuery.CountAsync(
                    p => p.ProposalStatus == ProposalWorkflow.Draft,
                    cancellationToken),
                Pending = await proposalQuery.CountAsync(
                    p => p.ProposalStatus == ProposalWorkflow.Pending,
                    cancellationToken),
                UnderReview = await proposalQuery.CountAsync(
                    p => p.ProposalStatus == ProposalWorkflow.UnderReview,
                    cancellationToken),
                Approved = await proposalQuery.CountAsync(
                    p => p.ProposalStatus == ProposalWorkflow.Approved,
                    cancellationToken),
                Rejected = await proposalQuery.CountAsync(
                    p => p.ProposalStatus == ProposalWorkflow.Rejected,
                    cancellationToken),
                MyProposals = currentUser.Id == 0
                    ? 0
                    : await _db.Proposals
                        .AsNoTracking()
                        .CountAsync(
                            p => p.ProducerId == currentUser.Id,
                            cancellationToken)
            };

            var response = new DashboardSummaryResponse
            {
                Proposals = proposalSummary
            };

            if (await _access.CanUseUsersAsync(currentUser, cancellationToken))
            {
                // User dashboard numbers are also scoped, so a restricted role
                // cannot learn total user counts from the dashboard endpoint.
                IQueryable<User> userQuery = _access.ApplyUserScope(
                    _db.Users.AsNoTracking(),
                    currentUser);

                response.Users = new UserDashboardSummary
                {
                    Total = await userQuery.CountAsync(cancellationToken),
                    Pending = await userQuery.CountAsync(
                        u => u.AccountStatus == UserAccountStatus.Pending,
                        cancellationToken),
                    Active = await userQuery.CountAsync(
                        u => u.AccountStatus == UserAccountStatus.Active,
                        cancellationToken),
                    Inactive = await userQuery.CountAsync(
                        u => u.AccountStatus == UserAccountStatus.Inactive,
                        cancellationToken),
                    Disabled = await userQuery.CountAsync(
                        u => u.AccountStatus == UserAccountStatus.Disabled,
                        cancellationToken)
                };
            }

            return Ok(response);
        }
    }
    //----------Constructor----------//
}
