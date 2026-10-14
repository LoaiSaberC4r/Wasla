using System;
using Microsoft.EntityFrameworkCore.Migrations;

#pragma warning disable CA1861 // Generated schema arrays.

#nullable disable

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase18ExportAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReportExportAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorRole = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ReportCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Format = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Language = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    AppliedFiltersJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ByteCount = table.Column<int>(type: "int", nullable: true),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportExportAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportExportAudits_ApplicationUsers_ActorApplicationUserId",
                        column: x => x.ActorApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReportExportAudits_ActorApplicationUserId_RecordedAtUtc",
                table: "ReportExportAudits",
                columns: new[] { "ActorApplicationUserId", "RecordedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportExportAudits_ReportCode_RecordedAtUtc",
                table: "ReportExportAudits",
                columns: new[] { "ReportCode", "RecordedAtUtc" });
            migrationBuilder.Sql("CREATE TRIGGER [TR_Immutable_ReportExportAudits] ON [ReportExportAudits] AFTER UPDATE, DELETE AS THROW 51018, 'Report export audit is immutable.', 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReportExportAudits");
        }
    }
}
