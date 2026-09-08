using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Features.Doctors;
using Wasla.Application.Persistence;
using Wasla.Domain.Doctors;
using Wasla.Domain.ReferenceData;
using static Wasla.Application.Features.Onboarding.PracticeLocationMapper;

namespace Wasla.Application.Features.Onboarding;

public sealed record LocationReferenceResponse(int Id, string NameAr, string? NameEn);
public sealed record DoctorPracticeLocationResponse(
    Guid Id,
    LocationReferenceResponse Governorate,
    LocationReferenceResponse City,
    LocationReferenceResponse Area,
    string DetailedAddress,
    decimal Latitude,
    decimal Longitude,
    string RowVersion);

public sealed record GetGovernoratesQuery : ICacheableQuery<IReadOnlyList<LocationReferenceResponse>>
{
    public string? CacheKey => "egypt-governorates:v1";
    public TimeSpan? TimeToLive => TimeSpan.FromHours(12);
    public IEnumerable<string> Tags => [OnboardingCacheTags.EgyptLocations];
}
public sealed record GetCitiesQuery(int GovernorateId) : ICacheableQuery<IReadOnlyList<LocationReferenceResponse>>
{
    public string? CacheKey => $"egypt-cities:v1:{GovernorateId}";
    public TimeSpan? TimeToLive => TimeSpan.FromHours(12);
    public IEnumerable<string> Tags => [OnboardingCacheTags.EgyptLocations];
}
public sealed record GetAreasQuery(int CityId) : ICacheableQuery<IReadOnlyList<LocationReferenceResponse>>
{
    public string? CacheKey => $"egypt-areas:v1:{CityId}";
    public TimeSpan? TimeToLive => TimeSpan.FromHours(12);
    public IEnumerable<string> Tags => [OnboardingCacheTags.EgyptLocations];
}
public sealed record GetMyDoctorPracticeLocationQuery : IQuery<DoctorPracticeLocationResponse>;
public sealed record UpsertMyDoctorPracticeLocationCommand(
    int GovernorateId,
    int CityId,
    int AreaId,
    string DetailedAddress,
    decimal Latitude,
    decimal Longitude,
    string? RowVersion) : ICommand<DoctorPracticeLocationResponse>, ITransactionalCommand<WaslaWritePersistence>;

internal sealed class GetCitiesQueryValidator : AbstractValidator<GetCitiesQuery>
{
    public GetCitiesQueryValidator() => RuleFor(query => query.GovernorateId).GreaterThan(0);
}

internal sealed class GetAreasQueryValidator : AbstractValidator<GetAreasQuery>
{
    public GetAreasQueryValidator() => RuleFor(query => query.CityId).GreaterThan(0);
}

internal sealed class UpsertMyDoctorPracticeLocationCommandValidator : AbstractValidator<UpsertMyDoctorPracticeLocationCommand>
{
    public UpsertMyDoctorPracticeLocationCommandValidator()
    {
        RuleFor(command => command.GovernorateId).GreaterThan(0);
        RuleFor(command => command.CityId).GreaterThan(0);
        RuleFor(command => command.AreaId).GreaterThan(0);
        RuleFor(command => command.DetailedAddress).NotEmpty().MaximumLength(500);
        RuleFor(command => command.Latitude).InclusiveBetween(-90m, 90m);
        RuleFor(command => command.Longitude).InclusiveBetween(-180m, 180m);
        RuleFor(command => command.RowVersion).Must(value => value is null || RowVersionCodec.IsValid(value));
    }
}

internal sealed class GetGovernoratesQueryHandler(IWaslaDataStore dataStore)
    : IQueryHandler<GetGovernoratesQuery, IReadOnlyList<LocationReferenceResponse>>
{
    public async Task<Result<IReadOnlyList<LocationReferenceResponse>>> Handle(
        GetGovernoratesQuery request,
        CancellationToken cancellationToken)
        => Result<IReadOnlyList<LocationReferenceResponse>>.Ok(
            (await dataStore.ListGovernoratesAsync(cancellationToken)).Select(MapReference).ToArray());
}

internal sealed class GetCitiesQueryHandler(IWaslaDataStore dataStore)
    : IQueryHandler<GetCitiesQuery, IReadOnlyList<LocationReferenceResponse>>
{
    public async Task<Result<IReadOnlyList<LocationReferenceResponse>>> Handle(
        GetCitiesQuery request,
        CancellationToken cancellationToken)
    {
        if (!await dataStore.ActiveGovernorateExistsAsync(request.GovernorateId, cancellationToken))
        {
            return Result<IReadOnlyList<LocationReferenceResponse>>.Fail(LocationErrors.GovernorateNotFound);
        }

        return Result<IReadOnlyList<LocationReferenceResponse>>.Ok(
            (await dataStore.ListCitiesAsync(request.GovernorateId, cancellationToken)).Select(MapReference).ToArray());
    }
}

