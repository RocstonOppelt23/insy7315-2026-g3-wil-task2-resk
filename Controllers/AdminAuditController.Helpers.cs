using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using RESK.WIL.Models;
using RESK.WIL.Services;

namespace RESK.WIL.Controllers
{
    // The filters chosen on the Audit log page.
    public class ReskAuditFilter
    {
        public string Search { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;

        public string Module { get; set; } = string.Empty;

        public DateTime From { get; set; }

        public DateTime To { get; set; }

        // True when the admin changed something from the default view.
        public bool Custom { get; set; }
    }


    public partial class AdminAuditController
    {
        // Default view: the last 7 days. "range=all" shows everything recorded.
        private static ReskAuditFilter ReadFilter(List<ReskAuditEvent> all, string? search, string? type, string? module,
            string? from, string? to, string? range)
        {
            DateTime today = SouthAfricaTime.ToLocal(DateTime.UtcNow).Date;

            var filter = new ReskAuditFilter
            {
                Search = (search ?? "").Trim(),
                Type = ReskAuditLog.Types.Any(t => t.Key == type) ? type! : "",
                Module = module != null && ReskAuditLog.Modules.Contains(module) ? module : "",
                From = today.AddDays(-6),
                To = today
            };

            bool hasFrom = TryDate(from, out DateTime fromDate);
            bool hasTo = TryDate(to, out DateTime toDate);

            if (hasFrom)
            {
                filter.From = fromDate;
            }

            if (hasTo)
            {
                filter.To = toDate;
            }

            if (range == "all" && !hasFrom && !hasTo)
            {
                // "all" is newest first, so the last one is the oldest.
                filter.From = all.Count == 0 ? today : SouthAfricaTime.ToLocal(all[^1].AtUtc).Date;
            }

            if (filter.From > filter.To)
            {
                (filter.From, filter.To) = (filter.To, filter.From);
            }

            filter.Custom = filter.Search.Length > 0 || filter.Type.Length > 0 || filter.Module.Length > 0 ||
                            hasFrom || hasTo || range == "all";

            return filter;
        }


        private static List<ReskAuditEvent> Apply(List<ReskAuditEvent> all, ReskAuditFilter filter)
        {
            IEnumerable<ReskAuditEvent> query = all.Where(e =>
            {
                DateTime day = SouthAfricaTime.ToLocal(e.AtUtc).Date;
                return day >= filter.From && day <= filter.To;
            });

            if (filter.Module.Length > 0)
            {
                query = query.Where(e => e.Module == filter.Module);
            }

            if (filter.Type.Length > 0)
            {
                query = filter.Type == "failed"
                    ? query.Where(e => e.Type == "failed" || e.IsFailed)
                    : query.Where(e => e.Type == filter.Type);
            }

            if (filter.Search.Length > 0)
            {
                query = query.Where(e =>
                    Has(e.UserName, filter.Search) || Has(e.UserEmail, filter.Search) || Has(e.Action, filter.Search) ||
                    Has(e.Record, filter.Search) || Has(e.RecordId, filter.Search) || Has(e.Id, filter.Search) ||
                    Has(e.Module, filter.Search));
            }

            return query.ToList();
        }


        private static bool Has(string? text, string search)
        {
            return text != null && text.Contains(search, StringComparison.OrdinalIgnoreCase);
        }


        private static bool TryDate(string? text, out DateTime date)
        {
            return DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }


        // "search=x&type=update&module=Roles&from=2026-08-01&to=2026-08-06" (only what was chosen)
        private static string QueryString(ReskAuditFilter filter)
        {
            var parts = new List<string>();

            if (filter.Search.Length > 0)
            {
                parts.Add("search=" + Uri.EscapeDataString(filter.Search));
            }

            if (filter.Type.Length > 0)
            {
                parts.Add("type=" + Uri.EscapeDataString(filter.Type));
            }

            if (filter.Module.Length > 0)
            {
                parts.Add("module=" + Uri.EscapeDataString(filter.Module));
            }

            if (filter.Custom)
            {
                parts.Add("from=" + filter.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                parts.Add("to=" + filter.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            }

            return string.Join("&", parts);
        }


        // "06 Aug, 10:42" (the year is added for older events)
        private static string ShortWhen(DateTime utc, DateTime today)
        {
            DateTime local = SouthAfricaTime.ToLocal(utc);

            return local.ToString(local.Year == today.Year ? "dd MMM, HH:mm" : "dd MMM yyyy, HH:mm", CultureInfo.InvariantCulture);
        }


        // One CSV value: quoted, and never starting a spreadsheet formula.
        private static string Cell(string value)
        {
            string text = (value ?? "").Replace("\r", " ").Replace("\n", " ");

            if (text.Length > 0 && "=+-@".Contains(text[0]))
            {
                text = "'" + text;
            }

            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }


        // Export needs the "Audit log - Export" permission of the admin's role.
        private bool MayExport()
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            ReskRole? role = userId.Length == 0 ? null : ReskRoleStore.RoleFor(Root, userId, new[] { "Admin" });

            return role == null || role.Can("audit", "export");
        }


        private void SetAdminInfo()
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            ProducerProfile profile = userId.Length == 0 ? new ProducerProfile() : ProducerProfileStore.Load(Root, userId);
            bool hasName = !string.IsNullOrWhiteSpace(profile.FullName);

            ViewData["AdminName"] = hasName ? profile.FullName.Trim() : "Admin User";
            ViewData["AdminInitials"] = hasName ? ProducerProfileStore.Initials(profile.FullName) : "AD";
        }
    }
}