using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Features.Doctors;
using Wasla.Application.Persistence;
using Wasla.Domain.Practices;

namespace Wasla.Application.Features.Practices;

public sealed record DoctorPracticeSegmentResponse(
    Guid Id,
    string NameAr,
    string? NameEn,
    int Priority,
    int? ReservedDailyQuota,
    int? QuotaReleaseBeforeMinutes,
    bool IsDefault,
    bool IsActive,
    string RowVersion);

public sealed record DoctorPracticeVisitTypeResponse(
    Guid Id,
    DoctorPracticeVisitTypeCode Type,
    string NameAr,
    string? NameEn,
    int DurationMinutes,
    bool IsActive,
    string RowVersion);

public sealed record DoctorPracticePriceResponse(
    Guid Id,
    Guid SegmentId,
    Guid VisitTypeId,
    decimal Price,
    string RowVersion);

public sealed record ListDoctorPracticeSegmentsQuery(Guid PracticeId)
    : IQuery<IReadOnlyList<DoctorPracticeSegmentResponse>>;
public sealed record CreateDoctorPracticeSegmentCommand(
    Guid PracticeId,
    string NameAr,
    string? NameEn,
    int Priority,
    int? ReservedDailyQuota,
    int? QuotaReleaseBeforeMinutes)
    : ICommand<DoctorPracticeSegmentResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record UpdateDoctorPracticeSegmentCommand(
    Guid PracticeId,
    Guid SegmentId,
    string NameAr,
    string? NameEn,
    int Priority,
    int? ReservedDailyQuota,
    int? QuotaReleaseBeforeMinutes,
    bool IsActive,
    string RowVersion)
    : ICommand<DoctorPracticeSegmentResponse>, ITransactionalCommand<WaslaWritePersistence>;

public sealed record ListDoctorPracticeVisitTypesQuery(Guid PracticeId)
    : IQuery<IReadOnlyList<DoctorPracticeVisitTypeResponse>>;
public sealed record UpdateDoctorPracticeVisitTypeCommand(
    Guid PracticeId,
    Guid VisitTypeId,
    string NameAr,
    string? NameEn,
    int DurationMinutes,
    bool IsActive,
    string RowVersion)
    : ICommand<DoctorPracticeVisitTypeResponse>, ITransactionalCommand<WaslaWritePersistence>;

public sealed record ListDoctorPracticePricesQuery(Guid PracticeId)
    : IQuery<IReadOnlyList<DoctorPracticePriceResponse>>;
public sealed record CreateDoctorPracticePriceCommand(
    Guid PracticeId,
    Guid SegmentId,
    Guid VisitTypeId,
    decimal Price)
    : ICommand<DoctorPracticePriceResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record UpdateDoctorPracticePriceCommand(
    Guid PracticeId,
    Guid PriceId,
    decimal Price,
    string RowVersion)
    : ICommand<DoctorPracticePriceResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record DeleteDoctorPracticePriceCommand(Guid PracticeId, Guid PriceId, string RowVersion)
    : ICommand, ITransactionalCommand<WaslaWritePersistence>;

