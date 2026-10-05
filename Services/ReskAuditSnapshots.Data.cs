using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RESK.WIL.Models;

namespace RESK.WIL.Services
{
    // Second half of ReskAuditSnapshots: users and proposals (read from the database).
    public static partial class ReskAuditSnapshots
    {
        // =========================================================
        // USER
        // =========================================================

        public static async Task<ReskAuditState?> UserAsync(RESK.WIL.Data.ApplicationDbContext db, string root, string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            IdentityUser? user = await db.Set<IdentityUser>().AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return null;
            }

            List<string> roles = await RoleNamesAsync(db, userId);
            ProducerProfile profile = ProducerProfileStore.Load(root, userId);
            ReskAccount account = ReskAccountStore.Load(root, userId);

            bool locked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;

            string status =
                !string.IsNullOrEmpty(account.Status) ? account.Status :
                locked ? "Locked" :
                roles.Count == 0 ? "Pending" : "Active";

            var state = new ReskAuditState
            {
                Name = ProducerProfileStore.DisplayName(profile, user),
                IdLabel = "Email: " + (user.Email ?? user.UserName ?? "\u2014"),
                Status = status
            };

            state.Set("Full name", profile.FullName);
            state.Set("Email", user.Email);
            state.Set("Phone", user.PhoneNumber);
            state.Set("Organisation", profile.Organisation);
            state.Set("Role", ReskRoleStore.RoleFor(root, userId, roles)?.Name ?? (roles.Count == 0 ? "No role yet" : roles[0]));
            state.Set("Account status", status);
            state.Set("Suspended until", account.SuspendedUntilUtc.HasValue
                ? SouthAfricaTime.ToLocal(account.SuspendedUntilUtc.Value).ToString("dd MMM yyyy 'at' HH:mm")
                : null);
            state.Set("Restriction reason", account.RestrictionReason);
            state.Set("Sign-in", locked ? "Locked" : "Allowed");
            state.Set("Requested position", profile.RequestedPosition);

            return state;
        }


        public static async Task<List<string>> RoleNamesAsync(RESK.WIL.Data.ApplicationDbContext db, string userId)
        {
            return await (
                from link in db.Set<IdentityUserRole<string>>().AsNoTracking()
                join role in db.Set<IdentityRole>().AsNoTracking() on link.RoleId equals role.Id
                where link.UserId == userId
                select role.Name ?? "").ToListAsync();
        }


        // =========================================================
        // PROPOSALS (id -> state)
        // =========================================================

        public static async Task<Dictionary<int, ReskAuditState>> ProposalsAsync(
            RESK.WIL.Data.ApplicationDbContext db, string root, string? ownerUserId)
        {
            IQueryable<ProducerProposal> query = db.ProducerProposals.AsNoTracking();

            if (!string.IsNullOrEmpty(ownerUserId))
            {
                query = query.Where(p => p.OwnerUserId == ownerUserId);
            }

            List<ProducerProposal> proposals = await query.ToListAsync();
            Dictionary<int, ProposalReview> reviews = ProposalReviewStore.LoadAll(root);
            var result = new Dictionary<int, ReskAuditState>();

            foreach (ProducerProposal proposal in proposals)
            {
                reviews.TryGetValue(proposal.Id, out ProposalReview? review);

                var state = new ReskAuditState
                {
                    Name = string.IsNullOrWhiteSpace(proposal.Reference) ? proposal.DisplayTitle : proposal.Reference,
                    IdLabel = string.IsNullOrWhiteSpace(proposal.Reference) ? "Draft #" + proposal.Id : proposal.DisplayTitle,
                    Status = proposal.Status
                };

                state.Set("Status", StatusLabel(proposal.Status));
                state.Set("Category", proposal.Category);
                state.Set("Reviewer", review?.ReviewerName);
                state.Set("Review deadline", review?.Deadline?.ToString("dd MMM yyyy"));
                state.Set("Reviewer recommendation", review != null && (review.ReviewSubmitted || review.Decision != null)
                    ? DecisionLabel(review.Recommendation)
                    : null);
                state.Set("Final decision", DecisionLabel(review?.Decision));

                result[proposal.Id] = state;
            }

            return result;
        }


        private static string? DecisionLabel(string? decision)
        {
            return decision switch
            {
                null or "" => null,
                ProposalReviewStore.Approve => "Approve",
                ProposalReviewStore.RequestChanges => "Request changes",
                ProposalReviewStore.Reject => "Do not approve",
                _ => decision
            };
        }


        public static string StatusLabel(string status)
        {
            return status switch
            {
                ProposalStatuses.Draft => "Draft",
                ProposalStatuses.InReview => "In review",
                ProposalStatuses.Approved => "Approved",
                ProposalStatuses.ChangesRequested => "Changes requested",
                ProposalStatuses.Rejected => "Not approved",
                _ => status
            };
        }
    }
}