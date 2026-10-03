namespace RESK.WIL.Services
{
    public static class ProposalTypes
    {
        // CTTV has fixed proposal form categories.
        // Added so API endpoints, filters and tests use the same source of truth.
        public const string IPPF = "IPPF";
        public const string MVSF = "MVSF";
        public const string CPAF = "CPAF";
        public const string PAF = "PAF";

        public static readonly string[] All =
        {
            IPPF,
            MVSF,
            CPAF,
            PAF
        };

        public static bool IsValid(string? proposalType) =>
            !string.IsNullOrWhiteSpace(proposalType) &&
            All.Contains(proposalType.Trim().ToUpperInvariant());
    }
}
