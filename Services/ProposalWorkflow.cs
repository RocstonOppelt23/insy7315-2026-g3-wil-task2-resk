using RESK.WIL.Models;

namespace RESK.WIL.Services
{
    public static class ProposalWorkflow
    {
        public const string Draft = "Draft";
        public const string Submitted = "Submitted";
        public const string UnderReview = "UnderReview";
        public const string Approved = "Approved";
        public const string Declined = "Declined";

        // Each state lists the only states it is allowed to move to.
        private static readonly Dictionary<string, string[]> Allowed = new()
        {
            [Draft] = new[] { Submitted },
            [Submitted] = new[] { UnderReview },
            [UnderReview] = new[] { Approved, Declined, Draft }, // Draft = sent back for changes
            [Approved] = Array.Empty<string>(),
            [Declined] = Array.Empty<string>()
        };

        public static bool CanMove(string from, string to) =>
            Allowed.TryGetValue(from, out var next) && next.Contains(to);

        public static bool TryMove(Proposal proposal, string to, out string error)
        {
            if (!CanMove(proposal.ProposalStatus, to))
            {
                error = $"A proposal cannot move from {proposal.ProposalStatus} to {to}.";
                return false;
            }

            proposal.ProposalStatus = to;
            proposal.UpdatedAtUtc = DateTime.UtcNow;

            if (to == Submitted && proposal.SubmittedAtUtc == null)
            {
                proposal.SubmittedAtUtc = DateTime.UtcNow;
            }

            error = string.Empty;
            return true;
        }


        // =====================================================
        // PRODUCER PROPOSALS (used by the producer and admin screens)
        // Draft -> InReview -> Approved / Rejected / ChangesRequested
        // ChangesRequested -> InReview (producer resubmits)
        // =====================================================

        private static readonly Dictionary<string, string[]> ProducerAllowed = new()
        {
            [ProposalStatuses.Draft] = new[] { ProposalStatuses.InReview },
            [ProposalStatuses.ChangesRequested] = new[] { ProposalStatuses.InReview },
            [ProposalStatuses.InReview] = new[]
            {
                ProposalStatuses.Approved,
                ProposalStatuses.Rejected,
                ProposalStatuses.ChangesRequested
            },
            [ProposalStatuses.Approved] = Array.Empty<string>(),
            [ProposalStatuses.Rejected] = Array.Empty<string>()
        };

        public static bool CanMoveProducer(string from, string to) =>
            ProducerAllowed.TryGetValue(from, out var next) && next.Contains(to);

        public static bool TryMove(ProducerProposal proposal, string to, out string error)
        {
            if (!CanMoveProducer(proposal.Status, to))
            {
                error = $"A proposal cannot move from {ProposalStatuses.Label(proposal.Status)} to {ProposalStatuses.Label(to)}.";
                return false;
            }

            proposal.Status = to;
            proposal.UpdatedAtUtc = DateTime.UtcNow;
            error = string.Empty;
            return true;
        }
    }
}