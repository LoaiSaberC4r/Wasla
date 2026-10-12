using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Clinical;
using Wasla.Domain.Diagnostics;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed partial class MedicalRecordReadService
{
    private Task<MedicalRecordItemDetailsResponse?> DiagnosticItemAsync(MedicalRecordScope s, string type, Guid id, CancellationToken ct)
        => type is "lab-request" or "lab-result" ? LabItemAsync(s, type, id, ct)
            : type is "radiology-request" or "radiology-result" ? RadiologyItemAsync(s, type, id, ct) : Task.FromResult<MedicalRecordItemDetailsResponse?>(null);
    private Task<MediaReference?> MediaReferenceAsync(MedicalRecordScope s, string type, Guid source, Guid attachment, CancellationToken ct)
        => type == "lab-result" ? LabMediaAsync(s, source, attachment, ct)
            : type == "radiology-result" ? RadiologyMediaAsync(s, source, attachment, ct) : Task.FromResult<MediaReference?>(null);

    private async Task<MedicalRecordItemDetailsResponse?> LabItemAsync(MedicalRecordScope s, string type, Guid id, CancellationToken ct)
    {
        var sources = Authorized(s);
        if (type == "lab-request")
        {
            var order = await (from e in sources join r in db.LabRequests on e.Id equals r.MedicalEncounterId
                where r.Id == id && r.Status != DiagnosticRequestStatus.Draft && r.RequestedAtUtc != null &&
                    r.PatientId == e.PatientId && r.DoctorId == e.DoctorId && r.DoctorPracticeId == e.DoctorPracticeId
                select new { e.Id, e.DoctorId, e.DoctorPracticeId, r.Status, r.RequestedAtUtc, r.PatientInstructions }).SingleOrDefaultAsync(ct);
            if (order is null) return null;
            var items = await db.LabRequestItems.AsNoTracking().Where(i => i.RequestId == id).OrderBy(i => i.Id)
                .Select(i => new SafeOrderItem(i.Id, i.NameArSnapshot, i.NameEnSnapshot, i.LoincCodeSnapshot, i.DoctorInstructions, i.Status.ToString())).ToArrayAsync(ct);
            var official = await db.LabResults.AnyAsync(r => r.RequestId == id && r.PatientId == s.PatientId && r.DoctorId == order.DoctorId && r.DoctorPracticeId == order.DoctorPracticeId &&
                db.LabResultVersions.Any(v => v.ResultId == r.Id && v.VersionNumber == r.CurrentVersionNumber && v.Status == DiagnosticVersionStatus.Finalized && v.ReviewedByDoctorId == order.DoctorId), ct);
            var pending = s.Mode == MedicalRecordMode.OwnPatient && await db.PatientLabResultSubmissions.AnyAsync(r => r.RequestId == id && r.PatientId == s.PatientId && r.Status == PatientSubmissionStatus.PendingReview, ct);
            return new(type, id, order.Id, order.Status.ToString(), null,
                new SafeOrderContent(DateTime.SpecifyKind(order.RequestedAtUtc!.Value, DateTimeKind.Utc), order.PatientInstructions, items, official ? "Official" : pending ? "PendingReview" : "MissingResult"), [], Capabilities(s));
        }
        var result = await (from e in sources join r in db.LabRequests on e.Id equals r.MedicalEncounterId
            join x in db.LabResults on r.Id equals x.RequestId join v in db.LabResultVersions on x.Id equals v.ResultId
            where x.Id == id && r.Status != DiagnosticRequestStatus.Draft && r.PatientId == e.PatientId && r.DoctorId == e.DoctorId &&
                r.DoctorPracticeId == e.DoctorPracticeId && x.PatientId == e.PatientId && x.DoctorId == e.DoctorId && x.DoctorPracticeId == e.DoctorPracticeId &&
                v.VersionNumber == x.CurrentVersionNumber && v.Status == DiagnosticVersionStatus.Finalized && v.ReviewedByDoctorId == e.DoctorId
            select new { EncounterId = e.Id, VersionId = v.Id, x.RequestId, v.VersionNumber, v.ReviewedAtUtc, v.ExternalProviderName, v.ExternalReportDate }).SingleOrDefaultAsync(ct);
        if (result is null) return null;
        var attachments = await db.LabResultAttachments.AsNoTracking().Where(a => a.ResultVersionId == result.VersionId).OrderBy(a => a.Id)
            .Select(a => new RecordAttachment(a.Id, SafeFileName(a.OriginalFileName), a.ContentType, a.SizeBytes, true)).ToArrayAsync(ct);
        var coverage = await db.LabResultCoverages.AsNoTracking().Where(c => c.ResultVersionId == result.VersionId).Select(c => c.RequestItemId).ToArrayAsync(ct);
        return new(type, id, result.EncounterId, result.VersionNumber > 1 ? "Corrected" : "Official", result.VersionNumber,
            new SafeResultContent(result.RequestId, DateTime.SpecifyKind(result.ReviewedAtUtc, DateTimeKind.Utc), result.ExternalProviderName, result.ExternalReportDate, coverage), attachments, Capabilities(s));
    }
    private Task<MediaReference?> LabMediaAsync(MedicalRecordScope s, Guid source, Guid attachment, CancellationToken ct)
        => (from e in Authorized(s) join r in db.LabRequests on e.Id equals r.MedicalEncounterId
            join x in db.LabResults on r.Id equals x.RequestId join v in db.LabResultVersions on x.Id equals v.ResultId
            join a in db.LabResultAttachments on v.Id equals a.ResultVersionId
            where x.Id == source && a.Id == attachment && r.Status != DiagnosticRequestStatus.Draft &&
                r.PatientId == e.PatientId && r.DoctorId == e.DoctorId && r.DoctorPracticeId == e.DoctorPracticeId &&
                x.PatientId == e.PatientId && x.DoctorId == e.DoctorId && x.DoctorPracticeId == e.DoctorPracticeId &&
                v.Status == DiagnosticVersionStatus.Finalized && v.VersionNumber == x.CurrentVersionNumber && v.ReviewedByDoctorId == e.DoctorId
            select new MediaReference(e.Id, a.PrivateMediaKey, a.OriginalFileName, a.ContentType, a.SizeBytes)).SingleOrDefaultAsync(ct);

    private async Task<MedicalRecordItemDetailsResponse?> RadiologyItemAsync(MedicalRecordScope s, string type, Guid id, CancellationToken ct)
    {
        var sources = Authorized(s);
        if (type == "radiology-request")
        {
            var order = await (from e in sources join r in db.RadiologyRequests on e.Id equals r.MedicalEncounterId
                where r.Id == id && r.Status != DiagnosticRequestStatus.Draft && r.RequestedAtUtc != null &&
                    r.PatientId == e.PatientId && r.DoctorId == e.DoctorId && r.DoctorPracticeId == e.DoctorPracticeId
                select new { e.Id, e.DoctorId, e.DoctorPracticeId, r.Status, r.RequestedAtUtc, r.PatientInstructions }).SingleOrDefaultAsync(ct);
            if (order is null) return null;
            var items = await db.RadiologyRequestItems.AsNoTracking().Where(i => i.RequestId == id).OrderBy(i => i.Id)
                .Select(i => new SafeOrderItem(i.Id, i.NameArSnapshot, i.NameEnSnapshot, i.LoincCodeSnapshot, i.DoctorInstructions, i.Status.ToString())).ToArrayAsync(ct);
            var official = await db.RadiologyResults.AnyAsync(r => r.RequestId == id && r.PatientId == s.PatientId && r.DoctorId == order.DoctorId && r.DoctorPracticeId == order.DoctorPracticeId &&
                db.RadiologyResultVersions.Any(v => v.ResultId == r.Id && v.VersionNumber == r.CurrentVersionNumber && v.Status == DiagnosticVersionStatus.Finalized && v.ReviewedByDoctorId == order.DoctorId), ct);
            var pending = s.Mode == MedicalRecordMode.OwnPatient && await db.PatientRadiologyResultSubmissions.AnyAsync(r => r.RequestId == id && r.PatientId == s.PatientId && r.Status == PatientSubmissionStatus.PendingReview, ct);
            return new(type, id, order.Id, order.Status.ToString(), null,
                new SafeOrderContent(DateTime.SpecifyKind(order.RequestedAtUtc!.Value, DateTimeKind.Utc), order.PatientInstructions, items, official ? "Official" : pending ? "PendingReview" : "MissingResult"), [], Capabilities(s));
        }
        var result = await (from e in sources join r in db.RadiologyRequests on e.Id equals r.MedicalEncounterId
            join x in db.RadiologyResults on r.Id equals x.RequestId join v in db.RadiologyResultVersions on x.Id equals v.ResultId
            where x.Id == id && r.Status != DiagnosticRequestStatus.Draft && r.PatientId == e.PatientId && r.DoctorId == e.DoctorId &&
                r.DoctorPracticeId == e.DoctorPracticeId && x.PatientId == e.PatientId && x.DoctorId == e.DoctorId && x.DoctorPracticeId == e.DoctorPracticeId &&
                v.VersionNumber == x.CurrentVersionNumber && v.Status == DiagnosticVersionStatus.Finalized && v.ReviewedByDoctorId == e.DoctorId
            select new { EncounterId = e.Id, VersionId = v.Id, x.RequestId, v.VersionNumber, v.ReviewedAtUtc, v.ExternalProviderName, v.ExternalReportDate }).SingleOrDefaultAsync(ct);
        if (result is null) return null;
        var attachments = await db.RadiologyResultAttachments.AsNoTracking().Where(a => a.ResultVersionId == result.VersionId).OrderBy(a => a.Id)
            .Select(a => new RecordAttachment(a.Id, SafeFileName(a.OriginalFileName), a.ContentType, a.SizeBytes, true)).ToArrayAsync(ct);
        var coverage = await db.RadiologyResultCoverages.AsNoTracking().Where(c => c.ResultVersionId == result.VersionId).Select(c => c.RequestItemId).ToArrayAsync(ct);
        return new(type, id, result.EncounterId, result.VersionNumber > 1 ? "Corrected" : "Official", result.VersionNumber,
            new SafeResultContent(result.RequestId, DateTime.SpecifyKind(result.ReviewedAtUtc, DateTimeKind.Utc), result.ExternalProviderName, result.ExternalReportDate, coverage), attachments, Capabilities(s));
    }
    private Task<MediaReference?> RadiologyMediaAsync(MedicalRecordScope s, Guid source, Guid attachment, CancellationToken ct)
        => (from e in Authorized(s) join r in db.RadiologyRequests on e.Id equals r.MedicalEncounterId
            join x in db.RadiologyResults on r.Id equals x.RequestId join v in db.RadiologyResultVersions on x.Id equals v.ResultId
            join a in db.RadiologyResultAttachments on v.Id equals a.ResultVersionId
            where x.Id == source && a.Id == attachment && r.Status != DiagnosticRequestStatus.Draft &&
                r.PatientId == e.PatientId && r.DoctorId == e.DoctorId && r.DoctorPracticeId == e.DoctorPracticeId &&
                x.PatientId == e.PatientId && x.DoctorId == e.DoctorId && x.DoctorPracticeId == e.DoctorPracticeId &&
                v.Status == DiagnosticVersionStatus.Finalized && v.VersionNumber == x.CurrentVersionNumber && v.ReviewedByDoctorId == e.DoctorId
            select new MediaReference(e.Id, a.PrivateMediaKey, a.OriginalFileName, a.ContentType, a.SizeBytes)).SingleOrDefaultAsync(ct);
}
