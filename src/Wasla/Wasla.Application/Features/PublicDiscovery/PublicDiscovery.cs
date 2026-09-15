using BuildingBlock.Application.Abstraction;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Features.Doctors;
using Wasla.Application.Media;
using Wasla.Domain.Resources;

namespace Wasla.Application.Features.PublicDiscovery;

public static class PublicDiscoveryPolicy
{
    public const int PublicBookingHorizonDays = 30;
}

public sealed record PublicSpecializationResponse(Guid Id, string NameAr, string? NameEn);

public sealed record PublicLocationResponse(int Id, string NameAr, string? NameEn);

public sealed record PublicDoctorSpecializationResponse(
    Guid Id,
    string NameAr,
    string? NameEn,
    bool IsPrimary);

public sealed record PublicPracticeResponse(
    Guid PracticeId,
    string NameAr,
    string? NameEn,
    string? LogoUrl,
    PublicLocationResponse Governorate,
    PublicLocationResponse City,
    PublicLocationResponse Area,
    string Address,
    decimal Latitude,
    decimal Longitude,
    decimal? PublicSearchPrice,
    DateOnly? NextAvailableSlotDate,
    TimeOnly? NextAvailableSlotTime,
    bool IsToday,
    bool IsBookable);

public sealed record PublicDoctorSearchItemResponse(
    Guid DoctorId,
    string? ProfileImageUrl,
    string NameAr,
    string? NameEn,
    IReadOnlyList<PublicDoctorSpecializationResponse> Specializations,
    IReadOnlyList<PublicPracticeResponse> Practices);

public sealed record PublicDoctorQualificationResponse(
    Guid Id,
    string NameAr,
    string? NameEn,
    int DisplayOrder);

public sealed record PublicDoctorDetailsResponse(
    Guid DoctorId,
    string? ProfileImageUrl,
    string NameAr,
    string? NameEn,
    IReadOnlyList<PublicDoctorSpecializationResponse> Specializations,
    string? Bio,
    IReadOnlyList<PublicDoctorQualificationResponse> Qualifications,
    IReadOnlyList<PublicPracticeResponse> Practices);

public sealed record PublicAvailableDateResponse(DateOnly Date, bool IsAvailable);

public sealed record PublicAvailableSlotResponse(DateOnly Date, TimeOnly Time);

public sealed record PublicBookingSegmentResponse(
    Guid SegmentId,
    string NameAr,
    string? NameEn,
    decimal Price,
    bool IsDefault);

public sealed record PublicBookingVisitTypeResponse(
    Guid VisitTypeId,
    string NameAr,
    string? NameEn,
    IReadOnlyList<PublicBookingSegmentResponse> Segments);

public sealed record PublicBookingOptionsResponse(
    Guid PracticeId,
    DateOnly Date,
    TimeOnly Time,
    IReadOnlyList<PublicBookingVisitTypeResponse> VisitTypes);

public sealed record SearchPublicDoctorsQuery(
    string? SearchText,
    Guid? SpecializationId,
    int? GovernorateId,
    int? CityId,
    int? AreaId,
    int PageNumber = 1,
    int PageSize = 20)
    : IQuery<PagedResponse<PublicDoctorSearchItemResponse>>;

public sealed record GetPublicDoctorDetailsQuery(Guid DoctorId) : IQuery<PublicDoctorDetailsResponse>;
public sealed record ListPublicSpecializationsQuery : IQuery<IReadOnlyList<PublicSpecializationResponse>>;
public sealed record GetPublicAvailableDatesQuery(Guid PracticeId)
    : IQuery<IReadOnlyList<PublicAvailableDateResponse>>;
public sealed record GetPublicAvailableSlotsQuery(Guid PracticeId, DateOnly Date)
    : IQuery<IReadOnlyList<PublicAvailableSlotResponse>>;
public sealed record GetPublicBookingOptionsQuery(Guid PracticeId, DateOnly Date, TimeOnly Time)
    : IQuery<PublicBookingOptionsResponse>;
