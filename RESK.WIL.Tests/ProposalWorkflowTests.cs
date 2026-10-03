using System.ComponentModel.DataAnnotations;
using RESK.WIL.Models;
using RESK.WIL.Services;

namespace RESK.WIL.Tests
{
    public class ProposalWorkflowTests
    {
        private static Proposal NewProposal(string status) =>
            new Proposal { ProposalStatus = status };

        // Rocston's code: allowed workflow transition tests.
        // Changed for integration with Kuan-Chi's code: Pending is the submitted state
        // because existing APIs already use Pending for submitted proposals.
        [Theory]
        [InlineData(ProposalWorkflow.Draft, ProposalWorkflow.Pending)]
        [InlineData(ProposalWorkflow.Pending, ProposalWorkflow.UnderReview)]
        [InlineData(ProposalWorkflow.UnderReview, ProposalWorkflow.Approved)]
        [InlineData(ProposalWorkflow.UnderReview, ProposalWorkflow.Rejected)]
        [InlineData(ProposalWorkflow.UnderReview, ProposalWorkflow.Draft)]
        public void Allowed_moves_succeed(string from, string to)
        {
            var proposal = NewProposal(from);

            var before = proposal.UpdatedAtUtc;
            bool ok = ProposalWorkflow.TryMove(proposal, to, out string error);

            Assert.True(ok);
            Assert.Equal(to, proposal.ProposalStatus);
            Assert.Equal(string.Empty, error);
            Assert.True(proposal.UpdatedAtUtc >= before);
        }

        // Rocston's code: illegal workflow transitions stay blocked.
        // Changed for integration with Kuan-Chi's code: Rejected is used instead of Declined
        // as the stored proposal status.
        [Theory]
        [InlineData(ProposalWorkflow.Draft, ProposalWorkflow.Approved)]
        [InlineData(ProposalWorkflow.Draft, ProposalWorkflow.UnderReview)]
        [InlineData(ProposalWorkflow.Pending, ProposalWorkflow.Approved)]
        [InlineData(ProposalWorkflow.Approved, ProposalWorkflow.Draft)]
        [InlineData(ProposalWorkflow.Rejected, ProposalWorkflow.Pending)]
        public void Illegal_moves_are_blocked_and_status_unchanged(string from, string to)
        {
            var proposal = NewProposal(from);
            var original = proposal.ProposalStatus;

            bool ok = ProposalWorkflow.TryMove(proposal, to, out string error);

            Assert.False(ok);
            Assert.NotEmpty(error);
            Assert.Equal(original, proposal.ProposalStatus);
        }

        // Rocston's code: submitted date is set only the first time.
        // Changed for integration with Kuan-Chi's code: uses Pending as the submitted state.
        [Fact]
        public void Submitting_sets_SubmittedAtUtc_only_first_time()
        {
            var proposal = new Proposal { ProposalStatus = ProposalWorkflow.Draft };

            Assert.Null(proposal.SubmittedAtUtc);

            bool ok1 = ProposalWorkflow.TryMove(proposal, ProposalWorkflow.Pending, out _);
            Assert.True(ok1);
            Assert.NotNull(proposal.SubmittedAtUtc);
            var first = proposal.SubmittedAtUtc;

            bool ok2 = ProposalWorkflow.TryMove(proposal, ProposalWorkflow.UnderReview, out _);
            Assert.True(ok2);

            bool ok3 = ProposalWorkflow.TryMove(proposal, ProposalWorkflow.Draft, out _);
            Assert.True(ok3);

            bool ok4 = ProposalWorkflow.TryMove(proposal, ProposalWorkflow.Pending, out _);
            Assert.True(ok4);

            Assert.Equal(first, proposal.SubmittedAtUtc);
        }

        // Rocston's code: successful transitions update UpdatedAtUtc.
        [Fact]
        public void Successful_move_updates_UpdatedAtUtc()
        {
            var proposal = new Proposal { ProposalStatus = ProposalWorkflow.Draft };
            var before = proposal.UpdatedAtUtc;

            Thread.Sleep(10);

            bool ok = ProposalWorkflow.TryMove(proposal, ProposalWorkflow.Pending, out _);
            Assert.True(ok);

            Assert.True(proposal.UpdatedAtUtc > before);
        }

        // Rocston's validation test idea integrated with Kuan-Chi's model.
        // Changed because Kuan-Chi's Proposal model does not have a Title property;
        // ProgrammeTitle is the field currently validated by the model.
        [Fact]
        public void IPPF_proposal_requires_ProgrammeTitle()
        {
            var proposal = new Proposal
            {
                ProposalType = "IPPF",
                ProgrammeTitle = string.Empty
            };

            var results = new List<ValidationResult>();
            var context = new ValidationContext(proposal);

            bool valid = Validator.TryValidateObject(
                proposal,
                context,
                results,
                validateAllProperties: true);

            Assert.False(valid);
            var memberNames = results.SelectMany(r => r.MemberNames).Distinct().ToList();
            Assert.Contains(nameof(Proposal.ProgrammeTitle), memberNames);
        }
    }
}
