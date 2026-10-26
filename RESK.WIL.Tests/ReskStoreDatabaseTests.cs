using System;
using System.Collections.Generic;
using System.Linq;
using RESK.WIL.Data;
using RESK.WIL.Services;
using Xunit;

namespace RESK.WIL.Tests
{
    // The review, account, role and audit stores now save to the database.
    [Collection(DatabaseCollection.Name)]
    public class ReskStoreDatabaseTests
    {
        private const string Root = TestDatabase.Root;


        // =========================================================
        // PROPOSAL REVIEWS
        // =========================================================

        [Fact]
        public void Review_that_was_never_saved_loads_empty()
        {
            new TestDatabase();

            ProposalReview review = ProposalReviewStore.Load(Root, 42);

            Assert.Equal(42, review.ProposalId);
            Assert.False(review.HasReviewer);
        }


        [Fact]
        public void Review_is_saved_then_updated_without_a_duplicate_row()
        {
            new TestDatabase();

            ProposalReviewStore.Save(Root, new ProposalReview
            {
                ProposalId = 7,
                ReviewerUserId = "user-1",
                ReviewerName = "Thandi",
                Deadline = new DateTime(2026, 11, 1)
            });

            ProposalReview loaded = ProposalReviewStore.Load(Root, 7);
            loaded.Recommendation = ProposalReviewStore.Approve;
            loaded.PurposeIsClear = true;
            ProposalReviewStore.Save(Root, loaded);

            ProposalReview again = ProposalReviewStore.Load(Root, 7);

            Assert.Equal("Thandi", again.ReviewerName);
            Assert.Equal(ProposalReviewStore.Approve, again.Recommendation);
            Assert.True(again.PurposeIsClear);
            Assert.Single(ProposalReviewStore.LoadAll(Root));

            using ApplicationDbContext db = ReskDb.Open();
            Assert.Equal(1, db.ReskProposalReviews.Count());
        }


        // =========================================================
        // ACCOUNTS
        // =========================================================

        [Fact]
        public void Account_and_its_activity_round_trip_newest_first()
        {
            new TestDatabase();

            var account = new ReskAccount { UserId = "u1", Status = ReskAccountStore.Pending };
            ReskAccountStore.Log(account, "Registered", "Signed up");
            ReskAccountStore.Save(Root, account);

            ReskAccount loaded = ReskAccountStore.Load(Root, "u1");
            ReskAccountStore.Log(loaded, "Approved", "Approved by admin", "success");
            loaded.Status = ReskAccountStore.Active;
            ReskAccountStore.Save(Root, loaded);

            ReskAccount again = ReskAccountStore.Load(Root, "u1");

            Assert.Equal(ReskAccountStore.Active, again.Status);
            Assert.Equal(new[] { "Approved", "Registered" }, again.Activity.Select(a => a.Title));

            using ApplicationDbContext db = ReskDb.Open();
            Assert.Equal(2, db.ReskAccountActivities.Count());
        }


        [Fact]
        public void Activity_history_is_trimmed_to_the_limit_in_the_database()
        {
            new TestDatabase();

            var account = new ReskAccount { UserId = "u2" };

            for (int i = 0; i < ReskAccountStore.MaxActivity + 15; i++)
            {
                ReskAccountStore.Log(account, "Event " + i, "detail");
            }

            ReskAccountStore.Save(Root, account);

            ReskAccount loaded = ReskAccountStore.Load(Root, "u2");
            ReskAccountStore.Log(loaded, "Newest", "detail");
            ReskAccountStore.Save(Root, loaded);

            using ApplicationDbContext db = ReskDb.Open();
            Assert.Equal(ReskAccountStore.MaxActivity, db.ReskAccountActivities.Count());
            Assert.Equal("Newest", ReskAccountStore.Load(Root, "u2").Activity.First().Title);
        }


        [Fact]
        public void Expired_suspension_ends_by_itself_and_is_saved()
        {
            new TestDatabase();

            ReskAccountStore.Save(Root, new ReskAccount
            {
                UserId = "u3",
                Status = ReskAccountStore.Suspended,
                SuspendedUntilUtc = DateTime.UtcNow.AddMinutes(-1),
                RestrictionReason = "Spam"
            });

            ReskAccount loaded = ReskAccountStore.Load(Root, "u3");

            Assert.Equal(string.Empty, loaded.Status);
            Assert.Null(loaded.RestrictionReason);

            using ApplicationDbContext db = ReskDb.Open();
            Assert.Equal(string.Empty, db.ReskAccounts.Find("u3")!.Status);
        }


        [Fact]
        public void Deleting_an_account_removes_its_activity()
        {
            new TestDatabase();

            var account = new ReskAccount { UserId = "u4" };
            ReskAccountStore.Log(account, "Registered", "Signed up");
            ReskAccountStore.Save(Root, account);

            ReskAccountStore.Delete(Root, "u4");

            using ApplicationDbContext db = ReskDb.Open();
            Assert.Empty(db.ReskAccounts);
            Assert.Empty(db.ReskAccountActivities);
        }


        // =========================================================
        // ROLES
        // =========================================================

        [Fact]
        public void Roles_are_seeded_once_with_a_full_access_Administrator()
        {
            new TestDatabase();

            List<ReskRole> first = ReskRoleStore.All(Root);
            List<ReskRole> second = ReskRoleStore.All(Root);

            ReskRole admin = Assert.Single(first, r => r.Id == ReskRoleStore.AdministratorId);
            Assert.Equal(ReskRoleStore.AllPermissions(), admin.Permissions);
            Assert.Equal(first.Select(r => r.Id), second.Select(r => r.Id));
        }


