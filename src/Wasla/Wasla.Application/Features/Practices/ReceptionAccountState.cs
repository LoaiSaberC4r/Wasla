using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Features.Doctors;
using Wasla.Application.Features.Subscriptions;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Practices;

namespace Wasla.Application.Features.Practices;

public sealed record SetReceptionAccountStateCommand(Guid ReceptionId, bool IsActive, string RowVersion)
    : ICommand<ReceptionResponse>, ITransactionalCommand<WaslaWritePersistence>;
internal sealed class SetReceptionAccountStateCommandValidator : AbstractValidator<SetReceptionAccountStateCommand>
{
    public SetReceptionAccountStateCommandValidator() { RuleFor(x => x.ReceptionId).NotEmpty(); RuleFor(x => x.RowVersion).Must(RowVersionCodec.IsValid); }
}
internal sealed class SetReceptionAccountStateCommandHandler(IWaslaDataStore store, ICurrentUser current,
    ISubscriptionDataStore subscriptions, PracticeSubscriptionAllocationService allocations) : ICommandHandler<SetReceptionAccountStateCommand, ReceptionResponse>
{
    public async Task<Result<ReceptionResponse>> Handle(SetReceptionAccountStateCommand request, CancellationToken cancellationToken)
    {
        var doctor = await DoctorPracticeAccess.ResolveDoctorAsync(store, current, cancellationToken);
        if (doctor.IsFailure) return Result<ReceptionResponse>.Fail(doctor.Errors);
        var access = await store.GetAccessSnapshotAsync(doctor.Value.ActorId, cancellationToken);
        if (access is null || !access.Permissions.Contains(Wasla.Domain.Subscriptions.SubscriptionPermissionNames.ReceptionAccountStateOwn, StringComparer.OrdinalIgnoreCase))
            return Result<ReceptionResponse>.Fail(ReceptionErrors.AccessDenied);
        await subscriptions.AcquireSubscriptionCapacityLockAsync(doctor.Value.Doctor.Id, cancellationToken);
        var owned = await ReceptionManagementAccess.LoadOwnedReceptionAsync(store, current, request.ReceptionId, cancellationToken);
        if (owned.IsFailure) return Result<ReceptionResponse>.Fail(owned.Errors);
        var record = owned.Value.Record;
        if (record.User.UserType != UserType.Reception) return Result<ReceptionResponse>.Fail(ReceptionErrors.AccessDenied);
        var row = RowVersionCodec.Decode(request.RowVersion);
        if (row is null || !record.Reception.RowVersion.AsSpan().SequenceEqual(row)) return Result<ReceptionResponse>.Fail(ReceptionErrors.ConcurrencyConflict);
        if (request.IsActive && !record.User.IsActive)
        {
            foreach (var assignment in (await store.ListReceptionAssignmentsAsync(record.Reception.Id, cancellationToken)).Where(a => a.IsActive))
            {
                var capacity = await allocations.ValidateReceptionAssignmentAsync(doctor.Value.Doctor.Id, assignment.DoctorPracticeId, record.Reception.Id, cancellationToken);
                if (capacity.IsFailure) return Result<ReceptionResponse>.Fail(capacity.Errors);
            }
        }
        if (request.IsActive) record.User.Activate(); else record.User.Deactivate();
        record.Reception.RecordAccountStateChange(); // Reception RowVersion protects the account transition in the same transaction.
        store.SetOriginalRowVersion(record.Reception, row);
        await store.SaveChangesAsync(cancellationToken);
        return Result<ReceptionResponse>.Ok(await ReceptionMapper.MapAsync(store, record, cancellationToken));
    }
}
