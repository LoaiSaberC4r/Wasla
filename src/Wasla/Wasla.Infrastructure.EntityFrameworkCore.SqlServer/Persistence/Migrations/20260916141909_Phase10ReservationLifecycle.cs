using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // EF-generated migration index column arrays.

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase10ReservationLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NoShowAfterPassedPatientsCount",
                table: "DoctorPracticeConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.Sql(
                """
                DECLARE @Now datetime2 = SYSUTCDATETIME();
                DECLARE @NewPermissions TABLE (Id uniqueidentifier, Name nvarchar(200), RoleId uniqueidentifier);
                INSERT INTO @NewPermissions (Id, Name, RoleId) VALUES
                ('20000000-0000-0000-0000-000000000079', 'Reservations.ViewOwn', '10000000-0000-0000-0000-000000000004'),
                ('20000000-0000-0000-0000-000000000080', 'Reservations.CreateOwn', '10000000-0000-0000-0000-000000000004'),
                ('20000000-0000-0000-0000-000000000081', 'Reservations.CancelOwn', '10000000-0000-0000-0000-000000000004'),
                ('20000000-0000-0000-0000-000000000082', 'Reservations.RescheduleOwn', '10000000-0000-0000-0000-000000000004'),
                ('20000000-0000-0000-0000-000000000083', 'Reservations.ViewDependents', '10000000-0000-0000-0000-000000000004'),
                ('20000000-0000-0000-0000-000000000084', 'Reservations.CreateDependents', '10000000-0000-0000-0000-000000000004'),
                ('20000000-0000-0000-0000-000000000085', 'Reservations.CancelDependents', '10000000-0000-0000-0000-000000000004'),
                ('20000000-0000-0000-0000-000000000086', 'Reservations.RescheduleDependents', '10000000-0000-0000-0000-000000000004'),
                ('20000000-0000-0000-0000-000000000087', 'DoctorPracticeReservations.ViewOwn', '10000000-0000-0000-0000-000000000002'),
                ('20000000-0000-0000-0000-000000000088', 'DoctorPracticeReservations.CancelOwn', '10000000-0000-0000-0000-000000000002'),
                ('20000000-0000-0000-0000-000000000089', 'DoctorPracticeReservations.RescheduleOwn', '10000000-0000-0000-0000-000000000002'),
                ('20000000-0000-0000-0000-000000000090', 'PracticeReservations.View', '10000000-0000-0000-0000-000000000003'),
                ('20000000-0000-0000-0000-000000000091', 'PracticeReservations.Create', '10000000-0000-0000-0000-000000000003'),
                ('20000000-0000-0000-0000-000000000092', 'PracticeReservations.Cancel', '10000000-0000-0000-0000-000000000003'),
                ('20000000-0000-0000-0000-000000000093', 'PracticeReservations.Reschedule', '10000000-0000-0000-0000-000000000003'),
                ('20000000-0000-0000-0000-000000000094', 'PracticeReservations.RestoreNoShow', '10000000-0000-0000-0000-000000000003'),
                ('20000000-0000-0000-0000-000000000095', 'Reservations.ViewAdministrative', '10000000-0000-0000-0000-000000000001');

                INSERT INTO Permissions (Id, Name, IsSystemPermission, CreatedByApplicationUserId, CreatedOnUtc, ModifiedOnUtc)
                SELECT source.Id, source.Name, 1, NULL, @Now, NULL
                FROM @NewPermissions source
                WHERE NOT EXISTS (SELECT 1 FROM Permissions target WHERE target.Id = source.Id OR target.Name = source.Name);

                INSERT INTO RolePermissions (Id, RoleId, PermissionId, CreatedByApplicationUserId, CreatedOnUtc, ModifiedOnUtc)
                SELECT NEWID(), source.RoleId, source.Id, NULL, @Now, NULL
                FROM @NewPermissions source
                WHERE EXISTS (SELECT 1 FROM Roles role WHERE role.Id = source.RoleId)
                  AND EXISTS (SELECT 1 FROM Permissions permission WHERE permission.Id = source.Id)
                  AND NOT EXISTS (
                      SELECT 1 FROM RolePermissions mapping
                      WHERE mapping.RoleId = source.RoleId AND mapping.PermissionId = source.Id);

                DECLARE @LegacyPermissionId uniqueidentifier = '20000000-0000-0000-0000-000000000073';
                INSERT INTO ReceptionPracticeAssignmentPermissions
                    (Id, AssignmentId, PermissionId, CreatedByApplicationUserId, CreatedOnUtc)
                SELECT NEWID(), legacy.AssignmentId, granular.Id, assignment.CreatedByApplicationUserId, @Now
                FROM ReceptionPracticeAssignmentPermissions legacy
                INNER JOIN ReceptionPracticeAssignments assignment ON assignment.Id = legacy.AssignmentId
                CROSS JOIN @NewPermissions granular
                WHERE legacy.PermissionId = @LegacyPermissionId
                  AND granular.Id IN (
                    '20000000-0000-0000-0000-000000000090',
                    '20000000-0000-0000-0000-000000000091',
                    '20000000-0000-0000-0000-000000000092',
                    '20000000-0000-0000-0000-000000000093',
                    '20000000-0000-0000-0000-000000000094')
                  AND NOT EXISTS (
                    SELECT 1 FROM ReceptionPracticeAssignmentPermissions existing
                    WHERE existing.AssignmentId = legacy.AssignmentId
                      AND existing.PermissionId = granular.Id);
                """);

            migrationBuilder.CreateTable(
                name: "Reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservationReference = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorPracticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SegmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VisitTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reservations", x => x.Id);
                    table.CheckConstraint("CK_Reservations_Duration", "[SlotDurationMinutesSnapshot] > 0");
                    table.CheckConstraint("CK_Reservations_Price", "[PriceSnapshot] > 0");
                    table.CheckConstraint("CK_Reservations_RescheduleCount", "[PatientInitiatedRescheduleCount] >= 0 AND [PatientInitiatedRescheduleCount] <= 2");
                    table.CheckConstraint("CK_Reservations_Status", "[Status] IN (1,2,3,4,5)");
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
                        name: "FK_Reservations_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReservationHistories",
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
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReservationIdempotencyRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResultReference = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationIdempotencyRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReservationIdempotencyRecords_ApplicationUsers_ActorApplicationUserId",
                        column: x => x.ActorApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservationIdempotencyRecords_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReservationHistories_PerformedByApplicationUserId",
                table: "ReservationHistories",
                column: "PerformedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationHistories_ReservationId_OccurredOnUtc",
                table: "ReservationHistories",
                columns: new[] { "ReservationId", "OccurredOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReservationIdempotencyRecords_ReservationId",
                table: "ReservationIdempotencyRecords",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "UX_ReservationIdempotency_ActorOperationKey",
                table: "ReservationIdempotencyRecords",
                columns: new[] { "ActorApplicationUserId", "Operation", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_CancelledByApplicationUserId",
                table: "Reservations",
                column: "CancelledByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_CreatedByApplicationUserId",
                table: "Reservations",
                column: "CreatedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_DoctorId_ConvertedToTicketOnUtc",
                table: "Reservations",
                columns: new[] { "DoctorId", "ConvertedToTicketOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_DoctorId_Status_ScheduledStartUtc",
                table: "Reservations",
                columns: new[] { "DoctorId", "Status", "ScheduledStartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_DoctorPracticeId_BusinessDate_SegmentId_Status",
                table: "Reservations",
                columns: new[] { "DoctorPracticeId", "BusinessDate", "SegmentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_DoctorPracticeId_BusinessDate_Status",
                table: "Reservations",
                columns: new[] { "DoctorPracticeId", "BusinessDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_ModifiedByApplicationUserId",
                table: "Reservations",
                column: "ModifiedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_PatientId_Status_ScheduledStartUtc",
                table: "Reservations",
                columns: new[] { "PatientId", "Status", "ScheduledStartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_SegmentId",
                table: "Reservations",
                column: "SegmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_Status_BusinessDate",
                table: "Reservations",
                columns: new[] { "Status", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_VisitTypeId",
                table: "Reservations",
                column: "VisitTypeId");

            migrationBuilder.CreateIndex(
                name: "UX_Reservations_ActivePatientPracticeDate",
                table: "Reservations",
                columns: new[] { "PatientId", "DoctorPracticeId", "BusinessDate" },
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "UX_Reservations_ConsumingPracticeSlot",
                table: "Reservations",
                columns: new[] { "DoctorPracticeId", "ScheduledStartUtc" },
                unique: true,
                filter: "[Status] IN (1,5)");

            migrationBuilder.CreateIndex(
                name: "UX_Reservations_Reference",
                table: "Reservations",
                column: "ReservationReference",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM ReceptionPracticeAssignmentPermissions
                WHERE PermissionId IN (
                    '20000000-0000-0000-0000-000000000090',
                    '20000000-0000-0000-0000-000000000091',
                    '20000000-0000-0000-0000-000000000092',
                    '20000000-0000-0000-0000-000000000093',
                    '20000000-0000-0000-0000-000000000094');
                DELETE FROM RolePermissions
                WHERE PermissionId IN (
                    SELECT Id FROM Permissions WHERE Name IN (
                        'Reservations.ViewOwn', 'Reservations.CreateOwn', 'Reservations.CancelOwn',
                        'Reservations.RescheduleOwn', 'Reservations.ViewDependents', 'Reservations.CreateDependents',
                        'Reservations.CancelDependents', 'Reservations.RescheduleDependents',
                        'DoctorPracticeReservations.ViewOwn', 'DoctorPracticeReservations.CancelOwn',
                        'DoctorPracticeReservations.RescheduleOwn', 'PracticeReservations.View',
                        'PracticeReservations.Create', 'PracticeReservations.Cancel',
                        'PracticeReservations.Reschedule', 'PracticeReservations.RestoreNoShow',
                        'Reservations.ViewAdministrative'));
                DELETE FROM Permissions
                WHERE Name IN (
                    'Reservations.ViewOwn', 'Reservations.CreateOwn', 'Reservations.CancelOwn',
                    'Reservations.RescheduleOwn', 'Reservations.ViewDependents', 'Reservations.CreateDependents',
                    'Reservations.CancelDependents', 'Reservations.RescheduleDependents',
                    'DoctorPracticeReservations.ViewOwn', 'DoctorPracticeReservations.CancelOwn',
                    'DoctorPracticeReservations.RescheduleOwn', 'PracticeReservations.View',
                    'PracticeReservations.Create', 'PracticeReservations.Cancel',
                    'PracticeReservations.Reschedule', 'PracticeReservations.RestoreNoShow',
                    'Reservations.ViewAdministrative');
                """);

            migrationBuilder.DropTable(
                name: "ReservationHistories");

            migrationBuilder.DropTable(
                name: "ReservationIdempotencyRecords");

            migrationBuilder.DropTable(
                name: "Reservations");

            migrationBuilder.DropColumn(
                name: "NoShowAfterPassedPatientsCount",
                table: "DoctorPracticeConfigurations");
        }
    }
}
