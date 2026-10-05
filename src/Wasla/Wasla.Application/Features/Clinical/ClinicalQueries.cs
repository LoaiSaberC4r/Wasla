using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Persistence;
using Wasla.Domain.Clinical;
using Wasla.Domain.Security;

namespace Wasla.Application.Features.Clinical;

internal sealed class ListDoctorEncountersValidator : AbstractValidator<ListDoctorEncountersQuery>
{
    public ListDoctorEncountersValidator()
    {
        RuleFor(q => q.PracticeId).NotEmpty(); RuleFor(q => q.PageNumber).InclusiveBetween(1, 1000000);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 100); RuleFor(q => q.Search).MaximumLength(200);
        RuleFor(q => q.Status).Must(s => s is null || Enum.IsDefined(s.Value));
        RuleFor(q => q.FromDate).Must(d => d is null || d > DateOnly.MinValue && d < DateOnly.MaxValue);
        RuleFor(q => q.ToDate).Must(d => d is null || d > DateOnly.MinValue && d < DateOnly.MaxValue);
        RuleFor(q => q.ToDate).Must((q, d) => d is null || q.FromDate is null || d >= q.FromDate);
    }
}
internal sealed class ListMyEncountersValidator : AbstractValidator<ListMyEncountersQuery>
{
    public ListMyEncountersValidator() { RuleFor(q => q.PageNumber).InclusiveBetween(1, 1000000); RuleFor(q => q.PageSize).InclusiveBetween(1, 100); }
}
internal sealed class ListMyFollowUpEligibilitiesValidator : AbstractValidator<ListMyFollowUpEligibilitiesQuery>
{
    public ListMyFollowUpEligibilitiesValidator()
    {
        RuleFor(q => q.PageNumber).InclusiveBetween(1, 1000000); RuleFor(q => q.PageSize).InclusiveBetween(1, 100);
        RuleFor(q => q.Status).Must(s => s is null || Enum.IsDefined(s.Value));
    }
}

