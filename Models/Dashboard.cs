namespace RESK.WIL.Models
{
    public class DashboardSummaryResponse
    {
        public ProposalDashboardSummary Proposals { get; set; } = new();
        public UserDashboardSummary? Users { get; set; }
    }

    public class ProposalDashboardSummary
    {
        public int Total { get; set; }
        public int Draft { get; set; }
        public int Pending { get; set; }
        public int UnderReview { get; set; }
        public int Approved { get; set; }
        public int Rejected { get; set; }
        public int MyProposals { get; set; }
    }

    public class UserDashboardSummary
    {
        public int Total { get; set; }
        public int Pending { get; set; }
        public int Active { get; set; }
        public int Inactive { get; set; }
        public int Disabled { get; set; }
    }
}