        [Fact]
        public void Saving_roles_keeps_order_and_permissions_and_removes_deleted_roles()
        {
            new TestDatabase();

            List<ReskRole> roles = ReskRoleStore.All(Root);
            roles.Add(new ReskRole
            {
                Id = "editor",
                Name = "Editor",
                Area = "Admin",
                Permissions = new List<string> { "proposals:view", "proposals:edit" }
            });
            roles.Add(new ReskRole { Id = "temp", Name = "Temporary", Area = "Reviewer" });
            ReskRoleStore.SaveAll(Root, roles);

            roles = ReskRoleStore.All(Root);
            ReskRole removed = roles.Single(r => r.Id == "temp");
            roles.Remove(removed);
            ReskRoleStore.SaveAll(Root, roles);

            List<ReskRole> loaded = ReskRoleStore.All(Root);

            Assert.Equal(roles.Select(r => r.Id), loaded.Select(r => r.Id));
            Assert.Equal(new[] { "proposals:view", "proposals:edit" }, ReskRoleStore.Get(Root, "editor")!.Permissions);
            Assert.Null(ReskRoleStore.Get(Root, removed.Id));
        }


        [Fact]
        public void Role_assignments_can_be_set_changed_and_cleared()
        {
            new TestDatabase();

            ReskRoleStore.Assign(Root, "u1", "reviewer");
            ReskRoleStore.Assign(Root, "u2", "viewer");
            ReskRoleStore.Assign(Root, "u1", "proposal-manager");
            ReskRoleStore.Assign(Root, "u2", null);

            Dictionary<string, string> map = ReskRoleStore.Assignments(Root);

            Assert.Single(map);
            Assert.Equal("proposal-manager", map["u1"]);
        }


        [Fact]
        public void Administrator_account_is_remembered_in_the_database()
        {
            new TestDatabase();

            Assert.Null(ReskRoleStore.AdministratorUserId(Root));

            ReskRoleStore.SetAdministrator(Root, "owner-1", "admin@resk.co.za");
            ReskRoleStore.ResetCache();

            Assert.Equal("owner-1", ReskRoleStore.AdministratorUserId(Root));
            Assert.True(ReskRoleStore.IsAdministratorAccount(Root, "owner-1"));
        }


        // =========================================================
        // AUDIT LOG
        // =========================================================

        [Fact]
        public void Audit_events_get_sequential_ids_and_are_listed_newest_first()
        {
            new TestDatabase();

            ReskAuditEvent first = ReskAuditLog.Add(Root, NewEvent("Created role", DateTime.UtcNow.AddMinutes(-5)));
            ReskAuditEvent second = ReskAuditLog.Add(Root, NewEvent("Deleted role", DateTime.UtcNow));

            string year = SouthAfricaYear(DateTime.UtcNow);

            Assert.Equal($"AUD-{year}-00001", first.Id);
            Assert.Equal($"AUD-{year}-00002", second.Id);
            Assert.Equal(new[] { second.Id, first.Id }, ReskAuditLog.All(Root).Select(e => e.Id));
        }


        [Fact]
        public void Audit_event_keeps_its_changes_and_is_found_case_insensitively()
        {
            new TestDatabase();

            ReskAuditEvent item = NewEvent("Updated role permissions", DateTime.UtcNow);
            item.Changes.Add(new ReskAuditChange { Field = "Status", Before = "Draft", After = "Active" });
            item.Changes.Add(new ReskAuditChange { Field = "Name", Before = "Old", After = "New" });

            string id = ReskAuditLog.Add(Root, item).Id;

            ReskAuditEvent? loaded = ReskAuditLog.Get(Root, id.ToLowerInvariant());

            Assert.NotNull(loaded);
            Assert.Equal(new[] { "Status", "Name" }, loaded!.Changes.Select(c => c.Field));
        }


        [Fact]
        public void Audit_events_older_than_the_retention_period_are_removed()
        {
            new TestDatabase();

            ReskAuditLog.Add(Root, NewEvent("Ancient", DateTime.UtcNow.AddYears(-(ReskAuditLog.RetentionYears + 1))));
            ReskAuditLog.Add(Root, NewEvent("Recent", DateTime.UtcNow));

            List<ReskAuditEvent> all = ReskAuditLog.All(Root);

            Assert.Equal("Recent", Assert.Single(all).Action);
        }


        [Fact]
        public void Audit_log_does_not_throw_when_the_database_is_unavailable()
        {
            ReskDb.Configure(new BrokenFactory());

            ReskAuditEvent item = ReskAuditLog.Add(Root, NewEvent("Signed in", DateTime.UtcNow));

            Assert.Equal("Signed in", item.Action);
        }


        private static ReskAuditEvent NewEvent(string action, DateTime atUtc) => new ReskAuditEvent
        {
            Action = action,
            AtUtc = atUtc,
            Module = "Roles",
            Record = "Role",
            UserName = "Test user"
        };


        private static string SouthAfricaYear(DateTime utc) =>
            RESK.WIL.Models.SouthAfricaTime.ToLocal(utc).Year.ToString();


        private sealed class BrokenFactory : Microsoft.EntityFrameworkCore.IDbContextFactory<ApplicationDbContext>
        {
            public ApplicationDbContext CreateDbContext() =>
                throw new InvalidOperationException("Database is down.");
        }
    }
}
