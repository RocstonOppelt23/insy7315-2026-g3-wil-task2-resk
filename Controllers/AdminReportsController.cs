using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RESK.WIL.Models;
using RESK.WIL.Services;

namespace RESK.WIL.Controllers
{
    /*
     * =========================================================
     * ADMIN - REPORTS
     * =========================================================
     *
     *   GET  /Admin/Reports                   overview
     *   GET  /Admin/Reports/Export            "Export CSV" settings screen
     *   GET  /Admin/Reports/Export/Preview    live count + first rows (JSON)
     *   POST /Admin/Reports/Export            downloads the CSV
     *   GET  /Admin/Reports/Summary           printable summary (Save as PDF)
     *
     * Only submitted proposals are reported. Drafts are private to
     * producers and never appear in reports or exports.
     * Date ranges use the SUBMISSION date in South African time.
     */
    [Authorize(Roles = "Admin")]
    public partial class AdminReportsController : Controller
    {
        private const string ViewFolder = "~/Views/AdminReports/";

        private readonly RESK.WIL.Data.ApplicationDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IWebHostEnvironment _environment;

        public AdminReportsController(
            RESK.WIL.Data.ApplicationDbContext db,
            UserManager<IdentityUser> userManager,
            IWebHostEnvironment environment)
        {
            _db = db;
            _userManager = userManager;
            _environment = environment;
        }

        private string Root => _environment.ContentRootPath;


        // Columns the CSV can contain: key, heading, ticked by default, shown under "More columns"
        public static readonly (string Key, string Label, bool Default, bool More)[] ExportColumns =
        {
            ("reference", "Proposal ID", true, false),
            ("title", "Programme title", true, false),
            ("producer", "Producer name", true, false),
            ("category", "Category", true, false),
            ("submitted", "Submission date", true, false),
            ("status", "Current status", true, false),
            ("reviewer", "Assigned reviewer", false, false),
            ("decision", "Decision date", false, false),
            ("email", "Producer email", false, true),
            ("format", "Programme format", false, true),
            ("duration", "Episode duration", false, true),
            ("language", "Primary language", false, true),
            ("deadline", "Review deadline", false, true),
            ("decidedBy", "Decided by", false, true),
            ("reviewDays", "Days to decision", false, true),
            ("updated", "Last updated", false, true)
        };

        public static readonly (string Key, string Label)[] StatusOptions =
        {
            ("", "All statuses"),
            ("open", "Open (not decided yet)"),
            ("assign", "Needs assignment"),
            ("review", "In review"),
            ("changes", "Changes needed"),
            ("approved", "Approved"),
            ("rejected", "Not approved"),
            ("decided", "Decided (approved or not approved)")
        };

        public static readonly (string Key, string Label)[] RangeOptions =
        {
            ("month", "This month"),
            ("lastmonth", "Last month"),
            ("30", "Last 30 days"),
            ("90", "Last 90 days"),
            ("year", "This year"),
            ("all", "All time"),
            ("custom", "Custom dates")
        };


        // =========================================================
        // OVERVIEW
        // =========================================================

        [HttpGet("Admin/Reports", Order = -1)]
        public async Task<IActionResult> Index(string? range, string? from, string? to)
        {
            await SetAdminInfoAsync();

            List<ReportRow> all = await LoadRowsAsync();
            ReportPeriod period = ResolvePeriod(range ?? "90", from, to);
            List<ReportRow> rows = all.Where(r => period.Contains(r.SubmittedLocal)).ToList();

            var model = new ReskReportsViewModel
            {
                Range = period.Range,
                From = period.FromText,
                To = period.ToText,
                PeriodLabel = period.Label
            };

            FillKpis(model, rows, all, period);
            FillBreakdowns(model, rows);
            FillMonthly(model, all);
            FillReviewers(model, all, period);
            FillWaiting(model, all);
            FillProducers(model, rows);

            model.RecentExports = ReskExportLog.All(Root).Take(6).Select(e => new ReskExportHistoryRow
            {
                FileName = e.FileName,
                By = e.By,
                When = SouthAfricaTime.ToLocal(e.AtUtc).ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture),
                Rows = e.Rows,
                Columns = e.Columns,
                Summary = e.Summary,
                Query = e.Query
            }).ToList();

            return View(ViewFolder + "Index.cshtml", model);
        }


        // =========================================================
        // EXPORT CSV
        // =========================================================

