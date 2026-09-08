using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Features.Doctors;
using Wasla.Application.Persistence;
using Wasla.Domain.ReferenceData;
using Wasla.Domain.Resources;
using static Wasla.Application.Features.Onboarding.MedicalSpecializationCommandHelpers;

namespace Wasla.Application.Features.Onboarding;

public static class OnboardingCacheTags
{
    public const string MedicalSpecializations = "medical-specializations";
    public const string EgyptLocations = "egypt-locations";
}

public sealed record MedicalSpecializationResponse(
    Guid Id,
    string NameAr,
    string? NameEn,
    string? DescriptionAr,
    string? DescriptionEn,
    bool IsActive,
    int SortOrder,
    bool IsDeleted,
    string RowVersion);

public sealed record ListMedicalSpecializationsQuery(
    string? Search,
    bool? IsActive,
    bool? IsDeleted,
    int PageNumber = 1,
    int PageSize = 20) : IQuery<PagedResponse<MedicalSpecializationResponse>>;

public sealed record GetMedicalSpecializationQuery(Guid Id) : IQuery<MedicalSpecializationResponse>;

public sealed record CreateMedicalSpecializationCommand(
    string NameAr,
    string? NameEn,
    string? DescriptionAr,
    string? DescriptionEn,
    int SortOrder) : ICommand<MedicalSpecializationResponse>, ITransactionalCommand<WaslaWritePersistence>, ICacheInvalidator
{
    public IEnumerable<string> Tags => [OnboardingCacheTags.MedicalSpecializations];
}

public sealed record UpdateMedicalSpecializationCommand(
    Guid Id,
    string NameAr,
    string? NameEn,
    string? DescriptionAr,
    string? DescriptionEn,
    int SortOrder,
    string RowVersion) : ICommand<MedicalSpecializationResponse>, ITransactionalCommand<WaslaWritePersistence>, ICacheInvalidator
{
    public IEnumerable<string> Tags => [OnboardingCacheTags.MedicalSpecializations];
}

public sealed record ChangeMedicalSpecializationStateCommand(
    Guid Id,
    MedicalSpecializationStateChange Change,
    string RowVersion) : ICommand<MedicalSpecializationResponse>, ITransactionalCommand<WaslaWritePersistence>, ICacheInvalidator
{
    public IEnumerable<string> Tags => [OnboardingCacheTags.MedicalSpecializations];
}

public enum MedicalSpecializationStateChange
{
    Activate,
    Deactivate,
    Delete,
    Restore
}

