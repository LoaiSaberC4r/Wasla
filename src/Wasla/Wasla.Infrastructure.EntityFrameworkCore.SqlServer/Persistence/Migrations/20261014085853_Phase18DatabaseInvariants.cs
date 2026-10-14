using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase18DatabaseInvariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TRIGGER [TR_OperatingSettlements_OneEffective] ON [OperatingSettlements] AFTER INSERT AS
                BEGIN
                 SET NOCOUNT ON;
                 IF EXISTS (SELECT 1 FROM inserted i JOIN [OperatingSettlements] s WITH (UPDLOCK,HOLDLOCK)
                    ON s.[ObligationId] = i.[ObligationId] AND s.[Id] <> i.[Id]
                    WHERE NOT EXISTS (SELECT 1 FROM [OperatingSettlementReversals] r WHERE r.[OriginalSettlementId] = s.[Id]))
                    THROW 51018, 'An obligation can have only one unreversed settlement.', 1;
                END
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER [TR_Immutable_DoctorInsightHistories] ON [DoctorInsightHistories] AFTER UPDATE, DELETE AS THROW 51018, 'Reporting evidence and history are immutable.', 1;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER [TR_Immutable_PracticeReportingPolicySnapshots] ON [PracticeReportingPolicySnapshots] AFTER UPDATE, DELETE AS THROW 51018, 'Reporting evidence and history are immutable.', 1;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER [TR_SalaryPlans_NoOverlap] ON [OperatingFinancialPlans] AFTER INSERT, UPDATE AS
                BEGIN
                 SET NOCOUNT ON;
                 IF EXISTS (SELECT 1 FROM inserted i JOIN [OperatingFinancialPlans] p WITH (UPDLOCK,HOLDLOCK)
                 ON p.[DoctorId] = i.[DoctorId] AND p.[ReceptionId] = i.[ReceptionId] AND p.[Id] <> i.[Id] AND p.[Kind] = 1 AND i.[Kind] = 1
                 AND (i.[EffectiveTo] IS NULL OR p.[EffectiveFrom] <= i.[EffectiveTo]) AND (p.[EffectiveTo] IS NULL OR p.[EffectiveTo] >= i.[EffectiveFrom])
                 WHERE NOT (p.[Status] = 3 AND p.[EffectiveTo] = p.[EffectiveFrom] AND NOT EXISTS (SELECT 1 FROM [OperatingObligations] o WHERE o.[PlanId] = p.[Id]))
                 AND NOT (i.[Status] = 3 AND i.[EffectiveTo] = i.[EffectiveFrom] AND NOT EXISTS (SELECT 1 FROM [OperatingObligations] o WHERE o.[PlanId] = i.[Id])))
                 THROW 51018, 'Salary plan effective intervals cannot overlap.', 1;
                END
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER [TR_NoDelete_OperatingFinancialPlans] ON [OperatingFinancialPlans] INSTEAD OF DELETE AS THROW 51018, 'Phase 18 records cannot be deleted.', 1;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER [TR_NoDelete_OperatingObligations] ON [OperatingObligations] INSTEAD OF DELETE AS THROW 51018, 'Phase 18 records cannot be deleted.', 1;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER [TR_NoDelete_DoctorInsightFollowUps] ON [DoctorInsightFollowUps] INSTEAD OF DELETE AS THROW 51018, 'Phase 18 records cannot be deleted.', 1;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER [TR_NoDelete_ReceptionHandovers] ON [ReceptionHandovers] INSTEAD OF DELETE AS THROW 51018, 'Phase 18 records cannot be deleted.', 1;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER [TR_NoDelete_AdministrativeInvestigations] ON [AdministrativeInvestigations] INSTEAD OF DELETE AS THROW 51018, 'Phase 18 records cannot be deleted.', 1;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER [TR_InsightEvidence_Ownership] ON [DoctorInsightFollowUps] AFTER UPDATE AS
                IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON i.[Id] = d.[Id] WHERE i.[DoctorId] <> d.[DoctorId] OR i.[PracticeId] <> d.[PracticeId] OR i.[MetricCode] <> d.[MetricCode] OR i.[EvidenceJson] <> d.[EvidenceJson])
                THROW 51018, 'Insight evidence and ownership are immutable.', 1;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER [TR_Handover_Ownership] ON [ReceptionHandovers] AFTER UPDATE AS
                IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON i.[Id] = d.[Id] WHERE i.[DoctorId] <> d.[DoctorId] OR i.[DoctorPracticeId] <> d.[DoctorPracticeId] OR i.[Summary] <> d.[Summary] OR i.[CreatedByApplicationUserId] <> d.[CreatedByApplicationUserId] OR ISNULL(i.[ReservationId],CONVERT(uniqueidentifier,0x0)) <> ISNULL(d.[ReservationId],CONVERT(uniqueidentifier,0x0)) OR ISNULL(i.[TicketId],CONVERT(uniqueidentifier,0x0)) <> ISNULL(d.[TicketId],CONVERT(uniqueidentifier,0x0)) OR ISNULL(i.[PreviousHandoverId],CONVERT(uniqueidentifier,0x0)) <> ISNULL(d.[PreviousHandoverId],CONVERT(uniqueidentifier,0x0)))
                THROW 51018, 'Handover ownership and original content are immutable.', 1;
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_OperatingSettlements_OneEffective];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_Immutable_DoctorInsightHistories];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_Immutable_PracticeReportingPolicySnapshots];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_SalaryPlans_NoOverlap];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_NoDelete_OperatingFinancialPlans];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_NoDelete_OperatingObligations];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_NoDelete_DoctorInsightFollowUps];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_NoDelete_ReceptionHandovers];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_NoDelete_AdministrativeInvestigations];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_InsightEvidence_Ownership];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_Handover_Ownership];");

        }
    }
}
