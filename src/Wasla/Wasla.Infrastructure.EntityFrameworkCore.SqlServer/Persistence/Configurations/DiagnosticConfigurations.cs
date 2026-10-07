using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.Domain.Labs;
using Wasla.Domain.Radiology;
using Wasla.Domain.Diagnostics;
using Wasla.Domain.Clinical;
using Wasla.Domain.Doctors;
using Wasla.Domain.Patients;
using Wasla.Domain.Practices;
namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Configurations;

internal sealed class LabTestCatalogConfiguration : IWriteEntityConfiguration<LabTestCatalog>
{
    public void ConfigureAggregate(EntityTypeBuilder<LabTestCatalog> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.ToTable("LabTestCatalogs", t =>
        {
            t.HasCheckConstraint("CK_LabTestCatalog_Source", "([Source] = 1 AND [LoincCode] IS NOT NULL) OR ([Source] = 2 AND [LoincCode] IS NULL)");
            t.HasCheckConstraint("CK_LabTestCatalog_Merge", "([Status] = 3 AND [MergedIntoId] IS NOT NULL AND [MergedIntoId] <> [Id]) OR ([Status] IN (1,2) AND [MergedIntoId] IS NULL)");
        });
        b.Ignore(x => x.IsSelectable); b.Ignore(x => x.NameEn); b.Ignore(x => x.NameAr);
        b.Property(x => x.LoincCode).HasMaxLength(20); b.Property(x => x.OfficialNameEn).HasMaxLength(1000); b.Property(x => x.OfficialNameAr).HasMaxLength(1000);
        b.Property(x => x.DisplayNameEn).HasMaxLength(1000); b.Property(x => x.DisplayNameAr).HasMaxLength(1000);
        b.Property(x => x.AliasesEn).HasMaxLength(4000); b.Property(x => x.AliasesAr).HasMaxLength(4000); b.Property(x => x.InternalNote).HasMaxLength(2000);
        b.Property(x => x.NormalizedSearch).HasMaxLength(12000); b.Property(x => x.NormalizedName).HasMaxLength(1000);
        b.Property(x => x.SourceVersion).HasMaxLength(30); b.Property(x => x.SourceHash).HasMaxLength(64); b.Property(x => x.ExternalStatus).HasMaxLength(40);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.LoincCode).IsUnique().HasFilter("[LoincCode] IS NOT NULL").HasDatabaseName("UX_LabTestCatalog_Loinc");
        b.HasIndex(x => new { x.Status, x.Source }); b.HasIndex(x => new { x.Status, x.IsCommonOrder });
        b.HasOne<LabTestCatalog>().WithMany().HasForeignKey(x => x.MergedIntoId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.History).WithOne().HasForeignKey(x => x.ResourceId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.History).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
internal sealed class LabTestCatalogHistoryConfiguration : IWriteEntityConfiguration<LabTestCatalogHistory>
{
    public void ConfigureAggregate(EntityTypeBuilder<LabTestCatalogHistory> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.ToTable("LabTestCatalogHistories");
        b.Property(x => x.Action).HasMaxLength(100); b.Property(x => x.Reason).HasMaxLength(1000);
        b.Property(x => x.ActorType).HasMaxLength(40); b.HasIndex(x => new { x.ResourceId, x.OccurredAtUtc });

    }
}
internal sealed class LabCatalogRequestConfiguration : IWriteEntityConfiguration<LabCatalogRequest>
{
    public void ConfigureAggregate(EntityTypeBuilder<LabCatalogRequest> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.ToTable("LabCatalogRequests"); b.Property(x => x.Name).HasMaxLength(1000); b.Property(x => x.Specimen).HasMaxLength(500);
        b.Property(x => x.CatalogClarificationNote).HasMaxLength(2000); b.Property(x => x.ReviewReason).HasMaxLength(1000); b.Property(x => x.ReasonType).HasMaxLength(40);
        b.Property(x => x.RowVersion).IsRowVersion(); b.HasIndex(x => new { x.RequestedByDoctorId, x.Status });
        b.HasOne<Doctor>().WithMany().HasForeignKey(x => x.RequestedByDoctorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<LabTestCatalog>().WithMany().HasForeignKey(x => x.CanonicalCatalogId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.History).WithOne().HasForeignKey(x => x.ResourceId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.History).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
internal sealed class LabCatalogRequestHistoryConfiguration : IWriteEntityConfiguration<LabCatalogRequestHistory>
{
    public void ConfigureAggregate(EntityTypeBuilder<LabCatalogRequestHistory> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.ToTable("LabCatalogRequestHistories");
        b.Property(x => x.Action).HasMaxLength(100); b.Property(x => x.Reason).HasMaxLength(1000);
        b.Property(x => x.ActorType).HasMaxLength(40); b.HasIndex(x => new { x.ResourceId, x.OccurredAtUtc });

    }
}
internal sealed class LabRequestConfiguration : IWriteEntityConfiguration<LabRequest>
{
    public void ConfigureAggregate(EntityTypeBuilder<LabRequest> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.ToTable("LabRequests", t =>
        {
            t.HasCheckConstraint("CK_LabRequests_State", "[Status] IN (1,2,3,4,5) AND [Origin] IN (1,2)");
            t.HasCheckConstraint("CK_LabRequests_PostVisit", "[Origin] = 1 OR ([PostVisitReason] IS NOT NULL AND [Status] <> 1)");
        });
        b.Property(x => x.PatientInstructions).HasMaxLength(2000); b.Property(x => x.PostVisitReason).HasMaxLength(1000);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.MedicalEncounterId).IsUnique().HasFilter("[Status] = 1").HasDatabaseName("UX_LabRequests_EncounterDraft");
        b.HasIndex(x => new { x.DoctorId, x.Status }); b.HasIndex(x => new { x.PatientId, x.Status }); b.HasIndex(x => new { x.DoctorPracticeId, x.Status }); b.HasIndex(x => x.RequestedAtUtc);
        b.HasOne<MedicalEncounter>().WithMany().HasForeignKey(x => x.MedicalEncounterId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Doctor>().WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Patient>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<DoctorPractice>().WithMany().HasForeignKey(x => x.DoctorPracticeId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.ClientCascade);
        b.Navigation(x => x.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Ignore(x => x.History);
    }
}
internal sealed class LabRequestHistoryConfiguration : IWriteEntityConfiguration<LabRequestHistory>
{
    public void ConfigureAggregate(EntityTypeBuilder<LabRequestHistory> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.ToTable("LabRequestHistories");
        b.Property(x => x.Action).HasMaxLength(100); b.Property(x => x.Reason).HasMaxLength(1000);
        b.Property(x => x.ActorType).HasMaxLength(40); b.HasIndex(x => new { x.ResourceId, x.OccurredAtUtc });

    }
}
internal sealed class LabRequestItemConfiguration : IWriteEntityConfiguration<LabRequestItem>
{
    public void ConfigureAggregate(EntityTypeBuilder<LabRequestItem> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.ToTable("LabRequestItems", t =>
        {
            t.HasCheckConstraint("CK_LabRequestItems_Source", "([Source] = 1 AND [CatalogId] IS NOT NULL AND [CatalogRequestId] IS NULL) OR ([Source] = 2 AND [CatalogId] IS NULL AND [CatalogRequestId] IS NOT NULL)");
            t.HasCheckConstraint("CK_LabRequestItems_State", "[Status] IN (1,2,3)");
        });
        b.Property(x => x.NameEnSnapshot).HasMaxLength(1000); b.Property(x => x.NameArSnapshot).HasMaxLength(1000); b.Property(x => x.NormalizedName).HasMaxLength(1000);
        b.Property(x => x.LoincCodeSnapshot).HasMaxLength(20); b.Property(x => x.DoctorInstructions).HasMaxLength(2000); b.Property(x => x.CancellationReason).HasMaxLength(1000);
        b.HasOne<LabTestCatalog>().WithMany().HasForeignKey(x => x.CatalogId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<LabCatalogRequest>().WithMany().HasForeignKey(x => x.CatalogRequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.RequestId); b.HasIndex(x => new { x.RequestId, x.CatalogId }).IsUnique().HasFilter("[CatalogId] IS NOT NULL");

    }
}
internal sealed class LabResultConfiguration : IWriteEntityConfiguration<LabResult>
{
    public void ConfigureAggregate(EntityTypeBuilder<LabResult> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.ToTable("LabResults"); b.Ignore(x => x.Current); b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne<LabRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.DoctorId, x.CreatedAtUtc }); b.HasIndex(x => new { x.PatientId, x.CreatedAtUtc }); b.HasIndex(x => x.DoctorPracticeId);
        b.HasIndex(x => x.AcceptedSubmissionId).IsUnique().HasFilter("[AcceptedSubmissionId] IS NOT NULL");
        b.HasMany(x => x.Versions).WithOne().HasForeignKey(x => x.ResultId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.Versions).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
        b.HasMany(x => x.History).WithOne().HasForeignKey(x => x.ResourceId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.History).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
internal sealed class LabResultHistoryConfiguration : IWriteEntityConfiguration<LabResultHistory>
{
    public void ConfigureAggregate(EntityTypeBuilder<LabResultHistory> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.ToTable("LabResultHistories");
        b.Property(x => x.Action).HasMaxLength(100); b.Property(x => x.Reason).HasMaxLength(1000);
        b.Property(x => x.ActorType).HasMaxLength(40); b.HasIndex(x => new { x.ResourceId, x.OccurredAtUtc });

    }
}
internal sealed class LabResultVersionConfiguration : IWriteEntityConfiguration<LabResultVersion>
{
    public void ConfigureAggregate(EntityTypeBuilder<LabResultVersion> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.ToTable("LabResultVersions", t => t.HasCheckConstraint("CK_LabResultVersions_State", "[Status] IN (1,2,3) AND [VersionNumber] > 0"));
        b.Property(x => x.ExternalProviderName).HasMaxLength(500); b.Property(x => x.CorrectionReason).HasMaxLength(1000); b.Property(x => x.VoidReason).HasMaxLength(1000);
        b.HasIndex(x => new { x.ResultId, x.VersionNumber }).IsUnique();
        b.HasIndex(x => x.ResultId).IsUnique().HasFilter("[Status] = 1").HasDatabaseName("UX_LabResultVersions_Current");
        b.HasMany(x => x.Coverage).WithOne().HasForeignKey(x => x.ResultVersionId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.Coverage).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
        b.HasMany(x => x.Attachments).WithOne().HasForeignKey(x => x.ResultVersionId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.Attachments).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
    }
}
internal sealed class LabResultCoverageConfiguration : IWriteEntityConfiguration<LabResultCoverage>
{
    public void ConfigureAggregate(EntityTypeBuilder<LabResultCoverage> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.ToTable("LabResultCoverages"); b.HasIndex(x => new { x.ResultVersionId, x.RequestItemId }).IsUnique();
        b.HasOne<LabRequestItem>().WithMany().HasForeignKey(x => x.RequestItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
internal sealed class PatientLabResultSubmissionConfiguration : IWriteEntityConfiguration<PatientLabResultSubmission>
{
    public void ConfigureAggregate(EntityTypeBuilder<PatientLabResultSubmission> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.ToTable("PatientLabResultSubmissions"); b.Property(x => x.RowVersion).IsRowVersion(); b.Property(x => x.ExternalProviderName).HasMaxLength(500);
        b.Property(x => x.PatientNote).HasMaxLength(2000); b.Property(x => x.PatientVisibleReason).HasMaxLength(1000);
        b.HasOne<LabRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<LabResult>().WithMany().HasForeignKey(x => x.AcceptedResultId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.DoctorId, x.Status, x.SubmittedAtUtc }); b.HasIndex(x => new { x.PatientId, x.SubmittedAtUtc }); b.HasIndex(x => x.DoctorPracticeId);
        b.HasMany(x => x.Attachments).WithOne().HasForeignKey(x => x.SubmissionId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.Attachments).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
        b.HasMany(x => x.History).WithOne().HasForeignKey(x => x.ResourceId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.History).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
internal sealed class PatientLabResultSubmissionHistoryConfiguration : IWriteEntityConfiguration<PatientLabResultSubmissionHistory>
{
    public void ConfigureAggregate(EntityTypeBuilder<PatientLabResultSubmissionHistory> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.ToTable("PatientLabResultSubmissionHistories");
        b.Property(x => x.Action).HasMaxLength(100); b.Property(x => x.Reason).HasMaxLength(1000);
        b.Property(x => x.ActorType).HasMaxLength(40); b.HasIndex(x => new { x.ResourceId, x.OccurredAtUtc });

    }
}
internal sealed class LabResultAttachmentConfiguration : IWriteEntityConfiguration<LabResultAttachment>
{
    public void ConfigureAggregate(EntityTypeBuilder<LabResultAttachment> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.ToTable("LabResultAttachments"); b.Property(x => x.PrivateMediaKey).HasMaxLength(500); b.Property(x => x.OriginalFileName).HasMaxLength(255);
        b.Property(x => x.ContentType).HasMaxLength(100); b.Property(x => x.Sha256).HasMaxLength(64);
    }
}
internal sealed class PatientLabResultSubmissionAttachmentConfiguration : IWriteEntityConfiguration<PatientLabResultSubmissionAttachment>
{
    public void ConfigureAggregate(EntityTypeBuilder<PatientLabResultSubmissionAttachment> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.ToTable("PatientLabResultSubmissionAttachments"); b.Property(x => x.PrivateMediaKey).HasMaxLength(500); b.Property(x => x.OriginalFileName).HasMaxLength(255);
        b.Property(x => x.ContentType).HasMaxLength(100); b.Property(x => x.Sha256).HasMaxLength(64);
    }
}
internal sealed class LabCatalogImportBatchConfiguration : IWriteEntityConfiguration<LabCatalogImportBatch>
{
    public void ConfigureAggregate(EntityTypeBuilder<LabCatalogImportBatch> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.ToTable("LabCatalogImportBatches"); b.Property(x => x.SourceVersion).HasMaxLength(30); b.Property(x => x.OriginalFileName).HasMaxLength(255);
        b.Property(x => x.FileSha256).HasMaxLength(64); b.Property(x => x.RowVersion).IsRowVersion(); b.HasIndex(x => x.UploadedAtUtc);
        b.HasMany(x => x.Records).WithOne().HasForeignKey(x => x.ImportBatchId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.Records).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
internal sealed class LabCatalogImportRecordConfiguration : IWriteEntityConfiguration<LabCatalogImportRecord>
{
    public void ConfigureAggregate(EntityTypeBuilder<LabCatalogImportRecord> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.ToTable("LabCatalogImportRecords"); b.Property(x => x.LoincCode).HasMaxLength(20); b.Property(x => x.NameEn).HasMaxLength(1000); b.Property(x => x.SourceHash).HasMaxLength(64);
        b.HasIndex(x => new { x.ImportBatchId, x.LoincCode }).IsUnique(); b.HasIndex(x => new { x.ImportBatchId, x.Disposition });
    }
}
internal sealed class RadiologyProcedureCatalogConfiguration : IWriteEntityConfiguration<RadiologyProcedureCatalog>
{
    public void ConfigureAggregate(EntityTypeBuilder<RadiologyProcedureCatalog> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.ToTable("RadiologyProcedureCatalogs", t =>
        {
            t.HasCheckConstraint("CK_RadiologyProcedureCatalog_Source", "([Source] = 1 AND [LoincCode] IS NOT NULL) OR ([Source] = 2 AND [LoincCode] IS NULL)");
            t.HasCheckConstraint("CK_RadiologyProcedureCatalog_Merge", "([Status] = 3 AND [MergedIntoId] IS NOT NULL AND [MergedIntoId] <> [Id]) OR ([Status] IN (1,2) AND [MergedIntoId] IS NULL)");
        });
        b.Ignore(x => x.IsSelectable); b.Ignore(x => x.NameEn); b.Ignore(x => x.NameAr);
        b.Property(x => x.LoincCode).HasMaxLength(20); b.Property(x => x.OfficialNameEn).HasMaxLength(1000); b.Property(x => x.OfficialNameAr).HasMaxLength(1000);
        b.Property(x => x.DisplayNameEn).HasMaxLength(1000); b.Property(x => x.DisplayNameAr).HasMaxLength(1000);
        b.Property(x => x.AliasesEn).HasMaxLength(4000); b.Property(x => x.AliasesAr).HasMaxLength(4000); b.Property(x => x.InternalNote).HasMaxLength(2000);
        b.Property(x => x.NormalizedSearch).HasMaxLength(12000); b.Property(x => x.NormalizedName).HasMaxLength(1000);
        b.Property(x => x.SourceVersion).HasMaxLength(30); b.Property(x => x.SourceHash).HasMaxLength(64); b.Property(x => x.ExternalStatus).HasMaxLength(40);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.LoincCode).IsUnique().HasFilter("[LoincCode] IS NOT NULL").HasDatabaseName("UX_RadiologyProcedureCatalog_Loinc");
        b.HasIndex(x => new { x.Status, x.Source }); b.HasIndex(x => new { x.Status, x.IsCommonOrder });
        b.HasOne<RadiologyProcedureCatalog>().WithMany().HasForeignKey(x => x.MergedIntoId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.History).WithOne().HasForeignKey(x => x.ResourceId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.History).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
internal sealed class RadiologyProcedureCatalogHistoryConfiguration : IWriteEntityConfiguration<RadiologyProcedureCatalogHistory>
{
    public void ConfigureAggregate(EntityTypeBuilder<RadiologyProcedureCatalogHistory> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.ToTable("RadiologyProcedureCatalogHistories");
        b.Property(x => x.Action).HasMaxLength(100); b.Property(x => x.Reason).HasMaxLength(1000);
        b.Property(x => x.ActorType).HasMaxLength(40); b.HasIndex(x => new { x.ResourceId, x.OccurredAtUtc });

    }
}
internal sealed class RadiologyCatalogRequestConfiguration : IWriteEntityConfiguration<RadiologyCatalogRequest>
{
    public void ConfigureAggregate(EntityTypeBuilder<RadiologyCatalogRequest> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.ToTable("RadiologyCatalogRequests"); b.Property(x => x.Name).HasMaxLength(1000); b.Property(x => x.Specimen).HasMaxLength(500);
        b.Property(x => x.CatalogClarificationNote).HasMaxLength(2000); b.Property(x => x.ReviewReason).HasMaxLength(1000); b.Property(x => x.ReasonType).HasMaxLength(40);
        b.Property(x => x.RowVersion).IsRowVersion(); b.HasIndex(x => new { x.RequestedByDoctorId, x.Status });
        b.HasOne<Doctor>().WithMany().HasForeignKey(x => x.RequestedByDoctorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<RadiologyProcedureCatalog>().WithMany().HasForeignKey(x => x.CanonicalCatalogId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.History).WithOne().HasForeignKey(x => x.ResourceId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.History).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
internal sealed class RadiologyCatalogRequestHistoryConfiguration : IWriteEntityConfiguration<RadiologyCatalogRequestHistory>
{
    public void ConfigureAggregate(EntityTypeBuilder<RadiologyCatalogRequestHistory> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.ToTable("RadiologyCatalogRequestHistories");
        b.Property(x => x.Action).HasMaxLength(100); b.Property(x => x.Reason).HasMaxLength(1000);
        b.Property(x => x.ActorType).HasMaxLength(40); b.HasIndex(x => new { x.ResourceId, x.OccurredAtUtc });

    }
}
internal sealed class RadiologyRequestConfiguration : IWriteEntityConfiguration<RadiologyRequest>
{
    public void ConfigureAggregate(EntityTypeBuilder<RadiologyRequest> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.ToTable("RadiologyRequests", t =>
        {
            t.HasCheckConstraint("CK_RadiologyRequests_State", "[Status] IN (1,2,3,4,5) AND [Origin] IN (1,2)");
            t.HasCheckConstraint("CK_RadiologyRequests_PostVisit", "[Origin] = 1 OR ([PostVisitReason] IS NOT NULL AND [Status] <> 1)");
        });
        b.Property(x => x.PatientInstructions).HasMaxLength(2000); b.Property(x => x.PostVisitReason).HasMaxLength(1000);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.MedicalEncounterId).IsUnique().HasFilter("[Status] = 1").HasDatabaseName("UX_RadiologyRequests_EncounterDraft");
        b.HasIndex(x => new { x.DoctorId, x.Status }); b.HasIndex(x => new { x.PatientId, x.Status }); b.HasIndex(x => new { x.DoctorPracticeId, x.Status }); b.HasIndex(x => x.RequestedAtUtc);
        b.HasOne<MedicalEncounter>().WithMany().HasForeignKey(x => x.MedicalEncounterId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Doctor>().WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Patient>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<DoctorPractice>().WithMany().HasForeignKey(x => x.DoctorPracticeId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.ClientCascade);
        b.Navigation(x => x.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Ignore(x => x.History);
    }
}
internal sealed class RadiologyRequestHistoryConfiguration : IWriteEntityConfiguration<RadiologyRequestHistory>
{
    public void ConfigureAggregate(EntityTypeBuilder<RadiologyRequestHistory> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.ToTable("RadiologyRequestHistories");
        b.Property(x => x.Action).HasMaxLength(100); b.Property(x => x.Reason).HasMaxLength(1000);
        b.Property(x => x.ActorType).HasMaxLength(40); b.HasIndex(x => new { x.ResourceId, x.OccurredAtUtc });

    }
}
internal sealed class RadiologyRequestItemConfiguration : IWriteEntityConfiguration<RadiologyRequestItem>
{
    public void ConfigureAggregate(EntityTypeBuilder<RadiologyRequestItem> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.ToTable("RadiologyRequestItems", t =>
        {
            t.HasCheckConstraint("CK_RadiologyRequestItems_Source", "([Source] = 1 AND [CatalogId] IS NOT NULL AND [CatalogRequestId] IS NULL) OR ([Source] = 2 AND [CatalogId] IS NULL AND [CatalogRequestId] IS NOT NULL)");
            t.HasCheckConstraint("CK_RadiologyRequestItems_State", "[Status] IN (1,2,3)");
        });
        b.Property(x => x.NameEnSnapshot).HasMaxLength(1000); b.Property(x => x.NameArSnapshot).HasMaxLength(1000); b.Property(x => x.NormalizedName).HasMaxLength(1000);
        b.Property(x => x.LoincCodeSnapshot).HasMaxLength(20); b.Property(x => x.DoctorInstructions).HasMaxLength(2000); b.Property(x => x.CancellationReason).HasMaxLength(1000);
        b.HasOne<RadiologyProcedureCatalog>().WithMany().HasForeignKey(x => x.CatalogId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<RadiologyCatalogRequest>().WithMany().HasForeignKey(x => x.CatalogRequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.RequestId); b.HasIndex(x => new { x.RequestId, x.CatalogId }).IsUnique().HasFilter("[CatalogId] IS NOT NULL");

    }
}
internal sealed class RadiologyResultConfiguration : IWriteEntityConfiguration<RadiologyResult>
{
    public void ConfigureAggregate(EntityTypeBuilder<RadiologyResult> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.ToTable("RadiologyResults"); b.Ignore(x => x.Current); b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne<RadiologyRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.DoctorId, x.CreatedAtUtc }); b.HasIndex(x => new { x.PatientId, x.CreatedAtUtc }); b.HasIndex(x => x.DoctorPracticeId);
        b.HasIndex(x => x.AcceptedSubmissionId).IsUnique().HasFilter("[AcceptedSubmissionId] IS NOT NULL");
        b.HasMany(x => x.Versions).WithOne().HasForeignKey(x => x.ResultId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.Versions).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
        b.HasMany(x => x.History).WithOne().HasForeignKey(x => x.ResourceId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.History).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
internal sealed class RadiologyResultHistoryConfiguration : IWriteEntityConfiguration<RadiologyResultHistory>
{
    public void ConfigureAggregate(EntityTypeBuilder<RadiologyResultHistory> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.ToTable("RadiologyResultHistories");
        b.Property(x => x.Action).HasMaxLength(100); b.Property(x => x.Reason).HasMaxLength(1000);
        b.Property(x => x.ActorType).HasMaxLength(40); b.HasIndex(x => new { x.ResourceId, x.OccurredAtUtc });

    }
}
internal sealed class RadiologyResultVersionConfiguration : IWriteEntityConfiguration<RadiologyResultVersion>
{
    public void ConfigureAggregate(EntityTypeBuilder<RadiologyResultVersion> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.ToTable("RadiologyResultVersions", t => t.HasCheckConstraint("CK_RadiologyResultVersions_State", "[Status] IN (1,2,3) AND [VersionNumber] > 0"));
        b.Property(x => x.ExternalProviderName).HasMaxLength(500); b.Property(x => x.CorrectionReason).HasMaxLength(1000); b.Property(x => x.VoidReason).HasMaxLength(1000);
        b.HasIndex(x => new { x.ResultId, x.VersionNumber }).IsUnique();
        b.HasIndex(x => x.ResultId).IsUnique().HasFilter("[Status] = 1").HasDatabaseName("UX_RadiologyResultVersions_Current");
        b.HasMany(x => x.Coverage).WithOne().HasForeignKey(x => x.ResultVersionId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.Coverage).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
        b.HasMany(x => x.Attachments).WithOne().HasForeignKey(x => x.ResultVersionId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.Attachments).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
    }
}
internal sealed class RadiologyResultCoverageConfiguration : IWriteEntityConfiguration<RadiologyResultCoverage>
{
    public void ConfigureAggregate(EntityTypeBuilder<RadiologyResultCoverage> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.ToTable("RadiologyResultCoverages"); b.HasIndex(x => new { x.ResultVersionId, x.RequestItemId }).IsUnique();
        b.HasOne<RadiologyRequestItem>().WithMany().HasForeignKey(x => x.RequestItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
internal sealed class PatientRadiologyResultSubmissionConfiguration : IWriteEntityConfiguration<PatientRadiologyResultSubmission>
{
    public void ConfigureAggregate(EntityTypeBuilder<PatientRadiologyResultSubmission> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.ToTable("PatientRadiologyResultSubmissions"); b.Property(x => x.RowVersion).IsRowVersion(); b.Property(x => x.ExternalProviderName).HasMaxLength(500);
        b.Property(x => x.PatientNote).HasMaxLength(2000); b.Property(x => x.PatientVisibleReason).HasMaxLength(1000);
        b.HasOne<RadiologyRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<RadiologyResult>().WithMany().HasForeignKey(x => x.AcceptedResultId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.DoctorId, x.Status, x.SubmittedAtUtc }); b.HasIndex(x => new { x.PatientId, x.SubmittedAtUtc }); b.HasIndex(x => x.DoctorPracticeId);
        b.HasMany(x => x.Attachments).WithOne().HasForeignKey(x => x.SubmissionId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.Attachments).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
        b.HasMany(x => x.History).WithOne().HasForeignKey(x => x.ResourceId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.History).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
internal sealed class PatientRadiologyResultSubmissionHistoryConfiguration : IWriteEntityConfiguration<PatientRadiologyResultSubmissionHistory>
{
    public void ConfigureAggregate(EntityTypeBuilder<PatientRadiologyResultSubmissionHistory> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.ToTable("PatientRadiologyResultSubmissionHistories");
        b.Property(x => x.Action).HasMaxLength(100); b.Property(x => x.Reason).HasMaxLength(1000);
        b.Property(x => x.ActorType).HasMaxLength(40); b.HasIndex(x => new { x.ResourceId, x.OccurredAtUtc });

    }
}
internal sealed class RadiologyResultAttachmentConfiguration : IWriteEntityConfiguration<RadiologyResultAttachment>
{
    public void ConfigureAggregate(EntityTypeBuilder<RadiologyResultAttachment> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.ToTable("RadiologyResultAttachments"); b.Property(x => x.PrivateMediaKey).HasMaxLength(500); b.Property(x => x.OriginalFileName).HasMaxLength(255);
        b.Property(x => x.ContentType).HasMaxLength(100); b.Property(x => x.Sha256).HasMaxLength(64);
    }
}
internal sealed class PatientRadiologyResultSubmissionAttachmentConfiguration : IWriteEntityConfiguration<PatientRadiologyResultSubmissionAttachment>
{
    public void ConfigureAggregate(EntityTypeBuilder<PatientRadiologyResultSubmissionAttachment> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.ToTable("PatientRadiologyResultSubmissionAttachments"); b.Property(x => x.PrivateMediaKey).HasMaxLength(500); b.Property(x => x.OriginalFileName).HasMaxLength(255);
        b.Property(x => x.ContentType).HasMaxLength(100); b.Property(x => x.Sha256).HasMaxLength(64);
    }
}
internal sealed class RadiologyCatalogImportBatchConfiguration : IWriteEntityConfiguration<RadiologyCatalogImportBatch>
{
    public void ConfigureAggregate(EntityTypeBuilder<RadiologyCatalogImportBatch> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.ToTable("RadiologyCatalogImportBatches"); b.Property(x => x.SourceVersion).HasMaxLength(30); b.Property(x => x.OriginalFileName).HasMaxLength(255);
        b.Property(x => x.FileSha256).HasMaxLength(64); b.Property(x => x.RowVersion).IsRowVersion(); b.HasIndex(x => x.UploadedAtUtc);
        b.HasMany(x => x.Records).WithOne().HasForeignKey(x => x.ImportBatchId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.Records).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
internal sealed class RadiologyCatalogImportRecordConfiguration : IWriteEntityConfiguration<RadiologyCatalogImportRecord>
{
    public void ConfigureAggregate(EntityTypeBuilder<RadiologyCatalogImportRecord> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.ToTable("RadiologyCatalogImportRecords"); b.Property(x => x.LoincCode).HasMaxLength(20); b.Property(x => x.NameEn).HasMaxLength(1000); b.Property(x => x.SourceHash).HasMaxLength(64);
        b.HasIndex(x => new { x.ImportBatchId, x.LoincCode }).IsUnique(); b.HasIndex(x => new { x.ImportBatchId, x.Disposition });
    }
}