internal sealed class ListMedicalSpecializationsQueryValidator : AbstractValidator<ListMedicalSpecializationsQuery>
{
    public ListMedicalSpecializationsQueryValidator()
    {
        RuleFor(query => query.Search).MaximumLength(200);
        RuleFor(query => query.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

internal sealed class CreateMedicalSpecializationCommandValidator : AbstractValidator<CreateMedicalSpecializationCommand>
{
    public CreateMedicalSpecializationCommandValidator()
    {
        Include(new MedicalSpecializationFieldsValidator());
    }
}

internal sealed class UpdateMedicalSpecializationCommandValidator : AbstractValidator<UpdateMedicalSpecializationCommand>
{
    public UpdateMedicalSpecializationCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(command => command.NameEn).MaximumLength(200);
        RuleFor(command => command.DescriptionAr).MaximumLength(1000);
        RuleFor(command => command.DescriptionEn).MaximumLength(1000);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.RowVersion).NotEmpty().Must(RowVersionCodec.IsValid);
    }
}

internal sealed class MedicalSpecializationFieldsValidator : AbstractValidator<CreateMedicalSpecializationCommand>
{
    public MedicalSpecializationFieldsValidator()
    {
        RuleFor(command => command.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(command => command.NameEn).MaximumLength(200);
        RuleFor(command => command.DescriptionAr).MaximumLength(1000);
        RuleFor(command => command.DescriptionEn).MaximumLength(1000);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class ChangeMedicalSpecializationStateCommandValidator : AbstractValidator<ChangeMedicalSpecializationStateCommand>
{
    public ChangeMedicalSpecializationStateCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.Change).IsInEnum();
        RuleFor(command => command.RowVersion).NotEmpty().Must(RowVersionCodec.IsValid);
    }
}

internal sealed class ListMedicalSpecializationsQueryHandler(IWaslaDataStore dataStore)
    : IQueryHandler<ListMedicalSpecializationsQuery, PagedResponse<MedicalSpecializationResponse>>
{
    public async Task<Result<PagedResponse<MedicalSpecializationResponse>>> Handle(
        ListMedicalSpecializationsQuery request,
        CancellationToken cancellationToken)
    {
        var (items, total) = await dataStore.ListMedicalSpecializationsAsync(
            request.Search,
            request.IsActive,
            request.IsDeleted,
            request.PageNumber,
            request.PageSize,
            cancellationToken);
        return Result<PagedResponse<MedicalSpecializationResponse>>.Ok(new(
            items.Select(MedicalSpecializationMapper.Map).ToArray(),
            total,
            request.PageNumber,
            request.PageSize));
    }
}

internal sealed class GetMedicalSpecializationQueryHandler(IWaslaDataStore dataStore)
    : IQueryHandler<GetMedicalSpecializationQuery, MedicalSpecializationResponse>
{
    public async Task<Result<MedicalSpecializationResponse>> Handle(
        GetMedicalSpecializationQuery request,
        CancellationToken cancellationToken)
    {
        var item = await dataStore.FindMedicalSpecializationAsync(request.Id, true, cancellationToken);
        return item is null
            ? Result<MedicalSpecializationResponse>.Fail(MedicalSpecializationErrors.NotFound)
            : Result<MedicalSpecializationResponse>.Ok(MedicalSpecializationMapper.Map(item));
    }
}

internal sealed class CreateMedicalSpecializationCommandHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser)
    : ICommandHandler<CreateMedicalSpecializationCommand, MedicalSpecializationResponse>
{
    public async Task<Result<MedicalSpecializationResponse>> Handle(
        CreateMedicalSpecializationCommand request,
        CancellationToken cancellationToken)
    {
        var actor = CurrentActor(currentUser);
        if (actor.IsFailure)
        {
            return Result<MedicalSpecializationResponse>.Fail(actor.Errors);
        }

        var duplicate = await EnsureNamesAvailableAsync(dataStore, request.NameAr, request.NameEn, null, cancellationToken);
        if (duplicate.IsFailure)
        {
            return Result<MedicalSpecializationResponse>.Fail(duplicate.Errors);
        }

        var created = MedicalSpecialization.Create(
            Guid.NewGuid(), request.NameAr, request.NameEn, request.DescriptionAr, request.DescriptionEn, request.SortOrder, actor.Value);
        if (created.IsFailure)
        {
            return Result<MedicalSpecializationResponse>.Fail(created.Errors);
        }

        dataStore.Add(created.Value);
        await dataStore.SaveChangesAsync(cancellationToken);
        return Result<MedicalSpecializationResponse>.Ok(MedicalSpecializationMapper.Map(created.Value));
    }
}

internal sealed class UpdateMedicalSpecializationCommandHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser)
    : ICommandHandler<UpdateMedicalSpecializationCommand, MedicalSpecializationResponse>
{
    public async Task<Result<MedicalSpecializationResponse>> Handle(
        UpdateMedicalSpecializationCommand request,
        CancellationToken cancellationToken)
    {
        var actor = CurrentActor(currentUser);
        if (actor.IsFailure)
        {
            return Result<MedicalSpecializationResponse>.Fail(actor.Errors);
        }

        var item = await dataStore.FindMedicalSpecializationAsync(request.Id, false, cancellationToken);
        if (item is null)
        {
            return Result<MedicalSpecializationResponse>.Fail(MedicalSpecializationErrors.NotFound);
        }

        var concurrency = VerifyRowVersion(item.RowVersion, request.RowVersion);
        if (concurrency.IsFailure)
        {
            return Result<MedicalSpecializationResponse>.Fail(concurrency.Errors);
        }

        var duplicate = await EnsureNamesAvailableAsync(dataStore, request.NameAr, request.NameEn, item.Id, cancellationToken);
        if (duplicate.IsFailure)
        {
            return Result<MedicalSpecializationResponse>.Fail(duplicate.Errors);
        }

        var update = item.Update(
            request.NameAr, request.NameEn, request.DescriptionAr, request.DescriptionEn, request.SortOrder, actor.Value);
        if (update.IsFailure)
        {
            return Result<MedicalSpecializationResponse>.Fail(update.Errors);
        }

        dataStore.SetOriginalRowVersion(item, concurrency.Value);
        await dataStore.SaveChangesAsync(cancellationToken);
        return Result<MedicalSpecializationResponse>.Ok(MedicalSpecializationMapper.Map(item));
    }
}

