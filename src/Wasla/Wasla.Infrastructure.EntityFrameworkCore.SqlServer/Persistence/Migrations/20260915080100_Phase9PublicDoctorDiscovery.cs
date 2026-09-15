using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase9PublicDoctorDiscovery : Migration
    {
        private static readonly string[] DoctorPracticePublicIndexColumns = ["DoctorId", "IsActive"];
        private static readonly string[] DoctorQualificationOrderIndexColumns = ["DoctorId", "DisplayOrder"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "DoctorPracticeVisitTypes");

            migrationBuilder.AddColumn<string>(
                name: "Bio",
                table: "Doctors",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedByApplicationUserId",
                table: "Doctors",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedNameAr",
                table: "Doctors",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedNameEn",
                table: "Doctors",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE [Doctors]
                SET [NormalizedNameAr] = LTRIM(RTRIM(
                    REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
                    [NameAr], N'أ', N'ا'), N'إ', N'ا'), N'آ', N'ا'), N'ٱ', N'ا'), N'ى', N'ي'), N'ـ', N''),
                    N'ً', N''), N'ٌ', N''), N'ٍ', N''), N'َ', N''), N'ُ', N''), N'ِ', N''), N'ّ', N''), N'ْ', N''))),
                    [NormalizedNameEn] = CASE WHEN [NameEn] IS NULL THEN NULL ELSE LOWER(LTRIM(RTRIM([NameEn]))) END;

                WHILE EXISTS (
                    SELECT 1 FROM [Doctors]
                    WHERE [NormalizedNameAr] LIKE N'%  %'
                       OR [NormalizedNameEn] LIKE N'%  %')
                BEGIN
                    UPDATE [Doctors]
                    SET [NormalizedNameAr] = REPLACE([NormalizedNameAr], N'  ', N' '),
                        [NormalizedNameEn] = REPLACE([NormalizedNameEn], N'  ', N' ');
                END;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedNameAr",
                table: "Doctors",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "DoctorQualifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorQualifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DoctorQualifications_ApplicationUsers_CreatedByApplicationUserId",
                        column: x => x.CreatedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorQualifications_ApplicationUsers_ModifiedByApplicationUserId",
                        column: x => x.ModifiedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorQualifications_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Doctors_ModifiedByApplicationUserId",
                table: "Doctors",
                column: "ModifiedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Doctors_NormalizedNameAr",
                table: "Doctors",
                column: "NormalizedNameAr");

            migrationBuilder.CreateIndex(
                name: "IX_Doctors_NormalizedNameEn",
                table: "Doctors",
                column: "NormalizedNameEn");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorPractices_DoctorId_IsActive",
                table: "DoctorPractices",
                columns: DoctorPracticePublicIndexColumns);

            migrationBuilder.CreateIndex(
                name: "IX_DoctorQualifications_CreatedByApplicationUserId",
                table: "DoctorQualifications",
                column: "CreatedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorQualifications_DoctorId_DisplayOrder",
                table: "DoctorQualifications",
                columns: DoctorQualificationOrderIndexColumns);

            migrationBuilder.CreateIndex(
                name: "IX_DoctorQualifications_ModifiedByApplicationUserId",
                table: "DoctorQualifications",
                column: "ModifiedByApplicationUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Doctors_ApplicationUsers_ModifiedByApplicationUserId",
                table: "Doctors",
                column: "ModifiedByApplicationUserId",
                principalTable: "ApplicationUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Doctors_ApplicationUsers_ModifiedByApplicationUserId",
                table: "Doctors");

            migrationBuilder.DropTable(
                name: "DoctorQualifications");

            migrationBuilder.DropIndex(
                name: "IX_Doctors_ModifiedByApplicationUserId",
                table: "Doctors");

            migrationBuilder.DropIndex(
                name: "IX_Doctors_NormalizedNameAr",
                table: "Doctors");

            migrationBuilder.DropIndex(
                name: "IX_Doctors_NormalizedNameEn",
                table: "Doctors");

            migrationBuilder.DropIndex(
                name: "IX_DoctorPractices_DoctorId_IsActive",
                table: "DoctorPractices");

            migrationBuilder.DropColumn(
                name: "Bio",
                table: "Doctors");

            migrationBuilder.DropColumn(
                name: "ModifiedByApplicationUserId",
                table: "Doctors");

            migrationBuilder.DropColumn(
                name: "NormalizedNameAr",
                table: "Doctors");

            migrationBuilder.DropColumn(
                name: "NormalizedNameEn",
                table: "Doctors");

            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "DoctorPracticeVisitTypes",
                type: "int",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.Sql(
                "UPDATE [DoctorPracticeVisitTypes] SET [DurationMinutes] = 15 WHERE [Type] = 2;");
        }
    }
}
