using RESK.WIL.Models;

namespace RESK.WIL.ViewModels
{
    public class UserSearchViewModel
    {
        public UserSearchRequest Filters { get; set; } = new();
        public List<UserSearch> Results { get; set; } = new();

        public List<string> SearchTypes { get; set; }
            = new() { "Name", "Role", "Status" };

        public List<string> SortOptions { get; set; }
            = new() { "Name", "Role", "Status", "ProposalCount", "CreatedAt" };

        public List<AccessRole> Roles { get; set; } = new();
    }

    public class ProposalSearchViewModel
    {
        public ProposalSearchRequest Filters { get; set; } = new();
        public List<ProposalSearch> Results { get; set; } = new();

        public List<string> SearchTypes { get; set; }
            = new() { "Title", "Producer", "Type", "Status", "Date" };

        public List<string> ProposalTypes { get; set; }
            = new() { "IPPF", "MVSF", "CPAF", "PAF" };

        public List<string> Statuses { get; set; }
            = new() { "Draft", "Pending", "Approved", "Rejected" };

        public List<string> SortOptions { get; set; }
            = new() { "Title", "Type", "Status", "CreatedAt", "UpdatedAt", "SubmittedAt" };
    }
}
