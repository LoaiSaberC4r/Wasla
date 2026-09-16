using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Features.Doctors;
using Wasla.Application.Features.PublicDiscovery;
using Wasla.Application.Features.Reservations;
using Wasla.Application.Persistence;
using Wasla.Domain.Practices;

namespace Wasla.Application.Features.Practices;

public sealed record DoctorPracticeSchedulePeriodResponse(
    Guid Id,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int SlotDurationMinutes,
    string RowVersion);

public sealed record DoctorPracticeScheduleExceptionResponse(
    Guid Id,
    DateOnly Date,
    DoctorPracticeScheduleExceptionType Type,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    int? SlotDurationMinutes,
    string RowVersion);

public sealed record DoctorPracticeScheduleResponse(
    IReadOnlyList<DoctorPracticeSchedulePeriodResponse> Periods,
    IReadOnlyList<DoctorPracticeScheduleExceptionResponse> Exceptions);

public sealed record EffectivePracticePeriodResponse(
    TimeOnly StartTime,
    TimeOnly EndTime,
    int SlotDurationMinutes,
    IReadOnlyList<TimeOnly> SlotStarts);

public sealed record ListDoctorPracticeScheduleQuery(Guid PracticeId) : IQuery<DoctorPracticeScheduleResponse>;
public sealed record GetEffectiveDoctorPracticeScheduleQuery(Guid PracticeId, DateOnly Date)
    : IQuery<IReadOnlyList<EffectivePracticePeriodResponse>>;
public sealed record CreateDoctorPracticeSchedulePeriodCommand(
    Guid PracticeId,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int SlotDurationMinutes)
    : ICommand<DoctorPracticeSchedulePeriodResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record UpdateDoctorPracticeSchedulePeriodCommand(
    Guid PracticeId,
    Guid PeriodId,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int SlotDurationMinutes,
    string RowVersion)
    : ICommand<DoctorPracticeSchedulePeriodResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record DeleteDoctorPracticeSchedulePeriodCommand(Guid PracticeId, Guid PeriodId, string RowVersion)
    : ICommand, ITransactionalCommand<WaslaWritePersistence>;
public sealed record CreateDoctorPracticeScheduleExceptionCommand(
    Guid PracticeId,
    DateOnly Date,
    DoctorPracticeScheduleExceptionType Type,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    int? SlotDurationMinutes)
    : ICommand<DoctorPracticeScheduleExceptionResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record UpdateDoctorPracticeScheduleExceptionCommand(
    Guid PracticeId,
    Guid ExceptionId,
    DateOnly Date,
    DoctorPracticeScheduleExceptionType Type,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    int? SlotDurationMinutes,
    string RowVersion)
    : ICommand<DoctorPracticeScheduleExceptionResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record DeleteDoctorPracticeScheduleExceptionCommand(
    Guid PracticeId,
    Guid ExceptionId,
    string RowVersion)
    : ICommand, ITransactionalCommand<WaslaWritePersistence>;

internal sealed class CreateDoctorPracticeSchedulePeriodCommandValidator
    : AbstractValidator<CreateDoctorPracticeSchedulePeriodCommand>
{
    public CreateDoctorPracticeSchedulePeriodCommandValidator()
    {
        AddRules(this);
    }

    internal static void AddRules(AbstractValidator<CreateDoctorPracticeSchedulePeriodCommand> validator)
    {
        validator.RuleFor(command => command.PracticeId).NotEmpty();
        validator.RuleFor(command => command.DayOfWeek).IsInEnum();
        validator.RuleFor(command => command.EndTime).GreaterThan(command => command.StartTime);
        validator.RuleFor(command => command.SlotDurationMinutes).InclusiveBetween(5, 480);
    }
}

