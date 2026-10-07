
using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Diagnostics;
using Wasla.Application.Features.Clinical;
using Wasla.Application.Features.Governance;
using Wasla.Domain.Diagnostics;
using Wasla.Domain.Labs;
using Wasla.Domain.Radiology;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class DiagnosticReadService(WaslaDbContext db) : IDiagnosticReadService, IDiagnosticCoverageReader, IDiagnosticMediaReferenceReader, IMedicalCatalogManagerReader
{
    private static int Page(int value) => Math.Clamp(value, 1, 1000000);
    private static int Size(int value) => Math.Clamp(value, 1, 100);
    private static bool Has(IReadOnlyList<string> permissions, string name) => permissions.Contains(name, StringComparer.OrdinalIgnoreCase);
    public async Task<IReadOnlySet<string>> RetainedKeysAsync(IReadOnlyList<string> keys, CancellationToken ct)
    {
        var retained = await db.LabResultAttachments.AsNoTracking().Where(x => keys.Contains(x.PrivateMediaKey)).Select(x => x.PrivateMediaKey).ToListAsync(ct);
        retained.AddRange(await db.RadiologyResultAttachments.AsNoTracking().Where(x => keys.Contains(x.PrivateMediaKey)).Select(x => x.PrivateMediaKey).ToArrayAsync(ct));
        retained.AddRange(await db.PatientLabResultSubmissionAttachments.AsNoTracking().Where(x => keys.Contains(x.PrivateMediaKey)).Select(x => x.PrivateMediaKey).ToArrayAsync(ct));
        retained.AddRange(await db.PatientRadiologyResultSubmissionAttachments.AsNoTracking().Where(x => keys.Contains(x.PrivateMediaKey)).Select(x => x.PrivateMediaKey).ToArrayAsync(ct));
        return retained.ToHashSet(StringComparer.Ordinal);
    }
    public async Task AuditReadAsync(DiagnosticKind kind, DiagnosticReadResource resource, Guid id, Guid? attachmentId, Guid actor, DateTime nowUtc, CancellationToken ct)
    {
        Guid? requestId = resource is DiagnosticReadResource.Results or DiagnosticReadResource.Versions
            ? kind == DiagnosticKind.Lab ? await db.LabResults.Where(x => x.Id == id).Select(x => (Guid?)x.RequestId).SingleOrDefaultAsync(ct)
                : await db.RadiologyResults.Where(x => x.Id == id).Select(x => (Guid?)x.RequestId).SingleOrDefaultAsync(ct)
            : resource == DiagnosticReadResource.Submissions
                ? kind == DiagnosticKind.Lab ? await db.PatientLabResultSubmissions.Where(x => x.Id == id).Select(x => (Guid?)x.RequestId).SingleOrDefaultAsync(ct)
                    : await db.PatientRadiologyResultSubmissions.Where(x => x.Id == id).Select(x => (Guid?)x.RequestId).SingleOrDefaultAsync(ct)
                : id;
        var encounter = kind == DiagnosticKind.Lab ? await db.LabRequests.Where(x => x.Id == requestId).Select(x => (Guid?)x.MedicalEncounterId).SingleOrDefaultAsync(ct)
            : await db.RadiologyRequests.Where(x => x.Id == requestId).Select(x => (Guid?)x.MedicalEncounterId).SingleOrDefaultAsync(ct);
        if (encounter is null) return;
        db.EncounterAuditEvents.Add(Wasla.Domain.Clinical.EncounterAuditEvent.RecordDiagnosticRead(encounter.Value,
            kind + (attachmentId.HasValue ? "AttachmentDownloaded" : resource + "Read"), attachmentId ?? id, actor, nowUtc));
        await db.SaveChangesAsync(ct);
    }
    public async Task<ClinicalPage<MedicalCatalogManagerResponse>> ListAsync(string? search, int page, int size, CancellationToken ct)
    {
        var q = db.ApplicationUsers.AsNoTracking().Where(u => u.UserType == Wasla.Domain.Common.UserType.MedicalCatalogManager);
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(u => u.UserName.Contains(search) || u.Email.Contains(search));
        return new(await q.OrderBy(u => u.UserName).ThenBy(u => u.Id).Skip((Page(page) - 1) * Size(size)).Take(Size(size))
            .Select(u => new MedicalCatalogManagerResponse(u.Id, u.UserName, u.Email, u.PhoneNumber, u.IsActive, u.IsFirstLogin, u.CreatedOnUtc)).ToArrayAsync(ct), await q.LongCountAsync(ct), Page(page), Size(size));
    }
    public Task<ClinicalPage<MedicalCatalogResponse>> CatalogAsync(SearchMedicalCatalogQuery q, IReadOnlyList<string> p, CancellationToken ct)
        => q.Kind == DiagnosticKind.Lab ? LabCatalogAsync(q, p, ct) : RadiologyCatalogAsync(q, p, ct);
    public Task<ClinicalPage<MedicalCatalogRequestResponse>> CatalogRequestsAsync(ListMedicalCatalogRequestsQuery q, Guid? doctor, IReadOnlyList<string> p, CancellationToken ct)
        => q.Kind == DiagnosticKind.Lab ? LabCatalogRequestsAsync(q, doctor, p, ct) : RadiologyCatalogRequestsAsync(q, doctor, p, ct);
    public Task<object?> ReadAsync(ReadDiagnosticQuery q, Guid owner, IReadOnlyList<string> p, CancellationToken ct)
        => q.Kind == DiagnosticKind.Lab ? ReadLabAsync(q, owner, p, ct) : ReadRadiologyAsync(q, owner, p, ct);
    public Task<DiagnosticRequestStateResponse?> RequestAsync(DiagnosticKind k, Guid id, Guid owner, bool patient, IReadOnlyList<string> p, CancellationToken ct)
        => k == DiagnosticKind.Lab ? LabRequestAsync(id, owner, patient, p, ct) : RadiologyRequestAsync(id, owner, patient, p, ct);
    public Task<DiagnosticResultResponse?> ResultAsync(DiagnosticKind k, Guid id, Guid owner, bool patient, IReadOnlyList<string> p, CancellationToken ct)
        => !Has(p, k + (patient ? "Results.ViewOwnCurrent" : "Results.ViewOwn")) ? Task.FromResult<DiagnosticResultResponse?>(null) :
            k == DiagnosticKind.Lab ? LabResultAsync(id, owner, patient, p, ct) : RadiologyResultAsync(id, owner, patient, p, ct);
    public Task<DiagnosticSubmissionResponse?> SubmissionAsync(DiagnosticKind k, Guid id, Guid owner, bool patient, IReadOnlyList<string> p, CancellationToken ct)
        => !Has(p, k + "ResultSubmissions.ViewOwn") ? Task.FromResult<DiagnosticSubmissionResponse?>(null) :
            k == DiagnosticKind.Lab ? LabSubmissionAsync(id, owner, patient, p, ct) : RadiologySubmissionAsync(id, owner, patient, p, ct);
    public Task<string?> MediaKeyAsync(DiagnosticMediaQuery q, Guid owner, CancellationToken ct)
        => q.Kind == DiagnosticKind.Lab ? LabMediaAsync(q, owner, ct) : RadiologyMediaAsync(q, owner, ct);
    public Task<object?> ImportAsync(ReadDiagnosticImportQuery q, IReadOnlyList<string> p, CancellationToken ct)
        => q.Kind == DiagnosticKind.Lab ? LabImportsAsync(q, p, ct) : RadiologyImportsAsync(q, p, ct);
    public Task<IReadOnlySet<Guid>> CurrentCoverageAsync(DiagnosticKind k, Guid id, CancellationToken ct)
        => k == DiagnosticKind.Lab ? LabCoverageAsync(id, ct) : RadiologyCoverageAsync(id, ct);
    private async Task<DiagnosticRequestStateResponse?> EmptyAsync(Guid doctorId, Guid encounterId, Guid? practiceId, DiagnosticKind k, IReadOnlyList<string> p, CancellationToken ct)
    {
        var row = await (from e in db.MedicalEncounters.AsNoTracking()
                         join d in db.Doctors on e.DoctorId equals d.Id
                         join practice in db.DoctorPractices on e.DoctorPracticeId equals practice.Id
                         where e.Id == encounterId && e.DoctorId == doctorId && practice.IsActive && (practiceId == null || e.DoctorPracticeId == practiceId)
                         select new { e.Id, e.Status, Doctor = new ClinicalPartyResponse(d.Id, d.NameAr, d.NameEn), Practice = new ClinicalPartyResponse(practice.Id, practice.NameAr, practice.NameEn) }).SingleOrDefaultAsync(ct);
        return row is null ? null : new(null, row.Id, row.Doctor, row.Practice, DiagnosticOrigin.DuringEncounter, null, null, null, null, [],
            new(row.Status == Wasla.Domain.Clinical.EncounterStatus.InProgress && Has(p, k + "Requests.ManageOwnDraft"), false, false), [], []);
    }

    private async Task<ClinicalPage<MedicalCatalogResponse>> LabCatalogAsync(SearchMedicalCatalogQuery r, IReadOnlyList<string> p, CancellationToken ct)
    {
        var q = db.Set<LabTestCatalog>().IgnoreAutoIncludes().AsNoTracking().AsQueryable();
        if (r.Id is { } id) q = q.Where(c => c.Id == id);
        if (r.DoctorSearch) q = q.Where(c => c.Status == MedicalCatalogStatus.Active && (c.Source == MedicalCatalogSource.Wasla || c.ExternalStatus == "ACTIVE"));
        else if (r.Status is { } status) q = q.Where(c => c.Status == status);
        if (r.Source is { } source) q = q.Where(c => c.Source == source);
        if (r.HasArabic is { } ar) q = q.Where(c => (c.DisplayNameAr != null || c.OfficialNameAr != null) == ar);
        if (r.CommonOnly) q = q.Where(c => c.IsCommonOrder);
        var term = DiagnosticText.Normalize(r.Search);
        if (term.Length > 0) q = q.Where(c => c.NormalizedSearch.Contains(term) || db.Set<LabTestCatalog>().Any(old => old.MergedIntoId == c.Id && old.NormalizedSearch.Contains(term)));
        var count = await q.LongCountAsync(ct); var page = Page(r.PageNumber); var size = Size(r.PageSize);
        var rows = await q.OrderByDescending(c => c.IsCommonOrder).ThenByDescending(c => c.LoincCode == term).ThenBy(c => c.NormalizedName).ThenBy(c => c.Id)
            .Skip((page - 1) * size).Take(size).ToArrayAsync(ct);
        IReadOnlyList<DiagnosticHistoryResponse>? history = r.Id.HasValue && !r.DoctorSearch ? await db.Set<LabTestCatalogHistory>().AsNoTracking().Where(h => h.ResourceId == r.Id)
            .OrderBy(h => h.OccurredAtUtc).ThenBy(h => h.Id).Select(h => new DiagnosticHistoryResponse(h.Id, h.Action, h.Reason, h.BeforeSnapshot, h.AfterSnapshot, h.ActorUserId, h.ActorType, h.OccurredAtUtc)).ToArrayAsync(ct) : null;
        return new(rows.Select(c => MapLabCatalog(c, p, r.DoctorSearch, history)).ToArray(), count, page, size);
    }
    private static MedicalCatalogResponse MapLabCatalog(LabTestCatalog c, IReadOnlyList<string> p, bool doctor, IReadOnlyList<DiagnosticHistoryResponse>? history = null)
        => new(c.Id, c.Source, c.Status, c.LoincCode, c.NameEn, c.NameAr, c.OfficialNameEn, c.OfficialNameAr, c.ExternalStatus, c.SourceVersion, c.IsCommonOrder,
            doctor ? c.Presentation() with { InternalNote = null } : c.Presentation(), doctor ? null : c.SourceDataJson, c.AttributesJson, c.MergedIntoId, Convert.ToBase64String(c.RowVersion),
            new(!doctor && c.Status != MedicalCatalogStatus.Merged && Has(p, "LabCatalog.Update"), !doctor && c.Status == MedicalCatalogStatus.Inactive && (c.Source == MedicalCatalogSource.Wasla || c.ExternalStatus == "ACTIVE") && Has(p, "LabCatalog.Activate"),
                !doctor && c.Status == MedicalCatalogStatus.Active && Has(p, "LabCatalog.Deactivate"), !doctor && c.Status != MedicalCatalogStatus.Merged && Has(p, "LabCatalog.Merge")), history);
    private async Task<ClinicalPage<MedicalCatalogRequestResponse>> LabCatalogRequestsAsync(ListMedicalCatalogRequestsQuery r, Guid? doctorId, IReadOnlyList<string> p, CancellationToken ct)
    {
        var q = db.Set<LabCatalogRequest>().IgnoreAutoIncludes().AsNoTracking().AsQueryable();
        if (r.Own) q = q.Where(x => x.RequestedByDoctorId == doctorId); else if (r.DoctorId is { } d) q = q.Where(x => x.RequestedByDoctorId == d);
        if (r.Id is { } id) q = q.Where(x => x.Id == id); if (r.Status is { } s) q = q.Where(x => x.Status == s);
        if (!string.IsNullOrWhiteSpace(r.Search)) q = q.Where(x => x.Name.Contains(r.Search));
        var count = await q.LongCountAsync(ct); var page = Page(r.PageNumber); var size = Size(r.PageSize);
        var rows = await q.OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip((page - 1) * size).Take(size).ToArrayAsync(ct);
        var history = r.Id.HasValue ? await db.Set<LabCatalogRequestHistory>().AsNoTracking().Where(h => h.ResourceId == r.Id).OrderBy(h => h.OccurredAtUtc)
            .Select(h => new DiagnosticHistoryResponse(h.Id, h.Action, h.Reason, h.BeforeSnapshot, h.AfterSnapshot, h.ActorUserId, h.ActorType, h.OccurredAtUtc)).ToArrayAsync(ct) : [];
        var canonicalIds = rows.Where(x => x.CanonicalCatalogId.HasValue).Select(x => x.CanonicalCatalogId!.Value).Distinct().ToArray();
        var catalogs = await db.Set<LabTestCatalog>().IgnoreAutoIncludes().AsNoTracking().Where(c => canonicalIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        return new(rows.Select(x => new MedicalCatalogRequestResponse(x.Id, x.RequestedByDoctorId, x.Name, x.Specimen, x.CatalogClarificationNote, x.Status, x.CanonicalCatalogId, x.ReasonType, x.ReviewReason, x.CreatedAtUtc,
            Convert.ToBase64String(x.RowVersion), new(r.Own && x.Status is MedicalCatalogRequestStatus.Pending or MedicalCatalogRequestStatus.NeedsMoreInfo && Has(p, "LabCatalogRequests.UpdateOwn"),
                !r.Own && x.Status == MedicalCatalogRequestStatus.Pending && Has(p, "LabCatalogRequests.Review"), !r.Own && x.Status == MedicalCatalogRequestStatus.Pending && Has(p, "LabCatalogRequests.Review"), !r.Own && x.Status == MedicalCatalogRequestStatus.Pending && Has(p, "LabCatalogRequests.Review")), history,
                x.CanonicalCatalogId is { } c && catalogs.TryGetValue(c, out var catalog) ? MapLabCatalog(catalog, p, r.Own) : null)).ToArray(), count, page, size);
    }
    private IQueryable<LabRequest> LabRequests(Guid owner, bool patient)
        => db.Set<LabRequest>().IgnoreAutoIncludes().AsNoTracking().Where(x => patient ? x.PatientId == owner && x.Status != DiagnosticRequestStatus.Draft : x.DoctorId == owner && db.DoctorPractices.Any(p => p.Id == x.DoctorPracticeId && p.IsActive));
    private IQueryable<LabResult> LabResults(Guid owner, bool patient)
        => db.Set<LabResult>().IgnoreAutoIncludes().AsNoTracking().Where(x => patient ? x.PatientId == owner && db.Set<LabResultVersion>().Any(v => v.ResultId == x.Id && v.Status == DiagnosticVersionStatus.Finalized) : x.DoctorId == owner && db.DoctorPractices.Any(p => p.Id == x.DoctorPracticeId && p.IsActive));
    private IQueryable<PatientLabResultSubmission> LabSubmissions(Guid owner, bool patient)
        => db.Set<PatientLabResultSubmission>().IgnoreAutoIncludes().AsNoTracking().Where(x => patient ? x.PatientId == owner : x.DoctorId == owner && db.DoctorPractices.Any(p => p.Id == x.DoctorPracticeId && p.IsActive));
    private async Task<DiagnosticRequestStateResponse?> LabRequestAsync(Guid id, Guid owner, bool patient, IReadOnlyList<string> p, CancellationToken ct)
    {
        var r = await LabRequests(owner, patient).SingleOrDefaultAsync(x => x.Id == id, ct); if (r is null) return null;
        var d = await db.Doctors.AsNoTracking().Where(x => x.Id == r.DoctorId).Select(x => new ClinicalPartyResponse(x.Id, x.NameAr, x.NameEn)).SingleAsync(ct);
        var practice = await db.DoctorPractices.AsNoTracking().Where(x => x.Id == r.DoctorPracticeId).Select(x => new ClinicalPartyResponse(x.Id, x.NameAr, x.NameEn)).SingleAsync(ct);
        var items = await db.Set<LabRequestItem>().AsNoTracking().Where(x => x.RequestId == id).OrderBy(x => x.Id).ToArrayAsync(ct);
        var results = await LabResults(owner, patient).Where(x => x.RequestId == id).Include(x => x.Versions.Where(v => v.Status == DiagnosticVersionStatus.Finalized)).ThenInclude(v => v.Attachments)
            .Include(x => x.Versions.Where(v => v.Status == DiagnosticVersionStatus.Finalized)).ThenInclude(v => v.Coverage).AsSplitQuery().ToArrayAsync(ct);
        var submissions = await LabSubmissions(owner, patient).Where(x => x.RequestId == id).OrderBy(x => x.SubmittedAtUtc)
            .Select(x => new DiagnosticSubmissionSummary(x.Id, x.RequestId, x.Status, x.SubmittedAtUtc)).ToArrayAsync(ct);
        var draft = r.Status == DiagnosticRequestStatus.Draft;
        var manage = !patient && draft && Has(p, "LabRequests.ManageOwnDraft"); var cancel = !patient && !draft && Has(p, "LabRequests.CancelOwn");
        return new(r.Id, r.MedicalEncounterId, d, practice, r.Origin, r.Status, r.PatientInstructions, r.RequestedAtUtc, Convert.ToBase64String(r.RowVersion),
            items.Select(i => new DiagnosticItemResponse(i.Id, i.NameArSnapshot, i.NameEnSnapshot, i.LoincCodeSnapshot, i.ModalitySnapshot, i.AnatomicLocationSnapshot, i.LateralitySnapshot, i.DoctorInstructions, i.Status, i.CancellationReason,
                new(manage, manage, cancel && i.Status == DiagnosticItemStatus.Requested, i.Status == DiagnosticItemStatus.Completed), patient ? null : i.CatalogId, patient ? null : i.CatalogRequestId)).ToArray(),
            new(manage, cancel && items.Any(i => i.Status == DiagnosticItemStatus.Requested), !patient && !draft && items.Any(i => i.Status == DiagnosticItemStatus.Requested) && Has(p, "LabResults.UploadOwn")),
            Has(p, patient ? "LabResults.ViewOwnCurrent" : "LabResults.ViewOwn") ? results.Where(x => x.Current is not null).Select(x => MapLabResult(x, p, patient)).ToArray() : [],
            Has(p, "LabResultSubmissions.ViewOwn") ? submissions : [], patient ? null : r.PostVisitReason);
    }
    private static DiagnosticVersionResponse MapLabVersion(LabResultVersion v, bool patient)
        => new(v.Id, v.VersionNumber, v.Status, v.ExternalProviderName, v.ExternalReportDate, v.OriginallyUploadedBy, v.OriginallyUploadedAtUtc, v.ReviewedByDoctorId, v.ReviewedAtUtc,
            v.Coverage.Select(c => c.RequestItemId).ToArray(), v.Attachments.Select(a => new DiagnosticAttachmentResponse(a.Id, a.OriginalFileName, a.ContentType, a.SizeBytes, a.Sha256, a.UploadedAtUtc, a.Kind)).ToArray(), patient ? null : v.CorrectionReason, patient ? null : v.VoidReason);
    private static DiagnosticResultResponse MapLabResult(LabResult x, IReadOnlyList<string> p, bool patient)
        => new(x.Id, x.RequestId, x.CurrentVersionNumber, x.Current is null ? null : MapLabVersion(x.Current, patient), Convert.ToBase64String(x.RowVersion),
            new(!patient && x.Current is not null && Has(p, "LabResults.CorrectOwn"), !patient && x.Current is not null && Has(p, "LabResults.VoidOwn")));
    private async Task<DiagnosticResultResponse?> LabResultAsync(Guid id, Guid owner, bool patient, IReadOnlyList<string> p, CancellationToken ct)
    {
        var x = await LabResults(owner, patient).Where(x => x.Id == id).Include(x => x.Versions.Where(v => v.Status == DiagnosticVersionStatus.Finalized)).ThenInclude(v => v.Attachments)
            .Include(x => x.Versions.Where(v => v.Status == DiagnosticVersionStatus.Finalized)).ThenInclude(v => v.Coverage).AsSplitQuery().SingleOrDefaultAsync(ct);
        return x is null ? null : MapLabResult(x, p, patient);
    }
    private async Task<DiagnosticSubmissionResponse?> LabSubmissionAsync(Guid id, Guid owner, bool patient, IReadOnlyList<string> p, CancellationToken ct)
    {
        var x = await LabSubmissions(owner, patient).Include(x => x.Attachments).SingleOrDefaultAsync(x => x.Id == id, ct); if (x is null) return null;
        var requested = await db.Set<LabRequestItem>().AnyAsync(i => i.RequestId == x.RequestId && i.Status == DiagnosticItemStatus.Requested, ct);
        var pending = x.Status == PatientSubmissionStatus.PendingReview;
        return new(x.Id, x.RequestId, x.Status, x.ExternalProviderName, x.ExternalReportDate, x.PatientNote, x.PatientVisibleReason, x.AcceptedResultId, x.SubmittedAtUtc, Convert.ToBase64String(x.RowVersion),
            x.Attachments.Select(a => new DiagnosticAttachmentResponse(a.Id, a.OriginalFileName, a.ContentType, a.SizeBytes, a.Sha256, a.UploadedAtUtc, a.Kind)).ToArray(),
            new(patient && pending && Has(p, "LabResultSubmissions.WithdrawOwn"), !patient && pending && requested && Has(p, "LabResultSubmissions.ReviewOwn"), !patient && pending && Has(p, "LabResultSubmissions.ReviewOwn")));
    }
    private async Task<object?> ReadLabAsync(ReadDiagnosticQuery r, Guid owner, IReadOnlyList<string> p, CancellationToken ct)
    {
        var f = r.Filter ?? new(); var page = Page(f.PageNumber); var size = Size(f.PageSize);
        if (r.Resource == DiagnosticReadResource.Draft)
        {
            var id = await LabRequests(owner, false).Where(x => x.MedicalEncounterId == r.EncounterId && x.DoctorPracticeId == r.PracticeId && x.Status == DiagnosticRequestStatus.Draft).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
            return id is { } draft ? await LabRequestAsync(draft, owner, false, p, ct) : await EmptyAsync(owner, r.EncounterId.GetValueOrDefault(), r.PracticeId, DiagnosticKind.Lab, p, ct);
        }
        if (r.Resource == DiagnosticReadResource.Requests)
        {
            if (r.Id is { } id) return await LabRequestAsync(id, owner, r.Patient, p, ct);
            var q = LabRequests(owner, r.Patient).Where(x => (f.PracticeId == null || x.DoctorPracticeId == f.PracticeId) && (f.PatientId == null || x.PatientId == f.PatientId) &&
                (f.EncounterId == null || x.MedicalEncounterId == f.EncounterId) && (f.Origin == null || x.Origin == f.Origin) && (f.Status == null || x.Status == f.Status) &&
                (f.FromUtc == null || x.RequestedAtUtc >= f.FromUtc) && (f.ToUtc == null || x.RequestedAtUtc <= f.ToUtc));
            return new ClinicalPage<DiagnosticRequestSummary>(await q.OrderByDescending(x => x.RequestedAtUtc).ThenBy(x => x.Id).Skip((page - 1) * size).Take(size)
                .Select(x => new DiagnosticRequestSummary(x.Id, x.MedicalEncounterId, x.PatientId, x.DoctorPracticeId, x.Origin, x.Status, x.RequestedAtUtc, Convert.ToBase64String(x.RowVersion))).ToArrayAsync(ct), await q.LongCountAsync(ct), page, size);
        }
        if (r.Resource == DiagnosticReadResource.Results)
        {
            if (r.Id is { } id) return await LabResultAsync(id, owner, r.Patient, p, ct);
            var q = LabResults(owner, r.Patient).Where(x => (f.PracticeId == null || x.DoctorPracticeId == f.PracticeId) && (f.PatientId == null || x.PatientId == f.PatientId) &&
                (f.RequestId == null || x.RequestId == f.RequestId) && (f.FromUtc == null || x.CreatedAtUtc >= f.FromUtc) && (f.ToUtc == null || x.CreatedAtUtc <= f.ToUtc));
            var rows = await q.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip((page - 1) * size).Take(size)
                .Include(x => x.Versions.Where(v => v.Status == DiagnosticVersionStatus.Finalized)).ThenInclude(v => v.Coverage)
                .Include(x => x.Versions.Where(v => v.Status == DiagnosticVersionStatus.Finalized)).ThenInclude(v => v.Attachments).AsSplitQuery().ToArrayAsync(ct);
            return new ClinicalPage<DiagnosticResultResponse>(rows.Select(x => MapLabResult(x, p, r.Patient)).ToArray(), await q.LongCountAsync(ct), page, size);
        }
        if (r.Resource == DiagnosticReadResource.Submissions)
        {
            if (r.Id is { } id) return await LabSubmissionAsync(id, owner, r.Patient, p, ct);
            if (f.RequestId is { } request && !await LabRequests(owner, r.Patient).AnyAsync(x => x.Id == request, ct)) return null;
            var q = LabSubmissions(owner, r.Patient).Where(x => (f.PracticeId == null || x.DoctorPracticeId == f.PracticeId) && (f.PatientId == null || x.PatientId == f.PatientId) &&
                (f.RequestId == null || x.RequestId == f.RequestId) && (f.FromUtc == null || x.SubmittedAtUtc >= f.FromUtc) && (f.ToUtc == null || x.SubmittedAtUtc <= f.ToUtc));
            var status = f.SubmissionStatus ?? (r.Patient ? (PatientSubmissionStatus?)null : PatientSubmissionStatus.PendingReview);
            if (status is not null) q = q.Where(x => x.Status == status);
            return new ClinicalPage<DiagnosticSubmissionSummary>(await q.OrderBy(x => x.SubmittedAtUtc).ThenBy(x => x.Id).Skip((page - 1) * size).Take(size)
                .Select(x => new DiagnosticSubmissionSummary(x.Id, x.RequestId, x.Status, x.SubmittedAtUtc)).ToArrayAsync(ct), await q.LongCountAsync(ct), page, size);
        }
        if (r.Resource == DiagnosticReadResource.RequestHistory)
        {
            if (!await LabRequests(owner, false).AnyAsync(x => x.Id == r.Id, ct)) return null;
            return await db.Set<LabRequestHistory>().AsNoTracking().Where(h => h.ResourceId == r.Id).OrderBy(h => h.OccurredAtUtc).ThenBy(h => h.Id)
                .Select(h => new DiagnosticHistoryResponse(h.Id, h.Action, h.Reason, h.BeforeSnapshot, h.AfterSnapshot, h.ActorUserId, h.ActorType, h.OccurredAtUtc)).ToArrayAsync(ct);
        }
        if (!await LabResults(owner, false).AnyAsync(x => x.Id == r.Id, ct)) return null;
        var versions = await db.Set<LabResultVersion>().AsNoTracking().Where(v => v.ResultId == r.Id && (r.VersionNumber == null || v.VersionNumber == r.VersionNumber))
            .Include(v => v.Coverage).Include(v => v.Attachments).AsSplitQuery().OrderBy(v => v.VersionNumber).ToArrayAsync(ct);
        if (r.VersionNumber is not null) return versions.Length == 0 ? null : MapLabVersion(versions.Single(), false);
        return versions.Select(v => MapLabVersion(v, false)).ToArray();
    }
    private async Task<string?> LabMediaAsync(DiagnosticMediaQuery q, Guid owner, CancellationToken ct)
    {
        if (q.Submission)
            return await (from a in db.Set<PatientLabResultSubmissionAttachment>().AsNoTracking()
                          join s in LabSubmissions(owner, q.Patient) on a.SubmissionId equals s.Id
                          where s.Id == q.Id && a.Id == q.AttachmentId
                          select a.PrivateMediaKey).SingleOrDefaultAsync(ct);
        return await (from a in db.Set<LabResultAttachment>().AsNoTracking()
                      join v in db.Set<LabResultVersion>().AsNoTracking() on a.ResultVersionId equals v.Id
                      join result in LabResults(owner, q.Patient) on v.ResultId equals result.Id
                      where result.Id == q.Id && a.Id == q.AttachmentId && (q.Patient ? v.Status == DiagnosticVersionStatus.Finalized && v.VersionNumber == result.CurrentVersionNumber : v.VersionNumber == q.VersionNumber)
                      select a.PrivateMediaKey).SingleOrDefaultAsync(ct);
    }
    private async Task<IReadOnlySet<Guid>> LabCoverageAsync(Guid request, CancellationToken ct)
        => (await (from c in db.Set<LabResultCoverage>().AsNoTracking()
                   join v in db.Set<LabResultVersion>().AsNoTracking() on c.ResultVersionId equals v.Id
                   join r in db.Set<LabResult>().AsNoTracking() on v.ResultId equals r.Id
                   where r.RequestId == request && v.Status == DiagnosticVersionStatus.Finalized
                   select c.RequestItemId).Distinct().ToArrayAsync(ct)).ToHashSet();
    private async Task<object?> LabImportsAsync(ReadDiagnosticImportQuery r, IReadOnlyList<string> p, CancellationToken ct)
    {
        var q = db.Set<LabCatalogImportBatch>().IgnoreAutoIncludes().AsNoTracking().AsQueryable();
        if (r.BatchId is { } id) q = q.Where(x => x.Id == id);
        if (r.Changes)
        {
            if (!await q.AnyAsync(ct)) return null;
            var changes = db.Set<LabCatalogImportRecord>().AsNoTracking().Where(x => x.ImportBatchId == r.BatchId && (r.Disposition == null || x.Disposition == r.Disposition));
            if (!string.IsNullOrWhiteSpace(r.Search)) changes = changes.Where(x => x.LoincCode.Contains(r.Search) || x.NameEn.Contains(r.Search));
            return new ClinicalPage<DiagnosticImportRecordResponse>(await changes.OrderBy(x => x.LoincCode).Skip((Page(r.PageNumber) - 1) * Size(r.PageSize)).Take(Size(r.PageSize))
                .Select(x => new DiagnosticImportRecordResponse(x.Id, x.LoincCode, x.NameEn, x.Disposition, x.SourceDataJson, x.MatchedCatalogId)).ToArrayAsync(ct), await changes.LongCountAsync(ct), Page(r.PageNumber), Size(r.PageSize));
        }
        var rows = await q.OrderByDescending(x => x.UploadedAtUtc).ThenBy(x => x.Id).Skip((Page(r.PageNumber) - 1) * Size(r.PageSize)).Take(Size(r.PageSize)).ToArrayAsync(ct);
        var ids = rows.Select(x => x.Id).ToArray();
        var counts = await db.Set<LabCatalogImportRecord>().AsNoTracking().Where(x => ids.Contains(x.ImportBatchId)).GroupBy(x => new { x.ImportBatchId, x.Disposition }).Select(g => new { g.Key.ImportBatchId, g.Key.Disposition, Count = g.Count() }).ToArrayAsync(ct);
        var mapped = rows.Select(x => new DiagnosticImportBatchResponse(x.Id, x.SourceVersion, x.OriginalFileName, x.FileSha256, x.UploadedByUserId, x.UploadedAtUtc, x.Status, x.TotalRecords,
            counts.Where(c => c.ImportBatchId == x.Id).ToDictionary(c => c.Disposition, c => c.Count), x.AppliedByUserId, x.AppliedAtUtc, Convert.ToBase64String(x.RowVersion),
            new(x.Status == DiagnosticImportStatus.Staged && Has(p, "LabCatalog.Import"), x.Status == DiagnosticImportStatus.Staged && Has(p, "LabCatalog.Import")))).ToArray();
        if (r.BatchId is not null) return mapped.SingleOrDefault();
        return new ClinicalPage<DiagnosticImportBatchResponse>(mapped, await q.LongCountAsync(ct), Page(r.PageNumber), Size(r.PageSize));
    }


    private async Task<ClinicalPage<MedicalCatalogResponse>> RadiologyCatalogAsync(SearchMedicalCatalogQuery r, IReadOnlyList<string> p, CancellationToken ct)
    {
        var q = db.Set<RadiologyProcedureCatalog>().IgnoreAutoIncludes().AsNoTracking().AsQueryable();
        if (r.Id is { } id) q = q.Where(c => c.Id == id);
        if (r.DoctorSearch) q = q.Where(c => c.Status == MedicalCatalogStatus.Active && (c.Source == MedicalCatalogSource.Wasla || c.ExternalStatus == "ACTIVE"));
        else if (r.Status is { } status) q = q.Where(c => c.Status == status);
        if (r.Source is { } source) q = q.Where(c => c.Source == source);
        if (r.HasArabic is { } ar) q = q.Where(c => (c.DisplayNameAr != null || c.OfficialNameAr != null) == ar);
        if (r.CommonOnly) q = q.Where(c => c.IsCommonOrder);
        var term = DiagnosticText.Normalize(r.Search);
        if (term.Length > 0) q = q.Where(c => c.NormalizedSearch.Contains(term) || db.Set<RadiologyProcedureCatalog>().Any(old => old.MergedIntoId == c.Id && old.NormalizedSearch.Contains(term)));
        var count = await q.LongCountAsync(ct); var page = Page(r.PageNumber); var size = Size(r.PageSize);
        var rows = await q.OrderByDescending(c => c.IsCommonOrder).ThenByDescending(c => c.LoincCode == term).ThenBy(c => c.NormalizedName).ThenBy(c => c.Id)
            .Skip((page - 1) * size).Take(size).ToArrayAsync(ct);
        IReadOnlyList<DiagnosticHistoryResponse>? history = r.Id.HasValue && !r.DoctorSearch ? await db.Set<RadiologyProcedureCatalogHistory>().AsNoTracking().Where(h => h.ResourceId == r.Id)
            .OrderBy(h => h.OccurredAtUtc).ThenBy(h => h.Id).Select(h => new DiagnosticHistoryResponse(h.Id, h.Action, h.Reason, h.BeforeSnapshot, h.AfterSnapshot, h.ActorUserId, h.ActorType, h.OccurredAtUtc)).ToArrayAsync(ct) : null;
        return new(rows.Select(c => MapRadiologyCatalog(c, p, r.DoctorSearch, history)).ToArray(), count, page, size);
    }
    private static MedicalCatalogResponse MapRadiologyCatalog(RadiologyProcedureCatalog c, IReadOnlyList<string> p, bool doctor, IReadOnlyList<DiagnosticHistoryResponse>? history = null)
        => new(c.Id, c.Source, c.Status, c.LoincCode, c.NameEn, c.NameAr, c.OfficialNameEn, c.OfficialNameAr, c.ExternalStatus, c.SourceVersion, c.IsCommonOrder,
            doctor ? c.Presentation() with { InternalNote = null } : c.Presentation(), doctor ? null : c.SourceDataJson, c.AttributesJson, c.MergedIntoId, Convert.ToBase64String(c.RowVersion),
            new(!doctor && c.Status != MedicalCatalogStatus.Merged && Has(p, "RadiologyCatalog.Update"), !doctor && c.Status == MedicalCatalogStatus.Inactive && (c.Source == MedicalCatalogSource.Wasla || c.ExternalStatus == "ACTIVE") && Has(p, "RadiologyCatalog.Activate"),
                !doctor && c.Status == MedicalCatalogStatus.Active && Has(p, "RadiologyCatalog.Deactivate"), !doctor && c.Status != MedicalCatalogStatus.Merged && Has(p, "RadiologyCatalog.Merge")), history);
    private async Task<ClinicalPage<MedicalCatalogRequestResponse>> RadiologyCatalogRequestsAsync(ListMedicalCatalogRequestsQuery r, Guid? doctorId, IReadOnlyList<string> p, CancellationToken ct)
    {
        var q = db.Set<RadiologyCatalogRequest>().IgnoreAutoIncludes().AsNoTracking().AsQueryable();
        if (r.Own) q = q.Where(x => x.RequestedByDoctorId == doctorId); else if (r.DoctorId is { } d) q = q.Where(x => x.RequestedByDoctorId == d);
        if (r.Id is { } id) q = q.Where(x => x.Id == id); if (r.Status is { } s) q = q.Where(x => x.Status == s);
        if (!string.IsNullOrWhiteSpace(r.Search)) q = q.Where(x => x.Name.Contains(r.Search));
        var count = await q.LongCountAsync(ct); var page = Page(r.PageNumber); var size = Size(r.PageSize);
        var rows = await q.OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip((page - 1) * size).Take(size).ToArrayAsync(ct);
        var history = r.Id.HasValue ? await db.Set<RadiologyCatalogRequestHistory>().AsNoTracking().Where(h => h.ResourceId == r.Id).OrderBy(h => h.OccurredAtUtc)
            .Select(h => new DiagnosticHistoryResponse(h.Id, h.Action, h.Reason, h.BeforeSnapshot, h.AfterSnapshot, h.ActorUserId, h.ActorType, h.OccurredAtUtc)).ToArrayAsync(ct) : [];
        var canonicalIds = rows.Where(x => x.CanonicalCatalogId.HasValue).Select(x => x.CanonicalCatalogId!.Value).Distinct().ToArray();
        var catalogs = await db.Set<RadiologyProcedureCatalog>().IgnoreAutoIncludes().AsNoTracking().Where(c => canonicalIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        return new(rows.Select(x => new MedicalCatalogRequestResponse(x.Id, x.RequestedByDoctorId, x.Name, x.Specimen, x.CatalogClarificationNote, x.Status, x.CanonicalCatalogId, x.ReasonType, x.ReviewReason, x.CreatedAtUtc,
            Convert.ToBase64String(x.RowVersion), new(r.Own && x.Status is MedicalCatalogRequestStatus.Pending or MedicalCatalogRequestStatus.NeedsMoreInfo && Has(p, "RadiologyCatalogRequests.UpdateOwn"),
                !r.Own && x.Status == MedicalCatalogRequestStatus.Pending && Has(p, "RadiologyCatalogRequests.Review"), !r.Own && x.Status == MedicalCatalogRequestStatus.Pending && Has(p, "RadiologyCatalogRequests.Review"), !r.Own && x.Status == MedicalCatalogRequestStatus.Pending && Has(p, "RadiologyCatalogRequests.Review")), history,
                x.CanonicalCatalogId is { } c && catalogs.TryGetValue(c, out var catalog) ? MapRadiologyCatalog(catalog, p, r.Own) : null)).ToArray(), count, page, size);
    }
    private IQueryable<RadiologyRequest> RadiologyRequests(Guid owner, bool patient)
        => db.Set<RadiologyRequest>().IgnoreAutoIncludes().AsNoTracking().Where(x => patient ? x.PatientId == owner && x.Status != DiagnosticRequestStatus.Draft : x.DoctorId == owner && db.DoctorPractices.Any(p => p.Id == x.DoctorPracticeId && p.IsActive));
    private IQueryable<RadiologyResult> RadiologyResults(Guid owner, bool patient)
        => db.Set<RadiologyResult>().IgnoreAutoIncludes().AsNoTracking().Where(x => patient ? x.PatientId == owner && db.Set<RadiologyResultVersion>().Any(v => v.ResultId == x.Id && v.Status == DiagnosticVersionStatus.Finalized) : x.DoctorId == owner && db.DoctorPractices.Any(p => p.Id == x.DoctorPracticeId && p.IsActive));
    private IQueryable<PatientRadiologyResultSubmission> RadiologySubmissions(Guid owner, bool patient)
        => db.Set<PatientRadiologyResultSubmission>().IgnoreAutoIncludes().AsNoTracking().Where(x => patient ? x.PatientId == owner : x.DoctorId == owner && db.DoctorPractices.Any(p => p.Id == x.DoctorPracticeId && p.IsActive));
    private async Task<DiagnosticRequestStateResponse?> RadiologyRequestAsync(Guid id, Guid owner, bool patient, IReadOnlyList<string> p, CancellationToken ct)
    {
        var r = await RadiologyRequests(owner, patient).SingleOrDefaultAsync(x => x.Id == id, ct); if (r is null) return null;
        var d = await db.Doctors.AsNoTracking().Where(x => x.Id == r.DoctorId).Select(x => new ClinicalPartyResponse(x.Id, x.NameAr, x.NameEn)).SingleAsync(ct);
        var practice = await db.DoctorPractices.AsNoTracking().Where(x => x.Id == r.DoctorPracticeId).Select(x => new ClinicalPartyResponse(x.Id, x.NameAr, x.NameEn)).SingleAsync(ct);
        var items = await db.Set<RadiologyRequestItem>().AsNoTracking().Where(x => x.RequestId == id).OrderBy(x => x.Id).ToArrayAsync(ct);
        var results = await RadiologyResults(owner, patient).Where(x => x.RequestId == id).Include(x => x.Versions.Where(v => v.Status == DiagnosticVersionStatus.Finalized)).ThenInclude(v => v.Attachments)
            .Include(x => x.Versions.Where(v => v.Status == DiagnosticVersionStatus.Finalized)).ThenInclude(v => v.Coverage).AsSplitQuery().ToArrayAsync(ct);
        var submissions = await RadiologySubmissions(owner, patient).Where(x => x.RequestId == id).OrderBy(x => x.SubmittedAtUtc)
            .Select(x => new DiagnosticSubmissionSummary(x.Id, x.RequestId, x.Status, x.SubmittedAtUtc)).ToArrayAsync(ct);
        var draft = r.Status == DiagnosticRequestStatus.Draft;
        var manage = !patient && draft && Has(p, "RadiologyRequests.ManageOwnDraft"); var cancel = !patient && !draft && Has(p, "RadiologyRequests.CancelOwn");
        return new(r.Id, r.MedicalEncounterId, d, practice, r.Origin, r.Status, r.PatientInstructions, r.RequestedAtUtc, Convert.ToBase64String(r.RowVersion),
            items.Select(i => new DiagnosticItemResponse(i.Id, i.NameArSnapshot, i.NameEnSnapshot, i.LoincCodeSnapshot, i.ModalitySnapshot, i.AnatomicLocationSnapshot, i.LateralitySnapshot, i.DoctorInstructions, i.Status, i.CancellationReason,
                new(manage, manage, cancel && i.Status == DiagnosticItemStatus.Requested, i.Status == DiagnosticItemStatus.Completed), patient ? null : i.CatalogId, patient ? null : i.CatalogRequestId)).ToArray(),
            new(manage, cancel && items.Any(i => i.Status == DiagnosticItemStatus.Requested), !patient && !draft && items.Any(i => i.Status == DiagnosticItemStatus.Requested) && Has(p, "RadiologyResults.UploadOwn")),
            Has(p, patient ? "RadiologyResults.ViewOwnCurrent" : "RadiologyResults.ViewOwn") ? results.Where(x => x.Current is not null).Select(x => MapRadiologyResult(x, p, patient)).ToArray() : [],
            Has(p, "RadiologyResultSubmissions.ViewOwn") ? submissions : [], patient ? null : r.PostVisitReason);
    }
    private static DiagnosticVersionResponse MapRadiologyVersion(RadiologyResultVersion v, bool patient)
        => new(v.Id, v.VersionNumber, v.Status, v.ExternalProviderName, v.ExternalReportDate, v.OriginallyUploadedBy, v.OriginallyUploadedAtUtc, v.ReviewedByDoctorId, v.ReviewedAtUtc,
            v.Coverage.Select(c => c.RequestItemId).ToArray(), v.Attachments.Select(a => new DiagnosticAttachmentResponse(a.Id, a.OriginalFileName, a.ContentType, a.SizeBytes, a.Sha256, a.UploadedAtUtc, a.Kind)).ToArray(), patient ? null : v.CorrectionReason, patient ? null : v.VoidReason);
    private static DiagnosticResultResponse MapRadiologyResult(RadiologyResult x, IReadOnlyList<string> p, bool patient)
        => new(x.Id, x.RequestId, x.CurrentVersionNumber, x.Current is null ? null : MapRadiologyVersion(x.Current, patient), Convert.ToBase64String(x.RowVersion),
            new(!patient && x.Current is not null && Has(p, "RadiologyResults.CorrectOwn"), !patient && x.Current is not null && Has(p, "RadiologyResults.VoidOwn")));
    private async Task<DiagnosticResultResponse?> RadiologyResultAsync(Guid id, Guid owner, bool patient, IReadOnlyList<string> p, CancellationToken ct)
    {
        var x = await RadiologyResults(owner, patient).Where(x => x.Id == id).Include(x => x.Versions.Where(v => v.Status == DiagnosticVersionStatus.Finalized)).ThenInclude(v => v.Attachments)
            .Include(x => x.Versions.Where(v => v.Status == DiagnosticVersionStatus.Finalized)).ThenInclude(v => v.Coverage).AsSplitQuery().SingleOrDefaultAsync(ct);
        return x is null ? null : MapRadiologyResult(x, p, patient);
    }
    private async Task<DiagnosticSubmissionResponse?> RadiologySubmissionAsync(Guid id, Guid owner, bool patient, IReadOnlyList<string> p, CancellationToken ct)
    {
        var x = await RadiologySubmissions(owner, patient).Include(x => x.Attachments).SingleOrDefaultAsync(x => x.Id == id, ct); if (x is null) return null;
        var requested = await db.Set<RadiologyRequestItem>().AnyAsync(i => i.RequestId == x.RequestId && i.Status == DiagnosticItemStatus.Requested, ct);
        var pending = x.Status == PatientSubmissionStatus.PendingReview;
        return new(x.Id, x.RequestId, x.Status, x.ExternalProviderName, x.ExternalReportDate, x.PatientNote, x.PatientVisibleReason, x.AcceptedResultId, x.SubmittedAtUtc, Convert.ToBase64String(x.RowVersion),
            x.Attachments.Select(a => new DiagnosticAttachmentResponse(a.Id, a.OriginalFileName, a.ContentType, a.SizeBytes, a.Sha256, a.UploadedAtUtc, a.Kind)).ToArray(),
            new(patient && pending && Has(p, "RadiologyResultSubmissions.WithdrawOwn"), !patient && pending && requested && Has(p, "RadiologyResultSubmissions.ReviewOwn"), !patient && pending && Has(p, "RadiologyResultSubmissions.ReviewOwn")));
    }
    private async Task<object?> ReadRadiologyAsync(ReadDiagnosticQuery r, Guid owner, IReadOnlyList<string> p, CancellationToken ct)
    {
        var f = r.Filter ?? new(); var page = Page(f.PageNumber); var size = Size(f.PageSize);
        if (r.Resource == DiagnosticReadResource.Draft)
        {
            var id = await RadiologyRequests(owner, false).Where(x => x.MedicalEncounterId == r.EncounterId && x.DoctorPracticeId == r.PracticeId && x.Status == DiagnosticRequestStatus.Draft).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
            return id is { } draft ? await RadiologyRequestAsync(draft, owner, false, p, ct) : await EmptyAsync(owner, r.EncounterId.GetValueOrDefault(), r.PracticeId, DiagnosticKind.Radiology, p, ct);
        }
        if (r.Resource == DiagnosticReadResource.Requests)
        {
            if (r.Id is { } id) return await RadiologyRequestAsync(id, owner, r.Patient, p, ct);
            var q = RadiologyRequests(owner, r.Patient).Where(x => (f.PracticeId == null || x.DoctorPracticeId == f.PracticeId) && (f.PatientId == null || x.PatientId == f.PatientId) &&
                (f.EncounterId == null || x.MedicalEncounterId == f.EncounterId) && (f.Origin == null || x.Origin == f.Origin) && (f.Status == null || x.Status == f.Status) &&
                (f.FromUtc == null || x.RequestedAtUtc >= f.FromUtc) && (f.ToUtc == null || x.RequestedAtUtc <= f.ToUtc));
            return new ClinicalPage<DiagnosticRequestSummary>(await q.OrderByDescending(x => x.RequestedAtUtc).ThenBy(x => x.Id).Skip((page - 1) * size).Take(size)
                .Select(x => new DiagnosticRequestSummary(x.Id, x.MedicalEncounterId, x.PatientId, x.DoctorPracticeId, x.Origin, x.Status, x.RequestedAtUtc, Convert.ToBase64String(x.RowVersion))).ToArrayAsync(ct), await q.LongCountAsync(ct), page, size);
        }
        if (r.Resource == DiagnosticReadResource.Results)
        {
            if (r.Id is { } id) return await RadiologyResultAsync(id, owner, r.Patient, p, ct);
            var q = RadiologyResults(owner, r.Patient).Where(x => (f.PracticeId == null || x.DoctorPracticeId == f.PracticeId) && (f.PatientId == null || x.PatientId == f.PatientId) &&
                (f.RequestId == null || x.RequestId == f.RequestId) && (f.FromUtc == null || x.CreatedAtUtc >= f.FromUtc) && (f.ToUtc == null || x.CreatedAtUtc <= f.ToUtc));
            var rows = await q.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip((page - 1) * size).Take(size)
                .Include(x => x.Versions.Where(v => v.Status == DiagnosticVersionStatus.Finalized)).ThenInclude(v => v.Coverage)
                .Include(x => x.Versions.Where(v => v.Status == DiagnosticVersionStatus.Finalized)).ThenInclude(v => v.Attachments).AsSplitQuery().ToArrayAsync(ct);
            return new ClinicalPage<DiagnosticResultResponse>(rows.Select(x => MapRadiologyResult(x, p, r.Patient)).ToArray(), await q.LongCountAsync(ct), page, size);
        }
        if (r.Resource == DiagnosticReadResource.Submissions)
        {
            if (r.Id is { } id) return await RadiologySubmissionAsync(id, owner, r.Patient, p, ct);
            if (f.RequestId is { } request && !await RadiologyRequests(owner, r.Patient).AnyAsync(x => x.Id == request, ct)) return null;
            var q = RadiologySubmissions(owner, r.Patient).Where(x => (f.PracticeId == null || x.DoctorPracticeId == f.PracticeId) && (f.PatientId == null || x.PatientId == f.PatientId) &&
                (f.RequestId == null || x.RequestId == f.RequestId) && (f.FromUtc == null || x.SubmittedAtUtc >= f.FromUtc) && (f.ToUtc == null || x.SubmittedAtUtc <= f.ToUtc));
            var status = f.SubmissionStatus ?? (r.Patient ? (PatientSubmissionStatus?)null : PatientSubmissionStatus.PendingReview);
            if (status is not null) q = q.Where(x => x.Status == status);
            return new ClinicalPage<DiagnosticSubmissionSummary>(await q.OrderBy(x => x.SubmittedAtUtc).ThenBy(x => x.Id).Skip((page - 1) * size).Take(size)
                .Select(x => new DiagnosticSubmissionSummary(x.Id, x.RequestId, x.Status, x.SubmittedAtUtc)).ToArrayAsync(ct), await q.LongCountAsync(ct), page, size);
        }
        if (r.Resource == DiagnosticReadResource.RequestHistory)
        {
            if (!await RadiologyRequests(owner, false).AnyAsync(x => x.Id == r.Id, ct)) return null;
            return await db.Set<RadiologyRequestHistory>().AsNoTracking().Where(h => h.ResourceId == r.Id).OrderBy(h => h.OccurredAtUtc).ThenBy(h => h.Id)
                .Select(h => new DiagnosticHistoryResponse(h.Id, h.Action, h.Reason, h.BeforeSnapshot, h.AfterSnapshot, h.ActorUserId, h.ActorType, h.OccurredAtUtc)).ToArrayAsync(ct);
        }
        if (!await RadiologyResults(owner, false).AnyAsync(x => x.Id == r.Id, ct)) return null;
        var versions = await db.Set<RadiologyResultVersion>().AsNoTracking().Where(v => v.ResultId == r.Id && (r.VersionNumber == null || v.VersionNumber == r.VersionNumber))
            .Include(v => v.Coverage).Include(v => v.Attachments).AsSplitQuery().OrderBy(v => v.VersionNumber).ToArrayAsync(ct);
        if (r.VersionNumber is not null) return versions.Length == 0 ? null : MapRadiologyVersion(versions.Single(), false);
        return versions.Select(v => MapRadiologyVersion(v, false)).ToArray();
    }
    private async Task<string?> RadiologyMediaAsync(DiagnosticMediaQuery q, Guid owner, CancellationToken ct)
    {
        if (q.Submission)
            return await (from a in db.Set<PatientRadiologyResultSubmissionAttachment>().AsNoTracking()
                          join s in RadiologySubmissions(owner, q.Patient) on a.SubmissionId equals s.Id
                          where s.Id == q.Id && a.Id == q.AttachmentId
                          select a.PrivateMediaKey).SingleOrDefaultAsync(ct);
        return await (from a in db.Set<RadiologyResultAttachment>().AsNoTracking()
                      join v in db.Set<RadiologyResultVersion>().AsNoTracking() on a.ResultVersionId equals v.Id
                      join result in RadiologyResults(owner, q.Patient) on v.ResultId equals result.Id
                      where result.Id == q.Id && a.Id == q.AttachmentId && (q.Patient ? v.Status == DiagnosticVersionStatus.Finalized && v.VersionNumber == result.CurrentVersionNumber : v.VersionNumber == q.VersionNumber)
                      select a.PrivateMediaKey).SingleOrDefaultAsync(ct);
    }
    private async Task<IReadOnlySet<Guid>> RadiologyCoverageAsync(Guid request, CancellationToken ct)
        => (await (from c in db.Set<RadiologyResultCoverage>().AsNoTracking()
                   join v in db.Set<RadiologyResultVersion>().AsNoTracking() on c.ResultVersionId equals v.Id
                   join r in db.Set<RadiologyResult>().AsNoTracking() on v.ResultId equals r.Id
                   where r.RequestId == request && v.Status == DiagnosticVersionStatus.Finalized
                   select c.RequestItemId).Distinct().ToArrayAsync(ct)).ToHashSet();
    private async Task<object?> RadiologyImportsAsync(ReadDiagnosticImportQuery r, IReadOnlyList<string> p, CancellationToken ct)
    {
        var q = db.Set<RadiologyCatalogImportBatch>().IgnoreAutoIncludes().AsNoTracking().AsQueryable();
        if (r.BatchId is { } id) q = q.Where(x => x.Id == id);
        if (r.Changes)
        {
            if (!await q.AnyAsync(ct)) return null;
            var changes = db.Set<RadiologyCatalogImportRecord>().AsNoTracking().Where(x => x.ImportBatchId == r.BatchId && (r.Disposition == null || x.Disposition == r.Disposition));
            if (!string.IsNullOrWhiteSpace(r.Search)) changes = changes.Where(x => x.LoincCode.Contains(r.Search) || x.NameEn.Contains(r.Search));
            return new ClinicalPage<DiagnosticImportRecordResponse>(await changes.OrderBy(x => x.LoincCode).Skip((Page(r.PageNumber) - 1) * Size(r.PageSize)).Take(Size(r.PageSize))
                .Select(x => new DiagnosticImportRecordResponse(x.Id, x.LoincCode, x.NameEn, x.Disposition, x.SourceDataJson, x.MatchedCatalogId)).ToArrayAsync(ct), await changes.LongCountAsync(ct), Page(r.PageNumber), Size(r.PageSize));
        }
        var rows = await q.OrderByDescending(x => x.UploadedAtUtc).ThenBy(x => x.Id).Skip((Page(r.PageNumber) - 1) * Size(r.PageSize)).Take(Size(r.PageSize)).ToArrayAsync(ct);
        var ids = rows.Select(x => x.Id).ToArray();
        var counts = await db.Set<RadiologyCatalogImportRecord>().AsNoTracking().Where(x => ids.Contains(x.ImportBatchId)).GroupBy(x => new { x.ImportBatchId, x.Disposition }).Select(g => new { g.Key.ImportBatchId, g.Key.Disposition, Count = g.Count() }).ToArrayAsync(ct);
        var mapped = rows.Select(x => new DiagnosticImportBatchResponse(x.Id, x.SourceVersion, x.OriginalFileName, x.FileSha256, x.UploadedByUserId, x.UploadedAtUtc, x.Status, x.TotalRecords,
            counts.Where(c => c.ImportBatchId == x.Id).ToDictionary(c => c.Disposition, c => c.Count), x.AppliedByUserId, x.AppliedAtUtc, Convert.ToBase64String(x.RowVersion),
            new(x.Status == DiagnosticImportStatus.Staged && Has(p, "RadiologyCatalog.Import"), x.Status == DiagnosticImportStatus.Staged && Has(p, "RadiologyCatalog.Import")))).ToArray();
        if (r.BatchId is not null) return mapped.SingleOrDefault();
        return new ClinicalPage<DiagnosticImportBatchResponse>(mapped, await q.LongCountAsync(ct), Page(r.PageNumber), Size(r.PageSize));
    }

}
