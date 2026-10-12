using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.Domain.Clinical;
using Wasla.Domain.Security;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Configurations;

internal sealed class EncounterVisibilityHistoryConfiguration : IWriteEntityConfiguration<EncounterVisibilityHistory>
{
    public void ConfigureAggregate(EntityTypeBuilder<EncounterVisibilityHistory> b)
    {
        b.ToTable("EncounterVisibilityHistories"); b.HasKey(h => h.Id); b.Property(h => h.Id).ValueGeneratedNever();
        b.Property(h => h.Source).HasMaxLength(50).IsRequired(); b.Property(h => h.OccurredAtUtc).HasColumnType("datetime2(3)");
        b.HasIndex(h => new { h.MedicalEncounterId, h.Revision }).IsUnique();
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(h => h.ActorApplicationUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