internal sealed class UpdateDoctorPracticeSchedulePeriodCommandValidator
    : AbstractValidator<UpdateDoctorPracticeSchedulePeriodCommand>
{
    public UpdateDoctorPracticeSchedulePeriodCommandValidator()
    {
        RuleFor(command => command.PracticeId).NotEmpty();
        RuleFor(command => command.PeriodId).NotEmpty();
        RuleFor(command => command.DayOfWeek).IsInEnum();
        RuleFor(command => command.EndTime).GreaterThan(command => command.StartTime);
        RuleFor(command => command.SlotDurationMinutes).InclusiveBetween(5, 480);
        RuleFor(command => command.RowVersion).Must(RowVersionCodec.IsValid);
    }
}

internal sealed class DeleteDoctorPracticeSchedulePeriodCommandValidator
    : AbstractValidator<DeleteDoctorPracticeSchedulePeriodCommand>
{
    public DeleteDoctorPracticeSchedulePeriodCommandValidator()
    {
        RuleFor(command => command.PracticeId).NotEmpty();
        RuleFor(command => command.PeriodId).NotEmpty();
        RuleFor(command => command.RowVersion).Must(RowVersionCodec.IsValid);
    }
}

internal sealed class CreateDoctorPracticeScheduleExceptionCommandValidator
    : AbstractValidator<CreateDoctorPracticeScheduleExceptionCommand>
{
    public CreateDoctorPracticeScheduleExceptionCommandValidator()
    {
        RuleFor(command => command.PracticeId).NotEmpty();
        RuleFor(command => command.Date).NotEmpty();
        RuleFor(command => command.Type).IsInEnum();
    }
}

internal sealed class UpdateDoctorPracticeScheduleExceptionCommandValidator
    : AbstractValidator<UpdateDoctorPracticeScheduleExceptionCommand>
{
    public UpdateDoctorPracticeScheduleExceptionCommandValidator()
    {
        RuleFor(command => command.PracticeId).NotEmpty();
        RuleFor(command => command.ExceptionId).NotEmpty();
        RuleFor(command => command.Date).NotEmpty();
        RuleFor(command => command.Type).IsInEnum();
        RuleFor(command => command.RowVersion).Must(RowVersionCodec.IsValid);
    }
}

internal sealed class DeleteDoctorPracticeScheduleExceptionCommandValidator
    : AbstractValidator<DeleteDoctorPracticeScheduleExceptionCommand>
{
    public DeleteDoctorPracticeScheduleExceptionCommandValidator()
    {
        RuleFor(command => command.PracticeId).NotEmpty();
        RuleFor(command => command.ExceptionId).NotEmpty();
        RuleFor(command => command.RowVersion).Must(RowVersionCodec.IsValid);
    }
}

internal sealed class ListDoctorPracticeScheduleQueryHandler(IWaslaDataStore dataStore, ICurrentUser currentUser)
    : IQueryHandler<ListDoctorPracticeScheduleQuery, DoctorPracticeScheduleResponse>
{
    public async Task<Result<DoctorPracticeScheduleResponse>> Handle(
        ListDoctorPracticeScheduleQuery request,
        CancellationToken cancellationToken)
    {
        var access = await DoctorPracticeAccess.LoadOwnedPracticeAsync(
            dataStore, currentUser, request.PracticeId, cancellationToken);
        if (access.IsFailure)
        {
            return Result<DoctorPracticeScheduleResponse>.Fail(access.Errors);
        }

        var periods = await dataStore.ListDoctorPracticeSchedulePeriodsAsync(request.PracticeId, cancellationToken);
        var exceptions = await dataStore.ListDoctorPracticeScheduleExceptionsAsync(request.PracticeId, cancellationToken);
        return Result<DoctorPracticeScheduleResponse>.Ok(new DoctorPracticeScheduleResponse(
            periods.Select(DoctorPracticeScheduleMapper.Map).ToArray(),
            exceptions.Select(DoctorPracticeScheduleMapper.Map).ToArray()));
    }
}

