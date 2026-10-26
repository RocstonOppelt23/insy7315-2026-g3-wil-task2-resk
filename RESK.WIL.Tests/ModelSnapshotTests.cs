using System.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using RESK.WIL.Data;
using Xunit;

namespace RESK.WIL.Tests
{
    public class ModelSnapshotTests
    {
        // Fails if someone changes an entity without adding a migration,
        // which would leave the database and the code out of step.
        // No database connection is needed.
        [Fact]
        public void Migrations_snapshot_matches_the_current_model()
        {
            using var db = CreateContext();

            IModel? snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot?.Model;
            Assert.NotNull(snapshot);

            if (snapshot is IMutableModel mutable)
            {
                snapshot = mutable.FinalizeModel();
            }

            snapshot = db.GetService<IModelRuntimeInitializer>().Initialize(snapshot);

            var differences = db.GetService<IMigrationsModelDiffer>().GetDifferences(
                snapshot.GetRelationalModel(),
                db.GetService<IDesignTimeModel>().Model.GetRelationalModel());

            Assert.True(
                differences.Count == 0,
                "Model has changes with no migration: " +
                string.Join("; ", differences.Select(Describe)));
        }


        [Fact]
        public void Renamed_migration_is_registered_under_its_new_name()
        {
            using var db = CreateContext();

            var ids = db.Database.GetMigrations().ToList();

            foreach ((string oldId, string newId) in ReskDatabaseSetup.RenamedMigrations)
            {
                Assert.Contains(newId, ids);
                Assert.DoesNotContain(oldId, ids);
            }
        }


        // Same model the app builds: AddDefaultIdentity limits Identity key columns to 128 characters.
        private static ApplicationDbContext CreateContext()
        {
            var services = new ServiceCollection();
            services.Configure<IdentityOptions>(o => o.Stores.MaxLengthForKeys = 128);

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer("Server=localhost;Database=model-check;Trusted_Connection=True")
                .UseApplicationServiceProvider(services.BuildServiceProvider())
                .Options;

            return new ApplicationDbContext(options);
        }


        // e.g. "AddColumnOperation ReskRoles.SortOrder"
        private static string Describe(MigrationOperation op)
        {
            string? table = op.GetType().GetProperty("Table")?.GetValue(op) as string;
            string? name = op.GetType().GetProperty("Name")?.GetValue(op) as string;

            return $"{op.GetType().Name} {string.Join(".", new[] { table, name }.Where(x => !string.IsNullOrEmpty(x)))}";
        }
    }
}
