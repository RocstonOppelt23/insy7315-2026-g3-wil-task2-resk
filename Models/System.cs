using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;
namespace WIL.Models
{
    //----------System----------//
    public class SystemFeatureSettings
    {
        [Key]
        public int Id { get; set; }

        public bool UsersEnabled { get; set; } = true;
        public bool ProposalsEnabled { get; set; } = true;
        public bool ReportsEnabled { get; set; } = true;
        public bool AuditEnabled { get; set; } = true;
        public bool RolesEnabled { get; set; } = true;
        public bool SettingsEnabled { get; set; } = true;
    }

    //----------MfaCode----------//
    public class MfaCode
    {
        [Key]
        public int Id { get; set; }
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        // Store a hash of the code, never the code itself.
        public string CodeHash { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public DateTime? UsedAtUtc { get; set; }

        public int FailedAttempts { get; set; }

        public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;
        public bool IsUsed => UsedAtUtc.HasValue;
    }
    //----------Constructor----------//
}
