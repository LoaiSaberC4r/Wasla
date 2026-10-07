using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
// EF-generated schema operations allocate their column arrays once per migration.
#pragma warning disable CA1861

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase15DiagnosticOrdersAndResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LabCatalogImportBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceVersion = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FileSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TotalRecords = table.Column<int>(type: "int", nullable: false),
                    AppliedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AppliedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DiscardedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DiscardedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabCatalogImportBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LabRequestHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    BeforeSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabRequestHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LabRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MedicalEncounterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorPracticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Origin = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PatientInstructions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PostVisitReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabRequests", x => x.Id);
                    table.CheckConstraint("CK_LabRequests_PostVisit", "[Origin] = 1 OR ([PostVisitReason] IS NOT NULL AND [Status] <> 1)");
                    table.CheckConstraint("CK_LabRequests_State", "[Status] IN (1,2,3,4,5) AND [Origin] IN (1,2)");
                    table.ForeignKey(
                        name: "FK_LabRequests_DoctorPractices_DoctorPracticeId",
                        column: x => x.DoctorPracticeId,
                        principalTable: "DoctorPractices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LabRequests_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LabRequests_MedicalEncounters_MedicalEncounterId",
                        column: x => x.MedicalEncounterId,
                        principalTable: "MedicalEncounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LabRequests_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LabTestCatalogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    LoincCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    OfficialNameEn = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OfficialNameAr = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ExternalStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    SourceVersion = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    SourceDataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Component = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ShortName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttributesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsCommonOrder = table.Column<bool>(type: "bit", nullable: false),
                    DisplayNameEn = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DisplayNameAr = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AliasesEn = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    AliasesAr = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    InternalNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    NormalizedSearch = table.Column<string>(type: "nvarchar(max)", maxLength: 12000, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    MergedIntoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsLocallyDeactivated = table.Column<bool>(type: "bit", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabTestCatalogs", x => x.Id);
                    table.CheckConstraint("CK_LabTestCatalog_Merge", "([Status] = 3 AND [MergedIntoId] IS NOT NULL AND [MergedIntoId] <> [Id]) OR ([Status] IN (1,2) AND [MergedIntoId] IS NULL)");
                    table.CheckConstraint("CK_LabTestCatalog_Source", "([Source] = 1 AND [LoincCode] IS NOT NULL) OR ([Source] = 2 AND [LoincCode] IS NULL)");
                    table.ForeignKey(
                        name: "FK_LabTestCatalogs_LabTestCatalogs_MergedIntoId",
                        column: x => x.MergedIntoId,
                        principalTable: "LabTestCatalogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RadiologyCatalogImportBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceVersion = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FileSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TotalRecords = table.Column<int>(type: "int", nullable: false),
                    AppliedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AppliedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DiscardedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DiscardedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadiologyCatalogImportBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RadiologyProcedureCatalogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    LoincCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    OfficialNameEn = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OfficialNameAr = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ExternalStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    SourceVersion = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    SourceDataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Component = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ShortName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttributesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsCommonOrder = table.Column<bool>(type: "bit", nullable: false),
                    DisplayNameEn = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DisplayNameAr = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AliasesEn = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    AliasesAr = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    InternalNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    NormalizedSearch = table.Column<string>(type: "nvarchar(max)", maxLength: 12000, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    MergedIntoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsLocallyDeactivated = table.Column<bool>(type: "bit", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadiologyProcedureCatalogs", x => x.Id);
                    table.CheckConstraint("CK_RadiologyProcedureCatalog_Merge", "([Status] = 3 AND [MergedIntoId] IS NOT NULL AND [MergedIntoId] <> [Id]) OR ([Status] IN (1,2) AND [MergedIntoId] IS NULL)");
                    table.CheckConstraint("CK_RadiologyProcedureCatalog_Source", "([Source] = 1 AND [LoincCode] IS NOT NULL) OR ([Source] = 2 AND [LoincCode] IS NULL)");
                    table.ForeignKey(
                        name: "FK_RadiologyProcedureCatalogs_RadiologyProcedureCatalogs_MergedIntoId",
                        column: x => x.MergedIntoId,
                        principalTable: "RadiologyProcedureCatalogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RadiologyRequestHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    BeforeSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadiologyRequestHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RadiologyRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MedicalEncounterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorPracticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Origin = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PatientInstructions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PostVisitReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadiologyRequests", x => x.Id);
                    table.CheckConstraint("CK_RadiologyRequests_PostVisit", "[Origin] = 1 OR ([PostVisitReason] IS NOT NULL AND [Status] <> 1)");
                    table.CheckConstraint("CK_RadiologyRequests_State", "[Status] IN (1,2,3,4,5) AND [Origin] IN (1,2)");
                    table.ForeignKey(
                        name: "FK_RadiologyRequests_DoctorPractices_DoctorPracticeId",
                        column: x => x.DoctorPracticeId,
                        principalTable: "DoctorPractices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RadiologyRequests_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RadiologyRequests_MedicalEncounters_MedicalEncounterId",
                        column: x => x.MedicalEncounterId,
                        principalTable: "MedicalEncounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RadiologyRequests_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LabCatalogImportRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImportBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoincCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SourceDataJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Disposition = table.Column<int>(type: "int", nullable: false),
                    MatchedCatalogId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MatchedRowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabCatalogImportRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabCatalogImportRecords_LabCatalogImportBatches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "LabCatalogImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LabResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorPracticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcceptedSubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CurrentVersionNumber = table.Column<int>(type: "int", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabResults_LabRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "LabRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LabCatalogRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByDoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Specimen = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CatalogClarificationNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CanonicalCatalogId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReasonType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ReviewReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabCatalogRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabCatalogRequests_Doctors_RequestedByDoctorId",
                        column: x => x.RequestedByDoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LabCatalogRequests_LabTestCatalogs_CanonicalCatalogId",
                        column: x => x.CanonicalCatalogId,
                        principalTable: "LabTestCatalogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LabTestCatalogHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    BeforeSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabTestCatalogHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabTestCatalogHistories_LabTestCatalogs_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "LabTestCatalogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RadiologyCatalogImportRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImportBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoincCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SourceDataJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Disposition = table.Column<int>(type: "int", nullable: false),
                    MatchedCatalogId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MatchedRowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadiologyCatalogImportRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RadiologyCatalogImportRecords_RadiologyCatalogImportBatches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "RadiologyCatalogImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RadiologyCatalogRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByDoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Specimen = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CatalogClarificationNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CanonicalCatalogId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReasonType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ReviewReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadiologyCatalogRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RadiologyCatalogRequests_Doctors_RequestedByDoctorId",
                        column: x => x.RequestedByDoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RadiologyCatalogRequests_RadiologyProcedureCatalogs_CanonicalCatalogId",
                        column: x => x.CanonicalCatalogId,
                        principalTable: "RadiologyProcedureCatalogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RadiologyProcedureCatalogHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    BeforeSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadiologyProcedureCatalogHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RadiologyProcedureCatalogHistories_RadiologyProcedureCatalogs_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "RadiologyProcedureCatalogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RadiologyResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorPracticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcceptedSubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CurrentVersionNumber = table.Column<int>(type: "int", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadiologyResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RadiologyResults_RadiologyRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "RadiologyRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LabResultHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    BeforeSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabResultHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabResultHistories_LabResults_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "LabResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LabResultVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ExternalProviderName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExternalReportDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OriginallyUploadedBy = table.Column<int>(type: "int", nullable: false),
                    OriginallyUploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginallyUploadedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedByDoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CorrectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    VoidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabResultVersions", x => x.Id);
                    table.CheckConstraint("CK_LabResultVersions_State", "[Status] IN (1,2,3) AND [VersionNumber] > 0");
                    table.ForeignKey(
                        name: "FK_LabResultVersions_LabResults_ResultId",
                        column: x => x.ResultId,
                        principalTable: "LabResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PatientLabResultSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorPracticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalProviderName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExternalReportDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PatientNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PatientVisibleReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AcceptedResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientLabResultSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientLabResultSubmissions_LabRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "LabRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientLabResultSubmissions_LabResults_AcceptedResultId",
                        column: x => x.AcceptedResultId,
                        principalTable: "LabResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LabCatalogRequestHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    BeforeSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabCatalogRequestHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabCatalogRequestHistories_LabCatalogRequests_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "LabCatalogRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LabRequestItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    CatalogId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CatalogRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NameArSnapshot = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    NameEnSnapshot = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    LoincCodeSnapshot = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ModalitySnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AnatomicLocationSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LateralitySnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DoctorInstructions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CancellationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CancelledByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabRequestItems", x => x.Id);
                    table.CheckConstraint("CK_LabRequestItems_Source", "([Source] = 1 AND [CatalogId] IS NOT NULL AND [CatalogRequestId] IS NULL) OR ([Source] = 2 AND [CatalogId] IS NULL AND [CatalogRequestId] IS NOT NULL)");
                    table.CheckConstraint("CK_LabRequestItems_State", "[Status] IN (1,2,3)");
                    table.ForeignKey(
                        name: "FK_LabRequestItems_LabCatalogRequests_CatalogRequestId",
                        column: x => x.CatalogRequestId,
                        principalTable: "LabCatalogRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LabRequestItems_LabRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "LabRequests",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LabRequestItems_LabTestCatalogs_CatalogId",
                        column: x => x.CatalogId,
                        principalTable: "LabTestCatalogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RadiologyCatalogRequestHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    BeforeSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadiologyCatalogRequestHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RadiologyCatalogRequestHistories_RadiologyCatalogRequests_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "RadiologyCatalogRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RadiologyRequestItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    CatalogId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CatalogRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NameArSnapshot = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    NameEnSnapshot = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    LoincCodeSnapshot = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ModalitySnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AnatomicLocationSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LateralitySnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DoctorInstructions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CancellationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CancelledByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadiologyRequestItems", x => x.Id);
                    table.CheckConstraint("CK_RadiologyRequestItems_Source", "([Source] = 1 AND [CatalogId] IS NOT NULL AND [CatalogRequestId] IS NULL) OR ([Source] = 2 AND [CatalogId] IS NULL AND [CatalogRequestId] IS NOT NULL)");
                    table.CheckConstraint("CK_RadiologyRequestItems_State", "[Status] IN (1,2,3)");
                    table.ForeignKey(
                        name: "FK_RadiologyRequestItems_RadiologyCatalogRequests_CatalogRequestId",
                        column: x => x.CatalogRequestId,
                        principalTable: "RadiologyCatalogRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RadiologyRequestItems_RadiologyProcedureCatalogs_CatalogId",
                        column: x => x.CatalogId,
                        principalTable: "RadiologyProcedureCatalogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RadiologyRequestItems_RadiologyRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "RadiologyRequests",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PatientRadiologyResultSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorPracticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalProviderName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExternalReportDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PatientNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PatientVisibleReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AcceptedResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientRadiologyResultSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientRadiologyResultSubmissions_RadiologyRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "RadiologyRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientRadiologyResultSubmissions_RadiologyResults_AcceptedResultId",
                        column: x => x.AcceptedResultId,
                        principalTable: "RadiologyResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RadiologyResultHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    BeforeSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadiologyResultHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RadiologyResultHistories_RadiologyResults_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "RadiologyResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RadiologyResultVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ExternalProviderName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExternalReportDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OriginallyUploadedBy = table.Column<int>(type: "int", nullable: false),
                    OriginallyUploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginallyUploadedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedByDoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CorrectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    VoidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadiologyResultVersions", x => x.Id);
                    table.CheckConstraint("CK_RadiologyResultVersions_State", "[Status] IN (1,2,3) AND [VersionNumber] > 0");
                    table.ForeignKey(
                        name: "FK_RadiologyResultVersions_RadiologyResults_ResultId",
                        column: x => x.ResultId,
                        principalTable: "RadiologyResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LabResultAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResultVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrivateMediaKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabResultAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabResultAttachments_LabResultVersions_ResultVersionId",
                        column: x => x.ResultVersionId,
                        principalTable: "LabResultVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PatientLabResultSubmissionAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrivateMediaKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientLabResultSubmissionAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientLabResultSubmissionAttachments_PatientLabResultSubmissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "PatientLabResultSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PatientLabResultSubmissionHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    BeforeSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientLabResultSubmissionHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientLabResultSubmissionHistories_PatientLabResultSubmissions_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "PatientLabResultSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LabResultCoverages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResultVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabResultCoverages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabResultCoverages_LabRequestItems_RequestItemId",
                        column: x => x.RequestItemId,
                        principalTable: "LabRequestItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LabResultCoverages_LabResultVersions_ResultVersionId",
                        column: x => x.ResultVersionId,
                        principalTable: "LabResultVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PatientRadiologyResultSubmissionAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrivateMediaKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientRadiologyResultSubmissionAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientRadiologyResultSubmissionAttachments_PatientRadiologyResultSubmissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "PatientRadiologyResultSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PatientRadiologyResultSubmissionHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    BeforeSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientRadiologyResultSubmissionHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientRadiologyResultSubmissionHistories_PatientRadiologyResultSubmissions_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "PatientRadiologyResultSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RadiologyResultAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResultVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrivateMediaKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadiologyResultAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RadiologyResultAttachments_RadiologyResultVersions_ResultVersionId",
                        column: x => x.ResultVersionId,
                        principalTable: "RadiologyResultVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RadiologyResultCoverages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResultVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadiologyResultCoverages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RadiologyResultCoverages_RadiologyRequestItems_RequestItemId",
                        column: x => x.RequestItemId,
                        principalTable: "RadiologyRequestItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RadiologyResultCoverages_RadiologyResultVersions_ResultVersionId",
                        column: x => x.ResultVersionId,
                        principalTable: "RadiologyResultVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LabCatalogImportBatches_UploadedAtUtc",
                table: "LabCatalogImportBatches",
                column: "UploadedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_LabCatalogImportRecords_ImportBatchId_Disposition",
                table: "LabCatalogImportRecords",
                columns: new[] { "ImportBatchId", "Disposition" });

            migrationBuilder.CreateIndex(
                name: "IX_LabCatalogImportRecords_ImportBatchId_LoincCode",
                table: "LabCatalogImportRecords",
                columns: new[] { "ImportBatchId", "LoincCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabCatalogRequestHistories_ResourceId_OccurredAtUtc",
                table: "LabCatalogRequestHistories",
                columns: new[] { "ResourceId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LabCatalogRequests_CanonicalCatalogId",
                table: "LabCatalogRequests",
                column: "CanonicalCatalogId");

            migrationBuilder.CreateIndex(
                name: "IX_LabCatalogRequests_RequestedByDoctorId_Status",
                table: "LabCatalogRequests",
                columns: new[] { "RequestedByDoctorId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LabRequestHistories_ResourceId_OccurredAtUtc",
                table: "LabRequestHistories",
                columns: new[] { "ResourceId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LabRequestItems_CatalogId",
                table: "LabRequestItems",
                column: "CatalogId");

            migrationBuilder.CreateIndex(
                name: "IX_LabRequestItems_CatalogRequestId",
                table: "LabRequestItems",
                column: "CatalogRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_LabRequestItems_RequestId",
                table: "LabRequestItems",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_LabRequestItems_RequestId_CatalogId",
                table: "LabRequestItems",
                columns: new[] { "RequestId", "CatalogId" },
                unique: true,
                filter: "[CatalogId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LabRequests_DoctorId_Status",
                table: "LabRequests",
                columns: new[] { "DoctorId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LabRequests_DoctorPracticeId_Status",
                table: "LabRequests",
                columns: new[] { "DoctorPracticeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LabRequests_PatientId_Status",
                table: "LabRequests",
                columns: new[] { "PatientId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LabRequests_RequestedAtUtc",
                table: "LabRequests",
                column: "RequestedAtUtc");

            migrationBuilder.CreateIndex(
                name: "UX_LabRequests_EncounterDraft",
                table: "LabRequests",
                column: "MedicalEncounterId",
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_LabResultAttachments_ResultVersionId",
                table: "LabResultAttachments",
                column: "ResultVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_LabResultCoverages_RequestItemId",
                table: "LabResultCoverages",
                column: "RequestItemId");

            migrationBuilder.CreateIndex(
                name: "IX_LabResultCoverages_ResultVersionId_RequestItemId",
                table: "LabResultCoverages",
                columns: new[] { "ResultVersionId", "RequestItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabResultHistories_ResourceId_OccurredAtUtc",
                table: "LabResultHistories",
                columns: new[] { "ResourceId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LabResults_AcceptedSubmissionId",
                table: "LabResults",
                column: "AcceptedSubmissionId",
                unique: true,
                filter: "[AcceptedSubmissionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LabResults_DoctorId_CreatedAtUtc",
                table: "LabResults",
                columns: new[] { "DoctorId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LabResults_DoctorPracticeId",
                table: "LabResults",
                column: "DoctorPracticeId");

            migrationBuilder.CreateIndex(
                name: "IX_LabResults_PatientId_CreatedAtUtc",
                table: "LabResults",
                columns: new[] { "PatientId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LabResults_RequestId",
                table: "LabResults",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_LabResultVersions_ResultId_VersionNumber",
                table: "LabResultVersions",
                columns: new[] { "ResultId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_LabResultVersions_Current",
                table: "LabResultVersions",
                column: "ResultId",
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_LabTestCatalogHistories_ResourceId_OccurredAtUtc",
                table: "LabTestCatalogHistories",
                columns: new[] { "ResourceId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LabTestCatalogs_MergedIntoId",
                table: "LabTestCatalogs",
                column: "MergedIntoId");

            migrationBuilder.CreateIndex(
                name: "IX_LabTestCatalogs_Status_IsCommonOrder",
                table: "LabTestCatalogs",
                columns: new[] { "Status", "IsCommonOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_LabTestCatalogs_Status_Source",
                table: "LabTestCatalogs",
                columns: new[] { "Status", "Source" });

            migrationBuilder.CreateIndex(
                name: "UX_LabTestCatalog_Loinc",
                table: "LabTestCatalogs",
                column: "LoincCode",
                unique: true,
                filter: "[LoincCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PatientLabResultSubmissionAttachments_SubmissionId",
                table: "PatientLabResultSubmissionAttachments",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientLabResultSubmissionHistories_ResourceId_OccurredAtUtc",
                table: "PatientLabResultSubmissionHistories",
                columns: new[] { "ResourceId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientLabResultSubmissions_AcceptedResultId",
                table: "PatientLabResultSubmissions",
                column: "AcceptedResultId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientLabResultSubmissions_DoctorId_Status_SubmittedAtUtc",
                table: "PatientLabResultSubmissions",
                columns: new[] { "DoctorId", "Status", "SubmittedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientLabResultSubmissions_DoctorPracticeId",
                table: "PatientLabResultSubmissions",
                column: "DoctorPracticeId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientLabResultSubmissions_PatientId_SubmittedAtUtc",
                table: "PatientLabResultSubmissions",
                columns: new[] { "PatientId", "SubmittedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientLabResultSubmissions_RequestId",
                table: "PatientLabResultSubmissions",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientRadiologyResultSubmissionAttachments_SubmissionId",
                table: "PatientRadiologyResultSubmissionAttachments",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientRadiologyResultSubmissionHistories_ResourceId_OccurredAtUtc",
                table: "PatientRadiologyResultSubmissionHistories",
                columns: new[] { "ResourceId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientRadiologyResultSubmissions_AcceptedResultId",
                table: "PatientRadiologyResultSubmissions",
                column: "AcceptedResultId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientRadiologyResultSubmissions_DoctorId_Status_SubmittedAtUtc",
                table: "PatientRadiologyResultSubmissions",
                columns: new[] { "DoctorId", "Status", "SubmittedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientRadiologyResultSubmissions_DoctorPracticeId",
                table: "PatientRadiologyResultSubmissions",
                column: "DoctorPracticeId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientRadiologyResultSubmissions_PatientId_SubmittedAtUtc",
                table: "PatientRadiologyResultSubmissions",
                columns: new[] { "PatientId", "SubmittedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientRadiologyResultSubmissions_RequestId",
                table: "PatientRadiologyResultSubmissions",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyCatalogImportBatches_UploadedAtUtc",
                table: "RadiologyCatalogImportBatches",
                column: "UploadedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyCatalogImportRecords_ImportBatchId_Disposition",
                table: "RadiologyCatalogImportRecords",
                columns: new[] { "ImportBatchId", "Disposition" });

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyCatalogImportRecords_ImportBatchId_LoincCode",
                table: "RadiologyCatalogImportRecords",
                columns: new[] { "ImportBatchId", "LoincCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyCatalogRequestHistories_ResourceId_OccurredAtUtc",
                table: "RadiologyCatalogRequestHistories",
                columns: new[] { "ResourceId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyCatalogRequests_CanonicalCatalogId",
                table: "RadiologyCatalogRequests",
                column: "CanonicalCatalogId");

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyCatalogRequests_RequestedByDoctorId_Status",
                table: "RadiologyCatalogRequests",
                columns: new[] { "RequestedByDoctorId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyProcedureCatalogHistories_ResourceId_OccurredAtUtc",
                table: "RadiologyProcedureCatalogHistories",
                columns: new[] { "ResourceId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyProcedureCatalogs_MergedIntoId",
                table: "RadiologyProcedureCatalogs",
                column: "MergedIntoId");

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyProcedureCatalogs_Status_IsCommonOrder",
                table: "RadiologyProcedureCatalogs",
                columns: new[] { "Status", "IsCommonOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyProcedureCatalogs_Status_Source",
                table: "RadiologyProcedureCatalogs",
                columns: new[] { "Status", "Source" });

            migrationBuilder.CreateIndex(
                name: "UX_RadiologyProcedureCatalog_Loinc",
                table: "RadiologyProcedureCatalogs",
                column: "LoincCode",
                unique: true,
                filter: "[LoincCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyRequestHistories_ResourceId_OccurredAtUtc",
                table: "RadiologyRequestHistories",
                columns: new[] { "ResourceId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyRequestItems_CatalogId",
                table: "RadiologyRequestItems",
                column: "CatalogId");

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyRequestItems_CatalogRequestId",
                table: "RadiologyRequestItems",
                column: "CatalogRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyRequestItems_RequestId",
                table: "RadiologyRequestItems",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyRequestItems_RequestId_CatalogId",
                table: "RadiologyRequestItems",
                columns: new[] { "RequestId", "CatalogId" },
                unique: true,
                filter: "[CatalogId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyRequests_DoctorId_Status",
                table: "RadiologyRequests",
                columns: new[] { "DoctorId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyRequests_DoctorPracticeId_Status",
                table: "RadiologyRequests",
                columns: new[] { "DoctorPracticeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyRequests_PatientId_Status",
                table: "RadiologyRequests",
                columns: new[] { "PatientId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyRequests_RequestedAtUtc",
                table: "RadiologyRequests",
                column: "RequestedAtUtc");

            migrationBuilder.CreateIndex(
                name: "UX_RadiologyRequests_EncounterDraft",
                table: "RadiologyRequests",
                column: "MedicalEncounterId",
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyResultAttachments_ResultVersionId",
                table: "RadiologyResultAttachments",
                column: "ResultVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyResultCoverages_RequestItemId",
                table: "RadiologyResultCoverages",
                column: "RequestItemId");

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyResultCoverages_ResultVersionId_RequestItemId",
                table: "RadiologyResultCoverages",
                columns: new[] { "ResultVersionId", "RequestItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyResultHistories_ResourceId_OccurredAtUtc",
                table: "RadiologyResultHistories",
                columns: new[] { "ResourceId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyResults_AcceptedSubmissionId",
                table: "RadiologyResults",
                column: "AcceptedSubmissionId",
                unique: true,
                filter: "[AcceptedSubmissionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyResults_DoctorId_CreatedAtUtc",
                table: "RadiologyResults",
                columns: new[] { "DoctorId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyResults_DoctorPracticeId",
                table: "RadiologyResults",
                column: "DoctorPracticeId");

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyResults_PatientId_CreatedAtUtc",
                table: "RadiologyResults",
                columns: new[] { "PatientId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyResults_RequestId",
                table: "RadiologyResults",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyResultVersions_ResultId_VersionNumber",
                table: "RadiologyResultVersions",
                columns: new[] { "ResultId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_RadiologyResultVersions_Current",
                table: "RadiologyResultVersions",
                column: "ResultId",
                unique: true,
                filter: "[Status] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LabCatalogImportRecords");

            migrationBuilder.DropTable(
                name: "LabCatalogRequestHistories");

            migrationBuilder.DropTable(
                name: "LabRequestHistories");

            migrationBuilder.DropTable(
                name: "LabResultAttachments");

            migrationBuilder.DropTable(
                name: "LabResultCoverages");

            migrationBuilder.DropTable(
                name: "LabResultHistories");

            migrationBuilder.DropTable(
                name: "LabTestCatalogHistories");

            migrationBuilder.DropTable(
                name: "PatientLabResultSubmissionAttachments");

            migrationBuilder.DropTable(
                name: "PatientLabResultSubmissionHistories");

            migrationBuilder.DropTable(
                name: "PatientRadiologyResultSubmissionAttachments");

            migrationBuilder.DropTable(
                name: "PatientRadiologyResultSubmissionHistories");

            migrationBuilder.DropTable(
                name: "RadiologyCatalogImportRecords");

            migrationBuilder.DropTable(
                name: "RadiologyCatalogRequestHistories");

            migrationBuilder.DropTable(
                name: "RadiologyProcedureCatalogHistories");

            migrationBuilder.DropTable(
                name: "RadiologyRequestHistories");

            migrationBuilder.DropTable(
                name: "RadiologyResultAttachments");

            migrationBuilder.DropTable(
                name: "RadiologyResultCoverages");

            migrationBuilder.DropTable(
                name: "RadiologyResultHistories");

            migrationBuilder.DropTable(
                name: "LabCatalogImportBatches");

            migrationBuilder.DropTable(
                name: "LabRequestItems");

            migrationBuilder.DropTable(
                name: "LabResultVersions");

            migrationBuilder.DropTable(
                name: "PatientLabResultSubmissions");

            migrationBuilder.DropTable(
                name: "PatientRadiologyResultSubmissions");

            migrationBuilder.DropTable(
                name: "RadiologyCatalogImportBatches");

            migrationBuilder.DropTable(
                name: "RadiologyRequestItems");

            migrationBuilder.DropTable(
                name: "RadiologyResultVersions");

            migrationBuilder.DropTable(
                name: "LabCatalogRequests");

            migrationBuilder.DropTable(
                name: "LabResults");

            migrationBuilder.DropTable(
                name: "RadiologyCatalogRequests");

            migrationBuilder.DropTable(
                name: "RadiologyResults");

            migrationBuilder.DropTable(
                name: "LabTestCatalogs");

            migrationBuilder.DropTable(
                name: "LabRequests");

            migrationBuilder.DropTable(
                name: "RadiologyProcedureCatalogs");

            migrationBuilder.DropTable(
                name: "RadiologyRequests");
        }
    }
}
