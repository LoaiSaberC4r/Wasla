using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.Domain.Clinical;
using Wasla.Domain.Doctors;
using Wasla.Domain.Medications;
using Wasla.Domain.Patients;
using Wasla.Domain.Security;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Configurations;

internal static class MedicationConfiguration
{
    public static void DrugFields<T>(EntityTypeBuilder<T> b) where T : class
    {
        b.Property<string>("CommercialNameEn").HasMaxLength(500).IsRequired();
        b.Property<string>("CommercialNameAr").HasMaxLength(500);
        b.Property<string>("ScientificName").HasMaxLength(800);
        b.Property<string>("Manufacturer").HasMaxLength(500);
        b.Property<string>("DrugClass").HasMaxLength(500);
        b.Property<string>("Route").HasMaxLength(200);
        b.Property<decimal?>("PriceEgp").HasPrecision(18, 2);
    }
}
internal sealed class DrugCatalogConfiguration : IWriteEntityConfiguration<DrugCatalog>
{
    public void ConfigureAggregate(EntityTypeBuilder<DrugCatalog> b)
    {
        b.ToTable("DrugCatalogs", t =>
        {
            t.HasCheckConstraint("CK_DrugCatalogs_Status", "[Status] IN (1,2,3,4)");
            t.HasCheckConstraint("CK_DrugCatalogs_Merge", "([Status] = 4 AND [MergedIntoDrugCatalogId] IS NOT NULL AND [MergedIntoDrugCatalogId] <> [Id]) OR ([Status] <> 4 AND [MergedIntoDrugCatalogId] IS NULL)");
            t.HasCheckConstraint("CK_DrugCatalogs_Price", "[PriceEgp] IS NULL OR [PriceEgp] >= 0");
        });
        b.HasKey(d => d.Id); b.Property(d => d.Id).ValueGeneratedNever(); MedicationConfiguration.DrugFields(b);
        b.Property(d => d.StrengthText).HasMaxLength(200); b.Property(d => d.DosageForm).HasMaxLength(200);
        b.Property(d => d.StatusReason).HasMaxLength(1000);
        b.Property(d => d.NormalizedCommercialNameEn).HasMaxLength(800);
        b.Property(d => d.NormalizedCommercialNameAr).HasMaxLength(800);
        b.Property(d => d.NormalizedScientificName).HasMaxLength(800); b.Property(d => d.NormalizedManufacturer).HasMaxLength(800);
        b.Property(d => d.SourceIdentityFingerprint).HasMaxLength(64); b.Property(d => d.SourceContentHash).HasMaxLength(64);
        b.Property(d => d.RowVersion).IsRowVersion().IsConcurrencyToken();
        b.HasIndex(d => new { d.Status, d.NormalizedCommercialNameEn }); b.HasIndex(d => new { d.Status, d.NormalizedCommercialNameAr });
        b.HasIndex(d => new { d.Status, d.NormalizedScientificName }); b.HasIndex(d => d.Status);
        b.HasIndex(d => d.SourceIdentityFingerprint).IsUnique().HasFilter("[SourceIdentityFingerprint] IS NOT NULL").HasDatabaseName("UX_DrugCatalogs_SourceIdentity");
        b.HasOne<DrugCatalog>().WithMany().HasForeignKey(d => d.MergedIntoDrugCatalogId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<DrugCatalogImportBatch>().WithMany().HasForeignKey(d => d.OriginImportBatchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<DrugCatalogImportBatch>().WithMany().HasForeignKey(d => d.MissingFromSourceSinceBatchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<DrugCatalogRequest>().WithMany().HasForeignKey(d => d.OriginDrugCatalogRequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(d => d.CreatedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(d => d.ModifiedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(d => d.History).WithOne().HasForeignKey(h => h.DrugCatalogId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(d => d.History).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
internal sealed class DrugCatalogHistoryConfiguration : IWriteEntityConfiguration<DrugCatalogHistory>
{
    public void ConfigureAggregate(EntityTypeBuilder<DrugCatalogHistory> b)
    {
        b.ToTable("DrugCatalogHistories"); b.HasKey(h => h.Id); b.Property(h => h.Id).ValueGeneratedNever();
        b.Property(h => h.Action).HasMaxLength(100); b.Property(h => h.Reason).HasMaxLength(1000);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(h => h.PerformedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(h => new { h.DrugCatalogId, h.OccurredAtUtc });
    }
}
internal sealed class DrugCatalogRequestConfiguration : IWriteEntityConfiguration<DrugCatalogRequest>
{
    public void ConfigureAggregate(EntityTypeBuilder<DrugCatalogRequest> b)
    {
        b.ToTable("DrugCatalogRequests", t => t.HasCheckConstraint("CK_DrugCatalogRequests_Status", "[Status] IN (1,2,3,4)"));
        b.HasKey(r => r.Id); b.Property(r => r.Id).ValueGeneratedNever();
        b.Property(r => r.MedicationName).HasMaxLength(500).IsRequired(); b.Property(r => r.ScientificName).HasMaxLength(800);
        b.Property(r => r.Manufacturer).HasMaxLength(500); b.Property(r => r.DrugClass).HasMaxLength(500);
        b.Property(r => r.Route).HasMaxLength(200); b.Property(r => r.StrengthText).HasMaxLength(200); b.Property(r => r.DosageForm).HasMaxLength(200);
        b.Property(r => r.DoctorNote).HasMaxLength(2000); b.Property(r => r.CurrentReviewReason).HasMaxLength(1000);
        b.Property(r => r.RowVersion).IsRowVersion().IsConcurrencyToken();
        b.HasOne<Doctor>().WithMany().HasForeignKey(r => r.RequestedByDoctorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.RequestedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.ReviewedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<DrugCatalog>().WithMany().HasForeignKey(r => r.ApprovedDrugCatalogId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<DrugCatalog>().WithMany().HasForeignKey(r => r.DuplicateOfDrugCatalogId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(r => new { r.RequestedByDoctorId, r.Status, r.CreatedAtUtc }); b.HasIndex(r => new { r.Status, r.CreatedAtUtc });
        b.HasMany(r => r.History).WithOne().HasForeignKey(h => h.DrugCatalogRequestId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(r => r.History).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
internal sealed class DrugCatalogRequestHistoryConfiguration : IWriteEntityConfiguration<DrugCatalogRequestHistory>
{
    public void ConfigureAggregate(EntityTypeBuilder<DrugCatalogRequestHistory> b)
    {
        b.ToTable("DrugCatalogRequestHistories"); b.HasKey(h => h.Id); b.Property(h => h.Id).ValueGeneratedNever();
        b.Property(h => h.Action).HasMaxLength(100); b.Property(h => h.Reason).HasMaxLength(1000);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(h => h.PerformedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(h => new { h.DrugCatalogRequestId, h.OccurredAtUtc });
    }
}
internal sealed class DrugCatalogImportBatchConfiguration : IWriteEntityConfiguration<DrugCatalogImportBatch>
{
    public void ConfigureAggregate(EntityTypeBuilder<DrugCatalogImportBatch> b)
    {
        b.ToTable("DrugCatalogImportBatches", t => t.HasCheckConstraint("CK_DrugCatalogImportBatches_Status", "[Status] IN (1,2,3,4)"));
        b.HasKey(r => r.Id); b.Property(r => r.Id).ValueGeneratedNever(); b.Property(r => r.Source).HasMaxLength(50);
        b.Property(r => r.SourceVersion).HasMaxLength(100); b.Property(r => r.SourceCommitSha).HasMaxLength(100); b.Property(r => r.FileSha256).HasMaxLength(64);
        b.Property(r => r.RowVersion).IsRowVersion().IsConcurrencyToken(); b.HasIndex(r => r.CreatedAtUtc);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.CreatedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(r => r.Records).WithOne().HasForeignKey(h => h.ImportBatchId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(r => r.Records).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
internal sealed class DrugCatalogImportRecordConfiguration : IWriteEntityConfiguration<DrugCatalogImportRecord>
{
    public void ConfigureAggregate(EntityTypeBuilder<DrugCatalogImportRecord> b)
    {
        b.ToTable("DrugCatalogImportRecords"); b.HasKey(r => r.Id); b.Property(r => r.Id).ValueGeneratedNever(); MedicationConfiguration.DrugFields(b);
        b.Property(r => r.IdentityFingerprint).HasMaxLength(64); b.Property(r => r.ContentHash).HasMaxLength(64); b.Property(r => r.MatchedRowVersion).HasMaxLength(8);
        b.HasIndex(r => new { r.ImportBatchId, r.ChangeType, r.SourceRowNumber });
        b.HasOne<DrugCatalog>().WithMany().HasForeignKey(r => r.MatchedDrugCatalogId).OnDelete(DeleteBehavior.Restrict);
    }
}
internal sealed class PrescriptionConfiguration : IWriteEntityConfiguration<Prescription>
{
    public void ConfigureAggregate(EntityTypeBuilder<Prescription> b)
    {
        b.ToTable("Prescriptions"); b.HasKey(p => p.Id); b.Property(p => p.Id).ValueGeneratedNever(); b.Property(p => p.RowVersion).IsRowVersion().IsConcurrencyToken();
        b.Ignore(p => p.Draft); b.Ignore(p => p.Current); b.Ignore(p => p.IsEmptyInitialDraft); b.Ignore(p => p.AuditEvents);
        b.HasIndex(p => p.MedicalEncounterId).IsUnique().HasDatabaseName("UX_Prescriptions_Encounter");
        b.HasIndex(p => new { p.PatientId, p.CreatedOnUtc }); b.HasIndex(p => new { p.DoctorId, p.CreatedOnUtc });
        b.HasOne<MedicalEncounter>().WithMany().HasForeignKey(p => p.MedicalEncounterId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Doctor>().WithMany().HasForeignKey(p => p.DoctorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Patient>().WithMany().HasForeignKey(p => p.PatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(p => p.Versions).WithOne().HasForeignKey(v => v.PrescriptionId).OnDelete(DeleteBehavior.ClientCascade);
        b.Navigation(p => p.Versions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
internal sealed class PrescriptionVersionConfiguration : IWriteEntityConfiguration<PrescriptionVersion>
{
    public void ConfigureAggregate(EntityTypeBuilder<PrescriptionVersion> b)
    {
        b.ToTable("PrescriptionVersions", t =>
        {
            t.HasCheckConstraint("CK_PrescriptionVersions_Status", "[Status] IN (1,2,3,4)");
            t.HasCheckConstraint("CK_PrescriptionVersions_Number", "[VersionNumber] > 0");
            t.HasCheckConstraint("CK_PrescriptionVersions_Lifecycle", "([Status] = 1 AND [FinalizedAtUtc] IS NULL AND [VoidedAtUtc] IS NULL) OR ([Status] IN (2,3) AND [FinalizedAtUtc] IS NOT NULL AND [VoidedAtUtc] IS NULL) OR ([Status] = 4 AND [FinalizedAtUtc] IS NOT NULL AND [VoidedAtUtc] IS NOT NULL AND [VoidReason] IS NOT NULL)");
        });
        b.HasKey(v => v.Id); b.Property(v => v.Id).ValueGeneratedNever();
        b.Property(v => v.CorrectionReason).HasMaxLength(1000); b.Property(v => v.VoidReason).HasMaxLength(1000);
        b.HasIndex(v => new { v.PrescriptionId, v.VersionNumber }).IsUnique().HasDatabaseName("UX_PrescriptionVersions_Number");
        b.HasIndex(v => v.PrescriptionId).IsUnique().HasFilter("[Status] = 1").HasDatabaseName("UX_PrescriptionVersions_Draft");
        // Separate named indexes are required for two filters over the same key.
        b.HasIndex(v => v.PrescriptionId, "IX_PrescriptionVersions_Current").IsUnique().HasFilter("[Status] = 2").HasDatabaseName("UX_PrescriptionVersions_Current");
        b.HasIndex(v => v.PreviousVersionId).IsUnique().HasFilter("[PreviousVersionId] IS NOT NULL").HasDatabaseName("UX_PrescriptionVersions_Previous");
        b.HasOne<PrescriptionVersion>().WithMany().HasForeignKey(v => v.PreviousVersionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(v => v.CreatedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(v => v.FinalizedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(v => v.VoidedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(v => v.Items).WithOne().HasForeignKey(i => i.PrescriptionVersionId).OnDelete(DeleteBehavior.ClientCascade);
        b.Navigation(v => v.Items).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
    }
}
internal sealed class PrescriptionItemConfiguration : IWriteEntityConfiguration<PrescriptionItem>
{
    public void ConfigureAggregate(EntityTypeBuilder<PrescriptionItem> b)
    {
        b.ToTable("PrescriptionItems", t =>
        {
            t.HasCheckConstraint("CK_PrescriptionItems_Source", "([MedicationSource] = 1 AND [DrugCatalogId] IS NOT NULL AND [DrugCatalogRequestId] IS NULL) OR ([MedicationSource] = 2 AND [DrugCatalogId] IS NULL AND [DrugCatalogRequestId] IS NOT NULL)");
            // Draft fields are nullable; supplied values must be coherent.
            t.HasCheckConstraint("CK_PrescriptionItems_Duration", "([DurationType] IS NULL AND [DurationValue] IS NULL AND [DurationUnit] IS NULL) OR ([DurationType] IS NOT NULL AND [DurationType] = 1 AND ([DurationValue] IS NULL OR [DurationValue] > 0) AND ([DurationUnit] IS NULL OR [DurationUnit] IN (1,2,3))) OR ([DurationType] IS NOT NULL AND [DurationType] = 2 AND [DurationValue] IS NULL AND [DurationUnit] IS NULL)");
            t.HasCheckConstraint("CK_PrescriptionItems_Quantity", "([QuantityValue] IS NULL AND [QuantityUnit] IS NULL) OR ([QuantityValue] IS NOT NULL AND [QuantityValue] > 0 AND [QuantityUnit] IS NOT NULL AND [QuantityUnit] IN (1,2,3,4,5,6,7,8,9,10))");
            t.HasCheckConstraint("CK_PrescriptionItems_Order", "[SortOrder] > 0");
        });
        b.HasKey(i => i.Id); b.Property(i => i.Id).ValueGeneratedNever();
        b.Property(i => i.MedicationNameSnapshot).HasMaxLength(500).IsRequired(); b.Property(i => i.ScientificNameSnapshot).HasMaxLength(800);
        foreach (var name in new[] { "StrengthSnapshot", "DosageFormSnapshot", "RouteSnapshot", "DoseText", "FrequencyText", "PrnReason", "MinimumIntervalText", "MaxPer24HoursText" }) b.Property<string>(name).HasMaxLength(200);
        b.Property(i => i.Instructions).HasMaxLength(2000); b.Property(i => i.QuantityValue).HasPrecision(18, 2);
        b.HasOne<DrugCatalog>().WithMany().HasForeignKey(i => i.DrugCatalogId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<DrugCatalogRequest>().WithMany().HasForeignKey(i => i.DrugCatalogRequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(i => new { i.PrescriptionVersionId, i.SortOrder }).IsUnique();
    }
}
internal sealed class PrescriptionAuditEventConfiguration : IWriteEntityConfiguration<PrescriptionAuditEvent>
{
    public void ConfigureAggregate(EntityTypeBuilder<PrescriptionAuditEvent> b)
    {
        b.ToTable("PrescriptionAuditEvents"); b.HasKey(a => a.Id); b.Property(a => a.Id).ValueGeneratedNever(); b.Property(a => a.Action).HasMaxLength(100);
        // IDs are immutable audit metadata: discarded drafts may be physically removed.
        b.HasOne<Doctor>().WithMany().HasForeignKey(a => a.DoctorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.ActorApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(a => new { a.PrescriptionId, a.OccurredAtUtc });
    }
}
internal sealed class MedicationIdempotencyRecordConfiguration : IWriteEntityConfiguration<MedicationIdempotencyRecord>
{
    public void ConfigureAggregate(EntityTypeBuilder<MedicationIdempotencyRecord> b)
    {
        b.ToTable("MedicationIdempotencyRecords"); b.HasKey(r => r.Id); b.Property(r => r.Id).ValueGeneratedNever();
        b.Property(r => r.Operation).HasMaxLength(100); b.Property(r => r.IdempotencyKey).HasMaxLength(200); b.Property(r => r.RequestFingerprint).HasMaxLength(64);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.ActorApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(r => new { r.ActorApplicationUserId, r.Operation, r.IdempotencyKey }).IsUnique();
    }
}
