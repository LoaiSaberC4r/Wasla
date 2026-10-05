using Microsoft.EntityFrameworkCore;
using BuildingBlock.Application.Time;
using Wasla.Application.Features.Clinical;
using Wasla.Domain.Clinical;
using Wasla.Application.Features.Medications;
using Wasla.Domain.Medications;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class ClinicalReadService(WaslaDbContext db, IDateTimeProvider clock, IMedicationReadService medications) : IClinicalReadService
{
    public async Task<ClinicalPage<EncounterSummaryResponse>> ListEncountersAsync(Guid? doctorId, Guid? practiceId,
        Guid? patientId, EncounterStatus? status, DateTime? fromUtc, DateTime? throughUtc, string? search,
        int page, int size, CancellationToken ct)
    {
        var query = from e in db.MedicalEncounters.AsNoTracking()
            join patient in db.Patients.AsNoTracking() on e.PatientId equals patient.Id
            join practice in db.DoctorPractices.AsNoTracking() on e.DoctorPracticeId equals practice.Id
            join ticket in db.Tickets.AsNoTracking() on e.TicketId equals ticket.Id
            where (doctorId == null || e.DoctorId == doctorId) && (practiceId == null || e.DoctorPracticeId == practiceId) &&
                (patientId == null || e.PatientId == patientId) && (status == null || e.Status == status) &&
                (fromUtc == null || e.StartedAtUtc >= fromUtc) && (throughUtc == null || e.StartedAtUtc < throughUtc)
            select new { e, patient.NameAr, patient.NameEn, PracticeNameAr = practice.NameAr,
                PracticeNameEn = practice.NameEn, ticket.VisitTypeCodeSnapshot, ticket.VisitTypeNameArSnapshot, ticket.VisitTypeNameEnSnapshot };
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(r => r.NameAr.Contains(term) || r.NameEn != null && r.NameEn.Contains(term));
        }
        var count = await query.LongCountAsync(ct);
        // No collection Includes: Any becomes EXISTS; Skip/Take are evaluated in the database.
        var rows = await query.OrderByDescending(r => r.e.StartedAtUtc).ThenBy(r => r.e.Id)
            .Skip((page - 1) * size).Take(size).Select(r => new
            {
                r.e.Id, r.e.TicketId, r.e.PatientId, r.NameAr, r.NameEn, r.e.DoctorPracticeId, r.PracticeNameAr,
                r.PracticeNameEn, r.VisitTypeCodeSnapshot, r.VisitTypeNameArSnapshot, r.VisitTypeNameEnSnapshot,
                r.e.Status, r.e.StartedAtUtc, r.e.CompletedAtUtc, r.e.RowVersion,
                HasDiagnosis = db.Diagnoses.Any(d => d.MedicalEncounterId == r.e.Id && !d.IsVoided),
                HasEligibility = db.FollowUpEligibilities.Any(f => f.SourceMedicalEncounterId == r.e.Id)
            }).ToArrayAsync(ct);
        return new(rows.Select(r => new EncounterSummaryResponse(r.Id, r.TicketId, r.PatientId, r.NameAr, r.NameEn,
            r.DoctorPracticeId, r.PracticeNameAr, r.PracticeNameEn, r.VisitTypeCodeSnapshot, r.VisitTypeNameArSnapshot,
            r.VisitTypeNameEnSnapshot, r.Status, r.StartedAtUtc, r.CompletedAtUtc, r.HasDiagnosis, r.HasEligibility,
            Convert.ToBase64String(r.RowVersion))).ToArray(), count, page, size);
    }

    public async Task<EncounterDetailsResponse?> DoctorDetailsAsync(Guid doctorId, Guid practiceId, Guid id, bool byTicket, CancellationToken ct)
    {
        var row = await (from e in db.MedicalEncounters.AsNoTracking()
            join doctor in db.Doctors.AsNoTracking() on e.DoctorId equals doctor.Id
            join practice in db.DoctorPractices.AsNoTracking() on e.DoctorPracticeId equals practice.Id
            join patient in db.Patients.AsNoTracking() on e.PatientId equals patient.Id
            where e.DoctorId == doctorId && e.DoctorPracticeId == practiceId && (byTicket ? e.TicketId == id : e.Id == id)
            select new { e.Id, e.TicketId, e.Status, e.StartedAtUtc, e.CompletedAtUtc, e.ClinicalNotes, e.RowVersion,
                Doctor = new ClinicalPartyResponse(doctor.Id, doctor.NameAr, doctor.NameEn),
                Practice = new ClinicalPartyResponse(practice.Id, practice.NameAr, practice.NameEn),
                Patient = new ClinicalPartyResponse(patient.Id, patient.NameAr, patient.NameEn) }).SingleOrDefaultAsync(ct);
        if (row is null) return null;
        var prescription = await medications.PrescriptionAsync(null, row.Id, doctorId, ct);
        var blockers = (prescription?.CompletionBlockers ?? []).ToList();
        if (row.Status == EncounterStatus.InProgress && string.IsNullOrWhiteSpace(row.ClinicalNotes))
            blockers.Add(new("MedicalEncounter.ClinicalNotesRequired", null, "clinicalNotes"));
        return new(row.Id, row.TicketId, row.Doctor, row.Practice, row.Patient, row.Status, row.StartedAtUtc, row.CompletedAtUtc,
            row.ClinicalNotes, await DiagnosesAsync(row.Id, ct), await SourceEligibilityAsync(row.Id, row.Patient.Id, ct),
            new(false, false, false, false, false), Convert.ToBase64String(row.RowVersion), prescription, blockers);
    }

    public async Task<PatientEncounterDetailsResponse?> PatientDetailsAsync(Guid patientId, Guid id, CancellationToken ct)
    {
        // Clinical notes and amendment data are deliberately absent from this database projection.
        var row = await (from e in db.MedicalEncounters.AsNoTracking()
            join doctor in db.Doctors.AsNoTracking() on e.DoctorId equals doctor.Id
            join practice in db.DoctorPractices.AsNoTracking() on e.DoctorPracticeId equals practice.Id
            where e.Id == id && e.PatientId == patientId && e.Status == EncounterStatus.Completed
            select new { e.Id, e.TicketId, e.StartedAtUtc, e.CompletedAtUtc,
                Doctor = new ClinicalPartyResponse(doctor.Id, doctor.NameAr, doctor.NameEn),
                Practice = new ClinicalPartyResponse(practice.Id, practice.NameAr, practice.NameEn) }).SingleOrDefaultAsync(ct);
        return row is null ? null : new(row.Id, row.TicketId, row.Doctor, row.Practice, row.StartedAtUtc,
            row.CompletedAtUtc!.Value, await DiagnosesAsync(row.Id, ct), await SourceEligibilityAsync(row.Id, patientId, ct));
    }

    private async Task<IReadOnlyList<DiagnosisResponse>> DiagnosesAsync(Guid id, CancellationToken ct)
        => await db.Diagnoses.AsNoTracking().Where(d => d.MedicalEncounterId == id && !d.IsVoided).OrderBy(d => d.Type).ThenBy(d => d.Id)
            .Select(d => new DiagnosisResponse(d.Id, d.Type, d.DisplayText, d.Notes)).ToArrayAsync(ct);
    private async Task<FollowUpEligibilityResponse?> SourceEligibilityAsync(Guid encounterId, Guid patientId, CancellationToken ct)
    {
        var id = await db.FollowUpEligibilities.AsNoTracking().Where(e => e.SourceMedicalEncounterId == encounterId)
            .Select(e => (Guid?)e.Id).SingleOrDefaultAsync(ct);
        return id is null ? null : (await EligibilitiesAsync([patientId], null, null, id, 1, 1, ct)).Items.SingleOrDefault();
    }

    public async Task<IReadOnlyList<EncounterAmendmentResponse>> AmendmentsAsync(Guid encounterId, CancellationToken ct)
    {
        var amendments = await db.EncounterAmendments.AsNoTracking().Include(a => a.Changes)
            .Where(a => a.MedicalEncounterId == encounterId).OrderBy(a => a.SequenceNumber).ToArrayAsync(ct);
        return amendments.Select(a => new EncounterAmendmentResponse(a.Id, a.SequenceNumber, a.Reason,
            a.CreatedByDoctorId, a.CreatedByApplicationUserId, a.CreatedAtUtc, a.Changes.Select(c =>
                new AmendmentChangeResponse(c.TargetType, c.TargetEntityId, c.ChangeType, c.BeforeSnapshot, c.AfterSnapshot)).ToArray())).ToArray();
    }

    public async Task<ClinicalPage<FollowUpEligibilityResponse>> EligibilitiesAsync(IReadOnlyCollection<Guid> patientIds,
        Guid? practiceId, FollowUpEligibilityStatus? status, Guid? id, int page, int size, CancellationToken ct)
    {
        // Resolve business dates once per distinct timezone, then filter and paginate in SQL.
        var zones = await db.DoctorPracticeConfigurations.AsNoTracking().Select(c => c.TimeZoneId).Distinct().ToArrayAsync(ct);
        var todayByZone = zones.Select(zone => new { Zone = zone, Today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(clock.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(zone))) }).ToArray();
        var query = from e in db.FollowUpEligibilities.AsNoTracking()
            join patient in db.Patients.AsNoTracking() on e.PatientId equals patient.Id
            join doctor in db.Doctors.AsNoTracking() on e.DoctorId equals doctor.Id
            join practice in db.DoctorPractices.AsNoTracking() on e.DoctorPracticeId equals practice.Id
            join config in db.DoctorPracticeConfigurations.AsNoTracking() on e.DoctorPracticeId equals config.DoctorPracticeId
            where patientIds.Contains(e.PatientId) && (practiceId == null || e.DoctorPracticeId == practiceId) && (id == null || e.Id == id)
            select new { e, patient, doctor, practice, config.TimeZoneId };
        if (status is FollowUpEligibilityStatus.Available or FollowUpEligibilityStatus.Expired)
        {
            // A timezone expression tree keeps expiration filtering before Skip/Take.
            var zonesQuery = query.Where(r => false);
            foreach (var zone in todayByZone)
            {
                var zoneId = zone.Zone; var today = zone.Today;
                zonesQuery = zonesQuery.Concat(query.Where(r => r.TimeZoneId == zoneId &&
                    (status == FollowUpEligibilityStatus.Available
                        ? r.e.Status == FollowUpEligibilityStatus.Available && r.e.ValidUntil >= today
                        : r.e.Status == FollowUpEligibilityStatus.Expired || r.e.Status == FollowUpEligibilityStatus.Available && r.e.ValidUntil < today)));
            }
            query = zonesQuery;
        }
        else if (status.HasValue) query = query.Where(r => r.e.Status == status);
        var count = await query.LongCountAsync(ct);
        var rows = await query.OrderByDescending(r => r.e.CreatedOnUtc).ThenBy(r => r.e.Id).Skip((page - 1) * size).Take(size)
            .Select(r => new { r.e, r.TimeZoneId,
                Patient = new ClinicalPartyResponse(r.patient.Id, r.patient.NameAr, r.patient.NameEn),
                Doctor = new ClinicalPartyResponse(r.doctor.Id, r.doctor.NameAr, r.doctor.NameEn),
                Practice = new ClinicalPartyResponse(r.practice.Id, r.practice.NameAr, r.practice.NameEn),
                CanOperate = r.practice.IsActive && r.doctor.ApprovalStatus == Wasla.Domain.Common.DoctorApprovalStatus.Approved &&
                    db.ApplicationUsers.Any(u => u.Id == r.doctor.ApplicationUserId && u.IsActive) }).ToArrayAsync(ct);
        return new(rows.Select(r =>
        {
            var today = todayByZone.Single(z => z.Zone == r.TimeZoneId).Today;
            var state = r.e.EffectiveStatus(today);
            return new FollowUpEligibilityResponse(r.e.Id, r.e.SourceMedicalEncounterId, r.Patient, r.Doctor, r.Practice,
                state, r.e.ValidUntil, r.e.ReservedReservationId, r.e.ReservedTicketId, r.e.ConsumedEncounterId,
                state == FollowUpEligibilityStatus.Available && r.CanOperate, Convert.ToBase64String(r.e.RowVersion));
        }).ToArray(), count, page, size);
    }

    public async Task AuditReadAsync(Guid encounterId, Guid actor, DateTime nowUtc, CancellationToken ct)
    {
        // Metadata only; this read audit does not modify the encounter's concurrency token.
        db.EncounterAuditEvents.Add(EncounterAuditEvent.RecordRead(encounterId, actor, nowUtc));
        await db.SaveChangesAsync(ct);
    }
}