public sealed record GetPublicDoctorProfileImageQuery(Guid DoctorId) : IQuery<PrivateMedia>;
public sealed record GetPublicPracticeLogoQuery(Guid PracticeId) : IQuery<PrivateMedia>;

public sealed record PracticeOccupancySnapshot(
    IReadOnlySet<TimeOnly> OccupiedSlots,
    int TotalReservations,
    IReadOnlyDictionary<Guid, int> SegmentReservationCounts)
{
    public static PracticeOccupancySnapshot Empty { get; } = new(
        new HashSet<TimeOnly>(),
        0,
        new Dictionary<Guid, int>());
}

public interface IPracticeReservationOccupancyReader
{
    Task<IReadOnlyDictionary<(Guid PracticeId, DateOnly Date), PracticeOccupancySnapshot>> ReadAsync(
        IReadOnlyCollection<Guid> practiceIds,
        DateOnly fromDate,
        DateOnly throughDate,
        CancellationToken cancellationToken);
}

public interface IPublicDoctorPopularityReader
{
    Task<IReadOnlyDictionary<Guid, long>> ReadSuccessfulReservationCountsAsync(
        IReadOnlyCollection<Guid> doctorIds,
        DateTime sinceUtc,
        CancellationToken cancellationToken);
}

/// <summary>
/// Refreshes the disposable read projection used to rank public doctor search results.
/// Future reservation create/cancel flows can call this seam after their transaction commits.
/// </summary>
public interface IPublicDiscoveryRankingProjectionRefresher
{
    Task RefreshPracticesAsync(
        IReadOnlyCollection<Guid> practiceIds,
        CancellationToken cancellationToken);

    Task RefreshDoctorsAsync(
        IReadOnlyCollection<Guid> doctorIds,
        CancellationToken cancellationToken);

    Task RefreshAllAsync(CancellationToken cancellationToken);
}

