using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.Domain.Families;
using Wasla.Domain.Patients;
using Wasla.Domain.Security;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Configurations;

internal sealed class VerifiedParentChildRelationshipConfiguration : IWriteEntityConfiguration<VerifiedParentChildRelationship>
{
    public void ConfigureAggregate(EntityTypeBuilder<VerifiedParentChildRelationship> b)
    {
        b.ToTable("VerifiedParentChildRelationships", t => {
            t.HasCheckConstraint("CK_VerifiedParentChild_Distinct", "[ParentPatientId] <> [ChildPatientId]");
            t.HasCheckConstraint("CK_VerifiedParentChild_Role", "[ParentRole] IN (1,2)");
            t.HasCheckConstraint("CK_VerifiedParentChild_Status", "[Status] IN (1,2)");
            t.HasCheckConstraint("CK_VerifiedParentChild_Revision", "[ApprovedRevisionNumber] >= 1");
        });
        b.HasKey(r => r.Id); b.Property(r => r.Id).ValueGeneratedNever();
        b.Property(r => r.RowVersion).IsRowVersion();
        b.Property(r => r.PolicyVersion).HasMaxLength(100).IsRequired();
        b.HasOne<Family>().WithMany().HasForeignKey(r => r.FamilyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Patient>().WithMany().HasForeignKey(r => r.ParentPatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Patient>().WithMany().HasForeignKey(r => r.ChildPatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FamilyMember>().WithMany().HasForeignKey(r => r.ParentFamilyMemberId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FamilyMember>().WithMany().HasForeignKey(r => r.ChildFamilyMemberId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FamilyRelationshipRequest>().WithMany().HasForeignKey(r => r.ApprovedRelationshipRequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.VerifiedBySuperAdminApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(r => new { r.FamilyId, r.ParentPatientId, r.ChildPatientId }).IsUnique().HasFilter("[Status] = 1");
        b.HasIndex(r => new { r.ParentPatientId, r.FamilyId, r.Status });
        b.HasIndex(r => new { r.ChildPatientId, r.FamilyId, r.Status });
    }
}
