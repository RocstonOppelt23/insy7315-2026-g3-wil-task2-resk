using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RESK.WIL.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProducerProposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Proposals",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Proposals",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Proposals",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Proposals",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ProducerProposals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CompletedSteps = table.Column<int>(type: "int", nullable: false),
                    ProgrammeTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProgrammeFormat = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EpisodeDuration = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PrimaryLanguage = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProducerDetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProgrammeDetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProductionDetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PilotShowreelLink = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProposalDocumentName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    ProposalDocumentStoredName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BudgetDocumentName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    BudgetDocumentStoredName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AdditionalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    AdditionalFileStoredName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProducerProposals", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProducerProposals_OwnerUserId_Status",
                table: "ProducerProposals",
                columns: new[] { "OwnerUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProducerProposals_Reference",
                table: "ProducerProposals",
                column: "Reference",
                unique: true,
                filter: "[Reference] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProducerProposals");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Proposals");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Proposals");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Proposals");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "Proposals");
        }
    }
}
