using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OptimizePublicDoctorSearchRanking : Migration
    {
        private static readonly string[] PopularityIndexColumns = ["PopularityScore", "DoctorId"];
        private static readonly bool[] PopularityIndexDescending = [true, false];
        private static readonly string[] DoctorAvailabilityIndexColumns =
            ["DoctorId", "IsAvailable", "VisibleFromUtc", "SlotStartUtc"];
        private static readonly string[] PracticeStartIndexColumns = ["DoctorPracticeId", "SlotStartUtc"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PublicDoctorSearchRanks",
                columns: table => new
                {
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PopularityScore = table.Column<long>(type: "bigint", nullable: false),
                    RefreshedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicDoctorSearchRanks", x => x.DoctorId);
                });

            migrationBuilder.CreateTable(
                name: "PublicPracticeAvailabilitySlots",
                columns: table => new
                {
                    DoctorPracticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LocalTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SlotStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VisibleFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false),
                    RefreshedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicPracticeAvailabilitySlots", x => new { x.DoctorPracticeId, x.LocalDate, x.LocalTime });
                });

            migrationBuilder.CreateIndex(
                name: "IX_PublicDoctorSearchRanks_Popularity_Doctor",
                table: "PublicDoctorSearchRanks",
                columns: PopularityIndexColumns,
                descending: PopularityIndexDescending);

            migrationBuilder.CreateIndex(
                name: "IX_PublicAvailabilitySlots_Doctor_Availability",
                table: "PublicPracticeAvailabilitySlots",
                columns: DoctorAvailabilityIndexColumns);

            migrationBuilder.CreateIndex(
                name: "IX_PublicAvailabilitySlots_Practice_Start",
                table: "PublicPracticeAvailabilitySlots",
                columns: PracticeStartIndexColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PublicDoctorSearchRanks");

            migrationBuilder.DropTable(
                name: "PublicPracticeAvailabilitySlots");
        }
    }
}
