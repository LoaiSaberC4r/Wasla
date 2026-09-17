using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase11TicketQueueRuntime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CheckInOpenBeforeMinutes",
                table: "DoctorPracticeConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<int>(
                name: "TicketNoShowReturnFastTrackLimit",
                table: "DoctorPracticeConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.CreateTable(
                name: "TicketDailyCounters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorPracticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LastNumber = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketDailyCounters", x => x.Id);
                    table.CheckConstraint("CK_TicketDailyCounters_LastNumber", "[LastNumber] >= 0");
                    table.ForeignKey(
                        name: "FK_TicketDailyCounters_DoctorPractices_DoctorPracticeId",
                        column: x => x.DoctorPracticeId,
                        principalTable: "DoctorPractices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Tickets",
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
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tickets", x => x.Id);
                    table.CheckConstraint("CK_Tickets_Number", "[TicketNumber] > 0");
                    table.CheckConstraint("CK_Tickets_Price", "[PriceSnapshot] > 0");
                    table.CheckConstraint("CK_Tickets_ReservationSource", "([Source] = 1 AND [ReservationId] IS NOT NULL) OR ([Source] = 2 AND [ReservationId] IS NULL)");
                    table.CheckConstraint("CK_Tickets_Source", "[Source] IN (1,2)");
                    table.CheckConstraint("CK_Tickets_Status", "[Status] IN (1,2,3,4,5,6)");
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
                        name: "FK_Tickets_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tickets_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorPracticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CollectedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CollectedOnUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.CheckConstraint("CK_Payments_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_Payments_Status", "[Status] = 1");
                    table.ForeignKey(
                        name: "FK_Payments_ApplicationUsers_CollectedByApplicationUserId",
                        column: x => x.CollectedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_DoctorPractices_DoctorPracticeId",
                        column: x => x.DoctorPracticeId,
                        principalTable: "DoctorPractices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TicketCallAttempts",
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
                    table.CheckConstraint("CK_TicketCallAttempts_Cycle", "[CallCycle] > 0");
                    table.CheckConstraint("CK_TicketCallAttempts_Number", "[AttemptNumber] > 0");
                    table.ForeignKey(
                        name: "FK_TicketCallAttempts_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TicketHistories",
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
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TicketIdempotencyRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketIdempotencyRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketIdempotencyRecords_ApplicationUsers_ActorApplicationUserId",
                        column: x => x.ActorApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketIdempotencyRecords_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CollectedByApplicationUserId",
                table: "Payments",
                column: "CollectedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_DoctorId",
                table: "Payments",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_DoctorPracticeId",
                table: "Payments",
                column: "DoctorPracticeId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PatientId",
                table: "Payments",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "UX_Payments_ReservationId",
                table: "Payments",
                column: "ReservationId",
                unique: true,
                filter: "[ReservationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Payments_TicketId",
                table: "Payments",
                column: "TicketId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_TicketCallAttempts_TicketCycleNumber",
                table: "TicketCallAttempts",
                columns: new[] { "TicketId", "CallCycle", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_TicketDailyCounters_PracticeDate",
                table: "TicketDailyCounters",
                columns: new[] { "DoctorPracticeId", "BusinessDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketHistories_ActorApplicationUserId",
                table: "TicketHistories",
                column: "ActorApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketHistories_TicketId_EventType_OccurredOnUtc",
                table: "TicketHistories",
                columns: new[] { "TicketId", "EventType", "OccurredOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketHistories_TicketId_OccurredOnUtc",
                table: "TicketHistories",
                columns: new[] { "TicketId", "OccurredOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketIdempotencyRecords_TicketId",
                table: "TicketIdempotencyRecords",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "UX_TicketIdempotency_ActorOperationKey",
                table: "TicketIdempotencyRecords",
                columns: new[] { "ActorApplicationUserId", "Operation", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_AuthoritativeQueueOrder",
                table: "Tickets",
                columns: new[] { "DoctorPracticeId", "BusinessDate", "Status", "IsFastTrack", "SegmentPrioritySnapshot", "QueueOrderTimeUtc", "TicketNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_CreatedByApplicationUserId",
                table: "Tickets",
                column: "CreatedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_DoctorId",
                table: "Tickets",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_DoctorPracticeId_BusinessDate_Status",
                table: "Tickets",
                columns: new[] { "DoctorPracticeId", "BusinessDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_ModifiedByApplicationUserId",
                table: "Tickets",
                column: "ModifiedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_PatientId_Status_LastUpdatedOnUtc",
                table: "Tickets",
                columns: new[] { "PatientId", "Status", "LastUpdatedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_SegmentId",
                table: "Tickets",
                column: "SegmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_VisitTypeId",
                table: "Tickets",
                column: "VisitTypeId");

            migrationBuilder.CreateIndex(
                name: "UX_Tickets_OneCalledOrInProgressPerPractice",
                table: "Tickets",
                column: "DoctorPracticeId",
                unique: true,
                filter: "[Status] IN (2,3)");

            migrationBuilder.CreateIndex(
                name: "UX_Tickets_OpenPatientPractice",
                table: "Tickets",
                columns: new[] { "PatientId", "DoctorPracticeId" },
                unique: true,
                filter: "[Status] IN (1,2,3,4)");

            migrationBuilder.CreateIndex(
                name: "UX_Tickets_PracticeDateNumber",
                table: "Tickets",
                columns: new[] { "DoctorPracticeId", "BusinessDate", "TicketNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Tickets_ReservationId",
                table: "Tickets",
                column: "ReservationId",
                unique: true,
                filter: "[ReservationId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "TicketCallAttempts");

            migrationBuilder.DropTable(
                name: "TicketDailyCounters");

            migrationBuilder.DropTable(
                name: "TicketHistories");

            migrationBuilder.DropTable(
                name: "TicketIdempotencyRecords");

            migrationBuilder.DropTable(
                name: "Tickets");

            migrationBuilder.DropColumn(
                name: "CheckInOpenBeforeMinutes",
                table: "DoctorPracticeConfigurations");

            migrationBuilder.DropColumn(
                name: "TicketNoShowReturnFastTrackLimit",
                table: "DoctorPracticeConfigurations");
        }
    }
}
