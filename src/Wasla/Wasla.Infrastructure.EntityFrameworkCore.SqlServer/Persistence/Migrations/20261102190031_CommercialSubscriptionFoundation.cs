using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CommercialSubscriptionFoundation : Migration
    {
        private static readonly string[] IndexColumns1 = new[] { "ProductId", "Code" };
        private static readonly string[] IndexColumns2 = new[] { "DoctorId", "Status" };
        private static readonly string[] IndexColumns3 = new[] { "Status", "EndsAtUtc" };
        private static readonly string[] IndexColumns4 = new[] { "PlanId", "FeatureId" };
        private static readonly string[] IndexColumns5 = new[] { "SubscriptionId", "Status" };
        private static readonly string[] IndexColumns6 = new[] { "SubscriptionId", "FeatureId" };
        private static readonly string[] IndexColumns7 = new[] { "SubscriptionId", "OccurredOnUtc" };
        private static readonly string[] IndexColumns8 = new[] { "SubscriptionId", "PaidAtUtc" };
        private static readonly string[] IndexColumns9 = new[] { "Status", "DueAtUtc" };
        private static readonly string[] IndexColumns10 = new[] { "SubscriptionId", "ReminderCode", "TrialRevision" };
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccountStateRevision",
                table: "Receptions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CommercialFeatures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DescriptionAr = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DescriptionEn = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    GroupCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ImplementationStatus = table.Column<int>(type: "int", nullable: false),
                    ReleaseReadiness = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsPubliclyVisible = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    ModifiedOnUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommercialFeatures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommercialProducts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DescriptionAr = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DescriptionEn = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsPubliclySelectable = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    ModifiedOnUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommercialProducts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LegacyTransitionEnrollmentBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    DurationDays = table.Column<int>(type: "int", nullable: false),
                    InitiatedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegacyTransitionEnrollmentBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LegacyTransitionEnrollmentBatches_ApplicationUsers_InitiatedByApplicationUserId",
                        column: x => x.InitiatedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FeaturePermissionMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeatureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequiredAccessLevel = table.Column<int>(type: "int", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeaturePermissionMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeaturePermissionMappings_CommercialFeatures_FeatureId",
                        column: x => x.FeatureId,
                        principalTable: "CommercialFeatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FeaturePermissionMappings_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommercialPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DescriptionAr = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DescriptionEn = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsPubliclySelectable = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    ModifiedOnUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommercialPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommercialPlans_CommercialProducts_ProductId",
                        column: x => x.ProductId,
                        principalTable: "CommercialProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DoctorSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionKind = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    BillingCycle = table.Column<int>(type: "int", nullable: true),
                    SelectedPracticeCount = table.Column<int>(type: "int", nullable: true),
                    PriceAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PracticeLimit = table.Column<int>(type: "int", nullable: false),
                    ReceptionLimit = table.Column<int>(type: "int", nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    EndsAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    TrialStartedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    TrialEndsAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    TrialExtensionCount = table.Column<int>(type: "int", nullable: false),
                    CatalogVersion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TermsSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WillNotRenew = table.Column<bool>(type: "bit", nullable: false),
                    CancellationRequestedOnUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ActivatedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    ModifiedOnUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorSubscriptions", x => x.Id);
                    table.CheckConstraint("CK_DoctorSubscriptions_Terms", "[PracticeLimit] > 0 AND [ReceptionLimit] >= 0 AND [PriceAmount] >= 0");
                    table.ForeignKey(
                        name: "FK_DoctorSubscriptions_ApplicationUsers_ActivatedByApplicationUserId",
                        column: x => x.ActivatedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorSubscriptions_CommercialPlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "CommercialPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorSubscriptions_CommercialProducts_ProductId",
                        column: x => x.ProductId,
                        principalTable: "CommercialProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorSubscriptions_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlanFeatureEntitlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeatureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccessLevel = table.Column<int>(type: "int", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanFeatureEntitlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanFeatureEntitlements_CommercialFeatures_FeatureId",
                        column: x => x.FeatureId,
                        principalTable: "CommercialFeatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlanFeatureEntitlements_CommercialPlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "CommercialPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionChangeRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangeType = table.Column<int>(type: "int", nullable: false),
                    TargetPlanCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TargetPracticeCount = table.Column<int>(type: "int", nullable: true),
                    TargetBillingCycle = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedOnUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    ReviewedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedOnUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TargetPriceSnapshot = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DifferenceAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EffectiveAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    TargetTermsSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionChangeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionChangeRequests_ApplicationUsers_RequestedByApplicationUserId",
                        column: x => x.RequestedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubscriptionChangeRequests_ApplicationUsers_ReviewedByApplicationUserId",
                        column: x => x.ReviewedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubscriptionChangeRequests_DoctorSubscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "DoctorSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionFeatureSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeatureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeatureCodeSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AccessLevel = table.Column<int>(type: "int", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionFeatureSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionFeatureSnapshots_CommercialFeatures_FeatureId",
                        column: x => x.FeatureId,
                        principalTable: "CommercialFeatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubscriptionFeatureSnapshots_DoctorSubscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "DoctorSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PreviousStatus = table.Column<int>(type: "int", nullable: true),
                    NewStatus = table.Column<int>(type: "int", nullable: true),
                    ActorApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OccurredOnUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PreviousTermsSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewTermsSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EventIdempotencyKey = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionHistories_ApplicationUsers_ActorApplicationUserId",
                        column: x => x.ActorApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubscriptionHistories_DoctorSubscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "DoctorSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentType = table.Column<int>(type: "int", nullable: false),
                    PaymentMethod = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    RecordedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionPayments_ApplicationUsers_RecordedByApplicationUserId",
                        column: x => x.RecordedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubscriptionPayments_DoctorSubscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "DoctorSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionReminderSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReminderCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DueAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    TrialRevision = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    ProcessedOnUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionReminderSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionReminderSchedules_DoctorSubscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "DoctorSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommercialFeatures_Code",
                table: "CommercialFeatures",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommercialPlans_ProductId_Code",
                table: "CommercialPlans",
                columns: IndexColumns1,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommercialProducts_Code",
                table: "CommercialProducts",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DoctorSubscriptions_ActivatedByApplicationUserId",
                table: "DoctorSubscriptions",
                column: "ActivatedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorSubscriptions_DoctorId_Status",
                table: "DoctorSubscriptions",
                columns: IndexColumns2);

            migrationBuilder.CreateIndex(
                name: "IX_DoctorSubscriptions_PlanId",
                table: "DoctorSubscriptions",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorSubscriptions_ProductId",
                table: "DoctorSubscriptions",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorSubscriptions_Status_EndsAtUtc",
                table: "DoctorSubscriptions",
                columns: IndexColumns3);

            migrationBuilder.CreateIndex(
                name: "UX_OneActiveSubscriptionPerDoctor",
                table: "DoctorSubscriptions",
                column: "DoctorId",
                unique: true,
                filter: "[Status] = 2");

            migrationBuilder.CreateIndex(
                name: "UX_OnePendingSubscriptionPerDoctor",
                table: "DoctorSubscriptions",
                column: "DoctorId",
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "UX_OneScheduledSubscriptionPerDoctor",
                table: "DoctorSubscriptions",
                column: "DoctorId",
                unique: true,
                filter: "[Status] = 6");

            migrationBuilder.CreateIndex(
                name: "UX_OneTrialPerDoctor",
                table: "DoctorSubscriptions",
                column: "DoctorId",
                unique: true,
                filter: "[SubscriptionKind] = 2");

            migrationBuilder.CreateIndex(
                name: "IX_FeaturePermissionMappings_FeatureId",
                table: "FeaturePermissionMappings",
                column: "FeatureId");

            migrationBuilder.CreateIndex(
                name: "IX_FeaturePermissionMappings_PermissionId",
                table: "FeaturePermissionMappings",
                column: "PermissionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LegacyTransitionEnrollmentBatches_InitiatedByApplicationUserId",
                table: "LegacyTransitionEnrollmentBatches",
                column: "InitiatedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanFeatureEntitlements_FeatureId",
                table: "PlanFeatureEntitlements",
                column: "FeatureId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanFeatureEntitlements_PlanId_FeatureId",
                table: "PlanFeatureEntitlements",
                columns: IndexColumns4,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionChangeRequests_RequestedByApplicationUserId",
                table: "SubscriptionChangeRequests",
                column: "RequestedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionChangeRequests_ReviewedByApplicationUserId",
                table: "SubscriptionChangeRequests",
                column: "ReviewedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionChangeRequests_SubscriptionId_Status",
                table: "SubscriptionChangeRequests",
                columns: IndexColumns5);

            migrationBuilder.CreateIndex(
                name: "UX_OnePendingChangePerSubscription",
                table: "SubscriptionChangeRequests",
                column: "SubscriptionId",
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionFeatureSnapshots_FeatureId",
                table: "SubscriptionFeatureSnapshots",
                column: "FeatureId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionFeatureSnapshots_SubscriptionId_FeatureId",
                table: "SubscriptionFeatureSnapshots",
                columns: IndexColumns6,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionHistories_ActorApplicationUserId",
                table: "SubscriptionHistories",
                column: "ActorApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionHistories_EventIdempotencyKey",
                table: "SubscriptionHistories",
                column: "EventIdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionHistories_SubscriptionId_OccurredOnUtc",
                table: "SubscriptionHistories",
                columns: IndexColumns7);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPayments_IdempotencyKey",
                table: "SubscriptionPayments",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPayments_RecordedByApplicationUserId",
                table: "SubscriptionPayments",
                column: "RecordedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPayments_SubscriptionId_PaidAtUtc",
                table: "SubscriptionPayments",
                columns: IndexColumns8);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionReminderSchedules_Status_DueAtUtc",
                table: "SubscriptionReminderSchedules",
                columns: IndexColumns9);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionReminderSchedules_SubscriptionId_ReminderCode_TrialRevision",
                table: "SubscriptionReminderSchedules",
                columns: IndexColumns10,
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeaturePermissionMappings");

            migrationBuilder.DropTable(
                name: "LegacyTransitionEnrollmentBatches");

            migrationBuilder.DropTable(
                name: "PlanFeatureEntitlements");

            migrationBuilder.DropTable(
                name: "SubscriptionChangeRequests");

            migrationBuilder.DropTable(
                name: "SubscriptionFeatureSnapshots");

            migrationBuilder.DropTable(
                name: "SubscriptionHistories");

            migrationBuilder.DropTable(
                name: "SubscriptionPayments");

            migrationBuilder.DropTable(
                name: "SubscriptionReminderSchedules");

            migrationBuilder.DropTable(
                name: "CommercialFeatures");

            migrationBuilder.DropTable(
                name: "DoctorSubscriptions");

            migrationBuilder.DropTable(
                name: "CommercialPlans");

            migrationBuilder.DropTable(
                name: "CommercialProducts");

            migrationBuilder.DropColumn(
                name: "AccountStateRevision",
                table: "Receptions");
        }
    }
}