public interface IPublicDiscoveryService
{
    Task<PagedResponse<PublicDoctorSearchItemResponse>> SearchDoctorsAsync(
        SearchPublicDoctorsQuery request,
        CancellationToken cancellationToken);
    Task<PublicDoctorDetailsResponse?> GetDoctorAsync(Guid doctorId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PublicSpecializationResponse>> ListSpecializationsAsync(CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<PublicAvailableDateResponse>>> GetAvailableDatesAsync(
        Guid practiceId,
        CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<PublicAvailableSlotResponse>>> GetAvailableSlotsAsync(
        Guid practiceId,
        DateOnly requestedDate,
        CancellationToken cancellationToken);
    Task<Result<PublicBookingOptionsResponse>> GetBookingOptionsAsync(
        Guid practiceId,
        DateOnly requestedDate,
        TimeOnly requestedTime,
        CancellationToken cancellationToken);
    Task<string?> GetDoctorProfileImageKeyAsync(Guid doctorId, CancellationToken cancellationToken);
    Task<string?> GetPracticeLogoKeyAsync(Guid practiceId, CancellationToken cancellationToken);
}

public static class PublicDiscoveryErrors
{
    public static Error DoctorNotFound => Error.NotFound(
        "PublicDiscovery.DoctorNotFound",
        ErrorMessage.PublicDoctorNotFound);
    public static Error PracticeNotFound => Error.NotFound(
        "PublicDiscovery.PracticeNotFound",
        ErrorMessage.PublicPracticeNotFound);
    public static Error PracticeNotBookable => Error.Conflict(
        "PublicDiscovery.PracticeNotBookable",
        ErrorMessage.PublicPracticeNotBookable);
    public static Error DateOutsideHorizon => Error.Validation(
        "PublicDiscovery.DateOutsideHorizon",
        ErrorMessage.PublicDateOutsideHorizon);
    public static Error SlotUnavailable => Error.Conflict(
        "PublicDiscovery.SlotUnavailable",
        ErrorMessage.PublicSlotUnavailable);
    public static Error MediaNotFound => Error.NotFound(
        "PublicDiscovery.MediaNotFound",
        ErrorMessage.PublicMediaNotFound);
}

internal sealed class SearchPublicDoctorsQueryValidator : AbstractValidator<SearchPublicDoctorsQuery>
{
    public SearchPublicDoctorsQueryValidator()
    {
        RuleFor(query => query.SearchText).MaximumLength(200);
        RuleFor(query => query.SpecializationId).NotEmpty().When(query => query.SpecializationId.HasValue);
        RuleFor(query => query.GovernorateId).GreaterThan(0).When(query => query.GovernorateId.HasValue);
        RuleFor(query => query.CityId).GreaterThan(0).When(query => query.CityId.HasValue);
        RuleFor(query => query.AreaId).GreaterThan(0).When(query => query.AreaId.HasValue);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

internal sealed class GetPublicDoctorDetailsQueryValidator : AbstractValidator<GetPublicDoctorDetailsQuery>
{
    public GetPublicDoctorDetailsQueryValidator() => RuleFor(query => query.DoctorId).NotEmpty();
}

internal sealed class GetPublicAvailableDatesQueryValidator : AbstractValidator<GetPublicAvailableDatesQuery>
{
    public GetPublicAvailableDatesQueryValidator() => RuleFor(query => query.PracticeId).NotEmpty();
}

internal sealed class GetPublicAvailableSlotsQueryValidator : AbstractValidator<GetPublicAvailableSlotsQuery>
{
    public GetPublicAvailableSlotsQueryValidator()
    {
        RuleFor(query => query.PracticeId).NotEmpty();
        RuleFor(query => query.Date).NotEmpty();
    }
}

internal sealed class GetPublicBookingOptionsQueryValidator : AbstractValidator<GetPublicBookingOptionsQuery>
{
    public GetPublicBookingOptionsQueryValidator()
    {
        RuleFor(query => query.PracticeId).NotEmpty();
        RuleFor(query => query.Date).NotEmpty();
    }
}

internal sealed class SearchPublicDoctorsQueryHandler(IPublicDiscoveryService service)
    : IQueryHandler<SearchPublicDoctorsQuery, PagedResponse<PublicDoctorSearchItemResponse>>
{
    public async Task<Result<PagedResponse<PublicDoctorSearchItemResponse>>> Handle(
        SearchPublicDoctorsQuery request,
        CancellationToken cancellationToken)
        => Result<PagedResponse<PublicDoctorSearchItemResponse>>.Ok(
            await service.SearchDoctorsAsync(request, cancellationToken));
}

internal sealed class GetPublicDoctorDetailsQueryHandler(IPublicDiscoveryService service)
    : IQueryHandler<GetPublicDoctorDetailsQuery, PublicDoctorDetailsResponse>
{
    public async Task<Result<PublicDoctorDetailsResponse>> Handle(
        GetPublicDoctorDetailsQuery request,
        CancellationToken cancellationToken)
    {
        var response = await service.GetDoctorAsync(request.DoctorId, cancellationToken);
        return response is null
            ? Result<PublicDoctorDetailsResponse>.Fail(PublicDiscoveryErrors.DoctorNotFound)
            : Result<PublicDoctorDetailsResponse>.Ok(response);
    }
}

internal sealed class ListPublicSpecializationsQueryHandler(IPublicDiscoveryService service)
    : IQueryHandler<ListPublicSpecializationsQuery, IReadOnlyList<PublicSpecializationResponse>>
{
    public async Task<Result<IReadOnlyList<PublicSpecializationResponse>>> Handle(
        ListPublicSpecializationsQuery request,
        CancellationToken cancellationToken)
        => Result<IReadOnlyList<PublicSpecializationResponse>>.Ok(
            await service.ListSpecializationsAsync(cancellationToken));
}

internal sealed class GetPublicAvailableDatesQueryHandler(IPublicDiscoveryService service)
    : IQueryHandler<GetPublicAvailableDatesQuery, IReadOnlyList<PublicAvailableDateResponse>>
{
    public Task<Result<IReadOnlyList<PublicAvailableDateResponse>>> Handle(
        GetPublicAvailableDatesQuery request,
        CancellationToken cancellationToken)
        => service.GetAvailableDatesAsync(request.PracticeId, cancellationToken);
}

internal sealed class GetPublicAvailableSlotsQueryHandler(IPublicDiscoveryService service)
    : IQueryHandler<GetPublicAvailableSlotsQuery, IReadOnlyList<PublicAvailableSlotResponse>>
{
    public Task<Result<IReadOnlyList<PublicAvailableSlotResponse>>> Handle(
        GetPublicAvailableSlotsQuery request,
        CancellationToken cancellationToken)
        => service.GetAvailableSlotsAsync(request.PracticeId, request.Date, cancellationToken);
}

internal sealed class GetPublicBookingOptionsQueryHandler(IPublicDiscoveryService service)
    : IQueryHandler<GetPublicBookingOptionsQuery, PublicBookingOptionsResponse>
{
    public Task<Result<PublicBookingOptionsResponse>> Handle(
        GetPublicBookingOptionsQuery request,
        CancellationToken cancellationToken)
        => service.GetBookingOptionsAsync(request.PracticeId, request.Date, request.Time, cancellationToken);
}

internal sealed class GetPublicDoctorProfileImageQueryHandler(
    IPublicDiscoveryService service,
    IPrivateMediaReader mediaReader)
    : IQueryHandler<GetPublicDoctorProfileImageQuery, PrivateMedia>
{
    public async Task<Result<PrivateMedia>> Handle(
        GetPublicDoctorProfileImageQuery request,
        CancellationToken cancellationToken)
    {
        var key = await service.GetDoctorProfileImageKeyAsync(request.DoctorId, cancellationToken);
        var media = key is null ? null : await mediaReader.OpenAsync(key, cancellationToken);
        return media is null
            ? Result<PrivateMedia>.Fail(PublicDiscoveryErrors.MediaNotFound)
            : Result<PrivateMedia>.Ok(media);
    }
}

internal sealed class GetPublicPracticeLogoQueryHandler(
    IPublicDiscoveryService service,
    IPrivateMediaReader mediaReader)
    : IQueryHandler<GetPublicPracticeLogoQuery, PrivateMedia>
{
    public async Task<Result<PrivateMedia>> Handle(
        GetPublicPracticeLogoQuery request,
        CancellationToken cancellationToken)
    {
        var key = await service.GetPracticeLogoKeyAsync(request.PracticeId, cancellationToken);
        var media = key is null ? null : await mediaReader.OpenAsync(key, cancellationToken);
        return media is null
            ? Result<PrivateMedia>.Fail(PublicDiscoveryErrors.MediaNotFound)
            : Result<PrivateMedia>.Ok(media);
    }
}

internal sealed class EmptyPracticeReservationOccupancyReader : IPracticeReservationOccupancyReader
{
    public Task<IReadOnlyDictionary<(Guid PracticeId, DateOnly Date), PracticeOccupancySnapshot>> ReadAsync(
        IReadOnlyCollection<Guid> practiceIds,
        DateOnly fromDate,
        DateOnly throughDate,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyDictionary<(Guid, DateOnly), PracticeOccupancySnapshot>>(
            new Dictionary<(Guid, DateOnly), PracticeOccupancySnapshot>());
    }
}

internal sealed class EmptyPublicDoctorPopularityReader : IPublicDoctorPopularityReader
{
    public Task<IReadOnlyDictionary<Guid, long>> ReadSuccessfulReservationCountsAsync(
        IReadOnlyCollection<Guid> doctorIds,
        DateTime sinceUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyDictionary<Guid, long>>(
            doctorIds.Distinct().ToDictionary(id => id, _ => 0L));
    }
}

internal sealed class EmptyPublicDiscoveryRankingProjectionRefresher
    : IPublicDiscoveryRankingProjectionRefresher
{
    public Task RefreshPracticesAsync(
        IReadOnlyCollection<Guid> practiceIds,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task RefreshDoctorsAsync(
        IReadOnlyCollection<Guid> doctorIds,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task RefreshAllAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
