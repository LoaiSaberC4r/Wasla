using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.Domain.Families;
using Wasla.Domain.Patients;
using Wasla.Domain.Security;
namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Configurations;
internal sealed class DependentProfileChangeConfiguration : IWriteEntityConfiguration<DependentProfileChange>
{
    public void ConfigureAggregate(EntityTypeBuilder<DependentProfileChange> b)
    {
        b.ToTable("DependentProfileChanges"); b.HasKey(r => r.Id); b.Property(r => r.Id).ValueGeneratedNever();
        b.Property(r => r.Operation).HasMaxLength(30); b.Property(r => r.IdempotencyKey).HasMaxLength(100); b.Property(r => r.Fingerprint).HasMaxLength(64);
        b.HasIndex(r => new { r.ActorUserId, r.Operation, r.IdempotencyKey }).IsUnique();
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Patient>().WithMany().HasForeignKey(r => r.ParentPatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Patient>().WithMany().HasForeignKey(r => r.ChildPatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PatientContact>().WithMany().HasForeignKey(r => r.ContactId).OnDelete(DeleteBehavior.Restrict);
    }
}
