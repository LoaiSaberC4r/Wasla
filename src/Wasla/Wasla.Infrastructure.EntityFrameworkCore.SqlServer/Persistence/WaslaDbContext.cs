using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Email;
using Wasla.Domain.Doctors;
using Wasla.Domain.Patients;
using Wasla.Domain.Security;
using Wasla.Domain.ReferenceData;
using Wasla.Domain.Families;
using Wasla.Domain.Practices;
using Wasla.Domain.Reservations;
using Wasla.Domain.Tickets;
using Wasla.Domain.Clinical;
using Wasla.Domain.Medications;
using Wasla.Domain.Payments;
using BuildingBlock.Infrastructure.Extensions;
using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

public sealed class WaslaDbContext(DbContextOptions<WaslaDbContext> options)
    : DbContext(options)
{
    public DbSet<EmailOutboxMessage> EmailOutboxMessages => Set<EmailOutboxMessage>();
    public DbSet<ApplicationUser> ApplicationUsers => Set<ApplicationUser>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<SuperAdmin> SuperAdmins => Set<SuperAdmin>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<DoctorQualification> DoctorQualifications => Set<DoctorQualification>();
    public DbSet<DoctorStatusHistory> DoctorStatusHistories => Set<DoctorStatusHistory>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<PatientAccountLink> PatientAccountLinks => Set<PatientAccountLink>();
    public DbSet<PatientContact> PatientContacts => Set<PatientContact>();
    public DbSet<Family> Families => Set<Family>();
    public DbSet<FamilyMember> FamilyMembers => Set<FamilyMember>();
    public DbSet<FamilyRelationshipRequest> FamilyRelationshipRequests => Set<FamilyRelationshipRequest>();
    public DbSet<FamilyRelationshipDocument> FamilyRelationshipDocuments => Set<FamilyRelationshipDocument>();
    public DbSet<FamilyRelationshipRequestHistory> FamilyRelationshipRequestHistories => Set<FamilyRelationshipRequestHistory>();
    public DbSet<PasswordResetChallenge> PasswordResetChallenges => Set<PasswordResetChallenge>();
    public DbSet<MedicalSpecialization> MedicalSpecializations => Set<MedicalSpecialization>();
    public DbSet<DoctorSpecialization> DoctorSpecializations => Set<DoctorSpecialization>();
    public DbSet<DoctorSpecializationRequest> DoctorSpecializationRequests => Set<DoctorSpecializationRequest>();
    public DbSet<DoctorSpecializationRequestRevision> DoctorSpecializationRequestRevisions => Set<DoctorSpecializationRequestRevision>();
    public DbSet<DoctorSpecializationRequestItem> DoctorSpecializationRequestItems => Set<DoctorSpecializationRequestItem>();
    public DbSet<DoctorSpecializationRequestHistory> DoctorSpecializationRequestHistories => Set<DoctorSpecializationRequestHistory>();
    public DbSet<Governorate> Governorates => Set<Governorate>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<Area> Areas => Set<Area>();
    public DbSet<DoctorPractice> DoctorPractices => Set<DoctorPractice>();
    public DbSet<DoctorPractice> DoctorPracticeLocations => Set<DoctorPractice>();
    public DbSet<DoctorPracticeConfiguration> DoctorPracticeConfigurations => Set<DoctorPracticeConfiguration>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<ReservationHistory> ReservationHistories => Set<ReservationHistory>();
    public DbSet<ReservationIdempotencyRecord> ReservationIdempotencyRecords => Set<ReservationIdempotencyRecord>();
    public DbSet<ReservationProjectionInvalidation> ReservationProjectionInvalidations
        => Set<ReservationProjectionInvalidation>();
    public DbSet<DrugCatalog> DrugCatalogs => Set<DrugCatalog>();
    public DbSet<DrugCatalogHistory> DrugCatalogHistories => Set<DrugCatalogHistory>();
    public DbSet<DrugCatalogRequest> DrugCatalogRequests => Set<DrugCatalogRequest>();
    public DbSet<DrugCatalogRequestHistory> DrugCatalogRequestHistories => Set<DrugCatalogRequestHistory>();
    public DbSet<DrugCatalogImportBatch> DrugCatalogImportBatches => Set<DrugCatalogImportBatch>();
    public DbSet<DrugCatalogImportRecord> DrugCatalogImportRecords => Set<DrugCatalogImportRecord>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<PrescriptionVersion> PrescriptionVersions => Set<PrescriptionVersion>();
    public DbSet<PrescriptionItem> PrescriptionItems => Set<PrescriptionItem>();
    public DbSet<PrescriptionAuditEvent> PrescriptionAuditEvents => Set<PrescriptionAuditEvent>();
    public DbSet<MedicationIdempotencyRecord> MedicationIdempotencyRecords => Set<MedicationIdempotencyRecord>();
    public DbSet<MedicalEncounter> MedicalEncounters => Set<MedicalEncounter>();
    public DbSet<Diagnosis> Diagnoses => Set<Diagnosis>();
    public DbSet<EncounterAmendment> EncounterAmendments => Set<EncounterAmendment>();
    public DbSet<EncounterAmendmentChange> EncounterAmendmentChanges => Set<EncounterAmendmentChange>();
    public DbSet<EncounterAuditEvent> EncounterAuditEvents => Set<EncounterAuditEvent>();
    public DbSet<FollowUpEligibility> FollowUpEligibilities => Set<FollowUpEligibility>();
    public DbSet<FollowUpEligibilityHistory> FollowUpEligibilityHistories => Set<FollowUpEligibilityHistory>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketHistory> TicketHistories => Set<TicketHistory>();
    public DbSet<TicketCallAttempt> TicketCallAttempts => Set<TicketCallAttempt>();
    public DbSet<TicketDailyCounter> TicketDailyCounters => Set<TicketDailyCounter>();
    public DbSet<TicketIdempotencyRecord> TicketIdempotencyRecords => Set<TicketIdempotencyRecord>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<PaymentCorrectionHistory> PaymentCorrectionHistories => Set<PaymentCorrectionHistory>();
    public DbSet<RefundCorrectionHistory> RefundCorrectionHistories => Set<RefundCorrectionHistory>();
    public DbSet<FinancialDailyCounter> FinancialDailyCounters => Set<FinancialDailyCounter>();
    public DbSet<FinancialIdempotencyRecord> FinancialIdempotencyRecords => Set<FinancialIdempotencyRecord>();
    public DbSet<DoctorPracticeBranding> DoctorPracticeBrandings => Set<DoctorPracticeBranding>();
    public DbSet<DoctorPracticeSchedulePeriod> DoctorPracticeSchedulePeriods => Set<DoctorPracticeSchedulePeriod>();
    public DbSet<DoctorPracticeScheduleException> DoctorPracticeScheduleExceptions => Set<DoctorPracticeScheduleException>();
    public DbSet<DoctorPracticeSegment> DoctorPracticeSegments => Set<DoctorPracticeSegment>();
    public DbSet<DoctorPracticeVisitType> DoctorPracticeVisitTypes => Set<DoctorPracticeVisitType>();
    public DbSet<DoctorPracticeSegmentVisitTypePrice> DoctorPracticeSegmentVisitTypePrices => Set<DoctorPracticeSegmentVisitTypePrice>();
    public DbSet<PublicPracticeAvailabilitySlot> PublicPracticeAvailabilitySlots => Set<PublicPracticeAvailabilitySlot>();
    public DbSet<PublicDoctorSearchRank> PublicDoctorSearchRanks => Set<PublicDoctorSearchRank>();
    public DbSet<Reception> Receptions => Set<Reception>();
    public DbSet<ReceptionPracticeAssignment> ReceptionPracticeAssignments => Set<ReceptionPracticeAssignment>();
    public DbSet<ReceptionPracticeAssignmentPermission> ReceptionPracticeAssignmentPermissions
        => Set<ReceptionPracticeAssignmentPermission>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
        => SaveChangesAsync(acceptAllChangesOnSuccess, CancellationToken.None).GetAwaiter().GetResult();

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        await SynchronizeFollowUpReleasesAsync(cancellationToken);
        await GuardCompletedDiagnosisDeletionAsync(cancellationToken);
        await GuardPrescriptionHistoryAsync(cancellationToken);
        EnsureFinancialHistoryIsAppendOnly();
        AddPrescriptionAuditEvents();
        PrepareSqliteRowVersions();
        // Release the filtered current-version key before acquiring it for the correction.
        var superseded = ChangeTracker.Entries<PrescriptionVersion>().Where(e => e.State == EntityState.Modified &&
            e.Property(v => v.Status).OriginalValue == PrescriptionVersionStatus.Finalized && e.Entity.Status == PrescriptionVersionStatus.Superseded).ToArray();
        await using var ownedTransaction = superseded.Length > 0 && Database.CurrentTransaction is null
            ? await Database.BeginTransactionAsync(cancellationToken) : null;
        foreach (var entry in superseded)
        {
            var affected = await Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [PrescriptionVersions] SET [Status] = 3 WHERE [Id] = {entry.Entity.Id} AND [Status] = 2", cancellationToken);
            if (affected != 1) throw new DbUpdateConcurrencyException("Prescription.ConcurrencyConflict");
            entry.Property(v => v.Status).OriginalValue = PrescriptionVersionStatus.Superseded;
            entry.Property(v => v.Status).IsModified = false;
        }
        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        if (ownedTransaction is not null) await ownedTransaction.CommitAsync(cancellationToken);
        return result;
    }

    private void AddPrescriptionAuditEvents()
    {
        var tracked = ChangeTracker.Entries<PrescriptionAuditEvent>().Select(e => e.Entity.Id).ToHashSet();
        var events = ChangeTracker.Entries<Prescription>().SelectMany(e => e.Entity.AuditEvents)
            .Where(e => !tracked.Contains(e.Id)).ToArray();
        PrescriptionAuditEvents.AddRange(events);
    }

    private async Task GuardPrescriptionHistoryAsync(CancellationToken ct)
    {
        var roots = ChangeTracker.Entries<Prescription>().Where(e => e.State == EntityState.Deleted).ToArray();
        foreach (var root in roots)
            if (!root.Entity.IsEmptyInitialDraft || await PrescriptionVersions.IgnoreAutoIncludes().AsNoTracking()
                .AnyAsync(v => v.PrescriptionId == root.Entity.Id && v.Status != PrescriptionVersionStatus.Draft, ct))
                throw new InvalidOperationException("Finalized prescriptions cannot be deleted.");
        foreach (var entry in ChangeTracker.Entries<PrescriptionVersion>())
        {
            var original = entry.Property(v => v.Status).OriginalValue;
            if (entry.State == EntityState.Deleted && original != PrescriptionVersionStatus.Draft)
                throw new InvalidOperationException("Finalized prescription versions cannot be deleted.");
            if (entry.State != EntityState.Modified || original == PrescriptionVersionStatus.Draft) continue;
            var allowed = original == PrescriptionVersionStatus.Finalized && entry.Entity.Status is PrescriptionVersionStatus.Superseded or PrescriptionVersionStatus.Voided;
            var allowedFields = entry.Entity.Status == PrescriptionVersionStatus.Voided
                ? new[] { nameof(PrescriptionVersion.Status), nameof(PrescriptionVersion.VoidReason), nameof(PrescriptionVersion.VoidedAtUtc), nameof(PrescriptionVersion.VoidedByApplicationUserId) }
                : [nameof(PrescriptionVersion.Status)];
            if (!allowed || entry.Properties.Any(p => p.IsModified && !allowedFields.Contains(p.Metadata.Name)))
                throw new InvalidOperationException("Finalized prescription content is immutable.");
        }
        var itemVersionIds = ChangeTracker.Entries<PrescriptionItem>().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => e.Entity.PrescriptionVersionId).Distinct().ToArray();
        if (itemVersionIds.Length > 0 && await PrescriptionVersions.IgnoreAutoIncludes().AsNoTracking()
            .AnyAsync(v => itemVersionIds.Contains(v.Id) && v.Status != PrescriptionVersionStatus.Draft, ct))
            throw new InvalidOperationException("Finalized prescription items are immutable.");
    }

    // Every operational cancellation/no-show/expiration path (including governance and background
    // processing) releases its matching claim in the same SaveChanges transaction. A stale writer
    // still fails on the eligibility rowversion, rolling back the entire operational transition.
    private async Task SynchronizeFollowUpReleasesAsync(CancellationToken ct)
    {
        var reservations = ChangeTracker.Entries<Reservation>()
            .Where(e => e.State == EntityState.Modified && e.Entity.FollowUpEligibilityId != null &&
                e.Property(r => r.Status).IsModified && e.Entity.Status is
                    ReservationStatus.Cancelled or ReservationStatus.NoShow or ReservationStatus.Expired)
            .Select(e => e.Entity).ToArray();
        var tickets = ChangeTracker.Entries<Ticket>()
            .Where(e => e.State == EntityState.Modified && e.Entity.FollowUpEligibilityId != null &&
                e.Property(t => t.Status).IsModified && e.Entity.Status is TicketStatus.Cancelled or TicketStatus.NoShow)
            .Select(e => e.Entity).ToArray();
        foreach (var reservation in reservations)
            await ReleaseAsync(reservation.FollowUpEligibilityId!.Value, reservation.Id, null,
                reservation.TimeZoneIdSnapshot, reservation.ModifiedByApplicationUserId,
                reservation.ModifiedOnUtc ?? DateTime.UtcNow, ct);
        foreach (var ticket in tickets)
            await ReleaseAsync(ticket.FollowUpEligibilityId!.Value, null, ticket.Id,
                ticket.PracticeTimeZoneIdSnapshot, ticket.ModifiedByApplicationUserId,
                ticket.LastUpdatedOnUtc, ct);
    }

    private async Task ReleaseAsync(Guid id, Guid? reservationId, Guid? ticketId, string timeZoneId,
        Guid? actor, DateTime nowUtc, CancellationToken ct)
    {
        var eligibility = await FollowUpEligibilities.SingleOrDefaultAsync(e => e.Id == id, ct);
        if (eligibility is null) throw new InvalidOperationException("Follow-up linkage is missing.");
        if (eligibility.Status != FollowUpEligibilityStatus.Reserved ||
            eligibility.ReservedReservationId != reservationId || eligibility.ReservedTicketId != ticketId) return;
        var config = await DoctorPracticeConfigurations.AsNoTracking()
            .SingleOrDefaultAsync(c => c.DoctorPracticeId == eligibility.DoctorPracticeId, ct);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById(config?.TimeZoneId ?? timeZoneId)));
        var release = eligibility.Release(reservationId, ticketId, today, actor, nowUtc);
        if (release.IsFailure) throw new InvalidOperationException("Follow-up claim could not be released.");
    }

    private async Task GuardCompletedDiagnosisDeletionAsync(CancellationToken ct)
    {
        var encounterIds = ChangeTracker.Entries<Diagnosis>().Where(e => e.State == EntityState.Deleted)
            .Select(e => e.Entity.MedicalEncounterId).Distinct().ToArray();
        if (encounterIds.Length == 0) return;
        if (ChangeTracker.Entries<MedicalEncounter>().Any(e => encounterIds.Contains(e.Entity.Id) && e.Entity.Status == EncounterStatus.Completed) ||
            await MedicalEncounters.AsNoTracking().AnyAsync(e => encounterIds.Contains(e.Id) && e.Status == EncounterStatus.Completed, ct))
            throw new InvalidOperationException("Completed diagnoses must be corrected or voided through an amendment.");
    }

    private void EnsureFinancialHistoryIsAppendOnly()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Deleted &&
                entry.Entity is DrugCatalog or DrugCatalogRequest or DrugCatalogImportBatch or MedicationIdempotencyRecord or Payment or Refund or PaymentCorrectionHistory or RefundCorrectionHistory or MedicalEncounter or EncounterAmendment or EncounterAmendmentChange or EncounterAuditEvent or FollowUpEligibility or FollowUpEligibilityHistory or DrugCatalogHistory or DrugCatalogRequestHistory or DrugCatalogImportRecord or PrescriptionAuditEvent)
            {
                throw new InvalidOperationException("Financial and clinical history cannot be deleted.");
            }

            if (entry.State == EntityState.Modified &&
                entry.Entity is PaymentCorrectionHistory or RefundCorrectionHistory or EncounterAmendment or EncounterAmendmentChange or EncounterAuditEvent or FollowUpEligibilityHistory or DrugCatalogHistory or DrugCatalogRequestHistory or DrugCatalogImportRecord or PrescriptionAuditEvent)
            {
                throw new InvalidOperationException("Financial and clinical correction history cannot be modified.");
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyWriteConfigurations(typeof(WaslaDbContext).Assembly);
        modelBuilder.ApplySoftDeleteQueryFilter();

        if (string.Equals(
                Database.ProviderName,
                "Microsoft.EntityFrameworkCore.Sqlite",
                StringComparison.Ordinal))
        {
            ConfigureSqliteConcurrencyFallback(modelBuilder);
            modelBuilder.Entity<MedicalEncounter>().ToTable("MedicalEncounters", table =>
                table.HasCheckConstraint("CK_MedicalEncounters_Completion", "([Status] = 1 AND [CompletedAtUtc] IS NULL) OR ([Status] = 2 AND [CompletedAtUtc] IS NOT NULL AND length(trim([ClinicalNotes])) > 0 AND [ClinicalNotes] IS NOT NULL)"));
        }
    }

    private static void ConfigureSqliteConcurrencyFallback(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var rowVersion = entityType.FindProperty(nameof(EmailOutboxMessage.RowVersion));
            if (rowVersion is null)
            {
                continue;
            }

            rowVersion.ValueGenerated = ValueGenerated.Never;
            rowVersion.SetBeforeSaveBehavior(PropertySaveBehavior.Save);
            rowVersion.SetAfterSaveBehavior(PropertySaveBehavior.Save);
        }
    }

    private void PrepareSqliteRowVersions()
    {
        if (!string.Equals(
                Database.ProviderName,
                "Microsoft.EntityFrameworkCore.Sqlite",
                StringComparison.Ordinal))
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries()
                     .Where(entry => entry.State is EntityState.Added or EntityState.Modified))
        {
            var rowVersion = entry.Metadata.FindProperty(nameof(EmailOutboxMessage.RowVersion));
            if (rowVersion is not null)
            {
                entry.Property(nameof(EmailOutboxMessage.RowVersion)).CurrentValue =
                    Guid.NewGuid().ToByteArray()[..8];
            }
        }
    }
}
