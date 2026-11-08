using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Domain.Results;
using Wasla.Application.Persistence;
using Wasla.Domain.Diagnostics;
using Wasla.Domain.Security;
namespace Wasla.Application.Features.Diagnostics;

public sealed record DependentDiagnosticMutation(Guid ChildPatientId, DiagnosticKind Kind, Guid RequestId, bool Withdraw,
    string IdempotencyKey, Guid? SubmissionId = null, string? RowVersion = null, IReadOnlyList<DiagnosticUpload>? Attachments = null,
    string? ExternalProviderName = null, DateOnly? ExternalReportDate = null, string? PatientNote = null)
    : ICommand<DependentDiagnosticReceipt>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record DependentDiagnosticReceipt(Guid SubmissionId, Guid RequestId, PatientSubmissionStatus Status, string RowVersion);
public sealed record DependentDiagnosticSubmissionsQuery(Guid ChildPatientId, DiagnosticKind Kind, Guid RequestId, int PageNumber = 1, int PageSize = 20) : IQuery<object>;
internal sealed class DependentDiagnosticMutationHandler(DiagnosticResultHandler handler) : ICommandHandler<DependentDiagnosticMutation, DependentDiagnosticReceipt>
{
    public async Task<Result<DependentDiagnosticReceipt>> Handle(DependentDiagnosticMutation r, CancellationToken ct)
    {
        if (!Enum.IsDefined(r.Kind) || r.ChildPatientId == Guid.Empty || r.RequestId == Guid.Empty || string.IsNullOrWhiteSpace(r.IdempotencyKey) || r.IdempotencyKey.Length > 100 ||
            r.Withdraw && (r.SubmissionId is null || string.IsNullOrWhiteSpace(r.RowVersion)) || !r.Withdraw && (r.Attachments is not { Count: > 0 and <= 5 } || r.Attachments.Any(a => a.Content.Length > 10485760)) ||
            r.ExternalProviderName?.Length > 200 || r.PatientNote?.Length > 2000)
            return Result<DependentDiagnosticReceipt>.Fail(DiagnosticErrors.Validation("DependentDiagnostic.Invalid"));
        var result = await handler.HandleDependentAsync(new(r.Kind, r.Withdraw ? DiagnosticResultMutation.Withdraw : DiagnosticResultMutation.Submit,
            r.RequestId, SubmissionId: r.SubmissionId, RowVersion: r.RowVersion, Attachments: r.Attachments, ExternalProviderName: r.ExternalProviderName,
            ExternalReportDate: r.ExternalReportDate, PatientNote: r.PatientNote, IdempotencyKey: r.IdempotencyKey), r.ChildPatientId, ct);
        return result.IsFailure ? Result<DependentDiagnosticReceipt>.Fail(result.Errors) : Result<DependentDiagnosticReceipt>.Ok(
            new(result.Value.Submission!.SubmissionId, r.RequestId, result.Value.Submission.Status, result.Value.Submission.RowVersion));
    }
}
internal sealed class DependentDiagnosticSubmissionsHandler(DiagnosticAccess access, IDiagnosticReadService reader,
    BuildingBlock.Application.Time.IDateTimeProvider clock) : IQueryHandler<DependentDiagnosticSubmissionsQuery, object>
{
    public async Task<Result<object>> Handle(DependentDiagnosticSubmissionsQuery r, CancellationToken ct)
    {
        if (!Enum.IsDefined(r.Kind) || r.PageNumber is < 1 or > 10000 || r.PageSize is < 1 or > 100) return Result<object>.Fail(DiagnosticErrors.Validation("DependentDiagnostic.Invalid"));
        var actor = await access.DependentAsync(r.ChildPatientId, DependentDiagnosticPermissionNames.For(r.Kind == DiagnosticKind.Lab, "View"), ct);
        if (actor.IsFailure) return Result<object>.Fail(actor.Errors);
        var order = await reader.RequestAsync(r.Kind, r.RequestId, r.ChildPatientId, true, actor.Value.Permissions, ct);
        if (order is null) return Result<object>.Fail(DiagnosticErrors.Denied());
        var result = await reader.ReadAsync(new(r.Kind, DiagnosticReadResource.Submissions, true, Filter: new(RequestId: r.RequestId, PatientId: r.ChildPatientId, PageNumber: r.PageNumber, PageSize: r.PageSize)), r.ChildPatientId, actor.Value.Permissions, ct);
        if (result is null || !await access.RecheckDependentAsync(actor.Value, ct)) return Result<object>.Fail(DiagnosticErrors.Denied());
        await reader.AuditReadAsync(r.Kind, DiagnosticReadResource.Submissions, r.RequestId, null, actor.Value.UserId, clock.UtcNow, ct);
        return await access.RecheckDependentAsync(actor.Value, ct) ? Result<object>.Ok(result) : Result<object>.Fail(DiagnosticErrors.Denied());
    }
}
