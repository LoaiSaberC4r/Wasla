#pragma warning disable CA1861 // Generated migration arrays.
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase16FamilyMedicalAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FamilyMedicalEligibilityReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdultPatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChildPatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdultFamilyMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChildFamilyMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PolicyVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BasisReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReviewReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReviewedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyMedicalEligibilityReviews", x => x.Id);
                    table.CheckConstraint("CK_FamilyMedicalEligibility_Status", "[Status] IN (1,2,3,4)");
                    table.ForeignKey(
                        name: "FK_FamilyMedicalEligibilityReviews_ApplicationUsers_ReviewedByApplicationUserId",
                        column: x => x.ReviewedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyMedicalEligibilityReviews_Families_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "Families",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyMedicalEligibilityReviews_FamilyMembers_AdultFamilyMemberId",
                        column: x => x.AdultFamilyMemberId,
                        principalTable: "FamilyMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyMedicalEligibilityReviews_FamilyMembers_ChildFamilyMemberId",
                        column: x => x.ChildFamilyMemberId,
                        principalTable: "FamilyMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyMedicalEligibilityReviews_Patients_AdultPatientId",
                        column: x => x.AdultPatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyMedicalEligibilityReviews_Patients_ChildPatientId",
                        column: x => x.ChildPatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FamilyMedicalEligibilityReviewHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OldStatus = table.Column<int>(type: "int", nullable: true),
                    NewStatus = table.Column<int>(type: "int", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    ActorApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReasonOrMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyMedicalEligibilityReviewHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FamilyMedicalEligibilityReviewHistories_ApplicationUsers_ActorApplicationUserId",
                        column: x => x.ActorApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyMedicalEligibilityReviewHistories_FamilyMedicalEligibilityReviews_ParentId",
                        column: x => x.ParentId,
                        principalTable: "FamilyMedicalEligibilityReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FamilyMedicalAccessRequestDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    DocumentType = table.Column<int>(type: "int", nullable: false),
                    MediaKey = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UploadedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyMedicalAccessRequestDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FamilyMedicalAccessRequestDocuments_ApplicationUsers_UploadedByApplicationUserId",
                        column: x => x.UploadedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FamilyMedicalAccessRequestHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OldStatus = table.Column<int>(type: "int", nullable: true),
                    NewStatus = table.Column<int>(type: "int", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    ActorApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReasonOrMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyMedicalAccessRequestHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FamilyMedicalAccessRequestHistories_ApplicationUsers_ActorApplicationUserId",
                        column: x => x.ActorApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FamilyMedicalAccessRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChildPatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AffectedAdultPatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedByPatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CurrentRevisionNumber = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WithdrawnAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedBySuperAdminApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WithdrawnByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AffectedRestrictionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyMedicalAccessRequests", x => x.Id);
                    table.CheckConstraint("CK_FamilyMedicalRequests_Actors", "[ChildPatientId] <> [AffectedAdultPatientId] AND (([RequestType] = 1 AND [SubmittedByPatientId] <> [AffectedAdultPatientId]) OR ([RequestType] = 2 AND [SubmittedByPatientId] = [AffectedAdultPatientId]))");
                    table.CheckConstraint("CK_FamilyMedicalRequests_Revision", "[CurrentRevisionNumber] >= 1");
                    table.CheckConstraint("CK_FamilyMedicalRequests_Status", "[Status] IN (1,2,3,4,5)");
                    table.CheckConstraint("CK_FamilyMedicalRequests_Type", "[RequestType] IN (1,2)");
                    table.ForeignKey(
                        name: "FK_FamilyMedicalAccessRequests_ApplicationUsers_ReviewedBySuperAdminApplicationUserId",
                        column: x => x.ReviewedBySuperAdminApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyMedicalAccessRequests_ApplicationUsers_SubmittedByApplicationUserId",
                        column: x => x.SubmittedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyMedicalAccessRequests_ApplicationUsers_WithdrawnByApplicationUserId",
                        column: x => x.WithdrawnByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyMedicalAccessRequests_Families_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "Families",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyMedicalAccessRequests_Patients_AffectedAdultPatientId",
                        column: x => x.AffectedAdultPatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyMedicalAccessRequests_Patients_ChildPatientId",
                        column: x => x.ChildPatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyMedicalAccessRequests_Patients_SubmittedByPatientId",
                        column: x => x.SubmittedByPatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FamilyMedicalAccessRestrictions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChildPatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AffectedAdultPatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RestrictionApprovedByRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestrictedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RestrictedBySuperAdminApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LiftedByRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LiftedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LiftedBySuperAdminApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyMedicalAccessRestrictions", x => x.Id);
                    table.CheckConstraint("CK_FamilyMedicalRestrictions_Status", "[Status] IN (1,2)");
                    table.ForeignKey(
                        name: "FK_FamilyMedicalAccessRestrictions_ApplicationUsers_LiftedBySuperAdminApplicationUserId",
                        column: x => x.LiftedBySuperAdminApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyMedicalAccessRestrictions_ApplicationUsers_RestrictedBySuperAdminApplicationUserId",
                        column: x => x.RestrictedBySuperAdminApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyMedicalAccessRestrictions_Families_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "Families",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyMedicalAccessRestrictions_FamilyMedicalAccessRequests_LiftedByRequestId",
                        column: x => x.LiftedByRequestId,
                        principalTable: "FamilyMedicalAccessRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyMedicalAccessRestrictions_FamilyMedicalAccessRequests_RestrictionApprovedByRequestId",
                        column: x => x.RestrictionApprovedByRequestId,
                        principalTable: "FamilyMedicalAccessRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyMedicalAccessRestrictions_Patients_AffectedAdultPatientId",
                        column: x => x.AffectedAdultPatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyMedicalAccessRestrictions_Patients_ChildPatientId",
                        column: x => x.ChildPatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FamilyMedicalAccessRestrictionHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OldStatus = table.Column<int>(type: "int", nullable: true),
                    NewStatus = table.Column<int>(type: "int", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    ActorApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReasonOrMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyMedicalAccessRestrictionHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FamilyMedicalAccessRestrictionHistories_ApplicationUsers_ActorApplicationUserId",
                        column: x => x.ActorApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyMedicalAccessRestrictionHistories_FamilyMedicalAccessRestrictions_ParentId",
                        column: x => x.ParentId,
                        principalTable: "FamilyMedicalAccessRestrictions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMedicalAccessRequestDocuments_RequestId_RevisionNumber",
                table: "FamilyMedicalAccessRequestDocuments",
                columns: new[] { "RequestId", "RevisionNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMedicalAccessRequestDocuments_UploadedByApplicationUserId",
                table: "FamilyMedicalAccessRequestDocuments",
                column: "UploadedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMedicalAccessRequestHistories_ActorApplicationUserId",
                table: "FamilyMedicalAccessRequestHistories",
                column: "ActorApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMedicalAccessRequestHistories_ParentId_OccurredAtUtc_Id",
                table: "FamilyMedicalAccessRequestHistories",
                columns: new[] { "ParentId", "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMedicalAccessRequests_AffectedAdultPatientId",
                table: "FamilyMedicalAccessRequests",
                column: "AffectedAdultPatientId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMedicalAccessRequests_AffectedRestrictionId",
                table: "FamilyMedicalAccessRequests",
                column: "AffectedRestrictionId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMedicalAccessRequests_ChildPatientId",
                table: "FamilyMedicalAccessRequests",
                column: "ChildPatientId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMedicalAccessRequests_ReviewedBySuperAdminApplicationUserId",
                table: "FamilyMedicalAccessRequests",
                column: "ReviewedBySuperAdminApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMedicalAccessRequests_Status_SubmittedAtUtc_Id",
                table: "FamilyMedicalAccessRequests",
                columns: new[] { "Status", "SubmittedAtUtc", "Id" },
                descending: new[] { false, true, false });

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMedicalAccessRequests_SubmittedByApplicationUserId_SubmittedAtUtc_Id",
                table: "FamilyMedicalAccessRequests",
                columns: new[] { "SubmittedByApplicationUserId", "SubmittedAtUtc", "Id" },
                descending: new[] { false, true, false });

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMedicalAccessRequests_SubmittedByPatientId",
                table: "FamilyMedicalAccessRequests",
                column: "SubmittedByPatientId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMedicalAccessRequests_WithdrawnByApplicationUserId",
                table: "FamilyMedicalAccessRequests",
                column: "WithdrawnByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "UX_FamilyMedicalRequests_Open",
                table: "FamilyMedicalAccessRequests",
                columns: new[] { "FamilyId", "ChildPatientId", "AffectedAdultPatientId", "RequestType" },
                unique: true,
                filter: "[Status] IN (1,2)");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMedicalAccessRestrictionHistories_ActorApplicationUserId",
                table: "FamilyMedicalAccessRestrictionHistories",
                column: "ActorApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMedicalAccessRestrictionHistories_ParentId_OccurredAtUtc_Id",
                table: "FamilyMedicalAccessRestrictionHistories",
                columns: new[] { "ParentId", "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMedicalAccessRestrictions_AffectedAdultPatientId",
                table: "FamilyMedicalAccessRestrictions",
                column: "AffectedAdultPatientId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMedicalAccessRestrictions_ChildPatientId",
                table: "FamilyMedicalAccessRestrictions",
                column: "ChildPatientId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMedicalAccessRestrictions_LiftedByRequestId",
                table: "FamilyMedicalAccessRestrictions",
                column: "LiftedByRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMedicalAccessRestrictions_LiftedBySuperAdminApplicationUserId",
                table: "FamilyMedicalAccessRestrictions",
                column: "LiftedBySuperAdminApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMedicalAccessRestrictions_RestrictedBySuperAdminApplicationUserId",
                table: "FamilyMedicalAccessRestrictions",
                column: "RestrictedBySuperAdminApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMedicalAccessRestrictions_RestrictionApprovedByRequestId",
                table: "FamilyMedicalAccessRestrictions",
                column: "RestrictionApprovedByRequestId");

            migrationBuilder.CreateIndex(
                name: "UX_FamilyMedicalRestrictions_Active",
                table: "FamilyMedicalAccessRestrictions",
                columns: new[] { "FamilyId", "ChildPatientId", "AffectedAdultPatientId" },
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMedicalEligibilityReviewHistories_ActorApplicationUserId",
                table: "FamilyMedicalEligibilityReviewHistories",
                column: "ActorApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMedicalEligibilityReviewHistories_ParentId_OccurredAtUtc_Id",
                table: "FamilyMedicalEligibilityReviewHistories",
                columns: new[] { "ParentId", "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMedicalEligibilityReviews_AdultFamilyMemberId",
                table: "FamilyMedicalEligibilityReviews",
                column: "AdultFamilyMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMedicalEligibilityReviews_AdultPatientId",
                table: "FamilyMedicalEligibilityReviews",
                column: "AdultPatientId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMedicalEligibilityReviews_ChildFamilyMemberId",
                table: "FamilyMedicalEligibilityReviews",
                column: "ChildFamilyMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMedicalEligibilityReviews_ChildPatientId",
                table: "FamilyMedicalEligibilityReviews",
                column: "ChildPatientId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMedicalEligibilityReviews_ReviewedByApplicationUserId",
                table: "FamilyMedicalEligibilityReviews",
                column: "ReviewedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMedicalEligibilityReviews_Status_CreatedAtUtc",
                table: "FamilyMedicalEligibilityReviews",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_FamilyMedicalEligibility_Pair",
                table: "FamilyMedicalEligibilityReviews",
                columns: new[] { "FamilyId", "AdultPatientId", "ChildPatientId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_FamilyMedicalAccessRequestDocuments_FamilyMedicalAccessRequests_RequestId",
                table: "FamilyMedicalAccessRequestDocuments",
                column: "RequestId",
                principalTable: "FamilyMedicalAccessRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FamilyMedicalAccessRequestHistories_FamilyMedicalAccessRequests_ParentId",
                table: "FamilyMedicalAccessRequestHistories",
                column: "ParentId",
                principalTable: "FamilyMedicalAccessRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FamilyMedicalAccessRequests_FamilyMedicalAccessRestrictions_AffectedRestrictionId",
                table: "FamilyMedicalAccessRequests",
                column: "AffectedRestrictionId",
                principalTable: "FamilyMedicalAccessRestrictions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            // Existing verified membership creates review candidates, never an approved entitlement.
            migrationBuilder.Sql("""
                INSERT INTO FamilyMedicalEligibilityReviews
                    (Id, FamilyId, AdultPatientId, ChildPatientId, AdultFamilyMemberId, ChildFamilyMemberId, Status, CreatedAtUtc)
                SELECT NEWID(), a.FamilyId, a.PatientId, c.PatientId, a.Id, c.Id, 1, SYSUTCDATETIME()
                FROM FamilyMembers a JOIN FamilyMembers c ON c.FamilyId = a.FamilyId
                JOIN Families f ON f.Id = a.FamilyId AND f.Status = 1
                JOIN FamilyRelationshipRequests ar ON ar.Id = a.AddedThroughRequestId AND ar.Status = 3
                JOIN FamilyRelationshipRequests cr ON cr.Id = c.AddedThroughRequestId AND cr.Status = 3
                WHERE a.IsActive = 1 AND c.IsActive = 1 AND a.Role IN (1,2,4,5) AND c.Role = 3
                  AND NOT EXISTS (SELECT 1 FROM FamilyMedicalEligibilityReviews e WHERE e.FamilyId = a.FamilyId AND e.AdultPatientId = a.PatientId AND e.ChildPatientId = c.PatientId);
                INSERT INTO FamilyMedicalEligibilityReviewHistories
                    (Id, ParentId, Action, OldStatus, NewStatus, Revision, ActorApplicationUserId, OccurredAtUtc, ReasonOrMessage)
                SELECT NEWID(), e.Id, 'LegacyPendingCandidate', NULL, 1, 1, NULL, e.CreatedAtUtc, NULL
                FROM FamilyMedicalEligibilityReviews e WHERE e.Status = 1
                  AND NOT EXISTS (SELECT 1 FROM FamilyMedicalEligibilityReviewHistories h WHERE h.ParentId = e.Id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FamilyMedicalAccessRestrictions_FamilyMedicalAccessRequests_LiftedByRequestId",
                table: "FamilyMedicalAccessRestrictions");

            migrationBuilder.DropForeignKey(
                name: "FK_FamilyMedicalAccessRestrictions_FamilyMedicalAccessRequests_RestrictionApprovedByRequestId",
                table: "FamilyMedicalAccessRestrictions");

            migrationBuilder.DropTable(
                name: "FamilyMedicalAccessRequestDocuments");

            migrationBuilder.DropTable(
                name: "FamilyMedicalAccessRequestHistories");

            migrationBuilder.DropTable(
                name: "FamilyMedicalAccessRestrictionHistories");

            migrationBuilder.DropTable(
                name: "FamilyMedicalEligibilityReviewHistories");

            migrationBuilder.DropTable(
                name: "FamilyMedicalEligibilityReviews");

            migrationBuilder.DropTable(
                name: "FamilyMedicalAccessRequests");

            migrationBuilder.DropTable(
                name: "FamilyMedicalAccessRestrictions");
        }
    }
}
