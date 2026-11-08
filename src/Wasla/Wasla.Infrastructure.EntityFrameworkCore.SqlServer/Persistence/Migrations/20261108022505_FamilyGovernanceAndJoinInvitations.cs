using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // Generated migration column arrays follow EF scaffolding.

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FamilyGovernanceAndJoinInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GovernanceCaseId",
                table: "FamilyCandidateSelections",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FamilyGovernanceCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentPatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChildPatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationalAuthorityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyGovernanceCases", x => x.Id);
                    table.CheckConstraint("CK_FamilyGovernance_Kind", "[Kind] IN (1,2)");
                    table.CheckConstraint("CK_FamilyGovernance_Revision", "[Revision] > 0");
                    table.CheckConstraint("CK_FamilyGovernance_Status", "[Status] IN (1,2,3,4,5)");
                    table.CheckConstraint("CK_FamilyGovernance_Subject", "[ParentPatientId] <> [ChildPatientId]");
                    table.ForeignKey(
                        name: "FK_FamilyGovernanceCases_ApplicationUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyGovernanceCases_ApplicationUsers_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyGovernanceCases_ParentChildOperationalAuthorities_OperationalAuthorityId",
                        column: x => x.OperationalAuthorityId,
                        principalTable: "ParentChildOperationalAuthorities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyGovernanceCases_Patients_ChildPatientId",
                        column: x => x.ChildPatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyGovernanceCases_Patients_ParentPatientId",
                        column: x => x.ParentPatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FamilyGovernanceEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_FamilyGovernanceEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FamilyGovernanceEvidence_FamilyGovernanceCases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "FamilyGovernanceCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FamilyGovernanceHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyGovernanceHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FamilyGovernanceHistories_ApplicationUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyGovernanceHistories_FamilyGovernanceCases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "FamilyGovernanceCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FamilyGovernanceOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ResponseJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyGovernanceOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FamilyGovernanceOperations_ApplicationUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyGovernanceOperations_FamilyGovernanceCases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "FamilyGovernanceCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FamilyGovernanceRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    BeforeSnapshot = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    AfterSnapshot = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    AuthorityVersion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyGovernanceRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FamilyGovernanceRevisions_FamilyGovernanceCases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "FamilyGovernanceCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FamilyJoinInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IssuedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IssuerPatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChildPatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationalAuthorityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntendedRole = table.Column<int>(type: "int", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ProtectedReference = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsumedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RedeemedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RedemptionKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RedemptionFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RelationshipRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GovernanceCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevocationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyJoinInvitations", x => x.Id);
                    table.CheckConstraint("CK_FamilyJoinInvitation_Consumed", "([ConsumedAtUtc] IS NULL AND [RedeemedByUserId] IS NULL AND [RelationshipRequestId] IS NULL AND [GovernanceCaseId] IS NULL) OR ([ConsumedAtUtc] IS NOT NULL AND [RedeemedByUserId] IS NOT NULL AND (([RelationshipRequestId] IS NOT NULL AND [GovernanceCaseId] IS NULL) OR ([RelationshipRequestId] IS NULL AND [GovernanceCaseId] IS NOT NULL)))");
                    table.CheckConstraint("CK_FamilyJoinInvitation_Expiry", "[ExpiresAtUtc] > [CreatedAtUtc]");
                    table.CheckConstraint("CK_FamilyJoinInvitation_Role", "[IntendedRole] IN (1,2)");
                    table.ForeignKey(
                        name: "FK_FamilyJoinInvitations_ApplicationUsers_IssuedByUserId",
                        column: x => x.IssuedByUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyJoinInvitations_ApplicationUsers_RedeemedByUserId",
                        column: x => x.RedeemedByUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyJoinInvitations_Families_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "Families",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyJoinInvitations_FamilyGovernanceCases_GovernanceCaseId",
                        column: x => x.GovernanceCaseId,
                        principalTable: "FamilyGovernanceCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyJoinInvitations_FamilyRelationshipRequests_RelationshipRequestId",
                        column: x => x.RelationshipRequestId,
                        principalTable: "FamilyRelationshipRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyJoinInvitations_ParentChildOperationalAuthorities_OperationalAuthorityId",
                        column: x => x.OperationalAuthorityId,
                        principalTable: "ParentChildOperationalAuthorities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyJoinInvitations_Patients_ChildPatientId",
                        column: x => x.ChildPatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyJoinInvitations_Patients_IssuerPatientId",
                        column: x => x.IssuerPatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FamilyCandidateSelections_GovernanceCaseId",
                table: "FamilyCandidateSelections",
                column: "GovernanceCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyGovernanceCases_ActorUserId_CreatedAtUtc_Id",
                table: "FamilyGovernanceCases",
                columns: new[] { "ActorUserId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FamilyGovernanceCases_ChildPatientId",
                table: "FamilyGovernanceCases",
                column: "ChildPatientId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyGovernanceCases_OperationalAuthorityId",
                table: "FamilyGovernanceCases",
                column: "OperationalAuthorityId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyGovernanceCases_ParentPatientId_ChildPatientId_Kind",
                table: "FamilyGovernanceCases",
                columns: new[] { "ParentPatientId", "ChildPatientId", "Kind" },
                unique: true,
                filter: "[Status] IN (1,2,5)");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyGovernanceCases_ReviewedByUserId",
                table: "FamilyGovernanceCases",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyGovernanceCases_Status_Kind_CreatedAtUtc_Id",
                table: "FamilyGovernanceCases",
                columns: new[] { "Status", "Kind", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FamilyGovernanceEvidence_CaseId_Revision_Id",
                table: "FamilyGovernanceEvidence",
                columns: new[] { "CaseId", "Revision", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FamilyGovernanceHistories_ActorUserId",
                table: "FamilyGovernanceHistories",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyGovernanceHistories_CaseId_Revision_OccurredAtUtc_Id",
                table: "FamilyGovernanceHistories",
                columns: new[] { "CaseId", "Revision", "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FamilyGovernanceOperations_ActorUserId_Operation_IdempotencyKey",
                table: "FamilyGovernanceOperations",
                columns: new[] { "ActorUserId", "Operation", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FamilyGovernanceOperations_CaseId",
                table: "FamilyGovernanceOperations",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyGovernanceRevisions_CaseId_Revision",
                table: "FamilyGovernanceRevisions",
                columns: new[] { "CaseId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FamilyJoinInvitations_ChildPatientId",
                table: "FamilyJoinInvitations",
                column: "ChildPatientId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyJoinInvitations_FamilyId",
                table: "FamilyJoinInvitations",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyJoinInvitations_GovernanceCaseId",
                table: "FamilyJoinInvitations",
                column: "GovernanceCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyJoinInvitations_IssuedByUserId_CreatedAtUtc",
                table: "FamilyJoinInvitations",
                columns: new[] { "IssuedByUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FamilyJoinInvitations_IssuedByUserId_IdempotencyKey",
                table: "FamilyJoinInvitations",
                columns: new[] { "IssuedByUserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FamilyJoinInvitations_IssuedByUserId_RevocationKey",
                table: "FamilyJoinInvitations",
                columns: new[] { "IssuedByUserId", "RevocationKey" },
                unique: true,
                filter: "[RevocationKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyJoinInvitations_IssuerPatientId",
                table: "FamilyJoinInvitations",
                column: "IssuerPatientId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyJoinInvitations_OperationalAuthorityId",
                table: "FamilyJoinInvitations",
                column: "OperationalAuthorityId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyJoinInvitations_RedeemedByUserId_RedemptionKey",
                table: "FamilyJoinInvitations",
                columns: new[] { "RedeemedByUserId", "RedemptionKey" },
                unique: true,
                filter: "[RedemptionKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyJoinInvitations_RelationshipRequestId",
                table: "FamilyJoinInvitations",
                column: "RelationshipRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyJoinInvitations_TokenHash",
                table: "FamilyJoinInvitations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_FamilyCandidateSelections_FamilyGovernanceCases_GovernanceCaseId",
                table: "FamilyCandidateSelections",
                column: "GovernanceCaseId",
                principalTable: "FamilyGovernanceCases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
            => throw new NotSupportedException("Family governance and invitation history is retained. Disable features and roll forward instead of dropping records.");
    }
}
