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
using Wasla.Domain.Labs;
using Wasla.Domain.Radiology;
using Wasla.Domain.Diagnostics;
using Wasla.Domain.Payments;
using BuildingBlock.Infrastructure.Extensions;
using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

public sealed partial class WaslaDbContext(DbContextOptions<WaslaDbContext> options,
    BuildingBlock.Application.Time.IDateTimeProvider? reportingClock = null,
    BuildingBlock.Application.Abstraction.Security.ICurrentUser? governanceActor = null)
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
    public DbSet<PatientOnboardingChallenge> PatientOnboardingChallenges => Set<PatientOnboardingChallenge>();
    public DbSet<PatientClaimRequest> PatientClaimRequests => Set<PatientClaimRequest>();
    public DbSet<PatientClaimEvidence> PatientClaimEvidences => Set<PatientClaimEvidence>();
    public DbSet<PatientClaimHistory> PatientClaimHistories => Set<PatientClaimHistory>();
    public DbSet<PatientActivationInvitation> PatientActivationInvitations => Set<PatientActivationInvitation>();
    public DbSet<PatientDuplicateCase> PatientDuplicateCases => Set<PatientDuplicateCase>();
    public DbSet<PatientIdentityAttestation> PatientIdentityAttestations => Set<PatientIdentityAttestation>();
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
        public DbSet<LabTestCatalog> LabTestCatalogs => Set<LabTestCatalog>();
    public DbSet<LabTestCatalogHistory> LabTestCatalogHistories => Set<LabTestCatalogHistory>();
    public DbSet<LabCatalogRequest> LabCatalogRequests => Set<LabCatalogRequest>();
    public DbSet<LabCatalogRequestHistory> LabCatalogRequestHistories => Set<LabCatalogRequestHistory>();
    public DbSet<LabRequest> LabRequests => Set<LabRequest>();
    public DbSet<LabRequestHistory> LabRequestHistories => Set<LabRequestHistory>();
    public DbSet<LabRequestItem> LabRequestItems => Set<LabRequestItem>();
    public DbSet<LabResult> LabResults => Set<LabResult>();
    public DbSet<LabResultHistory> LabResultHistories => Set<LabResultHistory>();
    public DbSet<LabResultVersion> LabResultVersions => Set<LabResultVersion>();
    public DbSet<LabResultCoverage> LabResultCoverages => Set<LabResultCoverage>();
    public DbSet<PatientLabResultSubmission> PatientLabResultSubmissions => Set<PatientLabResultSubmission>();
    public DbSet<PatientLabResultSubmissionHistory> PatientLabResultSubmissionHistories => Set<PatientLabResultSubmissionHistory>();
    public DbSet<LabResultAttachment> LabResultAttachments => Set<LabResultAttachment>();
    public DbSet<PatientLabResultSubmissionAttachment> PatientLabResultSubmissionAttachments => Set<PatientLabResultSubmissionAttachment>();
    public DbSet<LabCatalogImportBatch> LabCatalogImportBatches => Set<LabCatalogImportBatch>();
    public DbSet<LabCatalogImportRecord> LabCatalogImportRecords => Set<LabCatalogImportRecord>();
    public DbSet<RadiologyProcedureCatalog> RadiologyProcedureCatalogs => Set<RadiologyProcedureCatalog>();
    public DbSet<RadiologyProcedureCatalogHistory> RadiologyProcedureCatalogHistories => Set<RadiologyProcedureCatalogHistory>();
    public DbSet<RadiologyCatalogRequest> RadiologyCatalogRequests => Set<RadiologyCatalogRequest>();
    public DbSet<RadiologyCatalogRequestHistory> RadiologyCatalogRequestHistories => Set<RadiologyCatalogRequestHistory>();
    public DbSet<RadiologyRequest> RadiologyRequests => Set<RadiologyRequest>();
    public DbSet<RadiologyRequestHistory> RadiologyRequestHistories => Set<RadiologyRequestHistory>();
    public DbSet<RadiologyRequestItem> RadiologyRequestItems => Set<RadiologyRequestItem>();
    public DbSet<RadiologyResult> RadiologyResults => Set<RadiologyResult>();
    public DbSet<RadiologyResultHistory> RadiologyResultHistories => Set<RadiologyResultHistory>();
    public DbSet<RadiologyResultVersion> RadiologyResultVersions => Set<RadiologyResultVersion>();
    public DbSet<RadiologyResultCoverage> RadiologyResultCoverages => Set<RadiologyResultCoverage>();
    public DbSet<PatientRadiologyResultSubmission> PatientRadiologyResultSubmissions => Set<PatientRadiologyResultSubmission>();
    public DbSet<PatientRadiologyResultSubmissionHistory> PatientRadiologyResultSubmissionHistories => Set<PatientRadiologyResultSubmissionHistory>();
    public DbSet<RadiologyResultAttachment> RadiologyResultAttachments => Set<RadiologyResultAttachment>();
    public DbSet<PatientRadiologyResultSubmissionAttachment> PatientRadiologyResultSubmissionAttachments => Set<PatientRadiologyResultSubmissionAttachment>();
    public DbSet<RadiologyCatalogImportBatch> RadiologyCatalogImportBatches => Set<RadiologyCatalogImportBatch>();
    public DbSet<RadiologyCatalogImportRecord> RadiologyCatalogImportRecords => Set<RadiologyCatalogImportRecord>();
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
    public DbSet<Wasla.Domain.OperatingFinance.OperatingFinancialPlan> OperatingFinancialPlans => Set<Wasla.Domain.OperatingFinance.OperatingFinancialPlan>();
    public DbSet<Wasla.Domain.OperatingFinance.OperatingPlanRevision> OperatingPlanRevisions => Set<Wasla.Domain.OperatingFinance.OperatingPlanRevision>();
    public DbSet<Wasla.Domain.OperatingFinance.OperatingPlanAllocation> OperatingPlanAllocations => Set<Wasla.Domain.OperatingFinance.OperatingPlanAllocation>();
    public DbSet<Wasla.Domain.OperatingFinance.OperatingObligation> OperatingObligations => Set<Wasla.Domain.OperatingFinance.OperatingObligation>();
    public DbSet<Wasla.Domain.OperatingFinance.OperatingObligationRevision> OperatingObligationRevisions => Set<Wasla.Domain.OperatingFinance.OperatingObligationRevision>();
    public DbSet<Wasla.Domain.OperatingFinance.OperatingCostAllocationSnapshot> OperatingCostAllocationSnapshots => Set<Wasla.Domain.OperatingFinance.OperatingCostAllocationSnapshot>();
    public DbSet<Wasla.Domain.OperatingFinance.OperatingSettlement> OperatingSettlements => Set<Wasla.Domain.OperatingFinance.OperatingSettlement>();
    public DbSet<Wasla.Domain.OperatingFinance.OperatingSettlementReversal> OperatingSettlementReversals => Set<Wasla.Domain.OperatingFinance.OperatingSettlementReversal>();
    public DbSet<Wasla.Domain.OperatingFinance.OperatingFinanceHistory> OperatingFinanceHistories => Set<Wasla.Domain.OperatingFinance.OperatingFinanceHistory>();
    public DbSet<Reception> Receptions => Set<Reception>();
    public DbSet<ReceptionPracticeAssignment> ReceptionPracticeAssignments => Set<ReceptionPracticeAssignment>();
    public DbSet<ReceptionPracticeAssignmentPermission> ReceptionPracticeAssignmentPermissions
        => Set<ReceptionPracticeAssignmentPermission>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
        => SaveChangesAsync(acceptAllChangesOnSuccess, CancellationToken.None).GetAwaiter().GetResult();

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var hasNewArchiveIdentities = HasNewArchiveIdentities;
        await using var identityTransaction = hasNewArchiveIdentities && Database.CurrentTransaction is null
            ? await Database.BeginTransactionAsync(cancellationToken) : null;
        if (ChangeTracker.Entries<PatientClaimHistory>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Patient claim history is append-only; aggregate retention uses the restricted worker.");
        PrepareArchiveIdentities();
        await CaptureArchiveGovernanceAsync(cancellationToken);
        var diagnosticCorrection = ChangeTracker.Entries<LabResultVersion>().Any(e => e.State == EntityState.Modified && e.Entity.Status == DiagnosticVersionStatus.Superseded) ||
            ChangeTracker.Entries<RadiologyResultVersion>().Any(e => e.State == EntityState.Modified && e.Entity.Status == DiagnosticVersionStatus.Superseded);
        await using var diagnosticTransaction = diagnosticCorrection && Database.CurrentTransaction is null
            ? await Database.BeginTransactionAsync(cancellationToken) : null;
        var reportingPractices = ChangedReportingPractices();
        await using var reportingTransaction = reportingPractices.Length > 0 && Database.CurrentTransaction is null
            ? await Database.BeginTransactionAsync(cancellationToken) : null;
        await SynchronizeFollowUpReleasesAsync(cancellationToken);
        await SynchronizeMedicalRecordExpiryAsync(cancellationToken);
        await PrepareFamilyMedicalAsync(cancellationToken);
        await GuardCompletedDiagnosisDeletionAsync(cancellationToken);
        await GuardPrescriptionHistoryAsync(cancellationToken);
        await GuardDiagnosticContentAsync(cancellationToken);
        await GuardDiagnosticHistoryAsync(cancellationToken);
        AddDiagnosticDraftHistory();
        EnsureFinancialHistoryIsAppendOnly();
        await CaptureReportingPoliciesAsync(reportingPractices, cancellationToken);
        EnsureOperatingFinanceIsAppendOnly();
        AddPrescriptionAuditEvents();
        var trackedVisibility = ChangeTracker.Entries<EncounterVisibilityHistory>().Select(e => e.Entity.Id).ToHashSet();
        Set<EncounterVisibilityHistory>().AddRange(ChangeTracker.Entries<MedicalEncounter>().SelectMany(e => e.Entity.VisibilityHistory).Where(h => !trackedVisibility.Contains(h.Id)).ToArray());
        if (ChangeTracker.Entries<EncounterVisibilityHistory>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Visibility history is immutable.");
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
        if (reportingPractices.Length > 0 || hasNewArchiveIdentities)
        {
            var observedNow = reportingClock?.UtcNow ?? DateTime.UtcNow;
            var observedPractices = reportingPractices.Concat(ChangeTracker.Entries<Ticket>().Select(x => x.Entity.DoctorPracticeId))
                .Concat(ChangeTracker.Entries<Reservation>().Select(x => x.Entity.DoctorPracticeId)).Distinct().ToArray();
            foreach (var practiceId in observedPractices)
            {
                var config = await DoctorPracticeConfigurations.AsNoTracking().SingleOrDefaultAsync(x => x.DoctorPracticeId == practiceId, cancellationToken);
                if (config == null || !TimeZoneInfo.TryFindSystemTimeZoneById(config.TimeZoneId, out var zone)) continue;
                var currentDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(observedNow, DateTimeKind.Utc), zone));
                var dates = await PracticeOperationalDays.AsNoTracking().Where(x => x.DoctorPracticeId == practiceId && x.ClosedAtUtc == null && x.EndsAtUtc > observedNow)
                    .Select(x => x.BusinessDate).ToArrayAsync(cancellationToken);
                foreach (var businessDate in dates.Append(currentDay).Concat(ChangeTracker.Entries<Reservation>().Where(x => x.Entity.DoctorPracticeId == practiceId).Select(x => x.Entity.BusinessDate)).Distinct())
                    await ArchiveOperationalDays.ObserveAsync(this, practiceId, businessDate, observedNow, cancellationToken);
            }
            PrepareSqliteRowVersions();
            result += await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        if (ownedTransaction is not null) await ownedTransaction.CommitAsync(cancellationToken);
        if (diagnosticTransaction is not null) await diagnosticTransaction.CommitAsync(cancellationToken);
        if (reportingTransaction is not null) await reportingTransaction.CommitAsync(cancellationToken);
        if (identityTransaction is not null) await identityTransaction.CommitAsync(cancellationToken);
        return result;
    }

    private void AddDiagnosticDraftHistory()
    {
        var trackedLab = ChangeTracker.Entries<LabRequestHistory>().Select(e => e.Entity.Id).ToHashSet();
        Set<LabRequestHistory>().AddRange(ChangeTracker.Entries<LabRequest>().SelectMany(e => e.Entity.History).Where(e => !trackedLab.Contains(e.Id)).ToArray());
        var trackedRadiology = ChangeTracker.Entries<RadiologyRequestHistory>().Select(e => e.Entity.Id).ToHashSet();
        Set<RadiologyRequestHistory>().AddRange(ChangeTracker.Entries<RadiologyRequest>().SelectMany(e => e.Entity.History).Where(e => !trackedRadiology.Contains(e.Id)).ToArray());
    }
    private async Task GuardDiagnosticHistoryAsync(CancellationToken ct)
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted && entry.Entity is LabTestCatalogHistory or LabCatalogRequestHistory or LabRequestHistory or LabResultHistory or LabResultCoverage or PatientLabResultSubmissionHistory or LabResultAttachment or PatientLabResultSubmissionAttachment or LabCatalogImportRecord or RadiologyProcedureCatalogHistory or RadiologyCatalogRequestHistory or RadiologyRequestHistory or RadiologyResultHistory or RadiologyResultCoverage or PatientRadiologyResultSubmissionHistory or RadiologyResultAttachment or PatientRadiologyResultSubmissionAttachment or RadiologyCatalogImportRecord)
                throw new InvalidOperationException("Diagnostic history is immutable.");
            if (entry.State == EntityState.Deleted && entry.Entity is LabTestCatalog or RadiologyProcedureCatalog or LabCatalogRequest or RadiologyCatalogRequest or LabResult or RadiologyResult or PatientLabResultSubmission or PatientRadiologyResultSubmission or LabCatalogImportBatch or RadiologyCatalogImportBatch)
                throw new InvalidOperationException("Diagnostic records cannot be deleted.");
        }
        foreach (var entry in ChangeTracker.Entries<LabRequest>().Where(e => e.State == EntityState.Deleted))
            if (entry.Entity.Status != DiagnosticRequestStatus.Draft || entry.Entity.Items.Count != 0)
                throw new InvalidOperationException("Issued diagnostic orders cannot be deleted.");
        foreach (var entry in ChangeTracker.Entries<LabResultVersion>())
        {
            if (entry.State == EntityState.Deleted) throw new InvalidOperationException("Result versions cannot be deleted.");
            if (entry.State != EntityState.Modified) continue;
            var original = entry.Property(x => x.Status).OriginalValue;
            var allowed = original == DiagnosticVersionStatus.Finalized && entry.Entity.Status is DiagnosticVersionStatus.Superseded or DiagnosticVersionStatus.Voided;
            var fields = entry.Entity.Status == DiagnosticVersionStatus.Superseded ? new[] { "Status" } : new[] { "Status", "VoidReason", "VoidedAtUtc" };
            if (!allowed || entry.Properties.Any(p => p.IsModified && !fields.Contains(p.Metadata.Name)))
                throw new InvalidOperationException("Finalized result content is immutable.");
            if (entry.Entity.Status == DiagnosticVersionStatus.Superseded)
            {
                var count = await Database.ExecuteSqlInterpolatedAsync($"UPDATE [LabResultVersions] SET [Status] = 2 WHERE [Id] = {entry.Entity.Id} AND [Status] = 1", ct);
                if (count != 1) throw new DbUpdateConcurrencyException("LabResult.ConcurrencyConflict");
                entry.Property(x => x.Status).OriginalValue = DiagnosticVersionStatus.Superseded;
                entry.Property(x => x.Status).IsModified = false;
            }
        }
        foreach (var entry in ChangeTracker.Entries<RadiologyRequest>().Where(e => e.State == EntityState.Deleted))
            if (entry.Entity.Status != DiagnosticRequestStatus.Draft || entry.Entity.Items.Count != 0)
                throw new InvalidOperationException("Issued diagnostic orders cannot be deleted.");
        foreach (var entry in ChangeTracker.Entries<RadiologyResultVersion>())
        {
            if (entry.State == EntityState.Deleted) throw new InvalidOperationException("Result versions cannot be deleted.");
            if (entry.State != EntityState.Modified) continue;
            var original = entry.Property(x => x.Status).OriginalValue;
            var allowed = original == DiagnosticVersionStatus.Finalized && entry.Entity.Status is DiagnosticVersionStatus.Superseded or DiagnosticVersionStatus.Voided;
            var fields = entry.Entity.Status == DiagnosticVersionStatus.Superseded ? new[] { "Status" } : new[] { "Status", "VoidReason", "VoidedAtUtc" };
            if (!allowed || entry.Properties.Any(p => p.IsModified && !fields.Contains(p.Metadata.Name)))
                throw new InvalidOperationException("Finalized result content is immutable.");
            if (entry.Entity.Status == DiagnosticVersionStatus.Superseded)
            {
                var count = await Database.ExecuteSqlInterpolatedAsync($"UPDATE [RadiologyResultVersions] SET [Status] = 2 WHERE [Id] = {entry.Entity.Id} AND [Status] = 1", ct);
                if (count != 1) throw new DbUpdateConcurrencyException("RadiologyResult.ConcurrencyConflict");
                entry.Property(x => x.Status).OriginalValue = DiagnosticVersionStatus.Superseded;
                entry.Property(x => x.Status).IsModified = false;
            }
        }
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
        ConfigureArchives(modelBuilder);
        var utcConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
            value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
        foreach (var entity in modelBuilder.Model.GetEntityTypes().Where(e => e.ClrType.Namespace is
            "Wasla.Domain.OperatingFinance" or "Wasla.Domain.Reporting" or "Wasla.Domain.Operations" or "Wasla.Domain.Archives" or "Wasla.Domain.Tickets" or "Wasla.Domain.Reservations" or "Wasla.Domain.Patients"))
            foreach (var property in entity.GetProperties().Where(p => p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?)))
                if (!property.Name.Contains("Local", StringComparison.Ordinal)) property.SetValueConverter(utcConverter);
        modelBuilder.ApplySoftDeleteQueryFilter();

        if (string.Equals(
                Database.ProviderName,
                "Microsoft.EntityFrameworkCore.Sqlite",
                StringComparison.Ordinal))
        {
            ConfigureSqliteConcurrencyFallback(modelBuilder);
            modelBuilder.Entity<Patient>().ToTable("Patients", t => t.HasCheckConstraint("CK_Patients_Email", "[Email] IS NULL OR length(trim([Email])) > 0"));
            modelBuilder.Entity<HiddenEncounterAccessRequest>().ToTable("HiddenEncounterAccessRequests", t => t.HasCheckConstraint("CK_HiddenAccessRequests_Reason", "length(trim([Reason])) > 0"));
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
