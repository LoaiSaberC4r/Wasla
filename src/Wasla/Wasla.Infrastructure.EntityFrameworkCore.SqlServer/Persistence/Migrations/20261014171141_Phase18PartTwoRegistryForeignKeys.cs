#pragma warning disable CA1861 // EF-generated one-time migration arrays.
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase18PartTwoRegistryForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM dbo.Reservations s LEFT JOIN dbo.ReservationRegistry r ON r.Id=s.Id WHERE r.Id IS NULL) OR EXISTS (SELECT 1 FROM dbo.Tickets s LEFT JOIN dbo.TicketRegistry r ON r.Id=s.Id WHERE r.Id IS NULL) THROW 51000, 'Archive.RegistryPopulationRequired', 1;");

            migrationBuilder.DropForeignKey(
                name: "FK_FollowUpEligibilities_Reservations_ReservedReservationId",
                table: "FollowUpEligibilities");

            migrationBuilder.DropForeignKey(
                name: "FK_FollowUpEligibilities_Tickets_ReservedTicketId",
                table: "FollowUpEligibilities");

            migrationBuilder.DropForeignKey(
                name: "FK_HiddenEncounterAccessRequests_Tickets_RequestingTicketId",
                table: "HiddenEncounterAccessRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_MedicalEncounters_Tickets_TicketId",
                table: "MedicalEncounters");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Reservations_ReservationId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Tickets_TicketId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_ReceptionHandovers_Reservations_DoctorPracticeId_ReservationId",
                table: "ReceptionHandovers");

            migrationBuilder.DropForeignKey(
                name: "FK_ReceptionHandovers_Tickets_DoctorPracticeId_TicketId",
                table: "ReceptionHandovers");

            migrationBuilder.DropForeignKey(
                name: "FK_Refunds_Tickets_TicketId",
                table: "Refunds");

            migrationBuilder.DropForeignKey(
                name: "FK_ReservationIdempotencyRecords_Reservations_ReservationId",
                table: "ReservationIdempotencyRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_TicketIdempotencyRecords_Tickets_TicketId",
                table: "TicketIdempotencyRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Reservations_ReservationId",
                table: "Tickets");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Tickets_DoctorPracticeId_Id",
                table: "Tickets");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Reservations_DoctorPracticeId_Id",
                table: "Reservations");

            migrationBuilder.AddForeignKey(
                name: "FK_FollowUpEligibilities_ReservationRegistry_ReservedReservationId",
                table: "FollowUpEligibilities",
                column: "ReservedReservationId",
                principalTable: "ReservationRegistry",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FollowUpEligibilities_TicketRegistry_ReservedTicketId",
                table: "FollowUpEligibilities",
                column: "ReservedTicketId",
                principalTable: "TicketRegistry",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_HiddenEncounterAccessRequests_TicketRegistry_RequestingTicketId",
                table: "HiddenEncounterAccessRequests",
                column: "RequestingTicketId",
                principalTable: "TicketRegistry",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MedicalEncounters_TicketRegistry_TicketId",
                table: "MedicalEncounters",
                column: "TicketId",
                principalTable: "TicketRegistry",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_ReservationRegistry_ReservationId",
                table: "Payments",
                column: "ReservationId",
                principalTable: "ReservationRegistry",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_TicketRegistry_TicketId",
                table: "Payments",
                column: "TicketId",
                principalTable: "TicketRegistry",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReceptionHandovers_ReservationRegistry_DoctorPracticeId_ReservationId",
                table: "ReceptionHandovers",
                columns: new[] { "DoctorPracticeId", "ReservationId" },
                principalTable: "ReservationRegistry",
                principalColumns: new[] { "DoctorPracticeId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReceptionHandovers_TicketRegistry_DoctorPracticeId_TicketId",
                table: "ReceptionHandovers",
                columns: new[] { "DoctorPracticeId", "TicketId" },
                principalTable: "TicketRegistry",
                principalColumns: new[] { "DoctorPracticeId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Refunds_TicketRegistry_TicketId",
                table: "Refunds",
                column: "TicketId",
                principalTable: "TicketRegistry",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReservationIdempotencyRecords_ReservationRegistry_ReservationId",
                table: "ReservationIdempotencyRecords",
                column: "ReservationId",
                principalTable: "ReservationRegistry",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Reservations_ReservationRegistry_Id",
                table: "Reservations",
                column: "Id",
                principalTable: "ReservationRegistry",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TicketIdempotencyRecords_TicketRegistry_TicketId",
                table: "TicketIdempotencyRecords",
                column: "TicketId",
                principalTable: "TicketRegistry",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_ReservationRegistry_ReservationId",
                table: "Tickets",
                column: "ReservationId",
                principalTable: "ReservationRegistry",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_TicketRegistry_Id",
                table: "Tickets",
                column: "Id",
                principalTable: "TicketRegistry",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new InvalidOperationException("Archive migration reversal requires a separately validated recovery plan.");
        }
    }
}