internal sealed class GetAreasQueryHandler(IWaslaDataStore dataStore)
    : IQueryHandler<GetAreasQuery, IReadOnlyList<LocationReferenceResponse>>
{
    public async Task<Result<IReadOnlyList<LocationReferenceResponse>>> Handle(
        GetAreasQuery request,
        CancellationToken cancellationToken)
    {
        if (!await dataStore.ActiveCityExistsAsync(request.CityId, cancellationToken))
        {
            return Result<IReadOnlyList<LocationReferenceResponse>>.Fail(LocationErrors.CityNotFound);
        }

        return Result<IReadOnlyList<LocationReferenceResponse>>.Ok(
            (await dataStore.ListAreasAsync(request.CityId, cancellationToken)).Select(MapReference).ToArray());
    }
}

internal sealed class GetMyDoctorPracticeLocationQueryHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser)
    : IQueryHandler<GetMyDoctorPracticeLocationQuery, DoctorPracticeLocationResponse>
{
    public async Task<Result<DoctorPracticeLocationResponse>> Handle(
        GetMyDoctorPracticeLocationQuery request,
        CancellationToken cancellationToken)
    {
        var doctor = await DoctorOnboardingAccess.ResolveAsync(dataStore, currentUser, cancellationToken);
        if (doctor.IsFailure)
        {
            return Result<DoctorPracticeLocationResponse>.Fail(doctor.Errors);
        }

        var location = await dataStore.GetDoctorPracticeLocationAsync(doctor.Value.Id, cancellationToken);
        return location is null
            ? Result<DoctorPracticeLocationResponse>.Fail(DoctorPracticeLocationErrors.NotFound)
            : Result<DoctorPracticeLocationResponse>.Ok(MapLocation(location));
    }
}

internal sealed class UpsertMyDoctorPracticeLocationCommandHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser)
    : ICommandHandler<UpsertMyDoctorPracticeLocationCommand, DoctorPracticeLocationResponse>
{
    public async Task<Result<DoctorPracticeLocationResponse>> Handle(
        UpsertMyDoctorPracticeLocationCommand request,
        CancellationToken cancellationToken)
    {
        var doctor = await DoctorOnboardingAccess.ResolveAsync(dataStore, currentUser, cancellationToken);
        if (doctor.IsFailure)
        {
            return Result<DoctorPracticeLocationResponse>.Fail(doctor.Errors);
        }

        var hierarchy = await dataStore.FindLocationHierarchyAsync(request.AreaId, cancellationToken);
        if (hierarchy is null)
        {
            return Result<DoctorPracticeLocationResponse>.Fail(LocationErrors.AreaNotFound);
        }
        if (hierarchy.CityId != request.CityId || hierarchy.GovernorateId != request.GovernorateId)
        {
            return Result<DoctorPracticeLocationResponse>.Fail(LocationErrors.InvalidHierarchy);
        }
        if (!hierarchy.AreaIsActive || !hierarchy.CityIsActive || !hierarchy.GovernorateIsActive)
        {
            return Result<DoctorPracticeLocationResponse>.Fail(LocationErrors.Inactive);
        }

        var location = await dataStore.FindDoctorPracticeLocationAsync(doctor.Value.Id, cancellationToken);
        if (location is null)
        {
            if (request.RowVersion is not null)
            {
                return Result<DoctorPracticeLocationResponse>.Fail(DoctorPracticeLocationErrors.RowVersionMustBeNull);
            }

            var created = DoctorPracticeLocation.Create(
                Guid.NewGuid(), doctor.Value.Id, request.GovernorateId, request.CityId, request.AreaId,
                request.DetailedAddress, request.Latitude, request.Longitude);
            if (created.IsFailure)
            {
                return Result<DoctorPracticeLocationResponse>.Fail(created.Errors);
            }

            location = created.Value;
            dataStore.Add(location);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.RowVersion))
            {
                return Result<DoctorPracticeLocationResponse>.Fail(DoctorPracticeLocationErrors.RowVersionRequired);
            }

            var supplied = RowVersionCodec.Decode(request.RowVersion);
            if (supplied is null || !location.RowVersion.AsSpan().SequenceEqual(supplied))
            {
                return Result<DoctorPracticeLocationResponse>.Fail(DoctorPracticeLocationErrors.ConcurrencyConflict);
            }

            var updated = location.Update(
                request.GovernorateId, request.CityId, request.AreaId,
                request.DetailedAddress, request.Latitude, request.Longitude);
            if (updated.IsFailure)
            {
                return Result<DoctorPracticeLocationResponse>.Fail(updated.Errors);
            }
            dataStore.SetOriginalRowVersion(location, supplied);
        }

        await dataStore.SaveChangesAsync(cancellationToken);
        var view = await dataStore.GetDoctorPracticeLocationAsync(doctor.Value.Id, cancellationToken)
            ?? throw new InvalidOperationException("The saved Doctor practice location could not be reloaded.");
        return Result<DoctorPracticeLocationResponse>.Ok(MapLocation(view));
    }
}

internal static class PracticeLocationMapper
{
    public static LocationReferenceResponse MapReference(LocationReferenceRecord item)
        => new(item.Id, item.NameAr, item.NameEn);

    public static DoctorPracticeLocationResponse MapLocation(DoctorPracticeLocationViewRecord item)
        => new(
            item.Location.Id,
            MapReference(item.Governorate),
            MapReference(item.City),
            MapReference(item.Area),
            item.Location.DetailedAddress,
            item.Location.Latitude,
            item.Location.Longitude,
            RowVersionCodec.Encode(item.Location.RowVersion));
}
