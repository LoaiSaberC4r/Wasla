using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDoctorSpecializationsAndPracticeLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DoctorSpecializationRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CurrentRevisionNumber = table.Column<int>(type: "int", nullable: false),
                    SubmittedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorSpecializationRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DoctorSpecializationRequests_ApplicationUsers_ReviewedByApplicationUserId",
                        column: x => x.ReviewedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorSpecializationRequests_ApplicationUsers_SubmittedByApplicationUserId",
                        column: x => x.SubmittedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorSpecializationRequests_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Governorates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Governorates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MedicalSpecializations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DescriptionAr = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DescriptionEn = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RestoredByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RestoredOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalSpecializations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedicalSpecializations_ApplicationUsers_CreatedByApplicationUserId",
                        column: x => x.CreatedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedicalSpecializations_ApplicationUsers_DeletedByApplicationUserId",
                        column: x => x.DeletedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedicalSpecializations_ApplicationUsers_ModifiedByApplicationUserId",
                        column: x => x.ModifiedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedicalSpecializations_ApplicationUsers_RestoredByApplicationUserId",
                        column: x => x.RestoredByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DoctorSpecializationRequestHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorSpecializationRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    PreviousRevisionNumber = table.Column<int>(type: "int", nullable: true),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PerformedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerformedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorSpecializationRequestHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DoctorSpecializationRequestHistories_ApplicationUsers_PerformedByApplicationUserId",
                        column: x => x.PerformedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorSpecializationRequestHistories_DoctorSpecializationRequests_DoctorSpecializationRequestId",
                        column: x => x.DoctorSpecializationRequestId,
                        principalTable: "DoctorSpecializationRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DoctorSpecializationRequestRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorSpecializationRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    SubmittedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorSpecializationRequestRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DoctorSpecializationRequestRevisions_ApplicationUsers_SubmittedByApplicationUserId",
                        column: x => x.SubmittedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorSpecializationRequestRevisions_DoctorSpecializationRequests_DoctorSpecializationRequestId",
                        column: x => x.DoctorSpecializationRequestId,
                        principalTable: "DoctorSpecializationRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Cities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    GovernorateId = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cities_Governorates_GovernorateId",
                        column: x => x.GovernorateId,
                        principalTable: "Governorates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DoctorSpecializations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MedicalSpecializationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorSpecializations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DoctorSpecializations_ApplicationUsers_CreatedByApplicationUserId",
                        column: x => x.CreatedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorSpecializations_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorSpecializations_MedicalSpecializations_MedicalSpecializationId",
                        column: x => x.MedicalSpecializationId,
                        principalTable: "MedicalSpecializations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DoctorSpecializationRequestItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MedicalSpecializationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorSpecializationRequestItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DoctorSpecializationRequestItems_DoctorSpecializationRequestRevisions_RevisionId",
                        column: x => x.RevisionId,
                        principalTable: "DoctorSpecializationRequestRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorSpecializationRequestItems_MedicalSpecializations_MedicalSpecializationId",
                        column: x => x.MedicalSpecializationId,
                        principalTable: "MedicalSpecializations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Areas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    CityId = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Areas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Areas_Cities_CityId",
                        column: x => x.CityId,
                        principalTable: "Cities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DoctorPracticeLocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GovernorateId = table.Column<int>(type: "int", nullable: false),
                    CityId = table.Column<int>(type: "int", nullable: false),
                    AreaId = table.Column<int>(type: "int", nullable: false),
                    DetailedAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorPracticeLocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DoctorPracticeLocations_Areas_AreaId",
                        column: x => x.AreaId,
                        principalTable: "Areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorPracticeLocations_Cities_CityId",
                        column: x => x.CityId,
                        principalTable: "Cities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorPracticeLocations_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorPracticeLocations_Governorates_GovernorateId",
                        column: x => x.GovernorateId,
                        principalTable: "Governorates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Areas_CityId",
                table: "Areas",
                column: "CityId");

            migrationBuilder.CreateIndex(
                name: "IX_Areas_CityId_IsActive_DisplayOrder",
                table: "Areas",
                columns: new[] { "CityId", "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "UX_Areas_CityId_NameAr",
                table: "Areas",
                columns: new[] { "CityId", "NameAr" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Areas_CityId_NameEn",
                table: "Areas",
                columns: new[] { "CityId", "NameEn" },
                unique: true,
                filter: "[NameEn] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Cities_GovernorateId",
                table: "Cities",
                column: "GovernorateId");

            migrationBuilder.CreateIndex(
                name: "IX_Cities_GovernorateId_IsActive_DisplayOrder",
                table: "Cities",
                columns: new[] { "GovernorateId", "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "UX_Cities_GovernorateId_NameAr",
                table: "Cities",
                columns: new[] { "GovernorateId", "NameAr" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Cities_GovernorateId_NameEn",
                table: "Cities",
                columns: new[] { "GovernorateId", "NameEn" },
                unique: true,
                filter: "[NameEn] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorPracticeLocations_AreaId",
                table: "DoctorPracticeLocations",
                column: "AreaId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorPracticeLocations_CityId",
                table: "DoctorPracticeLocations",
                column: "CityId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorPracticeLocations_GovernorateId",
                table: "DoctorPracticeLocations",
                column: "GovernorateId");

            migrationBuilder.CreateIndex(
                name: "UX_DoctorPracticeLocations_DoctorId",
                table: "DoctorPracticeLocations",
                column: "DoctorId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DoctorSpecializationRequestHistories_PerformedByApplicationUserId",
                table: "DoctorSpecializationRequestHistories",
                column: "PerformedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorSpecializationRequestHistories_RequestId_PerformedOnUtc",
                table: "DoctorSpecializationRequestHistories",
                columns: new[] { "DoctorSpecializationRequestId", "PerformedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DoctorSpecializationRequestItems_MedicalSpecializationId",
                table: "DoctorSpecializationRequestItems",
                column: "MedicalSpecializationId");

            migrationBuilder.CreateIndex(
                name: "UX_DoctorSpecializationRequestItems_OnePrimaryPerRevision",
                table: "DoctorSpecializationRequestItems",
                column: "RevisionId",
                unique: true,
                filter: "[IsPrimary] = 1");

            migrationBuilder.CreateIndex(
                name: "UX_DoctorSpecializationRequestItems_RevisionId_MedicalSpecializationId",
                table: "DoctorSpecializationRequestItems",
                columns: new[] { "RevisionId", "MedicalSpecializationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DoctorSpecializationRequestRevisions_SubmittedByApplicationUserId",
                table: "DoctorSpecializationRequestRevisions",
                column: "SubmittedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "UX_DoctorSpecializationRequestRevisions_RequestId_RevisionNumber",
                table: "DoctorSpecializationRequestRevisions",
                columns: new[] { "DoctorSpecializationRequestId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DoctorSpecializationRequests_ReviewedByApplicationUserId",
                table: "DoctorSpecializationRequests",
                column: "ReviewedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorSpecializationRequests_ReviewQueue",
                table: "DoctorSpecializationRequests",
                columns: new[] { "Status", "Type", "SubmittedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DoctorSpecializationRequests_SubmittedByApplicationUserId",
                table: "DoctorSpecializationRequests",
                column: "SubmittedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "UX_DoctorSpecializationRequests_OneOpenPerDoctor",
                table: "DoctorSpecializationRequests",
                column: "DoctorId",
                unique: true,
                filter: "[Status] IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorSpecializations_CreatedByApplicationUserId",
                table: "DoctorSpecializations",
                column: "CreatedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorSpecializations_MedicalSpecializationId",
                table: "DoctorSpecializations",
                column: "MedicalSpecializationId");

            migrationBuilder.CreateIndex(
                name: "UX_DoctorSpecializations_DoctorId_MedicalSpecializationId",
                table: "DoctorSpecializations",
                columns: new[] { "DoctorId", "MedicalSpecializationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_DoctorSpecializations_OnePrimaryPerDoctor",
                table: "DoctorSpecializations",
                column: "DoctorId",
                unique: true,
                filter: "[IsPrimary] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Governorates_IsActive_DisplayOrder",
                table: "Governorates",
                columns: new[] { "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "UX_Governorates_NameAr",
                table: "Governorates",
                column: "NameAr",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Governorates_NameEn",
                table: "Governorates",
                column: "NameEn",
                unique: true,
                filter: "[NameEn] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MedicalSpecializations_CreatedByApplicationUserId",
                table: "MedicalSpecializations",
                column: "CreatedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicalSpecializations_DeletedByApplicationUserId",
                table: "MedicalSpecializations",
                column: "DeletedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicalSpecializations_IsActive_SortOrder",
                table: "MedicalSpecializations",
                columns: new[] { "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_MedicalSpecializations_ModifiedByApplicationUserId",
                table: "MedicalSpecializations",
                column: "ModifiedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicalSpecializations_RestoredByApplicationUserId",
                table: "MedicalSpecializations",
                column: "RestoredByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "UX_MedicalSpecializations_NameAr",
                table: "MedicalSpecializations",
                column: "NameAr",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_MedicalSpecializations_NameEn",
                table: "MedicalSpecializations",
                column: "NameEn",
                unique: true,
                filter: "[NameEn] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DoctorPracticeLocations");

            migrationBuilder.DropTable(
                name: "DoctorSpecializationRequestHistories");

            migrationBuilder.DropTable(
                name: "DoctorSpecializationRequestItems");

            migrationBuilder.DropTable(
                name: "DoctorSpecializations");

            migrationBuilder.DropTable(
                name: "Areas");

            migrationBuilder.DropTable(
                name: "DoctorSpecializationRequestRevisions");

            migrationBuilder.DropTable(
                name: "MedicalSpecializations");

            migrationBuilder.DropTable(
                name: "Cities");

            migrationBuilder.DropTable(
                name: "DoctorSpecializationRequests");

            migrationBuilder.DropTable(
                name: "Governorates");
        }
    }
}
