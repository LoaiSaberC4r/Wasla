using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // EF-generated immutable column arrays follow the existing migration convention.

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HistoricalTimelineAndIntakeRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "TimelineAtUtc",
                table: "PatientHistoricalDocumentVersions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
            migrationBuilder.Sql("UPDATE v SET [TimelineAtUtc] = CASE WHEN v.[DateUnknown] = 0 AND v.[DocumentDate] IS NOT NULL THEN CONVERT(datetime2, v.[DocumentDate]) ELSE d.[UploadedAtUtc] END FROM [PatientHistoricalDocumentVersions] v INNER JOIN [PatientHistoricalDocuments] d ON d.[Id] = v.[DocumentId];");

            migrationBuilder.CreateTable(
                name: "ChildOnboardingRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    Gender = table.Column<int>(type: "int", nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ContactNameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContactPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResubmitIdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ResubmitFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ResultRowVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChildOnboardingRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChildOnboardingRevisions_ChildOnboardingRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "ChildOnboardingRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChildOnboardingRevisions_RequestId_ResubmitIdempotencyKey",
                table: "ChildOnboardingRevisions",
                columns: new[] { "RequestId", "ResubmitIdempotencyKey" },
                unique: true,
                filter: "[ResubmitIdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ChildOnboardingRevisions_RequestId_Revision",
                table: "ChildOnboardingRevisions",
                columns: new[] { "RequestId", "Revision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Forward-only migration: retain historical source and intake revisions.");
        }
    }
}
