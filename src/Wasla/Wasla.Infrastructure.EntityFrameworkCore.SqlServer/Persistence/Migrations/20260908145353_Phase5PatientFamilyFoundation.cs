using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase5PatientFamilyFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Patients",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "Patients",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Patients",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: Array.Empty<byte>());

            migrationBuilder.CreateTable(
                name: "Families",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Families", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Families_ApplicationUsers_CreatedByApplicationUserId",
                        column: x => x.CreatedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PatientAccountLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    VerifiedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientAccountLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientAccountLinks_ApplicationUsers_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientAccountLinks_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM [Patients] AS [p]
                    LEFT JOIN [ApplicationUsers] AS [u] ON [u].[Id] = [p].[ApplicationUserId]
                    WHERE [u].[Id] IS NULL)
                    THROW 51000, 'Legacy Patient data contains an invalid ApplicationUserId.', 1;

                IF EXISTS (
                    SELECT [ApplicationUserId]
                    FROM [Patients]
                    GROUP BY [ApplicationUserId]
                    HAVING COUNT_BIG(*) > 1)
                    THROW 51001, 'Legacy Patient data violates account-link cardinality.', 1;

                UPDATE [p]
                SET [p].[PhoneNumber] = [u].[PhoneNumber],
                    [p].[Email] = [u].[Email]
                FROM [Patients] AS [p]
                INNER JOIN [ApplicationUsers] AS [u] ON [u].[Id] = [p].[ApplicationUserId];

                INSERT INTO [PatientAccountLinks]
                    ([Id], [ApplicationUserId], [PatientId], [Source], [VerifiedOnUtc], [CreatedOnUtc])
                SELECT NEWID(), [p].[ApplicationUserId], [p].[Id], 1, [p].[CreatedOnUtc], [p].[CreatedOnUtc]
                FROM [Patients] AS [p];
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Patients_ApplicationUsers_ApplicationUserId",
                table: "Patients");

            migrationBuilder.DropIndex(
                name: "UX_Patients_ApplicationUserId",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "ApplicationUserId",
                table: "Patients");

            migrationBuilder.CreateTable(
                name: "PatientContacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RelationshipType = table.Column<int>(type: "int", nullable: false),
                    LinkedPatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RestoredOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientContacts_Patients_LinkedPatientId",
                        column: x => x.LinkedPatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientContacts_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FamilyRelationshipRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestType = table.Column<int>(type: "int", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequesterPatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetPatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequesterClaimedRole = table.Column<int>(type: "int", nullable: false),
                    TargetClaimedRole = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CurrentRevisionNumber = table.Column<int>(type: "int", nullable: false),
                    SubmittedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedBySuperAdminApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ModificationMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyRelationshipRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FamilyRelationshipRequests_ApplicationUsers_ReviewedBySuperAdminApplicationUserId",
                        column: x => x.ReviewedBySuperAdminApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyRelationshipRequests_ApplicationUsers_SubmittedByApplicationUserId",
                        column: x => x.SubmittedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyRelationshipRequests_Families_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "Families",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyRelationshipRequests_Patients_RequesterPatientId",
                        column: x => x.RequesterPatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyRelationshipRequests_Patients_TargetPatientId",
                        column: x => x.TargetPatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FamilyMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    VerifiedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VerifiedBySuperAdminUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddedThroughRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JoinedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FamilyMembers_ApplicationUsers_VerifiedBySuperAdminUserId",
                        column: x => x.VerifiedBySuperAdminUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyMembers_Families_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "Families",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyMembers_FamilyRelationshipRequests_AddedThroughRequestId",
                        column: x => x.AddedThroughRequestId,
                        principalTable: "FamilyRelationshipRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyMembers_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FamilyRelationshipDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FamilyRelationshipRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    DocumentType = table.Column<int>(type: "int", nullable: false),
                    MediaKey = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    UploadedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyRelationshipDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FamilyRelationshipDocuments_ApplicationUsers_UploadedByApplicationUserId",
                        column: x => x.UploadedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyRelationshipDocuments_FamilyRelationshipRequests_FamilyRelationshipRequestId",
                        column: x => x.FamilyRelationshipRequestId,
                        principalTable: "FamilyRelationshipRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FamilyRelationshipRequestHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    OldStatus = table.Column<int>(type: "int", nullable: true),
                    NewStatus = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    MessageOrReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PerformedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerformedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyRelationshipRequestHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FamilyRelationshipRequestHistories_ApplicationUsers_PerformedByApplicationUserId",
                        column: x => x.PerformedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyRelationshipRequestHistories_FamilyRelationshipRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "FamilyRelationshipRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Patients_DateOfBirth",
                table: "Patients",
                column: "DateOfBirth");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_NameAr",
                table: "Patients",
                column: "NameAr");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_PhoneNumber",
                table: "Patients",
                column: "PhoneNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Families_CreatedByApplicationUserId",
                table: "Families",
                column: "CreatedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Families_Status",
                table: "Families",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMembers_AddedThroughRequestId",
                table: "FamilyMembers",
                column: "AddedThroughRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMembers_VerifiedBySuperAdminUserId",
                table: "FamilyMembers",
                column: "VerifiedBySuperAdminUserId");

            migrationBuilder.CreateIndex(
                name: "UX_FamilyMembers_FamilyId_PatientId",
                table: "FamilyMembers",
                columns: new[] { "FamilyId", "PatientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_FamilyMembers_OneActiveFamilyPerPatient",
                table: "FamilyMembers",
                column: "PatientId",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "UX_FamilyMembers_OneActiveParentRole",
                table: "FamilyMembers",
                columns: new[] { "FamilyId", "Role" },
                unique: true,
                filter: "[IsActive] = 1 AND [Role] IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyRelationshipDocuments_RequestId_RevisionNumber",
                table: "FamilyRelationshipDocuments",
                columns: new[] { "FamilyRelationshipRequestId", "RevisionNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_FamilyRelationshipDocuments_UploadedByApplicationUserId",
                table: "FamilyRelationshipDocuments",
                column: "UploadedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyRelationshipRequestHistories_PerformedByApplicationUserId",
                table: "FamilyRelationshipRequestHistories",
                column: "PerformedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyRelationshipRequestHistories_RequestId_PerformedOnUtc",
                table: "FamilyRelationshipRequestHistories",
                columns: new[] { "RequestId", "PerformedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FamilyRelationshipRequests_FamilyId",
                table: "FamilyRelationshipRequests",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyRelationshipRequests_RequesterPatientId",
                table: "FamilyRelationshipRequests",
                column: "RequesterPatientId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyRelationshipRequests_ReviewedBySuperAdminApplicationUserId",
                table: "FamilyRelationshipRequests",
                column: "ReviewedBySuperAdminApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyRelationshipRequests_Status",
                table: "FamilyRelationshipRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyRelationshipRequests_SubmittedByApplicationUserId",
                table: "FamilyRelationshipRequests",
                column: "SubmittedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyRelationshipRequests_SubmittedOnUtc",
                table: "FamilyRelationshipRequests",
                column: "SubmittedOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyRelationshipRequests_TargetPatientId",
                table: "FamilyRelationshipRequests",
                column: "TargetPatientId");

            migrationBuilder.CreateIndex(
                name: "UX_FamilyRelationshipRequests_OneOpenRelationship",
                table: "FamilyRelationshipRequests",
                columns: new[] { "RequesterPatientId", "TargetPatientId", "RequestType", "FamilyId" },
                unique: true,
                filter: "[Status] IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "UX_PatientAccountLinks_ApplicationUserId",
                table: "PatientAccountLinks",
                column: "ApplicationUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_PatientAccountLinks_PatientId",
                table: "PatientAccountLinks",
                column: "PatientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatientContacts_LinkedPatientId",
                table: "PatientContacts",
                column: "LinkedPatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientContacts_PatientId",
                table: "PatientContacts",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientContacts_PhoneNumber",
                table: "PatientContacts",
                column: "PhoneNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ApplicationUserId",
                table: "Patients",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE [p]
                SET [p].[ApplicationUserId] = [l].[ApplicationUserId]
                FROM [Patients] AS [p]
                LEFT JOIN [PatientAccountLinks] AS [l] ON [l].[PatientId] = [p].[Id];

                IF EXISTS (SELECT 1 FROM [Patients] WHERE [ApplicationUserId] IS NULL)
                    THROW 51002, 'Cannot restore the legacy schema because one or more Patients have no account link.', 1;

                ALTER TABLE [Patients] ALTER COLUMN [ApplicationUserId] uniqueidentifier NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "UX_Patients_ApplicationUserId",
                table: "Patients",
                column: "ApplicationUserId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Patients_ApplicationUsers_ApplicationUserId",
                table: "Patients",
                column: "ApplicationUserId",
                principalTable: "ApplicationUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropTable(
                name: "FamilyMembers");

            migrationBuilder.DropTable(
                name: "FamilyRelationshipDocuments");

            migrationBuilder.DropTable(
                name: "FamilyRelationshipRequestHistories");

            migrationBuilder.DropTable(
                name: "PatientAccountLinks");

            migrationBuilder.DropTable(
                name: "PatientContacts");

            migrationBuilder.DropTable(
                name: "FamilyRelationshipRequests");

            migrationBuilder.DropTable(
                name: "Families");

            migrationBuilder.DropIndex(
                name: "IX_Patients_DateOfBirth",
                table: "Patients");

            migrationBuilder.DropIndex(
                name: "IX_Patients_NameAr",
                table: "Patients");

            migrationBuilder.DropIndex(
                name: "IX_Patients_PhoneNumber",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Patients");

        }
    }
}
#pragma warning restore CA1861
