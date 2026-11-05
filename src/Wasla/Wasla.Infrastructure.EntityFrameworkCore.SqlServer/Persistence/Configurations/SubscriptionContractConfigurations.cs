using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.Domain.Subscriptions;
using Wasla.Domain.Doctors;
using Wasla.Domain.Practices;
using Wasla.Domain.Security;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Configurations;

internal sealed class SubscriptionContractConfiguration : IWriteEntityConfiguration<SubscriptionContract>
{
    public void ConfigureAggregate(EntityTypeBuilder<SubscriptionContract> b)
    {
        b.ToTable("SubscriptionContracts"); b.HasKey(x => x.Id);
        b.Property(x => x.CreatedOnUtc).HasColumnType("datetime2(7)"); b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne<Doctor>().WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.DoctorId, x.Kind }); b.Ignore(x => x.DomainEvents);
    }
}
internal sealed class PracticeSubscriptionAllocationConfiguration : IWriteEntityConfiguration<PracticeSubscriptionAllocation>
{
    public void ConfigureAggregate(EntityTypeBuilder<PracticeSubscriptionAllocation> b)
    {
        b.ToTable("PracticeSubscriptionAllocations", t => t.HasCheckConstraint("CK_PracticeSubscriptionAllocations_Time", "[ReleasedAtUtc] IS NULL OR [ReleasedAtUtc] >= [AllocatedFromUtc]"));
        b.HasKey(x => x.Id); b.Property(x => x.RowVersion).IsRowVersion();
        b.Property(x => x.AllocatedFromUtc).HasColumnType("datetime2(7)"); b.Property(x => x.ReleasedAtUtc).HasColumnType("datetime2(7)");
        b.Property(x => x.ReleaseReason).HasMaxLength(2000);
        b.HasOne<DoctorPractice>().WithMany().HasForeignKey(x => x.DoctorPracticeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SubscriptionContract>().WithMany().HasForeignKey(x => x.SubscriptionContractId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ReleasedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.DoctorPracticeId).IsUnique().HasFilter("[ReleasedAtUtc] IS NULL").HasDatabaseName("UX_OneCurrentAllocationPerPractice");
        b.HasIndex(x => new { x.SubscriptionContractId, x.ReleasedAtUtc }); b.Ignore(x => x.DomainEvents);
    }
}
internal sealed class SubscriptionAllocationSelectionConfiguration : IWriteEntityConfiguration<SubscriptionAllocationSelection>
{
    public void ConfigureAggregate(EntityTypeBuilder<SubscriptionAllocationSelection> b)
    {
        b.ToTable("SubscriptionAllocationSelections"); b.HasKey(x => x.Id); b.HasAlternateKey(x => x.SubscriptionId);
        b.Property(x => x.CreatedOnUtc).HasColumnType("datetime2(7)"); b.Property(x => x.AppliedOnUtc).HasColumnType("datetime2(7)");
        b.Property(x => x.RowVersion).IsRowVersion(); b.Property(x => x.SelectionJson).IsRequired();
        b.HasOne<DoctorSubscription>().WithMany().HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.RetainedPractices).WithOne().HasForeignKey(x => x.SubscriptionId).HasPrincipalKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.RetainedReceptions).WithOne().HasForeignKey(x => x.SubscriptionId).HasPrincipalKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
        b.Ignore(x => x.Practices); b.Ignore(x => x.DomainEvents);
    }
}
internal sealed class SubscriptionRetainedPracticeConfiguration : IWriteEntityConfiguration<SubscriptionRetainedPractice>
{
    public void ConfigureAggregate(EntityTypeBuilder<SubscriptionRetainedPractice> b)
    {
        b.ToTable("SubscriptionRetainedPractices"); b.HasKey(x => new { x.SubscriptionId, x.DoctorPracticeId });
        b.HasOne<PracticeSubscriptionAllocation>().WithMany().HasForeignKey(x => x.AllocationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<DoctorPractice>().WithMany().HasForeignKey(x => x.DoctorPracticeId).OnDelete(DeleteBehavior.Restrict);
    }
}
internal sealed class SubscriptionRetainedReceptionConfiguration : IWriteEntityConfiguration<SubscriptionRetainedReception>
{
    public void ConfigureAggregate(EntityTypeBuilder<SubscriptionRetainedReception> b)
    {
        b.ToTable("SubscriptionRetainedReceptions"); b.HasKey(x => new { x.SubscriptionId, x.DoctorPracticeId, x.ReceptionId });
        b.HasOne<Reception>().WithMany().HasForeignKey(x => x.ReceptionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<DoctorPractice>().WithMany().HasForeignKey(x => x.DoctorPracticeId).OnDelete(DeleteBehavior.Restrict);
    }
}
internal sealed class ReservationCommercialProvenanceConfiguration : IWriteEntityConfiguration<ReservationCommercialProvenance>
{
    public void ConfigureAggregate(EntityTypeBuilder<ReservationCommercialProvenance> b)
    {
        b.ToTable("ReservationCommercialProvenances"); b.HasKey(x => x.Id);
        b.Property(x => x.AcceptedAtUtc).HasColumnType("datetime2(7)"); b.Property(x => x.TermsSnapshotJson).IsRequired();
        b.HasOne<Doctor>().WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<DoctorPractice>().WithMany().HasForeignKey(x => x.DoctorPracticeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PracticeSubscriptionAllocation>().WithMany().HasForeignKey(x => x.AllocationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SubscriptionContract>().WithMany().HasForeignKey(x => x.SubscriptionContractId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<DoctorSubscription>().WithMany().HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.DoctorPracticeId, x.SubscriptionId }); b.Ignore(x => x.Terms); b.Ignore(x => x.DomainEvents);
        // Reservation is archived out of its live table. Id remains its durable logical key.
    }
}
internal sealed class SubscriptionMutationReceiptConfiguration : IWriteEntityConfiguration<SubscriptionMutationReceipt>
{
    public void ConfigureAggregate(EntityTypeBuilder<SubscriptionMutationReceipt> b)
    {
        b.ToTable("SubscriptionMutationReceipts"); b.HasKey(x => x.Id);
        b.Property(x => x.IdempotencyKey).HasMaxLength(150).IsRequired(); b.Property(x => x.Fingerprint).HasMaxLength(64).IsRequired();
        b.Property(x => x.CreatedOnUtc).HasColumnType("datetime2(7)");
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.IdempotencyKey).IsUnique(); b.Ignore(x => x.DomainEvents);
    }
}
internal sealed class SubscriptionMigrationIssueConfiguration : IWriteEntityConfiguration<SubscriptionMigrationIssue>
{
    public void ConfigureAggregate(EntityTypeBuilder<SubscriptionMigrationIssue> b)
    {
        b.ToTable("SubscriptionMigrationIssues"); b.HasKey(x => x.Id); b.Property(x => x.IssueCode).HasMaxLength(200).IsRequired();
        b.Property(x => x.CreatedOnUtc).HasColumnType("datetime2(7)"); b.HasIndex(x => new { x.ResourceId, x.IssueCode }).IsUnique(); b.Ignore(x => x.DomainEvents);
    }
}