        [HttpGet("Admin/Reports/Export", Order = -1)]
        public async Task<IActionResult> Export([FromQuery] ReskExportForm form)
        {
            await SetAdminInfoAsync();

            List<ReportRow> all = await LoadRowsAsync();

            // First visit: this month, the six standard columns.
            if (string.IsNullOrWhiteSpace(form.Range) && string.IsNullOrWhiteSpace(form.From))
            {
                form.Range = "month";
            }

            if (form.Columns == null || form.Columns.Count == 0)
            {
                form.Columns = ExportColumns.Where(c => c.Default).Select(c => c.Key).ToList();
            }

            ReportPeriod period = ResolvePeriod(form.Range, form.From, form.To);
            form.Range = period.Range;
            form.From = period.FromText;
            form.To = period.ToText;

            if (string.IsNullOrWhiteSpace(form.FileName))
            {
                form.FileName = DefaultFileName(period);
            }

            List<ReportRow> matching = ApplyFilters(all, form, period);

            var model = new ReskExportViewModel
            {
                Form = form,
                MatchingCount = matching.Count,
                Categories = all.Select(r => r.Category).Where(c => c != "—")
                    .Concat(ReskCategoryStore.All(Root).Select(c => c.Name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(c => c)
                    .ToList(),
                Producers = all.GroupBy(r => r.ProducerId)
                    .Select(g => (g.Key, g.First().Producer))
                    .OrderBy(p => p.Producer)
                    .ToList(),
                RecentExports = ReskExportLog.All(Root).Take(5).Select(e => new ReskExportHistoryRow
                {
                    FileName = e.FileName,
                    By = e.By,
                    When = SouthAfricaTime.ToLocal(e.AtUtc).ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture),
                    Rows = e.Rows,
                    Columns = e.Columns,
                    Summary = e.Summary,
                    Query = e.Query
                }).ToList()
            };

            return View(ViewFolder + "Export.cshtml", model);
        }


        [HttpGet("Admin/Reports/Export/Preview", Order = -1)]
        public async Task<IActionResult> Preview([FromQuery] ReskExportForm form)
        {
            List<ReportRow> all = await LoadRowsAsync();
            ReportPeriod period = ResolvePeriod(form.Range, form.From, form.To);
            List<ReportRow> matching = ApplyFilters(all, form, period);
            List<(string Key, string Label, bool Default, bool More)> columns = SelectedColumns(form);

            return Json(new
            {
                count = matching.Count,
                columns = columns.Select(c => c.Label),
                rows = matching.Take(5).Select(r => columns.Select(c => Cell(r, c.Key, form.DateFormat))),
                period = period.Label,
                fileName = string.IsNullOrWhiteSpace(form.FileName) ? DefaultFileName(period) : CleanFileName(form.FileName),
                error = period.Error
            });
        }


        [HttpPost("Admin/Reports/Export", Order = -1)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Download(ReskExportForm form)
        {
            List<ReportRow> all = await LoadRowsAsync();
            ReportPeriod period = ResolvePeriod(form.Range, form.From, form.To);
            List<(string Key, string Label, bool Default, bool More)> columns = SelectedColumns(form);

            if (period.Error != null || columns.Count == 0)
            {
                TempData["AdminError"] = period.Error ?? "Choose at least one column to include.";
                return Redirect("/Admin/Reports/Export" + BuildQuery(form));
            }

            List<ReportRow> matching = ApplyFilters(all, form, period);
            string fileName = string.IsNullOrWhiteSpace(form.FileName) ? DefaultFileName(period) : CleanFileName(form.FileName);

            var csv = new StringBuilder();
            csv.AppendLine(string.Join(",", columns.Select(c => Csv(c.Label))));

            foreach (ReportRow row in matching)
            {
                csv.AppendLine(string.Join(",", columns.Select(c => Csv(Cell(row, c.Key, form.DateFormat)))));
            }

            ReskExportLog.Add(Root, new ReskExportEntry
            {
                By = User.Identity?.Name ?? "Admin",
                FileName = fileName,
                Rows = matching.Count,
                Columns = columns.Count,
                Summary = DescribeFilters(form, period),
                Query = BuildQuery(form)
            });

            // UTF-8 with BOM so Excel shows accents and "—" correctly.
            byte[] bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();

            return File(bytes, "text/csv", fileName);
        }


        // =========================================================
        // PRINTABLE SUMMARY
        // =========================================================

        [HttpGet("Admin/Reports/Summary", Order = -1)]
        public async Task<IActionResult> Summary(string? range, string? from, string? to)
        {
            List<ReportRow> all = await LoadRowsAsync();
            ReportPeriod period = ResolvePeriod(range ?? "90", from, to);
            List<ReportRow> rows = all.Where(r => period.Contains(r.SubmittedLocal)).ToList();

            var model = new ReskReportsViewModel
            {
                Range = period.Range,
                From = period.FromText,
                To = period.ToText,
                PeriodLabel = period.Label
            };

            FillKpis(model, rows, all, period);
            FillBreakdowns(model, rows);
            FillReviewers(model, all, period);
            FillProducers(model, rows);

            model.Proposals = rows
                .OrderBy(r => r.SubmittedLocal)
                .Select(r => new ReskReportProposalRow
                {
                    Id = r.Id,
                    Reference = r.Reference,
                    Title = r.Title,
                    Producer = r.Producer,
                    Category = r.Category,
                    Status = r.StatusLabel,
                    StatusKey = r.StatusKey,
                    Submitted = r.SubmittedLocal.ToString("d MMM yyyy", CultureInfo.InvariantCulture),
                    Reviewer = r.Reviewer
                })
                .ToList();

            ViewData["GeneratedBy"] = User.Identity?.Name ?? "Admin";
            ViewData["GeneratedAt"] = SouthAfricaTime.ToLocal(DateTime.UtcNow).ToString("d MMMM yyyy 'at' HH:mm", CultureInfo.InvariantCulture);

            return View(ViewFolder + "Summary.cshtml", model);
        }
    }
}