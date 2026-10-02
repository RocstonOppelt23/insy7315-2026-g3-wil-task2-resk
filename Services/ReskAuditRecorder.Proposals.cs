using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using RESK.WIL.Models;

namespace RESK.WIL.Services
{
    // ReskAuditRecorder: proposals, and the small helpers the other parts share.
    public static partial class ReskAuditRecorder
    {
        // =========================================================
        // PROPOSALS
        // =========================================================

        private static async Task ProposalsAsync(HttpContext context, string root, RESK.WIL.Data.ApplicationDbContext db,
            ReskAuditRule rule, Dictionary<int, ReskAuditState> before, bool ok)
        {
            Dictionary<int, ReskAuditState> after =
                await ReskAuditSnapshots.ProposalsAsync(db, root, rule.OwnOnly ? UserId(context) : null);

            int recorded = 0;

            foreach (KeyValuePair<int, ReskAuditState> pair in after)
            {
                before.TryGetValue(pair.Key, out ReskAuditState? was);
                ReskAuditState now = pair.Value;
                (string Action, string Type)? name = ProposalAction(was, now);

                if (name == null)
                {
                    continue;
                }

                ReskAuditEvent item = ReskAuditMiddleware.NewEvent(context, root, "Proposals", name.Value.Action, name.Value.Type);
                item.Record = now.Name;
                item.RecordId = now.IdLabel;
                item.Changes = ReskAuditSnapshots.Diff(was, now);
                item.Summary = $"{name.Value.Action}. The status is now \"{now.Get("Status")}\".";
                ReskAuditLog.Add(root, item);
                recorded++;
            }

            foreach (KeyValuePair<int, ReskAuditState> pair in before.Where(b => !after.ContainsKey(b.Key)))
            {
                bool draft = pair.Value.Status == ProposalStatuses.Draft;

                ReskAuditEvent item = ReskAuditMiddleware.NewEvent(context, root, "Proposals", draft ? "Deleted draft proposal" : "Deleted proposal", "delete");
                item.Record = pair.Value.Name;
                item.RecordId = pair.Value.IdLabel;
                item.Summary = "The proposal was removed.";
                ReskAuditLog.Add(root, item);
                recorded++;
            }

            // A save that changed nothing the log compares (for example review notes).
            if (recorded == 0 && ok && rule.Action.Length > 0)
            {
                ReskAuditEvent item = ReskAuditMiddleware.NewEvent(context, root, rule.Module, rule.Action, rule.Type);

                if (int.TryParse(rule.Key, out int id) && after.TryGetValue(id, out ReskAuditState? target))
                {
                    item.Record = target.Name;
                    item.RecordId = target.IdLabel;
                }
                else
                {
                    item.Record = item.Source;
                }

                ReskAuditLog.Add(root, item);
            }
        }


        private static (string Action, string Type)? ProposalAction(ReskAuditState? was, ReskAuditState now)
        {
            if (was == null)
            {
                return now.Status == ProposalStatuses.Draft ? null : ("Submitted proposal", "submit");
            }

            if (was.Status != now.Status)
            {
                return now.Status switch
                {
                    ProposalStatuses.InReview => (was.Status == ProposalStatuses.ChangesRequested ? "Resubmitted proposal" : "Submitted proposal", "submit"),
                    ProposalStatuses.Approved => ("Approved proposal", "approve"),
                    ProposalStatuses.Rejected => ("Rejected proposal", "reject"),
                    ProposalStatuses.ChangesRequested => ("Requested changes to proposal", "reject"),
                    ProposalStatuses.Draft => ("Returned proposal to draft", "update"),
                    _ => ("Changed proposal status", "update")
                };
            }

            if (was.Get("Reviewer") != now.Get("Reviewer"))
            {
                return (was.Get("Reviewer") == "\u2014" ? "Assigned reviewer" : "Reassigned reviewer", "assign");
            }

            if (was.Get("Reviewer recommendation") != now.Get("Reviewer recommendation"))
            {
                return ("Submitted review recommendation", "submit");
            }

            if (was.Get("Final decision") != now.Get("Final decision"))
            {
                return ("Recorded final decision", "approve");
            }

            if (was.Get("Review deadline") != now.Get("Review deadline"))
            {
                return ("Changed review deadline", "update");
            }

            return null;
        }


        // =========================================================
        // SMALL HELPERS
        // =========================================================

        private static string UserId(HttpContext context)
        {
            return context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        }


        // The categories file is created by the Categories page; don't create it from here.
        private static bool CategoriesExist(string root)
        {
            return File.Exists(Path.Combine(root, "App_Data", "Categories.json"));
        }


        // A posted form value, only when the page already read the form.
        private static string? FormValue(HttpContext context, params string[] keys)
        {
            IFormCollection? form = context.Features.Get<IFormFeature>()?.Form;

            if (form == null)
            {
                return null;
            }

            foreach (string key in keys)
            {
                if (form.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    return ReskAuditMiddleware.Shorten(value.ToString().Trim(), 120);
                }
            }

            return null;
        }


        private static void AddFormChange(ReskAuditEvent item, HttpContext context, string field, string key)
        {
            string? value = FormValue(context, key);

            if (value != null)
            {
                item.Changes.Add(new ReskAuditChange { Field = field, Before = "\u2014", After = value });
            }
        }


        // True when the page reported an error to show after the redirect.
        private static bool HasTempError(HttpContext context)
        {
            ITempDataDictionaryFactory? factory = context.RequestServices.GetService<ITempDataDictionaryFactory>();

            if (factory == null)
            {
                return false;
            }

            try
            {
                ITempDataDictionary data = factory.GetTempData(context);

                return data.Keys.ToList().Any(key =>
                    key.Contains("Error", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(data.Peek(key) as string));
            }
            catch (InvalidOperationException)
            {
                // TempData isn't available for this request.
                return false;
            }
        }
    }
}