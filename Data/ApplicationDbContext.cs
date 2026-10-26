using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using RESK.WIL.Models;
using RESK.WIL.Services;

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

        // Data the admin screens used to keep in App_Data JSON files.
        public DbSet<Services.ProposalReview> ReskProposalReviews
            => Set<Services.ProposalReview>();

        public DbSet<ReskAccount> ReskAccounts => Set<ReskAccount>();

        public DbSet<ReskActivity> ReskAccountActivities => Set<ReskActivity>();

        public DbSet<ReskRole> ReskRoles => Set<ReskRole>();

        public DbSet<ReskRoleAssignment> ReskRoleAssignments
            => Set<ReskRoleAssignment>();

        public DbSet<ReskSystemValue> ReskSystemValues => Set<ReskSystemValue>();

        public DbSet<ReskAuditEvent> ReskAuditEvents => Set<ReskAuditEvent>();

        public DbSet<ReskAuditChange> ReskAuditChanges => Set<ReskAuditChange>();

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
            // Check constraints stop bad values reaching the table
            // even if a bug skips the validation in the app.
            modelBuilder.Entity<ProducerProposal>()
                .ToTable("ProducerProposals", t =>
                {
                    t.HasCheckConstraint(
                        "CK_ProducerProposals_Status",
                        "[Status] IN ('Draft', 'InReview', 'Approved', 'ChangesRequested', 'Rejected')");

                    t.HasCheckConstraint(
                        "CK_ProducerProposals_CompletedSteps",
                        "[CompletedSteps] BETWEEN 0 AND 5");

                    t.HasCheckConstraint(
                        "CK_ProducerProposals_SubmittedReference",
                        "[Status] = 'Draft' OR [SubmittedAtUtc] IS NOT NULL OR [Reference] IS NULL");
                });

            modelBuilder.Entity<ProducerProposal>()
                .HasIndex(p => new { p.OwnerUserId, p.Status });

            modelBuilder.Entity<ProducerProposal>()
                .HasIndex(p => p.Reference)
                .IsUnique()
                .HasFilter("[Reference] IS NOT NULL");

            ConfigureReskTables(modelBuilder);
        }


        // =====================================================
        // RESK ADMIN DATA (moved out of App_Data JSON files)
        // =====================================================

        private static void ConfigureReskTables(ModelBuilder modelBuilder)
        {
            // ---------- Proposal reviews ----------
            modelBuilder.Entity<Services.ProposalReview>(b =>
            {
                b.ToTable("ReskProposalReviews", t => t.HasCheckConstraint(
                    "CK_ReskProposalReviews_Recommendation",
                    "[Recommendation] IS NULL OR [Recommendation] IN ('Approve', 'RequestChanges', 'Reject')"));

                b.HasKey(r => r.ProposalId);
                b.Property(r => r.ProposalId).ValueGeneratedNever();
                b.Property(r => r.ReviewerUserId).HasMaxLength(450);
                b.Property(r => r.ReviewerName).HasMaxLength(256);
                b.Property(r => r.AssignedBy).HasMaxLength(256);
                b.Property(r => r.Recommendation).HasMaxLength(30);
                b.Property(r => r.Decision).HasMaxLength(30);
                b.Property(r => r.DecidedBy).HasMaxLength(256);
                b.HasIndex(r => r.ReviewerUserId);
            });

            // ---------- Accounts + activity history ----------
            modelBuilder.Entity<ReskAccount>(b =>
            {
                b.ToTable("ReskAccounts", t => t.HasCheckConstraint(
                    "CK_ReskAccounts_Status",
                    "[Status] IN ('', 'Pending', 'Active', 'Suspended', 'Rejected', 'Banned')"));

                b.HasKey(a => a.UserId);
                b.Property(a => a.UserId).HasMaxLength(450);
                b.Property(a => a.Status).HasMaxLength(20);
                b.Property(a => a.RestrictedBy).HasMaxLength(256);
                b.Property(a => a.RequestedRole).HasMaxLength(50);

                b.HasMany(a => a.Activity)
                    .WithOne()
                    .HasForeignKey(x => x.AccountUserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ReskActivity>(b =>
            {
                b.ToTable("ReskAccountActivities", t => t.HasCheckConstraint(
                    "CK_ReskAccountActivities_Kind",
                    "[Kind] IN ('info', 'success', 'warning', 'danger')"));

                b.HasKey(x => x.Id);
                b.Property(x => x.AccountUserId).HasMaxLength(450);
                b.Property(x => x.Title).HasMaxLength(300);
                b.Property(x => x.Kind).HasMaxLength(20);
                b.HasIndex(x => new { x.AccountUserId, x.AtUtc });
            });

            // ---------- Roles, assignments, Administrator ----------
            var permissionsComparer = new ValueComparer<List<string>>(
                (a, b) => (a == null && b == null) || (a != null && b != null && a.SequenceEqual(b)),
                list => list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
                list => list.ToList());

            modelBuilder.Entity<ReskRole>(b =>
            {
                b.ToTable("ReskRoles", t =>
                {
                    t.HasCheckConstraint(
                        "CK_ReskRoles_Status",
                        "[Status] IN ('Active', 'Inactive', 'Draft')");

                    t.HasCheckConstraint(
                        "CK_ReskRoles_Area",
                        "[Area] IN ('Admin', 'Reviewer', 'Producer')");
                });

                b.HasKey(r => r.Id);
                b.Property(r => r.Id).HasMaxLength(50).ValueGeneratedNever();
                b.Property(r => r.Name).HasMaxLength(200);
                b.Property(r => r.Description).HasMaxLength(1000);
                b.Property(r => r.Status).HasMaxLength(20);
                b.Property(r => r.Area).HasMaxLength(20);
                b.Property(r => r.Template).HasMaxLength(30);
                b.Property(r => r.CreatedBy).HasMaxLength(256);
                b.Property(r => r.UpdatedBy).HasMaxLength(256);

                // "users:view,proposals:edit,..." in one column.
                b.Property(r => r.Permissions)
                    .HasConversion(
                        list => string.Join(",", list),
                        text => text.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
                        permissionsComparer);
            });

            modelBuilder.Entity<ReskRoleAssignment>(b =>
            {
                b.ToTable("ReskRoleAssignments");
                b.HasKey(a => a.UserId);
                b.Property(a => a.UserId).HasMaxLength(450);
                b.Property(a => a.RoleId).HasMaxLength(50);
                b.HasIndex(a => a.RoleId);
            });

            modelBuilder.Entity<ReskSystemValue>(b =>
            {
                b.ToTable("ReskSystemValues");
                b.HasKey(v => v.Key);
                b.Property(v => v.Key).HasMaxLength(100);
            });

            // ---------- Audit log ----------
            modelBuilder.Entity<ReskAuditEvent>(b =>
            {
                b.ToTable("ReskAuditEvents", t => t.HasCheckConstraint(
                    "CK_ReskAuditEvents_Result",
                    "[Result] IN ('Successful', 'Failed')"));

                b.HasKey(e => e.Id);
                b.Property(e => e.Id).HasMaxLength(30).ValueGeneratedNever();
                b.Property(e => e.Action).HasMaxLength(300);
                b.Property(e => e.Type).HasMaxLength(20);
                b.Property(e => e.Module).HasMaxLength(50);
                b.Property(e => e.UserId).HasMaxLength(450);
                b.Property(e => e.UserName).HasMaxLength(256);
                b.Property(e => e.UserEmail).HasMaxLength(256);
                b.Property(e => e.UserRole).HasMaxLength(100);
                b.Property(e => e.Source).HasMaxLength(100);
                b.Property(e => e.SessionId).HasMaxLength(100);
                b.Property(e => e.Ip).HasMaxLength(64);
                b.Property(e => e.Result).HasMaxLength(20);

                b.HasIndex(e => e.AtUtc);
                b.HasIndex(e => e.Module);

                b.HasMany(e => e.Changes)
                    .WithOne()
                    .HasForeignKey(c => c.EventId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ReskAuditChange>(b =>
            {
                b.ToTable("ReskAuditChanges");
                b.HasKey(c => c.Id);
                b.Property(c => c.EventId).HasMaxLength(30);
                b.Property(c => c.Field).HasMaxLength(200);
            });
        }
    }
}
