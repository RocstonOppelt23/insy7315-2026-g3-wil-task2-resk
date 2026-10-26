using Microsoft.EntityFrameworkCore;
using RESK.WIL.Services;

namespace RESK.WIL.Data
{
    /*
     * =========================================================
     * DATABASE SETUP AT STARTUP
     * =========================================================
     *
     * 1. Renamed migrations: databases that already ran a migration
     *    under its old name get the history row renamed, so EF does
     *    not try to run it a second time.
     *
     * 2. Applies any pending migrations (can be switched off with
     *    "Database:MigrateOnStartup": false).
     *
     * 3. Copies data that older versions kept in App_Data JSON files
     *    into the database (only once - see ReskJsonImport).
     */
    public static class ReskDatabaseSetup
    {
        // Old migration id -> new migration id.
        public static readonly (string OldId, string NewId)[] RenamedMigrations =
        {
            ("20261003162748_AddEthanTables",
             "20261003162748_AddMfaCommentsReviewsAndFeatureSettings")
        };


        public static async Task RunAsync(WebApplication app)
        {
            using IServiceScope scope = app.Services.CreateScope();

            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var logger = scope.ServiceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("RESK.WIL.DatabaseSetup");

            if (db.Database.IsSqlServer() &&
                app.Configuration.GetValue("Database:MigrateOnStartup", true))
            {
                await FixRenamedMigrationsAsync(db);

                IEnumerable<string> pending = await db.Database.GetPendingMigrationsAsync();

                if (pending.Any())
                {
                    logger.LogInformation(
                        "Applying {Count} database migration(s): {Migrations}",
                        pending.Count(),
                        string.Join(", ", pending));

                    await db.Database.MigrateAsync();
                }
            }

            ReskJsonImport.ImportAll(app.Environment.ContentRootPath, db, logger);
        }


        internal static async Task FixRenamedMigrationsAsync(ApplicationDbContext db)
        {
            foreach ((string oldId, string newId) in RenamedMigrations)
            {
                await db.Database.ExecuteSqlRawAsync(
                    "IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NOT NULL " +
                    "UPDATE [__EFMigrationsHistory] SET [MigrationId] = {0} WHERE [MigrationId] = {1}",
                    newId,
                    oldId);
            }
        }
    }
}
