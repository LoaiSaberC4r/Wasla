#pragma warning disable CA1861 // EF-generated migration arrays are single-use metadata.
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase13ClinicalEncountersFollowUp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FollowUpEligibilityId",
                table: "Tickets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FollowUpEligibilityId",
                table: "Reservations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MedicalEncounters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorPracticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ClinicalNotes = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalEncounters", x => x.Id);
                    table.CheckConstraint("CK_MedicalEncounters_Completion", "([Status] = 1 AND [CompletedAtUtc] IS NULL) OR ([Status] = 2 AND [CompletedAtUtc] IS NOT NULL AND LEN(LTRIM(RTRIM([ClinicalNotes]))) > 0 AND [ClinicalNotes] IS NOT NULL)");
                    table.CheckConstraint("CK_MedicalEncounters_Status", "[Status] IN (1,2)");
                    table.ForeignKey(
                        name: "FK_MedicalEncounters_ApplicationUsers_CreatedByApplicationUserId",
                        column: x => x.CreatedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedicalEncounters_ApplicationUsers_ModifiedByApplicationUserId",
                        column: x => x.ModifiedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedicalEncounters_DoctorPractices_DoctorPracticeId",
                        column: x => x.DoctorPracticeId,
                        principalTable: "DoctorPractices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedicalEncounters_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedicalEncounters_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedicalEncounters_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Diagnoses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MedicalEncounterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    DisplayText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    NormalizedDisplayText = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsVoided = table.Column<bool>(type: "bit", nullable: false),
                    VoidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VoidedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Diagnoses", x => x.Id);
                    table.CheckConstraint("CK_Diagnoses_Type", "[Type] IN (1,2)");
                    table.ForeignKey(
                        name: "FK_Diagnoses_MedicalEncounters_MedicalEncounterId",
                        column: x => x.MedicalEncounterId,
                        principalTable: "MedicalEncounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EncounterAmendments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MedicalEncounterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedByDoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncounterAmendments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EncounterAmendments_ApplicationUsers_CreatedByApplicationUserId",
                        column: x => x.CreatedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EncounterAmendments_Doctors_CreatedByDoctorId",
                        column: x => x.CreatedByDoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EncounterAmendments_MedicalEncounters_MedicalEncounterId",
                        column: x => x.MedicalEncounterId,
                        principalTable: "MedicalEncounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EncounterAuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MedicalEncounterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncounterAuditEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EncounterAuditEvents_MedicalEncounters_MedicalEncounterId",
                        column: x => x.MedicalEncounterId,
                        principalTable: "MedicalEncounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FollowUpEligibilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceMedicalEncounterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorPracticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: false),
                    ReservedReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReservedTicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConsumedEncounterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FollowUpEligibilities", x => x.Id);
                    table.CheckConstraint("CK_FollowUpEligibilities_Claim", "([Status] = 2 AND (([ReservedReservationId] IS NOT NULL AND [ReservedTicketId] IS NULL) OR ([ReservedReservationId] IS NULL AND [ReservedTicketId] IS NOT NULL)) AND [ConsumedEncounterId] IS NULL) OR ([Status] IN (1,4) AND [ReservedReservationId] IS NULL AND [ReservedTicketId] IS NULL AND [ConsumedEncounterId] IS NULL) OR ([Status] = 3 AND [ReservedReservationId] IS NULL AND [ReservedTicketId] IS NULL AND [ConsumedEncounterId] IS NOT NULL)");
                    table.CheckConstraint("CK_FollowUpEligibilities_Status", "[Status] IN (1,2,3,4)");
                    table.ForeignKey(
                        name: "FK_FollowUpEligibilities_DoctorPractices_DoctorPracticeId",
                        column: x => x.DoctorPracticeId,
                        principalTable: "DoctorPractices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FollowUpEligibilities_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FollowUpEligibilities_MedicalEncounters_ConsumedEncounterId",
                        column: x => x.ConsumedEncounterId,
                        principalTable: "MedicalEncounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FollowUpEligibilities_MedicalEncounters_SourceMedicalEncounterId",
                        column: x => x.SourceMedicalEncounterId,
                        principalTable: "MedicalEncounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FollowUpEligibilities_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FollowUpEligibilities_Reservations_ReservedReservationId",
                        column: x => x.ReservedReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FollowUpEligibilities_Tickets_ReservedTicketId",
                        column: x => x.ReservedTicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EncounterAmendmentChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EncounterAmendmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetType = table.Column<int>(type: "int", nullable: false),
                    TargetEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChangeType = table.Column<int>(type: "int", nullable: false),
                    BeforeSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncounterAmendmentChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EncounterAmendmentChanges_EncounterAmendments_EncounterAmendmentId",
                        column: x => x.EncounterAmendmentId,
                        principalTable: "EncounterAmendments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FollowUpEligibilityHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FollowUpEligibilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: true),
                    ToStatus = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConsumedEncounterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FollowUpEligibilityHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FollowUpEligibilityHistories_FollowUpEligibilities_FollowUpEligibilityId",
                        column: x => x.FollowUpEligibilityId,
                        principalTable: "FollowUpEligibilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_FollowUpEligibilityId",
                table: "Tickets",
                column: "FollowUpEligibilityId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_FollowUpEligibilityId",
                table: "Reservations",
                column: "FollowUpEligibilityId");

            migrationBuilder.CreateIndex(
                name: "UX_Diagnoses_ActivePrimary",
                table: "Diagnoses",
                column: "MedicalEncounterId",
                unique: true,
                filter: "[Type] = 1 AND [IsVoided] = 0");

            migrationBuilder.CreateIndex(
                name: "UX_Diagnoses_ActiveText",
                table: "Diagnoses",
                columns: new[] { "MedicalEncounterId", "NormalizedDisplayText" },
                unique: true,
                filter: "[IsVoided] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_EncounterAmendmentChanges_EncounterAmendmentId",
                table: "EncounterAmendmentChanges",
                column: "EncounterAmendmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EncounterAmendments_CreatedByApplicationUserId",
                table: "EncounterAmendments",
                column: "CreatedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EncounterAmendments_CreatedByDoctorId",
                table: "EncounterAmendments",
                column: "CreatedByDoctorId");

            migrationBuilder.CreateIndex(
                name: "UX_EncounterAmendments_Sequence",
                table: "EncounterAmendments",
                columns: new[] { "MedicalEncounterId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EncounterAuditEvents_MedicalEncounterId_OccurredAtUtc",
                table: "EncounterAuditEvents",
                columns: new[] { "MedicalEncounterId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FollowUpEligibilities_ConsumedEncounterId",
                table: "FollowUpEligibilities",
                column: "ConsumedEncounterId",
                unique: true,
                filter: "[ConsumedEncounterId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FollowUpEligibilities_DoctorId_DoctorPracticeId_Status_ValidUntil",
                table: "FollowUpEligibilities",
                columns: new[] { "DoctorId", "DoctorPracticeId", "Status", "ValidUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_FollowUpEligibilities_DoctorPracticeId",
                table: "FollowUpEligibilities",
                column: "DoctorPracticeId");

            migrationBuilder.CreateIndex(
                name: "IX_FollowUpEligibilities_PatientId_Status_ValidUntil",
                table: "FollowUpEligibilities",
                columns: new[] { "PatientId", "Status", "ValidUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_FollowUpEligibilities_ReservedReservationId",
                table: "FollowUpEligibilities",
                column: "ReservedReservationId",
                unique: true,
                filter: "[ReservedReservationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FollowUpEligibilities_ReservedTicketId",
                table: "FollowUpEligibilities",
                column: "ReservedTicketId",
                unique: true,
                filter: "[ReservedTicketId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_FollowUpEligibilities_Source",
                table: "FollowUpEligibilities",
                column: "SourceMedicalEncounterId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FollowUpEligibilityHistories_FollowUpEligibilityId_OccurredAtUtc",
                table: "FollowUpEligibilityHistories",
                columns: new[] { "FollowUpEligibilityId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MedicalEncounters_CreatedByApplicationUserId",
                table: "MedicalEncounters",
                column: "CreatedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicalEncounters_DoctorId_DoctorPracticeId_Status_StartedAtUtc",
                table: "MedicalEncounters",
                columns: new[] { "DoctorId", "DoctorPracticeId", "Status", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MedicalEncounters_DoctorPracticeId",
                table: "MedicalEncounters",
                column: "DoctorPracticeId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicalEncounters_ModifiedByApplicationUserId",
                table: "MedicalEncounters",
                column: "ModifiedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicalEncounters_PatientId_Status_CompletedAtUtc",
                table: "MedicalEncounters",
                columns: new[] { "PatientId", "Status", "CompletedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_MedicalEncounters_TicketId",
                table: "MedicalEncounters",
                column: "TicketId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Reservations_FollowUpEligibilities_FollowUpEligibilityId",
                table: "Reservations",
                column: "FollowUpEligibilityId",
                principalTable: "FollowUpEligibilities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_FollowUpEligibilities_FollowUpEligibilityId",
                table: "Tickets",
                column: "FollowUpEligibilityId",
                principalTable: "FollowUpEligibilities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reservations_FollowUpEligibilities_FollowUpEligibilityId",
                table: "Reservations");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_FollowUpEligibilities_FollowUpEligibilityId",
                table: "Tickets");

            migrationBuilder.DropTable(
                name: "Diagnoses");

            migrationBuilder.DropTable(
                name: "EncounterAmendmentChanges");

            migrationBuilder.DropTable(
                name: "EncounterAuditEvents");

            migrationBuilder.DropTable(
                name: "FollowUpEligibilityHistories");

            migrationBuilder.DropTable(
                name: "EncounterAmendments");

            migrationBuilder.DropTable(
                name: "FollowUpEligibilities");

            migrationBuilder.DropTable(
                name: "MedicalEncounters");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_FollowUpEligibilityId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Reservations_FollowUpEligibilityId",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "FollowUpEligibilityId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "FollowUpEligibilityId",
                table: "Reservations");
        }
    }
}
