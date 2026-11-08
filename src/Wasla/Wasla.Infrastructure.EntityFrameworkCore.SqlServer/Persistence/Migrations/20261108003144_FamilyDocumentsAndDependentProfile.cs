using System;
#pragma warning disable CA1861 // EF-generated immutable column arrays follow the existing migration convention.
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FamilyDocumentsAndDependentProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DependentProfileChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentPatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChildPatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DependentProfileChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DependentProfileChanges_ApplicationUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DependentProfileChanges_PatientContacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "PatientContacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DependentProfileChanges_Patients_ChildPatientId",
                        column: x => x.ChildPatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DependentProfileChanges_Patients_ParentPatientId",
                        column: x => x.ParentPatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PatientHistoricalDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectPatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploaderPatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CurrentVersionNumber = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    LegalHold = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientHistoricalDocuments", x => x.Id);
                    table.CheckConstraint("CK_HistoricalDocument_State", "[State] IN (1,2,3,4,5)");
                    table.CheckConstraint("CK_HistoricalDocument_Version", "[CurrentVersionNumber] >= 0");
                    table.ForeignKey(
                        name: "FK_PatientHistoricalDocuments_ApplicationUsers_UploadedByApplicationUserId",
                        column: x => x.UploadedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientHistoricalDocuments_Patients_SubjectPatientId",
                        column: x => x.SubjectPatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientHistoricalDocuments_Patients_UploaderPatientId",
                        column: x => x.UploaderPatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HistoricalDocumentOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectPatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalDocumentOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoricalDocumentOperations_ApplicationUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoricalDocumentOperations_PatientHistoricalDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "PatientHistoricalDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoricalDocumentOperations_Patients_SubjectPatientId",
                        column: x => x.SubjectPatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PatientHistoricalDocumentHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientHistoricalDocumentHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientHistoricalDocumentHistories_ApplicationUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientHistoricalDocumentHistories_PatientHistoricalDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "PatientHistoricalDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PatientHistoricalDocumentVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    DocumentDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DateUnknown = table.Column<bool>(type: "bit", nullable: false),
                    ProviderName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PatientNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientHistoricalDocumentVersions", x => x.Id);
                    table.CheckConstraint("CK_HistoricalVersion_Category", "[Category] IN (1,2,3,4,5)");
                    table.CheckConstraint("CK_HistoricalVersion_Date", "([DateUnknown] = 1 AND [DocumentDate] IS NULL) OR ([DateUnknown] = 0 AND [DocumentDate] IS NOT NULL)");
                    table.CheckConstraint("CK_HistoricalVersion_Number", "[VersionNumber] > 0");
                    table.ForeignKey(
                        name: "FK_PatientHistoricalDocumentVersions_ApplicationUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientHistoricalDocumentVersions_PatientHistoricalDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "PatientHistoricalDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PatientHistoricalDocumentAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MediaKey = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Length = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ScannedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientHistoricalDocumentAttachments", x => x.Id);
                    table.CheckConstraint("CK_HistoricalAttachment_Length", "[Length] > 0 AND [Length] <= 10485760");
                    table.ForeignKey(
                        name: "FK_PatientHistoricalDocumentAttachments_PatientHistoricalDocumentVersions_VersionId",
                        column: x => x.VersionId,
                        principalTable: "PatientHistoricalDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DependentProfileChanges_ActorUserId_Operation_IdempotencyKey",
                table: "DependentProfileChanges",
                columns: new[] { "ActorUserId", "Operation", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DependentProfileChanges_ChildPatientId",
                table: "DependentProfileChanges",
                column: "ChildPatientId");

            migrationBuilder.CreateIndex(
                name: "IX_DependentProfileChanges_ContactId",
                table: "DependentProfileChanges",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_DependentProfileChanges_ParentPatientId",
                table: "DependentProfileChanges",
                column: "ParentPatientId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalDocumentOperations_ActorUserId_CreatedAtUtc",
                table: "HistoricalDocumentOperations",
                columns: new[] { "ActorUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalDocumentOperations_ActorUserId_Operation_IdempotencyKey",
                table: "HistoricalDocumentOperations",
                columns: new[] { "ActorUserId", "Operation", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalDocumentOperations_DocumentId",
                table: "HistoricalDocumentOperations",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalDocumentOperations_SubjectPatientId",
                table: "HistoricalDocumentOperations",
                column: "SubjectPatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientHistoricalDocumentAttachments_VersionId",
                table: "PatientHistoricalDocumentAttachments",
                column: "VersionId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientHistoricalDocumentHistories_ActorUserId",
                table: "PatientHistoricalDocumentHistories",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientHistoricalDocumentHistories_DocumentId_OccurredAtUtc_Id",
                table: "PatientHistoricalDocumentHistories",
                columns: new[] { "DocumentId", "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientHistoricalDocuments_SubjectPatientId_State_UploadedAtUtc_Id",
                table: "PatientHistoricalDocuments",
                columns: new[] { "SubjectPatientId", "State", "UploadedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientHistoricalDocuments_UploadedByApplicationUserId",
                table: "PatientHistoricalDocuments",
                column: "UploadedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientHistoricalDocuments_UploaderPatientId",
                table: "PatientHistoricalDocuments",
                column: "UploaderPatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientHistoricalDocumentVersions_CreatedByUserId",
                table: "PatientHistoricalDocumentVersions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientHistoricalDocumentVersions_DocumentId_VersionNumber",
                table: "PatientHistoricalDocumentVersions",
                columns: new[] { "DocumentId", "VersionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Forward-only migration: disable flags and retain clinical source history.");
        }
    }
}
