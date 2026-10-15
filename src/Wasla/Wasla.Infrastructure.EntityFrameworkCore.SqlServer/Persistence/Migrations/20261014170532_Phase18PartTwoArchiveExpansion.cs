#pragma warning disable CA1861 // EF-generated migration arrays are executed once.
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase18PartTwoArchiveExpansion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "archive");

            migrationBuilder.CreateTable(
                name: "ArchiveAuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DoctorPracticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ReasonHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchiveAuditEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ArchiveControlRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Paused = table.Column<bool>(type: "bit", nullable: false),
                    PausedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PausedByActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResultRowVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchiveControlRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ArchiveControlStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Paused = table.Column<bool>(type: "bit", nullable: false),
                    PausedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PausedByActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchiveControlStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ArchiveRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Scanned = table.Column<int>(type: "int", nullable: false),
                    ArchivedTickets = table.Column<int>(type: "int", nullable: false),
                    ArchivedReservations = table.Column<int>(type: "int", nullable: false),
                    Excluded = table.Column<int>(type: "int", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchiveRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PracticeOperationalDays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorPracticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ObservedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EvidenceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PracticeOperationalDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PracticeOperationalDays_DoctorPractices_DoctorPracticeId",
                        column: x => x.DoctorPracticeId,
                        principalTable: "DoctorPractices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReservationRegistry",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorPracticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservationReference = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    ArchivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Provenance = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationRegistry", x => x.Id);
                    table.UniqueConstraint("AK_ReservationRegistry_DoctorPracticeId_Id", x => new { x.DoctorPracticeId, x.Id });
                    table.CheckConstraint("CK_ReservationRegistry_Location", "([IsArchived] = 0 AND [ArchivedAtUtc] IS NULL) OR ([IsArchived] = 1 AND [ArchivedAtUtc] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_ReservationRegistry_DoctorPractices_DoctorPracticeId",
                        column: x => x.DoctorPracticeId,
                        principalTable: "DoctorPractices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservationRegistry_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservationRegistry_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ArchiveIntegrityIssues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorPracticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FailureCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    StateFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchiveIntegrityIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchiveIntegrityIssues_ArchiveRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "ArchiveRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Reservations",
                schema: "archive",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservationReference = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorPracticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SegmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VisitTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FollowUpEligibilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BookingSource = table.Column<int>(type: "int", nullable: false),
                    CreatedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookedOnBehalfRelationshipSnapshot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ScheduledStartUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ScheduledLocalDateTime = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TimeZoneIdSnapshot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SlotDurationMinutesSnapshot = table.Column<int>(type: "int", nullable: false),
                    SegmentNameArSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SegmentNameEnSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SegmentPrioritySnapshot = table.Column<int>(type: "int", nullable: false),
                    VisitTypeCodeSnapshot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    VisitTypeNameArSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    VisitTypeNameEnSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PriceSnapshot = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CheckInGracePeriodMinutesSnapshot = table.Column<int>(type: "int", nullable: true),
                    BookingNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PatientInitiatedRescheduleCount = table.Column<int>(type: "int", nullable: false),
                    LastRescheduledOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancellationInitiator = table.Column<int>(type: "int", nullable: true),
                    CancellationReasonCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CancellationComment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CancelledByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancelledOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastNoShowOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastNoShowRestoredOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiredOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConvertedToTicketOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reservations_ApplicationUsers_CancelledByApplicationUserId",
                        column: x => x.CancelledByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reservations_ApplicationUsers_CreatedByApplicationUserId",
                        column: x => x.CreatedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reservations_ApplicationUsers_ModifiedByApplicationUserId",
                        column: x => x.ModifiedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reservations_DoctorPracticeSegments_SegmentId",
                        column: x => x.SegmentId,
                        principalTable: "DoctorPracticeSegments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reservations_DoctorPracticeVisitTypes_VisitTypeId",
                        column: x => x.VisitTypeId,
                        principalTable: "DoctorPracticeVisitTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reservations_DoctorPractices_DoctorPracticeId",
                        column: x => x.DoctorPracticeId,
                        principalTable: "DoctorPractices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reservations_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reservations_FollowUpEligibilities_FollowUpEligibilityId",
                        column: x => x.FollowUpEligibilityId,
                        principalTable: "FollowUpEligibilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reservations_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reservations_ReservationRegistry_Id",
                        column: x => x.Id,
                        principalTable: "ReservationRegistry",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TicketRegistry",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorPracticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TicketNumber = table.Column<int>(type: "int", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    ArchivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Provenance = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketRegistry", x => x.Id);
                    table.UniqueConstraint("AK_TicketRegistry_DoctorPracticeId_Id", x => new { x.DoctorPracticeId, x.Id });
                    table.CheckConstraint("CK_TicketRegistry_Location", "([IsArchived] = 0 AND [ArchivedAtUtc] IS NULL) OR ([IsArchived] = 1 AND [ArchivedAtUtc] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_TicketRegistry_DoctorPractices_DoctorPracticeId",
                        column: x => x.DoctorPracticeId,
                        principalTable: "DoctorPractices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketRegistry_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketRegistry_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketRegistry_ReservationRegistry_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "ReservationRegistry",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReservationHistories",
                schema: "archive",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<int>(type: "int", nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: true),
                    ToStatus = table.Column<int>(type: "int", nullable: true),
                    ActionInitiator = table.Column<int>(type: "int", nullable: false),
                    PerformedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OccurredOnUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OldBusinessDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OldScheduledLocalDateTime = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    OldScheduledStartUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    OldSlotDurationMinutes = table.Column<int>(type: "int", nullable: true),
                    NewBusinessDate = table.Column<DateOnly>(type: "date", nullable: true),
                    NewScheduledLocalDateTime = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    NewScheduledStartUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    NewSlotDurationMinutes = table.Column<int>(type: "int", nullable: true),
                    PatientConsentConfirmed = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReservationHistories_ApplicationUsers_PerformedByApplicationUserId",
                        column: x => x.PerformedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservationHistories_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalSchema: "archive",
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Tickets",
                schema: "archive",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorPracticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TicketNumber = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    SegmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SegmentNameArSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SegmentNameEnSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SegmentPrioritySnapshot = table.Column<int>(type: "int", nullable: false),
                    VisitTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FollowUpEligibilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VisitTypeCodeSnapshot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    VisitTypeNameArSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    VisitTypeNameEnSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PriceSnapshot = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PracticeTimeZoneIdSnapshot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CheckInTimeUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    QueueOrderTimeUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    CheckInMode = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IsFastTrack = table.Column<bool>(type: "bit", nullable: false),
                    FastTrackGrantedOnUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CallCycle = table.Column<int>(type: "int", nullable: false),
                    CalledOnUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    NoShowOnUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    InProgressOnUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CompletedOnUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CancelledOnUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CancellationReasonCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastUpdatedOnUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tickets_ApplicationUsers_CreatedByApplicationUserId",
                        column: x => x.CreatedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tickets_ApplicationUsers_ModifiedByApplicationUserId",
                        column: x => x.ModifiedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tickets_DoctorPracticeSegments_SegmentId",
                        column: x => x.SegmentId,
                        principalTable: "DoctorPracticeSegments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tickets_DoctorPracticeVisitTypes_VisitTypeId",
                        column: x => x.VisitTypeId,
                        principalTable: "DoctorPracticeVisitTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tickets_DoctorPractices_DoctorPracticeId",
                        column: x => x.DoctorPracticeId,
                        principalTable: "DoctorPractices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tickets_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tickets_FollowUpEligibilities_FollowUpEligibilityId",
                        column: x => x.FollowUpEligibilityId,
                        principalTable: "FollowUpEligibilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tickets_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tickets_ReservationRegistry_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "ReservationRegistry",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tickets_TicketRegistry_Id",
                        column: x => x.Id,
                        principalTable: "TicketRegistry",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TicketCallAttempts",
                schema: "archive",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CallCycle = table.Column<int>(type: "int", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    CalledOnUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    OutcomeRecordedOnUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketCallAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketCallAttempts_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalSchema: "archive",
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TicketHistories",
                schema: "archive",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<int>(type: "int", nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: true),
                    ToStatus = table.Column<int>(type: "int", nullable: true),
                    ActorApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OccurredOnUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketHistories_ApplicationUsers_ActorApplicationUserId",
                        column: x => x.ActorApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketHistories_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalSchema: "archive",
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveAuditEvents_ActorId_OccurredAtUtc_Id",
                table: "ArchiveAuditEvents",
                columns: new[] { "ActorId", "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveAuditEvents_DoctorPracticeId_OccurredAtUtc_Id",
                table: "ArchiveAuditEvents",
                columns: new[] { "DoctorPracticeId", "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveControlRequests_ActorId_IdempotencyKey",
                table: "ArchiveControlRequests",
                columns: new[] { "ActorId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveIntegrityIssues_EntityId_EntityType_FailureCode_StateFingerprint",
                table: "ArchiveIntegrityIssues",
                columns: new[] { "EntityId", "EntityType", "FailureCode", "StateFingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveIntegrityIssues_OccurredAtUtc_Id",
                table: "ArchiveIntegrityIssues",
                columns: new[] { "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveIntegrityIssues_RunId",
                table: "ArchiveIntegrityIssues",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveRuns_StartedAtUtc_Id",
                table: "ArchiveRuns",
                columns: new[] { "StartedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PracticeOperationalDays_DoctorPracticeId_BusinessDate",
                table: "PracticeOperationalDays",
                columns: new[] { "DoctorPracticeId", "BusinessDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedReservationHistories_PerformedByApplicationUserId",
                schema: "archive",
                table: "ReservationHistories",
                column: "PerformedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedReservationHistories_ReservationId_OccurredOnUtc",
                schema: "archive",
                table: "ReservationHistories",
                columns: new[] { "ReservationId", "OccurredOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReservationRegistry_DoctorId",
                table: "ReservationRegistry",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationRegistry_PatientId",
                table: "ReservationRegistry",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationRegistry_ReservationReference",
                table: "ReservationRegistry",
                column: "ReservationReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedReservations_CancelledByApplicationUserId",
                schema: "archive",
                table: "Reservations",
                column: "CancelledByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedReservations_CreatedByApplicationUserId",
                schema: "archive",
                table: "Reservations",
                column: "CreatedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedReservations_DoctorId_ConvertedToTicketOnUtc",
                schema: "archive",
                table: "Reservations",
                columns: new[] { "DoctorId", "ConvertedToTicketOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedReservations_DoctorId_Status_ScheduledStartUtc",
                schema: "archive",
                table: "Reservations",
                columns: new[] { "DoctorId", "Status", "ScheduledStartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedReservations_DoctorPracticeId_BusinessDate_SegmentId_Status",
                schema: "archive",
                table: "Reservations",
                columns: new[] { "DoctorPracticeId", "BusinessDate", "SegmentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedReservations_DoctorPracticeId_BusinessDate_Status",
                schema: "archive",
                table: "Reservations",
                columns: new[] { "DoctorPracticeId", "BusinessDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedReservations_FollowUpEligibilityId",
                schema: "archive",
                table: "Reservations",
                column: "FollowUpEligibilityId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedReservations_ModifiedByApplicationUserId",
                schema: "archive",
                table: "Reservations",
                column: "ModifiedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedReservations_PatientId_Status_ScheduledStartUtc",
                schema: "archive",
                table: "Reservations",
                columns: new[] { "PatientId", "Status", "ScheduledStartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedReservations_ReservationReference",
                schema: "archive",
                table: "Reservations",
                column: "ReservationReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedReservations_SegmentId",
                schema: "archive",
                table: "Reservations",
                column: "SegmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedReservations_Status_BusinessDate",
                schema: "archive",
                table: "Reservations",
                columns: new[] { "Status", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedReservations_VisitTypeId",
                schema: "archive",
                table: "Reservations",
                column: "VisitTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_DoctorPracticeId_BusinessDate_Id",
                schema: "archive",
                table: "Reservations",
                columns: new[] { "DoctorPracticeId", "BusinessDate", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedTicketCallAttempts_TicketId_CallCycle_AttemptNumber",
                schema: "archive",
                table: "TicketCallAttempts",
                columns: new[] { "TicketId", "CallCycle", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedTicketHistories_ActorApplicationUserId",
                schema: "archive",
                table: "TicketHistories",
                column: "ActorApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedTicketHistories_TicketId_EventType_OccurredOnUtc",
                schema: "archive",
                table: "TicketHistories",
                columns: new[] { "TicketId", "EventType", "OccurredOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedTicketHistories_TicketId_OccurredOnUtc",
                schema: "archive",
                table: "TicketHistories",
                columns: new[] { "TicketId", "OccurredOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketRegistry_DoctorId",
                table: "TicketRegistry",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketRegistry_DoctorPracticeId_BusinessDate_TicketNumber",
                table: "TicketRegistry",
                columns: new[] { "DoctorPracticeId", "BusinessDate", "TicketNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketRegistry_PatientId",
                table: "TicketRegistry",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketRegistry_ReservationId",
                table: "TicketRegistry",
                column: "ReservationId",
                unique: true,
                filter: "[ReservationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedTickets_CreatedByApplicationUserId",
                schema: "archive",
                table: "Tickets",
                column: "CreatedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedTickets_DoctorId",
                schema: "archive",
                table: "Tickets",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedTickets_DoctorPracticeId_BusinessDate_Status",
                schema: "archive",
                table: "Tickets",
                columns: new[] { "DoctorPracticeId", "BusinessDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedTickets_DoctorPracticeId_BusinessDate_Status_IsFastTrack_SegmentPrioritySnapshot_QueueOrderTimeUtc_TicketNumber",
                schema: "archive",
                table: "Tickets",
                columns: new[] { "DoctorPracticeId", "BusinessDate", "Status", "IsFastTrack", "SegmentPrioritySnapshot", "QueueOrderTimeUtc", "TicketNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedTickets_DoctorPracticeId_BusinessDate_TicketNumber",
                schema: "archive",
                table: "Tickets",
                columns: new[] { "DoctorPracticeId", "BusinessDate", "TicketNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedTickets_FollowUpEligibilityId",
                schema: "archive",
                table: "Tickets",
                column: "FollowUpEligibilityId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedTickets_ModifiedByApplicationUserId",
                schema: "archive",
                table: "Tickets",
                column: "ModifiedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedTickets_PatientId_Status_LastUpdatedOnUtc",
                schema: "archive",
                table: "Tickets",
                columns: new[] { "PatientId", "Status", "LastUpdatedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedTickets_SegmentId",
                schema: "archive",
                table: "Tickets",
                column: "SegmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedTickets_VisitTypeId",
                schema: "archive",
                table: "Tickets",
                column: "VisitTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_DoctorPracticeId_BusinessDate_Id",
                schema: "archive",
                table: "Tickets",
                columns: new[] { "DoctorPracticeId", "BusinessDate", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_ReservationId",
                schema: "archive",
                table: "Tickets",
                column: "ReservationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new InvalidOperationException("Archive migration reversal requires a separately validated recovery plan.");
        }
    }
}
