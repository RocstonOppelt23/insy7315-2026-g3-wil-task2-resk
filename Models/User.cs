using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace RESK.WIL.Models
{
    //----------User----------//
    public class User : IValidatableObject
    {
        //----------Profile----------//
        // IValidatableObject part

        [Key]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;

        // Profile Information
        public string Phone { get; set; } = string.Empty;
        public string Organisation { get; set; } = string.Empty;
        public string PhysicalAddress { get; set; } = string.Empty;
        public string IdNumber { get; set; } = string.Empty;
        public string? ProfileImageUrl { get; set; }


        //----------Account Setting----------//

        // Language
        public string Language { get; set; } = string.Empty;
        public bool Preference { get; set; }

        // Notifications
        public string ProposalNotification { get; set; } = "Email and In-app";// Email, In-app, Email and In-app, none
        public string ReviewNotification { get; set; } = "Email and In-app";// Email, In-app, Email and In-app, none
        public string DraftNotification { get; set; } = "Email and In-app";// Email, In-app, Email and In-app, none

        //----------Security----------//
        public DateTime? PasswordLastChanged { get; set; }
        public bool IsMfaEnabled { get; set; } 
        public int ActiveSessions { get; set; }

        //----------Account----------//
        public UserAccountStatus AccountStatus { get; set; }
    = UserAccountStatus.Pending;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
        public string PositionChangeRequest { get; set; } = string.Empty;
        // assigned role represents the user role dont forget to add it in the page

        //----------Role and Permissions----------//
        public ICollection<UserRole> UserRoles { get; set; }
            = new List<UserRole>();

        public ICollection<Proposal> Proposals { get; set; }
            = new List<Proposal>();

        //----------Validation----------//
        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(Name))
                yield return Required(nameof(Name));

            if (string.IsNullOrWhiteSpace(LastName))
                yield return Required(nameof(LastName));

            if (string.IsNullOrWhiteSpace(Email))
            {
                yield return Required(nameof(Email));
            }
            else if (!new EmailAddressAttribute().IsValid(Email))
            {
                yield return new ValidationResult(
                    "Enter a valid email address.",
                    new[] { nameof(Email) });
            }

            if (string.IsNullOrWhiteSpace(Phone))
                yield return Required(nameof(Phone));

            if (string.IsNullOrWhiteSpace(PhysicalAddress))
                yield return Required(nameof(PhysicalAddress));

            if (!ValidNotification(ProposalNotification))
                yield return InvalidNotification(nameof(ProposalNotification));

            if (!ValidNotification(ReviewNotification))
                yield return InvalidNotification(nameof(ReviewNotification));

            if (!ValidNotification(DraftNotification))
                yield return InvalidNotification(nameof(DraftNotification));
        }

        private static bool ValidNotification(string? value) =>
            value is "Email" or "InApp" or "Both" or "None";

        private static ValidationResult Required(string propertyName) =>
            new($"{propertyName} is required.", new[] { propertyName });

        private static ValidationResult InvalidNotification(string propertyName) =>
            new(
                "Select Email, InApp, Both, or None.",
                new[] { propertyName });
    }


    //----------Enums----------//
    public enum UserAccountStatus
    {
        Pending = 1,
        Active = 2,
        Inactive = 3,
        Disabled = 4
    }

    public enum AccessScope
    {
        Own = 1,
        SelectedRoles = 2,
        All = 3
    }

    public enum RoleScopeArea
    {
        Users = 1,
        Proposals = 2,
        Reports = 3,
        Audit = 4,
        Roles = 5,
        Settings = 6,
        ProposalAssignment = 7
    }


    //----------Role and Permissions----------//
    public class Role : IValidatableObject
    {
        //----------Instructions----------//
        // Enabled 

        //----------Role Details----------//
        [Key]
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;

        //----------Users----------//
        public bool UsersView { get; set; }
        public bool UsersEdit { get; set; }
        public bool UsersDelete { get; set; }
        public bool UsersApprove { get; set; }
        public AccessScope UsersScope { get; set; } = AccessScope.Own;

        //----------Proposals----------//
        public bool ProposalsView { get; set; }
        public bool ProposalsCreate { get; set; }
        public bool ProposalsEdit { get; set; }
        public bool ProposalsDelete { get; set; }
        public bool ProposalsAssign { get; set; }
        public bool ProposalsApprove { get; set; }
        public bool ProposalsExport { get; set; } // View must be on
        public AccessScope ProposalsScope { get; set; } = AccessScope.Own;

        // Scope for who this role can assign proposals to.
        public AccessScope ProposalAssignmentScope { get; set; }
            = AccessScope.SelectedRoles;

        //----------Reports----------//
        public bool ReportsView { get; set; }
        public bool ReportsExport { get; set; }  // View must be on
        public AccessScope ReportsScope { get; set; } = AccessScope.Own;

        //----------Audit Log----------//
        public bool AuditView { get; set; }
        public bool AuditExport { get; set; }  // View must be on
        public AccessScope AuditScope { get; set; } = AccessScope.Own;

        //----------Roles and Permissions----------//
        public bool RolesView { get; set; }
        public bool RolesCreate { get; set; }
        public bool RolesEdit { get; set; }
        public bool RolesDelete { get; set; }
        public AccessScope RolesScope { get; set; } = AccessScope.Own;

        //----------Settings----------//
        public bool SettingsView { get; set; }
        public bool SettingsEdit { get; set; }
        public AccessScope SettingsScope { get; set; } = AccessScope.Own;

        //----------Relationships----------//
        public ICollection<UserRole> UserRoles { get; set; }
            = new List<UserRole>();

        public ICollection<RoleScopeTarget> ScopeTargets { get; set; }
            = new List<RoleScopeTarget>();

        //----------Validation----------//
        public IEnumerable<ValidationResult> Validate(
       ValidationContext validationContext)
        {
            bool hasPermission =
            UsersView || UsersEdit || UsersDelete || UsersApprove ||
            ProposalsView || ProposalsCreate || ProposalsEdit ||
            ProposalsDelete || ProposalsAssign || ProposalsApprove ||
            ProposalsExport ||
            ReportsView || ReportsExport ||
            AuditView || AuditExport ||
            RolesView || RolesCreate || RolesEdit || RolesDelete ||
            SettingsView || SettingsEdit;

            if (string.IsNullOrWhiteSpace(Title))
            {
                yield return new ValidationResult(
                "Role title is required.",
                new[] { nameof(Title) });
            }

            if (!hasPermission)
            {
                yield return new ValidationResult(
                    "Select at least one permission.");
            }

            if (ReportsExport && !ReportsView)
            {
                yield return new ValidationResult(
                    "Export reports requires View reports.",
                    new[] { nameof(ReportsExport), nameof(ReportsView) });
            }

            if (ProposalsExport && !ProposalsView)
                yield return new ValidationResult(
                    "Export proposals requires View proposals.",
                    new[] { nameof(ProposalsExport), nameof(ProposalsView) });

            if (AuditExport && !AuditView)
                yield return new ValidationResult(
                    "Export audit logs requires View audit logs.",
                    new[] { nameof(AuditExport), nameof(AuditView) });
        }
    }

    // Used when a section's scope is SelectedRoles.
    // Example: Reviewer can see proposals submitted by users in
    // the Producer role.
    public class RoleScopeTarget
    {
        [Key]
        public int Id { get; set; }
        public int RoleId { get; set; }
        public Role Role { get; set; } = null!;

        public RoleScopeArea Area { get; set; }

        public int TargetRoleId { get; set; }
        public Role TargetRole { get; set; } = null!;
    }

    // One user may have multiple admin-created access roles.
    public class UserRole
    {
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public int RoleId { get; set; }
        public Role Role { get; set; } = null!;

        public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;
        public int? AssignedByUserId { get; set; }
    }
    //----------Constructor----------//
}
