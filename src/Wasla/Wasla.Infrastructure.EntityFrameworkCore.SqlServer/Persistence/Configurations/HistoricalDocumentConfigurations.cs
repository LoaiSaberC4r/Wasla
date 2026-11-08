using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.Domain.Patients;
using Wasla.Domain.Security;
namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Configurations;
internal sealed class HistoricalDocumentConfiguration : IWriteEntityConfiguration<PatientHistoricalDocument>
{
    public void ConfigureAggregate(EntityTypeBuilder<PatientHistoricalDocument> b)
    {
        b.ToTable("PatientHistoricalDocuments", t => { t.HasCheckConstraint("CK_HistoricalDocument_State", "[State] IN (1,2,3,4,5)"); t.HasCheckConstraint("CK_HistoricalDocument_Version", "[CurrentVersionNumber] >= 0"); });
        b.HasKey(d => d.Id); b.Property(d => d.Id).ValueGeneratedNever(); b.Property(d => d.RowVersion).IsRowVersion();
        b.HasOne<Patient>().WithMany().HasForeignKey(d => d.SubjectPatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Patient>().WithMany().HasForeignKey(d => d.UploaderPatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(d => d.UploadedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(d => new { d.SubjectPatientId, d.State, d.UploadedAtUtc, d.Id });
    }
}
internal sealed class HistoricalDocumentVersionConfiguration : IWriteEntityConfiguration<PatientHistoricalDocumentVersion>
{
    public void ConfigureAggregate(EntityTypeBuilder<PatientHistoricalDocumentVersion> b)
    {
        b.ToTable("PatientHistoricalDocumentVersions", t => {
            t.HasCheckConstraint("CK_HistoricalVersion_Category", "[Category] IN (1,2,3,4,5)");
            t.HasCheckConstraint("CK_HistoricalVersion_Date", "([DateUnknown] = 1 AND [DocumentDate] IS NULL) OR ([DateUnknown] = 0 AND [DocumentDate] IS NOT NULL)");
            t.HasCheckConstraint("CK_HistoricalVersion_Number", "[VersionNumber] > 0"); });
        b.HasKey(v => v.Id); b.Property(v => v.Id).ValueGeneratedNever();
        b.Property(v => v.Title).HasMaxLength(200); b.Property(v => v.ProviderName).HasMaxLength(200); b.Property(v => v.PatientNote).HasMaxLength(2000); b.Property(v => v.Reason).HasMaxLength(1000);
        b.HasOne<PatientHistoricalDocument>().WithMany().HasForeignKey(v => v.DocumentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(v => v.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(v => new { v.DocumentId, v.VersionNumber }).IsUnique();
    }
}
internal sealed class HistoricalDocumentAttachmentConfiguration : IWriteEntityConfiguration<PatientHistoricalDocumentAttachment>
{
    public void ConfigureAggregate(EntityTypeBuilder<PatientHistoricalDocumentAttachment> b)
    {
        b.ToTable("PatientHistoricalDocumentAttachments", t => t.HasCheckConstraint("CK_HistoricalAttachment_Length", "[Length] > 0 AND [Length] <= 10485760"));
        b.HasKey(a => a.Id); b.Property(a => a.Id).ValueGeneratedNever(); b.Property(a => a.MediaKey).HasMaxLength(1000).IsRequired();
        b.Property(a => a.FileName).HasMaxLength(500).IsRequired(); b.Property(a => a.ContentType).HasMaxLength(100).IsRequired(); b.Property(a => a.Sha256).HasMaxLength(64).IsRequired();
        b.HasOne<PatientHistoricalDocumentVersion>().WithMany().HasForeignKey(a => a.VersionId).OnDelete(DeleteBehavior.Restrict); b.HasIndex(a => a.VersionId);
    }
}
internal sealed class HistoricalDocumentHistoryConfiguration : IWriteEntityConfiguration<PatientHistoricalDocumentHistory>
{
    public void ConfigureAggregate(EntityTypeBuilder<PatientHistoricalDocumentHistory> b)
    {
        b.ToTable("PatientHistoricalDocumentHistories"); b.HasKey(h => h.Id); b.Property(h => h.Id).ValueGeneratedNever(); b.Property(h => h.Action).HasMaxLength(40).IsRequired();
        b.HasOne<PatientHistoricalDocument>().WithMany().HasForeignKey(h => h.DocumentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(h => h.ActorUserId).OnDelete(DeleteBehavior.Restrict); b.HasIndex(h => new { h.DocumentId, h.OccurredAtUtc, h.Id });
    }
}
internal sealed class HistoricalDocumentOperationConfiguration : IWriteEntityConfiguration<HistoricalDocumentOperation>
{
    public void ConfigureAggregate(EntityTypeBuilder<HistoricalDocumentOperation> b)
    {
        b.ToTable("HistoricalDocumentOperations"); b.HasKey(o => o.Id); b.Property(o => o.Id).ValueGeneratedNever();
        b.Property(o => o.Operation).HasMaxLength(20).IsRequired(); b.Property(o => o.IdempotencyKey).HasMaxLength(100).IsRequired(); b.Property(o => o.Fingerprint).HasMaxLength(64).IsRequired();
        b.HasOne<PatientHistoricalDocument>().WithMany().HasForeignKey(o => o.DocumentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Patient>().WithMany().HasForeignKey(o => o.SubjectPatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(o => o.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(o => new { o.ActorUserId, o.Operation, o.IdempotencyKey }).IsUnique(); b.HasIndex(o => new { o.ActorUserId, o.CreatedAtUtc });
    }
}
