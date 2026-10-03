using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RESK.WIL.Models;
using RESK.WIL.Services;

namespace RESK.WIL.Controllers
{
    /*
     * =========================================================
     * ADMIN DASHBOARD
     * =========================================================
     *
     *   GET /Admin/Dashboard
     *
     * Every number on the page is worked out from the database:
     *
     *   Total proposals      submitted proposals (drafts are private
     *                        to producers and are not counted)
     *   Pending review       proposals with status "In review"
     *   Active users         accounts with a role that are not locked out
     *   Average review time  days from submission to the reviewer's
     *                        decision (approved, changes or not approved)
     *   Workflow activity    submissions and approvals per month,
     *                        for the last six months
     *   Pending actions      proposals waiting for review, position-change
     *                        requests from Settings, and accounts that
     *                        have no role yet
     *   Recent proposals     the five latest submissions
     */
    [Authorize(Roles = "Admin")]
    [Route("Admin")]
    public class AdminDashboardController : Controller
    {
        private const string ViewPath = "~/Views/AdminDashboard/Index.cshtml";

        private readonly RESK.WIL.Data.ApplicationDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IWebHostEnvironment _environment;

        public AdminDashboardController(
            RESK.WIL.Data.ApplicationDbContext db,
            UserManager<IdentityUser> userManager,
            IWebHostEnvironment environment)
        {
            _db = db;
            _userManager = userManager;
            _environment = environment;
        }


        [HttpGet("", Order = -1)]
        [HttpGet("Index", Order = -1)]
        
        [HttpGet("Dashboard", Order = -1)]
        public async Task<IActionResult> Dashboard()
        {
            DateTime nowUtc = DateTime.UtcNow;
            DateTime localNow = SouthAfricaTime.ToLocal(nowUtc);
            DateTime monthStart = new DateTime(localNow.Year, localNow.Month, 1);


            // ---------------------------------------------
            // PROPOSALS (submitted only)
            // ---------------------------------------------

            List<ProducerProposal> proposals =
                await _db.ProducerProposals
                    .AsNoTracking()
                    .Where(p => p.Status != ProposalStatuses.Draft)
                    .ToListAsync();

            int submittedThisMonth =
                proposals.Count(p =>
                    p.SubmittedAtUtc.HasValue &&
                    SouthAfricaTime.ToLocal(p.SubmittedAtUtc.Value) >= monthStart);

            int pendingReview =
                proposals.Count(p => p.Status == ProposalStatuses.InReview);


            // ---------------------------------------------
            // USERS
            // ---------------------------------------------

            // Set<>() is used because this DbContext has its own
            // "Users" and "UserRoles" tables with the same names.
            List<IdentityUser> users =
                await _db.Set<IdentityUser>()
                    .AsNoTracking()
                    .ToListAsync();

            HashSet<string> usersWithRole =
                new HashSet<string>(
                    await _db.Set<IdentityUserRole<string>>()
                        .AsNoTracking()
                        .Select(r => r.UserId)
                        .ToListAsync());

            int activeUsers =
                users.Count(u =>
                    usersWithRole.Contains(u.Id) &&
                    !(u.LockoutEnd.HasValue && u.LockoutEnd.Value > DateTimeOffset.UtcNow));

            int usersWithoutRole =
                users.Count(u => !usersWithRole.Contains(u.Id));

            // Producers who saved or submitted anything in the last 7 days.
            DateTime weekAgo = nowUtc.AddDays(-7);

            int activeThisWeek =
                await _db.ProducerProposals
                    .AsNoTracking()
                    .Where(p => p.UpdatedAtUtc >= weekAgo)
                    .Select(p => p.OwnerUserId)
                    .Distinct()
                    .CountAsync();


            // ---------------------------------------------
            // AVERAGE REVIEW TIME (submitted -> decision)
            // ---------------------------------------------

            List<double> reviewDays =
                proposals
                    .Where(p =>
                        (p.Status == ProposalStatuses.Approved ||
                         p.Status == ProposalStatuses.Rejected ||
                         p.Status == ProposalStatuses.ChangesRequested) &&
                        p.SubmittedAtUtc.HasValue &&
                        p.UpdatedAtUtc > p.SubmittedAtUtc.Value)
                    .Select(p => (p.UpdatedAtUtc - p.SubmittedAtUtc!.Value).TotalDays)
                    .ToList();

            string averageReview =
                reviewDays.Count == 0
                    ? "—"
                    : reviewDays.Average().ToString("0.0", CultureInfo.InvariantCulture);


            // ---------------------------------------------
            // WORKFLOW ACTIVITY (last six months)
            // ---------------------------------------------

            var months = new List<string>();
            var submittedPerMonth = new List<int>();
            var approvedPerMonth = new List<int>();

            for (int i = 5; i >= 0; i--)
            {
                DateTime from = monthStart.AddMonths(-i);
                DateTime to = from.AddMonths(1);

                months.Add(from.ToString("MMM", CultureInfo.InvariantCulture));

                submittedPerMonth.Add(
                    proposals.Count(p =>
                        p.SubmittedAtUtc.HasValue &&
                        InRange(SouthAfricaTime.ToLocal(p.SubmittedAtUtc.Value), from, to)));

                approvedPerMonth.Add(
                    proposals.Count(p =>
                        p.Status == ProposalStatuses.Approved &&
                        InRange(SouthAfricaTime.ToLocal(p.UpdatedAtUtc), from, to)));
            }


            // ---------------------------------------------
            // RECENT PROPOSALS
            // ---------------------------------------------

            Dictionary<string, IdentityUser> userById =
                users.ToDictionary(u => u.Id);

            List<AdminRecentProposalRow> recent =
                proposals
                    .OrderByDescending(p => p.SubmittedAtUtc ?? p.UpdatedAtUtc)
                    .Take(5)
                    .Select(p =>
                    {
                        userById.TryGetValue(p.OwnerUserId, out IdentityUser? owner);

                        ProducerProfile profile =
                            ProducerProfileStore.Load(_environment.ContentRootPath, p.OwnerUserId);

                        return new AdminRecentProposalRow
                        {
                            Id = p.Id,
                            Title = p.DisplayTitle,
                            Producer = ProducerProfileStore.DisplayName(profile, owner),
                            DateText = SouthAfricaTime.ShortDate(p.SubmittedAtUtc ?? p.UpdatedAtUtc),
                            StatusLabel = StatusLabel(p.Status),
                            StatusCss = StatusCss(p.Status)
                        };
                    })
                    .ToList();


            // ---------------------------------------------
            // SIGNED-IN ADMIN
            // ---------------------------------------------

            IdentityUser? admin = await _userManager.GetUserAsync(User);

            string adminName = "Admin User";
            string adminInitials = "AD";

            if (admin != null)
            {
                ProducerProfile adminProfile =
                    ProducerProfileStore.Load(_environment.ContentRootPath, admin.Id);

                if (!string.IsNullOrWhiteSpace(adminProfile.FullName))
                {
                    adminName = adminProfile.FullName.Trim();
                    adminInitials = ProducerProfileStore.Initials(adminName);
                }
            }


            var model =
                new AdminDashboardViewModel
                {
                    AdminName = adminName,
                    AdminInitials = adminInitials,

                    TotalProposals = proposals.Count,
                    SubmittedThisMonth = submittedThisMonth,
                    PendingReview = pendingReview,
                    ActiveUsers = activeUsers,
                    ActiveThisWeek = activeThisWeek,
                    AverageReviewDays = averageReview,

                    Months = months,
                    SubmittedPerMonth = submittedPerMonth,
                    ApprovedPerMonth = approvedPerMonth,

                    AwaitingReview = proposals.Count(p => p.Status == ProposalStatuses.InReview && !ProposalReviewStore.Load(_environment.ContentRootPath, p.Id).HasReviewer),
                    RoleChangeRequests = CountRoleChangeRequests(),
                    UsersWithoutRole = usersWithoutRole,

                    RecentProposals = recent
                };

            return View(ViewPath, model);
        }


