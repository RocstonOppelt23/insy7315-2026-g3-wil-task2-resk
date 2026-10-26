using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
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
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer("Server=localhost;Database=model-check;Trusted_Connection=True")
                .Options;

            using var db = new ApplicationDbContext(options);

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
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer("Server=localhost;Database=model-check;Trusted_Connection=True")
                .Options;

            using var db = new ApplicationDbContext(options);

            var ids = db.Database.GetMigrations().ToList();

            foreach ((string oldId, string newId) in ReskDatabaseSetup.RenamedMigrations)
            {
                Assert.Contains(newId, ids);
                Assert.DoesNotContain(oldId, ids);
            }
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
