using BuildingBlock.Application.Time;
using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Clinical;
using Wasla.Application.Features.DoctorPatients;
using Wasla.Domain.Archives;
using Wasla.Domain.Clinical;
using Wasla.Domain.Diagnostics;
using Wasla.Domain.Medications;
using Wasla.Domain.Reservations;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class DoctorPatientReadService(WaslaDbContext db, IDateTimeProvider clock) : IDoctorPatientReadService
{
    public Task<DoctorPatientPracticeScope?> PracticeScopeAsync(Guid practiceId, CancellationToken ct)
        => db.DoctorPractices.AsNoTracking().Where(p => p.Id == practiceId)
            .Select(p => new DoctorPatientPracticeScope(p.DoctorId, p.IsActive)).SingleOrDefaultAsync(ct);
    // Registry identity plus the actual immutable admission snapshot. No status filter: a later
    // cancellation/no-show cannot erase a successful check-in. Never infer admission from a booking.
    private IQueryable<Ticket> Attendances(Guid doctor) => QualifyingAttendances(db, doctor);
    internal static IQueryable<Ticket> QualifyingAttendances(WaslaDbContext db, Guid doctor) => db.AllTickets.Where(t => t.DoctorId == doctor &&
        t.TicketNumber > 0 && t.CheckInTimeUtc > DateTime.MinValue && t.CreatedByApplicationUserId != Guid.Empty &&
        ((t.Source == TicketSource.WalkIn && t.CheckInMode == CheckInMode.WalkIn && t.ReservationId == null) ||
         (t.Source == TicketSource.Reservation && t.ReservationId != null &&
          (t.CheckInMode == CheckInMode.Normal || t.CheckInMode == CheckInMode.Force) &&
          db.ReservationRegistry.Any(r => r.Id == t.ReservationId && r.DoctorId == doctor && r.PatientId == t.PatientId && r.DoctorPracticeId == t.DoctorPracticeId))) &&
        db.TicketRegistry.Any(r => r.Id == t.Id && r.DoctorId == doctor && r.PatientId == t.PatientId &&
            r.DoctorPracticeId == t.DoctorPracticeId && r.ReservationId == t.ReservationId &&
            r.BusinessDate == t.BusinessDate && r.TicketNumber == t.TicketNumber) &&
        db.DoctorPractices.Any(p => p.Id == t.DoctorPracticeId && p.DoctorId == doctor));
    public Task<bool> HasAttendanceAsync(Guid doctorId, Guid patientId, CancellationToken ct)
        => Attendances(doctorId).AnyAsync(t => t.PatientId == patientId, ct);
    private static bool Historical(DoctorPatientActor a) => a.Has(PermissionNames.DoctorPatientsViewHistoricalOwn);
    private IQueryable<Guid> ReadablePractices(DoctorPatientActor a) => db.DoctorPractices.AsNoTracking()
        .Where(p => p.DoctorId == a.DoctorId && (p.IsActive || Historical(a))).Select(p => p.Id);
    private IQueryable<Reservation> Reservations(DoctorPatientActor a) => db.AllReservations.Where(r => r.DoctorId == a.DoctorId &&
        ReadablePractices(a).Contains(r.DoctorPracticeId) && db.ReservationRegistry.Any(i => i.Id == r.Id &&
            i.DoctorId == a.DoctorId && i.PatientId == r.PatientId && i.DoctorPracticeId == r.DoctorPracticeId &&
            (!i.IsArchived || a.Has(Phase18PartTwoPermissionNames.ReservationsPractice))));

    public Task<DoctorPatientsPageResponse> DirectoryAsync(DoctorPatientActor a, DoctorPatientsQuery q, CancellationToken ct)
        => DirectoryCoreAsync(a, q, null, ct);
    private async Task<DoctorPatientsPageResponse> DirectoryCoreAsync(DoctorPatientActor a, DoctorPatientsQuery q,
        Guid? patientId, CancellationToken ct)
    {
        var asOf = q.AsOfUtc ?? clock.UtcNow;
        var tickets = Attendances(a.DoctorId).Where(t => t.CheckInTimeUtc <= asOf);
        if (patientId is { } selectedPatient) tickets = tickets.Where(t => t.PatientId == selectedPatient);
        if (q.PracticeId is { } practiceId) tickets = tickets.Where(t => t.DoctorPracticeId == practiceId);
        if (q.PracticeScope == "active") tickets = tickets.Where(t => db.DoctorPractices.Any(p => p.Id == t.DoctorPracticeId && p.IsActive));
        if (q.PracticeScope == "historicalOnly") tickets = tickets.Where(t => !Attendances(a.DoctorId).Any(other =>
            other.PatientId == t.PatientId && db.DoctorPractices.Any(p => p.Id == other.DoctorPracticeId && p.IsActive)));
        var groups = tickets.GroupBy(t => t.PatientId).Select(g => new
        {
            PatientId = g.Key, First = g.Min(t => t.CheckInTimeUtc), Last = g.Max(t => t.CheckInTimeUtc),
            Count = g.Count(), Practices = g.Select(t => t.DoctorPracticeId).Distinct().Count()
        });
        var cutoff = asOf.AddDays(-30);
        var summary = patientId.HasValue ? new DoctorPatientsSummary(0, 0, 0) : new DoctorPatientsSummary(await groups.LongCountAsync(ct),
            await groups.LongCountAsync(g => g.Last >= cutoff, ct), await groups.LongCountAsync(g => g.Count >= 2, ct));
        var clinical = a.Has(PermissionNames.MedicalEncountersViewOwn);
        var appointments = a.Has(PermissionNames.DoctorPracticeReservationsViewOwn);
        var now = clock.UtcNow;
        var rows = from g in groups
            join p in db.Patients.AsNoTracking() on g.PatientId equals p.Id
            join last in tickets on tickets.Where(t => t.PatientId == g.PatientId)
                .OrderByDescending(t => t.CheckInTimeUtc).ThenBy(t => t.Id).Select(t => t.Id).First() equals last.Id
            join practice in db.DoctorPractices.AsNoTracking() on last.DoctorPracticeId equals practice.Id
            select new
            {
                g, p.Id, p.NameAr, p.NameEn, p.PhoneNumber, p.Email, p.DateOfBirth, p.Gender,
                last.BusinessDate, PracticeId = practice.Id, PracticeNameAr = practice.NameAr, PracticeNameEn = practice.NameEn,
                practice.IsActive,
                HistoricalOnly = !Attendances(a.DoctorId).Any(t => t.PatientId == p.Id && db.DoctorPractices.Any(pr => pr.Id == t.DoctorPracticeId && pr.IsActive)),
                Completed = clinical && (Historical(a) || !tickets.Any(t => t.PatientId == p.Id && db.DoctorPractices.Any(pr => pr.Id == t.DoctorPracticeId && !pr.IsActive))) ? (int?)db.MedicalEncounters.Count(e => e.DoctorId == a.DoctorId && e.PatientId == p.Id &&
                    e.Status == EncounterStatus.Completed && ReadablePractices(a).Contains(e.DoctorPracticeId) &&
                    (q.PracticeId == null || e.DoctorPracticeId == q.PracticeId)) : null,
                Upcoming = appointments && (Historical(a) || !tickets.Any(t => t.PatientId == p.Id && db.DoctorPractices.Any(pr => pr.Id == t.DoctorPracticeId && !pr.IsActive))) ? (bool?)Reservations(a).Any(r => r.PatientId == p.Id && r.Status == ReservationStatus.Active &&
                    r.ScheduledStartUtc > now && (q.PracticeId == null || r.DoctorPracticeId == q.PracticeId)) : null
            };
        if (patientId is { } id) rows = rows.Where(r => r.Id == id);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim();
            var lower = term.ToLowerInvariant();
            var phone = new string(term.Where(c => char.IsAsciiDigit(c) || c == '+').ToArray());
#pragma warning disable CA1304, CA1311, CA1862 // SQL LOWER; invariant/comparison overloads cannot be translated by EF.
            rows = rows.Where(r => r.NameAr.Contains(term) || r.NameEn != null && r.NameEn.ToLower().Contains(lower) ||
                r.Email != null && r.Email.ToLower().Contains(lower) ||
                phone.Length > 0 && r.PhoneNumber != null && r.PhoneNumber.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "").Contains(phone));
#pragma warning restore CA1304, CA1311, CA1862
        }
        if (q.LastAttendanceFrom is { } from) rows = rows.Where(r => r.BusinessDate >= from);
        if (q.LastAttendanceTo is { } to) rows = rows.Where(r => r.BusinessDate <= to);
        if (q.MinAttendanceCount is { } count) rows = rows.Where(r => r.g.Count >= count);
        if (q.HasUpcomingAppointment is { } upcoming) rows = rows.Where(r => r.Upcoming == upcoming);
        var total = await rows.LongCountAsync(ct);
        var page = await rows.OrderByDescending(r => r.g.Last).ThenBy(r => r.Id).Skip((q.PageNumber - 1) * q.PageSize)
            .Take(q.PageSize).ToArrayAsync(ct);
        var today = DateOnly.FromDateTime(clock.UtcNow);
        return new(page.Select(r => new DoctorPatientListItem(r.Id, r.NameAr, r.NameEn, r.PhoneNumber, r.Email,
            r.DateOfBirth, Age(r.DateOfBirth, today), r.Gender, Utc(r.g.First), Utc(r.g.Last), r.BusinessDate, r.g.Count,
            r.Completed, r.g.Practices, new(r.PracticeId, r.PracticeNameAr, r.PracticeNameEn, r.IsActive), r.Upcoming,
            r.HistoricalOnly, Capabilities(a, !r.HistoricalOnly || Historical(a)))).ToArray(), total, q.PageNumber, q.PageSize, summary, asOf);
    }
    private static int Age(DateOnly dob, DateOnly today) => today.Year - dob.Year - (dob.AddYears(today.Year - dob.Year) > today ? 1 : 0);
    private static DoctorPatientCapabilities Capabilities(DoctorPatientActor a, bool scope)
    {
        bool Has(string p) => scope && a.Has(p);
        return new(Has(PermissionNames.DoctorPracticeTicketsViewOwn), Has(PermissionNames.MedicalEncountersViewOwn) && Has(PermissionNames.DiagnosesViewOwn),
            Has(PermissionNames.DiagnosesViewOwn), Has(PermissionNames.PrescriptionsViewOwn),
            Has(PermissionNames.PrescriptionsViewOwn) && Has(PermissionNames.PrescriptionsExportOwn),
            Has(PermissionNames.DoctorPracticeReservationsViewOwn), Has(PermissionNames.FollowUpEligibilityViewOwn),
            Has(PermissionNames.LabRequestsViewOwn), Has(PermissionNames.LabResultsViewOwn), Has(PermissionNames.LabResultSubmissionsViewOwn),
            Has(PermissionNames.RadiologyRequestsViewOwn), Has(PermissionNames.RadiologyResultsViewOwn), Has(PermissionNames.RadiologyResultSubmissionsViewOwn));
    }
    public async Task<object> HistoryAsync(DoctorPatientActor a, DoctorPatientHistoryQuery q, CancellationToken ct)
        => q.Resource switch
        {
            DoctorPatientHistory.Attendances => await AttendanceAsync(a, q, ct),
            DoctorPatientHistory.Diagnoses => await DiagnosesAsync(a, q, ct),
            DoctorPatientHistory.Prescriptions => await PrescriptionsAsync(a, q, ct),
            DoctorPatientHistory.Appointments => await AppointmentsAsync(a, q, ct),
            DoctorPatientHistory.FollowUps => await FollowUpsAsync(a, q, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(q))
        };
    private async Task<ClinicalPage<DoctorPatientAttendance>> AttendanceAsync(DoctorPatientActor a, DoctorPatientHistoryQuery q, CancellationToken ct)
    {
        var clinical = a.Has(PermissionNames.MedicalEncountersViewOwn) && a.Has(PermissionNames.DiagnosesViewOwn);
        var rows = from t in Attendances(a.DoctorId)
            join p in db.DoctorPractices.AsNoTracking() on t.DoctorPracticeId equals p.Id
            join r in db.TicketRegistry.AsNoTracking() on t.Id equals r.Id
            where t.PatientId == q.PatientId && ReadablePractices(a).Contains(p.Id) &&
                (q.PracticeId == null || p.Id == q.PracticeId) && (!r.IsArchived || a.Has(Phase18PartTwoPermissionNames.TicketsPractice)) &&
                (q.FromDate == null || t.BusinessDate >= q.FromDate) && (q.ToDate == null || t.BusinessDate <= q.ToDate)
            select new { t, p.NameAr, r.IsArchived };
        if (q.Status is { } status) { var parsed = Enum.Parse<TicketStatus>(status, true); rows = rows.Where(r => r.t.Status == parsed); }
        var total = await rows.LongCountAsync(ct);
        var result = await rows.OrderByDescending(r => r.t.CheckInTimeUtc).ThenBy(r => r.t.Id)
            .Skip((q.PageNumber - 1) * q.PageSize).Take(q.PageSize).Select(r => new
            {
                r.t.Id, r.t.DoctorPracticeId, r.NameAr, r.t.BusinessDate, r.t.CheckInTimeUtc, r.t.Source, r.t.CheckInMode,
                r.t.Status, r.t.TicketNumber, r.IsArchived, r.t.ReservationId,
                EncounterId = clinical ? db.MedicalEncounters.Where(e => e.TicketId == r.t.Id && e.DoctorId == a.DoctorId && e.PatientId == q.PatientId).Select(e => (Guid?)e.Id).SingleOrDefault() : null,
                EncounterStatus = clinical ? db.MedicalEncounters.Where(e => e.TicketId == r.t.Id && e.DoctorId == a.DoctorId && e.PatientId == q.PatientId).Select(e => (EncounterStatus?)e.Status).SingleOrDefault() : null
            }).ToArrayAsync(ct);
        return new(result.Select(r => new DoctorPatientAttendance(r.Id, r.DoctorPracticeId, r.NameAr, r.BusinessDate,
            Utc(r.CheckInTimeUtc), r.Source.ToString(), r.CheckInMode.ToString(), r.Status.ToString(), r.TicketNumber, r.IsArchived,
            r.ReservationId, r.EncounterId, r.EncounterStatus?.ToString(), true, r.EncounterId.HasValue)).ToArray(), total, q.PageNumber, q.PageSize);
    }
    private async Task<ClinicalPage<DoctorPatientDiagnosis>> DiagnosesAsync(DoctorPatientActor a, DoctorPatientHistoryQuery q, CancellationToken ct)
    {
        var rows = from d in db.Diagnoses.AsNoTracking()
            join e in db.MedicalEncounters.AsNoTracking() on d.MedicalEncounterId equals e.Id
            join p in db.DoctorPractices.AsNoTracking() on e.DoctorPracticeId equals p.Id
            join t in db.TicketRegistry.AsNoTracking() on e.TicketId equals t.Id
            where !d.IsVoided && e.DoctorId == a.DoctorId && e.PatientId == q.PatientId && ReadablePractices(a).Contains(p.Id) &&
                (q.PracticeId == null || p.Id == q.PracticeId) && (q.FromDate == null || t.BusinessDate >= q.FromDate) && (q.ToDate == null || t.BusinessDate <= q.ToDate)
            select new { d.Id, EncounterId = e.Id, PracticeId = p.Id, t.BusinessDate, e.StartedAtUtc, d.Type, d.DisplayText, e.Status, p.IsActive };
        var total = await rows.LongCountAsync(ct);
        var result = await rows.OrderByDescending(r => r.StartedAtUtc).ThenBy(r => r.Id).Skip((q.PageNumber - 1) * q.PageSize).Take(q.PageSize).ToArrayAsync(ct);
        return new(result.Select(r => new DoctorPatientDiagnosis(r.Id, r.EncounterId, r.PracticeId, r.BusinessDate,
            Utc(r.StartedAtUtc), r.Type.ToString(), r.DisplayText, r.Status.ToString(), true, !r.IsActive)).ToArray(), total, q.PageNumber, q.PageSize);
    }
    private async Task<ClinicalPage<DoctorPatientPrescription>> PrescriptionsAsync(DoctorPatientActor a, DoctorPatientHistoryQuery q, CancellationToken ct)
    {
        var rows = from rx in db.Prescriptions.AsNoTracking().IgnoreAutoIncludes()
            join e in db.MedicalEncounters.AsNoTracking() on rx.MedicalEncounterId equals e.Id
            join p in db.DoctorPractices.AsNoTracking() on e.DoctorPracticeId equals p.Id
            join t in db.TicketRegistry.AsNoTracking() on e.TicketId equals t.Id
            join v in db.PrescriptionVersions.AsNoTracking().IgnoreAutoIncludes() on rx.Id equals v.PrescriptionId
            where rx.DoctorId == a.DoctorId && e.DoctorId == a.DoctorId && rx.PatientId == q.PatientId && e.PatientId == q.PatientId &&
                e.Status == EncounterStatus.Completed && (v.Status == PrescriptionVersionStatus.Finalized || v.Status == PrescriptionVersionStatus.Voided) &&
                ReadablePractices(a).Contains(p.Id) && (q.PracticeId == null || p.Id == q.PracticeId) &&
                (q.FromDate == null || t.BusinessDate >= q.FromDate) && (q.ToDate == null || t.BusinessDate <= q.ToDate)
            select new { rx.Id, EncounterId = e.Id, PracticeId = p.Id, e.StartedAtUtc, v.VersionNumber, v.Status,
                MedicationCount = v.Items.Count, HasVersions = rx.Versions.Count(x => x.Status != PrescriptionVersionStatus.Draft) > 1, p.IsActive };
        if (q.Status is { } status) { var parsed = Enum.Parse<PrescriptionVersionStatus>(status, true); rows = rows.Where(r => r.Status == parsed); }
        var total = await rows.LongCountAsync(ct);
        var result = await rows.OrderByDescending(r => r.StartedAtUtc).ThenBy(r => r.Id).Skip((q.PageNumber - 1) * q.PageSize).Take(q.PageSize).ToArrayAsync(ct);
        return new(result.Select(r => new DoctorPatientPrescription(r.Id, r.EncounterId, r.PracticeId, Utc(r.StartedAtUtc),
            r.VersionNumber, r.Status.ToString(), r.MedicationCount, r.HasVersions, true, a.Has(PermissionNames.PrescriptionsExportOwn), !r.IsActive)).ToArray(), total, q.PageNumber, q.PageSize);
    }
    private async Task<ClinicalPage<DoctorPatientAppointment>> AppointmentsAsync(DoctorPatientActor a, DoctorPatientHistoryQuery q, CancellationToken ct, bool upcoming = false)
    {
        var rows = Reservations(a).Where(r => r.PatientId == q.PatientId && (q.PracticeId == null || r.DoctorPracticeId == q.PracticeId) &&
            (q.FromDate == null || r.BusinessDate >= q.FromDate) && (q.ToDate == null || r.BusinessDate <= q.ToDate));
        if (upcoming) rows = rows.Where(r => r.Status == ReservationStatus.Active && r.ScheduledStartUtc > clock.UtcNow);
        if (q.Status is { } status) { var parsed = Enum.Parse<ReservationStatus>(status, true); rows = rows.Where(r => r.Status == parsed); }
        var total = await rows.LongCountAsync(ct);
        var ordered = upcoming ? rows.OrderBy(r => r.ScheduledStartUtc).ThenBy(r => r.Id) : rows.OrderByDescending(r => r.ScheduledStartUtc).ThenBy(r => r.Id);
        var result = await ordered.Skip((q.PageNumber - 1) * q.PageSize).Take(q.PageSize).Select(r => new
        {
            r.Id, r.DoctorPracticeId, r.BusinessDate, r.ScheduledStartUtc, r.VisitTypeCodeSnapshot, r.SegmentNameArSnapshot,
            r.Status, r.FollowUpEligibilityId, r.BookingSource,
            TicketId = db.TicketRegistry.Where(t => t.ReservationId == r.Id && t.DoctorId == a.DoctorId).Select(t => (Guid?)t.Id).SingleOrDefault(),
            IsArchived = db.ReservationRegistry.Any(i => i.Id == r.Id && i.IsArchived)
        }).ToArrayAsync(ct);
        return new(result.Select(r => new DoctorPatientAppointment(r.Id, r.DoctorPracticeId, r.BusinessDate, Utc(r.ScheduledStartUtc),
            r.VisitTypeCodeSnapshot, r.SegmentNameArSnapshot, r.Status.ToString(), r.TicketId, r.FollowUpEligibilityId, r.IsArchived,
            r.BookingSource.ToString())).ToArray(), total, q.PageNumber, q.PageSize);
    }
    private async Task<ClinicalPage<DoctorPatientFollowUp>> FollowUpsAsync(DoctorPatientActor a, DoctorPatientHistoryQuery q, CancellationToken ct)
    {
        var zones = await (from config in db.DoctorPracticeConfigurations.AsNoTracking()
            join p in db.DoctorPractices.AsNoTracking() on config.DoctorPracticeId equals p.Id
            where p.DoctorId == a.DoctorId select config.TimeZoneId).Distinct().ToArrayAsync(ct);
        var rows = from f in db.FollowUpEligibilities.AsNoTracking()
            join p in db.DoctorPractices.AsNoTracking() on f.DoctorPracticeId equals p.Id
            join config in db.DoctorPracticeConfigurations.AsNoTracking() on p.Id equals config.DoctorPracticeId
            where f.DoctorId == a.DoctorId && f.PatientId == q.PatientId && ReadablePractices(a).Contains(p.Id) && (q.PracticeId == null || p.Id == q.PracticeId)
            select new { f, p.IsActive, config.TimeZoneId };
        if (q.Status is { } status)
        {
            var parsed = Enum.Parse<FollowUpEligibilityStatus>(status, true);
            if (parsed is FollowUpEligibilityStatus.Available or FollowUpEligibilityStatus.Expired)
            {
                var filtered = rows.Where(r => false);
                foreach (var zone in zones)
                {
                    var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(clock.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(zone)));
                    var id = zone;
                    filtered = filtered.Concat(rows.Where(r => r.TimeZoneId == id && (parsed == FollowUpEligibilityStatus.Available
                        ? r.f.Status == FollowUpEligibilityStatus.Available && r.f.ValidUntil >= today
                        : r.f.Status == FollowUpEligibilityStatus.Expired || r.f.Status == FollowUpEligibilityStatus.Available && r.f.ValidUntil < today)));
                }
                rows = filtered;
            }
            else rows = rows.Where(r => r.f.Status == parsed);
        }
        var total = await rows.LongCountAsync(ct);
        var result = await rows.OrderByDescending(r => r.f.CreatedOnUtc).ThenBy(r => r.f.Id).Skip((q.PageNumber - 1) * q.PageSize).Take(q.PageSize).ToArrayAsync(ct);
        return new(result.Select(r => new DoctorPatientFollowUp(r.f.Id, r.f.SourceMedicalEncounterId, r.f.DoctorPracticeId,
            r.f.EffectiveStatus(DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(clock.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(r.TimeZoneId)))).ToString(),
            r.f.ValidUntil, r.f.ReservedReservationId, r.f.ReservedTicketId, r.f.ConsumedEncounterId,
            false, false, !r.IsActive)).ToArray(), total, q.PageNumber, q.PageSize);
    }
    public async Task<DoctorMyPatientOverviewResponse?> OverviewAsync(DoctorPatientActor a, Guid patientId, CancellationToken ct)
    {
        var patient = (await DirectoryCoreAsync(a, new(), patientId, ct)).Items.SingleOrDefault();
        if (patient is null) return null;
        var practiceIds = Attendances(a.DoctorId).Where(t => t.PatientId == patientId).Select(t => t.DoctorPracticeId);
        var practices = await db.DoctorPractices.AsNoTracking().Where(p => p.DoctorId == a.DoctorId && practiceIds.Contains(p.Id))
            .OrderBy(p => p.NameAr).ThenBy(p => p.Id).Select(p => new DoctorPatientPractice(p.Id, p.NameAr, p.NameEn, p.IsActive)).ToArrayAsync(ct);
        var cap = patient.Capabilities;
        DoctorPatientHistoryQuery Query(DoctorPatientHistory kind) => new(patientId, kind, PageSize: 1);
        var attendance = cap.CanViewAttendances ? First((await AttendanceAsync(a, Query(DoctorPatientHistory.Attendances), ct)).Items) : null;
        var diagnosis = cap.CanViewDiagnoses ? First((await DiagnosesAsync(a, Query(DoctorPatientHistory.Diagnoses), ct)).Items) : null;
        var rx = cap.CanViewPrescriptions ? First((await PrescriptionsAsync(a, Query(DoctorPatientHistory.Prescriptions), ct)).Items) : null;
        var next = cap.CanViewAppointments ? First((await AppointmentsAsync(a, Query(DoctorPatientHistory.Appointments), ct, true)).Items) : null;
        var followUp = cap.CanViewFollowUps ? First((await FollowUpsAsync(a, Query(DoctorPatientHistory.FollowUps), ct)).Items) : null;
        int? labs = cap.CanViewLabRequests ? await db.LabRequests.CountAsync(r => r.DoctorId == a.DoctorId && r.PatientId == patientId && ReadablePractices(a).Contains(r.DoctorPracticeId) && r.Status != DiagnosticRequestStatus.Draft, ct) : null;
        int? radio = cap.CanViewRadiologyRequests ? await db.RadiologyRequests.CountAsync(r => r.DoctorId == a.DoctorId && r.PatientId == patientId && ReadablePractices(a).Contains(r.DoctorPracticeId) && r.Status != DiagnosticRequestStatus.Draft, ct) : null;
        return new(patient, practices, attendance, diagnosis, rx, labs, radio, next, followUp);
    }
    public async Task<PrescriptionPdfDocument?> PdfDocumentAsync(Guid doctorId, Guid prescriptionId, int number, CancellationToken ct)
    {
        var row = await (from rx in db.Prescriptions.AsNoTracking().IgnoreAutoIncludes()
            join e in db.MedicalEncounters.AsNoTracking() on rx.MedicalEncounterId equals e.Id
            join p in db.DoctorPractices.AsNoTracking() on e.DoctorPracticeId equals p.Id
            join d in db.Doctors.AsNoTracking() on e.DoctorId equals d.Id
            join patient in db.Patients.AsNoTracking() on e.PatientId equals patient.Id
            where rx.Id == prescriptionId && rx.DoctorId == doctorId && e.DoctorId == doctorId && p.DoctorId == doctorId &&
                rx.PatientId == e.PatientId && e.Status == EncounterStatus.Completed
            select new { PracticeId = p.Id, e.PatientId, EncounterId = e.Id, DoctorName = d.NameAr, PracticeName = p.NameAr,
                PatientName = patient.NameAr, e.StartedAtUtc }).SingleOrDefaultAsync(ct);
        if (row is null) return null;
        var version = await db.PrescriptionVersions.AsNoTracking().Where(v => v.PrescriptionId == prescriptionId && v.VersionNumber == number &&
            (v.Status == PrescriptionVersionStatus.Finalized || v.Status == PrescriptionVersionStatus.Superseded || v.Status == PrescriptionVersionStatus.Voided) && v.FinalizedAtUtc != null).SingleOrDefaultAsync(ct);
        return version is null ? null : new(prescriptionId, row.PracticeId, row.PatientId, row.EncounterId,
            row.DoctorName, row.PracticeName, row.PatientName, Utc(row.StartedAtUtc), MedicationReadService.Version(version));
    }
    public async Task AuditAsync(DoctorPatientActor a, string action, Guid targetId, CancellationToken ct)
    {
        // Existing append-only, metadata-only audit store; no search terms, contacts or medical text.
        db.ArchiveAuditEvents.Add(new ArchiveAuditEvent(action, "DoctorPatients", targetId == Guid.Empty ? null : targetId, null, a.UserId, clock.UtcNow));
        await db.SaveChangesAsync(ct);
    }
    private static T? First<T>(IReadOnlyList<T> items) where T : class => items.Count == 0 ? null : items[0];
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
