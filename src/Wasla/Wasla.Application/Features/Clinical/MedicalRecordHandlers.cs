using System.Diagnostics;
using BuildingBlock.Application.Abstraction;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Media;
using Wasla.Domain.Clinical;
using Wasla.Domain.Security;

namespace Wasla.Application.Features.Clinical;

public static class MedicalRecordInput
{
    public static readonly string[] ResourceTypes = ["prescription", "lab-request", "lab-result", "radiology-request", "radiology-result"];
    public static readonly string[] Filters = ["all", "visits", "diagnoses", "prescriptions", "labs", "radiology"];
    public static bool ValidTypes(string? types) => types is null || types.Split(',').All(t => Filters.Contains(t.Trim(), StringComparer.Ordinal));
    public static string Permission(MedicalRecordMode mode, bool log = false) => mode == MedicalRecordMode.OwnPatient
        ? log ? PermissionNames.MedicalRecordsViewOwnAccessLog : PermissionNames.MedicalRecordsViewOwn
        : mode == MedicalRecordMode.Dependent ? PermissionNames.MedicalRecordsViewDependent : PermissionNames.MedicalRecordsViewTreatingPatient;
}
internal sealed class MedicalRecordFilterValidator : AbstractValidator<MedicalRecordFilter>
{
    public MedicalRecordFilterValidator()
    {
        RuleFor(f => f.PageSize).InclusiveBetween(1, 100); RuleFor(f => f.Cursor).MaximumLength(4096);
        RuleFor(f => f.Types).MaximumLength(100).Must(MedicalRecordInput.ValidTypes);
        RuleFor(f => f.FromDate).Must(d => d is null || d > DateOnly.MinValue && d < DateOnly.MaxValue);
        RuleFor(f => f.ToDate).Must(d => d is null || d > DateOnly.MinValue && d < DateOnly.MaxValue);
        RuleFor(f => f.ToDate).Must((f, d) => d is null || f.FromDate is null || d >= f.FromDate);
    }
}
internal sealed class ReadMedicalRecordValidator : AbstractValidator<ReadMedicalRecordQuery>
{
    public ReadMedicalRecordValidator()
    {
        RuleFor(r => r.Read).IsInEnum(); RuleFor(r => r.Mode).IsInEnum();
        When(r => r.Filter is not null, () => RuleFor(r => r.Filter!).SetValidator(new MedicalRecordFilterValidator()));
        When(r => r.Mode == MedicalRecordMode.TreatingDoctor, () => { RuleFor(r => r.PracticeId).NotEmpty(); RuleFor(r => r.TicketId).NotEmpty(); });
        When(r => r.Read == MedicalRecordRead.Item, () => { RuleFor(r => r.SourceId).NotEmpty(); RuleFor(r => r.ResourceType).Must(t => MedicalRecordInput.ResourceTypes.Contains(t, StringComparer.Ordinal)); });
        When(r => r.Read == MedicalRecordRead.Encounter, () => RuleFor(r => r.EncounterId).NotEmpty());
    }
}
internal sealed class ReadMedicalRecordMediaValidator : AbstractValidator<ReadMedicalRecordMediaQuery>
{
    public ReadMedicalRecordMediaValidator()
    {
        RuleFor(r => r.Mode).IsInEnum(); RuleFor(r => r.ResourceType).Must(t => MedicalRecordInput.ResourceTypes.Contains(t, StringComparer.Ordinal));
        RuleFor(r => r.SourceId).NotEmpty(); RuleFor(r => r.AttachmentId).NotEmpty();
    }
}
internal sealed class ReadMedicalRecordHandler(IMedicalRecordAccessPolicy policy, IMedicalRecordReadService reader) : IQueryHandler<ReadMedicalRecordQuery, object>
{
    public async Task<Result<object>> Handle(ReadMedicalRecordQuery r, CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        var scope = await policy.ScopeAsync(r.Mode, r.PracticeId, r.TicketId, r.DependentPatientId, MedicalRecordInput.Permission(r.Mode, r.Read == MedicalRecordRead.AccessLog), ct);
        if (scope.IsFailure) { MedicalRecordTelemetry.Denied(r.Mode); return Result<object>.Fail(MedicalRecordErrors.Denied); }
        try
        {
            var value = await reader.ReadAsync(r, scope.Value, ct);
            if (value is null) { MedicalRecordTelemetry.Denied(r.Mode); return Result<object>.Fail(MedicalRecordErrors.Denied); }
            MedicalRecordTelemetry.Read(r.Mode, Stopwatch.GetElapsedTime(started).TotalMilliseconds, r.Filter?.PageSize ?? 20);
            return Result<object>.Ok(value);
        }
        catch (ArgumentException) { return Result<object>.Fail(MedicalRecordErrors.InvalidInput); }
    }
}
internal sealed class ReadMedicalRecordMediaHandler(IMedicalRecordAccessPolicy policy, IMedicalRecordMediaReader reader) : IQueryHandler<ReadMedicalRecordMediaQuery, PrivateMedia>
{
    public async Task<Result<PrivateMedia>> Handle(ReadMedicalRecordMediaQuery r, CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        var scope = await policy.ScopeAsync(r.Mode, r.PracticeId, r.TicketId, r.DependentPatientId, MedicalRecordInput.Permission(r.Mode), ct);
        if (scope.IsFailure) { MedicalRecordTelemetry.Denied(r.Mode); return Result<PrivateMedia>.Fail(MedicalRecordErrors.Denied); }
        var media = await reader.ReadAsync(r, scope.Value, ct);
        if (media is null) { MedicalRecordTelemetry.Denied(r.Mode); return Result<PrivateMedia>.Fail(MedicalRecordErrors.Denied); }
        MedicalRecordTelemetry.Read(r.Mode, Stopwatch.GetElapsedTime(started).TotalMilliseconds, 1);
        return Result<PrivateMedia>.Ok(media);
    }
}
