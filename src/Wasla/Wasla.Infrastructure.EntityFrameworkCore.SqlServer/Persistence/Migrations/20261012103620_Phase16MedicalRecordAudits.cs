using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // Generated migration column arrays.

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase16MedicalRecordAudits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MedicalRecordAccessAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetEncounterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestingTicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorCategory = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ResourceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AccessDecision = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AccessReasonCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalRecordAccessAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedicalRecordAccessAudits_ApplicationUsers_ActorApplicationUserId",
                        column: x => x.ActorApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedicalRecordAccessAudits_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MedicalRecordAccessAudits_ActorApplicationUserId_OccurredAtUtc",
                table: "MedicalRecordAccessAudits",
                columns: new[] { "ActorApplicationUserId", "OccurredAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_MedicalRecordAccessAudits_PatientId_OccurredAtUtc_Id",
                table: "MedicalRecordAccessAudits",
                columns: new[] { "PatientId", "OccurredAtUtc", "Id" },
                descending: new[] { false, true, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MedicalRecordAccessAudits");
        }
    }
}
