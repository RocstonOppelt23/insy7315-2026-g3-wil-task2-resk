using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using RESK.WIL.Models;
using RESK.WIL.Services;
using Xunit;

namespace RESK.WIL.Tests
{
    public class ProposalWorkflowTests
    {
        private static Proposal NewProposal(string status) =>
            new Proposal { ProposalStatus = status };

        [Theory]
        [InlineData(ProposalWorkflow.Draft, ProposalWorkflow.Submitted)]
        [InlineData(ProposalWorkflow.Submitted, ProposalWorkflow.UnderReview)]
        [InlineData(ProposalWorkflow.UnderReview, ProposalWorkflow.Approved)]
        [InlineData(ProposalWorkflow.UnderReview, ProposalWorkflow.Declined)]
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

        [Theory]
        [InlineData(ProposalWorkflow.Draft, ProposalWorkflow.Approved)]
        [InlineData(ProposalWorkflow.Draft, ProposalWorkflow.UnderReview)]
        [InlineData(ProposalWorkflow.Submitted, ProposalWorkflow.Approved)]
        [InlineData(ProposalWorkflow.Approved, ProposalWorkflow.Draft)]
        [InlineData(ProposalWorkflow.Declined, ProposalWorkflow.Submitted)]
        [InlineData("Pending", ProposalWorkflow.Submitted)]
        public void Illegal_moves_are_blocked_and_status_unchanged(string from, string to)
        {
            var proposal = NewProposal(from);
            var original = proposal.ProposalStatus;

            bool ok = ProposalWorkflow.TryMove(proposal, to, out string error);

            Assert.False(ok);
            Assert.NotEmpty(error);
            Assert.Equal(original, proposal.ProposalStatus);
        }

        [Fact]
        public void Submitting_sets_SubmittedAtUtc_only_first_time()
        {
            var proposal = new Proposal { ProposalStatus = ProposalWorkflow.Draft };

            Assert.Null(proposal.SubmittedAtUtc);

            bool ok1 = ProposalWorkflow.TryMove(proposal, ProposalWorkflow.Submitted, out _);
            Assert.True(ok1);
            Assert.NotNull(proposal.SubmittedAtUtc);
            var first = proposal.SubmittedAtUtc;

            bool ok2 = ProposalWorkflow.TryMove(proposal, ProposalWorkflow.UnderReview, out _);
            Assert.True(ok2);

            bool ok3 = ProposalWorkflow.TryMove(proposal, ProposalWorkflow.Draft, out _);
            Assert.True(ok3);

            bool ok4 = ProposalWorkflow.TryMove(proposal, ProposalWorkflow.Submitted, out _);
            Assert.True(ok4);

            Assert.Equal(first, proposal.SubmittedAtUtc);
        }

        [Fact]
        public void Successful_move_updates_UpdatedAtUtc()
        {
            var proposal = new Proposal { ProposalStatus = ProposalWorkflow.Draft };
            var before = proposal.UpdatedAtUtc;

            System.Threading.Thread.Sleep(10);

            bool ok = ProposalWorkflow.TryMove(proposal, ProposalWorkflow.Submitted, out _);
            Assert.True(ok);

            Assert.True(proposal.UpdatedAtUtc > before);
        }

        [Fact]
        public void IPPF_proposal_requires_Title_and_ProgrammeTitle()
        {
            var proposal = new Proposal
            {
                ProposalType = "IPPF",
                Title = string.Empty,
                ProgrammeTitle = string.Empty
            };

            var results = new System.Collections.Generic.List<ValidationResult>();
            var context = new ValidationContext(proposal);

            bool valid = Validator.TryValidateObject(proposal, context, results, validateAllProperties: true);

            Assert.False(valid);
            // Ensure at least Title and ProgrammeTitle are in the validation results member names
            var memberNames = results.SelectMany(r => r.MemberNames).Distinct().ToList();
            Assert.Contains(nameof(Proposal.Title), memberNames);
            Assert.Contains(nameof(Proposal.ProgrammeTitle), memberNames);
        }
    }
}
