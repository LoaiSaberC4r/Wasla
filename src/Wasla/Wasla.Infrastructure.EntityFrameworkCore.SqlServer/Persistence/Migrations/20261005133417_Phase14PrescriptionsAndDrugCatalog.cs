// EF-generated schema operations allocate their column arrays once per migration.
#pragma warning disable CA1861
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase14PrescriptionsAndDrugCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DrugCatalogImportBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SourceVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SourceCommitSha = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FileSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TotalRecords = table.Column<int>(type: "int", nullable: false),
                    NewRecords = table.Column<int>(type: "int", nullable: false),
                    UnchangedRecords = table.Column<int>(type: "int", nullable: false),
                    PriceChanges = table.Column<int>(type: "int", nullable: false),
                    NeedsReviewRecords = table.Column<int>(type: "int", nullable: false),
                    MissingRecords = table.Column<int>(type: "int", nullable: false),
                    PossibleDuplicateRecords = table.Column<int>(type: "int", nullable: false),
                    ExactDuplicateRecords = table.Column<int>(type: "int", nullable: false),
                    CreatedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AppliedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrugCatalogImportBatches", x => x.Id);
                    table.CheckConstraint("CK_DrugCatalogImportBatches_Status", "[Status] IN (1,2,3,4)");
                    table.ForeignKey(
                        name: "FK_DrugCatalogImportBatches_ApplicationUsers_CreatedByApplicationUserId",
                        column: x => x.CreatedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MedicationIdempotencyRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ResponseJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicationIdempotencyRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedicationIdempotencyRecords_ApplicationUsers_ActorApplicationUserId",
                        column: x => x.ActorApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PrescriptionAuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrescriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrescriptionVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrescriptionAuditEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrescriptionAuditEvents_ApplicationUsers_ActorApplicationUserId",
                        column: x => x.ActorApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrescriptionAuditEvents_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Prescriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MedicalEncounterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    LastVersionNumber = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prescriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Prescriptions_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Prescriptions_MedicalEncounters_MedicalEncounterId",
                        column: x => x.MedicalEncounterId,
                        principalTable: "MedicalEncounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Prescriptions_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PrescriptionVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrescriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PreviousVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CorrectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinalizedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinalizedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VoidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VoidedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrescriptionVersions", x => x.Id);
                    table.CheckConstraint("CK_PrescriptionVersions_Lifecycle", "([Status] = 1 AND [FinalizedAtUtc] IS NULL AND [VoidedAtUtc] IS NULL) OR ([Status] IN (2,3) AND [FinalizedAtUtc] IS NOT NULL AND [VoidedAtUtc] IS NULL) OR ([Status] = 4 AND [FinalizedAtUtc] IS NOT NULL AND [VoidedAtUtc] IS NOT NULL AND [VoidReason] IS NOT NULL)");
                    table.CheckConstraint("CK_PrescriptionVersions_Number", "[VersionNumber] > 0");
                    table.CheckConstraint("CK_PrescriptionVersions_Status", "[Status] IN (1,2,3,4)");
                    table.ForeignKey(
                        name: "FK_PrescriptionVersions_ApplicationUsers_CreatedByApplicationUserId",
                        column: x => x.CreatedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrescriptionVersions_ApplicationUsers_FinalizedByApplicationUserId",
                        column: x => x.FinalizedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrescriptionVersions_ApplicationUsers_VoidedByApplicationUserId",
                        column: x => x.VoidedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrescriptionVersions_PrescriptionVersions_PreviousVersionId",
                        column: x => x.PreviousVersionId,
                        principalTable: "PrescriptionVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrescriptionVersions_Prescriptions_PrescriptionId",
                        column: x => x.PrescriptionId,
                        principalTable: "Prescriptions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DrugCatalogHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DrugCatalogId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    BeforeSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PerformedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrugCatalogHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DrugCatalogHistories_ApplicationUsers_PerformedByApplicationUserId",
                        column: x => x.PerformedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DrugCatalogImportRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImportBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceRowNumber = table.Column<int>(type: "int", nullable: false),
                    CommercialNameEn = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CommercialNameAr = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ScientificName = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: true),
                    Manufacturer = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DrugClass = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Route = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PriceEgp = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    IdentityFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ChangeType = table.Column<int>(type: "int", nullable: false),
                    MatchedDrugCatalogId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MatchedRowVersion = table.Column<byte[]>(type: "varbinary(8)", maxLength: 8, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrugCatalogImportRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DrugCatalogImportRecords_DrugCatalogImportBatches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "DrugCatalogImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DrugCatalogRequestHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DrugCatalogRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    BeforeSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PerformedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrugCatalogRequestHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DrugCatalogRequestHistories_ApplicationUsers_PerformedByApplicationUserId",
                        column: x => x.PerformedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DrugCatalogRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByDoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MedicationName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ScientificName = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: true),
                    Manufacturer = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DrugClass = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Route = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StrengthText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DosageForm = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DoctorNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ApprovedDrugCatalogId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DuplicateOfDrugCatalogId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrentReviewReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReviewedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrugCatalogRequests", x => x.Id);
                    table.CheckConstraint("CK_DrugCatalogRequests_Status", "[Status] IN (1,2,3,4)");
                    table.ForeignKey(
                        name: "FK_DrugCatalogRequests_ApplicationUsers_RequestedByApplicationUserId",
                        column: x => x.RequestedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DrugCatalogRequests_ApplicationUsers_ReviewedByApplicationUserId",
                        column: x => x.ReviewedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DrugCatalogRequests_Doctors_RequestedByDoctorId",
                        column: x => x.RequestedByDoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DrugCatalogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommercialNameEn = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CommercialNameAr = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ScientificName = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: true),
                    Manufacturer = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DrugClass = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Route = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StrengthText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DosageForm = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PriceEgp = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StatusReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MergedIntoDrugCatalogId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OriginType = table.Column<int>(type: "int", nullable: false),
                    OriginImportBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OriginDrugCatalogRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceIdentityFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SourceContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IsManagerReviewed = table.Column<bool>(type: "bit", nullable: false),
                    IsPriceManuallyOverridden = table.Column<bool>(type: "bit", nullable: false),
                    NormalizedCommercialNameEn = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: false),
                    NormalizedCommercialNameAr = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: true),
                    NormalizedScientificName = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: true),
                    NormalizedManufacturer = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: true),
                    IsMissingFromLatestSource = table.Column<bool>(type: "bit", nullable: false),
                    MissingFromSourceSinceBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrugCatalogs", x => x.Id);
                    table.CheckConstraint("CK_DrugCatalogs_Merge", "([Status] = 4 AND [MergedIntoDrugCatalogId] IS NOT NULL AND [MergedIntoDrugCatalogId] <> [Id]) OR ([Status] <> 4 AND [MergedIntoDrugCatalogId] IS NULL)");
                    table.CheckConstraint("CK_DrugCatalogs_Price", "[PriceEgp] IS NULL OR [PriceEgp] >= 0");
                    table.CheckConstraint("CK_DrugCatalogs_Status", "[Status] IN (1,2,3,4)");
                    table.ForeignKey(
                        name: "FK_DrugCatalogs_ApplicationUsers_CreatedByApplicationUserId",
                        column: x => x.CreatedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DrugCatalogs_ApplicationUsers_ModifiedByApplicationUserId",
                        column: x => x.ModifiedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DrugCatalogs_DrugCatalogImportBatches_MissingFromSourceSinceBatchId",
                        column: x => x.MissingFromSourceSinceBatchId,
                        principalTable: "DrugCatalogImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DrugCatalogs_DrugCatalogImportBatches_OriginImportBatchId",
                        column: x => x.OriginImportBatchId,
                        principalTable: "DrugCatalogImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DrugCatalogs_DrugCatalogRequests_OriginDrugCatalogRequestId",
                        column: x => x.OriginDrugCatalogRequestId,
                        principalTable: "DrugCatalogRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DrugCatalogs_DrugCatalogs_MergedIntoDrugCatalogId",
                        column: x => x.MergedIntoDrugCatalogId,
                        principalTable: "DrugCatalogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PrescriptionItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrescriptionVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    MedicationSource = table.Column<int>(type: "int", nullable: false),
                    DrugCatalogId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DrugCatalogRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MedicationNameSnapshot = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ScientificNameSnapshot = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: true),
                    StrengthSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DosageFormSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RouteSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DoseText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FrequencyCode = table.Column<int>(type: "int", nullable: true),
                    FrequencyText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DurationType = table.Column<int>(type: "int", nullable: true),
                    DurationValue = table.Column<int>(type: "int", nullable: true),
                    DurationUnit = table.Column<int>(type: "int", nullable: true),
                    AsNeeded = table.Column<bool>(type: "bit", nullable: false),
                    PrnReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MinimumIntervalText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MaxPer24HoursText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    QuantityValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    QuantityUnit = table.Column<int>(type: "int", nullable: true),
                    Instructions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrescriptionItems", x => x.Id);
                    table.CheckConstraint("CK_PrescriptionItems_Duration", "([DurationType] IS NULL AND [DurationValue] IS NULL AND [DurationUnit] IS NULL) OR ([DurationType] IS NOT NULL AND [DurationType] = 1 AND ([DurationValue] IS NULL OR [DurationValue] > 0) AND ([DurationUnit] IS NULL OR [DurationUnit] IN (1,2,3))) OR ([DurationType] IS NOT NULL AND [DurationType] = 2 AND [DurationValue] IS NULL AND [DurationUnit] IS NULL)");
                    table.CheckConstraint("CK_PrescriptionItems_Order", "[SortOrder] > 0");
                    table.CheckConstraint("CK_PrescriptionItems_Quantity", "([QuantityValue] IS NULL AND [QuantityUnit] IS NULL) OR ([QuantityValue] IS NOT NULL AND [QuantityValue] > 0 AND [QuantityUnit] IS NOT NULL AND [QuantityUnit] IN (1,2,3,4,5,6,7,8,9,10))");
                    table.CheckConstraint("CK_PrescriptionItems_Source", "([MedicationSource] = 1 AND [DrugCatalogId] IS NOT NULL AND [DrugCatalogRequestId] IS NULL) OR ([MedicationSource] = 2 AND [DrugCatalogId] IS NULL AND [DrugCatalogRequestId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_PrescriptionItems_DrugCatalogRequests_DrugCatalogRequestId",
                        column: x => x.DrugCatalogRequestId,
                        principalTable: "DrugCatalogRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrescriptionItems_DrugCatalogs_DrugCatalogId",
                        column: x => x.DrugCatalogId,
                        principalTable: "DrugCatalogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrescriptionItems_PrescriptionVersions_PrescriptionVersionId",
                        column: x => x.PrescriptionVersionId,
                        principalTable: "PrescriptionVersions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_DrugCatalogHistories_DrugCatalogId_OccurredAtUtc",
                table: "DrugCatalogHistories",
                columns: new[] { "DrugCatalogId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DrugCatalogHistories_PerformedByApplicationUserId",
                table: "DrugCatalogHistories",
                column: "PerformedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DrugCatalogImportBatches_CreatedAtUtc",
                table: "DrugCatalogImportBatches",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_DrugCatalogImportBatches_CreatedByApplicationUserId",
                table: "DrugCatalogImportBatches",
                column: "CreatedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DrugCatalogImportRecords_ImportBatchId_ChangeType_SourceRowNumber",
                table: "DrugCatalogImportRecords",
                columns: new[] { "ImportBatchId", "ChangeType", "SourceRowNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_DrugCatalogImportRecords_MatchedDrugCatalogId",
                table: "DrugCatalogImportRecords",
                column: "MatchedDrugCatalogId");

            migrationBuilder.CreateIndex(
                name: "IX_DrugCatalogRequestHistories_DrugCatalogRequestId_OccurredAtUtc",
                table: "DrugCatalogRequestHistories",
                columns: new[] { "DrugCatalogRequestId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DrugCatalogRequestHistories_PerformedByApplicationUserId",
                table: "DrugCatalogRequestHistories",
                column: "PerformedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DrugCatalogRequests_ApprovedDrugCatalogId",
                table: "DrugCatalogRequests",
                column: "ApprovedDrugCatalogId");

            migrationBuilder.CreateIndex(
                name: "IX_DrugCatalogRequests_DuplicateOfDrugCatalogId",
                table: "DrugCatalogRequests",
                column: "DuplicateOfDrugCatalogId");

            migrationBuilder.CreateIndex(
                name: "IX_DrugCatalogRequests_RequestedByApplicationUserId",
                table: "DrugCatalogRequests",
                column: "RequestedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DrugCatalogRequests_RequestedByDoctorId_Status_CreatedAtUtc",
                table: "DrugCatalogRequests",
                columns: new[] { "RequestedByDoctorId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DrugCatalogRequests_ReviewedByApplicationUserId",
                table: "DrugCatalogRequests",
                column: "ReviewedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DrugCatalogRequests_Status_CreatedAtUtc",
                table: "DrugCatalogRequests",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DrugCatalogs_CreatedByApplicationUserId",
                table: "DrugCatalogs",
                column: "CreatedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DrugCatalogs_MergedIntoDrugCatalogId",
                table: "DrugCatalogs",
                column: "MergedIntoDrugCatalogId");

            migrationBuilder.CreateIndex(
                name: "IX_DrugCatalogs_MissingFromSourceSinceBatchId",
                table: "DrugCatalogs",
                column: "MissingFromSourceSinceBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_DrugCatalogs_ModifiedByApplicationUserId",
                table: "DrugCatalogs",
                column: "ModifiedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DrugCatalogs_OriginDrugCatalogRequestId",
                table: "DrugCatalogs",
                column: "OriginDrugCatalogRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_DrugCatalogs_OriginImportBatchId",
                table: "DrugCatalogs",
                column: "OriginImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_DrugCatalogs_Status",
                table: "DrugCatalogs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_DrugCatalogs_Status_NormalizedCommercialNameAr",
                table: "DrugCatalogs",
                columns: new[] { "Status", "NormalizedCommercialNameAr" });

            migrationBuilder.CreateIndex(
                name: "IX_DrugCatalogs_Status_NormalizedCommercialNameEn",
                table: "DrugCatalogs",
                columns: new[] { "Status", "NormalizedCommercialNameEn" });

            migrationBuilder.CreateIndex(
                name: "IX_DrugCatalogs_Status_NormalizedScientificName",
                table: "DrugCatalogs",
                columns: new[] { "Status", "NormalizedScientificName" });

            migrationBuilder.CreateIndex(
                name: "UX_DrugCatalogs_SourceIdentity",
                table: "DrugCatalogs",
                column: "SourceIdentityFingerprint",
                unique: true,
                filter: "[SourceIdentityFingerprint] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MedicationIdempotencyRecords_ActorApplicationUserId_Operation_IdempotencyKey",
                table: "MedicationIdempotencyRecords",
                columns: new[] { "ActorApplicationUserId", "Operation", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionAuditEvents_ActorApplicationUserId",
                table: "PrescriptionAuditEvents",
                column: "ActorApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionAuditEvents_DoctorId",
                table: "PrescriptionAuditEvents",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionAuditEvents_PrescriptionId_OccurredAtUtc",
                table: "PrescriptionAuditEvents",
                columns: new[] { "PrescriptionId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionItems_DrugCatalogId",
                table: "PrescriptionItems",
                column: "DrugCatalogId");

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionItems_DrugCatalogRequestId",
                table: "PrescriptionItems",
                column: "DrugCatalogRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionItems_PrescriptionVersionId_SortOrder",
                table: "PrescriptionItems",
                columns: new[] { "PrescriptionVersionId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_DoctorId_CreatedOnUtc",
                table: "Prescriptions",
                columns: new[] { "DoctorId", "CreatedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_PatientId_CreatedOnUtc",
                table: "Prescriptions",
                columns: new[] { "PatientId", "CreatedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_Prescriptions_Encounter",
                table: "Prescriptions",
                column: "MedicalEncounterId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionVersions_CreatedByApplicationUserId",
                table: "PrescriptionVersions",
                column: "CreatedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionVersions_FinalizedByApplicationUserId",
                table: "PrescriptionVersions",
                column: "FinalizedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionVersions_VoidedByApplicationUserId",
                table: "PrescriptionVersions",
                column: "VoidedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "UX_PrescriptionVersions_Current",
                table: "PrescriptionVersions",
                column: "PrescriptionId",
                unique: true,
                filter: "[Status] = 2");

            migrationBuilder.CreateIndex(
                name: "UX_PrescriptionVersions_Draft",
                table: "PrescriptionVersions",
                column: "PrescriptionId",
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "UX_PrescriptionVersions_Number",
                table: "PrescriptionVersions",
                columns: new[] { "PrescriptionId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_PrescriptionVersions_Previous",
                table: "PrescriptionVersions",
                column: "PreviousVersionId",
                unique: true,
                filter: "[PreviousVersionId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_DrugCatalogHistories_DrugCatalogs_DrugCatalogId",
                table: "DrugCatalogHistories",
                column: "DrugCatalogId",
                principalTable: "DrugCatalogs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DrugCatalogImportRecords_DrugCatalogs_MatchedDrugCatalogId",
                table: "DrugCatalogImportRecords",
                column: "MatchedDrugCatalogId",
                principalTable: "DrugCatalogs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DrugCatalogRequestHistories_DrugCatalogRequests_DrugCatalogRequestId",
                table: "DrugCatalogRequestHistories",
                column: "DrugCatalogRequestId",
                principalTable: "DrugCatalogRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DrugCatalogRequests_DrugCatalogs_ApprovedDrugCatalogId",
                table: "DrugCatalogRequests",
                column: "ApprovedDrugCatalogId",
                principalTable: "DrugCatalogs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DrugCatalogRequests_DrugCatalogs_DuplicateOfDrugCatalogId",
                table: "DrugCatalogRequests",
                column: "DuplicateOfDrugCatalogId",
                principalTable: "DrugCatalogs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DrugCatalogRequests_DrugCatalogs_ApprovedDrugCatalogId",
                table: "DrugCatalogRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_DrugCatalogRequests_DrugCatalogs_DuplicateOfDrugCatalogId",
                table: "DrugCatalogRequests");

            migrationBuilder.DropTable(
                name: "DrugCatalogHistories");

            migrationBuilder.DropTable(
                name: "DrugCatalogImportRecords");

            migrationBuilder.DropTable(
                name: "DrugCatalogRequestHistories");

            migrationBuilder.DropTable(
                name: "MedicationIdempotencyRecords");

            migrationBuilder.DropTable(
                name: "PrescriptionAuditEvents");

            migrationBuilder.DropTable(
                name: "PrescriptionItems");

            migrationBuilder.DropTable(
                name: "PrescriptionVersions");

            migrationBuilder.DropTable(
                name: "Prescriptions");

            migrationBuilder.DropTable(
                name: "DrugCatalogs");

            migrationBuilder.DropTable(
                name: "DrugCatalogImportBatches");

            migrationBuilder.DropTable(
                name: "DrugCatalogRequests");
        }
    }
}
