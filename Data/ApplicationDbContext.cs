using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RESK.WIL.Models;

namespace RESK.WIL.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        public DbSet<User> Users => Set<User>();
        public DbSet<Proposal> Proposals => Set<Proposal>();
        public DbSet<Role> Roles => Set<Role>();
        public DbSet<UserRole> UserRoles => Set<UserRole>();
        public DbSet<RoleScopeTarget> RoleScopeTargets
            => Set<RoleScopeTarget>();
        public DbSet<SystemFeatureSettings> SystemFeatureSettings => Set<SystemFeatureSettings>();
        public DbSet<MfaCode> MfaCodes => Set<MfaCode>();
        public DbSet<ProposalComment> ProposalComments => Set<ProposalComment>();
        public DbSet<ProposalReview> ProposalReviews => Set<ProposalReview>();

        // Proposals created through the producer "New proposal"
        // wizard, including drafts.
        public DbSet<ProducerProposal> ProducerProposals
            => Set<ProducerProposal>();

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

            // One row per user-role combination.
            modelBuilder.Entity<UserRole>()
                .HasKey(ur => new { ur.UserId, ur.RoleId });

            modelBuilder.Entity<UserRole>()
                .HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId);

            modelBuilder.Entity<UserRole>()
                .HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId);

            // These two FKs both point to Role, so configure them separately.
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

            // Producer wizard proposals: each producer only ever
            // loads their own rows, usually filtered by status.
            modelBuilder.Entity<ProducerProposal>()
                .ToTable("ProducerProposals");

            modelBuilder.Entity<ProducerProposal>()
                .HasIndex(p => new { p.OwnerUserId, p.Status });

            modelBuilder.Entity<ProducerProposal>()
                .HasIndex(p => p.Reference)
                .IsUnique()
                .HasFilter("[Reference] IS NOT NULL");
        }
    }
}
