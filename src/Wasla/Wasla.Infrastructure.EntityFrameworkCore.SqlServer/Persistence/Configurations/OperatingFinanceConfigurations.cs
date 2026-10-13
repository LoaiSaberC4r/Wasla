using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.Domain.OperatingFinance;
using Wasla.Domain.Practices;
using Wasla.Domain.Doctors;
using Wasla.Domain.Security;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Configurations;

internal sealed class OperatingFinancialPlanConfiguration : IWriteEntityConfiguration<OperatingFinancialPlan>
{
    public void ConfigureAggregate(EntityTypeBuilder<OperatingFinancialPlan> b)
    {
        b.ToTable("OperatingFinancialPlans", t =>
        {
            t.HasCheckConstraint("CK_OperatingPlan_Kind", "[Kind] IN (1,2)");
            t.HasCheckConstraint("CK_OperatingPlan_Status", "[Status] IN (1,2,3)");
            t.HasCheckConstraint("CK_OperatingPlan_Reception", "([Kind] = 1 AND [ReceptionId] IS NOT NULL) OR ([Kind] = 2 AND [ReceptionId] IS NULL)");
            t.HasCheckConstraint("CK_OperatingPlan_Dates", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
        });
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.DoctorId, x.Id });
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne<Doctor>().WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Reception>().WithMany().HasForeignKey(x => new { x.DoctorId, x.ReceptionId })
            .HasPrincipalKey(x => new { x.OwnerDoctorId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ModifiedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Revisions).WithOne().HasForeignKey(x => new { x.DoctorId, x.PlanId })
            .HasPrincipalKey(x => new { x.DoctorId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.Revisions).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasIndex(x => new { x.DoctorId, x.Kind, x.Status, x.EffectiveFrom });
        b.HasIndex(x => new { x.DoctorId, x.ReceptionId, x.EffectiveFrom, x.EffectiveTo });
    }
}
internal sealed class OperatingPlanRevisionConfiguration : IWriteEntityConfiguration<OperatingPlanRevision>
{
    public void ConfigureAggregate(EntityTypeBuilder<OperatingPlanRevision> b)
    {
        b.ToTable("OperatingPlanRevisions", t =>
        {
            t.UseSqlOutputClause(false);
            t.HasCheckConstraint("CK_OperatingPlanRevision_Amount", "CAST([Amount] AS decimal(18,2)) > 0");
            t.HasCheckConstraint("CK_OperatingPlanRevision_Currency", "[CurrencyCode] = 'EGP'");
            t.HasCheckConstraint("CK_OperatingPlanRevision_Day", "[DueDay] BETWEEN 1 AND 31");
            t.HasCheckConstraint("CK_OperatingPlanRevision_Cadence", "[Cadence] IN (1,3,12)");
            t.HasCheckConstraint("CK_OperatingPlanRevision_Policy", "[AmountPolicy] IN (1,2)");
        });
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.DoctorId, x.Id });
        b.Property(x => x.Amount).HasPrecision(18, 2); b.Property(x => x.CurrencyCode).HasMaxLength(3);
        b.Property(x => x.DescriptionAr).HasMaxLength(1000); b.Property(x => x.DescriptionEn).HasMaxLength(1000);
        b.Property(x => x.Reason).HasMaxLength(2000);
        b.HasIndex(x => new { x.PlanId, x.RevisionNumber }).IsUnique();
        b.HasIndex(x => new { x.PlanId, x.EffectiveFrom, x.RecordedAtUtc });
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Allocations).WithOne().HasForeignKey(x => new { x.DoctorId, x.RevisionId })
            .HasPrincipalKey(x => new { x.DoctorId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.Allocations).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
internal sealed class OperatingPlanAllocationConfiguration : IWriteEntityConfiguration<OperatingPlanAllocation>
{
    public void ConfigureAggregate(EntityTypeBuilder<OperatingPlanAllocation> b)
    {
        b.ToTable("OperatingPlanAllocations", t =>
        { t.UseSqlOutputClause(false); t.HasCheckConstraint("CK_OperatingPlanAllocation_Percent", "CAST([Percentage] AS decimal(9,4)) > 0 AND CAST([Percentage] AS decimal(9,4)) <= 100"); });
        b.HasKey(x => x.Id); b.Property(x => x.Percentage).HasPrecision(9, 4);
        b.HasIndex(x => new { x.RevisionId, x.PracticeId }).IsUnique();
        b.HasOne<DoctorPractice>().WithMany().HasForeignKey(x => new { x.DoctorId, x.PracticeId })
            .HasPrincipalKey(x => new { x.DoctorId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
internal sealed class OperatingObligationConfiguration : IWriteEntityConfiguration<OperatingObligation>
{
    public void ConfigureAggregate(EntityTypeBuilder<OperatingObligation> b)
    {
        b.ToTable("OperatingObligations", t =>
        {
            t.HasCheckConstraint("CK_OperatingObligation_Amount", "[AmountMinorUnits] > 0 AND [PlannedAmountMinorUnits] > 0");
            t.HasCheckConstraint("CK_OperatingObligation_Currency", "[CurrencyCode] = 'EGP'");
            t.HasCheckConstraint("CK_OperatingObligation_State", "([Status] = 2 AND [ActiveSettlementId] IS NOT NULL) OR ([Status] IN (1,3) AND [ActiveSettlementId] IS NULL)");
            t.HasCheckConstraint("CK_OperatingObligation_Occurrence", "([Kind] = 2 AND [PlanId] IS NULL AND [OccurrenceKey] IS NULL) OR ([Kind] IN (1,3) AND [PlanId] IS NOT NULL AND [OccurrenceKey] IS NOT NULL AND [PeriodMonth] BETWEEN 1 AND 12 AND [PeriodYear] BETWEEN 1 AND 9999)");
        });
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.DoctorId, x.Id });
        b.Property(x => x.RowVersion).IsRowVersion(); b.Property(x => x.CurrencyCode).HasMaxLength(3);
        b.Property(x => x.OccurrenceKey).HasMaxLength(16); b.Property(x => x.DescriptionAr).HasMaxLength(1000);
        b.Property(x => x.DescriptionEn).HasMaxLength(1000);
        b.HasOne<Doctor>().WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OperatingFinancialPlan>().WithMany().HasForeignKey(x => new { x.DoctorId, x.PlanId })
            .HasPrincipalKey(x => new { x.DoctorId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Reception>().WithMany().HasForeignKey(x => new { x.DoctorId, x.ReceptionId })
            .HasPrincipalKey(x => new { x.OwnerDoctorId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OperatingSettlement>().WithMany().HasForeignKey(x => new { x.Id, x.ActiveSettlementId })
            .HasPrincipalKey(x => new { x.ObligationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Revisions).WithOne().HasForeignKey(x => new { x.DoctorId, x.ObligationId })
            .HasPrincipalKey(x => new { x.DoctorId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.Revisions).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasIndex(x => new { x.PlanId, x.PeriodYear, x.PeriodMonth }).IsUnique().HasFilter("[PlanId] IS NOT NULL AND [Kind] = 1");
        b.HasIndex(x => new { x.PlanId, x.OccurrenceKey }).IsUnique().HasFilter("[PlanId] IS NOT NULL");
        b.HasIndex(x => x.ActiveSettlementId).IsUnique().HasFilter("[ActiveSettlementId] IS NOT NULL");
        b.HasIndex(x => new { x.DoctorId, x.Status, x.DueBusinessDate });
    }
}
internal sealed class OperatingObligationRevisionConfiguration : IWriteEntityConfiguration<OperatingObligationRevision>
{
    public void ConfigureAggregate(EntityTypeBuilder<OperatingObligationRevision> b)
    {
        b.ToTable("OperatingObligationRevisions", t =>
        { t.UseSqlOutputClause(false); t.HasCheckConstraint("CK_OperatingObligationRevision_Amount", "[AmountMinorUnits] > 0"); });
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.DoctorId, x.Id }); b.HasAlternateKey(x => new { x.ObligationId, x.Id });
        b.Property(x => x.DescriptionAr).HasMaxLength(1000); b.Property(x => x.DescriptionEn).HasMaxLength(1000);
        b.Property(x => x.Reason).HasMaxLength(2000);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Allocations).WithOne().HasForeignKey(x => new { x.DoctorId, x.RevisionId })
            .HasPrincipalKey(x => new { x.DoctorId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.Allocations).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasIndex(x => new { x.ObligationId, x.RevisionNumber }).IsUnique();
        b.HasIndex(x => new { x.DoctorId, x.RecordedAtUtc, x.DueBusinessDate });
    }
}
internal sealed class OperatingCostAllocationSnapshotConfiguration : IWriteEntityConfiguration<OperatingCostAllocationSnapshot>
{
    public void ConfigureAggregate(EntityTypeBuilder<OperatingCostAllocationSnapshot> b)
    {
        b.ToTable("OperatingCostAllocationSnapshots", t =>
        { t.UseSqlOutputClause(false); t.HasCheckConstraint("CK_OperatingCostAllocation_Amount", "[AmountMinorUnits] >= 0");
            t.HasCheckConstraint("CK_OperatingCostAllocation_Percent", "CAST([Percentage] AS decimal(9,4)) > 0 AND CAST([Percentage] AS decimal(9,4)) <= 100"); });
        b.HasKey(x => x.Id); b.Property(x => x.Percentage).HasPrecision(9, 4);
        b.HasIndex(x => new { x.RevisionId, x.PracticeId }).IsUnique();
        b.HasIndex(x => new { x.PracticeId, x.RevisionId });
        b.HasOne<DoctorPractice>().WithMany().HasForeignKey(x => new { x.DoctorId, x.PracticeId })
            .HasPrincipalKey(x => new { x.DoctorId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
internal sealed class OperatingSettlementConfiguration : IWriteEntityConfiguration<OperatingSettlement>
{
    public void ConfigureAggregate(EntityTypeBuilder<OperatingSettlement> b)
    {
        b.ToTable("OperatingSettlements", t =>
        { t.UseSqlOutputClause(false); t.HasCheckConstraint("CK_OperatingSettlement_Amount", "[AmountMinorUnits] > 0");
            t.HasCheckConstraint("CK_OperatingSettlement_Currency", "[CurrencyCode] = 'EGP'");
            t.HasCheckConstraint("CK_OperatingSettlement_Method", "[PaymentMethod] IN (1,2,3,4)"); });
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.ObligationId, x.Id });
        b.Property(x => x.CurrencyCode).HasMaxLength(3); b.Property(x => x.ReceiptNumber).HasMaxLength(40);
        b.Property(x => x.Reference).HasMaxLength(200); b.Property(x => x.BackdateReason).HasMaxLength(2000);
        b.Property(x => x.ReportTimeZoneId).HasMaxLength(100);
        b.HasOne<OperatingObligation>().WithMany().HasForeignKey(x => new { x.DoctorId, x.ObligationId })
            .HasPrincipalKey(x => new { x.DoctorId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OperatingObligationRevision>().WithMany().HasForeignKey(x => new { x.ObligationId, x.ObligationRevisionId })
            .HasPrincipalKey(x => new { x.ObligationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.ReceiptNumber).IsUnique(); b.HasIndex(x => new { x.DoctorId, x.BusinessDate, x.RecordedAtUtc });
    }
}
internal sealed class OperatingSettlementReversalConfiguration : IWriteEntityConfiguration<OperatingSettlementReversal>
{
    public void ConfigureAggregate(EntityTypeBuilder<OperatingSettlementReversal> b)
    {
        b.ToTable("OperatingSettlementReversals", t =>
        { t.UseSqlOutputClause(false); t.HasCheckConstraint("CK_OperatingReversal_Amount", "[AmountMinorUnits] > 0");
            t.HasCheckConstraint("CK_OperatingReversal_Reason", "NULLIF(LTRIM(RTRIM([Reason])), '') IS NOT NULL"); });
        b.HasKey(x => x.Id); b.Property(x => x.Reason).HasMaxLength(2000); b.Property(x => x.ReportTimeZoneId).HasMaxLength(100);
        b.HasOne<OperatingSettlement>().WithMany().HasForeignKey(x => new { x.ObligationId, x.OriginalSettlementId })
            .HasPrincipalKey(x => new { x.ObligationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OperatingObligation>().WithMany().HasForeignKey(x => new { x.DoctorId, x.ObligationId })
            .HasPrincipalKey(x => new { x.DoctorId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.OriginalSettlementId).IsUnique(); b.HasIndex(x => new { x.DoctorId, x.BusinessDate, x.RecordedAtUtc });
    }
}
internal sealed class OperatingFinanceHistoryConfiguration : IWriteEntityConfiguration<OperatingFinanceHistory>
{
    public void ConfigureAggregate(EntityTypeBuilder<OperatingFinanceHistory> b)
    {
        b.ToTable("OperatingFinanceHistories", t => t.UseSqlOutputClause(false)); b.HasKey(x => x.Id);
        b.Property(x => x.ResourceType).HasMaxLength(40); b.Property(x => x.Operation).HasMaxLength(80); b.Property(x => x.Reason).HasMaxLength(2000);
        b.HasOne<Doctor>().WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.DoctorId, x.ResourceType, x.ResourceId, x.RecordedAtUtc });
    }
}

internal sealed class OperatingGenerationRunConfiguration : IWriteEntityConfiguration<OperatingGenerationRun>
{
    public void ConfigureAggregate(EntityTypeBuilder<OperatingGenerationRun> b)
    {
        b.ToTable("OperatingGenerationRuns"); b.HasKey(x => x.Id); b.Property(x => x.Status).HasMaxLength(20);
        b.Property(x => x.FailureCode).HasMaxLength(100); b.HasIndex(x => x.StartedAtUtc);
    }
}
