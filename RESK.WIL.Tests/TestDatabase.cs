using System;
using Microsoft.EntityFrameworkCore;
using RESK.WIL.Data;
using RESK.WIL.Services;
using Xunit;

namespace RESK.WIL.Tests
{
    // Tests that use the static ReskDb share one collection so they never run at the same time.
    [CollectionDefinition(Name)]
    public class DatabaseCollection
    {
        public const string Name = "Database";
    }


    // Gives each test its own empty in-memory database.
    public sealed class TestDatabase : IDbContextFactory<ApplicationDbContext>
    {
        private readonly DbContextOptions<ApplicationDbContext> _options;

        public const string Root = "test-content-root";


        public TestDatabase()
        {
            _options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("resk-tests-" + Guid.NewGuid())
                .Options;

            ReskDb.Configure(this);
            ReskRoleStore.ResetCache();
            ReskAuditLog.ResetCache();
        }


        public ApplicationDbContext CreateDbContext() => new ApplicationDbContext(_options);
    }
}
