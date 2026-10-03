using RESK.WIL.Services;

namespace RESK.WIL.Tests
{
    public class ProposalTypesTests
    {
        [Fact]
        public void All_contains_only_fixed_cttv_proposal_types()
        {
            Assert.Equal(
                new[] { "IPPF", "MVSF", "CPAF", "PAF" },
                ProposalTypes.All);
        }

        [Theory]
        [InlineData("IPPF")]
        [InlineData("mvsf")]
        [InlineData(" cpaf ")]
        [InlineData("PAF")]
        public void IsValid_accepts_fixed_proposal_types(string proposalType)
        {
            Assert.True(ProposalTypes.IsValid(proposalType));
        }

        [Theory]
        [InlineData("")]
        [InlineData("TDLA")]
        [InlineData("Music")]
        public void IsValid_rejects_unknown_proposal_types(string proposalType)
        {
            Assert.False(ProposalTypes.IsValid(proposalType));
        }
    }
}
