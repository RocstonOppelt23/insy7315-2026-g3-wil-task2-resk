namespace RESK.WIL.Models
{
    //----------Search Filters----------//
    public class SearchRequest
    {
        public string? Query { get; set; }
        public string? SearchType { get; set; }
        public string? SortBy { get; set; }
        public string? SortDirection { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class ProposalSearchRequest : SearchRequest
    {
        public string? ProposalType { get; set; }
        public string? Status { get; set; }
        public DateTime? FromDateUtc { get; set; }
        public DateTime? ToDateUtc { get; set; }
    }

    public class UserSearchRequest : SearchRequest
    {
        public int? RoleId { get; set; }
        public UserAccountStatus? AccountStatus { get; set; }
    }

    //----------Search Results----------//
    public class UserSearch
    {
        public int Id { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public UserAccountStatus AccountStatus { get; set; }
        public int? RoleId { get; set; }
        public string? RoleTitle { get; set; }
        public int ProposalCount { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public class ProposalSearch
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public DateTime? SubmittedAtUtc { get; set; }
        public int ProducerId { get; set; }
        public string ProducerName { get; set; } = string.Empty;
    }

    public class SearchResponse<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}
