using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // EF-generated migration column arrays; follows existing migration conventions.

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FamilySelfServiceOperationalFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChildOnboardingRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentPatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentRole = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    Gender = table.Column<int>(type: "int", nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ContactNameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContactPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IntentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ApprovedPatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FamilyRelationshipRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewIdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReviewFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ResubmitIdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ResubmitFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChildOnboardingRequests", x => x.Id);
                    table.CheckConstraint("CK_ChildOnboarding_Revision", "[Revision] >= 1");
                    table.CheckConstraint("CK_ChildOnboarding_Role", "[ParentRole] IN (1,2)");
                    table.CheckConstraint("CK_ChildOnboarding_Status", "[Status] IN (1,2,3,4)");
                    table.ForeignKey(
                        name: "FK_ChildOnboardingRequests_ApplicationUsers_ActorApplicationUserId",
                        column: x => x.ActorApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChildOnboardingRequests_ApplicationUsers_ReviewedByApplicationUserId",
                        column: x => x.ReviewedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChildOnboardingRequests_FamilyRelationshipRequests_FamilyRelationshipRequestId",
                        column: x => x.FamilyRelationshipRequestId,
                        principalTable: "FamilyRelationshipRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChildOnboardingRequests_Patients_ApprovedPatientId",
                        column: x => x.ApprovedPatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChildOnboardingRequests_Patients_ParentPatientId",
                        column: x => x.ParentPatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FamilyCandidateSelections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentPatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetPatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CriteriaHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsumedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SubmissionFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RelationshipRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyCandidateSelections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FamilyCandidateSelections_ApplicationUsers_ActorApplicationUserId",
                        column: x => x.ActorApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyCandidateSelections_FamilyRelationshipRequests_RelationshipRequestId",
                        column: x => x.RelationshipRequestId,
                        principalTable: "FamilyRelationshipRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyCandidateSelections_Patients_ParentPatientId",
                        column: x => x.ParentPatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyCandidateSelections_Patients_TargetPatientId",
                        column: x => x.TargetPatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParentChildOperationalAuthorities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentPatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChildPatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentFamilyMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChildFamilyMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentRole = table.Column<int>(type: "int", nullable: false),
                    ApprovedRelationshipRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedRevisionNumber = table.Column<int>(type: "int", nullable: false),
                    ReviewedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevocationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RevokedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RevocationIdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentChildOperationalAuthorities", x => x.Id);
                    table.CheckConstraint("CK_OperationalAuthority_Distinct", "[ParentPatientId] <> [ChildPatientId] AND [ParentFamilyMemberId] <> [ChildFamilyMemberId]");
                    table.CheckConstraint("CK_OperationalAuthority_Revision", "[ApprovedRevisionNumber] >= 1");
                    table.CheckConstraint("CK_OperationalAuthority_Role", "[ParentRole] IN (1,2,5)");
                    table.ForeignKey(
                        name: "FK_ParentChildOperationalAuthorities_ApplicationUsers_ReviewedByApplicationUserId",
                        column: x => x.ReviewedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParentChildOperationalAuthorities_ApplicationUsers_RevokedByApplicationUserId",
                        column: x => x.RevokedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParentChildOperationalAuthorities_Families_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "Families",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParentChildOperationalAuthorities_FamilyMembers_ChildFamilyMemberId",
                        column: x => x.ChildFamilyMemberId,
                        principalTable: "FamilyMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParentChildOperationalAuthorities_FamilyMembers_ParentFamilyMemberId",
                        column: x => x.ParentFamilyMemberId,
                        principalTable: "FamilyMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParentChildOperationalAuthorities_FamilyRelationshipRequests_ApprovedRelationshipRequestId",
                        column: x => x.ApprovedRelationshipRequestId,
                        principalTable: "FamilyRelationshipRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParentChildOperationalAuthorities_Patients_ChildPatientId",
                        column: x => x.ChildPatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParentChildOperationalAuthorities_Patients_ParentPatientId",
                        column: x => x.ParentPatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChildOnboardingEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    DocumentType = table.Column<int>(type: "int", nullable: false),
                    MediaKey = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChildOnboardingEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChildOnboardingEvidence_ChildOnboardingRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "ChildOnboardingRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChildOnboardingHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    ActorApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChildOnboardingHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChildOnboardingHistories_ApplicationUsers_ActorApplicationUserId",
                        column: x => x.ActorApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChildOnboardingHistories_ChildOnboardingRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "ChildOnboardingRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OperationalAuthorityHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalAuthorityHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationalAuthorityHistories_ApplicationUsers_ActorApplicationUserId",
                        column: x => x.ActorApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalAuthorityHistories_ParentChildOperationalAuthorities_AuthorityId",
                        column: x => x.AuthorityId,
                        principalTable: "ParentChildOperationalAuthorities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Patients_DateOfBirth_Gender",
                table: "Patients",
                columns: new[] { "DateOfBirth", "Gender" });

            migrationBuilder.CreateIndex(
                name: "IX_ChildOnboardingEvidence_RequestId_Revision_Id",
                table: "ChildOnboardingEvidence",
                columns: new[] { "RequestId", "Revision", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ChildOnboardingHistories_ActorApplicationUserId",
                table: "ChildOnboardingHistories",
                column: "ActorApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChildOnboardingHistories_RequestId_Revision_OccurredAtUtc_Id",
                table: "ChildOnboardingHistories",
                columns: new[] { "RequestId", "Revision", "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ChildOnboardingRequests_ActorApplicationUserId_IdempotencyKey",
                table: "ChildOnboardingRequests",
                columns: new[] { "ActorApplicationUserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChildOnboardingRequests_ApprovedPatientId",
                table: "ChildOnboardingRequests",
                column: "ApprovedPatientId");

            migrationBuilder.CreateIndex(
                name: "IX_ChildOnboardingRequests_FamilyRelationshipRequestId",
                table: "ChildOnboardingRequests",
                column: "FamilyRelationshipRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ChildOnboardingRequests_ParentPatientId_IntentHash",
                table: "ChildOnboardingRequests",
                columns: new[] { "ParentPatientId", "IntentHash" },
                unique: true,
                filter: "[Status] IN (1,2)");

            migrationBuilder.CreateIndex(
                name: "IX_ChildOnboardingRequests_ReviewedByApplicationUserId",
                table: "ChildOnboardingRequests",
                column: "ReviewedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChildOnboardingRequests_Status_CreatedAtUtc_Id",
                table: "ChildOnboardingRequests",
                columns: new[] { "Status", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FamilyCandidateSelections_ActorApplicationUserId_ExpiresAtUtc",
                table: "FamilyCandidateSelections",
                columns: new[] { "ActorApplicationUserId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FamilyCandidateSelections_ActorApplicationUserId_IdempotencyKey",
                table: "FamilyCandidateSelections",
                columns: new[] { "ActorApplicationUserId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyCandidateSelections_ParentPatientId",
                table: "FamilyCandidateSelections",
                column: "ParentPatientId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyCandidateSelections_RelationshipRequestId",
                table: "FamilyCandidateSelections",
                column: "RelationshipRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyCandidateSelections_TargetPatientId",
                table: "FamilyCandidateSelections",
                column: "TargetPatientId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyCandidateSelections_TokenHash",
                table: "FamilyCandidateSelections",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperationalAuthorityHistories_ActorApplicationUserId",
                table: "OperationalAuthorityHistories",
                column: "ActorApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalAuthorityHistories_AuthorityId_OccurredAtUtc_Id",
                table: "OperationalAuthorityHistories",
                columns: new[] { "AuthorityId", "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ParentChildOperationalAuthorities_ApprovedRelationshipRequestId",
                table: "ParentChildOperationalAuthorities",
                column: "ApprovedRelationshipRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParentChildOperationalAuthorities_ChildFamilyMemberId",
                table: "ParentChildOperationalAuthorities",
                column: "ChildFamilyMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentChildOperationalAuthorities_ChildPatientId",
                table: "ParentChildOperationalAuthorities",
                column: "ChildPatientId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentChildOperationalAuthorities_ParentFamilyMemberId",
                table: "ParentChildOperationalAuthorities",
                column: "ParentFamilyMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentChildOperationalAuthorities_ParentPatientId",
                table: "ParentChildOperationalAuthorities",
                column: "ParentPatientId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentChildOperationalAuthorities_ReviewedByApplicationUserId",
                table: "ParentChildOperationalAuthorities",
                column: "ReviewedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentChildOperationalAuthorities_RevokedByApplicationUserId",
                table: "ParentChildOperationalAuthorities",
                column: "RevokedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "UX_OperationalAuthority_ActivePair",
                table: "ParentChildOperationalAuthorities",
                columns: new[] { "FamilyId", "ParentPatientId", "ChildPatientId" },
                unique: true,
                filter: "[IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Forward-only migration: disable the feature and retain reviewed identity evidence, proofs and histories. Use the controlled rollback runbook.");
        }
    }
}
#pragma warning restore CA1861
