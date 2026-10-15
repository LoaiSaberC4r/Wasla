#pragma warning disable CA1304, CA1311, CA1309 // EF SQL translates lower/compare; GUID hex keys have culture-independent ordering.
using BuildingBlock.Application.Time;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Clinical;
using Wasla.Application.Features.Medications;
using Wasla.Application.Media;
using Wasla.Domain.Clinical;
using Wasla.Domain.Diagnostics;
using Wasla.Domain.Medications;
using Wasla.Domain.Resources;
using Wasla.Domain.Tickets;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed record RecordCursor(string Scope, string Version, DateTime Snapshot, DateTime Time, int Kind, string Key);
internal sealed record TimelineCandidate
{
    public DateTime Time { get; init; }
    public int Kind { get; init; }
    public string Key { get; init; } = string.Empty;
    public Guid SourceId { get; init; }
    public Guid EncounterId { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool HasAttachments { get; init; }
}
internal sealed record HiddenCandidate
{
    public DateOnly Date { get; init; }
    public Guid Id { get; init; }
    public int Revision { get; init; }
    public string Key { get; init; } = string.Empty;
    public Guid? RequestId { get; init; }
    public HiddenAccessRequestStatus? Status { get; init; }
}
internal sealed record MediaReference(Guid EncounterId, string Key, string FileName, string ContentType, long SizeBytes);

internal sealed partial class MedicalRecordReadService(WaslaDbContext db, IMedicalRecordTokens tokens, IMedicalRecordAccessPolicy policy,
    IDateTimeProvider clock, MedicalRecordAuditWriter audit, IMedicationReadService medications, IPrivateMediaReader media)
    : IMedicalRecordReadService, IMedicalRecordMediaReader
{
    private IQueryable<MedicalEncounter> Completed(MedicalRecordScope s) => db.MedicalEncounters.AsNoTracking()
        .Where(e => e.PatientId == s.PatientId && e.Status == EncounterStatus.Completed &&
            db.AllTickets.Any(t => t.Id == e.TicketId && t.Status == TicketStatus.Completed && t.PatientId == e.PatientId &&
                t.DoctorId == e.DoctorId && t.DoctorPracticeId == e.DoctorPracticeId) &&
            db.DoctorPractices.Any(p => p.Id == e.DoctorPracticeId && p.DoctorId == e.DoctorId));
    private IQueryable<MedicalEncounter> Authorized(MedicalRecordScope s)
    {
        var q = Completed(s);
        if (s.Mode != MedicalRecordMode.TreatingDoctor) return q;
        return q.Where(e => e.DoctorId == s.DoctorId && db.DoctorPractices.Any(p => p.Id == e.DoctorPracticeId && p.IsActive) ||
            e.DoctorId != s.DoctorId && (e.Visibility == EncounterVisibility.Visible ||
            db.Set<HiddenEncounterAccessRequest>().Any(r => r.HiddenEncounterId == e.Id && r.PatientId == s.PatientId &&
                r.OwningDoctorId == e.DoctorId && r.RequestingDoctorId == s.DoctorId && r.RequestingPracticeId == s.PracticeId &&
                r.RequestingTicketId == s.TicketId && r.RequestingEncounterId == s.TreatingEncounterId &&
                r.Status == HiddenAccessRequestStatus.Approved && r.VisibilityRevisionAtApproval == e.VisibilityRevision)));
    }
    private static RecordCapabilities Capabilities(MedicalRecordScope s) => new(true, true, s.CanRequestHiddenAccess);
    private async Task<bool> RecheckAsync(MedicalRecordScope s, bool log, CancellationToken ct)
    {
        var fresh = await policy.ScopeAsync(s.Mode, s.PracticeId, s.TicketId, s.Mode == MedicalRecordMode.Dependent ? s.PatientId : null,
            MedicalRecordInput.Permission(s.Mode, log), ct);
        return fresh.IsSuccess && fresh.Value == s;
    }
    public async Task<object?> ReadAsync(ReadMedicalRecordQuery q, MedicalRecordScope s, CancellationToken ct)
    {
        var f = q.Filter ?? new MedicalRecordFilter();
        if (f.EncounterId.HasValue && !await Authorized(s).AnyAsync(e => e.Id == f.EncounterId, ct)) return null;
        object? value = q.Read switch
        {
            MedicalRecordRead.Overview => await OverviewAsync(s, f, ct),
            MedicalRecordRead.Timeline => await TimelineAsync(s, f, ct),
            MedicalRecordRead.Encounter => await EncounterAsync(s, q.EncounterId!.Value, ct),
            MedicalRecordRead.Item => await ItemAsync(s, q.ResourceType!, q.SourceId!.Value, ct),
            MedicalRecordRead.AccessLog when s.Mode == MedicalRecordMode.OwnPatient => await AccessLogAsync(s, f, ct),
            _ => null
        };
        value = value switch
        {
            MedicalRecordOverviewResponse overview => s.Mode == MedicalRecordMode.TreatingDoctor
                ? new DoctorSharedMedicalRecordOverviewResponse(overview) : new PatientSafeMedicalRecordOverviewResponse(overview),
            MedicalRecordEncounterDetailsResponse encounter => s.Mode == MedicalRecordMode.TreatingDoctor
                ? new DoctorSharedMedicalRecordEncounterResponse(encounter) : new PatientSafeMedicalRecordEncounterResponse(encounter),
            MedicalRecordItemDetailsResponse clinicalItem => s.Mode == MedicalRecordMode.TreatingDoctor
                ? new DoctorSharedMedicalRecordItemResponse(clinicalItem) : new PatientSafeMedicalRecordItemResponse(clinicalItem),
            _ => value
        };
        if (value is null || !await RecheckAsync(s, q.Read == MedicalRecordRead.AccessLog, ct)) return null;
        if (q.Read == MedicalRecordRead.Encounter && !await Authorized(s).AnyAsync(e => e.Id == q.EncounterId, ct)) return null;
        if (value is MedicalRecordItemDetailsResponse item && !await Authorized(s).AnyAsync(e => e.Id == item.EncounterId, ct)) return null;
        await audit.WriteAsync(s, q.Read.ToString(), q.ResourceType ?? "medical-record", q.SourceId, q.EncounterId, ct);
        return await RecheckAsync(s, q.Read == MedicalRecordRead.AccessLog, ct) ? value : null;
    }
    private async Task<MedicalRecordOverviewResponse?> OverviewAsync(MedicalRecordScope s, MedicalRecordFilter f, CancellationToken ct)
    {
        var version = await VersionAsync(s, ct);
        var patient = await db.Patients.AsNoTracking().Where(p => p.Id == s.PatientId).Select(p => new MedicalRecordPatient(p.Id, p.NameAr, p.NameEn, p.DateOfBirth, p.Gender)).SingleOrDefaultAsync(ct);
        if (patient is null) return null;
        var sources = Authorized(s); var events = Candidates(sources);
        var summary = new RecordSummary(await sources.CountAsync(ct), await events.CountAsync(e => e.Kind == 3, ct),
            await events.CountAsync(e => e.Kind == 5, ct), await events.CountAsync(e => e.Kind == 7, ct),
            s.Mode == MedicalRecordMode.TreatingDoctor ? await Completed(s).CountAsync(e => e.DoctorId != s.DoctorId && e.Visibility == EncounterVisibility.Hidden && !sources.Any(a => a.Id == e.Id), ct) : 0);
        var timeline = await TimelineAsync(s, f, ct);
        if (await VersionAsync(s, ct) != version) return null;
        return new(patient, new(s.Mode, s.TicketId, s.TreatingEncounterId, s.TicketId is null ? null : "InProgress"), summary,
            timeline, Capabilities(s), MedicalRecordInput.Filters);
    }
    private async Task<string> VersionAsync(MedicalRecordScope s, CancellationToken ct)
    {
        // Small aggregate watermark: changing any current clinical/visibility state invalidates old cursors.
        var e = await Completed(s).SumAsync(e => (long?)e.Revision + e.VisibilityRevision + 1, ct) ?? 0;
        var p = await db.Prescriptions.Where(p => p.PatientId == s.PatientId).SumAsync(p => (long?)p.Revision + 1, ct) ?? 0;
        var l = await db.LabRequests.Where(p => p.PatientId == s.PatientId).SumAsync(p => (long?)p.Revision + 1, ct) ?? 0;
        var lr = await db.LabResults.Where(p => p.PatientId == s.PatientId).SumAsync(p => (long?)p.Revision + 1, ct) ?? 0;
        var r = await db.RadiologyRequests.Where(p => p.PatientId == s.PatientId).SumAsync(p => (long?)p.Revision + 1, ct) ?? 0;
        var rr = await db.RadiologyResults.Where(p => p.PatientId == s.PatientId).SumAsync(p => (long?)p.Revision + 1, ct) ?? 0;
        var grants = s.TicketId.HasValue ? await db.Set<HiddenEncounterAccessRequest>().Where(r => r.RequestingTicketId == s.TicketId)
            .SumAsync(r => (long?)r.Status + 1, ct) ?? 0 : 0;
        return $"{e}:{p}:{l}:{lr}:{r}:{rr}:{grants}:{s.AccessVersion}";
    }
    private IQueryable<TimelineCandidate> Candidates(IQueryable<MedicalEncounter> sources)
    {
        var visits = sources.Select(e => new TimelineCandidate { Time = e.CompletedAtUtc!.Value, Kind = 1, Key = e.Id.ToString().ToLower(), SourceId = e.Id, EncounterId = e.Id, Status = "Current", HasAttachments = false });
        var diagnoses = from e in sources join d in db.Diagnoses on e.Id equals d.MedicalEncounterId where !d.IsVoided
            select new TimelineCandidate { Time = d.ModifiedOnUtc ?? e.CompletedAtUtc!.Value, Kind = 2, Key = d.Id.ToString().ToLower(), SourceId = d.Id, EncounterId = e.Id, Status = "Current", HasAttachments = false };
        var prescriptions = from e in sources join p in db.Prescriptions on e.Id equals p.MedicalEncounterId
            join v in db.PrescriptionVersions on p.Id equals v.PrescriptionId
            where p.PatientId == e.PatientId && p.DoctorId == e.DoctorId && v.Status == PrescriptionVersionStatus.Finalized
            select new TimelineCandidate { Time = v.FinalizedAtUtc!.Value, Kind = 3, Key = p.Id.ToString().ToLower(), SourceId = p.Id, EncounterId = e.Id, Status = v.VersionNumber > 1 ? "Corrected" : "Current", HasAttachments = false };
        var labs = from e in sources join r in db.LabRequests on e.Id equals r.MedicalEncounterId
            where r.PatientId == e.PatientId && r.DoctorId == e.DoctorId && r.DoctorPracticeId == e.DoctorPracticeId && r.Status != DiagnosticRequestStatus.Draft && r.RequestedAtUtc != null
            select new TimelineCandidate { Time = r.RequestedAtUtc!.Value, Kind = 4, Key = r.Id.ToString().ToLower(), SourceId = r.Id, EncounterId = e.Id, Status = "Issued", HasAttachments = false };
        var labResults = from e in sources join r in db.LabRequests on e.Id equals r.MedicalEncounterId
            join x in db.LabResults on r.Id equals x.RequestId join v in db.LabResultVersions on x.Id equals v.ResultId
            where r.PatientId == e.PatientId && r.DoctorId == e.DoctorId && r.DoctorPracticeId == e.DoctorPracticeId && r.Status != DiagnosticRequestStatus.Draft &&
                x.PatientId == e.PatientId && x.DoctorId == e.DoctorId && x.DoctorPracticeId == e.DoctorPracticeId &&
                v.VersionNumber == x.CurrentVersionNumber && v.Status == DiagnosticVersionStatus.Finalized && v.ReviewedByDoctorId == e.DoctorId
            select new TimelineCandidate { Time = v.ReviewedAtUtc, Kind = 5, Key = x.Id.ToString().ToLower(), SourceId = x.Id, EncounterId = e.Id, Status = v.VersionNumber > 1 ? "Corrected" : "Official", HasAttachments = true };
        var radiology = from e in sources join r in db.RadiologyRequests on e.Id equals r.MedicalEncounterId
            where r.PatientId == e.PatientId && r.DoctorId == e.DoctorId && r.DoctorPracticeId == e.DoctorPracticeId && r.Status != DiagnosticRequestStatus.Draft && r.RequestedAtUtc != null
            select new TimelineCandidate { Time = r.RequestedAtUtc!.Value, Kind = 6, Key = r.Id.ToString().ToLower(), SourceId = r.Id, EncounterId = e.Id, Status = "Issued", HasAttachments = false };
        var radioResults = from e in sources join r in db.RadiologyRequests on e.Id equals r.MedicalEncounterId
            join x in db.RadiologyResults on r.Id equals x.RequestId join v in db.RadiologyResultVersions on x.Id equals v.ResultId
            where r.PatientId == e.PatientId && r.DoctorId == e.DoctorId && r.DoctorPracticeId == e.DoctorPracticeId && r.Status != DiagnosticRequestStatus.Draft &&
                x.PatientId == e.PatientId && x.DoctorId == e.DoctorId && x.DoctorPracticeId == e.DoctorPracticeId &&
                v.VersionNumber == x.CurrentVersionNumber && v.Status == DiagnosticVersionStatus.Finalized && v.ReviewedByDoctorId == e.DoctorId
            select new TimelineCandidate { Time = v.ReviewedAtUtc, Kind = 7, Key = x.Id.ToString().ToLower(), SourceId = x.Id, EncounterId = e.Id, Status = v.VersionNumber > 1 ? "Corrected" : "Official", HasAttachments = true };
        return visits.Concat(diagnoses).Concat(prescriptions).Concat(labs).Concat(labResults).Concat(radiology).Concat(radioResults);
    }
    private async Task<RecordPage<TimelineEntry>> TimelineAsync(MedicalRecordScope s, MedicalRecordFilter f, CancellationToken ct)
    {
        var version = await VersionAsync(s, ct);
        var scope = $"{s.ActorId}:{s.PatientId}:{s.Mode}:{s.TicketId}:{f.Types}:{f.FromDate}:{f.ToDate}:{f.EncounterId}";
        var key = f.Cursor is null ? null : tokens.Read<RecordCursor>("Timeline", f.Cursor);
        if (f.Cursor is not null && (key is null || key.Scope != scope || key.Version != version || key.Snapshot < clock.UtcNow.AddHours(-1)))
            throw new ArgumentException("Invalid or stale cursor.", nameof(f));
        var snapshot = key?.Snapshot ?? clock.UtcNow;
        var sources = Authorized(s); if (f.EncounterId.HasValue) sources = sources.Where(e => e.Id == f.EncounterId);
        var query = Candidates(sources).Where(e => e.Time <= snapshot);
        var types = (f.Types ?? "all").Split(',').Select(x => x.Trim()).ToArray();
        if (!types.Contains("all", StringComparer.Ordinal)) query = query.Where(e => types.Contains("visits") && e.Kind == 1 ||
            types.Contains("diagnoses") && e.Kind == 2 || types.Contains("prescriptions") && e.Kind == 3 ||
            types.Contains("labs") && (e.Kind == 4 || e.Kind == 5) || types.Contains("radiology") && (e.Kind == 6 || e.Kind == 7));
        if (f.FromDate.HasValue) { var from = f.FromDate.Value.ToDateTime(TimeOnly.MinValue); query = query.Where(e => e.Time >= from); }
        if (f.ToDate.HasValue) { var through = f.ToDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue); query = query.Where(e => e.Time < through); }
        if (key is not null) query = query.Where(e => e.Time < key.Time || e.Time == key.Time && (e.Kind > key.Kind || e.Kind == key.Kind && string.Compare(e.Key, key.Key) > 0));
        var clinical = await query.OrderByDescending(e => e.Time).ThenBy(e => e.Kind).ThenBy(e => e.Key).Take(f.PageSize + 1).ToArrayAsync(ct);
        var hidden = new List<HiddenCandidate>();
        if (s.Mode == MedicalRecordMode.TreatingDoctor)
        {
            var hiddenQ = from e in Completed(s) join t in db.AllTickets on e.TicketId equals t.Id
                where !sources.Any(a => a.Id == e.Id) && e.DoctorId != s.DoctorId && e.Visibility == EncounterVisibility.Hidden &&
                    (!f.EncounterId.HasValue || e.Id == f.EncounterId) && e.CompletedAtUtc <= snapshot
                select new HiddenCandidate { Date = t.BusinessDate, Id = e.Id, Revision = e.VisibilityRevision, Key = e.Id.ToString().ToLower(), RequestId = db.Set<HiddenEncounterAccessRequest>().Where(r => r.HiddenEncounterId == e.Id && r.RequestingTicketId == s.TicketId && r.RequestingDoctorId == s.DoctorId).Select(r => (Guid?)r.Id).FirstOrDefault(), Status = db.Set<HiddenEncounterAccessRequest>().Where(r => r.HiddenEncounterId == e.Id && r.RequestingTicketId == s.TicketId && r.RequestingDoctorId == s.DoctorId).Select(r => (HiddenAccessRequestStatus?)(r.Status == HiddenAccessRequestStatus.Pending && r.VisibilityRevisionAtRequest != e.VisibilityRevision || r.Status == HiddenAccessRequestStatus.Approved && r.VisibilityRevisionAtApproval != e.VisibilityRevision ? HiddenAccessRequestStatus.Expired : r.Status)).FirstOrDefault() };
            if (f.FromDate.HasValue) hiddenQ = hiddenQ.Where(e => e.Date >= f.FromDate);
            if (f.ToDate.HasValue) hiddenQ = hiddenQ.Where(e => e.Date <= f.ToDate);
            if (key is not null)
            {
                var date = DateOnly.FromDateTime(key.Time); var afterMidnight = key.Time.TimeOfDay != TimeSpan.Zero;
                hiddenQ = hiddenQ.Where(e => e.Date < date || e.Date == date && (afterMidnight || key.Kind == 0 && string.Compare(e.Key, key.Key) > 0));
            }
            hidden.AddRange(await hiddenQ.OrderByDescending(e => e.Date).ThenBy(e => e.Key).Take(f.PageSize + 1).ToArrayAsync(ct));
        }
        var merged = clinical.Select(e => (e.Time, e.Kind, e.Key, Entry: (TimelineEntry)Map(e)))
            .Concat(hidden.Select(e => (e.Date.ToDateTime(TimeOnly.MinValue), 0, e.Key, Entry: (TimelineEntry)MapHidden(e, s))))
            .OrderByDescending(e => e.Item1).ThenBy(e => e.Item2).ThenBy(e => e.Key, StringComparer.Ordinal).Take(f.PageSize + 1).ToArray();
        var page = merged.Take(f.PageSize).ToArray(); var more = merged.Length > f.PageSize;
        // No state changes between watermark and disclosure are accepted; refresh restarts pagination.
        if (await VersionAsync(s, ct) != version) throw new ArgumentException("Record changed while paging.", nameof(f));
        return new(page.Select(e => e.Entry).ToArray(), more ? tokens.Protect("Timeline", new RecordCursor(scope, version, snapshot, page[^1].Item1, page[^1].Item2, page[^1].Key)) : null, more);
    }
    private static ClinicalTimelineEntry Map(TimelineCandidate e)
    {
        var (type, resource, title) = e.Kind switch
        {
            1 => ("EncounterCompleted", "encounter", "MedicalRecord.VisitTitle"), 2 => ("DiagnosisCurrent", "diagnosis", "MedicalRecord.DiagnosisTitle"),
            3 => ("PrescriptionFinalized", "prescription", "MedicalRecord.PrescriptionTitle"), 4 => ("LabRequestIssued", "lab-request", "MedicalRecord.LabOrderTitle"),
            5 => ("LabResultPublished", "lab-result", "MedicalRecord.LabResultTitle"), 6 => ("RadiologyRequestIssued", "radiology-request", "MedicalRecord.RadiologyOrderTitle"),
            _ => ("RadiologyResultPublished", "radiology-result", "MedicalRecord.RadiologyResultTitle")
        };
        return new(type, $"{e.Kind}:{e.Key}", DateTime.SpecifyKind(e.Time, DateTimeKind.Utc), e.SourceId, e.EncounterId, resource, e.Status, ErrorMessage.GetString(title), new(true, e.HasAttachments));
    }
    private HiddenTimelineEntry MapHidden(HiddenCandidate e, MedicalRecordScope s)
    {
        var token = tokens.Protect("HiddenEncounter", new HiddenEncounterToken(s.ActorId, s.DoctorId!.Value, s.TicketId!.Value, e.Id, e.Revision, clock.UtcNow));
        return new(tokens.OpaqueEntryId($"{s.ActorId}:{s.TicketId}:{e.Id}"), e.Date, true, token,
            new(e.Status == HiddenAccessRequestStatus.Approved ? "Expired" : e.Status?.ToString() ?? "NotRequested", e.RequestId), new(e.RequestId is null && s.CanRequestHiddenAccess));
    }
    private async Task<MedicalRecordEncounterDetailsResponse?> EncounterAsync(MedicalRecordScope s, Guid id, CancellationToken ct)
    {
        var row = await (from e in Authorized(s) join d in db.Doctors on e.DoctorId equals d.Id
            join p in db.DoctorPractices on e.DoctorPracticeId equals p.Id join t in db.AllTickets on e.TicketId equals t.Id
            where e.Id == id select new { e.Id, t.BusinessDate, Doctor = new ClinicalPartyResponse(d.Id, d.NameAr, d.NameEn), Practice = new ClinicalPartyResponse(p.Id, p.NameAr, p.NameEn) }).SingleOrDefaultAsync(ct);
        if (row is null) return null;
        var diagnoses = await db.Diagnoses.AsNoTracking().Where(d => d.MedicalEncounterId == id && !d.IsVoided).OrderBy(d => d.Type).ThenBy(d => d.Id)
            .Select(d => new SafeDiagnosis(d.Id, d.Type.ToString(), d.DisplayText)).ToArrayAsync(ct);
        var outputs = await TimelineAsync(s, new(PageSize: 100, EncounterId: id), ct);
        var entries = outputs.Items.OfType<ClinicalTimelineEntry>().ToArray();
        return new(id, row.BusinessDate, row.Doctor, row.Practice, diagnoses, outputs, Capabilities(s),
            entries.FirstOrDefault(e => e.ResourceType == "prescription"), entries.Where(e => e.ResourceType == "lab-request").ToArray(),
            entries.Where(e => e.ResourceType == "lab-result").ToArray(), entries.Where(e => e.ResourceType == "radiology-request").ToArray(),
            entries.Where(e => e.ResourceType == "radiology-result").ToArray());
    }
    private async Task<MedicalRecordItemDetailsResponse?> ItemAsync(MedicalRecordScope s, string type, Guid id, CancellationToken ct)
    {
        if (type == "prescription")
        {
            var encounter = await (from e in Authorized(s) join p in db.Prescriptions on e.Id equals p.MedicalEncounterId
                where p.Id == id && p.PatientId == e.PatientId && p.DoctorId == e.DoctorId select (Guid?)e.Id).SingleOrDefaultAsync(ct);
            if (encounter is null) return null;
            var prescription = await medications.PatientPrescriptionAsync(s.PatientId, id, ct);
            if (prescription is null) return null;
            return new(type, id, encounter.Value, prescription.Status == PrescriptionVersionStatus.Voided ? "Voided" : prescription.VersionNumber > 1 ? "Corrected" : "Current",
                prescription.VersionNumber, new SafePrescriptionContent(prescription.Status == PrescriptionVersionStatus.Finalized ? prescription.Items : []), [], Capabilities(s));
        }
        return await DiagnosticItemAsync(s, type, id, ct);
    }
    private async Task<RecordPage<PatientSafeAccessLog>> AccessLogAsync(MedicalRecordScope s, MedicalRecordFilter f, CancellationToken ct)
    {
        var key = f.Cursor is null ? null : tokens.Read<RequestCursor>("AccessLog", f.Cursor);
        if (f.Cursor is not null && (key is null || key.Scope != $"{s.ActorId}:{s.PatientId}")) throw new ArgumentException("Invalid cursor.", nameof(f));
        var query = db.Set<MedicalRecordAccessAudit>().AsNoTracking().Where(a => a.PatientId == s.PatientId);
        if (key is not null) query = query.Where(a => a.OccurredAtUtc < key.Time || a.OccurredAtUtc == key.Time && a.Id.CompareTo(key.Id) > 0);
        var rows = await query.OrderByDescending(a => a.OccurredAtUtc).ThenBy(a => a.Id).Take(f.PageSize + 1)
            .Select(a => new PatientSafeAccessLog(a.Id, a.ActorCategory, a.Action, DateTime.SpecifyKind(a.OccurredAtUtc, DateTimeKind.Utc))).ToArrayAsync(ct);
        var page = rows.Take(f.PageSize).ToArray(); var more = rows.Length > f.PageSize;
        return new(page, more ? tokens.Protect("AccessLog", new RequestCursor($"{s.ActorId}:{s.PatientId}", page[^1].OccurredAtUtc, page[^1].EntryId)) : null, more);
    }
    public async Task<PrivateMedia?> ReadAsync(ReadMedicalRecordMediaQuery q, MedicalRecordScope s, CancellationToken ct)
    {
        var reference = await MediaReferenceAsync(s, q.ResourceType, q.SourceId, q.AttachmentId, ct);
        if (reference is null || reference.SizeBytes is <= 0 or > 25 * 1024 * 1024 || !await RecheckAsync(s, false, ct)) return null;
        // Re-resolve the exact current version and visibility immediately before opening the private stream.
        if (await MediaReferenceAsync(s, q.ResourceType, q.SourceId, q.AttachmentId, ct) != reference) return null;
        PrivateMedia? opened;
        try { opened = await media.OpenAsync(reference.Key, ct); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
        if (opened is null) return null;
        if (opened.Content.CanSeek && opened.Content.Length != reference.SizeBytes)
        { await opened.Content.DisposeAsync(); return null; }
        try { await audit.WriteAsync(s, "MediaRead", q.ResourceType, q.SourceId, reference.EncounterId, ct); }
        catch { await opened.Content.DisposeAsync(); throw; }
        if (!await RecheckAsync(s, false, ct) || await MediaReferenceAsync(s, q.ResourceType, q.SourceId, q.AttachmentId, ct) != reference)
        { await opened.Content.DisposeAsync(); return null; }
        return new(opened.Content, reference.ContentType, SafeFileName(reference.FileName));
    }
    private static string SafeFileName(string original)
    {
        var filename = Path.GetFileName(original.Replace('\\', '/'));
        filename = new string(filename.Select(c => char.IsControl(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(filename) ? "medical-record" : filename;
    }
}
