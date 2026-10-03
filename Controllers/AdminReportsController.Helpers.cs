using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using RESK.WIL.Models;
using RESK.WIL.Services;

namespace RESK.WIL.Controllers
{
    public partial class AdminReportsController
    {
        private static ReportPeriod ResolvePeriod(string? range, string? from, string? to)
        {
            DateTime today = SouthAfricaTime.ToLocal(DateTime.UtcNow).Date;
            var period = new ReportPeriod { Range = range ?? "month" };

            switch (period.Range)
            {
                case "month":
                    period.FromDate = new DateTime(today.Year, today.Month, 1);
                    period.ToDate = period.FromDate.Value.AddMonths(1).AddDays(-1);
                    break;
                case "lastmonth":
                    period.FromDate = new DateTime(today.Year, today.Month, 1).AddMonths(-1);
                    period.ToDate = period.FromDate.Value.AddMonths(1).AddDays(-1);
                    break;
                case "30":
                    period.FromDate = today.AddDays(-29);
                    period.ToDate = today;
                    break;
                case "90":
                    period.FromDate = today.AddDays(-89);
                    period.ToDate = today;
                    break;
                case "year":
                    period.FromDate = new DateTime(today.Year, 1, 1);
                    period.ToDate = new DateTime(today.Year, 12, 31);
                    break;
                case "all":
                    break;
                default:
                    period.Range = "custom";
                    period.FromDate = ParseDate(from);
                    period.ToDate = ParseDate(to);

                    if (period.FromDate.HasValue && period.ToDate.HasValue && period.FromDate > period.ToDate)
                    {
                        period.Error = "The start date must be on or before the end date.";
                        (period.FromDate, period.ToDate) = (period.ToDate, period.FromDate);
                    }
                    break;
            }

            period.FromText = period.FromDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";
            period.ToText = period.ToDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";

            string Format(DateTime d) => d.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

            period.Label =
                period.FromDate.HasValue && period.ToDate.HasValue ? $"{Format(period.FromDate.Value)} — {Format(period.ToDate.Value)}" :
                period.FromDate.HasValue ? $"From {Format(period.FromDate.Value)}" :
                period.ToDate.HasValue ? $"Up to {Format(period.ToDate.Value)}" :
                "All time";

            return period;
        }


        private static DateTime? ParseDate(string? text)
        {
            return DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime d)
                ? d.Date
                : null;
        }


        private static string DefaultFileName(ReportPeriod period)
        {
            if (period.Range is "month" or "lastmonth" && period.FromDate.HasValue)
            {
                return "proposal-report-" + period.FromDate.Value.ToString("MMMM-yyyy", CultureInfo.InvariantCulture).ToLowerInvariant() + ".csv";
            }

            if (period.Range == "year" && period.FromDate.HasValue)
            {
                return $"proposal-report-{period.FromDate.Value.Year}.csv";
            }

            if (period.FromDate.HasValue && period.ToDate.HasValue)
            {
                return $"proposal-report-{period.FromText}-to-{period.ToText}.csv";
            }

            return "proposal-report-all-time.csv";
        }


        private static string CleanFileName(string name)
        {
            string clean = Regex.Replace(name.Trim(), @"[^A-Za-z0-9._\- ]", "").Replace(' ', '-');
            clean = Regex.Replace(clean, @"\.csv$", "", RegexOptions.IgnoreCase).Trim('.', '-');

            if (clean.Length == 0)
            {
                clean = "proposal-report";
            }

            if (clean.Length > 80)
            {
                clean = clean.Substring(0, 80);
            }

            return clean + ".csv";
        }


        private static string DescribeFilters(ReskExportForm form, ReportPeriod period)
        {
            var parts = new List<string> { period.Label };

            string? status = StatusOptions.FirstOrDefault(s => s.Key == form.Status && s.Key != "").Label;

            if (!string.IsNullOrEmpty(status))
            {
                parts.Add(status);
            }

            if (!string.IsNullOrWhiteSpace(form.Category))
            {
                parts.Add(form.Category);
            }

            if (!string.IsNullOrWhiteSpace(form.Producer))
            {
                parts.Add("one producer");
            }

            return string.Join(" • ", parts);
        }


        private static string BuildQuery(ReskExportForm form)
        {
            var pairs = new List<string>();

            void Add(string key, string? value)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    pairs.Add(key + "=" + Uri.EscapeDataString(value));
                }
            }

            Add("Range", form.Range);
            Add("From", form.From);
            Add("To", form.To);
            Add("Status", form.Status);
            Add("Category", form.Category);
            Add("Producer", form.Producer);
            Add("DateFormat", form.DateFormat);
            Add("FileName", form.FileName);

            foreach (string column in form.Columns ?? new List<string>())
            {
                Add("Columns", column);
            }

            return pairs.Count == 0 ? "" : "?" + string.Join("&", pairs);
        }


        private async Task SetAdminInfoAsync()
        {
            IdentityUser? me = await _userManager.GetUserAsync(User);
            ProducerProfile profile = me == null ? new ProducerProfile() : ProducerProfileStore.Load(Root, me.Id);

            bool hasName = !string.IsNullOrWhiteSpace(profile.FullName);

            ViewData["AdminName"] = hasName ? profile.FullName.Trim() : "Admin User";
            ViewData["AdminInitials"] = hasName ? ProducerProfileStore.Initials(profile.FullName) : "AD";
        }


        private class ReportRow
        {
            public int Id { get; set; }
            public string Reference { get; set; } = "";
            public string Title { get; set; } = "";
            public string ProducerId { get; set; } = "";
            public string Producer { get; set; } = "";
            public string ProducerEmail { get; set; } = "";
            public string Category { get; set; } = "—";
            public string Format { get; set; } = "";
            public string Duration { get; set; } = "";
            public string Language { get; set; } = "";
            public string StatusKey { get; set; } = "";
            public string StatusLabel { get; set; } = "";
            public string Reviewer { get; set; } = "Unassigned";
            public bool HasReviewer { get; set; }
            public DateTime? DeadlineLocal { get; set; }
            public DateTime SubmittedUtc { get; set; }
            public DateTime SubmittedLocal { get; set; }
            public DateTime? DecisionUtc { get; set; }
            public string DecidedBy { get; set; } = "";
            public DateTime UpdatedUtc { get; set; }
            public double? ReviewDays { get; set; }
        }


        private class ReportPeriod
        {
            public string Range { get; set; } = "month";
            public DateTime? FromDate { get; set; }
            public DateTime? ToDate { get; set; }
            public string FromText { get; set; } = "";
            public string ToText { get; set; } = "";
            public string Label { get; set; } = "";
            public string? Error { get; set; }

            public bool Contains(DateTime local)
            {
                return (!FromDate.HasValue || local.Date >= FromDate.Value) &&
                       (!ToDate.HasValue || local.Date <= ToDate.Value);
            }
        }
    }
}