using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.Domain.Families;
using Wasla.Domain.Patients;
using Wasla.Domain.Security;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Configurations;
internal sealed class FamilyCandidateConfiguration : IWriteEntityConfiguration<FamilyCandidateSelection>
{
    public void ConfigureAggregate(EntityTypeBuilder<FamilyCandidateSelection> b)
    {
        b.ToTable("FamilyCandidateSelections"); b.HasKey(s => s.Id); b.Property(s => s.Id).ValueGeneratedNever();
        b.Property(s => s.TokenHash).HasMaxLength(64).IsRequired(); b.Property(s => s.CriteriaHash).HasMaxLength(64).IsRequired();
        b.Property(s => s.Purpose).HasMaxLength(50).IsRequired(); b.Property(s => s.IdempotencyKey).HasMaxLength(100);
        b.Property(s => s.SubmissionFingerprint).HasMaxLength(64); b.Property(s => s.RowVersion).IsRowVersion();
        b.HasIndex(s => s.TokenHash).IsUnique(); b.HasIndex(s => new { s.ActorApplicationUserId, s.ExpiresAtUtc });
        b.HasIndex(s => new { s.ActorApplicationUserId, s.IdempotencyKey }).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(s => s.ActorApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Patient>().WithMany().HasForeignKey(s => s.ParentPatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Patient>().WithMany().HasForeignKey(s => s.TargetPatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FamilyGovernanceCase>().WithMany().HasForeignKey(s => s.GovernanceCaseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FamilyRelationshipRequest>().WithMany().HasForeignKey(s => s.RelationshipRequestId).OnDelete(DeleteBehavior.Restrict);
    }
}