internal sealed class GetEffectiveDoctorPracticeScheduleQueryHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser)
    : IQueryHandler<GetEffectiveDoctorPracticeScheduleQuery, IReadOnlyList<EffectivePracticePeriodResponse>>
{
    public async Task<Result<IReadOnlyList<EffectivePracticePeriodResponse>>> Handle(
        GetEffectiveDoctorPracticeScheduleQuery request,
        CancellationToken cancellationToken)
    {
        var access = await DoctorPracticeAccess.LoadOwnedPracticeAsync(
            dataStore, currentUser, request.PracticeId, cancellationToken);
        if (access.IsFailure)
        {
            return Result<IReadOnlyList<EffectivePracticePeriodResponse>>.Fail(access.Errors);
        }

        var periods = await dataStore.ListDoctorPracticeSchedulePeriodsAsync(request.PracticeId, cancellationToken);
        var exceptions = await dataStore.ListDoctorPracticeScheduleExceptionsAsync(request.PracticeId, cancellationToken);
        var effective = DoctorPracticeAvailabilityCalculator.GetEffectiveWorkingPeriods(
            request.Date, periods, exceptions);
        return Result<IReadOnlyList<EffectivePracticePeriodResponse>>.Ok(effective.Select(period =>
            new EffectivePracticePeriodResponse(
                period.StartTime,
                period.EndTime,
                period.SlotDurationMinutes,
                DoctorPracticeAvailabilityCalculator.CalculateSlotStarts(period))).ToArray());
    }
}

internal sealed class CreateDoctorPracticeSchedulePeriodCommandHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser,
    IPublicDiscoveryRankingProjectionRefresher projectionRefresher)
    : ICommandHandler<CreateDoctorPracticeSchedulePeriodCommand, DoctorPracticeSchedulePeriodResponse>
{
    public async Task<Result<DoctorPracticeSchedulePeriodResponse>> Handle(
        CreateDoctorPracticeSchedulePeriodCommand request,
        CancellationToken cancellationToken)
    {
        var access = await DoctorPracticeAccess.LoadOwnedPracticeAsync(
            dataStore, currentUser, request.PracticeId, cancellationToken);
        if (access.IsFailure)
        {
            return Result<DoctorPracticeSchedulePeriodResponse>.Fail(access.Errors);
        }

        var overlap = await EnsureNoOverlapAsync(
            dataStore,
            access.Value.Doctor.Id,
            request.PracticeId,
            null,
            request.DayOfWeek,
            request.StartTime,
            request.EndTime,
            cancellationToken);
        if (overlap.IsFailure)
        {
            return Result<DoctorPracticeSchedulePeriodResponse>.Fail(overlap.Errors);
        }

        var period = DoctorPracticeSchedulePeriod.Create(
            Guid.NewGuid(),
            request.PracticeId,
            request.DayOfWeek,
            request.StartTime,
            request.EndTime,
            request.SlotDurationMinutes,
            access.Value.ActorId);
        if (period.IsFailure)
        {
            return Result<DoctorPracticeSchedulePeriodResponse>.Fail(period.Errors);
        }

        dataStore.Add(period.Value);
        await dataStore.SaveChangesAsync(cancellationToken);
        await projectionRefresher.RefreshPracticesAsync([request.PracticeId], cancellationToken);
        return Result<DoctorPracticeSchedulePeriodResponse>.Ok(DoctorPracticeScheduleMapper.Map(period.Value));
    }

    internal static async Task<Result> EnsureNoOverlapAsync(
        IWaslaDataStore dataStore,
        Guid doctorId,
        Guid practiceId,
        Guid? excludingPeriodId,
        DayOfWeek dayOfWeek,
        TimeOnly startTime,
        TimeOnly endTime,
        CancellationToken cancellationToken)
    {
        await dataStore.AcquireDoctorScheduleLockAsync(doctorId, cancellationToken);
        var practicePeriods = await dataStore.ListDoctorPracticeSchedulePeriodsAsync(practiceId, cancellationToken);
        if (practicePeriods.Any(period => period.Id != excludingPeriodId && period.DayOfWeek == dayOfWeek &&
                                          period.Overlaps(startTime, endTime)))
        {
            return Result.Fail(DoctorPracticeScheduleErrors.PeriodOverlap);
        }

        return await dataStore.HasDoctorScheduleOverlapAsync(
            doctorId,
            practiceId,
            excludingPeriodId,
            dayOfWeek,
            startTime,
            endTime,
            cancellationToken)
            ? Result.Fail(DoctorPracticeScheduleErrors.DoctorCrossPracticeOverlap)
            : Result.Ok();
    }
}

