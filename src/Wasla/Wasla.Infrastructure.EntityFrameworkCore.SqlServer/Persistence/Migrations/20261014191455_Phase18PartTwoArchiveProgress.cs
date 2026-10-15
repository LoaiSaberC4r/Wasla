#pragma warning disable CA1861 // EF-generated one-time migration arrays.
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase18PartTwoArchiveProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM dbo.ArchiveControlStates) INSERT dbo.ArchiveControlStates (Id,Paused,PausedAtUtc,PausedByActorId,EventKeySecret) VALUES ('18020000-0000-0000-0000-000000000001',0,NULL,NULL,CRYPT_GEN_RANDOM(32));");

            migrationBuilder.DropIndex(
                name: "UX_Tickets_OpenPatientPractice",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_ArchiveControlRequests_ActorId_IdempotencyKey",
                table: "ArchiveControlRequests");

            migrationBuilder.AddColumn<bool>(
                name: "IsOperationallyOpen",
                schema: "archive",
                table: "Tickets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "OperationalFinalizedAtUtc",
                schema: "archive",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOperationallyOpen",
                table: "Tickets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "OperationalFinalizedAtUtc",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ArchiveSequence",
                table: "TicketRegistry",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ArchiveSequence",
                table: "ReservationRegistry",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EvidenceVerified",
                table: "PracticeOperationalDays",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "CanPause",
                table: "ArchiveControlRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanResume",
                table: "ArchiveControlRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Operation",
                table: "ArchiveControlRequests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "ArchiveAuditEvents",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FailureCode",
                table: "ArchiveAuditEvents",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ArchiveProgress",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    LastDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LastId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Mode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Sequence = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchiveProgress", x => x.Id);
                });

            migrationBuilder.Sql("UPDATE dbo.Tickets SET IsOperationallyOpen=CASE WHEN Status IN (1,2,3,4) THEN 1 ELSE 0 END; UPDATE dbo.ArchiveControlRequests SET Operation=CASE WHEN Paused=1 THEN N'Pause' ELSE N'Resume' END;");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM dbo.TicketRegistry WHERE IsArchived=1) OR EXISTS (SELECT 1 FROM dbo.ReservationRegistry WHERE IsArchived=1) THROW 51000, 'Archive.EarlyActivationRequiresReview', 1;");
            migrationBuilder.Sql("ALTER TABLE archive.Tickets ADD CONSTRAINT CK_ArchiveTickets_Terminal CHECK (Status IN (4,5,6) AND IsOperationallyOpen=0); ALTER TABLE archive.Reservations ADD CONSTRAINT CK_ArchiveReservations_Terminal CHECK (Status IN (2,3,4,5));");
            migrationBuilder.CreateIndex(
                name: "IX_Tickets_PatientId_BusinessDate_Id",
                schema: "archive",
                table: "Tickets",
                columns: new[] { "PatientId", "BusinessDate", "Id" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ArchivedTickets_CK_Tickets_Number",
                schema: "archive",
                table: "Tickets",
                sql: "[TicketNumber] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ArchivedTickets_CK_Tickets_OperationalLease",
                schema: "archive",
                table: "Tickets",
                sql: "([Status] IN (1,2,3) AND [IsOperationallyOpen]=1) OR ([Status] IN (5,6) AND [IsOperationallyOpen]=0) OR [Status]=4");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ArchivedTickets_CK_Tickets_Price",
                schema: "archive",
                table: "Tickets",
                sql: "[PriceSnapshot] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ArchivedTickets_CK_Tickets_ReservationSource",
                schema: "archive",
                table: "Tickets",
                sql: "([Source] = 1 AND [ReservationId] IS NOT NULL) OR ([Source] = 2 AND [ReservationId] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ArchivedTickets_CK_Tickets_Source",
                schema: "archive",
                table: "Tickets",
                sql: "[Source] IN (1,2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ArchivedTickets_CK_Tickets_Status",
                schema: "archive",
                table: "Tickets",
                sql: "[Status] IN (1,2,3,4,5,6)");

            migrationBuilder.CreateIndex(
                name: "UX_Tickets_OpenPatientPractice",
                table: "Tickets",
                columns: new[] { "PatientId", "DoctorPracticeId" },
                unique: true,
                filter: "[IsOperationallyOpen] = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Tickets_OperationalLease",
                table: "Tickets",
                sql: "([Status] IN (1,2,3) AND [IsOperationallyOpen]=1) OR ([Status] IN (5,6) AND [IsOperationallyOpen]=0) OR [Status]=4");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ArchivedTicketCallAttempts_CK_TicketCallAttempts_Cycle",
                schema: "archive",
                table: "TicketCallAttempts",
                sql: "[CallCycle] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ArchivedTicketCallAttempts_CK_TicketCallAttempts_Number",
                schema: "archive",
                table: "TicketCallAttempts",
                sql: "[AttemptNumber] > 0");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_PatientId_BusinessDate_Id",
                schema: "archive",
                table: "Reservations",
                columns: new[] { "PatientId", "BusinessDate", "Id" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ArchivedReservations_CK_Reservations_Duration",
                schema: "archive",
                table: "Reservations",
                sql: "[SlotDurationMinutesSnapshot] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ArchivedReservations_CK_Reservations_Price",
                schema: "archive",
                table: "Reservations",
                sql: "[PriceSnapshot] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ArchivedReservations_CK_Reservations_RescheduleCount",
                schema: "archive",
                table: "Reservations",
                sql: "[PatientInitiatedRescheduleCount] >= 0 AND [PatientInitiatedRescheduleCount] <= 2");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ArchivedReservations_CK_Reservations_Status",
                schema: "archive",
                table: "Reservations",
                sql: "[Status] IN (1,2,3,4,5)");

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveControlRequests_ActorId_Operation_IdempotencyKey",
                table: "ArchiveControlRequests",
                columns: new[] { "ActorId", "Operation", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new InvalidOperationException("Archive progress/liveness reversal requires a validated recovery plan.");
        }
    }
}
