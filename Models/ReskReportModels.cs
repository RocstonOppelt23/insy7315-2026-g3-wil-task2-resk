// Models for the admin Reports screens (see Controllers/AdminReportsController*.cs).

namespace RESK.WIL.Models
{
    public class ReskExportForm
    {
        public string? FileName { get; set; }
        public string? Range { get; set; }
        public string? From { get; set; }
        public string? To { get; set; }
        public string? Status { get; set; }
        public string? Category { get; set; }
        public string? Producer { get; set; }
        public List<string> Columns { get; set; } = new();

        // "dmy" (01 Aug 2026) or "iso" (2026-08-01)
        public string? DateFormat { get; set; } = "dmy";
    }


    public class ReskExportViewModel
    {
        public ReskExportForm Form { get; set; } = new();
        public int MatchingCount { get; set; }
        public List<string> Categories { get; set; } = new();
        public List<(string Id, string Name)> Producers { get; set; } = new();
        public List<ReskExportHistoryRow> RecentExports { get; set; } = new();
    }


    public class ReskExportHistoryRow
    {
        public string FileName { get; set; } = "";
        public string By { get; set; } = "";
        public string When { get; set; } = "";
        public int Rows { get; set; }
        public int Columns { get; set; }
        public string Summary { get; set; } = "";
        public string Query { get; set; } = "";
    }


    public class ReskReportsViewModel
    {
        public string Range { get; set; } = "90";
        public string From { get; set; } = "";
        public string To { get; set; } = "";
        public string PeriodLabel { get; set; } = "";

        public int Submitted { get; set; }
        public string? SubmittedChange { get; set; }
        public bool SubmittedChangeUp { get; set; }
        public int Approved { get; set; }
        public int Rejected { get; set; }
        public int Changes { get; set; }
        public int OpenNow { get; set; }
        public int NeedsAssignment { get; set; }
        public string ApprovalRate { get; set; } = "—";
        public string AverageReviewDays { get; set; } = "—";

        public List<ReskMonthPoint> Monthly { get; set; } = new();
        public List<ReskBreakdownRow> Statuses { get; set; } = new();
        public List<ReskBreakdownRow> Categories { get; set; } = new();
        public List<ReskReviewerRow> Reviewers { get; set; } = new();
        public List<ReskWaitingRow> Waiting { get; set; } = new();
        public List<ReskProducerRow> TopProducers { get; set; } = new();
        public List<ReskExportHistoryRow> RecentExports { get; set; } = new();
        public List<ReskReportProposalRow> Proposals { get; set; } = new();
    }


    public class ReskMonthPoint
    {
        public string Label { get; set; } = "";
        public string FullLabel { get; set; } = "";
        public int Submitted { get; set; }
        public int Approved { get; set; }
    }


    public class ReskBreakdownRow
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public int Count { get; set; }
        public double Percent { get; set; }
        public string Colour { get; set; } = "#0a9fb2";
    }


    public class ReskReviewerRow
    {
        public string Name { get; set; } = "";
        public int Open { get; set; }
        public int Completed { get; set; }
        public int Approved { get; set; }
        public string AverageDays { get; set; } = "—";
    }


    public class ReskWaitingRow
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Reference { get; set; } = "";
        public string Reviewer { get; set; } = "";
        public string StatusKey { get; set; } = "";
        public int Days { get; set; }
    }


    public class ReskProducerRow
    {
        public string Name { get; set; } = "";
        public int Submitted { get; set; }
        public int Approved { get; set; }
    }


    public class ReskReportProposalRow
    {
        public int Id { get; set; }
        public string Reference { get; set; } = "";
        public string Title { get; set; } = "";
        public string Producer { get; set; } = "";
        public string Category { get; set; } = "";
        public string Status { get; set; } = "";
        public string StatusKey { get; set; } = "";
        public string Submitted { get; set; } = "";
        public string Reviewer { get; set; } = "";
    }
}