using BuildingBlock.Application.Abstraction;
using BuildingBlock.Domain.Results;
using Wasla.Domain.Clinical;
using Wasla.Domain.Medications;
using Wasla.Domain.Security;

namespace Wasla.Application.Features.DoctorPatients;

internal sealed class DoctorPatientHandlers(DoctorPatientRelationshipPolicy policy, IDoctorPatientReadService reader)
    : IQueryHandler<DoctorPatientsQuery, DoctorPatientsPageResponse>,
      IQueryHandler<DoctorMyPatientQuery, DoctorMyPatientOverviewResponse>, IQueryHandler<DoctorPatientHistoryQuery, object>
{
    public async Task<Result<DoctorPatientsPageResponse>> Handle(DoctorPatientsQuery q, CancellationToken ct)
    {
        var actor = await policy.ActorAsync(PermissionNames.DoctorPatientsViewOwn, ct);
        if (actor.IsFailure) return Result<DoctorPatientsPageResponse>.Fail(actor.Errors);
        if (!await policy.OwnPracticeAsync(actor.Value, q.PracticeId, ct)) return Result<DoctorPatientsPageResponse>.Fail(ClinicalErrors.NotFound);
        if (q.HasUpcomingAppointment.HasValue && !actor.Value.Has(PermissionNames.DoctorPracticeReservationsViewOwn))
            return Result<DoctorPatientsPageResponse>.Fail(ClinicalErrors.AccessDenied);
        var result = await reader.DirectoryAsync(actor.Value, q, ct);
        var fresh = await policy.ActorAsync(PermissionNames.DoctorPatientsViewOwn, ct);
        if (fresh.IsFailure || !SamePermissions(actor.Value, fresh.Value)) return Result<DoctorPatientsPageResponse>.Fail(ClinicalErrors.AccessDenied);
        await reader.AuditAsync(fresh.Value, "DoctorPatientsDirectoryRead", Guid.Empty, ct);
        return Result<DoctorPatientsPageResponse>.Ok(result);
    }
    public async Task<Result<DoctorMyPatientOverviewResponse>> Handle(DoctorMyPatientQuery q, CancellationToken ct)
    {
        var actor = await policy.AuthorizeAsync(q.PatientId, ct);
        if (actor.IsFailure) return Result<DoctorMyPatientOverviewResponse>.Fail(actor.Errors);
        var result = await reader.OverviewAsync(actor.Value, q.PatientId, ct);
        if (result is null) return Result<DoctorMyPatientOverviewResponse>.Fail(ClinicalErrors.NotFound);
        var fresh = await policy.AuthorizeAsync(q.PatientId, ct);
        if (fresh.IsFailure || !SamePermissions(actor.Value, fresh.Value)) return Result<DoctorMyPatientOverviewResponse>.Fail(ClinicalErrors.AccessDenied);
        await reader.AuditAsync(fresh.Value, "DoctorPatientOverviewRead", q.PatientId, ct);
        return Result<DoctorMyPatientOverviewResponse>.Ok(result);
    }
    public async Task<Result<object>> Handle(DoctorPatientHistoryQuery q, CancellationToken ct)
    {
        var actor = await policy.AuthorizeAsync(q.PatientId, ct);
        if (actor.IsFailure) return Result<object>.Fail(actor.Errors);
        var permission = q.Resource switch
        {
            DoctorPatientHistory.Attendances => PermissionNames.DoctorPracticeTicketsViewOwn,
            DoctorPatientHistory.Diagnoses => PermissionNames.DiagnosesViewOwn,
            DoctorPatientHistory.Prescriptions => PermissionNames.PrescriptionsViewOwn,
            DoctorPatientHistory.Appointments => PermissionNames.DoctorPracticeReservationsViewOwn,
            DoctorPatientHistory.FollowUps => PermissionNames.FollowUpEligibilityViewOwn,
            _ => throw new ArgumentOutOfRangeException(nameof(q))
        };
        if (!actor.Value.Has(permission)) return Result<object>.Fail(ClinicalErrors.AccessDenied);
        if (!await policy.OwnPracticeAsync(actor.Value, q.PracticeId, ct)) return Result<object>.Fail(ClinicalErrors.NotFound);
        var result = await reader.HistoryAsync(actor.Value, q, ct);
        var fresh = await policy.AuthorizeAsync(q.PatientId, ct);
        if (fresh.IsFailure || !SamePermissions(actor.Value, fresh.Value)) return Result<object>.Fail(ClinicalErrors.AccessDenied);
        await reader.AuditAsync(fresh.Value, "DoctorPatient" + q.Resource + "Read", q.PatientId, ct);
        return Result<object>.Ok(result);
    }
    private static bool SamePermissions(DoctorPatientActor before, DoctorPatientActor after)
        => before.DoctorId == after.DoctorId && before.Permissions.All(after.Has);
}
internal sealed class PrescriptionPdfHandler(DoctorPatientRelationshipPolicy policy, DoctorOwnHistoricalReadPolicy history,
    IDoctorPatientReadService reader, IPrescriptionPdfRenderer renderer) : IQueryHandler<PrescriptionPdfQuery, PrescriptionPdfResponse>
{
    public async Task<Result<PrescriptionPdfResponse>> Handle(PrescriptionPdfQuery q, CancellationToken ct)
    {
        var actor = await policy.ActorAsync(PermissionNames.PrescriptionsViewOwn, ct);
        if (actor.IsFailure) return Result<PrescriptionPdfResponse>.Fail(actor.Errors);
        if (!actor.Value.Has(PermissionNames.PrescriptionsExportOwn)) return Result<PrescriptionPdfResponse>.Fail(ClinicalErrors.AccessDenied);
        var doc = await reader.PdfDocumentAsync(actor.Value.DoctorId, q.PrescriptionId, q.VersionNumber, ct);
        if (doc is null) return Result<PrescriptionPdfResponse>.Fail(ClinicalErrors.NotFound);
        var scope = await history.AuthorizeOwnReadAsync(doc.PracticeId, PermissionNames.PrescriptionsViewOwn, doc.PatientId, ct);
        if (scope.IsFailure) return Result<PrescriptionPdfResponse>.Fail(scope.Errors);
        var pdf = await renderer.RenderAsync(doc, ct);
        if (pdf.IsFailure) return Result<PrescriptionPdfResponse>.Fail(pdf.Errors);
        // Rendering can take seconds: reload immutable source/status and current DB grants before
        // releasing any bytes. A concurrent void/correction requires a new render/request.
        var fresh = await policy.ActorAsync(PermissionNames.PrescriptionsExportOwn, ct);
        if (fresh.IsFailure || !fresh.Value.Has(PermissionNames.PrescriptionsViewOwn) || fresh.Value.DoctorId != actor.Value.DoctorId)
            return Result<PrescriptionPdfResponse>.Fail(ClinicalErrors.AccessDenied);
        scope = await history.AuthorizeOwnReadAsync(doc.PracticeId, PermissionNames.PrescriptionsViewOwn, doc.PatientId, ct);
        if (scope.IsFailure) return Result<PrescriptionPdfResponse>.Fail(scope.Errors);
        var latest = await reader.PdfDocumentAsync(fresh.Value.DoctorId, q.PrescriptionId, q.VersionNumber, ct);
        if (latest is null) return Result<PrescriptionPdfResponse>.Fail(ClinicalErrors.NotFound);
        if (latest.Version.Status != doc.Version.Status) return Result<PrescriptionPdfResponse>.Fail(MedicationErrors.Conflict("Prescription.InvalidState"));
        await reader.AuditAsync(fresh.Value, "PrescriptionVersionPdfExport", doc.Version.VersionId, ct);
        return Result<PrescriptionPdfResponse>.Ok(new(pdf.Value, $"wasla-prescription-{q.PrescriptionId:N}-v{q.VersionNumber}.pdf", q.Disposition));
    }
}
