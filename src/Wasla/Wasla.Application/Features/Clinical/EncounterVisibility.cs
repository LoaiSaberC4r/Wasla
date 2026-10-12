using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Application.Persistence;
using Wasla.Domain.Clinical;
using Wasla.Domain.Security;

namespace Wasla.Application.Features.Clinical;

public sealed record VisibilityCapabilities(bool CanChangeVisibility);
public sealed record EncounterVisibilityResponse(Guid EncounterId, EncounterVisibility Visibility,
    int VisibilityRevision, string RowVersion, VisibilityCapabilities Capabilities);
public sealed record VisibilityHistoryResponse(Guid HistoryId, EncounterVisibility? OldVisibility,
    EncounterVisibility NewVisibility, int Revision, Guid? ActorApplicationUserId, DateTime OccurredAtUtc, string Source);
public sealed record RecordPage<T>(IReadOnlyList<T> Items, string? NextCursor, bool HasMore);
public sealed record ChangeEncounterVisibilityCommand(Guid PracticeId, Guid EncounterId, EncounterVisibility Visibility, string RowVersion)
    : ICommand<EncounterVisibilityResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record VisibilityHistoryQuery(Guid PracticeId, Guid EncounterId, string? Cursor = null, int PageSize = 20)
    : IQuery<RecordPage<VisibilityHistoryResponse>>;
public interface IEncounterVisibilityReader
{
    Task<RecordPage<VisibilityHistoryResponse>> HistoryAsync(Guid encounterId, string? cursor, int pageSize, CancellationToken ct);
}
public interface IMedicalRecordTokens
{
    string OpaqueEntryId(string value);
    string Protect<T>(string purpose, T value);
    T? Read<T>(string purpose, string token);
}

internal sealed class ChangeEncounterVisibilityValidator : AbstractValidator<ChangeEncounterVisibilityCommand>
{
    public ChangeEncounterVisibilityValidator()
    {
        RuleFor(r => r.PracticeId).NotEmpty(); RuleFor(r => r.EncounterId).NotEmpty();
        RuleFor(r => r.Visibility).IsInEnum(); RuleFor(r => r.RowVersion).Must(TicketRowVersion.IsValid);
    }
}
internal sealed class VisibilityHistoryValidator : AbstractValidator<VisibilityHistoryQuery>
{
    public VisibilityHistoryValidator()
    {
        RuleFor(r => r.PracticeId).NotEmpty(); RuleFor(r => r.EncounterId).NotEmpty();
        RuleFor(r => r.PageSize).InclusiveBetween(1, 100); RuleFor(r => r.Cursor).MaximumLength(4096);
    }
}
internal sealed class ChangeEncounterVisibilityHandler(ClinicalAccessService access, IUnitOfWork<WaslaWritePersistence> uow,
    ITicketQueueLock locks, IDateTimeProvider clock) : ICommandHandler<ChangeEncounterVisibilityCommand, EncounterVisibilityResponse>
{
    public async Task<Result<EncounterVisibilityResponse>> Handle(ChangeEncounterVisibilityCommand r, CancellationToken ct)
    {
        var actor = await access.DoctorAsync(r.PracticeId, PermissionNames.MedicalEncounterVisibilityUpdateOwn, ct);
        if (actor.IsFailure) return Result<EncounterVisibilityResponse>.Fail(MedicalRecordErrors.Denied);
        await locks.AcquireIdempotencyAsync(Guid.Empty, "Phase16:Source", r.EncounterId.ToString("N"), ct);
        var e = await uow.WriteRepository<MedicalEncounter>().FirstOrDefaultAsync(new EncounterForUpdateSpecification(r.EncounterId), ct);
        if (e is null || e.DoctorId != actor.Value.DoctorId || e.DoctorPracticeId != r.PracticeId)
            return Result<EncounterVisibilityResponse>.Fail(MedicalRecordErrors.Denied);
        if (!TicketRowVersion.Matches(e.RowVersion, r.RowVersion)) return Result<EncounterVisibilityResponse>.Fail(MedicalRecordErrors.Conflict);
        var result = e.ChangeVisibility(r.Visibility, actor.Value.DoctorId, r.PracticeId, actor.Value.UserId, clock.UtcNow);
        if (result.IsFailure) return Result<EncounterVisibilityResponse>.Fail(result.Errors);
        await uow.SaveChangesAsync(ct);
        return Result<EncounterVisibilityResponse>.Ok(new(e.Id, e.Visibility, e.VisibilityRevision, Convert.ToBase64String(e.RowVersion), new(true)));
    }
}
internal sealed class VisibilityHistoryHandler(ClinicalAccessService access, IUnitOfWork<WaslaWritePersistence> uow,
    IEncounterVisibilityReader reader) : IQueryHandler<VisibilityHistoryQuery, RecordPage<VisibilityHistoryResponse>>
{
    public async Task<Result<RecordPage<VisibilityHistoryResponse>>> Handle(VisibilityHistoryQuery r, CancellationToken ct)
    {
        var actor = await access.DoctorAsync(r.PracticeId, PermissionNames.MedicalEncounterVisibilityViewHistoryOwn, ct);
        if (actor.IsFailure) return Result<RecordPage<VisibilityHistoryResponse>>.Fail(MedicalRecordErrors.Denied);
        var e = await uow.WriteRepository<MedicalEncounter>().FirstOrDefaultAsync(new EncounterForUpdateSpecification(r.EncounterId), ct);
        if (e is null || e.DoctorId != actor.Value.DoctorId || e.DoctorPracticeId != r.PracticeId)
            return Result<RecordPage<VisibilityHistoryResponse>>.Fail(MedicalRecordErrors.Denied);
        try { return Result<RecordPage<VisibilityHistoryResponse>>.Ok(await reader.HistoryAsync(e.Id, r.Cursor, r.PageSize, ct)); }
        catch (ArgumentException) { return Result<RecordPage<VisibilityHistoryResponse>>.Fail(MedicalRecordErrors.InvalidInput); }
    }
}