internal sealed class UpdateDoctorPracticeSchedulePeriodCommandHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser,
    IPublicDiscoveryRankingProjectionRefresher projectionRefresher,
    ReservationScheduleGuard reservationGuard)
    : ICommandHandler<UpdateDoctorPracticeSchedulePeriodCommand, DoctorPracticeSchedulePeriodResponse>
{
    public async Task<Result<DoctorPracticeSchedulePeriodResponse>> Handle(
        UpdateDoctorPracticeSchedulePeriodCommand request,
        CancellationToken cancellationToken)
    {
        var access = await DoctorPracticeAccess.LoadOwnedPracticeAsync(
            dataStore, currentUser, request.PracticeId, cancellationToken);
        if (access.IsFailure)
        {
            return Result<DoctorPracticeSchedulePeriodResponse>.Fail(access.Errors);
        }

        var period = await dataStore.FindDoctorPracticeSchedulePeriodAsync(request.PeriodId, cancellationToken);
        if (period is null || period.DoctorPracticeId != request.PracticeId)
        {
            return Result<DoctorPracticeSchedulePeriodResponse>.Fail(DoctorPracticeScheduleErrors.NotFound);
        }

        var supplied = DoctorPracticeAccess.VerifyRowVersion(
            period.RowVersion, request.RowVersion, DoctorPracticeScheduleErrors.ConcurrencyConflict);
        if (supplied.IsFailure)
        {
            return Result<DoctorPracticeSchedulePeriodResponse>.Fail(supplied.Errors);
        }

        var overlap = await CreateDoctorPracticeSchedulePeriodCommandHandler.EnsureNoOverlapAsync(
            dataStore,
            access.Value.Doctor.Id,
            request.PracticeId,
            request.PeriodId,
            request.DayOfWeek,
            request.StartTime,
            request.EndTime,
            cancellationToken);
        if (overlap.IsFailure)
        {
            return Result<DoctorPracticeSchedulePeriodResponse>.Fail(overlap.Errors);
        }

        var updated = period.Update(
            request.DayOfWeek,
            request.StartTime,
            request.EndTime,
            request.SlotDurationMinutes,
            access.Value.ActorId);
        if (updated.IsFailure)
        {
            return Result<DoctorPracticeSchedulePeriodResponse>.Fail(updated.Errors);
        }

        var finalPeriods = (await dataStore.ListDoctorPracticeSchedulePeriodsAsync(
                request.PracticeId, cancellationToken))
            .Where(item => item.Id != period.Id)
            .Append(period)
            .ToArray();
        var finalExceptions = await dataStore.ListDoctorPracticeScheduleExceptionsAsync(
            request.PracticeId, cancellationToken);
        var reservationConflicts = await reservationGuard.EnsureFutureReservationsRemainValidAsync(
            request.PracticeId, finalPeriods, finalExceptions, cancellationToken);
        if (reservationConflicts.IsFailure)
        {
            return Result<DoctorPracticeSchedulePeriodResponse>.Fail(reservationConflicts.Errors);
        }

        dataStore.SetOriginalRowVersion(period, supplied.Value);
        await dataStore.SaveChangesAsync(cancellationToken);
        await projectionRefresher.RefreshPracticesAsync([request.PracticeId], cancellationToken);
        return Result<DoctorPracticeSchedulePeriodResponse>.Ok(DoctorPracticeScheduleMapper.Map(period));
    }
}

