using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RESK.WIL.Models;

namespace RESK.WIL.Data
{
    public class ApplicationDbContext : IdentityDbContext<User, IdentityRole<int>, int>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Proposal> Proposals => Set<Proposal>();
        public DbSet<ProposalComment> ProposalComments => Set<ProposalComment>();
        public DbSet<ProposalReview> ProposalReviews => Set<ProposalReview>();
        public DbSet<AccessRole> AccessRoles => Set<AccessRole>();
        public DbSet<RoleScopeTarget> RoleScopeTargets => Set<RoleScopeTarget>();
        public DbSet<SystemFeatureSettings> SystemFeatureSettings
            => Set<SystemFeatureSettings>();
        public DbSet<MfaCode> MfaCodes => Set<MfaCode>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Proposal.Id is its PK.
            // Proposal.ProducerId references User.Id.
            modelBuilder.Entity<Proposal>()
                .HasOne(p => p.Producer)
                .WithMany(u => u.Proposals)
                .HasForeignKey(p => p.ProducerId)
                .OnDelete(DeleteBehavior.Restrict);

            // One user can only have one custom access role at a time.
            modelBuilder.Entity<User>()
                .HasOne(u => u.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleId)
                .OnDelete(DeleteBehavior.SetNull);

            // These two FKs both point to AccessRole, so configure them separately.
            modelBuilder.Entity<RoleScopeTarget>()
                .HasOne(t => t.Role)
                .WithMany(r => r.ScopeTargets)
                .HasForeignKey(t => t.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<RoleScopeTarget>()
                .HasOne(t => t.TargetRole)
                .WithMany()
                .HasForeignKey(t => t.TargetRoleId)
                .OnDelete(DeleteBehavior.Restrict);

            // Avoid adding the same target role twice for one section.
            modelBuilder.Entity<RoleScopeTarget>()
                .HasIndex(t => new { t.RoleId, t.Area, t.TargetRoleId })
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();
        }
    }
}
