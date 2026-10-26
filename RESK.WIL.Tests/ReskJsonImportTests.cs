using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using RESK.WIL.Data;
using RESK.WIL.Services;
using Xunit;

namespace RESK.WIL.Tests
{
    // Old App_Data JSON files are copied into the database once, then kept as ".imported" backups.
    [Collection(DatabaseCollection.Name)]
    public class ReskJsonImportTests : IDisposable
    {
        private readonly string _root =
            Path.Combine(Path.GetTempPath(), "resk-import-" + Guid.NewGuid().ToString("N"));

        private string Data => Path.Combine(_root, "App_Data");


        public ReskJsonImportTests()
        {
            Directory.CreateDirectory(Path.Combine(Data, "Reviews"));
            Directory.CreateDirectory(Path.Combine(Data, "Accounts"));

            File.WriteAllText(Path.Combine(Data, "Reviews", "12.json"),
                "{\"ReviewerUserId\":\"rev-1\",\"ReviewerName\":\"Sipho\",\"Recommendation\":\"Approve\"}");

            File.WriteAllText(Path.Combine(Data, "Accounts", "user-9.json"),
                "{\"UserId\":\"user-9\",\"Status\":\"Active\",\"Activity\":[" +
                "{\"AtUtc\":\"2026-10-02T10:00:00Z\",\"Title\":\"Approved\",\"Detail\":\"x\",\"Kind\":\"success\"}," +
                "{\"AtUtc\":\"2026-10-01T10:00:00Z\",\"Title\":\"Registered\",\"Detail\":\"x\",\"Kind\":\"info\"}]}");

            File.WriteAllText(Path.Combine(Data, "Roles.json"),
                "[{\"Id\":\"administrator\",\"Name\":\"Administrator\",\"Area\":\"Admin\",\"Status\":\"Active\",\"IsSystem\":true,\"Permissions\":[\"users:view\"]}," +
                "{\"Id\":\"editor\",\"Name\":\"Editor\",\"Area\":\"Admin\",\"Status\":\"Active\",\"Permissions\":[\"proposals:view\",\"proposals:edit\"]}]");

            File.WriteAllText(Path.Combine(Data, "RoleAssignments.json"), "{\"user-9\":\"editor\"}");

            File.WriteAllText(Path.Combine(Data, "Administrator.json"),
                "{\"UserId\":\"owner-1\",\"Email\":\"admin@resk.co.za\"}");

            File.WriteAllLines(Path.Combine(Data, "AuditLog.jsonl"), new[]
            {
                "{\"Id\":\"AUD-2026-00001\",\"AtUtc\":\"2026-10-01T08:00:00Z\",\"Action\":\"Signed in\",\"Module\":\"Security\",\"Record\":\"Session\",\"UserName\":\"A\",\"Result\":\"Successful\"}",
                "not json",
                "{\"Id\":\"AUD-2026-00002\",\"AtUtc\":\"2026-10-01T09:00:00Z\",\"Action\":\"Updated role\",\"Module\":\"Roles\",\"Record\":\"Role\",\"UserName\":\"A\",\"Result\":\"Successful\",\"Changes\":[{\"Field\":\"Name\",\"Before\":\"a\",\"After\":\"b\"}]}"
            });
        }


        [Fact]
        public void Every_json_file_is_imported_and_renamed()
        {
            new TestDatabase();

            using (ApplicationDbContext db = ReskDb.Open())
            {
                ReskJsonImport.ImportAll(_root, db, NullLogger.Instance);
            }

            Assert.Equal("Sipho", ProposalReviewStore.Load(_root, 12).ReviewerName);

            ReskAccount account = ReskAccountStore.Load(_root, "user-9");
            Assert.Equal(ReskAccountStore.Active, account.Status);
            Assert.Equal(new[] { "Approved", "Registered" }, account.Activity.Select(a => a.Title));

            Assert.Equal(new[] { "proposals:view", "proposals:edit" }, ReskRoleStore.Get(_root, "editor")!.Permissions);
            Assert.Equal("editor", ReskRoleStore.Assignments(_root)["user-9"]);
            Assert.Equal("owner-1", ReskRoleStore.AdministratorUserId(_root));

            ReskAuditEvent updated = ReskAuditLog.Get(_root, "AUD-2026-00002")!;
            Assert.Equal("Name", Assert.Single(updated.Changes).Field);
            Assert.Equal(2, ReskAuditLog.All(_root).Count);

            Assert.True(Directory.Exists(Path.Combine(Data, "Reviews" + ReskJsonImport.ImportedSuffix)));
            Assert.True(File.Exists(Path.Combine(Data, "AuditLog.jsonl" + ReskJsonImport.ImportedSuffix)));
            Assert.False(File.Exists(Path.Combine(Data, "Roles.json")));
        }


        [Fact]
        public void Import_never_overwrites_data_already_in_the_database()
        {
            new TestDatabase();

            ProposalReviewStore.Save(_root, new ProposalReview { ProposalId = 99, ReviewerName = "Already here" });

            using (ApplicationDbContext db = ReskDb.Open())
            {
                ReskJsonImport.ImportAll(_root, db, NullLogger.Instance);
            }

            var all = ProposalReviewStore.LoadAll(_root);

            Assert.Equal(new[] { 99 }, all.Keys);
            Assert.True(Directory.Exists(Path.Combine(Data, "Reviews")));
        }


        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
