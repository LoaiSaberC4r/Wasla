using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // Generated migration column arrays.

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase16HiddenEncounterAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HiddenEncounterAccessRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HiddenEncounterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwningDoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestingDoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestingPracticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestingTicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestingEncounterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    DecidedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RevokedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VisibilityRevisionAtRequest = table.Column<int>(type: "int", nullable: false),
                    VisibilityRevisionAtApproval = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HiddenEncounterAccessRequests", x => x.Id);
                    table.CheckConstraint("CK_HiddenAccessRequests_Actors", "[OwningDoctorId] <> [RequestingDoctorId]");
                    table.CheckConstraint("CK_HiddenAccessRequests_Reason", "LEN(LTRIM(RTRIM([Reason]))) > 0");
                    table.CheckConstraint("CK_HiddenAccessRequests_Status", "[Status] IN (1,2,3,4,5)");
                    table.ForeignKey(
                        name: "FK_HiddenEncounterAccessRequests_ApplicationUsers_DecidedByApplicationUserId",
                        column: x => x.DecidedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HiddenEncounterAccessRequests_ApplicationUsers_RevokedByApplicationUserId",
                        column: x => x.RevokedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HiddenEncounterAccessRequests_DoctorPractices_RequestingPracticeId",
                        column: x => x.RequestingPracticeId,
                        principalTable: "DoctorPractices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HiddenEncounterAccessRequests_Doctors_OwningDoctorId",
                        column: x => x.OwningDoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HiddenEncounterAccessRequests_Doctors_RequestingDoctorId",
                        column: x => x.RequestingDoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HiddenEncounterAccessRequests_MedicalEncounters_HiddenEncounterId",
                        column: x => x.HiddenEncounterId,
                        principalTable: "MedicalEncounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HiddenEncounterAccessRequests_MedicalEncounters_RequestingEncounterId",
                        column: x => x.RequestingEncounterId,
                        principalTable: "MedicalEncounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HiddenEncounterAccessRequests_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HiddenEncounterAccessRequests_Tickets_RequestingTicketId",
                        column: x => x.RequestingTicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HiddenEncounterAccessRequestHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ActorApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HiddenEncounterAccessRequestHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HiddenEncounterAccessRequestHistories_ApplicationUsers_ActorApplicationUserId",
                        column: x => x.ActorApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HiddenEncounterAccessRequestHistories_HiddenEncounterAccessRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "HiddenEncounterAccessRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HiddenEncounterAccessRequestHistories_ActorApplicationUserId",
                table: "HiddenEncounterAccessRequestHistories",
                column: "ActorApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HiddenEncounterAccessRequestHistories_RequestId_OccurredAtUtc_Id",
                table: "HiddenEncounterAccessRequestHistories",
                columns: new[] { "RequestId", "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_HiddenEncounterAccessRequests_DecidedByApplicationUserId",
                table: "HiddenEncounterAccessRequests",
                column: "DecidedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HiddenEncounterAccessRequests_OwningDoctorId_Status_RequestedAtUtc_Id",
                table: "HiddenEncounterAccessRequests",
                columns: new[] { "OwningDoctorId", "Status", "RequestedAtUtc", "Id" },
                descending: new[] { false, false, true, false });

            migrationBuilder.CreateIndex(
                name: "IX_HiddenEncounterAccessRequests_PatientId",
                table: "HiddenEncounterAccessRequests",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_HiddenEncounterAccessRequests_RequestingDoctorId_RequestingTicketId_RequestedAtUtc",
                table: "HiddenEncounterAccessRequests",
                columns: new[] { "RequestingDoctorId", "RequestingTicketId", "RequestedAtUtc" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_HiddenEncounterAccessRequests_RequestingEncounterId",
                table: "HiddenEncounterAccessRequests",
                column: "RequestingEncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_HiddenEncounterAccessRequests_RequestingPracticeId",
                table: "HiddenEncounterAccessRequests",
                column: "RequestingPracticeId");

            migrationBuilder.CreateIndex(
                name: "IX_HiddenEncounterAccessRequests_RequestingTicketId",
                table: "HiddenEncounterAccessRequests",
                column: "RequestingTicketId");

            migrationBuilder.CreateIndex(
                name: "IX_HiddenEncounterAccessRequests_RevokedByApplicationUserId",
                table: "HiddenEncounterAccessRequests",
                column: "RevokedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "UX_HiddenAccessRequests_Target_Ticket",
                table: "HiddenEncounterAccessRequests",
                columns: new[] { "HiddenEncounterId", "RequestingTicketId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HiddenEncounterAccessRequestHistories");

            migrationBuilder.DropTable(
                name: "HiddenEncounterAccessRequests");
        }
    }
}
