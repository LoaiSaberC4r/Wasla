using System;
#pragma warning disable CA1861 // Generated migration schema arrays.
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase18ReportsAndInsights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CheckInGracePeriodMinutesSnapshot",
                table: "Reservations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "GeneratedBySystem",
                table: "OperatingObligations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<Guid>(
                name: "ActorApplicationUserId",
                table: "OperatingFinanceHistories",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "SnapshotRevisionId",
                table: "OperatingFinanceHistories",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DoctorInsightFollowUps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PracticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MetricCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EvidenceJson = table.Column<string>(type: "nvarchar(max)", maxLength: 12000, nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    CreatedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorInsightFollowUps", x => x.Id);
                    table.CheckConstraint("CK_DoctorInsight_State", "[State] IN (1,2,3,4)");
                    table.ForeignKey(
                        name: "FK_DoctorInsightFollowUps_ApplicationUsers_CreatedByApplicationUserId",
                        column: x => x.CreatedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorInsightFollowUps_DoctorPractices_DoctorId_PracticeId",
                        columns: x => new { x.DoctorId, x.PracticeId },
                        principalTable: "DoctorPractices",
                        principalColumns: new[] { "DoctorId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PracticeReportingPolicySnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PracticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionNumber = table.Column<long>(type: "bigint", nullable: false),
                    PolicyJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PracticeReportingPolicySnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PracticeReportingPolicySnapshots_DoctorPractices_DoctorId_PracticeId",
                        columns: x => new { x.DoctorId, x.PracticeId },
                        principalTable: "DoctorPractices",
                        principalColumns: new[] { "DoctorId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DoctorInsightHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InsightId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    ActorApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorInsightHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DoctorInsightHistories_ApplicationUsers_ActorApplicationUserId",
                        column: x => x.ActorApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorInsightHistories_DoctorInsightFollowUps_InsightId",
                        column: x => x.InsightId,
                        principalTable: "DoctorInsightFollowUps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DoctorInsightFollowUps_CreatedByApplicationUserId",
                table: "DoctorInsightFollowUps",
                column: "CreatedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorInsightFollowUps_DoctorId_PracticeId_State_CreatedOnUtc",
                table: "DoctorInsightFollowUps",
                columns: new[] { "DoctorId", "PracticeId", "State", "CreatedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DoctorInsightHistories_ActorApplicationUserId",
                table: "DoctorInsightHistories",
                column: "ActorApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorInsightHistories_InsightId_RecordedAtUtc",
                table: "DoctorInsightHistories",
                columns: new[] { "InsightId", "RecordedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DoctorInsightHistories_InsightId_SequenceNumber",
                table: "DoctorInsightHistories",
                columns: new[] { "InsightId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PracticeReportingPolicySnapshots_DoctorId_PracticeId",
                table: "PracticeReportingPolicySnapshots",
                columns: new[] { "DoctorId", "PracticeId" });

            migrationBuilder.CreateIndex(
                name: "IX_PracticeReportingPolicySnapshots_PracticeId_RecordedAtUtc",
                table: "PracticeReportingPolicySnapshots",
                columns: new[] { "PracticeId", "RecordedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PracticeReportingPolicySnapshots_PracticeId_RevisionNumber",
                table: "PracticeReportingPolicySnapshots",
                columns: new[] { "PracticeId", "RevisionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DoctorInsightHistories");

            migrationBuilder.DropTable(
                name: "PracticeReportingPolicySnapshots");

            migrationBuilder.DropTable(
                name: "DoctorInsightFollowUps");

            migrationBuilder.DropColumn(
                name: "CheckInGracePeriodMinutesSnapshot",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "GeneratedBySystem",
                table: "OperatingObligations");

            migrationBuilder.DropColumn(
                name: "SnapshotRevisionId",
                table: "OperatingFinanceHistories");

            // EF's nullable-to-required fallback emits an UPDATE even for an empty
            // table, which correctly trips the immutable-history trigger. Never
            // invent a zero-Guid actor when reverting system-authored evidence.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [OperatingFinanceHistories] WHERE [ActorApplicationUserId] IS NULL)
                    THROW 51018, 'Cannot downgrade system-authored financial history without an approved preservation plan.', 1;
                """);
            migrationBuilder.DropForeignKey(name: "FK_OperatingFinanceHistories_ApplicationUsers_ActorApplicationUserId", table: "OperatingFinanceHistories");
            migrationBuilder.DropIndex(name: "IX_OperatingFinanceHistories_ActorApplicationUserId", table: "OperatingFinanceHistories");
            migrationBuilder.Sql("ALTER TABLE [OperatingFinanceHistories] ALTER COLUMN [ActorApplicationUserId] uniqueidentifier NOT NULL;");
            migrationBuilder.CreateIndex(name: "IX_OperatingFinanceHistories_ActorApplicationUserId", table: "OperatingFinanceHistories", column: "ActorApplicationUserId");
            migrationBuilder.AddForeignKey(name: "FK_OperatingFinanceHistories_ApplicationUsers_ActorApplicationUserId", table: "OperatingFinanceHistories",
                column: "ActorApplicationUserId", principalTable: "ApplicationUsers", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        }
    }
}
