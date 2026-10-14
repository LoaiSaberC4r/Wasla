using System;
using Microsoft.EntityFrameworkCore.Migrations;

#pragma warning disable CA1861 // Generated schema arrays.

#nullable disable

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase18ReceptionHandovers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_Tickets_DoctorPracticeId_Id",
                table: "Tickets",
                columns: new[] { "DoctorPracticeId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Reservations_DoctorPracticeId_Id",
                table: "Reservations",
                columns: new[] { "DoctorPracticeId", "Id" });

            migrationBuilder.CreateTable(
                name: "ReceptionHandovers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorPracticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PreviousHandoverId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(max)", maxLength: 12000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcknowledgedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AcknowledgedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClosedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosureOutcome = table.Column<string>(type: "nvarchar(max)", maxLength: 12000, nullable: true),
                    HistorySequence = table.Column<int>(type: "int", nullable: false),
                    ModifiedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceptionHandovers", x => x.Id);
                    table.UniqueConstraint("AK_ReceptionHandovers_DoctorPracticeId_Id", x => new { x.DoctorPracticeId, x.Id });
                    table.CheckConstraint("CK_Handover_Status", "[Status] IN (1,2,3)");
                    table.ForeignKey(
                        name: "FK_ReceptionHandovers_ApplicationUsers_AcknowledgedByApplicationUserId",
                        column: x => x.AcknowledgedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceptionHandovers_ApplicationUsers_ClosedByApplicationUserId",
                        column: x => x.ClosedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceptionHandovers_ApplicationUsers_CreatedByApplicationUserId",
                        column: x => x.CreatedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceptionHandovers_DoctorPractices_DoctorId_DoctorPracticeId",
                        columns: x => new { x.DoctorId, x.DoctorPracticeId },
                        principalTable: "DoctorPractices",
                        principalColumns: new[] { "DoctorId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceptionHandovers_ReceptionHandovers_DoctorPracticeId_PreviousHandoverId",
                        columns: x => new { x.DoctorPracticeId, x.PreviousHandoverId },
                        principalTable: "ReceptionHandovers",
                        principalColumns: new[] { "DoctorPracticeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceptionHandovers_Reservations_DoctorPracticeId_ReservationId",
                        columns: x => new { x.DoctorPracticeId, x.ReservationId },
                        principalTable: "Reservations",
                        principalColumns: new[] { "DoctorPracticeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceptionHandovers_Tickets_DoctorPracticeId_TicketId",
                        columns: x => new { x.DoctorPracticeId, x.TicketId },
                        principalTable: "Tickets",
                        principalColumns: new[] { "DoctorPracticeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReceptionHandoverHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HandoverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ActorApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", maxLength: 12000, nullable: true),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceptionHandoverHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceptionHandoverHistories_ApplicationUsers_ActorApplicationUserId",
                        column: x => x.ActorApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceptionHandoverHistories_ReceptionHandovers_HandoverId",
                        column: x => x.HandoverId,
                        principalTable: "ReceptionHandovers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReceptionHandoverHistories_ActorApplicationUserId",
                table: "ReceptionHandoverHistories",
                column: "ActorApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceptionHandoverHistories_HandoverId_SequenceNumber",
                table: "ReceptionHandoverHistories",
                columns: new[] { "HandoverId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReceptionHandovers_AcknowledgedByApplicationUserId",
                table: "ReceptionHandovers",
                column: "AcknowledgedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceptionHandovers_ClosedByApplicationUserId",
                table: "ReceptionHandovers",
                column: "ClosedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceptionHandovers_CreatedByApplicationUserId",
                table: "ReceptionHandovers",
                column: "CreatedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceptionHandovers_DoctorId_DoctorPracticeId",
                table: "ReceptionHandovers",
                columns: new[] { "DoctorId", "DoctorPracticeId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceptionHandovers_DoctorPracticeId_PreviousHandoverId",
                table: "ReceptionHandovers",
                columns: new[] { "DoctorPracticeId", "PreviousHandoverId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceptionHandovers_DoctorPracticeId_ReservationId",
                table: "ReceptionHandovers",
                columns: new[] { "DoctorPracticeId", "ReservationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceptionHandovers_DoctorPracticeId_Status_CreatedOnUtc",
                table: "ReceptionHandovers",
                columns: new[] { "DoctorPracticeId", "Status", "CreatedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceptionHandovers_DoctorPracticeId_TicketId",
                table: "ReceptionHandovers",
                columns: new[] { "DoctorPracticeId", "TicketId" });
            migrationBuilder.Sql("CREATE TRIGGER [TR_Immutable_ReceptionHandoverHistories] ON [ReceptionHandoverHistories] AFTER UPDATE, DELETE AS THROW 51018, 'Handover history is immutable.', 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReceptionHandoverHistories");

            migrationBuilder.DropTable(
                name: "ReceptionHandovers");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Tickets_DoctorPracticeId_Id",
                table: "Tickets");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Reservations_DoctorPracticeId_Id",
                table: "Reservations");
        }
    }
}
