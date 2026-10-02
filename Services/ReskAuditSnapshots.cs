namespace RESK.WIL.Services
{
    // The values of one record at one moment (compared before / after a request).
    public class ReskAuditState
    {
        public string Name { get; set; } = string.Empty;

        // "Role ID: reviewer"
        public string IdLabel { get; set; } = string.Empty;

        // Raw status, used to name the action ("Approved proposal").
        public string Status { get; set; } = string.Empty;

        public List<KeyValuePair<string, string>> Fields { get; } = new();

        public void Set(string field, string? value)
        {
            Fields.Add(new KeyValuePair<string, string>(field, string.IsNullOrWhiteSpace(value) ? "\u2014" : value.Trim()));
        }

        public string Get(string field)
        {
            return Fields.FirstOrDefault(f => f.Key == field).Value ?? "\u2014";
        }
    }


    /*
     * =========================================================
     * AUDIT SNAPSHOTS
     * =========================================================
     *
     * The audit middleware reads a record before the request and
     * again after it. The difference is the "Change summary" shown
     * on the Audit event details page.
     */
    public static partial class ReskAuditSnapshots
    {
        // Field / previous value / new value, for the fields that changed.
        public static List<ReskAuditChange> Diff(ReskAuditState? before, ReskAuditState? after)
        {
            var changes = new List<ReskAuditChange>();

            if (before == null && after == null)
            {
                return changes;
            }

            IEnumerable<string> fields =
                (before?.Fields.Select(f => f.Key) ?? Enumerable.Empty<string>())
                .Concat(after?.Fields.Select(f => f.Key) ?? Enumerable.Empty<string>())
                .Distinct();

            foreach (string field in fields)
            {
                string was = before?.Get(field) ?? "\u2014";
                string now = after?.Get(field) ?? "\u2014";

                // A new record: only list what was filled in or allowed.
                if (before == null && (now == "\u2014" || now == "Denied" || now == "No"))
                {
                    continue;
                }

                if (was != now)
                {
                    changes.Add(new ReskAuditChange { Field = field, Before = was, After = now });
                }
            }

            return changes;
        }


        // =========================================================
        // ROLE
        // =========================================================

        public static ReskAuditState? Role(string root, string id)
        {
            ReskRole? role = ReskRoleStore.Get(root, id);
            return role == null ? null : Role(role);
        }


        public static ReskAuditState Role(ReskRole role)
        {
            var state = new ReskAuditState { Name = role.Name, IdLabel = "Role ID: " + role.Id, Status = role.Status };

            state.Set("Role name", role.Name);
            state.Set("Status", role.Status);
            state.Set("Description", role.Description);
            state.Set("Access area", ReskRoleStore.AreaLabel(role.Area));

            foreach (var module in ReskRoleStore.Modules)
            {
                foreach (var action in ReskRoleStore.Actions.Where(a => module.Actions.Contains(a.Key)))
                {
                    state.Set(module.Name + " \u2014 " + action.Label, role.Can(module.Key, action.Key) ? "Allowed" : "Denied");
                }
            }

            return state;
        }


        // =========================================================
        // CATEGORY
        // =========================================================

        public static ReskAuditState? Category(string root, string id)
        {
            ReskCategory? category = int.TryParse(id, out int number) ? ReskCategoryStore.Get(root, number) : null;
            return category == null ? null : Category(category);
        }


        public static ReskAuditState Category(ReskCategory category)
        {
            var state = new ReskAuditState
            {
                Name = category.Name,
                IdLabel = "Category code: " + (string.IsNullOrWhiteSpace(category.Code) ? category.Id.ToString() : category.Code),
                Status = category.Status
            };

            state.Set("Category name", category.Name);
            state.Set("Code", category.Code);
            state.Set("Description", category.Description);
            state.Set("Status", category.Status);
            state.Set("Display order", category.DisplayOrder.ToString());
            state.Set("Icon colour", category.IconColour);
            state.Set("Shown on the proposal form", category.ShowOnForm ? "Yes" : "No");
            state.Set("Requires supporting documents", category.RequireSupportingDocuments ? "Yes" : "No");

            return state;
        }
    }
}