internal sealed class CreateDoctorPracticeSegmentCommandValidator
    : AbstractValidator<CreateDoctorPracticeSegmentCommand>
{
    public CreateDoctorPracticeSegmentCommandValidator()
    {
        RuleFor(command => command.PracticeId).NotEmpty();
        RuleFor(command => command.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(command => command.NameEn).MaximumLength(200);
        RuleFor(command => command.Priority).InclusiveBetween(-1000, 1000);
        RuleFor(command => command.ReservedDailyQuota).InclusiveBetween(1, 10000)
            .When(command => command.ReservedDailyQuota.HasValue);
        RuleFor(command => command.QuotaReleaseBeforeMinutes).InclusiveBetween(0, 10080)
            .When(command => command.QuotaReleaseBeforeMinutes.HasValue);
        RuleFor(command => command.QuotaReleaseBeforeMinutes).Null()
            .When(command => !command.ReservedDailyQuota.HasValue);
        RuleFor(command => command.QuotaReleaseBeforeMinutes).NotNull()
            .When(command => command.ReservedDailyQuota.HasValue);
    }
}

internal sealed class UpdateDoctorPracticeSegmentCommandValidator
    : AbstractValidator<UpdateDoctorPracticeSegmentCommand>
{
    public UpdateDoctorPracticeSegmentCommandValidator()
    {
        RuleFor(command => command.PracticeId).NotEmpty();
        RuleFor(command => command.SegmentId).NotEmpty();
        RuleFor(command => command.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(command => command.NameEn).MaximumLength(200);
        RuleFor(command => command.Priority).InclusiveBetween(-1000, 1000);
        RuleFor(command => command.ReservedDailyQuota).InclusiveBetween(1, 10000)
            .When(command => command.ReservedDailyQuota.HasValue);
        RuleFor(command => command.QuotaReleaseBeforeMinutes).InclusiveBetween(0, 10080)
            .When(command => command.QuotaReleaseBeforeMinutes.HasValue);
        RuleFor(command => command.QuotaReleaseBeforeMinutes).Null()
            .When(command => !command.ReservedDailyQuota.HasValue);
        RuleFor(command => command.QuotaReleaseBeforeMinutes).NotNull()
            .When(command => command.ReservedDailyQuota.HasValue);
        RuleFor(command => command.RowVersion).Must(RowVersionCodec.IsValid);
    }
}

internal sealed class UpdateDoctorPracticeVisitTypeCommandValidator
    : AbstractValidator<UpdateDoctorPracticeVisitTypeCommand>
{
    public UpdateDoctorPracticeVisitTypeCommandValidator()
    {
        RuleFor(command => command.PracticeId).NotEmpty();
        RuleFor(command => command.VisitTypeId).NotEmpty();
        RuleFor(command => command.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(command => command.NameEn).MaximumLength(200);
        RuleFor(command => command.DurationMinutes).InclusiveBetween(5, 480);
        RuleFor(command => command.RowVersion).Must(RowVersionCodec.IsValid);
    }
}

internal sealed class CreateDoctorPracticePriceCommandValidator
    : AbstractValidator<CreateDoctorPracticePriceCommand>
{
    public CreateDoctorPracticePriceCommandValidator()
    {
        RuleFor(command => command.PracticeId).NotEmpty();
        RuleFor(command => command.SegmentId).NotEmpty();
        RuleFor(command => command.VisitTypeId).NotEmpty();
        RuleFor(command => command.Price).GreaterThan(0).LessThanOrEqualTo(10000000);
    }
}

internal sealed class UpdateDoctorPracticePriceCommandValidator
    : AbstractValidator<UpdateDoctorPracticePriceCommand>
{
    public UpdateDoctorPracticePriceCommandValidator()
    {
        RuleFor(command => command.PracticeId).NotEmpty();
        RuleFor(command => command.PriceId).NotEmpty();
        RuleFor(command => command.Price).GreaterThan(0).LessThanOrEqualTo(10000000);
        RuleFor(command => command.RowVersion).Must(RowVersionCodec.IsValid);
    }
}

internal sealed class DeleteDoctorPracticePriceCommandValidator
    : AbstractValidator<DeleteDoctorPracticePriceCommand>
{
    public DeleteDoctorPracticePriceCommandValidator()
    {
        RuleFor(command => command.PracticeId).NotEmpty();
        RuleFor(command => command.PriceId).NotEmpty();
        RuleFor(command => command.RowVersion).Must(RowVersionCodec.IsValid);
    }
}

internal sealed class ListDoctorPracticeSegmentsQueryHandler(IWaslaDataStore dataStore, ICurrentUser currentUser)
    : IQueryHandler<ListDoctorPracticeSegmentsQuery, IReadOnlyList<DoctorPracticeSegmentResponse>>
{
    public async Task<Result<IReadOnlyList<DoctorPracticeSegmentResponse>>> Handle(
        ListDoctorPracticeSegmentsQuery request,
        CancellationToken cancellationToken)
    {
        var access = await DoctorPracticeAccess.LoadOwnedPracticeAsync(
            dataStore, currentUser, request.PracticeId, cancellationToken);
        if (access.IsFailure)
        {
            return Result<IReadOnlyList<DoctorPracticeSegmentResponse>>.Fail(access.Errors);
        }

        var items = await dataStore.ListDoctorPracticeSegmentsAsync(request.PracticeId, cancellationToken);
        return Result<IReadOnlyList<DoctorPracticeSegmentResponse>>.Ok(
            items.Select(DoctorPracticeCatalogMapper.Map).ToArray());
    }
}

internal sealed class CreateDoctorPracticeSegmentCommandHandler(IWaslaDataStore dataStore, ICurrentUser currentUser)
    : ICommandHandler<CreateDoctorPracticeSegmentCommand, DoctorPracticeSegmentResponse>
{
    public async Task<Result<DoctorPracticeSegmentResponse>> Handle(
        CreateDoctorPracticeSegmentCommand request,
        CancellationToken cancellationToken)
    {
        var access = await DoctorPracticeAccess.LoadOwnedPracticeAsync(
            dataStore, currentUser, request.PracticeId, cancellationToken);
        if (access.IsFailure)
        {
            return Result<DoctorPracticeSegmentResponse>.Fail(access.Errors);
        }

        var segment = DoctorPracticeSegment.Create(
            Guid.NewGuid(), request.PracticeId, request.NameAr, request.NameEn, request.Priority,
            request.ReservedDailyQuota, request.QuotaReleaseBeforeMinutes, actorId: access.Value.ActorId);
        if (segment.IsFailure)
        {
            return Result<DoctorPracticeSegmentResponse>.Fail(segment.Errors);
        }

        dataStore.Add(segment.Value);
        await dataStore.SaveChangesAsync(cancellationToken);
        return Result<DoctorPracticeSegmentResponse>.Ok(DoctorPracticeCatalogMapper.Map(segment.Value));
    }
}

internal sealed class UpdateDoctorPracticeSegmentCommandHandler(IWaslaDataStore dataStore, ICurrentUser currentUser)
    : ICommandHandler<UpdateDoctorPracticeSegmentCommand, DoctorPracticeSegmentResponse>
{
    public async Task<Result<DoctorPracticeSegmentResponse>> Handle(
        UpdateDoctorPracticeSegmentCommand request,
        CancellationToken cancellationToken)
    {
        var access = await DoctorPracticeAccess.LoadOwnedPracticeAsync(
            dataStore, currentUser, request.PracticeId, cancellationToken);
        if (access.IsFailure)
        {
            return Result<DoctorPracticeSegmentResponse>.Fail(access.Errors);
        }

        var segment = await dataStore.FindDoctorPracticeSegmentAsync(request.SegmentId, cancellationToken);
        if (segment is null || segment.DoctorPracticeId != request.PracticeId)
        {
            return Result<DoctorPracticeSegmentResponse>.Fail(DoctorPracticeSegmentErrors.NotFound);
        }

        var supplied = DoctorPracticeAccess.VerifyRowVersion(
            segment.RowVersion, request.RowVersion, DoctorPracticeSegmentErrors.ConcurrencyConflict);
        if (supplied.IsFailure)
        {
            return Result<DoctorPracticeSegmentResponse>.Fail(supplied.Errors);
        }

        var updated = segment.Update(
            request.NameAr, request.NameEn, request.Priority, request.ReservedDailyQuota,
            request.QuotaReleaseBeforeMinutes, request.IsActive, access.Value.ActorId);
        if (updated.IsFailure)
        {
            return Result<DoctorPracticeSegmentResponse>.Fail(updated.Errors);
        }

        dataStore.SetOriginalRowVersion(segment, supplied.Value);
        await dataStore.SaveChangesAsync(cancellationToken);
        return Result<DoctorPracticeSegmentResponse>.Ok(DoctorPracticeCatalogMapper.Map(segment));
    }
}

internal sealed class ListDoctorPracticeVisitTypesQueryHandler(IWaslaDataStore dataStore, ICurrentUser currentUser)
    : IQueryHandler<ListDoctorPracticeVisitTypesQuery, IReadOnlyList<DoctorPracticeVisitTypeResponse>>
{
    public async Task<Result<IReadOnlyList<DoctorPracticeVisitTypeResponse>>> Handle(
        ListDoctorPracticeVisitTypesQuery request,
        CancellationToken cancellationToken)
    {
        var access = await DoctorPracticeAccess.LoadOwnedPracticeAsync(
            dataStore, currentUser, request.PracticeId, cancellationToken);
        if (access.IsFailure)
        {
            return Result<IReadOnlyList<DoctorPracticeVisitTypeResponse>>.Fail(access.Errors);
        }

        var items = await dataStore.ListDoctorPracticeVisitTypesAsync(request.PracticeId, cancellationToken);
        return Result<IReadOnlyList<DoctorPracticeVisitTypeResponse>>.Ok(
            items.Select(DoctorPracticeCatalogMapper.Map).ToArray());
    }
}

internal sealed class UpdateDoctorPracticeVisitTypeCommandHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser)
    : ICommandHandler<UpdateDoctorPracticeVisitTypeCommand, DoctorPracticeVisitTypeResponse>
{
    public async Task<Result<DoctorPracticeVisitTypeResponse>> Handle(
        UpdateDoctorPracticeVisitTypeCommand request,
        CancellationToken cancellationToken)
    {
        var access = await DoctorPracticeAccess.LoadOwnedPracticeAsync(
            dataStore, currentUser, request.PracticeId, cancellationToken);
        if (access.IsFailure)
        {
            return Result<DoctorPracticeVisitTypeResponse>.Fail(access.Errors);
        }

        var visitType = await dataStore.FindDoctorPracticeVisitTypeAsync(request.VisitTypeId, cancellationToken);
        if (visitType is null || visitType.DoctorPracticeId != request.PracticeId)
        {
            return Result<DoctorPracticeVisitTypeResponse>.Fail(DoctorPracticeVisitTypeErrors.NotFound);
        }

        var supplied = DoctorPracticeAccess.VerifyRowVersion(
            visitType.RowVersion, request.RowVersion, DoctorPracticeVisitTypeErrors.ConcurrencyConflict);
        if (supplied.IsFailure)
        {
            return Result<DoctorPracticeVisitTypeResponse>.Fail(supplied.Errors);
        }

        var updated = visitType.Update(
            request.NameAr, request.NameEn, request.DurationMinutes, request.IsActive, access.Value.ActorId);
        if (updated.IsFailure)
        {
            return Result<DoctorPracticeVisitTypeResponse>.Fail(updated.Errors);
        }

        dataStore.SetOriginalRowVersion(visitType, supplied.Value);
        await dataStore.SaveChangesAsync(cancellationToken);
        return Result<DoctorPracticeVisitTypeResponse>.Ok(DoctorPracticeCatalogMapper.Map(visitType));
    }
}

internal sealed class ListDoctorPracticePricesQueryHandler(IWaslaDataStore dataStore, ICurrentUser currentUser)
    : IQueryHandler<ListDoctorPracticePricesQuery, IReadOnlyList<DoctorPracticePriceResponse>>
{
    public async Task<Result<IReadOnlyList<DoctorPracticePriceResponse>>> Handle(
        ListDoctorPracticePricesQuery request,
        CancellationToken cancellationToken)
    {
        var access = await DoctorPracticeAccess.LoadOwnedPracticeAsync(
            dataStore, currentUser, request.PracticeId, cancellationToken);
        if (access.IsFailure)
        {
            return Result<IReadOnlyList<DoctorPracticePriceResponse>>.Fail(access.Errors);
        }

        var items = await dataStore.ListDoctorPracticePricesAsync(request.PracticeId, cancellationToken);
        return Result<IReadOnlyList<DoctorPracticePriceResponse>>.Ok(
            items.Select(DoctorPracticeCatalogMapper.Map).ToArray());
    }
}

internal sealed class CreateDoctorPracticePriceCommandHandler(IWaslaDataStore dataStore, ICurrentUser currentUser)
    : ICommandHandler<CreateDoctorPracticePriceCommand, DoctorPracticePriceResponse>
{
    public async Task<Result<DoctorPracticePriceResponse>> Handle(
        CreateDoctorPracticePriceCommand request,
        CancellationToken cancellationToken)
    {
        var access = await DoctorPracticeAccess.LoadOwnedPracticeAsync(
            dataStore, currentUser, request.PracticeId, cancellationToken);
        if (access.IsFailure)
        {
            return Result<DoctorPracticePriceResponse>.Fail(access.Errors);
        }

        var scope = await ValidateScopeAsync(dataStore, request.PracticeId, request.SegmentId,
            request.VisitTypeId, cancellationToken);
        if (scope.IsFailure)
        {
            return Result<DoctorPracticePriceResponse>.Fail(scope.Errors);
        }

        if (await dataStore.DoctorPracticePriceExistsAsync(
                request.PracticeId, request.SegmentId, request.VisitTypeId, null, cancellationToken))
        {
            return Result<DoctorPracticePriceResponse>.Fail(DoctorPracticePricingErrors.Duplicate);
        }

        var price = DoctorPracticeSegmentVisitTypePrice.Create(
            Guid.NewGuid(), request.PracticeId, request.SegmentId, request.VisitTypeId,
            request.Price, access.Value.ActorId);
        if (price.IsFailure)
        {
            return Result<DoctorPracticePriceResponse>.Fail(price.Errors);
        }

        dataStore.Add(price.Value);
        await dataStore.SaveChangesAsync(cancellationToken);
        return Result<DoctorPracticePriceResponse>.Ok(DoctorPracticeCatalogMapper.Map(price.Value));
    }

    private static async Task<Result> ValidateScopeAsync(
        IWaslaDataStore dataStore,
        Guid practiceId,
        Guid segmentId,
        Guid visitTypeId,
        CancellationToken cancellationToken)
    {
        var segment = await dataStore.FindDoctorPracticeSegmentAsync(segmentId, cancellationToken);
        var visitType = await dataStore.FindDoctorPracticeVisitTypeAsync(visitTypeId, cancellationToken);
        return segment is not null && visitType is not null &&
               segment.DoctorPracticeId == practiceId && visitType.DoctorPracticeId == practiceId
            ? Result.Ok()
            : Result.Fail(DoctorPracticePricingErrors.ScopeMismatch);
    }
}

internal sealed class UpdateDoctorPracticePriceCommandHandler(IWaslaDataStore dataStore, ICurrentUser currentUser)
    : ICommandHandler<UpdateDoctorPracticePriceCommand, DoctorPracticePriceResponse>
{
    public async Task<Result<DoctorPracticePriceResponse>> Handle(
        UpdateDoctorPracticePriceCommand request,
        CancellationToken cancellationToken)
    {
        var access = await DoctorPracticeAccess.LoadOwnedPracticeAsync(
            dataStore, currentUser, request.PracticeId, cancellationToken);
        if (access.IsFailure)
        {
            return Result<DoctorPracticePriceResponse>.Fail(access.Errors);
        }

        var price = await dataStore.FindDoctorPracticePriceAsync(request.PriceId, cancellationToken);
        if (price is null || price.DoctorPracticeId != request.PracticeId)
        {
            return Result<DoctorPracticePriceResponse>.Fail(DoctorPracticePricingErrors.NotFound);
        }

        var supplied = DoctorPracticeAccess.VerifyRowVersion(
            price.RowVersion, request.RowVersion, DoctorPracticePricingErrors.ConcurrencyConflict);
        if (supplied.IsFailure)
        {
            return Result<DoctorPracticePriceResponse>.Fail(supplied.Errors);
        }

        var updated = price.Update(request.Price, access.Value.ActorId);
        if (updated.IsFailure)
        {
            return Result<DoctorPracticePriceResponse>.Fail(updated.Errors);
        }

        dataStore.SetOriginalRowVersion(price, supplied.Value);
        await dataStore.SaveChangesAsync(cancellationToken);
        return Result<DoctorPracticePriceResponse>.Ok(DoctorPracticeCatalogMapper.Map(price));
    }
}

internal sealed class DeleteDoctorPracticePriceCommandHandler(IWaslaDataStore dataStore, ICurrentUser currentUser)
    : ICommandHandler<DeleteDoctorPracticePriceCommand>
{
    public async Task<Result> Handle(
        DeleteDoctorPracticePriceCommand request,
        CancellationToken cancellationToken)
    {
        var access = await DoctorPracticeAccess.LoadOwnedPracticeAsync(
            dataStore, currentUser, request.PracticeId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Fail(access.Errors);
        }

        var price = await dataStore.FindDoctorPracticePriceAsync(request.PriceId, cancellationToken);
        if (price is null || price.DoctorPracticeId != request.PracticeId)
        {
            return Result.Fail(DoctorPracticePricingErrors.NotFound);
        }

        var supplied = DoctorPracticeAccess.VerifyRowVersion(
            price.RowVersion, request.RowVersion, DoctorPracticePricingErrors.ConcurrencyConflict);
        if (supplied.IsFailure)
        {
            return Result.Fail(supplied.Errors);
        }

        dataStore.SetOriginalRowVersion(price, supplied.Value);
        dataStore.Remove(price);
        await dataStore.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }
}

internal static class DoctorPracticeCatalogMapper
{
    public static DoctorPracticeSegmentResponse Map(DoctorPracticeSegment item)
        => new(item.Id, item.NameAr, item.NameEn, item.Priority, item.ReservedDailyQuota,
            item.QuotaReleaseBeforeMinutes, item.IsDefault, item.IsActive, RowVersionCodec.Encode(item.RowVersion));

    public static DoctorPracticeVisitTypeResponse Map(DoctorPracticeVisitType item)
        => new(item.Id, item.Type, item.NameAr, item.NameEn, item.DurationMinutes,
            item.IsActive, RowVersionCodec.Encode(item.RowVersion));

    public static DoctorPracticePriceResponse Map(DoctorPracticeSegmentVisitTypePrice item)
        => new(item.Id, item.SegmentId, item.VisitTypeId, item.Price, RowVersionCodec.Encode(item.RowVersion));
}
