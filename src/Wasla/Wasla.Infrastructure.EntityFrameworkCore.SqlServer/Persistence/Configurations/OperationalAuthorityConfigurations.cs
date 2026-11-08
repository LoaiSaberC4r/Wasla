using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.Domain.Families;
using Wasla.Domain.Patients;
using Wasla.Domain.Security;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Configurations;

internal sealed class OperationalAuthorityConfiguration : IWriteEntityConfiguration<ParentChildOperationalAuthority>
{
    public void ConfigureAggregate(EntityTypeBuilder<ParentChildOperationalAuthority> b)
    {
        b.ToTable("ParentChildOperationalAuthorities", t => {
            t.HasCheckConstraint("CK_OperationalAuthority_Distinct", "[ParentPatientId] <> [ChildPatientId] AND [ParentFamilyMemberId] <> [ChildFamilyMemberId]");
            t.HasCheckConstraint("CK_OperationalAuthority_Role", "[ParentRole] IN (1,2,5)");
            t.HasCheckConstraint("CK_OperationalAuthority_Revision", "[ApprovedRevisionNumber] >= 1");
        });
        b.HasKey(p => p.Id); b.Property(p => p.Id).ValueGeneratedNever(); b.Property(p => p.RowVersion).IsRowVersion();
        b.Property(p => p.RevocationReason).HasMaxLength(1000);
        b.Property(p => p.RevocationIdempotencyKey).HasMaxLength(100);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(p => p.RevokedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Family>().WithMany().HasForeignKey(p => p.FamilyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Patient>().WithMany().HasForeignKey(p => p.ParentPatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Patient>().WithMany().HasForeignKey(p => p.ChildPatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FamilyMember>().WithMany().HasForeignKey(p => p.ParentFamilyMemberId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FamilyMember>().WithMany().HasForeignKey(p => p.ChildFamilyMemberId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FamilyRelationshipRequest>().WithMany().HasForeignKey(p => p.ApprovedRelationshipRequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(p => p.ReviewedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(p => new { p.FamilyId, p.ParentPatientId, p.ChildPatientId }).IsUnique().HasFilter("[IsActive] = 1").HasDatabaseName("UX_OperationalAuthority_ActivePair");
        b.HasIndex(p => p.ApprovedRelationshipRequestId).IsUnique();
    }
}
internal sealed class OperationalAuthorityHistoryConfiguration : IWriteEntityConfiguration<OperationalAuthorityHistory>
{
    public void ConfigureAggregate(EntityTypeBuilder<OperationalAuthorityHistory> b)
    {
        b.ToTable("OperationalAuthorityHistories"); b.HasKey(p => p.Id); b.Property(p => p.Id).ValueGeneratedNever();
        b.Property(p => p.Action).HasMaxLength(50);
        b.HasOne<ParentChildOperationalAuthority>().WithMany().HasForeignKey(p => p.AuthorityId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(p => p.ActorApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(p => new { p.AuthorityId, p.OccurredAtUtc, p.Id });
    }
}
