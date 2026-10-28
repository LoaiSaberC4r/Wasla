using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // EF-generated one-time migration column arrays.

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DoctorMyPatientsReadIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ArchivedTickets_DoctorId",
                schema: "archive",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_DoctorId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_TicketRegistry_DoctorId",
                table: "TicketRegistry");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedTickets_DoctorId_PatientId_CheckInTimeUtc_Id",
                schema: "archive",
                table: "Tickets",
                columns: new[] { "DoctorId", "PatientId", "CheckInTimeUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_DoctorId_PatientId_CheckInTimeUtc_Id",
                table: "Tickets",
                columns: new[] { "DoctorId", "PatientId", "CheckInTimeUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketRegistry_DoctorId_PatientId_BusinessDate",
                table: "TicketRegistry",
                columns: new[] { "DoctorId", "PatientId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedReservations_DoctorId_PatientId_ScheduledStartUtc",
                schema: "archive",
                table: "Reservations",
                columns: new[] { "DoctorId", "PatientId", "ScheduledStartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_DoctorId_PatientId_ScheduledStartUtc",
                table: "Reservations",
                columns: new[] { "DoctorId", "PatientId", "ScheduledStartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MedicalEncounters_DoctorId_PatientId_StartedAtUtc",
                table: "MedicalEncounters",
                columns: new[] { "DoctorId", "PatientId", "StartedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ArchivedTickets_DoctorId_PatientId_CheckInTimeUtc_Id",
                schema: "archive",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_DoctorId_PatientId_CheckInTimeUtc_Id",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_TicketRegistry_DoctorId_PatientId_BusinessDate",
                table: "TicketRegistry");

            migrationBuilder.DropIndex(
                name: "IX_ArchivedReservations_DoctorId_PatientId_ScheduledStartUtc",
                schema: "archive",
                table: "Reservations");

            migrationBuilder.DropIndex(
                name: "IX_Reservations_DoctorId_PatientId_ScheduledStartUtc",
                table: "Reservations");

            migrationBuilder.DropIndex(
                name: "IX_MedicalEncounters_DoctorId_PatientId_StartedAtUtc",
                table: "MedicalEncounters");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedTickets_DoctorId",
                schema: "archive",
                table: "Tickets",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_DoctorId",
                table: "Tickets",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketRegistry_DoctorId",
                table: "TicketRegistry",
                column: "DoctorId");
        }
    }
}
