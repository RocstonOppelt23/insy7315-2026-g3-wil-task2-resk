using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RESK.WIL.Data.Migrations
{
    /// <summary>
    /// Moves reviews, accounts, roles and the audit log out of the
    /// App_Data JSON files into their own tables, and adds check
    /// constraints to ProducerProposals.
    /// </summary>
    public partial class MoveAdminDataToDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---------- ProducerProposals check constraints ----------
            migrationBuilder.AddCheckConstraint(
                name: "CK_ProducerProposals_CompletedSteps",
                table: "ProducerProposals",
                sql: "[CompletedSteps] BETWEEN 0 AND 5");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProducerProposals_Status",
                table: "ProducerProposals",
                sql: "[Status] IN ('Draft', 'InReview', 'Approved', 'ChangesRequested', 'Rejected')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProducerProposals_SubmittedReference",
                table: "ProducerProposals",
                sql: "[Status] = 'Draft' OR [SubmittedAtUtc] IS NOT NULL OR [Reference] IS NULL");

            // ---------- Accounts ----------
            migrationBuilder.CreateTable(
                name: "ReskAccounts",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SuspendedUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RestrictionReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RestrictedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    JoinedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequestedRole = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RegistrationReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AdminNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequirePasswordChange = table.Column<bool>(type: "bit", nullable: false),
                    PasswordChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StatusChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReskAccounts", x => x.UserId);
                    table.CheckConstraint("CK_ReskAccounts_Status", "[Status] IN ('', 'Pending', 'Active', 'Suspended', 'Rejected', 'Banned')");
                });

            migrationBuilder.CreateTable(
                name: "ReskAccountActivities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReskAccountActivities", x => x.Id);
                    table.CheckConstraint("CK_ReskAccountActivities_Kind", "[Kind] IN ('info', 'success', 'warning', 'danger')");
                    table.ForeignKey(
                        name: "FK_ReskAccountActivities_ReskAccounts_AccountUserId",
                        column: x => x.AccountUserId,
                        principalTable: "ReskAccounts",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            // ---------- Audit log ----------
            migrationBuilder.CreateTable(
                name: "ReskAuditEvents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Module = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UserEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UserRole = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Record = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecordId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SessionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Ip = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Device = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Result = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Request = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReskAuditEvents", x => x.Id);
                    table.CheckConstraint("CK_ReskAuditEvents_Result", "[Result] IN ('Successful', 'Failed')");
                });

            migrationBuilder.CreateTable(
                name: "ReskAuditChanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Field = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Before = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    After = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReskAuditChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReskAuditChanges_ReskAuditEvents_EventId",
                        column: x => x.EventId,
                        principalTable: "ReskAuditEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ---------- Proposal reviews ----------
            migrationBuilder.CreateTable(
                name: "ReskProposalReviews",
                columns: table => new
                {
                    ProposalId = table.Column<int>(type: "int", nullable: false),
                    ReviewerUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReviewerName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Deadline = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AssignmentNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssignedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Recommendation = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PurposeIsClear = table.Column<bool>(type: "bit", nullable: false),
                    AudienceIsSuitable = table.Column<bool>(type: "bit", nullable: false),
                    PlanIsRealistic = table.Column<bool>(type: "bit", nullable: false),
                    BudgetIsComplete = table.Column<bool>(type: "bit", nullable: false),
                    ReviewSavedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewSubmitted = table.Column<bool>(type: "bit", nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReskProposalReviews", x => x.ProposalId);
                    table.CheckConstraint("CK_ReskProposalReviews_Recommendation", "[Recommendation] IS NULL OR [Recommendation] IN ('Approve', 'RequestChanges', 'Reject')");
                });

            // ---------- Roles ----------
            migrationBuilder.CreateTable(
                name: "ReskRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Area = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Template = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    Permissions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReskRoles", x => x.Id);
                    table.CheckConstraint("CK_ReskRoles_Area", "[Area] IN ('Admin', 'Reviewer', 'Producer')");
                    table.CheckConstraint("CK_ReskRoles_Status", "[Status] IN ('Active', 'Inactive', 'Draft')");
                });

            migrationBuilder.CreateTable(
                name: "ReskRoleAssignments",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReskRoleAssignments", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "ReskSystemValues",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReskSystemValues", x => x.Key);
                });

            // ---------- Indexes ----------
            migrationBuilder.CreateIndex(
                name: "IX_ReskAccountActivities_AccountUserId_AtUtc",
                table: "ReskAccountActivities",
                columns: new[] { "AccountUserId", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReskAuditChanges_EventId",
                table: "ReskAuditChanges",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_ReskAuditEvents_AtUtc",
                table: "ReskAuditEvents",
                column: "AtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ReskAuditEvents_Module",
                table: "ReskAuditEvents",
                column: "Module");

            migrationBuilder.CreateIndex(
                name: "IX_ReskProposalReviews_ReviewerUserId",
                table: "ReskProposalReviews",
                column: "ReviewerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReskRoleAssignments_RoleId",
                table: "ReskRoleAssignments",
                column: "RoleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ReskAccountActivities");
            migrationBuilder.DropTable(name: "ReskAuditChanges");
            migrationBuilder.DropTable(name: "ReskProposalReviews");
            migrationBuilder.DropTable(name: "ReskRoleAssignments");
            migrationBuilder.DropTable(name: "ReskRoles");
            migrationBuilder.DropTable(name: "ReskSystemValues");
            migrationBuilder.DropTable(name: "ReskAccounts");
            migrationBuilder.DropTable(name: "ReskAuditEvents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProducerProposals_CompletedSteps",
                table: "ProducerProposals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProducerProposals_Status",
                table: "ProducerProposals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProducerProposals_SubmittedReference",
                table: "ProducerProposals");
        }
    }
}
