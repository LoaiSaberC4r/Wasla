using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.Domain.Families;
using Wasla.Domain.Patients;
using Wasla.Domain.Security;
namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Configurations;
internal sealed class FamilyJoinInvitationConfiguration : IWriteEntityConfiguration<FamilyJoinInvitation>
{
    public void ConfigureAggregate(EntityTypeBuilder<FamilyJoinInvitation> b)
    {
        b.ToTable("FamilyJoinInvitations", t => {
            t.HasCheckConstraint("CK_FamilyJoinInvitation_Role", "[IntendedRole] IN (1,2)");
            t.HasCheckConstraint("CK_FamilyJoinInvitation_Expiry", "[ExpiresAtUtc] > [CreatedAtUtc]");
            t.HasCheckConstraint("CK_FamilyJoinInvitation_Consumed", "([ConsumedAtUtc] IS NULL AND [RedeemedByUserId] IS NULL AND [RelationshipRequestId] IS NULL AND [GovernanceCaseId] IS NULL) OR ([ConsumedAtUtc] IS NOT NULL AND [RedeemedByUserId] IS NOT NULL AND (([RelationshipRequestId] IS NOT NULL AND [GovernanceCaseId] IS NULL) OR ([RelationshipRequestId] IS NULL AND [GovernanceCaseId] IS NOT NULL)))");
        });
        b.HasKey(i => i.Id); b.Property(i => i.Id).ValueGeneratedNever(); b.Property(i => i.RowVersion).IsRowVersion();
        b.Property(i => i.TokenHash).HasMaxLength(64).IsRequired(); b.Property(i => i.Fingerprint).HasMaxLength(64).IsRequired();
        b.Property(i => i.IdempotencyKey).HasMaxLength(100).IsRequired(); b.Property(i => i.RedemptionKey).HasMaxLength(100); b.Property(i => i.RedemptionFingerprint).HasMaxLength(64);
        b.Property(i => i.RevocationKey).HasMaxLength(100);
        b.HasIndex(i => new { i.IssuedByUserId, i.RevocationKey }).IsUnique().HasFilter("[RevocationKey] IS NOT NULL");
        b.Property(i => i.ProtectedReference).HasMaxLength(2000).IsRequired();
        b.HasIndex(i => i.TokenHash).IsUnique(); b.HasIndex(i => new { i.IssuedByUserId, i.IdempotencyKey }).IsUnique();
        b.HasIndex(i => new { i.RedeemedByUserId, i.RedemptionKey }).IsUnique().HasFilter("[RedemptionKey] IS NOT NULL");
        b.HasIndex(i => new { i.IssuedByUserId, i.CreatedAtUtc });
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(i => i.IssuedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(i => i.RedeemedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Patient>().WithMany().HasForeignKey(i => i.IssuerPatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Patient>().WithMany().HasForeignKey(i => i.ChildPatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Family>().WithMany().HasForeignKey(i => i.FamilyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ParentChildOperationalAuthority>().WithMany().HasForeignKey(i => i.OperationalAuthorityId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FamilyRelationshipRequest>().WithMany().HasForeignKey(i => i.RelationshipRequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FamilyGovernanceCase>().WithMany().HasForeignKey(i => i.GovernanceCaseId).OnDelete(DeleteBehavior.Restrict);
    }
}
