using System.Text.Json.Serialization;

namespace RESK.WIL.Services
{
    // One changed field on the "Audit event details" page.
    public class ReskAuditChange
    {
        public string Field { get; set; } = string.Empty;

        public string Before { get; set; } = string.Empty;

        public string After { get; set; } = string.Empty;
    }


    public class ReskAuditEvent
    {
        // AUD-2026-00482
        public string Id { get; set; } = string.Empty;

        public DateTime AtUtc { get; set; } = DateTime.UtcNow;

        // "Updated role permissions"
        public string Action { get; set; } = string.Empty;

        // create, update, delete, submit, approve, reject, assign, export, signin, failed
        public string Type { get; set; } = "update";

        // Proposals, Users, Roles, Categories, Reports, Settings, Security
        public string Module { get; set; } = string.Empty;

        // ---------- who ----------
        public string? UserId { get; set; }

        public string UserName { get; set; } = "Unknown user";

        public string? UserEmail { get; set; }

        public string? UserRole { get; set; }

        // ---------- what ----------
        public string Record { get; set; } = string.Empty;

        // "Role ID: proposal-manager"
        public string? RecordId { get; set; }

        public string? Summary { get; set; }

        public List<ReskAuditChange> Changes { get; set; } = new();

        // ---------- technical ----------
        public string Source { get; set; } = "Web application";

        public string? SessionId { get; set; }

        public string? Ip { get; set; }

        public string? Device { get; set; }

        // Successful or Failed
        public string Result { get; set; } = ReskAuditLog.Successful;

        // "POST /Admin/Roles/reviewer/Save"
        public string? Request { get; set; }

        [JsonIgnore]
        public bool IsFailed => Result == ReskAuditLog.Failed;

        [JsonIgnore]
        public bool IsRoleChange => Module == "Roles" || Action == "Changed user role";
    }
}