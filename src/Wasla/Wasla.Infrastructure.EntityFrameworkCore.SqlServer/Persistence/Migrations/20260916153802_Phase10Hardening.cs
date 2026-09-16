using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase10Hardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReservationHistories_Reservations_ReservationId",
                table: "ReservationHistories");

            migrationBuilder.Sql(
                """
                DECLARE @LegacyPermissionId uniqueidentifier = '20000000-0000-0000-0000-000000000073';

                INSERT INTO [ReceptionPracticeAssignmentPermissions] ([Id], [ReceptionPracticeAssignmentId], [PermissionId])
                SELECT NEWID(), legacy.[ReceptionPracticeAssignmentId], granular.[PermissionId]
                FROM [ReceptionPracticeAssignmentPermissions] legacy
                CROSS APPLY (VALUES
                    (CONVERT(uniqueidentifier, '20000000-0000-0000-0000-000000000090')),
                    (CONVERT(uniqueidentifier, '20000000-0000-0000-0000-000000000091')),
                    (CONVERT(uniqueidentifier, '20000000-0000-0000-0000-000000000092')),
                    (CONVERT(uniqueidentifier, '20000000-0000-0000-0000-000000000093')),
                    (CONVERT(uniqueidentifier, '20000000-0000-0000-0000-000000000094'))
                ) granular([PermissionId])
                WHERE legacy.[PermissionId] = @LegacyPermissionId
                  AND EXISTS (SELECT 1 FROM [Permissions] permission WHERE permission.[Id] = granular.[PermissionId])
                  AND NOT EXISTS (
                      SELECT 1
                      FROM [ReceptionPracticeAssignmentPermissions] existing
                      WHERE existing.[ReceptionPracticeAssignmentId] = legacy.[ReceptionPracticeAssignmentId]
                        AND existing.[PermissionId] = granular.[PermissionId]);

                DELETE FROM [ReceptionPracticeAssignmentPermissions]
                WHERE [PermissionId] = @LegacyPermissionId;
                """);

            migrationBuilder.CreateTable(
                name: "ReservationProjectionInvalidations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    PracticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RefreshAvailability = table.Column<bool>(type: "bit", nullable: false),
                    RefreshPopularity = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    NextAttemptOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProcessingToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationProjectionInvalidations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReservationProjectionInvalidations_Pending",
                table: "ReservationProjectionInvalidations",
                columns: ["ProcessedOnUtc", "NextAttemptOnUtc"]);

            migrationBuilder.CreateIndex(
                name: "UX_ReservationProjectionInvalidations_IdempotencyKey",
                table: "ReservationProjectionInvalidations",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ReservationHistories_Reservations_ReservationId",
                table: "ReservationHistories",
                column: "ReservationId",
                principalTable: "Reservations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReservationHistories_Reservations_ReservationId",
                table: "ReservationHistories");

            migrationBuilder.DropTable(
                name: "ReservationProjectionInvalidations");

            migrationBuilder.AddForeignKey(
                name: "FK_ReservationHistories_Reservations_ReservationId",
                table: "ReservationHistories",
                column: "ReservationId",
                principalTable: "Reservations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
