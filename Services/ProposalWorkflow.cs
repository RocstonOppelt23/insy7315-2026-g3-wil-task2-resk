using RESK.WIL.Models;

namespace RESK.WIL.Services
{
    public static class ProposalWorkflow
    {
        // Rocston's code: central workflow constants and transition validation.
        // Changed for integration with Kuan-Chi's API: Pending is kept as the submitted state
        // because ApiProposalController already submits drafts by setting ProposalStatus to Pending.
        public const string Draft = "Draft";
        public const string Pending = "Pending";
        public const string Submitted = Pending;
        public const string UnderReview = "UnderReview";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public const string Declined = Rejected;

        // Rocston's code: each state lists the only states it is allowed to move to.
        // Changed for integration with Kuan-Chi's code: Rejected is used as the stored value,
        // while Declined remains an alias for the review API wording.
        private static readonly Dictionary<string, string[]> Allowed = new()
        {
            [Draft] = new[] { Pending },
            [Pending] = new[] { UnderReview },
            [UnderReview] = new[] { Approved, Rejected, Draft },
            [Approved] = Array.Empty<string>(),
            [Rejected] = Array.Empty<string>()
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

            if (to == Pending && proposal.SubmittedAtUtc == null)
            {
                proposal.SubmittedAtUtc = DateTime.UtcNow;
            }

            error = string.Empty;
            return true;
        }
    }
}
