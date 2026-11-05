using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.Domain.Subscriptions;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Configurations;

internal sealed class CommercialFeatureConfiguration : IWriteEntityConfiguration<CommercialFeature>
{
    public void ConfigureAggregate(EntityTypeBuilder<CommercialFeature> b)
    {
        b.ToTable("CommercialFeatures");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(200).IsRequired();
        b.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
        b.Property(x => x.NameEn).HasMaxLength(200);
        b.Property(x => x.DescriptionAr).HasMaxLength(2000);
        b.Property(x => x.DescriptionEn).HasMaxLength(2000);
        b.Property(x => x.GroupCode).HasMaxLength(200).IsRequired();
        b.Property(x => x.CreatedOnUtc).HasColumnType("datetime2(7)");
        b.Property(x => x.ModifiedOnUtc).HasColumnType("datetime2(7)");
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.Code).IsUnique();
    }
}

internal sealed class CommercialPlanConfiguration : IWriteEntityConfiguration<CommercialPlan>
{
    public void ConfigureAggregate(EntityTypeBuilder<CommercialPlan> b)
    {
        b.ToTable("CommercialPlans");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(200).IsRequired();
        b.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
        b.Property(x => x.NameEn).HasMaxLength(200);
        b.Property(x => x.DescriptionAr).HasMaxLength(2000);
        b.Property(x => x.DescriptionEn).HasMaxLength(2000);
        b.Property(x => x.CreatedOnUtc).HasColumnType("datetime2(7)");
        b.Property(x => x.ModifiedOnUtc).HasColumnType("datetime2(7)");
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne<CommercialProduct>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ProductId, x.Code }).IsUnique();
    }
}

internal sealed class CommercialProductConfiguration : IWriteEntityConfiguration<CommercialProduct>
{
    public void ConfigureAggregate(EntityTypeBuilder<CommercialProduct> b)
    {
        b.ToTable("CommercialProducts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(200).IsRequired();
        b.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
        b.Property(x => x.NameEn).HasMaxLength(200);
        b.Property(x => x.DescriptionAr).HasMaxLength(2000);
        b.Property(x => x.DescriptionEn).HasMaxLength(2000);
        b.Property(x => x.CreatedOnUtc).HasColumnType("datetime2(7)");
        b.Property(x => x.ModifiedOnUtc).HasColumnType("datetime2(7)");
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.Code).IsUnique();
    }
}

internal sealed class DoctorSubscriptionConfiguration : IWriteEntityConfiguration<DoctorSubscription>
{
    public void ConfigureAggregate(EntityTypeBuilder<DoctorSubscription> b)
    {
        b.ToTable("DoctorSubscriptions");
        b.HasKey(x => x.Id);
        b.Property(x => x.PriceAmount).HasPrecision(18, 2);
        b.Property(x => x.Currency).HasMaxLength(200).IsRequired();
        b.Property(x => x.StartsAtUtc).HasColumnType("datetime2(7)");
        b.Property(x => x.EndsAtUtc).HasColumnType("datetime2(7)");
        b.Property(x => x.TrialStartedAtUtc).HasColumnType("datetime2(7)");
        b.Property(x => x.TrialEndsAtUtc).HasColumnType("datetime2(7)");
        b.Property(x => x.CatalogVersion).HasMaxLength(200).IsRequired();
        b.Property(x => x.TermsSnapshotJson);
        b.Property(x => x.CancellationRequestedOnUtc).HasColumnType("datetime2(7)");
        b.Property(x => x.CreatedOnUtc).HasColumnType("datetime2(7)");
        b.Property(x => x.ModifiedOnUtc).HasColumnType("datetime2(7)");
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne<Wasla.Domain.Doctors.Doctor>().WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CommercialProduct>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CommercialPlan>().WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Wasla.Domain.Security.ApplicationUser>().WithMany().HasForeignKey(x => x.ActivatedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.DoctorId, x.Status });
        b.HasIndex(x => new { x.Status, x.EndsAtUtc });
        b.HasOne(x => x.Contract).WithMany().HasForeignKey(x => x.SubscriptionContractId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.SubscriptionContractId, "UX_OnePendingPeriodPerContract").IsUnique().HasFilter("[Status] = 1");
        b.HasIndex(x => x.SubscriptionContractId, "UX_OneActivePeriodPerContract").IsUnique().HasFilter("[Status] = 2");
        b.HasIndex(x => x.DoctorId, "UX_OneTrialPerDoctor").IsUnique().HasFilter("[SubscriptionKind] = 2");
        b.HasIndex(x => x.SubscriptionContractId, "UX_OneScheduledPeriodPerContract").IsUnique().HasFilter("[Status] = 6");
        b.HasIndex(x => new { x.SubscriptionContractId, x.StartsAtUtc, x.EndsAtUtc });
        b.Ignore(x => x.Terms);
        b.Ignore(x => x.DomainEvents);
        b.ToTable(t => t.HasCheckConstraint("CK_DoctorSubscriptions_Terms", "[PracticeLimit] > 0 AND [ReceptionLimit] >= 0 AND [PriceAmount] >= 0"));
        b.ToTable(t => t.HasTrigger("TR_DoctorSubscriptions_NoPeriodOverlap"));
    }
}

