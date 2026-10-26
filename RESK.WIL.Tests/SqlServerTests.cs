using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using RESK.WIL.Data;
using RESK.WIL.Models;
using RESK.WIL.Services;
using Xunit;

namespace RESK.WIL.Tests
{
    /*
     * Runs against a REAL SQL Server, so the migrations, check
     * constraints and column sizes are tested exactly as they run
     * on Azure.
     *
     * The CI pipeline starts a SQL Server container and sets
     * RESK_TEST_SQLSERVER. Without it (e.g. on a laptop) these
     * tests show as skipped.
     */
    public sealed class SqlServerFactAttribute : FactAttribute
    {
        public const string Variable = "RESK_TEST_SQLSERVER";

        public SqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Variable)))
            {
                Skip = $"Set {Variable} to a SQL Server connection string to run this test.";
            }
        }
    }


    // A new, empty database on the test server; dropped afterwards.
    public sealed class SqlServerDatabase : IDbContextFactory<ApplicationDbContext>, IDisposable
    {
        public string ConnectionString { get; }


        public SqlServerDatabase()
        {
            var builder = new SqlConnectionStringBuilder(
                Environment.GetEnvironmentVariable(SqlServerFactAttribute.Variable))
            {
                InitialCatalog = "resk_ci_" + Guid.NewGuid().ToString("N")
            };

            ConnectionString = builder.ConnectionString;
        }


        public ApplicationDbContext CreateDbContext()
        {
            // Same Identity key length as the real app (AddDefaultIdentity).
            var services = new ServiceCollection();
            services.Configure<IdentityOptions>(o => o.Stores.MaxLengthForKeys = 128);

            return new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseSqlServer(ConnectionString)
                    .UseApplicationServiceProvider(services.BuildServiceProvider())
                    .Options);
        }


        public void Dispose()
        {
            SqlConnection.ClearAllPools();

            using ApplicationDbContext db = CreateDbContext();
            db.Database.EnsureDeleted();
        }
    }


    [Collection(DatabaseCollection.Name)]
    public class SqlServerTests
    {
        private const string BeforeMoveMigration = "20261003162748_AddMfaCommentsReviewsAndFeatureSettings";


        [SqlServerFact]
        public void Every_migration_applies_to_an_empty_database()
        {
            using var server = new SqlServerDatabase();
            using ApplicationDbContext db = server.CreateDbContext();

            db.Database.Migrate();

            Assert.Empty(db.Database.GetPendingMigrations());

            List<string> tables = db.Database
                .SqlQueryRaw<string>("SELECT TABLE_NAME AS [Value] FROM INFORMATION_SCHEMA.TABLES")
                .ToList();

            foreach (string table in new[]
                     {
                         "ProducerProposals", "ReskProposalReviews", "ReskAccounts", "ReskAccountActivities",
                         "ReskRoles", "ReskRoleAssignments", "ReskSystemValues", "ReskAuditEvents", "ReskAuditChanges"
                     })
            {
                Assert.Contains(table, tables);
            }
        }


        [SqlServerFact]
        public void Newest_migration_can_be_rolled_back_and_applied_again()
        {
            using var server = new SqlServerDatabase();
            using ApplicationDbContext db = server.CreateDbContext();

            db.Database.Migrate();

            IMigrator migrator = db.GetService<IMigrator>();
            migrator.Migrate(BeforeMoveMigration);
            Assert.NotEmpty(db.Database.GetPendingMigrations());

            migrator.Migrate();
            Assert.Empty(db.Database.GetPendingMigrations());
        }


        [SqlServerFact]
        public async Task Database_that_ran_AddEthanTables_upgrades_without_errors()
        {
            using var server = new SqlServerDatabase();
            using ApplicationDbContext db = server.CreateDbContext();

            // A database created before the rename: the history row has the old name.
            db.GetService<IMigrator>().Migrate(BeforeMoveMigration);

            foreach ((string oldId, string newId) in ReskDatabaseSetup.RenamedMigrations)
            {
                db.Database.ExecuteSqlRaw(
                    "UPDATE [__EFMigrationsHistory] SET [MigrationId] = {0} WHERE [MigrationId] = {1}",
                    oldId,
                    newId);
            }

            await ReskDatabaseSetup.FixRenamedMigrationsAsync(db);
            await db.Database.MigrateAsync();

            List<string> applied = db.Database.GetAppliedMigrations().ToList();

            Assert.Contains("20261026150000_MoveAdminDataToDatabase", applied);
            Assert.DoesNotContain("20261003162748_AddEthanTables", applied);
            Assert.Empty(db.Database.GetPendingMigrations());
        }


        [SqlServerFact]
        public void Check_constraints_reject_invalid_proposals_and_accounts()
        {
            using var server = new SqlServerDatabase();

            using (ApplicationDbContext db = server.CreateDbContext())
            {
                db.Database.Migrate();
            }

            AssertRejected(server, db => db.ProducerProposals.Add(NewProposal(p => p.Status = "Bogus")), "CK_ProducerProposals_Status");
            AssertRejected(server, db => db.ProducerProposals.Add(NewProposal(p => p.CompletedSteps = 9)), "CK_ProducerProposals_CompletedSteps");
            AssertRejected(server, db => db.ProducerProposals.Add(NewProposal(p =>
            {
                p.Status = ProposalStatuses.InReview;
                p.Reference = "CTV-2026-0001";
                p.SubmittedAtUtc = null;
            })), "CK_ProducerProposals_SubmittedReference");
            AssertRejected(server, db => db.ReskAccounts.Add(new ReskAccount { UserId = "u1", Status = "Weird" }), "CK_ReskAccounts_Status");
            AssertRejected(server, db => db.ReskRoles.Add(new ReskRole { Id = "x", Name = "X", Area = "Nowhere" }), "CK_ReskRoles_Area");

            // A valid proposal is accepted.
            using (ApplicationDbContext db = server.CreateDbContext())
            {
                db.ProducerProposals.Add(NewProposal(p =>
                {
                    p.Status = ProposalStatuses.InReview;
                    p.CompletedSteps = 5;
                    p.Reference = "CTV-2026-0002";
                    p.SubmittedAtUtc = DateTime.UtcNow;
                }));

                db.SaveChanges();
            }
        }


        [SqlServerFact]
        public void Stores_save_and_load_on_SqlServer()
        {
            using var server = new SqlServerDatabase();

            using (ApplicationDbContext db = server.CreateDbContext())
            {
                db.Database.Migrate();
            }

            ReskDb.Configure(server);
            ReskRoleStore.ResetCache();
            ReskAuditLog.ResetCache();

            const string root = "sql-root";

            // Reviews
            ProposalReviewStore.Save(root, new ProposalReview
            {
                ProposalId = 5,
                ReviewerName = "Reviewer",
                Recommendation = ProposalReviewStore.RequestChanges,
                Comments = new string('x', 5000)
            });

            Assert.Equal(5000, ProposalReviewStore.Load(root, 5).Comments!.Length);

            // Accounts + activity
            var account = new ReskAccount { UserId = Guid.NewGuid().ToString(), Status = ReskAccountStore.Pending };
            ReskAccountStore.Log(account, "Registered", "Signed up from the website");
            ReskAccountStore.Save(root, account);

            ReskAccount loaded = ReskAccountStore.Load(root, account.UserId);
            ReskAccountStore.Log(loaded, "Approved", "Approved by admin", "success");
            ReskAccountStore.Save(root, loaded);

            Assert.Equal(2, ReskAccountStore.Load(root, account.UserId).Activity.Count);

            // Roles: the built-in roles (long descriptions, all permissions) fit the columns.
            List<ReskRole> roles = ReskRoleStore.All(root);
            Assert.Contains(roles, r => r.Id == ReskRoleStore.AdministratorId);

            ReskRoleStore.Assign(root, account.UserId, ReskRoleStore.ReviewerId);
            Assert.Equal(ReskRoleStore.ReviewerId, ReskRoleStore.Assignments(root)[account.UserId]);

            // Audit log
            var item = new ReskAuditEvent
            {
                Action = "Updated role permissions",
                Module = "Roles",
                Record = "Role: Reviewer",
                UserName = "Admin",
                Request = "POST /Admin/Roles/reviewer/Save"
            };
            item.Changes.Add(new ReskAuditChange { Field = "Permissions", Before = "a", After = "b" });

            string id = ReskAuditLog.Add(root, item).Id;

            Assert.StartsWith("AUD-", id);
            Assert.Single(ReskAuditLog.Get(root, id)!.Changes);
        }


        [SqlServerFact]
        public async Task App_starts_migrates_and_reports_healthy_on_SqlServer()
        {
            using var server = new SqlServerDatabase();

            string? before = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", server.ConnectionString);

            try
            {
                using var app = new WebApplicationFactory<Program>()
                    .WithWebHostBuilder(b => b.UseEnvironment("Production"));

                HttpClient client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

                HttpResponseMessage health = await client.GetAsync("/health");
                string body = await health.Content.ReadAsStringAsync();

                Assert.True(health.StatusCode == HttpStatusCode.OK, body);
                Assert.Contains("\"status\":\"Healthy\"", body);

                HttpResponseMessage login = await client.GetAsync("/Account/Login");
                Assert.Equal(HttpStatusCode.OK, login.StatusCode);

                using ApplicationDbContext db = server.CreateDbContext();
                Assert.Empty(db.Database.GetPendingMigrations());
            }
            finally
            {
                Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", before);
            }
        }


        private static ProducerProposal NewProposal(Action<ProducerProposal> change)
        {
            var proposal = new ProducerProposal
            {
                OwnerUserId = "owner",
                ProgrammeTitle = "Test programme",
                Category = "Documentary",
                Status = ProposalStatuses.Draft,
                CompletedSteps = 1
            };

            change(proposal);
            return proposal;
        }


        private static void AssertRejected(SqlServerDatabase server, Action<ApplicationDbContext> add, string constraint)
        {
            using ApplicationDbContext db = server.CreateDbContext();
            add(db);

            DbUpdateException error = Assert.Throws<DbUpdateException>(() => db.SaveChanges());
            Assert.Contains(constraint, error.InnerException?.Message);
        }
    }
}
