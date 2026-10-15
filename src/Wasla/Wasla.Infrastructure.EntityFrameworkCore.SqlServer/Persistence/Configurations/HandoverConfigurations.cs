using Wasla.Domain.Archives;
using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.Domain.Operations;
using Wasla.Domain.Practices;
using Wasla.Domain.Reservations;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Configurations;

internal sealed class ReceptionHandoverConfiguration : IWriteEntityConfiguration<ReceptionHandover>
{
    public void ConfigureAggregate(EntityTypeBuilder<ReceptionHandover> b)
    {
        b.ToTable("ReceptionHandovers", t => { t.UseSqlOutputClause(false); t.HasCheckConstraint("CK_Handover_Status", "[Status] IN (1,2,3)"); });
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.DoctorPracticeId, x.Id }); b.Property(x => x.RowVersion).IsRowVersion();
        b.Property(x => x.Summary).HasMaxLength(12000); b.Property(x => x.ClosureOutcome).HasMaxLength(12000);
        b.HasOne<DoctorPractice>().WithMany().HasForeignKey(x => new { x.DoctorId, x.DoctorPracticeId })
            .HasPrincipalKey(x => new { x.DoctorId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ReservationRegistry>().WithMany().HasForeignKey(x => new { x.DoctorPracticeId, x.ReservationId })
            .HasPrincipalKey(x => new { x.DoctorPracticeId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<TicketRegistry>().WithMany().HasForeignKey(x => new { x.DoctorPracticeId, x.TicketId })
            .HasPrincipalKey(x => new { x.DoctorPracticeId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ReceptionHandover>().WithMany().HasForeignKey(x => new { x.DoctorPracticeId, x.PreviousHandoverId })
            .HasPrincipalKey(x => new { x.DoctorPracticeId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AcknowledgedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ClosedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.History).WithOne().HasForeignKey(x => x.HandoverId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.History).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasIndex(x => new { x.DoctorPracticeId, x.Status, x.CreatedOnUtc });
    }
}
internal sealed class ReceptionHandoverHistoryConfiguration : IWriteEntityConfiguration<ReceptionHandoverHistory>
{
    public void ConfigureAggregate(EntityTypeBuilder<ReceptionHandoverHistory> b)
    {
        b.ToTable("ReceptionHandoverHistories", t => t.UseSqlOutputClause(false)); b.HasKey(x => x.Id);
        b.Property(x => x.Operation).HasMaxLength(50); b.Property(x => x.Note).HasMaxLength(12000);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.HandoverId, x.SequenceNumber }).IsUnique();
    }
}
