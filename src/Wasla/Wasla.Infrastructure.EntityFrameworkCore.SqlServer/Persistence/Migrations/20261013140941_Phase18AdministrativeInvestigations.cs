using System;
using Microsoft.EntityFrameworkCore.Migrations;

#pragma warning disable CA1861 // Generated schema arrays.

#nullable disable

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase18AdministrativeInvestigations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdministrativeInvestigations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TitleAr = table.Column<string>(type: "nvarchar(1200)", maxLength: 1200, nullable: false),
                    TitleEn = table.Column<string>(type: "nvarchar(1200)", maxLength: 1200, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 12000, nullable: false),
                    IssueType = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    LinkedResourceType = table.Column<int>(type: "int", nullable: false),
                    LinkedResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LinkedDoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LinkedSpecializationRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LinkedBackgroundJobRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LinkedOutboxMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedSuperAdminId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ResolutionType = table.Column<int>(type: "int", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(max)", maxLength: 12000, nullable: true),
                    ClosureNotes = table.Column<string>(type: "nvarchar(max)", maxLength: 12000, nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HistorySequence = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdministrativeInvestigations", x => x.Id);
                    table.CheckConstraint("CK_Investigation_Assignee", "[Status] <> 2 OR [AssignedSuperAdminId] IS NOT NULL");
                    table.CheckConstraint("CK_Investigation_Closure", "[Status] <> 4 OR ([ClosureNotes] IS NOT NULL AND [ClosedOnUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_Investigation_Link", "([LinkedResourceType] = 0 AND [LinkedResourceId] IS NULL AND [LinkedDoctorId] IS NULL AND [LinkedSpecializationRequestId] IS NULL AND [LinkedBackgroundJobRunId] IS NULL AND [LinkedOutboxMessageId] IS NULL) OR ([LinkedResourceType] = 1 AND [LinkedResourceId] = [LinkedDoctorId] AND [LinkedDoctorId] IS NOT NULL AND [LinkedSpecializationRequestId] IS NULL AND [LinkedBackgroundJobRunId] IS NULL AND [LinkedOutboxMessageId] IS NULL) OR ([LinkedResourceType] = 2 AND [LinkedResourceId] = [LinkedSpecializationRequestId] AND [LinkedSpecializationRequestId] IS NOT NULL AND [LinkedDoctorId] IS NULL AND [LinkedBackgroundJobRunId] IS NULL AND [LinkedOutboxMessageId] IS NULL) OR ([LinkedResourceType] = 3 AND [LinkedResourceId] = [LinkedBackgroundJobRunId] AND [LinkedBackgroundJobRunId] IS NOT NULL AND [LinkedDoctorId] IS NULL AND [LinkedSpecializationRequestId] IS NULL AND [LinkedOutboxMessageId] IS NULL) OR ([LinkedResourceType] = 4 AND [LinkedResourceId] = [LinkedOutboxMessageId] AND [LinkedOutboxMessageId] IS NOT NULL AND [LinkedDoctorId] IS NULL AND [LinkedSpecializationRequestId] IS NULL AND [LinkedBackgroundJobRunId] IS NULL)");
                    table.CheckConstraint("CK_Investigation_Resolution", "[Status] <> 3 OR ([ResolutionType] IS NOT NULL AND [ResolutionNotes] IS NOT NULL AND [ResolvedOnUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_Investigation_State", "[Status] IN (1,2,3,4) AND [Priority] IN (1,2) AND [IssueType] BETWEEN 1 AND 7");
                    table.ForeignKey(
                        name: "FK_AdministrativeInvestigations_ApplicationUsers_CreatedByApplicationUserId",
                        column: x => x.CreatedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdministrativeInvestigations_DoctorSpecializationRequests_LinkedSpecializationRequestId",
                        column: x => x.LinkedSpecializationRequestId,
                        principalTable: "DoctorSpecializationRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdministrativeInvestigations_Doctors_LinkedDoctorId",
                        column: x => x.LinkedDoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdministrativeInvestigations_EmailOutboxMessages_LinkedOutboxMessageId",
                        column: x => x.LinkedOutboxMessageId,
                        principalTable: "EmailOutboxMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdministrativeInvestigations_OperatingGenerationRuns_LinkedBackgroundJobRunId",
                        column: x => x.LinkedBackgroundJobRunId,
                        principalTable: "OperatingGenerationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdministrativeInvestigations_SuperAdmins_AssignedSuperAdminId",
                        column: x => x.AssignedSuperAdminId,
                        principalTable: "SuperAdmins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvestigationHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ActorApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedSuperAdminId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResolutionType = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", maxLength: 12000, nullable: true),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvestigationHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvestigationHistories_AdministrativeInvestigations_CaseId",
                        column: x => x.CaseId,
                        principalTable: "AdministrativeInvestigations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvestigationHistories_ApplicationUsers_ActorApplicationUserId",
                        column: x => x.ActorApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvestigationHistories_SuperAdmins_AssignedSuperAdminId",
                        column: x => x.AssignedSuperAdminId,
                        principalTable: "SuperAdmins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdministrativeInvestigations_AssignedSuperAdminId_Status",
                table: "AdministrativeInvestigations",
                columns: new[] { "AssignedSuperAdminId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AdministrativeInvestigations_CreatedByApplicationUserId",
                table: "AdministrativeInvestigations",
                column: "CreatedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AdministrativeInvestigations_IssueType_CreatedOnUtc",
                table: "AdministrativeInvestigations",
                columns: new[] { "IssueType", "CreatedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AdministrativeInvestigations_LinkedBackgroundJobRunId",
                table: "AdministrativeInvestigations",
                column: "LinkedBackgroundJobRunId");

            migrationBuilder.CreateIndex(
                name: "IX_AdministrativeInvestigations_LinkedDoctorId",
                table: "AdministrativeInvestigations",
                column: "LinkedDoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_AdministrativeInvestigations_LinkedOutboxMessageId",
                table: "AdministrativeInvestigations",
                column: "LinkedOutboxMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_AdministrativeInvestigations_LinkedSpecializationRequestId",
                table: "AdministrativeInvestigations",
                column: "LinkedSpecializationRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_AdministrativeInvestigations_Status_Priority_CreatedOnUtc",
                table: "AdministrativeInvestigations",
                columns: new[] { "Status", "Priority", "CreatedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InvestigationHistories_ActorApplicationUserId",
                table: "InvestigationHistories",
                column: "ActorApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestigationHistories_AssignedSuperAdminId",
                table: "InvestigationHistories",
                column: "AssignedSuperAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestigationHistories_CaseId_SequenceNumber",
                table: "InvestigationHistories",
                columns: new[] { "CaseId", "SequenceNumber" },
                unique: true);
            migrationBuilder.Sql("CREATE TRIGGER [TR_Immutable_InvestigationHistories] ON [InvestigationHistories] AFTER UPDATE, DELETE AS THROW 51018, 'Investigation history is immutable.', 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvestigationHistories");

            migrationBuilder.DropTable(
                name: "AdministrativeInvestigations");
        }
    }
}
