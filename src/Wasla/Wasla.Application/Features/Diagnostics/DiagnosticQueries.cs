using BuildingBlock.Application.Abstraction;
using BuildingBlock.Domain.Results;
using Wasla.Application.Features.Medications;
using Wasla.Application.Media;
using Wasla.Domain.Common;
using Wasla.Domain.Diagnostics;

namespace Wasla.Application.Features.Diagnostics;

internal sealed class ReadDiagnosticHandler(DiagnosticAccess access, IDiagnosticReadService reader, IPrivateMediaReader media, BuildingBlock.Application.Time.IDateTimeProvider clock)
    : IQueryHandler<ReadDiagnosticQuery, object>, IQueryHandler<DiagnosticMediaQuery, PrivateMedia>
{
    private static string Permission(ReadDiagnosticQuery r) => r.Kind + (r.Resource switch
    {
        DiagnosticReadResource.Results or DiagnosticReadResource.Versions => r.Patient ? "Results.ViewOwnCurrent" : "Results.ViewOwn",
        DiagnosticReadResource.Submissions => "ResultSubmissions.ViewOwn",
        _ => r.Patient ? "Requests.ViewOwnIssued" : "Requests.ViewOwn"
    });
    public async Task<Result<object>> Handle(ReadDiagnosticQuery r, CancellationToken ct)
    {
        var actor = await access.ActorAsync(Permission(r), r.Patient ? UserType.Patient : UserType.Doctor, ct);
        if (actor.IsFailure) return Result<object>.Fail(actor.Errors);
        if (r.Patient && r.Resource is DiagnosticReadResource.Versions or DiagnosticReadResource.RequestHistory or DiagnosticReadResource.Draft)
            return Result<object>.Fail(DiagnosticErrors.Denied());
        var response = await reader.ReadAsync(r, actor.Value.OwnerId, actor.Value.Permissions, ct);
        if (response is not null && r.Id is { } id) await reader.AuditReadAsync(r.Kind, r.Resource, id, null, actor.Value.UserId, clock.UtcNow, ct);
        return response is null ? Result<object>.Fail(DiagnosticErrors.NotFound(r.Kind + "Request.NotFound")) : Result<object>.Ok(response);
    }
    public async Task<Result<PrivateMedia>> Handle(DiagnosticMediaQuery r, CancellationToken ct)
    {
        var permission = r.Kind + (r.Submission ? "ResultSubmissions.ViewOwn" : r.Patient ? "Results.ViewOwnCurrent" : "Results.ViewOwn");
        var actor = await access.ActorAsync(permission, r.Patient ? UserType.Patient : UserType.Doctor, ct);
        if (actor.IsFailure) return Result<PrivateMedia>.Fail(actor.Errors);
        var key = await reader.MediaKeyAsync(r, actor.Value.OwnerId, ct);
        if (key is null) return Result<PrivateMedia>.Fail(DiagnosticErrors.NotFound(r.Kind + "Result.AttachmentNotFound"));
        var content = await media.OpenAsync(key, ct);
        if (content is not null) await reader.AuditReadAsync(r.Kind, r.Submission ? DiagnosticReadResource.Submissions : DiagnosticReadResource.Results,
            r.Id, r.AttachmentId, actor.Value.UserId, clock.UtcNow, ct);
        return content is null ? Result<PrivateMedia>.Fail(DiagnosticErrors.NotFound(r.Kind + "Result.AttachmentNotFound")) : Result<PrivateMedia>.Ok(content);
    }
}

internal sealed class DiagnosticImportHandlers(DiagnosticAccess access, IDiagnosticImportService importer,
    IDiagnosticReadService reader, MedicationIdempotency idem, BuildingBlock.Application.Time.IDateTimeProvider clock)
    : ICommandHandler<PreviewDiagnosticImportCommand, DiagnosticImportBatchResponse>,
      ICommandHandler<MutateDiagnosticImportCommand, DiagnosticImportBatchResponse>, IQueryHandler<ReadDiagnosticImportQuery, object>
{
    public async Task<Result<DiagnosticImportBatchResponse>> Handle(PreviewDiagnosticImportCommand r, CancellationToken ct)
    {
        var actor = await access.ActorAsync(r.Kind + "Catalog.Import", UserType.MedicalCatalogManager, ct);
        if (actor.IsFailure) return Result<DiagnosticImportBatchResponse>.Fail(actor.Errors);
        return await importer.StageAsync(r, actor.Value.UserId, clock.UtcNow, ct);
    }
    public async Task<Result<DiagnosticImportBatchResponse>> Handle(MutateDiagnosticImportCommand r, CancellationToken ct)
    {
        var actor = await access.ActorAsync(r.Kind + "Catalog.Import", UserType.MedicalCatalogManager, ct);
        if (actor.IsFailure) return Result<DiagnosticImportBatchResponse>.Fail(actor.Errors);
        await idem.LockResourceAsync("Phase15:" + r.Kind + "CatalogApply", Guid.Empty, ct);
        return r.Apply ? await idem.ExecuteAsync(actor.Value.UserId, "Phase15:" + r.Kind + "ImportApply", r.IdempotencyKey, r,
            () => importer.MutateAsync(r, actor.Value.UserId, clock.UtcNow, ct), ct) : await importer.MutateAsync(r, actor.Value.UserId, clock.UtcNow, ct);
    }
    public async Task<Result<object>> Handle(ReadDiagnosticImportQuery r, CancellationToken ct)
    {
        var actor = await access.ActorAsync(r.Kind + "Catalog.ImportHistory", UserType.MedicalCatalogManager, ct);
        if (actor.IsFailure) return Result<object>.Fail(actor.Errors);
        var response = await reader.ImportAsync(r, actor.Value.Permissions, ct);
        return response is null ? Result<object>.Fail(DiagnosticErrors.NotFound("DiagnosticCatalogImport.BatchNotFound")) : Result<object>.Ok(response);
    }
}
