using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RESK.WIL.Data;
using RESK.WIL.Models;

namespace RESK.WIL.Controllers
{
    [Authorize]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // ADMIN DASHBOARD
        // /Admin
        // /Admin/Index
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // =====================================================
            // USER STATISTICS
            // =====================================================

            ViewBag.TotalUsers =
                await _context.Users.CountAsync();

            ViewBag.PendingUsers =
                await _context.Users.CountAsync(u =>
                    u.AccountStatus == UserAccountStatus.Pending);

            ViewBag.ActiveUsers =
                await _context.Users.CountAsync(u =>
                    u.AccountStatus == UserAccountStatus.Active);

            ViewBag.SuspendedUsers =
                await _context.Users.CountAsync(u =>
                    u.AccountStatus == UserAccountStatus.Disabled);


            // =====================================================
            // PROPOSAL STATISTICS
            // ProposalStatus:
            // Draft, Pending, Approved, Rejected
            // =====================================================

            ViewBag.TotalProposals =
                await _context.Proposals.CountAsync();

            ViewBag.PendingProposals =
                await _context.Proposals.CountAsync(p =>
                    p.ProposalStatus == "Pending");

            ViewBag.ApprovedProposals =
                await _context.Proposals.CountAsync(p =>
                    p.ProposalStatus == "Approved");

            ViewBag.RejectedProposals =
                await _context.Proposals.CountAsync(p =>
                    p.ProposalStatus == "Rejected");

            ViewBag.DraftProposals =
                await _context.Proposals.CountAsync(p =>
                    p.ProposalStatus == "Draft");


            // =====================================================
            // RECENT PROPOSALS
            // =====================================================

            ViewBag.RecentProposals =
                await _context.Proposals
                    .AsNoTracking()
                    .Include(p => p.Producer)
                    .OrderByDescending(p => p.UpdatedAtUtc)
                    .Take(5)
                    .ToListAsync();


            // =====================================================
            // RECENT USERS
            // =====================================================

            ViewBag.RecentUsers =
                await _context.Users
                    .AsNoTracking()
                    .Include(u => u.UserRoles)
                        .ThenInclude(ur => ur.Role)
                    .OrderByDescending(u => u.CreatedAtUtc)
                    .Take(5)
                    .ToListAsync();


            // =====================================================
            // PENDING USER APPROVALS
            // =====================================================

            ViewBag.PendingRegistrations =
                await _context.Users
                    .AsNoTracking()
                    .Include(u => u.UserRoles)
                        .ThenInclude(ur => ur.Role)
                    .Where(u =>
                        u.AccountStatus == UserAccountStatus.Pending)
                    .OrderBy(u => u.CreatedAtUtc)
                    .Take(5)
                    .ToListAsync();


            // =====================================================
            // RECENT APPROVED PROPOSALS
            // =====================================================

            ViewBag.RecentApprovedProposals =
                await _context.Proposals
                    .AsNoTracking()
                    .Include(p => p.Producer)
                    .Where(p =>
                        p.ProposalStatus == "Approved")
                    .OrderByDescending(p => p.UpdatedAtUtc)
                    .Take(5)
                    .ToListAsync();


            // =====================================================
            // RECENT REJECTED PROPOSALS
            // =====================================================

            ViewBag.RecentRejectedProposals =
                await _context.Proposals
                    .AsNoTracking()
                    .Include(p => p.Producer)
                    .Where(p =>
                        p.ProposalStatus == "Rejected")
                    .OrderByDescending(p => p.UpdatedAtUtc)
                    .Take(5)
                    .ToListAsync();

            return View();
        }
    }
}