using Wasla.Domain.Archives;
using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.Domain.Clinical;
using Wasla.Domain.Security;
using Wasla.Domain.Doctors;
using Wasla.Domain.Patients;
using Wasla.Domain.Practices;
using Wasla.Domain.Tickets;

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

internal sealed class MedicalRecordAccessAuditConfiguration : IWriteEntityConfiguration<MedicalRecordAccessAudit>
{
    public void ConfigureAggregate(EntityTypeBuilder<MedicalRecordAccessAudit> b)
    {
        b.ToTable("MedicalRecordAccessAudits"); b.HasKey(a => a.Id); b.Property(a => a.Id).ValueGeneratedNever();
        b.Property(a => a.ActorCategory).HasMaxLength(30); b.Property(a => a.Action).HasMaxLength(50);
        b.Property(a => a.ResourceType).HasMaxLength(50); b.Property(a => a.AccessDecision).HasMaxLength(20);
        b.Property(a => a.AccessReasonCode).HasMaxLength(30); b.Property(a => a.CorrelationId).HasMaxLength(100);
        b.Property(a => a.OccurredAtUtc).HasColumnType("datetime2(3)");
        b.HasOne<Patient>().WithMany().HasForeignKey(a => a.PatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.ActorApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(a => new { a.PatientId, a.OccurredAtUtc, a.Id }).IsDescending(false, true, false);
        b.HasIndex(a => new { a.ActorApplicationUserId, a.OccurredAtUtc }).IsDescending(false, true);
    }
}

internal sealed class HiddenEncounterAccessRequestConfiguration : IWriteEntityConfiguration<HiddenEncounterAccessRequest>
{
    public void ConfigureAggregate(EntityTypeBuilder<HiddenEncounterAccessRequest> b)
    {
        b.ToTable("HiddenEncounterAccessRequests", t =>
        {
            t.HasCheckConstraint("CK_HiddenAccessRequests_Status", "[Status] IN (1,2,3,4,5)");
            t.HasCheckConstraint("CK_HiddenAccessRequests_Reason", "LEN(LTRIM(RTRIM([Reason]))) > 0");
            t.HasCheckConstraint("CK_HiddenAccessRequests_Actors", "[OwningDoctorId] <> [RequestingDoctorId]");
        });
        b.HasKey(r => r.Id); b.Property(r => r.Id).ValueGeneratedNever(); b.Property(r => r.Reason).HasMaxLength(1000).IsRequired();
        b.Property(r => r.RowVersion).IsRowVersion(); b.Property(r => r.RequestedAtUtc).HasColumnType("datetime2(3)");
        b.Property(r => r.DecidedAtUtc).HasColumnType("datetime2(3)"); b.Property(r => r.RevokedAtUtc).HasColumnType("datetime2(3)");
        b.HasOne<MedicalEncounter>().WithMany().HasForeignKey(r => r.HiddenEncounterId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<MedicalEncounter>().WithMany().HasForeignKey(r => r.RequestingEncounterId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<TicketRegistry>().WithMany().HasForeignKey(r => r.RequestingTicketId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Doctor>().WithMany().HasForeignKey(r => r.OwningDoctorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Doctor>().WithMany().HasForeignKey(r => r.RequestingDoctorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<DoctorPractice>().WithMany().HasForeignKey(r => r.RequestingPracticeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Patient>().WithMany().HasForeignKey(r => r.PatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.DecidedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.RevokedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(r => new { r.HiddenEncounterId, r.RequestingTicketId }).IsUnique().HasDatabaseName("UX_HiddenAccessRequests_Target_Ticket");
        b.HasIndex(r => new { r.OwningDoctorId, r.Status, r.RequestedAtUtc, r.Id }).IsDescending(false, false, true, false);
        b.HasIndex(r => new { r.RequestingDoctorId, r.RequestingTicketId, r.RequestedAtUtc }).IsDescending(false, false, true);
        b.HasMany(r => r.History).WithOne().HasForeignKey(h => h.RequestId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(r => r.History).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
internal sealed class HiddenEncounterAccessRequestHistoryConfiguration : IWriteEntityConfiguration<HiddenEncounterAccessRequestHistory>
{
    public void ConfigureAggregate(EntityTypeBuilder<HiddenEncounterAccessRequestHistory> b)
    {
        b.ToTable("HiddenEncounterAccessRequestHistories"); b.HasKey(h => h.Id); b.Property(h => h.Id).ValueGeneratedNever();
        b.Property(h => h.Action).HasMaxLength(50); b.Property(h => h.OccurredAtUtc).HasColumnType("datetime2(3)");
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(h => h.ActorApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(h => new { h.RequestId, h.OccurredAtUtc, h.Id });
    }
}
