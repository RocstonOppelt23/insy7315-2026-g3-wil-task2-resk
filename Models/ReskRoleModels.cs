// Models for Roles & permissions and the Edit user screen.

namespace RESK.WIL.Models
{
    public class ReskRoleListItem
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Initials { get; set; } = "";
        public string Colour { get; set; } = "#1d3557";
        public string Status { get; set; } = "Active";
        public string AreaLabel { get; set; } = "";
        public string Tagline { get; set; } = "";
        public int Users { get; set; }
        public bool IsSystem { get; set; }
    }


    public class ReskRolesIndexViewModel
    {
        public List<ReskRoleListItem> Roles { get; set; } = new();
        public int ActiveCount { get; set; }

        // Selected role (right-hand panel)
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Status { get; set; } = "Active";
        public string Description { get; set; } = "";
        public string Area { get; set; } = "Admin";
        public bool IsAdministrator { get; set; }
        public bool IsSystem { get; set; }
        public bool IsMine { get; set; }
        public int UserCount { get; set; }
        public List<string> UserNames { get; set; } = new();
        public List<string> Permissions { get; set; } = new();
        public string UpdatedText { get; set; } = "";

        // "Role created successfully" pop-up
        public string? CreatedId { get; set; }
        public string? CreatedName { get; set; }
    }


    public class ReskRoleSaveForm
    {
        public string? Name { get; set; }
        public string? Status { get; set; }
        public string? Description { get; set; }
        public List<string> Perms { get; set; } = new();
    }


    public class ReskRoleWizardForm
    {
        public int Step { get; set; } = 1;

        // next, back, draft, create, step1, step2
        public string? Go { get; set; }

        public string? DraftId { get; set; }

        public string? Name { get; set; }
        public string? Description { get; set; }
        public string Status { get; set; } = "Active";
        public string Area { get; set; } = "Admin";
        public string Template { get; set; } = "custom";
        public string? AppliedTemplate { get; set; }
        public bool AssignNow { get; set; } = true;
        public List<string> Perms { get; set; } = new();
    }


    public class ReskUserAccessForm
    {
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Enter the user's full name.")]
        [System.ComponentModel.DataAnnotations.StringLength(100)]
        public string FullName { get; set; } = "";

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Enter an email address.")]
        [System.ComponentModel.DataAnnotations.EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public string Email { get; set; } = "";

        [System.ComponentModel.DataAnnotations.StringLength(30)]
        [System.ComponentModel.DataAnnotations.RegularExpression(@"^[0-9+()\s-]*$", ErrorMessage = "Use numbers, spaces, + and - only.")]
        public string? Phone { get; set; }

        [System.ComponentModel.DataAnnotations.StringLength(150)]
        public string? Organisation { get; set; }

        public string RoleId { get; set; } = "";

        // Active, Suspended, Banned (Pending accounts stay Pending)
        public string AccountStatus { get; set; } = "Active";
    }


    public class ReskUserAccessViewModel
    {
        public string UserId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public bool IsSelf { get; set; }
        public bool IsPending { get; set; }
        public string CurrentRoleId { get; set; } = "";
        public string CurrentStatus { get; set; } = "Active";
        public ReskUserAccessForm Form { get; set; } = new();
        public List<(string Id, string Name, string Area, bool Active)> Roles { get; set; } = new();

        // roleId -> lines for "Role permissions preview"
        public Dictionary<string, List<string>> Previews { get; set; } = new();
    }
}