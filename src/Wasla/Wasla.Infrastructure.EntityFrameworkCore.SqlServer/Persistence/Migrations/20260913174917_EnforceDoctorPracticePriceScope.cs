using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceDoctorPracticePriceScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DoctorPracticeSegmentVisitTypePrices_DoctorPracticeSegments_SegmentId",
                table: "DoctorPracticeSegmentVisitTypePrices");

            migrationBuilder.DropForeignKey(
                name: "FK_DoctorPracticeSegmentVisitTypePrices_DoctorPracticeVisitTypes_VisitTypeId",
                table: "DoctorPracticeSegmentVisitTypePrices");

            migrationBuilder.DropIndex(
                name: "IX_DoctorPracticeSegmentVisitTypePrices_SegmentId",
                table: "DoctorPracticeSegmentVisitTypePrices");

            migrationBuilder.DropIndex(
                name: "IX_DoctorPracticeSegmentVisitTypePrices_VisitTypeId",
                table: "DoctorPracticeSegmentVisitTypePrices");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_DoctorPracticeVisitTypes_Id_PracticeId",
                table: "DoctorPracticeVisitTypes",
                columns: new[] { "Id", "DoctorPracticeId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_DoctorPracticeSegments_Id_PracticeId",
                table: "DoctorPracticeSegments",
                columns: new[] { "Id", "DoctorPracticeId" });

            migrationBuilder.CreateIndex(
                name: "IX_DoctorPracticeSegmentVisitTypePrices_SegmentId_DoctorPracticeId",
                table: "DoctorPracticeSegmentVisitTypePrices",
                columns: new[] { "SegmentId", "DoctorPracticeId" });

            migrationBuilder.CreateIndex(
                name: "IX_DoctorPracticeSegmentVisitTypePrices_VisitTypeId_DoctorPracticeId",
                table: "DoctorPracticeSegmentVisitTypePrices",
                columns: new[] { "VisitTypeId", "DoctorPracticeId" });

            migrationBuilder.AddForeignKey(
                name: "FK_DoctorPracticeSegmentVisitTypePrices_DoctorPracticeSegments_SegmentId_DoctorPracticeId",
                table: "DoctorPracticeSegmentVisitTypePrices",
                columns: new[] { "SegmentId", "DoctorPracticeId" },
                principalTable: "DoctorPracticeSegments",
                principalColumns: new[] { "Id", "DoctorPracticeId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DoctorPracticeSegmentVisitTypePrices_DoctorPracticeVisitTypes_VisitTypeId_DoctorPracticeId",
                table: "DoctorPracticeSegmentVisitTypePrices",
                columns: new[] { "VisitTypeId", "DoctorPracticeId" },
                principalTable: "DoctorPracticeVisitTypes",
                principalColumns: new[] { "Id", "DoctorPracticeId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DoctorPracticeSegmentVisitTypePrices_DoctorPracticeSegments_SegmentId_DoctorPracticeId",
                table: "DoctorPracticeSegmentVisitTypePrices");

            migrationBuilder.DropForeignKey(
                name: "FK_DoctorPracticeSegmentVisitTypePrices_DoctorPracticeVisitTypes_VisitTypeId_DoctorPracticeId",
                table: "DoctorPracticeSegmentVisitTypePrices");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_DoctorPracticeVisitTypes_Id_PracticeId",
                table: "DoctorPracticeVisitTypes");

            migrationBuilder.DropIndex(
                name: "IX_DoctorPracticeSegmentVisitTypePrices_SegmentId_DoctorPracticeId",
                table: "DoctorPracticeSegmentVisitTypePrices");

            migrationBuilder.DropIndex(
                name: "IX_DoctorPracticeSegmentVisitTypePrices_VisitTypeId_DoctorPracticeId",
                table: "DoctorPracticeSegmentVisitTypePrices");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_DoctorPracticeSegments_Id_PracticeId",
                table: "DoctorPracticeSegments");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorPracticeSegmentVisitTypePrices_SegmentId",
                table: "DoctorPracticeSegmentVisitTypePrices",
                column: "SegmentId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorPracticeSegmentVisitTypePrices_VisitTypeId",
                table: "DoctorPracticeSegmentVisitTypePrices",
                column: "VisitTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_DoctorPracticeSegmentVisitTypePrices_DoctorPracticeSegments_SegmentId",
                table: "DoctorPracticeSegmentVisitTypePrices",
                column: "SegmentId",
                principalTable: "DoctorPracticeSegments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DoctorPracticeSegmentVisitTypePrices_DoctorPracticeVisitTypes_VisitTypeId",
                table: "DoctorPracticeSegmentVisitTypePrices",
                column: "VisitTypeId",
                principalTable: "DoctorPracticeVisitTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
