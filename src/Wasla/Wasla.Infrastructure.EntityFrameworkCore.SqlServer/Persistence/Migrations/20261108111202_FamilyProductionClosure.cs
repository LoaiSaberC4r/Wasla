using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // EF-generated immutable migration arrays.

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FamilyProductionClosure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChildOnboardingRequests_ParentPatientId_IntentHash",
                table: "ChildOnboardingRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ChildOnboarding_Status",
                table: "ChildOnboardingRequests");

            migrationBuilder.AddColumn<string>(
                name: "AfterSnapshot",
                table: "DependentProfileChanges",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BeforeSnapshot",
                table: "DependentProfileChanges",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GovernanceCaseId",
                table: "ChildOnboardingRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PrivateMediaReconciliationCheckpoints",
                columns: table => new
                {
                    Module = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LastFileKey = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LastReferenceKey = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrivateMediaReconciliationCheckpoints", x => x.Module);
                });

            migrationBuilder.CreateTable(
                name: "PrivateMediaReconciliationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Module = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    InspectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExaminedFiles = table.Column<long>(type: "bigint", nullable: false),
                    ExaminedReferences = table.Column<long>(type: "bigint", nullable: false),
                    OrphanCandidates = table.Column<long>(type: "bigint", nullable: false),
                    MissingFiles = table.Column<long>(type: "bigint", nullable: false),
                    HasMore = table.Column<bool>(type: "bit", nullable: false),
                    FindingsJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrivateMediaReconciliationRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChildOnboardingRequests_GovernanceCaseId",
                table: "ChildOnboardingRequests",
                column: "GovernanceCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ChildOnboardingRequests_ParentPatientId_IntentHash",
                table: "ChildOnboardingRequests",
                columns: new[] { "ParentPatientId", "IntentHash" },
                unique: true,
                filter: "[Status] IN (1,2,5)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChildOnboarding_Status",
                table: "ChildOnboardingRequests",
                sql: "[Status] IN (1,2,3,4,5)");

            migrationBuilder.CreateIndex(
                name: "IX_PrivateMediaReconciliationRuns_Module_InspectedAtUtc_Id",
                table: "PrivateMediaReconciliationRuns",
                columns: new[] { "Module", "InspectedAtUtc", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_ChildOnboardingRequests_FamilyGovernanceCases_GovernanceCaseId",
                table: "ChildOnboardingRequests",
                column: "GovernanceCaseId",
                principalTable: "FamilyGovernanceCases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
            => throw new NotSupportedException("Retain governance, audit and validation receipts. Roll back the application using the documented forward-only deployment procedure.");
    }
}
