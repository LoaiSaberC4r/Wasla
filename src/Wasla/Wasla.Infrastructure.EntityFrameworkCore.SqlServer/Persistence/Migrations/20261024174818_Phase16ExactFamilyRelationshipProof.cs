using System;
using Microsoft.EntityFrameworkCore.Migrations;

#pragma warning disable CA1861 // EF-generated one-time migration column arrays.
#nullable disable

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase16ExactFamilyRelationshipProof : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AuthorityRelationshipRequestId",
                table: "FamilyMedicalEligibilityReviews",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VerifiedParentChildRelationships",
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
                    VerifiedBySuperAdminApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PolicyVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerifiedParentChildRelationships", x => x.Id);
                    table.CheckConstraint("CK_VerifiedParentChild_Distinct", "[ParentPatientId] <> [ChildPatientId]");
                    table.CheckConstraint("CK_VerifiedParentChild_Revision", "[ApprovedRevisionNumber] >= 1");
                    table.CheckConstraint("CK_VerifiedParentChild_Role", "[ParentRole] IN (1,2)");
                    table.CheckConstraint("CK_VerifiedParentChild_Status", "[Status] IN (1,2)");
                    table.ForeignKey(
                        name: "FK_VerifiedParentChildRelationships_ApplicationUsers_VerifiedBySuperAdminApplicationUserId",
                        column: x => x.VerifiedBySuperAdminApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VerifiedParentChildRelationships_Families_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "Families",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VerifiedParentChildRelationships_FamilyMembers_ChildFamilyMemberId",
                        column: x => x.ChildFamilyMemberId,
                        principalTable: "FamilyMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VerifiedParentChildRelationships_FamilyMembers_ParentFamilyMemberId",
                        column: x => x.ParentFamilyMemberId,
                        principalTable: "FamilyMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VerifiedParentChildRelationships_FamilyRelationshipRequests_ApprovedRelationshipRequestId",
                        column: x => x.ApprovedRelationshipRequestId,
                        principalTable: "FamilyRelationshipRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VerifiedParentChildRelationships_Patients_ChildPatientId",
                        column: x => x.ChildPatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VerifiedParentChildRelationships_Patients_ParentPatientId",
                        column: x => x.ParentPatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMedicalEligibilityReviews_AuthorityRelationshipRequestId",
                table: "FamilyMedicalEligibilityReviews",
                column: "AuthorityRelationshipRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_VerifiedParentChildRelationships_ApprovedRelationshipRequestId",
                table: "VerifiedParentChildRelationships",
                column: "ApprovedRelationshipRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_VerifiedParentChildRelationships_ChildFamilyMemberId",
                table: "VerifiedParentChildRelationships",
                column: "ChildFamilyMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_VerifiedParentChildRelationships_ChildPatientId_FamilyId_Status",
                table: "VerifiedParentChildRelationships",
                columns: new[] { "ChildPatientId", "FamilyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_VerifiedParentChildRelationships_FamilyId_ParentPatientId_ChildPatientId",
                table: "VerifiedParentChildRelationships",
                columns: new[] { "FamilyId", "ParentPatientId", "ChildPatientId" },
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_VerifiedParentChildRelationships_ParentFamilyMemberId",
                table: "VerifiedParentChildRelationships",
                column: "ParentFamilyMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_VerifiedParentChildRelationships_ParentPatientId_FamilyId_Status",
                table: "VerifiedParentChildRelationships",
                columns: new[] { "ParentPatientId", "FamilyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_VerifiedParentChildRelationships_VerifiedBySuperAdminApplicationUserId",
                table: "VerifiedParentChildRelationships",
                column: "VerifiedBySuperAdminApplicationUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_FamilyMedicalEligibilityReviews_FamilyRelationshipRequests_AuthorityRelationshipRequestId",
                table: "FamilyMedicalEligibilityReviews",
                column: "AuthorityRelationshipRequestId",
                principalTable: "FamilyRelationshipRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Medical proof provenance must be preserved. Disable sharing and retain the additive schema for rollback.");
        }
    }
}
