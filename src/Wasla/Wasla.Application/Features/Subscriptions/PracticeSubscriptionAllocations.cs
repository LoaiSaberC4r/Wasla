using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Features.Doctors;
using Wasla.Application.Features.PublicDiscovery;
using Wasla.Application.Features.Reservations;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Subscriptions;

namespace Wasla.Application.Features.Subscriptions;

public sealed record PracticeAllocationQuery(Guid PracticeId) : IQuery<PracticeAllocationDetails>;
public sealed record PracticeAllocationCommand(Guid PracticeId, Guid? TargetSubscriptionContractId, IReadOnlyList<Guid> ContinuedReceptionIds,
    string RowVersion, string IdempotencyKey, string? Reason = null) : ICommand<PracticeAllocationDetails>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record PracticeAllocationDetails(Guid PracticeId, PracticeSubscriptionAllocation? Allocation, DoctorSubscription? EffectivePeriod,
    SubscriptionUsage? ContractUsage, IReadOnlyList<Guid> ActiveReceptionIds);
internal sealed class PracticeAllocationCommandValidator : AbstractValidator<PracticeAllocationCommand>
{
    public PracticeAllocationCommandValidator()
    {
        RuleFor(x => x.PracticeId).NotEmpty(); RuleFor(x => x.RowVersion).Must(RowVersionCodec.IsValid);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(150); RuleFor(x => x.Reason).MaximumLength(2000);
        RuleFor(x => x.ContinuedReceptionIds).NotNull();
    }
}
internal sealed class PracticeAllocationCommandHandler(PracticeSubscriptionAllocationService service) : ICommandHandler<PracticeAllocationCommand, PracticeAllocationDetails>
{
    public Task<Result<PracticeAllocationDetails>> Handle(PracticeAllocationCommand request, CancellationToken ct) => service.MoveAsync(request, ct);
}
internal sealed class PracticeAllocationQueryHandler(PracticeSubscriptionAllocationService service) : IQueryHandler<PracticeAllocationQuery, PracticeAllocationDetails>
{
    public Task<Result<PracticeAllocationDetails>> Handle(PracticeAllocationQuery request, CancellationToken ct) => service.ReadAsync(request.PracticeId, ct);
}
public sealed class PracticeSubscriptionAllocationService(ISubscriptionDataStore store, IWaslaDataStore data, ICurrentUser current,
    IDateTimeProvider clock, IReservationProjectionInvalidationOutbox projections, SubscriptionLifecycle lifecycle)
{
    public async Task<Result> ValidateReceptionAssignmentAsync(Guid doctorId, Guid practiceId, Guid receptionId, CancellationToken ct)
    {
        await store.AcquireSubscriptionCapacityLockAsync(doctorId, ct);
        await store.ApplyDueSelectionsAsync(doctorId, clock.UtcNow, current.UserId!.Value, ct);
        var practice = await data.FindDoctorPracticeAsync(practiceId, ct);
        var reception = await data.FindDoctorReceptionAsync(receptionId, ct);
        if (practice?.DoctorId != doctorId || reception?.Reception.OwnerDoctorId != doctorId) return Result.Fail(SubscriptionErrors.NotFound);
        var period = await store.EffectivePracticePeriodAsync(doctorId, practiceId, clock.UtcNow, ct);
        if (period is null || !period.Terms.Features.Any(f => f.Code == "RECEPTION_MANAGEMENT" && f.AccessLevel == FeatureAccessLevel.Full))
            return Result.Fail(SubscriptionErrors.Denied("FeatureNotEntitled"));
        var ids = (await store.ActiveReceptionIdsAsync(period.SubscriptionContractId, null, ct)).ToHashSet();
        ids.Add(receptionId);
        return ids.Count > period.ReceptionLimit ? Result.Fail(SubscriptionErrors.Conflict("ReceptionCapacityReached")) : Result.Ok();
    }
    private async Task<Guid?> ActorDoctorAsync(string permission, CancellationToken ct)
    {
        if (current.UserId is not { } actor || !current.IsAuthenticated) return null;
        var access = await data.GetAccessSnapshotAsync(actor, ct);
        return access is { User.UserType: UserType.Doctor, User.IsActive: true, User.IsFirstLogin: false, DoctorStatus: DoctorApprovalStatus.Approved } &&
            access.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase) ? access.DoctorId : null;
    }
    public async Task<Result<DoctorSubscription>> ValidateTargetAsync(Guid doctorId, Guid contractId, CancellationToken ct)
    {
        await store.AcquireSubscriptionCapacityLockAsync(doctorId, ct);
        var actor = current.UserId is { } userId ? await data.GetAccessSnapshotAsync(userId, ct) : null;
        if (actor is not { User.UserType: UserType.Doctor, User.IsActive: true, User.IsFirstLogin: false } || actor.DoctorId != doctorId ||
            !actor.Permissions.Any(p => p is "DoctorPractices.ManageOwn" or "Subscriptions.AllocationsManageOwn")) return Result<DoctorSubscription>.Fail(SubscriptionErrors.Denied());
        var doctor = await data.FindDoctorByIdAsync(doctorId, ct);
        var account = doctor is null ? null : await data.FindUserByIdAsync(doctor.ApplicationUserId, ct);
        if (doctor?.ApprovalStatus != DoctorApprovalStatus.Approved || account is not { IsActive: true }) return Result<DoctorSubscription>.Fail(SubscriptionErrors.Denied("DoctorNotApproved"));
        await store.ApplyDueSelectionsAsync(doctorId, clock.UtcNow, current.UserId ?? doctor.ApplicationUserId, ct);
        var contract = await store.ContractAsync(contractId, ct);
        if (contract?.DoctorId != doctorId) return Result<DoctorSubscription>.Fail(SubscriptionErrors.NotFound);
        var period = await store.EffectivePeriodAsync(contractId, clock.UtcNow, ct);
        if (period is null) return Result<DoctorSubscription>.Fail(SubscriptionErrors.Denied("TargetSubscriptionNotActive"));
        if (!period.Terms.Features.Any(f => f.Code == "PRACTICE_MANAGEMENT" && f.AccessLevel == FeatureAccessLevel.Full)) return Result<DoctorSubscription>.Fail(SubscriptionErrors.Denied());
        var usage = await store.ContractUsageAsync(contractId, ct);
        if (usage.PracticeCount >= period.PracticeLimit) CommercialSubscriptionDiagnostics.Capacity("Practice");
        return usage.PracticeCount >= period.PracticeLimit ? Result<DoctorSubscription>.Fail(SubscriptionErrors.Conflict("ContractCapacityReached")) : Result<DoctorSubscription>.Ok(period);
    }
    public async Task<Result<PracticeAllocationDetails>> ReadAsync(Guid practiceId, CancellationToken ct)
    {
        var doctorId = await ActorDoctorAsync(SubscriptionPermissionNames.ViewOwn, ct);
        var practice = await data.FindDoctorPracticeAsync(practiceId, ct);
        return doctorId is null || practice?.DoctorId != doctorId ? Result<PracticeAllocationDetails>.Fail(SubscriptionErrors.NotFound) :
            Result<PracticeAllocationDetails>.Ok(await DetailsAsync(practiceId, doctorId.Value, ct));
    }
    private async Task<PracticeAllocationDetails> DetailsAsync(Guid practiceId, Guid doctorId, CancellationToken ct)
    {
        var allocation = await store.AllocationAsync(practiceId, ct);
        return new(practiceId, allocation, await store.EffectivePracticePeriodAsync(doctorId, practiceId, clock.UtcNow, ct),
            allocation is null ? null : await store.ContractUsageAsync(allocation.SubscriptionContractId, ct),
            allocation is null ? [] : await store.ActiveReceptionIdsAsync(allocation.SubscriptionContractId, null, ct));
    }
    public async Task<Result<PracticeAllocationDetails>> MoveAsync(PracticeAllocationCommand command, CancellationToken ct)
    {
        var doctorId = await ActorDoctorAsync(SubscriptionPermissionNames.AllocationsManageOwn, ct);
        if (doctorId is null) return Result<PracticeAllocationDetails>.Fail(SubscriptionErrors.Denied());
        await store.AcquireSubscriptionCapacityLockAsync(doctorId.Value, ct);
        var practice = await data.FindDoctorPracticeAsync(command.PracticeId, ct);
        if (practice?.DoctorId != doctorId) return Result<PracticeAllocationDetails>.Fail(SubscriptionErrors.NotFound);
        var actor = current.UserId!.Value;
        var fingerprint = SubscriptionManager.MutationFingerprint(new { Operation = "PracticeAllocation", Actor = actor, command.PracticeId, command.TargetSubscriptionContractId,
            ContinuedReceptionIds = command.ContinuedReceptionIds.Distinct().Order().ToArray(), command.RowVersion, command.Reason });
        var receipt = await store.ReceiptAsync(command.IdempotencyKey.Trim(), ct);
        if (receipt is not null) return receipt.ActorId == actor && receipt.Fingerprint == fingerprint
            ? Result<PracticeAllocationDetails>.Ok(await DetailsAsync(command.PracticeId, doctorId.Value, ct)) : Result<PracticeAllocationDetails>.Fail(SubscriptionErrors.Conflict("IdempotencyConflict"));
        await store.ApplyDueSelectionsAsync(doctorId.Value, clock.UtcNow, actor, ct);
        var source = await store.AllocationAsync(command.PracticeId, ct);
        var row = RowVersionCodec.Decode(command.RowVersion);
        var actualRow = source?.RowVersion ?? practice.RowVersion;
        if (row is null || !actualRow.AsSpan().SequenceEqual(row)) return Result<PracticeAllocationDetails>.Fail(SubscriptionErrors.Conflict("StaleRowVersion"));
        if (source?.SubscriptionContractId == command.TargetSubscriptionContractId) return Result<PracticeAllocationDetails>.Fail(SubscriptionErrors.Conflict("AllocationConflict"));
        var assignments = await store.AssignmentsAsync(command.PracticeId, ct);
        if (command.TargetSubscriptionContractId is { } targetId)
        {
            var target = await ValidateTargetAsync(doctorId.Value, targetId, ct);
            if (target.IsFailure) return Result<PracticeAllocationDetails>.Fail(target.Errors);
            if (command.ContinuedReceptionIds.Any(id => !assignments.Any(a => a.ReceptionId == id && a.IsActive))) return Result<PracticeAllocationDetails>.Fail(SubscriptionErrors.Invalid("ReceptionSelectionInvalid"));
            var staff = (await store.ActiveReceptionIdsAsync(targetId, command.PracticeId, ct)).ToHashSet();
            foreach (var id in command.ContinuedReceptionIds)
            {
                var reception = await data.FindDoctorReceptionAsync(id, ct);
                if (reception?.Reception.OwnerDoctorId != doctorId || !reception.User.IsActive) return Result<PracticeAllocationDetails>.Fail(SubscriptionErrors.Invalid("ReceptionSelectionInvalid"));
                staff.Add(id);
            }
            if (staff.Count > target.Value.ReceptionLimit) return Result<PracticeAllocationDetails>.Fail(SubscriptionErrors.Conflict("ReceptionCapacityReached"));
            if (source is not null) { var release = source.Release(clock.UtcNow, actor, command.Reason); if (release.IsFailure) return Result<PracticeAllocationDetails>.Fail(release.Errors); }
            await store.SaveChangesAsync(ct); // Release filtered current index inside the transaction.
            store.Add(new PracticeSubscriptionAllocation(command.PracticeId, targetId, clock.UtcNow, actor));
        }
        else
        {
            if (source is null) return Result<PracticeAllocationDetails>.Fail(SubscriptionErrors.Conflict("PracticeUnallocated"));
            var release = source.Release(clock.UtcNow, actor, command.Reason);
            if (release.IsFailure) return Result<PracticeAllocationDetails>.Fail(release.Errors);
        }
        foreach (var assignment in assignments.Where(a => a.IsActive && (!command.TargetSubscriptionContractId.HasValue || !command.ContinuedReceptionIds.Contains(a.ReceptionId)))) assignment.Deactivate(actor);
        var periods = await store.PeriodsAsync(doctorId.Value, ct);
        if (source is not null && periods.FirstOrDefault(p => p.SubscriptionContractId == source.SubscriptionContractId) is { } sourcePeriod)
            lifecycle.History(sourcePeriod, "PracticeAllocationReleased", sourcePeriod.Status, actor, command.Reason, "allocation-source:" + command.IdempotencyKey.Trim(),
                metadata: new { command.PracticeId, AllocationId = source.Id, command.TargetSubscriptionContractId });
        if (command.TargetSubscriptionContractId is { } targetContract && periods.SingleOrDefault(p => p.SubscriptionContractId == targetContract && p.IsValidAt(clock.UtcNow)) is { } targetPeriod)
            lifecycle.History(targetPeriod, "PracticeAllocated", targetPeriod.Status, actor, command.Reason, "allocation-target:" + command.IdempotencyKey.Trim(),
                metadata: new { command.PracticeId, SourceContractId = source?.SubscriptionContractId, command.ContinuedReceptionIds });
        store.Add(new SubscriptionMutationReceipt(command.IdempotencyKey.Trim(), actor, fingerprint, command.PracticeId, clock.UtcNow));
        await projections.QueueAsync(new("commercial-allocation:" + command.IdempotencyKey.Trim(), command.PracticeId, doctorId.Value, true, true), ct);
        await store.SaveChangesAsync(ct);
        return Result<PracticeAllocationDetails>.Ok(await DetailsAsync(command.PracticeId, doctorId.Value, ct));
    }
}
