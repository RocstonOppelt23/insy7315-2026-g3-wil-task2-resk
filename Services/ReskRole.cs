namespace RESK.WIL.Services
{
    // One role: its access area and its permission matrix. See ReskRoleStore.
    public class ReskRole
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 8);

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        // Active, Inactive or Draft
        public string Status { get; set; } = ReskRoleStore.Active;

        // Admin, Reviewer or Producer
        public string Area { get; set; } = "Admin";

        // custom, manager, reviewer, viewer
        public string Template { get; set; } = "custom";

        // Built-in roles can't be deleted.
        public bool IsSystem { get; set; }

        // "users:view", "proposals:approve", ...
        public List<string> Permissions { get; set; } = new();

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public string? CreatedBy { get; set; }

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        public string? UpdatedBy { get; set; }

        public bool IsAdministrator => Id == ReskRoleStore.AdministratorId;

        public bool IsActive => Status == ReskRoleStore.Active;

        public bool Can(string module, string action)
        {
            return IsAdministrator || Permissions.Contains(module + ":" + action);
        }
    }
}