internal sealed class ChangeMedicalSpecializationStateCommandHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser)
    : ICommandHandler<ChangeMedicalSpecializationStateCommand, MedicalSpecializationResponse>
{
    public async Task<Result<MedicalSpecializationResponse>> Handle(
        ChangeMedicalSpecializationStateCommand request,
        CancellationToken cancellationToken)
    {
        var actor = CurrentActor(currentUser);
        if (actor.IsFailure)
        {
            return Result<MedicalSpecializationResponse>.Fail(actor.Errors);
        }

        var item = await dataStore.FindMedicalSpecializationAsync(
            request.Id,
            request.Change == MedicalSpecializationStateChange.Restore,
            cancellationToken);
        if (item is null)
        {
            return Result<MedicalSpecializationResponse>.Fail(MedicalSpecializationErrors.NotFound);
        }

        var concurrency = VerifyRowVersion(item.RowVersion, request.RowVersion);
        if (concurrency.IsFailure)
        {
            return Result<MedicalSpecializationResponse>.Fail(concurrency.Errors);
        }

        var result = request.Change switch
        {
            MedicalSpecializationStateChange.Activate => item.Activate(actor.Value),
            MedicalSpecializationStateChange.Deactivate => item.Deactivate(actor.Value),
            MedicalSpecializationStateChange.Delete => item.SoftDelete(actor.Value),
            MedicalSpecializationStateChange.Restore => item.Restore(actor.Value),
            _ => Result.Fail(MedicalSpecializationErrors.Invalid)
        };
        if (result.IsFailure)
        {
            return Result<MedicalSpecializationResponse>.Fail(result.Errors);
        }

        dataStore.SetOriginalRowVersion(item, concurrency.Value);
        await dataStore.SaveChangesAsync(cancellationToken);
        return Result<MedicalSpecializationResponse>.Ok(MedicalSpecializationMapper.Map(item));
    }
}

internal static class MedicalSpecializationMapper
{
    public static MedicalSpecializationResponse Map(MedicalSpecialization item)
        => new(
            item.Id,
            item.NameAr,
            item.NameEn,
            item.DescriptionAr,
            item.DescriptionEn,
            item.IsActive,
            item.SortOrder,
            item.IsDeleted,
            RowVersionCodec.Encode(item.RowVersion));
}

internal static class MedicalSpecializationCommandHelpers
{
    public static Result<Guid> CurrentActor(ICurrentUser currentUser)
        => currentUser.IsAuthenticated && currentUser.UserId is { } actorId
            ? Result<Guid>.Ok(actorId)
            : Result<Guid>.Fail(Error.Unauthorized("Auth.AuthenticationRequired", ErrorMessage.AuthenticationRequired));

    public static Result<byte[]> VerifyRowVersion(byte[] actual, string encoded)
    {
        var supplied = RowVersionCodec.Decode(encoded);
        return supplied is not null && actual.AsSpan().SequenceEqual(supplied)
            ? Result<byte[]>.Ok(supplied)
            : Result<byte[]>.Fail(MedicalSpecializationErrors.ConcurrencyConflict);
    }

    public static async Task<Result> EnsureNamesAvailableAsync(
        IWaslaDataStore dataStore,
        string nameAr,
        string? nameEn,
        Guid? excludingId,
        CancellationToken cancellationToken)
    {
        if (await dataStore.MedicalSpecializationNameArExistsAsync(nameAr.Trim(), excludingId, cancellationToken))
        {
            return Result.Fail(MedicalSpecializationErrors.DuplicateNameAr);
        }

        var normalizedEn = string.IsNullOrWhiteSpace(nameEn) ? null : nameEn.Trim();
        return normalizedEn is not null &&
               await dataStore.MedicalSpecializationNameEnExistsAsync(normalizedEn, excludingId, cancellationToken)
            ? Result.Fail(MedicalSpecializationErrors.DuplicateNameEn)
            : Result.Ok();
    }
}
