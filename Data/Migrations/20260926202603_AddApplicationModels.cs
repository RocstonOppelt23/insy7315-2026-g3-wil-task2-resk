using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RESK.WIL.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    UsersView = table.Column<bool>(type: "bit", nullable: false),
                    UsersEdit = table.Column<bool>(type: "bit", nullable: false),
                    UsersDelete = table.Column<bool>(type: "bit", nullable: false),
                    UsersApprove = table.Column<bool>(type: "bit", nullable: false),
                    UsersScope = table.Column<int>(type: "int", nullable: false),
                    ProposalsView = table.Column<bool>(type: "bit", nullable: false),
                    ProposalsCreate = table.Column<bool>(type: "bit", nullable: false),
                    ProposalsEdit = table.Column<bool>(type: "bit", nullable: false),
                    ProposalsDelete = table.Column<bool>(type: "bit", nullable: false),
                    ProposalsAssign = table.Column<bool>(type: "bit", nullable: false),
                    ProposalsApprove = table.Column<bool>(type: "bit", nullable: false),
                    ProposalsExport = table.Column<bool>(type: "bit", nullable: false),
                    ProposalsScope = table.Column<int>(type: "int", nullable: false),
                    ProposalAssignmentScope = table.Column<int>(type: "int", nullable: false),
                    ReportsView = table.Column<bool>(type: "bit", nullable: false),
                    ReportsExport = table.Column<bool>(type: "bit", nullable: false),
                    ReportsScope = table.Column<int>(type: "int", nullable: false),
                    AuditView = table.Column<bool>(type: "bit", nullable: false),
                    AuditExport = table.Column<bool>(type: "bit", nullable: false),
                    AuditScope = table.Column<int>(type: "int", nullable: false),
                    RolesView = table.Column<bool>(type: "bit", nullable: false),
                    RolesCreate = table.Column<bool>(type: "bit", nullable: false),
                    RolesEdit = table.Column<bool>(type: "bit", nullable: false),
                    RolesDelete = table.Column<bool>(type: "bit", nullable: false),
                    RolesScope = table.Column<int>(type: "int", nullable: false),
                    SettingsView = table.Column<bool>(type: "bit", nullable: false),
                    SettingsEdit = table.Column<bool>(type: "bit", nullable: false),
                    SettingsScope = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Organisation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PhysicalAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IdNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProfileImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Preference = table.Column<bool>(type: "bit", nullable: false),
                    ProposalNotification = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReviewNotification = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DraftNotification = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PasswordLastChanged = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsMfaEnabled = table.Column<bool>(type: "bit", nullable: false),
                    ActiveSessions = table.Column<int>(type: "int", nullable: false),
                    AccountStatus = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PositionChangeRequest = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoleScopeTargets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    Area = table.Column<int>(type: "int", nullable: false),
                    TargetRoleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleScopeTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoleScopeTargets_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoleScopeTargets_Roles_TargetRoleId",
                        column: x => x.TargetRoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Proposals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProducerId = table.Column<int>(type: "int", nullable: false),
                    LastChangerId = table.Column<int>(type: "int", nullable: true),
                    LastAction = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProposalStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProposalType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PhysicalAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProgrammeTitle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Partner = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TdlaFileUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Publisher = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Duration = table.Column<int>(type: "int", nullable: false),
                    Episodes = table.Column<int>(type: "int", nullable: true),
                    Introduction = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Background = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Motivation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Synopsis = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Treatment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FinancePlan = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResourceSkills = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TargetAudience = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MediaHandles = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Genre = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Copyright = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WebsiteUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CTTVSupport = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Sponsors = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LicenceDuration = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VerificationCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsTdlaRead = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Proposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Proposals_Users_ProducerId",
                        column: x => x.ProducerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AssignedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_ProducerId",
                table: "Proposals",
                column: "ProducerId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleScopeTargets_RoleId_Area_TargetRoleId",
                table: "RoleScopeTargets",
                columns: new[] { "RoleId", "Area", "TargetRoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoleScopeTargets_TargetRoleId",
                table: "RoleScopeTargets",
                column: "TargetRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Proposals");

            migrationBuilder.DropTable(
                name: "RoleScopeTargets");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
