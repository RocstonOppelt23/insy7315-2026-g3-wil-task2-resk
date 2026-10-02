using RESK.WIL.Services;

namespace RESK.WIL.Models
{
    // One line in the Audit log table.
    public class ReskAuditRow
    {
        public string Id { get; set; } = string.Empty;

        // "06 Aug, 10:42"
        public string When { get; set; } = string.Empty;

        public string User { get; set; } = string.Empty;

        public string Action { get; set; } = string.Empty;

        public string Module { get; set; } = string.Empty;

        public string Record { get; set; } = string.Empty;

        public bool Failed { get; set; }
    }


    public class ReskAuditIndexViewModel
    {
        // ---------- filters ----------
        public string Search { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;

        public string Module { get; set; } = string.Empty;

        // yyyy-MM-dd
        public string From { get; set; } = string.Empty;

        public string To { get; set; } = string.Empty;

        // True when the admin changed a filter (search, type, module or dates).
        public bool HasFilters { get; set; }

        // True when nothing at all has been recorded yet.
        public bool LogIsEmpty { get; set; }

        // The filters as a query string, for Export / Refresh / paging links.
        public string Query { get; set; } = string.Empty;

        public bool CanExport { get; set; }

        // ---------- cards (today) ----------
        public int EventsToday { get; set; }

        public int SecurityToday { get; set; }

        public int RoleChangesToday { get; set; }

        public int ProposalActionsToday { get; set; }

        // ---------- table ----------
        public List<ReskAuditRow> Rows { get; set; } = new();

        public int Total { get; set; }

        public int Page { get; set; } = 1;

        public int Pages { get; set; } = 1;

        public int FirstRow { get; set; }

        public int LastRow { get; set; }
    }


    public class ReskAuditDetailsViewModel
    {
        public ReskAuditEvent Event { get; set; } = new();

        // "06 August 2026 at 10:42:16"
        public string When { get; set; } = string.Empty;

        public string? NewerId { get; set; }

        public string? OlderId { get; set; }
    }
}