using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using RESK.WIL.Models;
using RESK.WIL.Services;

namespace RESK.WIL.Controllers
{
    /*
     * =========================================================
     * ADMIN - AUDIT LOG (read-only)
     * =========================================================
     *
     *   GET /Admin/Audit              list, cards and filters
     *   GET /Admin/Audit/{id}         Audit event details
     *   GET /Admin/Audit/Export       CSV of the filtered events
     *
     * Events are written by ReskAuditMiddleware. This controller only
     * reads them: there is no edit or delete action on purpose.
     */
    [Authorize(Roles = "Admin")]
    public partial class AdminAuditController : Controller
    {
        private const string ViewFolder = "~/Views/AdminAudit/";
        private const int PageSize = 25;

        private readonly IWebHostEnvironment _environment;

        public AdminAuditController(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        private string Root => _environment.ContentRootPath;


        [HttpGet("Admin/Audit", Order = -1)]
        [HttpGet("Admin/AuditLog", Order = -1)]
        public IActionResult Index(string? search, string? type, string? module, string? from, string? to, string? range, int p = 1)
        {
            SetAdminInfo();

            List<ReskAuditEvent> all = ReskAuditLog.All(Root);
            ReskAuditFilter filter = ReadFilter(all, search, type, module, from, to, range);
            List<ReskAuditEvent> matches = Apply(all, filter);

            DateTime today = SouthAfricaTime.ToLocal(DateTime.UtcNow).Date;
            List<ReskAuditEvent> todays = all.Where(e => SouthAfricaTime.ToLocal(e.AtUtc).Date == today).ToList();

            int pages = Math.Max(1, (int)Math.Ceiling(matches.Count / (double)PageSize));
            int page = Math.Clamp(p, 1, pages);

            var model = new ReskAuditIndexViewModel
            {
                Search = filter.Search,
                Type = filter.Type,
                Module = filter.Module,
                From = filter.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                To = filter.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                HasFilters = filter.Custom,
                LogIsEmpty = all.Count == 0,
                Query = QueryString(filter),
                CanExport = MayExport(),

                EventsToday = todays.Count,
                SecurityToday = todays.Count(e => e.Module == "Security"),
                RoleChangesToday = todays.Count(e => e.IsRoleChange),
                ProposalActionsToday = todays.Count(e => e.Module == "Proposals"),

                Total = matches.Count,
                Page = page,
                Pages = pages,
                FirstRow = matches.Count == 0 ? 0 : (page - 1) * PageSize + 1,
                LastRow = Math.Min(page * PageSize, matches.Count),

                Rows = matches.Skip((page - 1) * PageSize).Take(PageSize).Select(e => new ReskAuditRow
                {
                    Id = e.Id,
                    When = ShortWhen(e.AtUtc, today),
                    User = e.UserName,
                    Action = e.Action,
                    Module = e.Module,
                    Record = e.Record,
                    Failed = e.IsFailed
                }).ToList()
            };

            return View(ViewFolder + "Index.cshtml", model);
        }


        [HttpGet("Admin/Audit/{id}", Order = -1)]
        public IActionResult Details(string id)
        {
            List<ReskAuditEvent> all = ReskAuditLog.All(Root);
            int index = all.FindIndex(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));

            if (index < 0)
            {
                TempData["AdminError"] = "That audit event could not be found.";
                return Redirect("/Admin/Audit");
            }

            SetAdminInfo();

            // "all" is newest first.
            return View(ViewFolder + "Details.cshtml", new ReskAuditDetailsViewModel
            {
                Event = all[index],
                When = SouthAfricaTime.ToLocal(all[index].AtUtc).ToString("dd MMMM yyyy 'at' HH:mm:ss", CultureInfo.InvariantCulture),
                NewerId = index > 0 ? all[index - 1].Id : null,
                OlderId = index < all.Count - 1 ? all[index + 1].Id : null
            });
        }


        [HttpGet("Admin/Audit/Export", Order = -1)]
        public IActionResult Export(string? search, string? type, string? module, string? from, string? to, string? range)
        {
            if (!MayExport())
            {
                return Redirect("/Admin/NoAccess?module=audit&need=export");
            }

            List<ReskAuditEvent> all = ReskAuditLog.All(Root);
            ReskAuditFilter filter = ReadFilter(all, search, type, module, from, to, range);

            var csv = new StringBuilder();
            csv.AppendLine("Event ID,Date,Time,User,Email,Role,Action,Module,Record,Record details,Result,Changes,IP address,Device,Source,Session ID");

            foreach (ReskAuditEvent e in Apply(all, filter))
            {
                DateTime local = SouthAfricaTime.ToLocal(e.AtUtc);
                string changes = string.Join(" | ", e.Changes.Select(c => $"{c.Field}: {c.Before} -> {c.After}"));

                csv.AppendLine(string.Join(",", new[]
                {
                    e.Id, local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), local.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                    e.UserName, e.UserEmail ?? "", e.UserRole ?? "", e.Action, e.Module, e.Record, e.RecordId ?? "",
                    e.Result, changes, e.Ip ?? "", e.Device ?? "", e.Source, e.SessionId ?? ""
                }.Select(Cell)));
            }

            string name = $"audit-log-{filter.From:yyyy-MM-dd}-to-{filter.To:yyyy-MM-dd}.csv";

            // The BOM lets Excel open the file with the right characters.
            byte[] bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();

            return File(bytes, "text/csv", name);
        }
    }
}