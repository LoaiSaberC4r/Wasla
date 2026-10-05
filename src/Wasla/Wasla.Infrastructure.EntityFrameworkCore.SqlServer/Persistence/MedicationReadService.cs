using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Clinical;
using Wasla.Application.Features.Governance;
using Wasla.Application.Features.Medications;
using Wasla.Domain.Clinical;
using Wasla.Domain.Common;
using Wasla.Domain.Medications;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed partial class MedicationReadService(WaslaDbContext db) : IMedicationReadService, IDrugCatalogManagerReader
{
    public async Task<ClinicalPage<DrugCatalogManagerResponse>> ListAsync(string? search, int page, int size, CancellationToken ct)
    {
        var query = db.ApplicationUsers.AsNoTracking().Where(u => u.UserType == UserType.DrugCatalogManager);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(u => u.UserName.Contains(search) || u.Email.Contains(search));
        var count = await query.LongCountAsync(ct);
        var rows = await query.OrderBy(u => u.UserName).ThenBy(u => u.Id).Skip((page - 1) * size).Take(size)
            .Select(u => new DrugCatalogManagerResponse(u.Id, u.UserName, u.Email, u.PhoneNumber, u.IsActive, u.IsFirstLogin, u.CreatedOnUtc)).ToArrayAsync(ct);
        return new(rows, count, page, size);
    }
    public async Task<ClinicalPage<DrugCatalogResponse>> CatalogAsync(string? search, DrugCatalogStatus? status, bool doctorSearch, int page, int size, CancellationToken ct)
    {
        var term = MedicationText.Normalize(search);
        var query = db.DrugCatalogs.AsNoTracking().Where(d => status == null || d.Status == status);
        if (term.Length > 0) query = query.Where(d => d.NormalizedCommercialNameEn.Contains(term) ||
            d.NormalizedCommercialNameAr != null && d.NormalizedCommercialNameAr.Contains(term) ||
            d.NormalizedScientificName != null && d.NormalizedScientificName.Contains(term) ||
            d.NormalizedManufacturer != null && d.NormalizedManufacturer.Contains(term));
        var count = await query.LongCountAsync(ct);
        var rows = await query.OrderBy(d => d.NormalizedCommercialNameEn == term ? 0 : d.NormalizedCommercialNameEn.StartsWith(term) ? 1 :
            d.NormalizedCommercialNameAr != null && d.NormalizedCommercialNameAr.Contains(term) ? 2 :
            d.NormalizedScientificName != null && d.NormalizedScientificName.Contains(term) ? 3 :
            d.NormalizedCommercialNameEn.Contains(term) ? 4 : 5).ThenBy(d => d.NormalizedCommercialNameEn).ThenBy(d => d.Id)
            .Skip((page - 1) * size).Take(size).ToArrayAsync(ct);
        return new(rows.Select(d => Drug(d, doctorSearch)).ToArray(), count, page, size);
    }
    private static DrugCatalogResponse Drug(DrugCatalog d, bool doctorSearch = false)
    {
        var strength = StrengthPattern().Match(d.CommercialNameEn);
        var form = FormPattern().Match(d.CommercialNameEn);
        return new(d.Id, d.CommercialNameEn, d.CommercialNameAr, d.ScientificName, d.Manufacturer, d.DrugClass,
            d.StrengthText, d.DosageForm, !doctorSearch || MedicationText.UsableRoute(d.Route) ? d.Route : null,
            d.StrengthText is null && strength.Success ? strength.Value : null,
            d.DosageForm is null && form.Success ? form.Value : null, d.PriceEgp, d.Status, d.StatusReason, d.MergedIntoDrugCatalogId,
            d.OriginType, d.IsMissingFromLatestSource, d.IsManagerReviewed, d.IsPriceManuallyOverridden, Convert.ToBase64String(d.RowVersion));
    }
    [GeneratedRegex(@"\b\d+(?:\.\d+)?\s*(?:MG|MCG|G|ML|IU|%)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StrengthPattern();
    [GeneratedRegex(@"\b(?:TABS?\.?|TABLETS?|CAPS?\.?|CAPSULES?|SYRUP|CREAM|OINTMENT|AMPOULES?|INJECTION|DROPS?)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FormPattern();
    public async Task<DrugCatalogDetailsResponse?> DrugAsync(Guid id, CancellationToken ct)
    {
        var d = await db.DrugCatalogs.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id, ct);
        if (d is null) return null;
        var history = await db.DrugCatalogHistories.AsNoTracking().Where(h => h.DrugCatalogId == id).OrderBy(h => h.OccurredAtUtc).ThenBy(h => h.Id)
            .Select(h => new MedicationHistoryResponse(h.Id, h.Action, h.Reason, h.BeforeSnapshot, h.AfterSnapshot, h.PerformedByApplicationUserId, h.OccurredAtUtc)).ToArrayAsync(ct);
        return new(Drug(d), history);
    }
    public async Task<ClinicalPage<DrugRequestResponse>> RequestsAsync(Guid? doctorId, DrugCatalogRequestStatus? status, string? search,
        Guid? id, int page, int size, CancellationToken ct)
    {
        var query = db.DrugCatalogRequests.AsNoTracking().Where(r => (doctorId == null || r.RequestedByDoctorId == doctorId) &&
            (status == null || r.Status == status) && (id == null || r.Id == id));
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(r => r.MedicationName.Contains(search));
        var count = await query.LongCountAsync(ct);
        var rows = await query.OrderByDescending(r => r.CreatedAtUtc).ThenBy(r => r.Id).Skip((page - 1) * size).Take(size).ToArrayAsync(ct);
        var drugIds = rows.SelectMany(r => new[] { r.ApprovedDrugCatalogId, r.DuplicateOfDrugCatalogId }).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        var drugs = await db.DrugCatalogs.AsNoTracking().Where(d => drugIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, ct);
        var history = id is null ? [] : await db.DrugCatalogRequestHistories.AsNoTracking().Where(h => h.DrugCatalogRequestId == id)
            .OrderBy(h => h.OccurredAtUtc).ThenBy(h => h.Id).Select(h => new MedicationHistoryResponse(h.Id, h.Action, h.Reason,
                h.BeforeSnapshot, h.AfterSnapshot, h.PerformedByApplicationUserId, h.OccurredAtUtc)).ToArrayAsync(ct);
        return new(rows.Select(r => new DrugRequestResponse(r.Id, r.RequestedByDoctorId, r.Data(), r.Status,
            r.ApprovedDrugCatalogId, r.DuplicateOfDrugCatalogId, r.CurrentReviewReason, r.CreatedAtUtc, r.ModifiedAtUtc,
            Convert.ToBase64String(r.RowVersion), history,
            r.ApprovedDrugCatalogId is { } approved && drugs.TryGetValue(approved, out var approvedDrug) ? Drug(approvedDrug) : null,
            r.DuplicateOfDrugCatalogId is { } duplicate && drugs.TryGetValue(duplicate, out var duplicateDrug) ? Drug(duplicateDrug) : null)).ToArray(), count, page, size);
    }
    public async Task<ClinicalPage<DrugImportBatchResponse>> BatchesAsync(Guid? id, int page, int size, CancellationToken ct)
    {
        var query = db.DrugCatalogImportBatches.AsNoTracking().Where(b => id == null || b.Id == id);
        var count = await query.LongCountAsync(ct);
        var rows = await query.OrderByDescending(b => b.CreatedAtUtc).ThenBy(b => b.Id).Skip((page - 1) * size).Take(size).ToArrayAsync(ct);
        return new(rows.Select(b => new DrugImportBatchResponse(b.Id, b.Source, b.SourceVersion, b.SourceCommitSha, b.FileSha256,
            b.Status, b.TotalRecords, b.NewRecords, b.UnchangedRecords, b.PriceChanges, b.NeedsReviewRecords, b.MissingRecords,
            b.PossibleDuplicateRecords, b.ExactDuplicateRecords, b.CreatedAtUtc, b.AppliedAtUtc, Convert.ToBase64String(b.RowVersion))).ToArray(), count, page, size);
    }
    public async Task<ClinicalPage<DrugImportRecordResponse>> ImportChangesAsync(Guid batchId, DrugImportChangeType? change, int page, int size, CancellationToken ct)
    {
        var query = db.DrugCatalogImportRecords.AsNoTracking().Where(r => r.ImportBatchId == batchId && (change == null || r.ChangeType == change));
        var count = await query.LongCountAsync(ct);
        var rows = await query.OrderBy(r => r.SourceRowNumber).ThenBy(r => r.Id).Skip((page - 1) * size).Take(size).ToArrayAsync(ct);
        return new(rows.Select(r => new DrugImportRecordResponse(r.Id, r.SourceRowNumber, r.Data(), r.ChangeType, r.MatchedDrugCatalogId)).ToArray(), count, page, size);
    }
    internal static PrescriptionVersionResponse Version(PrescriptionVersion v)
        => new(v.Id, v.VersionNumber, v.Status, v.PreviousVersionId, v.CorrectionReason, v.CreatedAtUtc, v.FinalizedAtUtc,
            v.VoidedAtUtc, v.VoidReason, v.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.Id).Select(i =>
                new PrescriptionItemResponse(i.Id, i.SortOrder, i.MedicationSource, i.DrugCatalogId, i.DrugCatalogRequestId,
                    i.MedicationNameSnapshot, i.ScientificNameSnapshot, i.Data())).ToArray());
    public async Task<PrescriptionStateResponse?> PrescriptionAsync(Guid? prescriptionId, Guid? encounterId, Guid doctorId, CancellationToken ct)
    {
        var p = await db.Prescriptions.AsNoTracking().Include(p => p.Versions).AsSplitQuery()
            .SingleOrDefaultAsync(p => p.DoctorId == doctorId && (prescriptionId == null || p.Id == prescriptionId) &&
                (encounterId == null || p.MedicalEncounterId == encounterId), ct);
        if (p is null) return null;
        var practiceId = await db.MedicalEncounters.AsNoTracking().Where(e => e.Id == p.MedicalEncounterId).Select(e => e.DoctorPracticeId).SingleAsync(ct);
        return new(p.Id, p.MedicalEncounterId, practiceId, p.Current is { } current ? Version(current) : null,
            p.Draft is { } draft ? Version(draft) : null, Convert.ToBase64String(p.RowVersion), new(false, false, false, false, false, false),
            p.Draft is null ? [] : p.CompletionBlockers());
    }
    public async Task<IReadOnlyList<PrescriptionVersionResponse>> VersionsAsync(Guid prescriptionId, Guid doctorId, int? number, CancellationToken ct)
    {
        var versions = await db.PrescriptionVersions.AsNoTracking().AsSplitQuery().Where(v => v.PrescriptionId == prescriptionId &&
            (number == null || v.VersionNumber == number) && db.Prescriptions.Any(p => p.Id == prescriptionId && p.DoctorId == doctorId))
            .OrderBy(v => v.VersionNumber).ToArrayAsync(ct);
        return versions.Select(Version).ToArray();
    }
    public async Task<ClinicalPage<PatientPrescriptionSummaryResponse>> PatientListAsync(Guid patientId, int page, int size, CancellationToken ct)
    {
        var query = from p in db.Prescriptions.AsNoTracking()
            join e in db.MedicalEncounters.AsNoTracking() on p.MedicalEncounterId equals e.Id
            join d in db.Doctors.AsNoTracking() on p.DoctorId equals d.Id
            join practice in db.DoctorPractices.AsNoTracking() on e.DoctorPracticeId equals practice.Id
            join v in db.PrescriptionVersions.AsNoTracking().IgnoreAutoIncludes() on p.Id equals v.PrescriptionId
            where p.PatientId == patientId && e.Status == EncounterStatus.Completed && (v.Status == PrescriptionVersionStatus.Finalized || v.Status == PrescriptionVersionStatus.Voided)
            select new { PrescriptionId = p.Id, EncounterId = e.Id, DoctorNameAr = d.NameAr, DoctorNameEn = d.NameEn, PracticeNameAr = practice.NameAr, PracticeNameEn = practice.NameEn, VisitDateUtc = e.StartedAtUtc, v.VersionNumber, v.Status };
        var count = await query.LongCountAsync(ct);
        return new(await query.OrderByDescending(p => p.VisitDateUtc).ThenBy(p => p.PrescriptionId).Skip((page - 1) * size).Take(size)
            .Select(p => new PatientPrescriptionSummaryResponse(p.PrescriptionId, p.EncounterId, p.DoctorNameAr, p.DoctorNameEn, p.PracticeNameAr, p.PracticeNameEn, p.VisitDateUtc, p.VersionNumber, p.Status)).ToArrayAsync(ct), count, page, size);
    }
    public async Task<PatientPrescriptionResponse?> PatientPrescriptionAsync(Guid patientId, Guid id, CancellationToken ct)
    {
        var row = await (from p in db.Prescriptions.AsNoTracking()
            join e in db.MedicalEncounters.AsNoTracking() on p.MedicalEncounterId equals e.Id
            join d in db.Doctors.AsNoTracking() on p.DoctorId equals d.Id
            join practice in db.DoctorPractices.AsNoTracking() on e.DoctorPracticeId equals practice.Id
            join v in db.PrescriptionVersions.AsNoTracking().IgnoreAutoIncludes() on p.Id equals v.PrescriptionId
            where p.Id == id && p.PatientId == patientId && e.Status == EncounterStatus.Completed && (v.Status == PrescriptionVersionStatus.Finalized || v.Status == PrescriptionVersionStatus.Voided)
            select new { p.Id, EncounterId = e.Id, e.StartedAtUtc, VersionId = v.Id, v.VersionNumber, v.Status, v.FinalizedAtUtc, v.VoidedAtUtc,
                Doctor = new ClinicalPartyResponse(d.Id, d.NameAr, d.NameEn), Practice = new ClinicalPartyResponse(practice.Id, practice.NameAr, practice.NameEn) }).SingleOrDefaultAsync(ct);
        if (row is null) return null;
        var items = await db.PrescriptionItems.AsNoTracking().Where(i => i.PrescriptionVersionId == row.VersionId).OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
            .Select(i => new PatientPrescriptionItemResponse(i.SortOrder, i.MedicationNameSnapshot, i.ScientificNameSnapshot,
                new PrescriptionItemData(i.StrengthSnapshot, i.DosageFormSnapshot, i.RouteSnapshot, i.DoseText, i.FrequencyCode, i.FrequencyText,
                    i.DurationType, i.DurationValue, i.DurationUnit, i.AsNeeded, i.PrnReason, i.MinimumIntervalText, i.MaxPer24HoursText, i.QuantityValue, i.QuantityUnit, i.Instructions))).ToArrayAsync(ct);
        return new(row.Id, row.EncounterId, row.Doctor, row.Practice, row.StartedAtUtc, row.VersionNumber, row.Status, row.FinalizedAtUtc, row.VoidedAtUtc, items);
    }
}
