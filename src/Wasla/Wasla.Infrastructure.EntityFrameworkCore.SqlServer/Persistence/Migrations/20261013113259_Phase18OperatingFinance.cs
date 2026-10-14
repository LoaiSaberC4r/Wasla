using System;
// EF schema array arguments are intentionally local to generated migration operations.
#pragma warning disable CA1861
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase18OperatingFinance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_Receptions_OwnerDoctorId_Id",
                table: "Receptions",
                columns: new[] { "OwnerDoctorId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_DoctorPractices_DoctorId_Id",
                table: "DoctorPractices",
                columns: new[] { "DoctorId", "Id" });

            migrationBuilder.CreateTable(
                name: "OperatingFinanceHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ActorApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatingFinanceHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperatingFinanceHistories_ApplicationUsers_ActorApplicationUserId",
                        column: x => x.ActorApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperatingFinanceHistories_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OperatingFinancialPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    FirstCoveredMonth = table.Column<DateOnly>(type: "date", nullable: false),
                    CurrentRevisionNumber = table.Column<int>(type: "int", nullable: false),
                    CreatedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatingFinancialPlans", x => x.Id);
                    table.UniqueConstraint("AK_OperatingFinancialPlans_DoctorId_Id", x => new { x.DoctorId, x.Id });
                    table.CheckConstraint("CK_OperatingPlan_Dates", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                    table.CheckConstraint("CK_OperatingPlan_Kind", "[Kind] IN (1,2)");
                    table.CheckConstraint("CK_OperatingPlan_Reception", "([Kind] = 1 AND [ReceptionId] IS NOT NULL) OR ([Kind] = 2 AND [ReceptionId] IS NULL)");
                    table.CheckConstraint("CK_OperatingPlan_Status", "[Status] IN (1,2,3)");
                    table.ForeignKey(
                        name: "FK_OperatingFinancialPlans_ApplicationUsers_CreatedByApplicationUserId",
                        column: x => x.CreatedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperatingFinancialPlans_ApplicationUsers_ModifiedByApplicationUserId",
                        column: x => x.ModifiedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperatingFinancialPlans_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperatingFinancialPlans_Receptions_DoctorId_ReceptionId",
                        columns: x => new { x.DoctorId, x.ReceptionId },
                        principalTable: "Receptions",
                        principalColumns: new[] { "OwnerDoctorId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OperatingGenerationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    GeneratedCount = table.Column<int>(type: "int", nullable: false),
                    FailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatingGenerationRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperatingPlanRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    DueDay = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Cadence = table.Column<int>(type: "int", nullable: false),
                    AmountPolicy = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    DescriptionAr = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DescriptionEn = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IsLifecycleChange = table.Column<bool>(type: "bit", nullable: false),
                    ActorApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatingPlanRevisions", x => x.Id);
                    table.UniqueConstraint("AK_OperatingPlanRevisions_DoctorId_Id", x => new { x.DoctorId, x.Id });
                    table.CheckConstraint("CK_OperatingPlanRevision_Amount", "CAST([Amount] AS decimal(18,2)) > 0");
                    table.CheckConstraint("CK_OperatingPlanRevision_Cadence", "[Cadence] IN (1,3,12)");
                    table.CheckConstraint("CK_OperatingPlanRevision_Currency", "[CurrencyCode] = 'EGP'");
                    table.CheckConstraint("CK_OperatingPlanRevision_Day", "[DueDay] BETWEEN 1 AND 31");
                    table.CheckConstraint("CK_OperatingPlanRevision_Policy", "[AmountPolicy] IN (1,2)");
                    table.ForeignKey(
                        name: "FK_OperatingPlanRevisions_ApplicationUsers_ActorApplicationUserId",
                        column: x => x.ActorApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperatingPlanRevisions_OperatingFinancialPlans_DoctorId_PlanId",
                        columns: x => new { x.DoctorId, x.PlanId },
                        principalTable: "OperatingFinancialPlans",
                        principalColumns: new[] { "DoctorId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OperatingPlanAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PracticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Percentage = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatingPlanAllocations", x => x.Id);
                    table.CheckConstraint("CK_OperatingPlanAllocation_Percent", "CAST([Percentage] AS decimal(9,4)) > 0 AND CAST([Percentage] AS decimal(9,4)) <= 100");
                    table.ForeignKey(
                        name: "FK_OperatingPlanAllocations_DoctorPractices_DoctorId_PracticeId",
                        columns: x => new { x.DoctorId, x.PracticeId },
                        principalTable: "DoctorPractices",
                        principalColumns: new[] { "DoctorId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperatingPlanAllocations_OperatingPlanRevisions_DoctorId_RevisionId",
                        columns: x => new { x.DoctorId, x.RevisionId },
                        principalTable: "OperatingPlanRevisions",
                        principalColumns: new[] { "DoctorId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OperatingCostAllocationSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PracticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Percentage = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    AmountMinorUnits = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatingCostAllocationSnapshots", x => x.Id);
                    table.CheckConstraint("CK_OperatingCostAllocation_Amount", "[AmountMinorUnits] >= 0");
                    table.CheckConstraint("CK_OperatingCostAllocation_Percent", "CAST([Percentage] AS decimal(9,4)) > 0 AND CAST([Percentage] AS decimal(9,4)) <= 100");
                    table.ForeignKey(
                        name: "FK_OperatingCostAllocationSnapshots_DoctorPractices_DoctorId_PracticeId",
                        columns: x => new { x.DoctorId, x.PracticeId },
                        principalTable: "DoctorPractices",
                        principalColumns: new[] { "DoctorId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OperatingObligationRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObligationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    AmountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    DueBusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    DescriptionAr = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DescriptionEn = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ActualAmountConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ActorApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatingObligationRevisions", x => x.Id);
                    table.UniqueConstraint("AK_OperatingObligationRevisions_DoctorId_Id", x => new { x.DoctorId, x.Id });
                    table.UniqueConstraint("AK_OperatingObligationRevisions_ObligationId_Id", x => new { x.ObligationId, x.Id });
                    table.CheckConstraint("CK_OperatingObligationRevision_Amount", "[AmountMinorUnits] > 0");
                    table.ForeignKey(
                        name: "FK_OperatingObligationRevisions_ApplicationUsers_ActorApplicationUserId",
                        column: x => x.ActorApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OperatingObligations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReceptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    OccurrenceKey = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    PeriodYear = table.Column<int>(type: "int", nullable: true),
                    PeriodMonth = table.Column<int>(type: "int", nullable: true),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueBusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PlannedAmountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    AmountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    AmountPolicy = table.Column<int>(type: "int", nullable: false),
                    ActualAmountConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    DescriptionAr = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DescriptionEn = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ActiveSettlementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrentRevisionNumber = table.Column<int>(type: "int", nullable: false),
                    CreatedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatingObligations", x => x.Id);
                    table.UniqueConstraint("AK_OperatingObligations_DoctorId_Id", x => new { x.DoctorId, x.Id });
                    table.CheckConstraint("CK_OperatingObligation_Amount", "[AmountMinorUnits] > 0 AND [PlannedAmountMinorUnits] > 0");
                    table.CheckConstraint("CK_OperatingObligation_Currency", "[CurrencyCode] = 'EGP'");
                    table.CheckConstraint("CK_OperatingObligation_Occurrence", "([Kind] = 2 AND [PlanId] IS NULL AND [OccurrenceKey] IS NULL) OR ([Kind] IN (1,3) AND [PlanId] IS NOT NULL AND [OccurrenceKey] IS NOT NULL AND [PeriodMonth] BETWEEN 1 AND 12 AND [PeriodYear] BETWEEN 1 AND 9999)");
                    table.CheckConstraint("CK_OperatingObligation_State", "([Status] = 2 AND [ActiveSettlementId] IS NOT NULL) OR ([Status] IN (1,3) AND [ActiveSettlementId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_OperatingObligations_ApplicationUsers_CreatedByApplicationUserId",
                        column: x => x.CreatedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperatingObligations_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperatingObligations_OperatingFinancialPlans_DoctorId_PlanId",
                        columns: x => new { x.DoctorId, x.PlanId },
                        principalTable: "OperatingFinancialPlans",
                        principalColumns: new[] { "DoctorId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperatingObligations_Receptions_DoctorId_ReceptionId",
                        columns: x => new { x.DoctorId, x.ReceptionId },
                        principalTable: "Receptions",
                        principalColumns: new[] { "OwnerDoctorId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OperatingSettlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObligationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObligationRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceiptNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AmountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    PaymentMethod = table.Column<int>(type: "int", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReportTimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BackdateReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ActorApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatingSettlements", x => x.Id);
                    table.UniqueConstraint("AK_OperatingSettlements_ObligationId_Id", x => new { x.ObligationId, x.Id });
                    table.CheckConstraint("CK_OperatingSettlement_Amount", "[AmountMinorUnits] > 0");
                    table.CheckConstraint("CK_OperatingSettlement_Currency", "[CurrencyCode] = 'EGP'");
                    table.CheckConstraint("CK_OperatingSettlement_Method", "[PaymentMethod] IN (1,2,3,4)");
                    table.ForeignKey(
                        name: "FK_OperatingSettlements_ApplicationUsers_ActorApplicationUserId",
                        column: x => x.ActorApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperatingSettlements_OperatingObligationRevisions_ObligationId_ObligationRevisionId",
                        columns: x => new { x.ObligationId, x.ObligationRevisionId },
                        principalTable: "OperatingObligationRevisions",
                        principalColumns: new[] { "ObligationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperatingSettlements_OperatingObligations_DoctorId_ObligationId",
                        columns: x => new { x.DoctorId, x.ObligationId },
                        principalTable: "OperatingObligations",
                        principalColumns: new[] { "DoctorId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OperatingSettlementReversals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalSettlementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObligationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AmountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReportTimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ActorApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatingSettlementReversals", x => x.Id);
                    table.CheckConstraint("CK_OperatingReversal_Amount", "[AmountMinorUnits] > 0");
                    table.CheckConstraint("CK_OperatingReversal_Reason", "NULLIF(LTRIM(RTRIM([Reason])), '') IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_OperatingSettlementReversals_ApplicationUsers_ActorApplicationUserId",
                        column: x => x.ActorApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperatingSettlementReversals_OperatingObligations_DoctorId_ObligationId",
                        columns: x => new { x.DoctorId, x.ObligationId },
                        principalTable: "OperatingObligations",
                        principalColumns: new[] { "DoctorId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperatingSettlementReversals_OperatingSettlements_ObligationId_OriginalSettlementId",
                        columns: x => new { x.ObligationId, x.OriginalSettlementId },
                        principalTable: "OperatingSettlements",
                        principalColumns: new[] { "ObligationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperatingCostAllocationSnapshots_DoctorId_PracticeId",
                table: "OperatingCostAllocationSnapshots",
                columns: new[] { "DoctorId", "PracticeId" });

            migrationBuilder.CreateIndex(
                name: "IX_OperatingCostAllocationSnapshots_DoctorId_RevisionId",
                table: "OperatingCostAllocationSnapshots",
                columns: new[] { "DoctorId", "RevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_OperatingCostAllocationSnapshots_PracticeId_RevisionId",
                table: "OperatingCostAllocationSnapshots",
                columns: new[] { "PracticeId", "RevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_OperatingCostAllocationSnapshots_RevisionId_PracticeId",
                table: "OperatingCostAllocationSnapshots",
                columns: new[] { "RevisionId", "PracticeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperatingFinanceHistories_ActorApplicationUserId",
                table: "OperatingFinanceHistories",
                column: "ActorApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OperatingFinanceHistories_DoctorId_ResourceType_ResourceId_RecordedAtUtc",
                table: "OperatingFinanceHistories",
                columns: new[] { "DoctorId", "ResourceType", "ResourceId", "RecordedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OperatingFinancialPlans_CreatedByApplicationUserId",
                table: "OperatingFinancialPlans",
                column: "CreatedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OperatingFinancialPlans_DoctorId_Kind_Status_EffectiveFrom",
                table: "OperatingFinancialPlans",
                columns: new[] { "DoctorId", "Kind", "Status", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_OperatingFinancialPlans_DoctorId_ReceptionId_EffectiveFrom_EffectiveTo",
                table: "OperatingFinancialPlans",
                columns: new[] { "DoctorId", "ReceptionId", "EffectiveFrom", "EffectiveTo" });

            migrationBuilder.CreateIndex(
                name: "IX_OperatingFinancialPlans_ModifiedByApplicationUserId",
                table: "OperatingFinancialPlans",
                column: "ModifiedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OperatingGenerationRuns_StartedAtUtc",
                table: "OperatingGenerationRuns",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OperatingObligationRevisions_ActorApplicationUserId",
                table: "OperatingObligationRevisions",
                column: "ActorApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OperatingObligationRevisions_DoctorId_ObligationId",
                table: "OperatingObligationRevisions",
                columns: new[] { "DoctorId", "ObligationId" });

            migrationBuilder.CreateIndex(
                name: "IX_OperatingObligationRevisions_DoctorId_RecordedAtUtc_DueBusinessDate",
                table: "OperatingObligationRevisions",
                columns: new[] { "DoctorId", "RecordedAtUtc", "DueBusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_OperatingObligationRevisions_ObligationId_RevisionNumber",
                table: "OperatingObligationRevisions",
                columns: new[] { "ObligationId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperatingObligations_ActiveSettlementId",
                table: "OperatingObligations",
                column: "ActiveSettlementId",
                unique: true,
                filter: "[ActiveSettlementId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OperatingObligations_CreatedByApplicationUserId",
                table: "OperatingObligations",
                column: "CreatedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OperatingObligations_DoctorId_PlanId",
                table: "OperatingObligations",
                columns: new[] { "DoctorId", "PlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_OperatingObligations_DoctorId_ReceptionId",
                table: "OperatingObligations",
                columns: new[] { "DoctorId", "ReceptionId" });

            migrationBuilder.CreateIndex(
                name: "IX_OperatingObligations_DoctorId_Status_DueBusinessDate",
                table: "OperatingObligations",
                columns: new[] { "DoctorId", "Status", "DueBusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_OperatingObligations_Id_ActiveSettlementId",
                table: "OperatingObligations",
                columns: new[] { "Id", "ActiveSettlementId" });

            migrationBuilder.CreateIndex(
                name: "IX_OperatingObligations_PlanId_OccurrenceKey",
                table: "OperatingObligations",
                columns: new[] { "PlanId", "OccurrenceKey" },
                unique: true,
                filter: "[PlanId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OperatingObligations_PlanId_PeriodYear_PeriodMonth",
                table: "OperatingObligations",
                columns: new[] { "PlanId", "PeriodYear", "PeriodMonth" },
                unique: true,
                filter: "[PlanId] IS NOT NULL AND [Kind] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_OperatingPlanAllocations_DoctorId_PracticeId",
                table: "OperatingPlanAllocations",
                columns: new[] { "DoctorId", "PracticeId" });

            migrationBuilder.CreateIndex(
                name: "IX_OperatingPlanAllocations_DoctorId_RevisionId",
                table: "OperatingPlanAllocations",
                columns: new[] { "DoctorId", "RevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_OperatingPlanAllocations_RevisionId_PracticeId",
                table: "OperatingPlanAllocations",
                columns: new[] { "RevisionId", "PracticeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperatingPlanRevisions_ActorApplicationUserId",
                table: "OperatingPlanRevisions",
                column: "ActorApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OperatingPlanRevisions_DoctorId_PlanId",
                table: "OperatingPlanRevisions",
                columns: new[] { "DoctorId", "PlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_OperatingPlanRevisions_PlanId_EffectiveFrom_RecordedAtUtc",
                table: "OperatingPlanRevisions",
                columns: new[] { "PlanId", "EffectiveFrom", "RecordedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OperatingPlanRevisions_PlanId_RevisionNumber",
                table: "OperatingPlanRevisions",
                columns: new[] { "PlanId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperatingSettlementReversals_ActorApplicationUserId",
                table: "OperatingSettlementReversals",
                column: "ActorApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OperatingSettlementReversals_DoctorId_BusinessDate_RecordedAtUtc",
                table: "OperatingSettlementReversals",
                columns: new[] { "DoctorId", "BusinessDate", "RecordedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OperatingSettlementReversals_DoctorId_ObligationId",
                table: "OperatingSettlementReversals",
                columns: new[] { "DoctorId", "ObligationId" });

            migrationBuilder.CreateIndex(
                name: "IX_OperatingSettlementReversals_ObligationId_OriginalSettlementId",
                table: "OperatingSettlementReversals",
                columns: new[] { "ObligationId", "OriginalSettlementId" });

            migrationBuilder.CreateIndex(
                name: "IX_OperatingSettlementReversals_OriginalSettlementId",
                table: "OperatingSettlementReversals",
                column: "OriginalSettlementId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperatingSettlements_ActorApplicationUserId",
                table: "OperatingSettlements",
                column: "ActorApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OperatingSettlements_DoctorId_BusinessDate_RecordedAtUtc",
                table: "OperatingSettlements",
                columns: new[] { "DoctorId", "BusinessDate", "RecordedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OperatingSettlements_DoctorId_ObligationId",
                table: "OperatingSettlements",
                columns: new[] { "DoctorId", "ObligationId" });

            migrationBuilder.CreateIndex(
                name: "IX_OperatingSettlements_ObligationId_ObligationRevisionId",
                table: "OperatingSettlements",
                columns: new[] { "ObligationId", "ObligationRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_OperatingSettlements_ReceiptNumber",
                table: "OperatingSettlements",
                column: "ReceiptNumber",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_OperatingCostAllocationSnapshots_OperatingObligationRevisions_DoctorId_RevisionId",
                table: "OperatingCostAllocationSnapshots",
                columns: new[] { "DoctorId", "RevisionId" },
                principalTable: "OperatingObligationRevisions",
                principalColumns: new[] { "DoctorId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OperatingObligationRevisions_OperatingObligations_DoctorId_ObligationId",
                table: "OperatingObligationRevisions",
                columns: new[] { "DoctorId", "ObligationId" },
                principalTable: "OperatingObligations",
                principalColumns: new[] { "DoctorId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OperatingObligations_OperatingSettlements_Id_ActiveSettlementId",
                table: "OperatingObligations",
                columns: new[] { "Id", "ActiveSettlementId" },
                principalTable: "OperatingSettlements",
                principalColumns: new[] { "ObligationId", "Id" },
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("CREATE TRIGGER [TR_OperatingPlanRevisions_Immutable] ON [OperatingPlanRevisions] INSTEAD OF UPDATE, DELETE AS BEGIN THROW 51018, 'Operating financial history is immutable.', 1; END;");
            migrationBuilder.Sql("CREATE TRIGGER [TR_OperatingPlanAllocations_Immutable] ON [OperatingPlanAllocations] INSTEAD OF UPDATE, DELETE AS BEGIN THROW 51018, 'Operating financial history is immutable.', 1; END;");
            migrationBuilder.Sql("CREATE TRIGGER [TR_OperatingObligationRevisions_Immutable] ON [OperatingObligationRevisions] INSTEAD OF UPDATE, DELETE AS BEGIN THROW 51018, 'Operating financial history is immutable.', 1; END;");
            migrationBuilder.Sql("CREATE TRIGGER [TR_OperatingCostAllocationSnapshots_Immutable] ON [OperatingCostAllocationSnapshots] INSTEAD OF UPDATE, DELETE AS BEGIN THROW 51018, 'Operating financial history is immutable.', 1; END;");
            migrationBuilder.Sql("CREATE TRIGGER [TR_OperatingSettlements_Immutable] ON [OperatingSettlements] INSTEAD OF UPDATE, DELETE AS BEGIN THROW 51018, 'Operating financial history is immutable.', 1; END;");
            migrationBuilder.Sql("CREATE TRIGGER [TR_OperatingSettlementReversals_Immutable] ON [OperatingSettlementReversals] INSTEAD OF UPDATE, DELETE AS BEGIN THROW 51018, 'Operating financial history is immutable.', 1; END;");
            migrationBuilder.Sql("CREATE TRIGGER [TR_OperatingFinanceHistories_Immutable] ON [OperatingFinanceHistories] INSTEAD OF UPDATE, DELETE AS BEGIN THROW 51018, 'Operating financial history is immutable.', 1; END;");
            migrationBuilder.Sql("""
                CREATE TRIGGER [TR_OperatingSettlements_FullAmount] ON [OperatingSettlements] AFTER INSERT AS
                BEGIN
                    IF EXISTS (SELECT 1 FROM inserted i JOIN OperatingObligationRevisions r ON r.Id = i.ObligationRevisionId
                        WHERE i.AmountMinorUnits <> r.AmountMinorUnits OR r.ActualAmountConfirmed = 0)
                        THROW 51018, 'Full settlement of a confirmed obligation is required.', 1;
                END;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER [TR_OperatingReversals_FullAmount] ON [OperatingSettlementReversals] AFTER INSERT AS
                BEGIN
                    IF EXISTS (SELECT 1 FROM inserted i JOIN OperatingSettlements s ON s.Id = i.OriginalSettlementId
                        WHERE i.AmountMinorUnits <> s.AmountMinorUnits OR i.BusinessDate < s.BusinessDate)
                        THROW 51018, 'A full compensating reversal is required.', 1;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OperatingSettlements_OperatingObligationRevisions_ObligationId_ObligationRevisionId",
                table: "OperatingSettlements");

            migrationBuilder.DropForeignKey(
                name: "FK_OperatingSettlements_OperatingObligations_DoctorId_ObligationId",
                table: "OperatingSettlements");

            migrationBuilder.DropTable(
                name: "OperatingCostAllocationSnapshots");

            migrationBuilder.DropTable(
                name: "OperatingFinanceHistories");

            migrationBuilder.DropTable(
                name: "OperatingGenerationRuns");

            migrationBuilder.DropTable(
                name: "OperatingPlanAllocations");

            migrationBuilder.DropTable(
                name: "OperatingSettlementReversals");

            migrationBuilder.DropTable(
                name: "OperatingPlanRevisions");

            migrationBuilder.DropTable(
                name: "OperatingObligationRevisions");

            migrationBuilder.DropTable(
                name: "OperatingObligations");

            migrationBuilder.DropTable(
                name: "OperatingFinancialPlans");

            migrationBuilder.DropTable(
                name: "OperatingSettlements");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Receptions_OwnerDoctorId_Id",
                table: "Receptions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_DoctorPractices_DoctorId_Id",
                table: "DoctorPractices");
        }
    }
}
