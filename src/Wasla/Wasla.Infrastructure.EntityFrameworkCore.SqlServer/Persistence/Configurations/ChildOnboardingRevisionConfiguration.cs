using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.Domain.Families;
namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Configurations;
internal sealed class ChildOnboardingRevisionConfiguration : IWriteEntityConfiguration<ChildOnboardingRevision>
{
    public void ConfigureAggregate(EntityTypeBuilder<ChildOnboardingRevision> b)
    {
        b.ToTable("ChildOnboardingRevisions"); b.HasKey(r => r.Id); b.Property(r => r.Id).ValueGeneratedNever();
        b.Property(r => r.NameAr).HasMaxLength(200).IsRequired(); b.Property(r => r.NameEn).HasMaxLength(200); b.Property(r => r.PhoneNumber).HasMaxLength(30);
        b.Property(r => r.ContactNameAr).HasMaxLength(200).IsRequired(); b.Property(r => r.ContactPhone).HasMaxLength(30).IsRequired();
        b.Property(r => r.ResubmitIdempotencyKey).HasMaxLength(100); b.Property(r => r.ResubmitFingerprint).HasMaxLength(64); b.Property(r => r.ResultRowVersion).HasMaxLength(40).IsRequired();
        b.HasIndex(r => new { r.RequestId, r.ResubmitIdempotencyKey }).IsUnique().HasFilter("[ResubmitIdempotencyKey] IS NOT NULL");
        b.HasOne<ChildOnboardingRequest>().WithMany().HasForeignKey(r => r.RequestId).OnDelete(DeleteBehavior.Restrict); b.HasIndex(r => new { r.RequestId, r.Revision }).IsUnique();
    }
}