        // =========================================================
        // HELPERS
        // =========================================================

        private static bool InRange(DateTime value, DateTime from, DateTime to)
        {
            return value >= from && value < to;
        }


        private static string StatusLabel(string status)
        {
            return status switch
            {
                ProposalStatuses.InReview => "In review",
                ProposalStatuses.Approved => "Approved",
                ProposalStatuses.ChangesRequested => "Changes needed",
                ProposalStatuses.Rejected => "Not approved",
                _ => "Draft"
            };
        }


        private static string StatusCss(string status)
        {
            return status switch
            {
                ProposalStatuses.InReview => "pill-review",
                ProposalStatuses.Approved => "pill-approved",
                ProposalStatuses.ChangesRequested => "pill-changes",
                ProposalStatuses.Rejected => "pill-rejected",
                _ => "pill-draft"
            };
        }


        // Position-change requests made on the producer Settings page
        // (saved in App_Data/Profiles/*.json).
        private int CountRoleChangeRequests()
        {
            string folder =
                Path.Combine(_environment.ContentRootPath, "App_Data", "Profiles");

            if (!Directory.Exists(folder))
            {
                return 0;
            }

            int count = 0;

            foreach (string file in Directory.GetFiles(folder, "*.json"))
            {
                try
                {
                    ProducerProfile? profile =
                        JsonSerializer.Deserialize<ProducerProfile>(System.IO.File.ReadAllText(file));

                    if (!string.IsNullOrWhiteSpace(profile?.RequestedPosition))
                    {
                        count++;
                    }
                }
                catch (IOException)
                {
                }
                catch (JsonException)
                {
                }
            }

            return count;
        }
    }
}


namespace RESK.WIL.Models
{
    public class AdminDashboardViewModel
    {
        public string AdminName { get; set; } = "Admin User";

        public string AdminInitials { get; set; } = "AD";

        // Stat cards
        public int TotalProposals { get; set; }

        public int SubmittedThisMonth { get; set; }

        public int PendingReview { get; set; }

        public int ActiveUsers { get; set; }

        public int ActiveThisWeek { get; set; }

        // "4.8" or "—" when nothing has been reviewed yet
        public string AverageReviewDays { get; set; } = "—";

        // Chart (six months, oldest first)
        public List<string> Months { get; set; } = new();

        public List<int> SubmittedPerMonth { get; set; } = new();

        public List<int> ApprovedPerMonth { get; set; } = new();

        // Pending actions
        public int AwaitingReview { get; set; }

        public int RoleChangeRequests { get; set; }

        public int UsersWithoutRole { get; set; }

        // Recent proposals table
        public List<AdminRecentProposalRow> RecentProposals { get; set; } = new();
    }


    public class AdminRecentProposalRow
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Producer { get; set; } = string.Empty;

        // "05 Aug"
        public string DateText { get; set; } = string.Empty;

        public string StatusLabel { get; set; } = string.Empty;

        public string StatusCss { get; set; } = string.Empty;
    }
}