internal sealed class DeleteDoctorPracticeSchedulePeriodCommandHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser,
    IPublicDiscoveryRankingProjectionRefresher projectionRefresher,
    ReservationScheduleGuard reservationGuard)
    : ICommandHandler<DeleteDoctorPracticeSchedulePeriodCommand>
{
    public async Task<Result> Handle(
        DeleteDoctorPracticeSchedulePeriodCommand request,
        CancellationToken cancellationToken)
    {
        var access = await DoctorPracticeAccess.LoadOwnedPracticeAsync(
            dataStore, currentUser, request.PracticeId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Fail(access.Errors);
        }

        var period = await dataStore.FindDoctorPracticeSchedulePeriodAsync(request.PeriodId, cancellationToken);
        if (period is null || period.DoctorPracticeId != request.PracticeId)
        {
            return Result.Fail(DoctorPracticeScheduleErrors.NotFound);
        }

        var supplied = DoctorPracticeAccess.VerifyRowVersion(
            period.RowVersion, request.RowVersion, DoctorPracticeScheduleErrors.ConcurrencyConflict);
        if (supplied.IsFailure)
        {
            return Result.Fail(supplied.Errors);
        }

        var finalPeriods = (await dataStore.ListDoctorPracticeSchedulePeriodsAsync(
                request.PracticeId, cancellationToken))
            .Where(item => item.Id != period.Id)
            .ToArray();
        var finalExceptions = await dataStore.ListDoctorPracticeScheduleExceptionsAsync(
            request.PracticeId, cancellationToken);
        var reservationConflicts = await reservationGuard.EnsureFutureReservationsRemainValidAsync(
            request.PracticeId, finalPeriods, finalExceptions, cancellationToken);
        if (reservationConflicts.IsFailure)
        {
            return Result.Fail(reservationConflicts.Errors);
        }

        dataStore.SetOriginalRowVersion(period, supplied.Value);
        dataStore.Remove(period);
        await dataStore.SaveChangesAsync(cancellationToken);
        await projectionRefresher.RefreshPracticesAsync([request.PracticeId], cancellationToken);
        return Result.Ok();
    }
}

