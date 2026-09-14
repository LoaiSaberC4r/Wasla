using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DoctorPracticesOperationalFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_DoctorPracticeLocations_DoctorId",
                table: "DoctorPracticeLocations");

            migrationBuilder.RenameTable(
                name: "DoctorPracticeLocations",
                newName: "DoctorPractices");

            migrationBuilder.RenameIndex(
                name: "IX_DoctorPracticeLocations_AreaId",
                table: "DoctorPractices",
                newName: "IX_DoctorPractices_AreaId");

            migrationBuilder.RenameIndex(
                name: "IX_DoctorPracticeLocations_CityId",
                table: "DoctorPractices",
                newName: "IX_DoctorPractices_CityId");

            migrationBuilder.RenameIndex(
                name: "IX_DoctorPracticeLocations_GovernorateId",
                table: "DoctorPractices",
                newName: "IX_DoctorPractices_GovernorateId");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByApplicationUserId",
                table: "DoctorPractices",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "DoctorPractices",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsLegacyOnboarding",
                table: "DoctorPractices",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedByApplicationUserId",
                table: "DoctorPractices",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameAr",
                table: "DoctorPractices",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "العيادة الرئيسية");

            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "DoctorPractices",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE practice
                SET CreatedByApplicationUserId = doctor.ApplicationUserId
                FROM DoctorPractices AS practice
                INNER JOIN Doctors AS doctor ON doctor.Id = practice.DoctorId;
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_DoctorPractices_ApplicationUsers_CreatedByApplicationUserId",
                table: "DoctorPractices",
                column: "CreatedByApplicationUserId",
                principalTable: "ApplicationUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DoctorPractices_ApplicationUsers_ModifiedByApplicationUserId",
                table: "DoctorPractices",
                column: "ModifiedByApplicationUserId",
                principalTable: "ApplicationUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.CreateTable(
                name: "Receptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerDoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Receptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Receptions_ApplicationUsers_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Receptions_ApplicationUsers_CreatedByApplicationUserId",
                        column: x => x.CreatedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Receptions_Doctors_OwnerDoctorId",
                        column: x => x.OwnerDoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DoctorPracticeBrandings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorPracticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LogoMediaKey = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PrimaryColor = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true),
                    SecondaryColor = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true),
                    BackgroundColor = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true),
                    TextColor = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true),
                    CreatedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorPracticeBrandings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DoctorPracticeBrandings_ApplicationUsers_CreatedByApplicationUserId",
                        column: x => x.CreatedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorPracticeBrandings_ApplicationUsers_ModifiedByApplicationUserId",
                        column: x => x.ModifiedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorPracticeBrandings_DoctorPractices_DoctorPracticeId",
                        column: x => x.DoctorPracticeId,
                        principalTable: "DoctorPractices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DoctorPracticeConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorPracticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllowOnlineBooking = table.Column<bool>(type: "bit", nullable: false),
                    AllowWalkIn = table.Column<bool>(type: "bit", nullable: false),
                    DefaultSlotDurationMinutes = table.Column<int>(type: "int", nullable: false),
                    CheckInGracePeriodMinutes = table.Column<int>(type: "int", nullable: false),
                    PatientSelfCancellationCutoffMinutes = table.Column<int>(type: "int", nullable: false),
                    MaximumDailyPatients = table.Column<int>(type: "int", nullable: true),
                    MaximumTicketCallAttempts = table.Column<int>(type: "int", nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorPracticeConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DoctorPracticeConfigurations_ApplicationUsers_CreatedByApplicationUserId",
                        column: x => x.CreatedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorPracticeConfigurations_ApplicationUsers_ModifiedByApplicationUserId",
                        column: x => x.ModifiedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorPracticeConfigurations_DoctorPractices_DoctorPracticeId",
                        column: x => x.DoctorPracticeId,
                        principalTable: "DoctorPractices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DoctorPracticeScheduleExceptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorPracticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    SlotDurationMinutes = table.Column<int>(type: "int", nullable: true),
                    CreatedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorPracticeScheduleExceptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DoctorPracticeScheduleExceptions_ApplicationUsers_CreatedByApplicationUserId",
                        column: x => x.CreatedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorPracticeScheduleExceptions_ApplicationUsers_ModifiedByApplicationUserId",
                        column: x => x.ModifiedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorPracticeScheduleExceptions_DoctorPractices_DoctorPracticeId",
                        column: x => x.DoctorPracticeId,
                        principalTable: "DoctorPractices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DoctorPracticeSchedulePeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorPracticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    SlotDurationMinutes = table.Column<int>(type: "int", nullable: false),
                    CreatedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorPracticeSchedulePeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DoctorPracticeSchedulePeriods_ApplicationUsers_CreatedByApplicationUserId",
                        column: x => x.CreatedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorPracticeSchedulePeriods_ApplicationUsers_ModifiedByApplicationUserId",
                        column: x => x.ModifiedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorPracticeSchedulePeriods_DoctorPractices_DoctorPracticeId",
                        column: x => x.DoctorPracticeId,
                        principalTable: "DoctorPractices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DoctorPracticeSegments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorPracticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    ReservedDailyQuota = table.Column<int>(type: "int", nullable: true),
                    QuotaReleaseBeforeMinutes = table.Column<int>(type: "int", nullable: true),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorPracticeSegments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DoctorPracticeSegments_ApplicationUsers_CreatedByApplicationUserId",
                        column: x => x.CreatedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorPracticeSegments_ApplicationUsers_ModifiedByApplicationUserId",
                        column: x => x.ModifiedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorPracticeSegments_DoctorPractices_DoctorPracticeId",
                        column: x => x.DoctorPracticeId,
                        principalTable: "DoctorPractices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DoctorPracticeVisitTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorPracticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorPracticeVisitTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DoctorPracticeVisitTypes_ApplicationUsers_CreatedByApplicationUserId",
                        column: x => x.CreatedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorPracticeVisitTypes_ApplicationUsers_ModifiedByApplicationUserId",
                        column: x => x.ModifiedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorPracticeVisitTypes_DoctorPractices_DoctorPracticeId",
                        column: x => x.DoctorPracticeId,
                        principalTable: "DoctorPractices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReceptionPracticeAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorPracticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceptionPracticeAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceptionPracticeAssignments_ApplicationUsers_CreatedByApplicationUserId",
                        column: x => x.CreatedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceptionPracticeAssignments_ApplicationUsers_ModifiedByApplicationUserId",
                        column: x => x.ModifiedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceptionPracticeAssignments_DoctorPractices_DoctorPracticeId",
                        column: x => x.DoctorPracticeId,
                        principalTable: "DoctorPractices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceptionPracticeAssignments_Receptions_ReceptionId",
                        column: x => x.ReceptionId,
                        principalTable: "Receptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DoctorPracticeSegmentVisitTypePrices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorPracticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SegmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VisitTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorPracticeSegmentVisitTypePrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DoctorPracticeSegmentVisitTypePrices_ApplicationUsers_CreatedByApplicationUserId",
                        column: x => x.CreatedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorPracticeSegmentVisitTypePrices_ApplicationUsers_ModifiedByApplicationUserId",
                        column: x => x.ModifiedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorPracticeSegmentVisitTypePrices_DoctorPracticeSegments_SegmentId",
                        column: x => x.SegmentId,
                        principalTable: "DoctorPracticeSegments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorPracticeSegmentVisitTypePrices_DoctorPracticeVisitTypes_VisitTypeId",
                        column: x => x.VisitTypeId,
                        principalTable: "DoctorPracticeVisitTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorPracticeSegmentVisitTypePrices_DoctorPractices_DoctorPracticeId",
                        column: x => x.DoctorPracticeId,
                        principalTable: "DoctorPractices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReceptionPracticeAssignmentPermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceptionPracticeAssignmentPermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceptionPracticeAssignmentPermissions_ApplicationUsers_CreatedByApplicationUserId",
                        column: x => x.CreatedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceptionPracticeAssignmentPermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceptionPracticeAssignmentPermissions_ReceptionPracticeAssignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "ReceptionPracticeAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO DoctorPracticeConfigurations
                    (Id, DoctorPracticeId, AllowOnlineBooking, AllowWalkIn, DefaultSlotDurationMinutes,
                     CheckInGracePeriodMinutes, PatientSelfCancellationCutoffMinutes, MaximumDailyPatients,
                     MaximumTicketCallAttempts, TimeZoneId, CreatedByApplicationUserId, ModifiedByApplicationUserId,
                     CreatedOnUtc, ModifiedOnUtc)
                SELECT NEWID(), Id, 1, 1, 20, 15, 120, NULL, 3, N'Africa/Cairo',
                       CreatedByApplicationUserId, NULL, CreatedOnUtc, NULL
                FROM DoctorPractices;

                INSERT INTO DoctorPracticeBrandings
                    (Id, DoctorPracticeId, LogoMediaKey, PrimaryColor, SecondaryColor, BackgroundColor, TextColor,
                     CreatedByApplicationUserId, ModifiedByApplicationUserId, CreatedOnUtc, ModifiedOnUtc)
                SELECT NEWID(), Id, NULL, N'#176B87', N'#64CCC5', N'#FFFFFF', N'#102A43',
                       CreatedByApplicationUserId, NULL, CreatedOnUtc, NULL
                FROM DoctorPractices;

                INSERT INTO DoctorPracticeSegments
                    (Id, DoctorPracticeId, NameAr, NameEn, Priority, ReservedDailyQuota,
                     QuotaReleaseBeforeMinutes, IsDefault, IsActive, CreatedByApplicationUserId,
                     ModifiedByApplicationUserId, CreatedOnUtc, ModifiedOnUtc)
                SELECT NEWID(), Id, N'عادي', N'Normal', 0, NULL, NULL, 1, 1,
                       CreatedByApplicationUserId, NULL, CreatedOnUtc, NULL
                FROM DoctorPractices;

                INSERT INTO DoctorPracticeVisitTypes
                    (Id, DoctorPracticeId, Type, NameAr, NameEn, DurationMinutes, IsActive,
                     CreatedByApplicationUserId, ModifiedByApplicationUserId, CreatedOnUtc, ModifiedOnUtc)
                SELECT NEWID(), Id, 1, N'كشف جديد', N'New consultation', 30, 1,
                       CreatedByApplicationUserId, NULL, CreatedOnUtc, NULL
                FROM DoctorPractices;

                INSERT INTO DoctorPracticeVisitTypes
                    (Id, DoctorPracticeId, Type, NameAr, NameEn, DurationMinutes, IsActive,
                     CreatedByApplicationUserId, ModifiedByApplicationUserId, CreatedOnUtc, ModifiedOnUtc)
                SELECT NEWID(), Id, 2, N'متابعة', N'Follow-up', 15, 1,
                       CreatedByApplicationUserId, NULL, CreatedOnUtc, NULL
                FROM DoctorPractices;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_DoctorPracticeBrandings_CreatedByApplicationUserId",
                table: "DoctorPracticeBrandings",
                column: "CreatedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorPracticeBrandings_ModifiedByApplicationUserId",
                table: "DoctorPracticeBrandings",
                column: "ModifiedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "UX_DoctorPracticeBrandings_DoctorPracticeId",
                table: "DoctorPracticeBrandings",
                column: "DoctorPracticeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DoctorPracticeConfigurations_CreatedByApplicationUserId",
                table: "DoctorPracticeConfigurations",
                column: "CreatedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorPracticeConfigurations_ModifiedByApplicationUserId",
                table: "DoctorPracticeConfigurations",
                column: "ModifiedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "UX_DoctorPracticeConfigurations_DoctorPracticeId",
                table: "DoctorPracticeConfigurations",
                column: "DoctorPracticeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DoctorPractices_CreatedByApplicationUserId",
                table: "DoctorPractices",
                column: "CreatedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorPractices_DoctorId",
                table: "DoctorPractices",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorPractices_ModifiedByApplicationUserId",
                table: "DoctorPractices",
                column: "ModifiedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "UX_DoctorPractices_OneLegacyOnboardingPerDoctor",
                table: "DoctorPractices",
                column: "DoctorId",
                unique: true,
                filter: "[IsLegacyOnboarding] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorPracticeScheduleExceptions_CreatedByApplicationUserId",
                table: "DoctorPracticeScheduleExceptions",
                column: "CreatedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorPracticeScheduleExceptions_ModifiedByApplicationUserId",
                table: "DoctorPracticeScheduleExceptions",
                column: "ModifiedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorPracticeScheduleExceptions_Practice_Date",
                table: "DoctorPracticeScheduleExceptions",
                columns: new[] { "DoctorPracticeId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_DoctorPracticeSchedulePeriods_CreatedByApplicationUserId",
                table: "DoctorPracticeSchedulePeriods",
                column: "CreatedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorPracticeSchedulePeriods_ModifiedByApplicationUserId",
                table: "DoctorPracticeSchedulePeriods",
                column: "ModifiedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorPracticeSchedulePeriods_Practice_Day",
                table: "DoctorPracticeSchedulePeriods",
                columns: new[] { "DoctorPracticeId", "DayOfWeek" });

            migrationBuilder.CreateIndex(
                name: "IX_DoctorPracticeSegments_CreatedByApplicationUserId",
                table: "DoctorPracticeSegments",
                column: "CreatedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorPracticeSegments_ModifiedByApplicationUserId",
                table: "DoctorPracticeSegments",
                column: "ModifiedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorPracticeSegments_PracticeId",
                table: "DoctorPracticeSegments",
                column: "DoctorPracticeId");

            migrationBuilder.CreateIndex(
                name: "UX_DoctorPracticeSegments_OneDefaultPerPractice",
                table: "DoctorPracticeSegments",
                column: "DoctorPracticeId",
                unique: true,
                filter: "[IsDefault] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorPracticeSegmentVisitTypePrices_CreatedByApplicationUserId",
                table: "DoctorPracticeSegmentVisitTypePrices",
                column: "CreatedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorPracticeSegmentVisitTypePrices_ModifiedByApplicationUserId",
                table: "DoctorPracticeSegmentVisitTypePrices",
                column: "ModifiedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorPracticeSegmentVisitTypePrices_SegmentId",
                table: "DoctorPracticeSegmentVisitTypePrices",
                column: "SegmentId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorPracticeSegmentVisitTypePrices_VisitTypeId",
                table: "DoctorPracticeSegmentVisitTypePrices",
                column: "VisitTypeId");

            migrationBuilder.CreateIndex(
                name: "UX_DoctorPracticePrices_Practice_Segment_VisitType",
                table: "DoctorPracticeSegmentVisitTypePrices",
                columns: new[] { "DoctorPracticeId", "SegmentId", "VisitTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DoctorPracticeVisitTypes_CreatedByApplicationUserId",
                table: "DoctorPracticeVisitTypes",
                column: "CreatedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorPracticeVisitTypes_ModifiedByApplicationUserId",
                table: "DoctorPracticeVisitTypes",
                column: "ModifiedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "UX_DoctorPracticeVisitTypes_Practice_Type",
                table: "DoctorPracticeVisitTypes",
                columns: new[] { "DoctorPracticeId", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReceptionPracticeAssignmentPermissions_CreatedByApplicationUserId",
                table: "ReceptionPracticeAssignmentPermissions",
                column: "CreatedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceptionPracticeAssignmentPermissions_PermissionId",
                table: "ReceptionPracticeAssignmentPermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "UX_ReceptionAssignmentPermissions_Assignment_Permission",
                table: "ReceptionPracticeAssignmentPermissions",
                columns: new[] { "AssignmentId", "PermissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReceptionPracticeAssignments_CreatedByApplicationUserId",
                table: "ReceptionPracticeAssignments",
                column: "CreatedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceptionPracticeAssignments_ModifiedByApplicationUserId",
                table: "ReceptionPracticeAssignments",
                column: "ModifiedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceptionPracticeAssignments_PracticeId",
                table: "ReceptionPracticeAssignments",
                column: "DoctorPracticeId");

            migrationBuilder.CreateIndex(
                name: "UX_ReceptionPracticeAssignments_Reception_Practice",
                table: "ReceptionPracticeAssignments",
                columns: new[] { "ReceptionId", "DoctorPracticeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Receptions_CreatedByApplicationUserId",
                table: "Receptions",
                column: "CreatedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Receptions_OwnerDoctorId",
                table: "Receptions",
                column: "OwnerDoctorId");

            migrationBuilder.CreateIndex(
                name: "UX_Receptions_ApplicationUserId",
                table: "Receptions",
                column: "ApplicationUserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT DoctorId
                    FROM DoctorPractices
                    GROUP BY DoctorId
                    HAVING COUNT(*) > 1)
                THROW 51000, 'Cannot downgrade while a doctor owns multiple practices.', 1;
                """);

            migrationBuilder.DropTable(
                name: "DoctorPracticeBrandings");

            migrationBuilder.DropTable(
                name: "DoctorPracticeConfigurations");

            migrationBuilder.DropTable(
                name: "DoctorPracticeScheduleExceptions");

            migrationBuilder.DropTable(
                name: "DoctorPracticeSchedulePeriods");

            migrationBuilder.DropTable(
                name: "DoctorPracticeSegmentVisitTypePrices");

            migrationBuilder.DropTable(
                name: "ReceptionPracticeAssignmentPermissions");

            migrationBuilder.DropTable(
                name: "DoctorPracticeSegments");

            migrationBuilder.DropTable(
                name: "DoctorPracticeVisitTypes");

            migrationBuilder.DropTable(
                name: "ReceptionPracticeAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_DoctorPractices_ApplicationUsers_CreatedByApplicationUserId",
                table: "DoctorPractices");

            migrationBuilder.DropForeignKey(
                name: "FK_DoctorPractices_ApplicationUsers_ModifiedByApplicationUserId",
                table: "DoctorPractices");

            migrationBuilder.DropIndex(
                name: "IX_DoctorPractices_CreatedByApplicationUserId",
                table: "DoctorPractices");

            migrationBuilder.DropIndex(
                name: "IX_DoctorPractices_DoctorId",
                table: "DoctorPractices");

            migrationBuilder.DropIndex(
                name: "IX_DoctorPractices_ModifiedByApplicationUserId",
                table: "DoctorPractices");

            migrationBuilder.DropIndex(
                name: "UX_DoctorPractices_OneLegacyOnboardingPerDoctor",
                table: "DoctorPractices");

            migrationBuilder.DropColumn(
                name: "CreatedByApplicationUserId",
                table: "DoctorPractices");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "DoctorPractices");

            migrationBuilder.DropColumn(
                name: "IsLegacyOnboarding",
                table: "DoctorPractices");

            migrationBuilder.DropColumn(
                name: "ModifiedByApplicationUserId",
                table: "DoctorPractices");

            migrationBuilder.DropColumn(
                name: "NameAr",
                table: "DoctorPractices");

            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "DoctorPractices");

            migrationBuilder.RenameIndex(
                name: "IX_DoctorPractices_AreaId",
                table: "DoctorPractices",
                newName: "IX_DoctorPracticeLocations_AreaId");

            migrationBuilder.RenameIndex(
                name: "IX_DoctorPractices_CityId",
                table: "DoctorPractices",
                newName: "IX_DoctorPracticeLocations_CityId");

            migrationBuilder.RenameIndex(
                name: "IX_DoctorPractices_GovernorateId",
                table: "DoctorPractices",
                newName: "IX_DoctorPracticeLocations_GovernorateId");

            migrationBuilder.RenameTable(
                name: "DoctorPractices",
                newName: "DoctorPracticeLocations");

            migrationBuilder.CreateIndex(
                name: "UX_DoctorPracticeLocations_DoctorId",
                table: "DoctorPracticeLocations",
                column: "DoctorId",
                unique: true);

            migrationBuilder.DropTable(
                name: "Receptions");

        }
    }
}