internal sealed class FeaturePermissionMappingConfiguration : IWriteEntityConfiguration<FeaturePermissionMapping>
{
    public void ConfigureAggregate(EntityTypeBuilder<FeaturePermissionMapping> b)
    {
        b.ToTable("FeaturePermissionMappings");
        b.HasKey(x => x.Id);
        b.Property(x => x.CreatedOnUtc).HasColumnType("datetime2(7)");
        b.HasOne<CommercialFeature>().WithMany().HasForeignKey(x => x.FeatureId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Wasla.Domain.Security.Permission>().WithMany().HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.PermissionId).IsUnique();
    }
}

internal sealed class LegacyTransitionEnrollmentBatchConfiguration : IWriteEntityConfiguration<LegacyTransitionEnrollmentBatch>
{
    public void ConfigureAggregate(EntityTypeBuilder<LegacyTransitionEnrollmentBatch> b)
    {
        b.ToTable("LegacyTransitionEnrollmentBatches");
        b.HasKey(x => x.Id);
        b.Property(x => x.StartedAtUtc).HasColumnType("datetime2(7)");
        b.Property(x => x.EndsAtUtc).HasColumnType("datetime2(7)");
        b.Property(x => x.Status).HasMaxLength(200).IsRequired();
        b.Property(x => x.CompletedAtUtc).HasColumnType("datetime2(7)");
        b.Property(x => x.CreatedOnUtc).HasColumnType("datetime2(7)");
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne<Wasla.Domain.Security.ApplicationUser>().WithMany().HasForeignKey(x => x.InitiatedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PlanFeatureEntitlementConfiguration : IWriteEntityConfiguration<PlanFeatureEntitlement>
{
    public void ConfigureAggregate(EntityTypeBuilder<PlanFeatureEntitlement> b)
    {
        b.ToTable("PlanFeatureEntitlements");
        b.HasKey(x => x.Id);
        b.Property(x => x.CreatedOnUtc).HasColumnType("datetime2(7)");
        b.HasOne<CommercialPlan>().WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CommercialFeature>().WithMany().HasForeignKey(x => x.FeatureId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.PlanId, x.FeatureId }).IsUnique();
    }
}

internal sealed class SubscriptionChangeRequestConfiguration : IWriteEntityConfiguration<SubscriptionChangeRequest>
{
    public void ConfigureAggregate(EntityTypeBuilder<SubscriptionChangeRequest> b)
    {
        b.ToTable("SubscriptionChangeRequests");
        b.HasKey(x => x.Id);
        b.Property(x => x.TargetPlanCode).HasMaxLength(200);
        b.Property(x => x.RequestedOnUtc).HasColumnType("datetime2(7)");
        b.Property(x => x.ReviewedOnUtc).HasColumnType("datetime2(7)");
        b.Property(x => x.RejectionReason).HasMaxLength(2000);
        b.Property(x => x.TargetPriceSnapshot).HasPrecision(18, 2);
        b.Property(x => x.DifferenceAmount).HasPrecision(18, 2);
        b.Property(x => x.EffectiveAtUtc).HasColumnType("datetime2(7)");
        b.Property(x => x.TargetTermsSnapshotJson);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne<DoctorSubscription>().WithMany().HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Wasla.Domain.Security.ApplicationUser>().WithMany().HasForeignKey(x => x.RequestedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Wasla.Domain.Security.ApplicationUser>().WithMany().HasForeignKey(x => x.ReviewedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.SubscriptionId).IsUnique().HasFilter("[Status] = 1").HasDatabaseName("UX_OnePendingChangePerSubscription");
        b.HasIndex(x => new { x.SubscriptionId, x.Status });
        b.Ignore(x => x.TargetTerms);
        b.Ignore(x => x.RetainedAllocations);
    }
}

internal sealed class SubscriptionFeatureSnapshotConfiguration : IWriteEntityConfiguration<SubscriptionFeatureSnapshot>
{
    public void ConfigureAggregate(EntityTypeBuilder<SubscriptionFeatureSnapshot> b)
    {
        b.ToTable("SubscriptionFeatureSnapshots");
        b.HasKey(x => x.Id);
        b.Property(x => x.FeatureCodeSnapshot).HasMaxLength(200).IsRequired();
        b.Property(x => x.CreatedOnUtc).HasColumnType("datetime2(7)");
        b.HasOne<DoctorSubscription>().WithMany().HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CommercialFeature>().WithMany().HasForeignKey(x => x.FeatureId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.SubscriptionId, x.FeatureId }).IsUnique();
    }
}

internal sealed class SubscriptionHistoryConfiguration : IWriteEntityConfiguration<SubscriptionHistory>
{
    public void ConfigureAggregate(EntityTypeBuilder<SubscriptionHistory> b)
    {
        b.ToTable("SubscriptionHistories");
        b.HasKey(x => x.Id);
        b.Property(x => x.EventType).HasMaxLength(200).IsRequired();
        b.Property(x => x.OccurredOnUtc).HasColumnType("datetime2(7)");
        b.Property(x => x.Reason).HasMaxLength(2000);
        b.Property(x => x.PreviousTermsSnapshotJson);
        b.Property(x => x.NewTermsSnapshotJson);
        b.Property(x => x.MetadataJson);
        b.Property(x => x.EventIdempotencyKey).HasMaxLength(450).IsRequired();
        b.Property(x => x.CreatedOnUtc).HasColumnType("datetime2(7)");
        b.HasOne<DoctorSubscription>().WithMany().HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Wasla.Domain.Security.ApplicationUser>().WithMany().HasForeignKey(x => x.ActorApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.EventIdempotencyKey).IsUnique();
        b.HasIndex(x => new { x.SubscriptionId, x.OccurredOnUtc });
    }
}

internal sealed class SubscriptionPaymentConfiguration : IWriteEntityConfiguration<SubscriptionPayment>
{
    public void ConfigureAggregate(EntityTypeBuilder<SubscriptionPayment> b)
    {
        b.ToTable("SubscriptionPayments");
        b.HasKey(x => x.Id);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Currency).HasMaxLength(200).IsRequired();
        b.Property(x => x.PaidAtUtc).HasColumnType("datetime2(7)");
        b.Property(x => x.Reference).HasMaxLength(200);
        b.Property(x => x.Notes).HasMaxLength(2000);
        b.Property(x => x.IdempotencyKey).HasMaxLength(450).IsRequired();
        b.Property(x => x.CreatedOnUtc).HasColumnType("datetime2(7)");
        b.HasOne<DoctorSubscription>().WithMany().HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Wasla.Domain.Security.ApplicationUser>().WithMany().HasForeignKey(x => x.RecordedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.IdempotencyKey).IsUnique();
        b.HasIndex(x => new { x.SubscriptionId, x.PaidAtUtc });
    }
}

internal sealed class SubscriptionReminderScheduleConfiguration : IWriteEntityConfiguration<SubscriptionReminderSchedule>
{
    public void ConfigureAggregate(EntityTypeBuilder<SubscriptionReminderSchedule> b)
    {
        b.ToTable("SubscriptionReminderSchedules");
        b.HasKey(x => x.Id);
        b.Property(x => x.ReminderCode).HasMaxLength(200).IsRequired();
        b.Property(x => x.DueAtUtc).HasColumnType("datetime2(7)");
        b.Property(x => x.CreatedOnUtc).HasColumnType("datetime2(7)");
        b.Property(x => x.ProcessedOnUtc).HasColumnType("datetime2(7)");
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne<DoctorSubscription>().WithMany().HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.SubscriptionId, x.ReminderCode, x.TrialRevision }).IsUnique();
        b.HasIndex(x => new { x.Status, x.DueAtUtc });
    }
}