internal sealed class CreateDoctorPracticeScheduleExceptionCommandHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser,
    IPublicDiscoveryRankingProjectionRefresher projectionRefresher,
    ReservationScheduleGuard reservationGuard)
    : ICommandHandler<CreateDoctorPracticeScheduleExceptionCommand, DoctorPracticeScheduleExceptionResponse>
{
    public async Task<Result<DoctorPracticeScheduleExceptionResponse>> Handle(
        CreateDoctorPracticeScheduleExceptionCommand request,
        CancellationToken cancellationToken)
    {
        var access = await DoctorPracticeAccess.LoadOwnedPracticeAsync(
            dataStore, currentUser, request.PracticeId, cancellationToken);
        if (access.IsFailure)
        {
            return Result<DoctorPracticeScheduleExceptionResponse>.Fail(access.Errors);
        }

        await dataStore.AcquireDoctorScheduleLockAsync(access.Value.Doctor.Id, cancellationToken);
        var exception = DoctorPracticeScheduleException.Create(
            Guid.NewGuid(), request.PracticeId, request.Date, request.Type,
            request.StartTime, request.EndTime, request.SlotDurationMinutes, access.Value.ActorId);
        if (exception.IsFailure)
        {
            return Result<DoctorPracticeScheduleExceptionResponse>.Fail(exception.Errors);
        }

        var conflicts = await HasExceptionConflictAsync(dataStore, request.PracticeId, null, request.Date,
            request.Type, request.StartTime, request.EndTime, cancellationToken);
        if (conflicts)
        {
            return Result<DoctorPracticeScheduleExceptionResponse>.Fail(DoctorPracticeScheduleErrors.PeriodOverlap);
        }

        var crossPractice = await EnsureNoCrossPracticeOverlapAsync(
            dataStore, access.Value.Doctor.Id, request.PracticeId, exception.Value, null, cancellationToken);
        if (crossPractice.IsFailure)
        {
            return Result<DoctorPracticeScheduleExceptionResponse>.Fail(crossPractice.Errors);
        }

        var finalPeriods = await dataStore.ListDoctorPracticeSchedulePeriodsAsync(
            request.PracticeId, cancellationToken);
        var finalExceptions = (await dataStore.ListDoctorPracticeScheduleExceptionsAsync(
                request.PracticeId, cancellationToken))
            .Append(exception.Value)
            .ToArray();
        var reservationConflicts = await reservationGuard.EnsureFutureReservationsRemainValidAsync(
            request.PracticeId, finalPeriods, finalExceptions, cancellationToken);
        if (reservationConflicts.IsFailure)
        {
            return Result<DoctorPracticeScheduleExceptionResponse>.Fail(reservationConflicts.Errors);
        }

        dataStore.Add(exception.Value);
        await dataStore.SaveChangesAsync(cancellationToken);
        await projectionRefresher.RefreshPracticesAsync([request.PracticeId], cancellationToken);
        return Result<DoctorPracticeScheduleExceptionResponse>.Ok(DoctorPracticeScheduleMapper.Map(exception.Value));
    }

    internal static async Task<bool> HasExceptionConflictAsync(
        IWaslaDataStore dataStore,
        Guid practiceId,
        Guid? excludingId,
        DateOnly date,
        DoctorPracticeScheduleExceptionType type,
        TimeOnly? startTime,
        TimeOnly? endTime,
        CancellationToken cancellationToken)
    {
        var existing = (await dataStore.ListDoctorPracticeScheduleExceptionsAsync(practiceId, cancellationToken))
            .Where(item => item.Id != excludingId && item.Date == date)
            .ToArray();
        var isClosure = type is DoctorPracticeScheduleExceptionType.DayOff or
            DoctorPracticeScheduleExceptionType.Vacation;
        if (isClosure)
        {
            return existing.Length > 0;
        }

        if (existing.Any(item => item.Type is DoctorPracticeScheduleExceptionType.DayOff or
                DoctorPracticeScheduleExceptionType.Vacation))
        {
            return true;
        }

        return existing.Where(item => item.Type == type).Any(item => item.StartTime.HasValue && item.EndTime.HasValue &&
                                    startTime < item.EndTime && item.StartTime < endTime);
    }

    internal static async Task<Result> EnsureNoCrossPracticeOverlapAsync(
        IWaslaDataStore dataStore,
        Guid doctorId,
        Guid practiceId,
        DoctorPracticeScheduleException candidate,
        Guid? excludingId,
        CancellationToken cancellationToken)
    {
        if (candidate.Type != DoctorPracticeScheduleExceptionType.CustomWorkingHours)
        {
            return Result.Ok();
        }

        var targetRecurring = await dataStore.ListDoctorPracticeSchedulePeriodsAsync(practiceId, cancellationToken);
        var targetExceptions = (await dataStore.ListDoctorPracticeScheduleExceptionsAsync(
                practiceId, cancellationToken))
            .Where(item => item.Id != excludingId)
            .Append(candidate)
            .ToArray();
        var targetEffective = DoctorPracticeAvailabilityCalculator.GetEffectiveWorkingPeriods(
            candidate.Date, targetRecurring, targetExceptions);

        var practices = await dataStore.ListDoctorPracticesAsync(doctorId, cancellationToken);
        foreach (var otherPractice in practices.Where(item => item.Practice.Id != practiceId))
        {
            var otherRecurring = await dataStore.ListDoctorPracticeSchedulePeriodsAsync(
                otherPractice.Practice.Id, cancellationToken);
            var otherExceptions = await dataStore.ListDoctorPracticeScheduleExceptionsAsync(
                otherPractice.Practice.Id, cancellationToken);
            var otherEffective = DoctorPracticeAvailabilityCalculator.GetEffectiveWorkingPeriods(
                candidate.Date, otherRecurring, otherExceptions);
            if (targetEffective.Any(target => otherEffective.Any(other =>
                    target.StartTime < other.EndTime && other.StartTime < target.EndTime)))
            {
                return Result.Fail(DoctorPracticeScheduleErrors.DoctorCrossPracticeOverlap);
            }
        }

        return Result.Ok();
    }
}

