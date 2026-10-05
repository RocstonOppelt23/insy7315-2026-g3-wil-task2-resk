using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RESK.WIL.Models;
using RESK.WIL.Services;

namespace RESK.WIL.Controllers
{
    public partial class AdminReportsController
    {
        // =========================================================
        // OVERVIEW SECTIONS
        // =========================================================

        private static void FillKpis(ReskReportsViewModel model, List<ReportRow> rows, List<ReportRow> all, ReportPeriod period)
        {
            model.Submitted = rows.Count;
            model.Approved = rows.Count(r => r.StatusKey == "approved");
            model.Rejected = rows.Count(r => r.StatusKey == "rejected");
            model.Changes = rows.Count(r => r.StatusKey == "changes");
            model.OpenNow = all.Count(r => r.StatusKey is "assign" or "review");
            model.NeedsAssignment = all.Count(r => r.StatusKey == "assign");

            int decided = model.Approved + model.Rejected;
            model.ApprovalRate = decided == 0 ? "—" : Math.Round(100.0 * model.Approved / decided).ToString("0", CultureInfo.InvariantCulture) + "%";

            List<double> days = rows.Where(r => r.ReviewDays.HasValue).Select(r => r.ReviewDays!.Value).ToList();
            model.AverageReviewDays = days.Count == 0 ? "—" : days.Average().ToString("0.0", CultureInfo.InvariantCulture);

            // Compare with the same length of time just before.
            if (period.FromDate.HasValue && period.ToDate.HasValue)
            {
                int length = (period.ToDate.Value - period.FromDate.Value).Days + 1;
                DateTime prevTo = period.FromDate.Value.AddDays(-1);
                DateTime prevFrom = prevTo.AddDays(-(length - 1));
                int previous = all.Count(r => r.SubmittedLocal.Date >= prevFrom && r.SubmittedLocal.Date <= prevTo);

                if (previous == 0)
                {
                    model.SubmittedChange = model.Submitted == 0 ? "No change on the previous period" : "None in the previous period";
                }
                else
                {
                    double change = 100.0 * (model.Submitted - previous) / previous;
                    model.SubmittedChange = change == 0
                        ? "Same as the previous period"
                        : $"{(change > 0 ? "▲" : "▼")} {Math.Abs(change):0}% vs previous period ({previous})";
                    model.SubmittedChangeUp = change > 0;
                }
            }
        }


        private void FillBreakdowns(ReskReportsViewModel model, List<ReportRow> rows)
        {
            int total = Math.Max(rows.Count, 1);

            model.Statuses = new[]
            {
                ("assign", "Needs assignment"),
                ("review", "In review"),
                ("changes", "Changes needed"),
                ("approved", "Approved"),
                ("rejected", "Not approved")
            }
            .Select(s =>
            {
                int count = rows.Count(r => r.StatusKey == s.Item1);
                return new ReskBreakdownRow { Key = s.Item1, Label = s.Item2, Count = count, Percent = 100.0 * count / total };
            })
            .ToList();

            Dictionary<string, string> colours = ReskCategoryStore.All(Root)
                .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => ReskCategoryStore.Hex(g.First().IconColour), StringComparer.OrdinalIgnoreCase);

            List<ReskBreakdownRow> categories = rows
                .GroupBy(r => r.Category, StringComparer.OrdinalIgnoreCase)
                .Select(g => new ReskBreakdownRow
                {
                    Key = g.Key,
                    Label = g.Key == "—" ? "No category" : g.Key,
                    Count = g.Count(),
                    Percent = 100.0 * g.Count() / total,
                    Colour = colours.TryGetValue(g.Key, out string? hex) ? hex : "#8493aa"
                })
                .OrderByDescending(c => c.Count)
                .ThenBy(c => c.Label)
                .ToList();

            // Top 7 + "Other"
            if (categories.Count > 8)
            {
                int other = categories.Skip(7).Sum(c => c.Count);
                categories = categories.Take(7).ToList();
                categories.Add(new ReskBreakdownRow { Key = "", Label = "Other", Count = other, Percent = 100.0 * other / total, Colour = "#8493aa" });
            }

            model.Categories = categories;
        }


        private static void FillMonthly(ReskReportsViewModel model, List<ReportRow> all)
        {
            DateTime now = SouthAfricaTime.ToLocal(DateTime.UtcNow);
            DateTime first = new DateTime(now.Year, now.Month, 1).AddMonths(-11);

            for (int i = 0; i < 12; i++)
            {
                DateTime start = first.AddMonths(i);
                DateTime end = start.AddMonths(1);

                model.Monthly.Add(new ReskMonthPoint
                {
                    Label = start.ToString("MMM", CultureInfo.InvariantCulture),
                    FullLabel = start.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
                    Submitted = all.Count(r => r.SubmittedLocal >= start && r.SubmittedLocal < end),
                    Approved = all.Count(r =>
                        r.StatusKey == "approved" &&
                        r.DecisionUtc.HasValue &&
                        SouthAfricaTime.ToLocal(r.DecisionUtc.Value) >= start &&
                        SouthAfricaTime.ToLocal(r.DecisionUtc.Value) < end)
                });
            }
        }


        private static void FillReviewers(ReskReportsViewModel model, List<ReportRow> all, ReportPeriod period)
        {
            model.Reviewers = all
                .Where(r => r.HasReviewer)
                .GroupBy(r => r.Reviewer)
                .Select(g =>
                {
                    List<ReportRow> decidedInPeriod = g
                        .Where(r => r.DecisionUtc.HasValue && period.Contains(SouthAfricaTime.ToLocal(r.DecisionUtc.Value)))
                        .ToList();
                    List<double> days = decidedInPeriod.Where(r => r.ReviewDays.HasValue).Select(r => r.ReviewDays!.Value).ToList();

                    return new ReskReviewerRow
                    {
                        Name = g.Key,
                        Open = g.Count(r => r.StatusKey == "review"),
                        Completed = decidedInPeriod.Count,
                        Approved = decidedInPeriod.Count(r => r.StatusKey == "approved"),
                        AverageDays = days.Count == 0 ? "—" : days.Average().ToString("0.0", CultureInfo.InvariantCulture)
                    };
                })
                .OrderByDescending(r => r.Open)
                .ThenByDescending(r => r.Completed)
                .ToList();
        }


        private static void FillWaiting(ReskReportsViewModel model, List<ReportRow> all)
        {
            DateTime now = DateTime.UtcNow;

            model.Waiting = all
                .Where(r => r.StatusKey is "assign" or "review")
                .OrderBy(r => r.SubmittedUtc)
                .Take(5)
                .Select(r => new ReskWaitingRow
                {
                    Id = r.Id,
                    Title = r.Title,
                    Reference = r.Reference,
                    Reviewer = r.Reviewer,
                    StatusKey = r.StatusKey,
                    Days = Math.Max(0, (int)Math.Floor((now - r.SubmittedUtc).TotalDays))
                })
                .ToList();
        }


        private static void FillProducers(ReskReportsViewModel model, List<ReportRow> rows)
        {
            model.TopProducers = rows
                .GroupBy(r => r.ProducerId)
                .Select(g => new ReskProducerRow
                {
                    Name = g.First().Producer,
                    Submitted = g.Count(),
                    Approved = g.Count(r => r.StatusKey == "approved")
                })
                .OrderByDescending(p => p.Submitted)
                .ThenByDescending(p => p.Approved)
                .ThenBy(p => p.Name)
                .Take(5)
                .ToList();
        }
    }
}