internal sealed class ListDoctorEncountersHandler(ClinicalAccessService access, IClinicalReadService reader,
    IWaslaDataStore store) : IQueryHandler<ListDoctorEncountersQuery, ClinicalPage<EncounterSummaryResponse>>
{
    public async Task<Result<ClinicalPage<EncounterSummaryResponse>>> Handle(ListDoctorEncountersQuery r, CancellationToken ct)
    {
        var actor = await access.DoctorAsync(r.PracticeId, PermissionNames.MedicalEncountersViewOwn, ct);
        if (actor.IsFailure) return Result<ClinicalPage<EncounterSummaryResponse>>.Fail(actor.Errors);
        var config = await store.FindDoctorPracticeConfigurationAsync(r.PracticeId, ct);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(config!.TimeZoneId);
        DateTime? Boundary(DateOnly? date)
        {
            if (date is null) return null;
            var local = DateTime.SpecifyKind(date.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
            // Cairo's spring transition can skip midnight. The day begins at its first valid instant.
            while (zone.IsInvalidTime(local)) local = local.AddMinutes(1);
            return zone.IsAmbiguousTime(local)
                ? new DateTimeOffset(local, zone.GetAmbiguousTimeOffsets(local).Max()).UtcDateTime
                : TimeZoneInfo.ConvertTimeToUtc(local, zone);
        }
        return Result<ClinicalPage<EncounterSummaryResponse>>.Ok(await reader.ListEncountersAsync(actor.Value.DoctorId,
            r.PracticeId, r.PatientId, r.Status, Boundary(r.FromDate), Boundary(r.ToDate?.AddDays(1)), r.Search, r.PageNumber, r.PageSize, ct));
    }
}
internal sealed class GetDoctorEncounterHandler(ClinicalAccessService access, IClinicalReadService reader, IDateTimeProvider clock)
    : IQueryHandler<GetDoctorEncounterQuery, EncounterDetailsResponse>
{
    public async Task<Result<EncounterDetailsResponse>> Handle(GetDoctorEncounterQuery r, CancellationToken ct)
    {
        var actor = await access.DoctorAsync(r.PracticeId, PermissionNames.MedicalEncountersViewOwn, ct);
        if (actor.IsFailure) return Result<EncounterDetailsResponse>.Fail(actor.Errors);
        if (!access.HasPermission(PermissionNames.DiagnosesViewOwn)) return Result<EncounterDetailsResponse>.Fail(ClinicalErrors.AccessDenied);
        var details = await reader.DoctorDetailsAsync(actor.Value.DoctorId, r.PracticeId, r.Id, r.ByTicket, ct);
        if (details is null) return Result<EncounterDetailsResponse>.Fail(ClinicalErrors.NotFound);
        await reader.AuditReadAsync(details.EncounterId, actor.Value.UserId, clock.UtcNow, ct);
        return Result<EncounterDetailsResponse>.Ok(details with { Capabilities = ClinicalCapabilities.For(details, access) });
    }
}
internal sealed class ListMyEncountersHandler(ClinicalAccessService access, IClinicalReadService reader)
    : IQueryHandler<ListMyEncountersQuery, ClinicalPage<EncounterSummaryResponse>>
{
    public async Task<Result<ClinicalPage<EncounterSummaryResponse>>> Handle(ListMyEncountersQuery r, CancellationToken ct)
    {
        var patient = await access.OwnPatientAsync(ct);
        return patient.IsFailure ? Result<ClinicalPage<EncounterSummaryResponse>>.Fail(patient.Errors)
            : Result<ClinicalPage<EncounterSummaryResponse>>.Ok(await reader.ListEncountersAsync(null, null, patient.Value,
                EncounterStatus.Completed, null, null, null, r.PageNumber, r.PageSize, ct));
    }
}
internal sealed class GetMyEncounterHandler(ClinicalAccessService access, IClinicalReadService reader, IDateTimeProvider clock)
    : IQueryHandler<GetMyEncounterQuery, PatientEncounterDetailsResponse>
{
    public async Task<Result<PatientEncounterDetailsResponse>> Handle(GetMyEncounterQuery r, CancellationToken ct)
    {
        var patient = await access.OwnPatientAsync(ct);
        if (patient.IsFailure) return Result<PatientEncounterDetailsResponse>.Fail(patient.Errors);
        var details = await reader.PatientDetailsAsync(patient.Value, r.EncounterId, ct);
        if (details is null) return Result<PatientEncounterDetailsResponse>.Fail(ClinicalErrors.NotFound);
        await reader.AuditReadAsync(details.EncounterId, access.ActorId, clock.UtcNow, ct);
        return Result<PatientEncounterDetailsResponse>.Ok(details);
    }
}
internal sealed class ListEncounterAmendmentsHandler(ClinicalAccessService access, IClinicalReadService reader, IDateTimeProvider clock)
    : IQueryHandler<ListEncounterAmendmentsQuery, IReadOnlyList<EncounterAmendmentResponse>>
{
    public async Task<Result<IReadOnlyList<EncounterAmendmentResponse>>> Handle(ListEncounterAmendmentsQuery r, CancellationToken ct)
    {
        var actor = await access.DoctorAsync(r.PracticeId, PermissionNames.MedicalEncountersViewOwn, ct);
        if (actor.IsFailure) return Result<IReadOnlyList<EncounterAmendmentResponse>>.Fail(actor.Errors);
        if (!access.HasPermission(PermissionNames.DiagnosesViewOwn)) return Result<IReadOnlyList<EncounterAmendmentResponse>>.Fail(ClinicalErrors.AccessDenied);
        var details = await reader.DoctorDetailsAsync(actor.Value.DoctorId, r.PracticeId, r.EncounterId, false, ct);
        if (details is null) return Result<IReadOnlyList<EncounterAmendmentResponse>>.Fail(ClinicalErrors.NotFound);
        await reader.AuditReadAsync(details.EncounterId, actor.Value.UserId, clock.UtcNow, ct);
        return Result<IReadOnlyList<EncounterAmendmentResponse>>.Ok(await reader.AmendmentsAsync(r.EncounterId, ct));
    }
}
internal sealed class ListMyFollowUpEligibilitiesHandler(ClinicalAccessService access, IClinicalReadService reader)
    : IQueryHandler<ListMyFollowUpEligibilitiesQuery, ClinicalPage<FollowUpEligibilityResponse>>
{
    public async Task<Result<ClinicalPage<FollowUpEligibilityResponse>>> Handle(ListMyFollowUpEligibilitiesQuery r, CancellationToken ct)
    {
        var ids = await access.BookablePatientIdsAsync(r.PatientId, ct);
        return ids.IsFailure ? Result<ClinicalPage<FollowUpEligibilityResponse>>.Fail(ids.Errors)
            : Result<ClinicalPage<FollowUpEligibilityResponse>>.Ok(await reader.EligibilitiesAsync(ids.Value, null, r.Status, null, r.PageNumber, r.PageSize, ct));
    }
}
internal sealed class GetMyFollowUpEligibilityHandler(ClinicalAccessService access, IClinicalReadService reader)
    : IQueryHandler<GetMyFollowUpEligibilityQuery, FollowUpEligibilityResponse>
{
    public async Task<Result<FollowUpEligibilityResponse>> Handle(GetMyFollowUpEligibilityQuery r, CancellationToken ct)
    {
        var ids = await access.BookablePatientIdsAsync(null, ct);
        if (ids.IsFailure) return Result<FollowUpEligibilityResponse>.Fail(ids.Errors);
        var page = await reader.EligibilitiesAsync(ids.Value, null, null, r.EligibilityId, 1, 1, ct);
        return page.Items.Count == 0 ? Result<FollowUpEligibilityResponse>.Fail(FollowUpErrors.NotFound)
            : Result<FollowUpEligibilityResponse>.Ok(page.Items[0]);
    }
}
internal sealed class ListReceptionFollowUpEligibilitiesHandler(ClinicalAccessService access, IClinicalReadService reader)
    : IQueryHandler<ListReceptionFollowUpEligibilitiesQuery, IReadOnlyList<ReceptionFollowUpEligibilityResponse>>
{
    public async Task<Result<IReadOnlyList<ReceptionFollowUpEligibilityResponse>>> Handle(ListReceptionFollowUpEligibilitiesQuery r, CancellationToken ct)
    {
        var allowed = await access.ReceptionAsync(r.PracticeId, ct);
        if (allowed.IsFailure) return Result<IReadOnlyList<ReceptionFollowUpEligibilityResponse>>.Fail(allowed.Errors);
        var page = await reader.EligibilitiesAsync([r.PatientId], r.PracticeId, FollowUpEligibilityStatus.Available, null, 1, 100, ct);
        return Result<IReadOnlyList<ReceptionFollowUpEligibilityResponse>>.Ok(page.Items.Select(e =>
            new ReceptionFollowUpEligibilityResponse(e.EligibilityId, e.Patient.Id, e.Doctor.Id, e.Practice.Id,
                e.ValidUntil, e.Status, e.CanBook, e.RowVersion)).ToArray());
    }
}