internal sealed class UpdateDoctorPracticeScheduleExceptionCommandHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser,
    IPublicDiscoveryRankingProjectionRefresher projectionRefresher,
    ReservationScheduleGuard reservationGuard)
    : ICommandHandler<UpdateDoctorPracticeScheduleExceptionCommand, DoctorPracticeScheduleExceptionResponse>
{
    public async Task<Result<DoctorPracticeScheduleExceptionResponse>> Handle(
        UpdateDoctorPracticeScheduleExceptionCommand request,
        CancellationToken cancellationToken)
    {
        var access = await DoctorPracticeAccess.LoadOwnedPracticeAsync(
            dataStore, currentUser, request.PracticeId, cancellationToken);
        if (access.IsFailure)
        {
            return Result<DoctorPracticeScheduleExceptionResponse>.Fail(access.Errors);
        }

        var exception = await dataStore.FindDoctorPracticeScheduleExceptionAsync(
            request.ExceptionId, cancellationToken);
        if (exception is null || exception.DoctorPracticeId != request.PracticeId)
        {
            return Result<DoctorPracticeScheduleExceptionResponse>.Fail(DoctorPracticeScheduleErrors.NotFound);
        }

        var supplied = DoctorPracticeAccess.VerifyRowVersion(
            exception.RowVersion, request.RowVersion, DoctorPracticeScheduleErrors.ConcurrencyConflict);
        if (supplied.IsFailure)
        {
            return Result<DoctorPracticeScheduleExceptionResponse>.Fail(supplied.Errors);
        }

        await dataStore.AcquireDoctorScheduleLockAsync(access.Value.Doctor.Id, cancellationToken);
        var candidate = DoctorPracticeScheduleException.Create(
            Guid.NewGuid(), request.PracticeId, request.Date, request.Type,
            request.StartTime, request.EndTime, request.SlotDurationMinutes, access.Value.ActorId);
        if (candidate.IsFailure)
        {
            return Result<DoctorPracticeScheduleExceptionResponse>.Fail(candidate.Errors);
        }

        var conflicts = await CreateDoctorPracticeScheduleExceptionCommandHandler.HasExceptionConflictAsync(
            dataStore, request.PracticeId, request.ExceptionId, request.Date, request.Type,
            request.StartTime, request.EndTime, cancellationToken);
        if (conflicts)
        {
            return Result<DoctorPracticeScheduleExceptionResponse>.Fail(DoctorPracticeScheduleErrors.PeriodOverlap);
        }

        var crossPractice = await CreateDoctorPracticeScheduleExceptionCommandHandler
            .EnsureNoCrossPracticeOverlapAsync(
                dataStore,
                access.Value.Doctor.Id,
                request.PracticeId,
                candidate.Value,
                request.ExceptionId,
                cancellationToken);
        if (crossPractice.IsFailure)
        {
            return Result<DoctorPracticeScheduleExceptionResponse>.Fail(crossPractice.Errors);
        }

        var updated = exception.Update(
            request.Date, request.Type, request.StartTime, request.EndTime,
            request.SlotDurationMinutes, access.Value.ActorId);
        if (updated.IsFailure)
        {
            return Result<DoctorPracticeScheduleExceptionResponse>.Fail(updated.Errors);
        }

        var finalPeriods = await dataStore.ListDoctorPracticeSchedulePeriodsAsync(
            request.PracticeId, cancellationToken);
        var finalExceptions = (await dataStore.ListDoctorPracticeScheduleExceptionsAsync(
                request.PracticeId, cancellationToken))
            .Where(item => item.Id != exception.Id)
            .Append(exception)
            .ToArray();
        var reservationConflicts = await reservationGuard.EnsureFutureReservationsRemainValidAsync(
            request.PracticeId, finalPeriods, finalExceptions, cancellationToken);
        if (reservationConflicts.IsFailure)
        {
            return Result<DoctorPracticeScheduleExceptionResponse>.Fail(reservationConflicts.Errors);
        }

        dataStore.SetOriginalRowVersion(exception, supplied.Value);
        await dataStore.SaveChangesAsync(cancellationToken);
        await projectionRefresher.RefreshPracticesAsync([request.PracticeId], cancellationToken);
        return Result<DoctorPracticeScheduleExceptionResponse>.Ok(DoctorPracticeScheduleMapper.Map(exception));
    }
}

