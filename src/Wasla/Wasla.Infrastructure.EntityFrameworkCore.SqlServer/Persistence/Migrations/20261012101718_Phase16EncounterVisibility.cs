using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // EF migration scaffold uses constant column arrays.

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase16EncounterVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Visibility",
                table: "MedicalEncounters",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "VisibilityRevision",
                table: "MedicalEncounters",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "EncounterVisibilityHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MedicalEncounterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OldVisibility = table.Column<int>(type: "int", nullable: true),
                    NewVisibility = table.Column<int>(type: "int", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    ActorApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncounterVisibilityHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EncounterVisibilityHistories_ApplicationUsers_ActorApplicationUserId",
                        column: x => x.ActorApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EncounterVisibilityHistories_MedicalEncounters_MedicalEncounterId",
                        column: x => x.MedicalEncounterId,
                        principalTable: "MedicalEncounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_MedicalEncounters_Visibility",
                table: "MedicalEncounters",
                sql: "[Visibility] IN (1,2) AND [VisibilityRevision] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_EncounterVisibilityHistories_ActorApplicationUserId",
                table: "EncounterVisibilityHistories",
                column: "ActorApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EncounterVisibilityHistories_MedicalEncounterId_Revision",
                table: "EncounterVisibilityHistories",
                columns: new[] { "MedicalEncounterId", "Revision" },
                unique: true);
            // Conservative constant default also covers inserts from older application instances.
            // Baseline history is metadata only; no clinical update/backfill changes rowversions.
            migrationBuilder.Sql("""
                INSERT INTO [EncounterVisibilityHistories]
                    ([Id], [MedicalEncounterId], [OldVisibility], [NewVisibility], [Revision], [ActorApplicationUserId], [OccurredAtUtc], [Source])
                SELECT NEWID(), [Id], NULL, 2, 0, NULL, SYSUTCDATETIME(), N'LegacyMigration'
                FROM [MedicalEncounters];
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EncounterVisibilityHistories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MedicalEncounters_Visibility",
                table: "MedicalEncounters");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "MedicalEncounters");

            migrationBuilder.DropColumn(
                name: "VisibilityRevision",
                table: "MedicalEncounters");
        }
    }
}