internal sealed class DeleteDoctorPracticeScheduleExceptionCommandHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser,
    IPublicDiscoveryRankingProjectionRefresher projectionRefresher,
    ReservationScheduleGuard reservationGuard)
    : ICommandHandler<DeleteDoctorPracticeScheduleExceptionCommand>
{
    public async Task<Result> Handle(
        DeleteDoctorPracticeScheduleExceptionCommand request,
        CancellationToken cancellationToken)
    {
        var access = await DoctorPracticeAccess.LoadOwnedPracticeAsync(
            dataStore, currentUser, request.PracticeId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Fail(access.Errors);
        }

        var exception = await dataStore.FindDoctorPracticeScheduleExceptionAsync(
            request.ExceptionId, cancellationToken);
        if (exception is null || exception.DoctorPracticeId != request.PracticeId)
        {
            return Result.Fail(DoctorPracticeScheduleErrors.NotFound);
        }

        var supplied = DoctorPracticeAccess.VerifyRowVersion(
            exception.RowVersion, request.RowVersion, DoctorPracticeScheduleErrors.ConcurrencyConflict);
        if (supplied.IsFailure)
        {
            return Result.Fail(supplied.Errors);
        }

        var finalPeriods = await dataStore.ListDoctorPracticeSchedulePeriodsAsync(
            request.PracticeId, cancellationToken);
        var finalExceptions = (await dataStore.ListDoctorPracticeScheduleExceptionsAsync(
                request.PracticeId, cancellationToken))
            .Where(item => item.Id != exception.Id)
            .ToArray();
        var reservationConflicts = await reservationGuard.EnsureFutureReservationsRemainValidAsync(
            request.PracticeId, finalPeriods, finalExceptions, cancellationToken);
        if (reservationConflicts.IsFailure)
        {
            return Result.Fail(reservationConflicts.Errors);
        }

        dataStore.SetOriginalRowVersion(exception, supplied.Value);
        dataStore.Remove(exception);
        await dataStore.SaveChangesAsync(cancellationToken);
        await projectionRefresher.RefreshPracticesAsync([request.PracticeId], cancellationToken);
        return Result.Ok();
    }
}

internal static class DoctorPracticeScheduleMapper
{
    public static DoctorPracticeSchedulePeriodResponse Map(DoctorPracticeSchedulePeriod item)
        => new(item.Id, item.DayOfWeek, item.StartTime, item.EndTime, item.SlotDurationMinutes,
            RowVersionCodec.Encode(item.RowVersion));

    public static DoctorPracticeScheduleExceptionResponse Map(DoctorPracticeScheduleException item)
        => new(item.Id, item.Date, item.Type, item.StartTime, item.EndTime, item.SlotDurationMinutes,
            RowVersionCodec.Encode(item.RowVersion));
}
