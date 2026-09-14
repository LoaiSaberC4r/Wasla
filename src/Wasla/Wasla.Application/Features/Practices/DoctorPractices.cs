using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Media;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Features.Doctors;
using Wasla.Application.Media;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Doctors;
using Wasla.Domain.Practices;
using Wasla.Domain.ReferenceData;
using Wasla.Domain.Resources;

namespace Wasla.Application.Features.Practices;

public sealed record PracticeLocationResponse(
    int GovernorateId,
    string GovernorateNameAr,
    string? GovernorateNameEn,
    int CityId,
    string CityNameAr,
    string? CityNameEn,
    int AreaId,
    string AreaNameAr,
    string? AreaNameEn,
    string DetailedAddress,
    decimal Latitude,
    decimal Longitude);

public sealed record DoctorPracticeResponse(
    Guid Id,
    string NameAr,
    string? NameEn,
    PracticeLocationResponse Location,
    bool IsActive,
    bool HasLogo,
    string RowVersion);

public sealed record CreateDoctorPracticeCommand(
    string NameAr,
    string? NameEn,
    int GovernorateId,
    int CityId,
    int AreaId,
    string DetailedAddress,
    decimal Latitude,
    decimal Longitude)
    : ICommand<DoctorPracticeResponse>, ITransactionalCommand<WaslaWritePersistence>;

public sealed record UpdateDoctorPracticeCommand(
    Guid PracticeId,
    string NameAr,
    string? NameEn,
    int GovernorateId,
    int CityId,
    int AreaId,
    string DetailedAddress,
    decimal Latitude,
    decimal Longitude,
    string RowVersion)
    : ICommand<DoctorPracticeResponse>, ITransactionalCommand<WaslaWritePersistence>;

public sealed record ListMyDoctorPracticesQuery : IQuery<IReadOnlyList<DoctorPracticeResponse>>;
public sealed record GetMyDoctorPracticeQuery(Guid PracticeId) : IQuery<DoctorPracticeResponse>;
public sealed record ActivateDoctorPracticeCommand(Guid PracticeId, string RowVersion)
    : ICommand<DoctorPracticeResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record DeactivateDoctorPracticeCommand(Guid PracticeId, string RowVersion)
    : ICommand<DoctorPracticeResponse>, ITransactionalCommand<WaslaWritePersistence>;

public sealed record DoctorPracticeConfigurationResponse(
    Guid Id,
    Guid DoctorPracticeId,
    bool AllowOnlineBooking,
    bool AllowWalkIn,
    int DefaultSlotDurationMinutes,
    int CheckInGracePeriodMinutes,
    int PatientSelfCancellationCutoffMinutes,
    int? MaximumDailyPatients,
    int MaximumTicketCallAttempts,
    string TimeZoneId,
    string RowVersion);

public sealed record GetDoctorPracticeConfigurationQuery(Guid PracticeId)
    : IQuery<DoctorPracticeConfigurationResponse>;
public sealed record UpdateDoctorPracticeConfigurationCommand(
    Guid PracticeId,
    bool AllowOnlineBooking,
    bool AllowWalkIn,
    int DefaultSlotDurationMinutes,
    int CheckInGracePeriodMinutes,
    int PatientSelfCancellationCutoffMinutes,
    int? MaximumDailyPatients,
    int MaximumTicketCallAttempts,
    string TimeZoneId,
    string RowVersion)
    : ICommand<DoctorPracticeConfigurationResponse>, ITransactionalCommand<WaslaWritePersistence>;

public sealed record DoctorPracticeBrandingResponse(
    Guid Id,
    Guid DoctorPracticeId,
    bool HasLogo,
    string? PrimaryColor,
    string? SecondaryColor,
    string? BackgroundColor,
    string? TextColor,
    string RowVersion);

public sealed record GetDoctorPracticeBrandingQuery(Guid PracticeId) : IQuery<DoctorPracticeBrandingResponse>;
public sealed record GetDoctorPracticeLogoQuery(Guid PracticeId) : IQuery<PrivateMedia>;
public sealed record UpdateDoctorPracticeBrandingCommand(
    Guid PracticeId,
    string? PrimaryColor,
    string? SecondaryColor,
    string? BackgroundColor,
    string? TextColor,
    string RowVersion)
    : ICommand<DoctorPracticeBrandingResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record ReplaceDoctorPracticeLogoCommand(Guid PracticeId, MediaUpload Logo, string RowVersion)
    : ICommand<DoctorPracticeBrandingResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record RemoveDoctorPracticeLogoCommand(Guid PracticeId, string RowVersion)
    : ICommand<DoctorPracticeBrandingResponse>, ITransactionalCommand<WaslaWritePersistence>;

internal sealed class CreateDoctorPracticeCommandValidator : AbstractValidator<CreateDoctorPracticeCommand>
{
    public CreateDoctorPracticeCommandValidator()
    {
        RuleFor(command => command.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(command => command.NameEn).MaximumLength(200);
        RuleFor(command => command.GovernorateId).GreaterThan(0);
        RuleFor(command => command.CityId).GreaterThan(0);
        RuleFor(command => command.AreaId).GreaterThan(0);
        RuleFor(command => command.DetailedAddress).NotEmpty().MaximumLength(500);
        RuleFor(command => command.Latitude).InclusiveBetween(-90, 90);
        RuleFor(command => command.Longitude).InclusiveBetween(-180, 180);
    }
}

internal sealed class UpdateDoctorPracticeCommandValidator : AbstractValidator<UpdateDoctorPracticeCommand>
{
    public UpdateDoctorPracticeCommandValidator()
    {
        RuleFor(command => command.PracticeId).NotEmpty();
        RuleFor(command => command.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(command => command.NameEn).MaximumLength(200);
        RuleFor(command => command.GovernorateId).GreaterThan(0);
        RuleFor(command => command.CityId).GreaterThan(0);
        RuleFor(command => command.AreaId).GreaterThan(0);
        RuleFor(command => command.DetailedAddress).NotEmpty().MaximumLength(500);
        RuleFor(command => command.Latitude).InclusiveBetween(-90, 90);
        RuleFor(command => command.Longitude).InclusiveBetween(-180, 180);
        RuleFor(command => command.RowVersion).Must(RowVersionCodec.IsValid);
    }
}

internal sealed class DoctorPracticeIdQueryValidator : AbstractValidator<GetMyDoctorPracticeQuery>
{
    public DoctorPracticeIdQueryValidator() => RuleFor(query => query.PracticeId).NotEmpty();
}

internal sealed class ActivateDoctorPracticeCommandValidator : AbstractValidator<ActivateDoctorPracticeCommand>
{
    public ActivateDoctorPracticeCommandValidator()
    {
        RuleFor(command => command.PracticeId).NotEmpty();
        RuleFor(command => command.RowVersion).Must(RowVersionCodec.IsValid);
    }
}

internal sealed class DeactivateDoctorPracticeCommandValidator : AbstractValidator<DeactivateDoctorPracticeCommand>
{
    public DeactivateDoctorPracticeCommandValidator()
    {
        RuleFor(command => command.PracticeId).NotEmpty();
        RuleFor(command => command.RowVersion).Must(RowVersionCodec.IsValid);
    }
}

internal sealed class UpdateDoctorPracticeConfigurationCommandValidator
    : AbstractValidator<UpdateDoctorPracticeConfigurationCommand>
{
    public UpdateDoctorPracticeConfigurationCommandValidator()
    {
        RuleFor(command => command.PracticeId).NotEmpty();
        RuleFor(command => command.DefaultSlotDurationMinutes).InclusiveBetween(5, 480);
        RuleFor(command => command.CheckInGracePeriodMinutes).InclusiveBetween(0, 1440);
        RuleFor(command => command.PatientSelfCancellationCutoffMinutes).InclusiveBetween(0, 43200);
        RuleFor(command => command.MaximumDailyPatients).InclusiveBetween(1, 10000)
            .When(command => command.MaximumDailyPatients.HasValue);
        RuleFor(command => command.MaximumTicketCallAttempts).InclusiveBetween(1, 100);
        RuleFor(command => command.TimeZoneId).NotEmpty().MaximumLength(100);
        RuleFor(command => command.RowVersion).Must(RowVersionCodec.IsValid);
    }
}

internal sealed class UpdateDoctorPracticeBrandingCommandValidator
    : AbstractValidator<UpdateDoctorPracticeBrandingCommand>
{
    public UpdateDoctorPracticeBrandingCommandValidator()
    {
        RuleFor(command => command.PracticeId).NotEmpty();
        RuleFor(command => command.PrimaryColor).MaximumLength(7);
        RuleFor(command => command.SecondaryColor).MaximumLength(7);
        RuleFor(command => command.BackgroundColor).MaximumLength(7);
        RuleFor(command => command.TextColor).MaximumLength(7);
        RuleFor(command => command.RowVersion).Must(RowVersionCodec.IsValid);
    }
}

internal sealed class ReplaceDoctorPracticeLogoCommandValidator
    : AbstractValidator<ReplaceDoctorPracticeLogoCommand>
{
    public ReplaceDoctorPracticeLogoCommandValidator()
    {
        RuleFor(command => command.PracticeId).NotEmpty();
        RuleFor(command => command.Logo).NotNull();
        RuleFor(command => command.RowVersion).Must(RowVersionCodec.IsValid);
    }
}

internal sealed class RemoveDoctorPracticeLogoCommandValidator
    : AbstractValidator<RemoveDoctorPracticeLogoCommand>
{
    public RemoveDoctorPracticeLogoCommandValidator()
    {
        RuleFor(command => command.PracticeId).NotEmpty();
        RuleFor(command => command.RowVersion).Must(RowVersionCodec.IsValid);
    }
}

internal sealed class ListMyDoctorPracticesQueryHandler(IWaslaDataStore dataStore, ICurrentUser currentUser)
    : IQueryHandler<ListMyDoctorPracticesQuery, IReadOnlyList<DoctorPracticeResponse>>
{
    public async Task<Result<IReadOnlyList<DoctorPracticeResponse>>> Handle(
        ListMyDoctorPracticesQuery request,
        CancellationToken cancellationToken)
    {
        var doctor = await DoctorPracticeAccess.ResolveDoctorAsync(dataStore, currentUser, cancellationToken);
        if (doctor.IsFailure)
        {
            return Result<IReadOnlyList<DoctorPracticeResponse>>.Fail(doctor.Errors);
        }

        var practices = await dataStore.ListDoctorPracticesAsync(doctor.Value.Doctor.Id, cancellationToken);
        var responses = new List<DoctorPracticeResponse>(practices.Count);
        foreach (var practice in practices)
        {
            var branding = await dataStore.FindDoctorPracticeBrandingAsync(practice.Practice.Id, cancellationToken);
            responses.Add(DoctorPracticeMapper.Map(practice, branding?.LogoMediaKey is not null));
        }

        return Result<IReadOnlyList<DoctorPracticeResponse>>.Ok(responses);
    }
}

internal sealed class GetMyDoctorPracticeQueryHandler(IWaslaDataStore dataStore, ICurrentUser currentUser)
    : IQueryHandler<GetMyDoctorPracticeQuery, DoctorPracticeResponse>
{
    public async Task<Result<DoctorPracticeResponse>> Handle(
        GetMyDoctorPracticeQuery request,
        CancellationToken cancellationToken)
    {
        var access = await DoctorPracticeAccess.LoadOwnedPracticeAsync(
            dataStore, currentUser, request.PracticeId, cancellationToken);
        if (access.IsFailure)
        {
            return Result<DoctorPracticeResponse>.Fail(access.Errors);
        }

        return await DoctorPracticeMapper.ReloadAsync(dataStore, request.PracticeId, cancellationToken);
    }
}

internal sealed class CreateDoctorPracticeCommandHandler(IWaslaDataStore dataStore, ICurrentUser currentUser)
    : ICommandHandler<CreateDoctorPracticeCommand, DoctorPracticeResponse>
{
    public async Task<Result<DoctorPracticeResponse>> Handle(
        CreateDoctorPracticeCommand request,
        CancellationToken cancellationToken)
    {
        var doctor = await DoctorPracticeAccess.ResolveDoctorAsync(dataStore, currentUser, cancellationToken);
        if (doctor.IsFailure)
        {
            return Result<DoctorPracticeResponse>.Fail(doctor.Errors);
        }

        var location = await DoctorPracticeAccess.ValidateLocationAsync(
            dataStore, request.GovernorateId, request.CityId, request.AreaId, cancellationToken);
        if (location.IsFailure)
        {
            return Result<DoctorPracticeResponse>.Fail(location.Errors);
        }

        var practiceId = Guid.NewGuid();
        var practice = DoctorPractice.Create(
            practiceId,
            doctor.Value.Doctor.Id,
            request.NameAr,
            request.NameEn,
            request.GovernorateId,
            request.CityId,
            request.AreaId,
            request.DetailedAddress,
            request.Latitude,
            request.Longitude,
            doctor.Value.ActorId);
        if (practice.IsFailure)
        {
            return Result<DoctorPracticeResponse>.Fail(practice.Errors);
        }

        dataStore.Add(practice.Value);
        DoctorPracticeCreation.AddDefaults(dataStore, practiceId, doctor.Value.ActorId);
        await dataStore.SaveChangesAsync(cancellationToken);
        return await DoctorPracticeMapper.ReloadAsync(dataStore, practiceId, cancellationToken);
    }
}

internal sealed class UpdateDoctorPracticeCommandHandler(IWaslaDataStore dataStore, ICurrentUser currentUser)
    : ICommandHandler<UpdateDoctorPracticeCommand, DoctorPracticeResponse>
{
    public async Task<Result<DoctorPracticeResponse>> Handle(
        UpdateDoctorPracticeCommand request,
        CancellationToken cancellationToken)
    {
        var access = await DoctorPracticeAccess.LoadOwnedPracticeAsync(
            dataStore, currentUser, request.PracticeId, cancellationToken);
        if (access.IsFailure)
        {
            return Result<DoctorPracticeResponse>.Fail(access.Errors);
        }

        var location = await DoctorPracticeAccess.ValidateLocationAsync(
            dataStore, request.GovernorateId, request.CityId, request.AreaId, cancellationToken);
        if (location.IsFailure)
        {
            return Result<DoctorPracticeResponse>.Fail(location.Errors);
        }

        var supplied = DoctorPracticeAccess.VerifyRowVersion(
            access.Value.Practice.RowVersion, request.RowVersion, DoctorPracticeErrors.ConcurrencyConflict);
        if (supplied.IsFailure)
        {
            return Result<DoctorPracticeResponse>.Fail(supplied.Errors);
        }

        var updated = access.Value.Practice.Update(
            request.NameAr,
            request.NameEn,
            request.GovernorateId,
            request.CityId,
            request.AreaId,
            request.DetailedAddress,
            request.Latitude,
            request.Longitude,
            access.Value.ActorId);
        if (updated.IsFailure)
        {
            return Result<DoctorPracticeResponse>.Fail(updated.Errors);
        }

        dataStore.SetOriginalRowVersion(access.Value.Practice, supplied.Value);
        await dataStore.SaveChangesAsync(cancellationToken);
        return await DoctorPracticeMapper.ReloadAsync(dataStore, request.PracticeId, cancellationToken);
    }
}

internal sealed class ActivateDoctorPracticeCommandHandler(IWaslaDataStore dataStore, ICurrentUser currentUser)
    : ICommandHandler<ActivateDoctorPracticeCommand, DoctorPracticeResponse>
{
    public async Task<Result<DoctorPracticeResponse>> Handle(
        ActivateDoctorPracticeCommand request,
        CancellationToken cancellationToken)
    {
        var access = await DoctorPracticeAccess.LoadOwnedPracticeAsync(
            dataStore, currentUser, request.PracticeId, cancellationToken);
        if (access.IsFailure)
        {
            return Result<DoctorPracticeResponse>.Fail(access.Errors);
        }

        var supplied = DoctorPracticeAccess.VerifyRowVersion(
            access.Value.Practice.RowVersion, request.RowVersion, DoctorPracticeErrors.ConcurrencyConflict);
        if (supplied.IsFailure)
        {
            return Result<DoctorPracticeResponse>.Fail(supplied.Errors);
        }

        var configuration = await dataStore.FindDoctorPracticeConfigurationAsync(request.PracticeId, cancellationToken);
        var branding = await dataStore.FindDoctorPracticeBrandingAsync(request.PracticeId, cancellationToken);
        var activated = access.Value.Practice.Activate(
            configuration is not null,
            branding is not null,
            branding?.LogoMediaKey is not null,
            access.Value.ActorId);
        if (activated.IsFailure)
        {
            return Result<DoctorPracticeResponse>.Fail(activated.Errors);
        }

        dataStore.SetOriginalRowVersion(access.Value.Practice, supplied.Value);
        await dataStore.SaveChangesAsync(cancellationToken);
        return await DoctorPracticeMapper.ReloadAsync(dataStore, request.PracticeId, cancellationToken);
    }
}

internal sealed class DeactivateDoctorPracticeCommandHandler(IWaslaDataStore dataStore, ICurrentUser currentUser)
    : ICommandHandler<DeactivateDoctorPracticeCommand, DoctorPracticeResponse>
{
    public async Task<Result<DoctorPracticeResponse>> Handle(
        DeactivateDoctorPracticeCommand request,
        CancellationToken cancellationToken)
    {
        var access = await DoctorPracticeAccess.LoadOwnedPracticeAsync(
            dataStore, currentUser, request.PracticeId, cancellationToken);
        if (access.IsFailure)
        {
            return Result<DoctorPracticeResponse>.Fail(access.Errors);
        }

        var supplied = DoctorPracticeAccess.VerifyRowVersion(
            access.Value.Practice.RowVersion, request.RowVersion, DoctorPracticeErrors.ConcurrencyConflict);
        if (supplied.IsFailure)
        {
            return Result<DoctorPracticeResponse>.Fail(supplied.Errors);
        }

        var deactivated = access.Value.Practice.Deactivate(access.Value.ActorId);
        if (deactivated.IsFailure)
        {
            return Result<DoctorPracticeResponse>.Fail(deactivated.Errors);
        }

        dataStore.SetOriginalRowVersion(access.Value.Practice, supplied.Value);
        await dataStore.SaveChangesAsync(cancellationToken);
        return await DoctorPracticeMapper.ReloadAsync(dataStore, request.PracticeId, cancellationToken);
    }
}

internal sealed class GetDoctorPracticeConfigurationQueryHandler(IWaslaDataStore dataStore, ICurrentUser currentUser)
    : IQueryHandler<GetDoctorPracticeConfigurationQuery, DoctorPracticeConfigurationResponse>
{
    public async Task<Result<DoctorPracticeConfigurationResponse>> Handle(
        GetDoctorPracticeConfigurationQuery request,
        CancellationToken cancellationToken)
    {
        var access = await DoctorPracticeAccess.LoadOwnedPracticeAsync(
            dataStore, currentUser, request.PracticeId, cancellationToken);
        if (access.IsFailure)
        {
            return Result<DoctorPracticeConfigurationResponse>.Fail(access.Errors);
        }

        var configuration = await dataStore.FindDoctorPracticeConfigurationAsync(request.PracticeId, cancellationToken);
        return configuration is null
            ? Result<DoctorPracticeConfigurationResponse>.Fail(DoctorPracticeConfigurationErrors.NotFound)
            : Result<DoctorPracticeConfigurationResponse>.Ok(DoctorPracticeMapper.Map(configuration));
    }
}

internal sealed class UpdateDoctorPracticeConfigurationCommandHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser)
    : ICommandHandler<UpdateDoctorPracticeConfigurationCommand, DoctorPracticeConfigurationResponse>
{
    public async Task<Result<DoctorPracticeConfigurationResponse>> Handle(
        UpdateDoctorPracticeConfigurationCommand request,
        CancellationToken cancellationToken)
    {
        var access = await DoctorPracticeAccess.LoadOwnedPracticeAsync(
            dataStore, currentUser, request.PracticeId, cancellationToken);
        if (access.IsFailure)
        {
            return Result<DoctorPracticeConfigurationResponse>.Fail(access.Errors);
        }

        var configuration = await dataStore.FindDoctorPracticeConfigurationAsync(request.PracticeId, cancellationToken);
        if (configuration is null)
        {
            return Result<DoctorPracticeConfigurationResponse>.Fail(DoctorPracticeConfigurationErrors.NotFound);
        }

        var supplied = DoctorPracticeAccess.VerifyRowVersion(
            configuration.RowVersion, request.RowVersion, DoctorPracticeConfigurationErrors.ConcurrencyConflict);
        if (supplied.IsFailure)
        {
            return Result<DoctorPracticeConfigurationResponse>.Fail(supplied.Errors);
        }

        var updated = configuration.Update(
            request.AllowOnlineBooking,
            request.AllowWalkIn,
            request.DefaultSlotDurationMinutes,
            request.CheckInGracePeriodMinutes,
            request.PatientSelfCancellationCutoffMinutes,
            request.MaximumDailyPatients,
            request.MaximumTicketCallAttempts,
            request.TimeZoneId,
            access.Value.ActorId);
        if (updated.IsFailure)
        {
            return Result<DoctorPracticeConfigurationResponse>.Fail(updated.Errors);
        }

        dataStore.SetOriginalRowVersion(configuration, supplied.Value);
        await dataStore.SaveChangesAsync(cancellationToken);
        return Result<DoctorPracticeConfigurationResponse>.Ok(DoctorPracticeMapper.Map(configuration));
    }
}

internal sealed class GetDoctorPracticeBrandingQueryHandler(IWaslaDataStore dataStore, ICurrentUser currentUser)
    : IQueryHandler<GetDoctorPracticeBrandingQuery, DoctorPracticeBrandingResponse>
{
    public async Task<Result<DoctorPracticeBrandingResponse>> Handle(
        GetDoctorPracticeBrandingQuery request,
        CancellationToken cancellationToken)
    {
        var access = await DoctorPracticeAccess.LoadOwnedPracticeAsync(
            dataStore, currentUser, request.PracticeId, cancellationToken);
        if (access.IsFailure)
        {
            return Result<DoctorPracticeBrandingResponse>.Fail(access.Errors);
        }

        var branding = await dataStore.FindDoctorPracticeBrandingAsync(request.PracticeId, cancellationToken);
        return branding is null
            ? Result<DoctorPracticeBrandingResponse>.Fail(DoctorPracticeBrandingErrors.NotFound)
            : Result<DoctorPracticeBrandingResponse>.Ok(DoctorPracticeMapper.Map(branding));
    }
}

internal sealed class GetDoctorPracticeLogoQueryHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser,
    IPrivateMediaReader mediaReader)
    : IQueryHandler<GetDoctorPracticeLogoQuery, PrivateMedia>
{
    public async Task<Result<PrivateMedia>> Handle(
        GetDoctorPracticeLogoQuery request,
        CancellationToken cancellationToken)
    {
        var access = await DoctorPracticeAccess.LoadOwnedPracticeAsync(
            dataStore, currentUser, request.PracticeId, cancellationToken);
        if (access.IsFailure)
        {
            return Result<PrivateMedia>.Fail(access.Errors);
        }

        var branding = await dataStore.FindDoctorPracticeBrandingAsync(request.PracticeId, cancellationToken);
        if (branding?.LogoMediaKey is null)
        {
            return Result<PrivateMedia>.Fail(DoctorPracticeBrandingErrors.LogoNotFound);
        }

        var media = await mediaReader.OpenAsync(branding.LogoMediaKey, cancellationToken);
        return media is null
            ? Result<PrivateMedia>.Fail(DoctorPracticeBrandingErrors.LogoNotFound)
            : Result<PrivateMedia>.Ok(media);
    }
}

internal sealed class UpdateDoctorPracticeBrandingCommandHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser)
    : ICommandHandler<UpdateDoctorPracticeBrandingCommand, DoctorPracticeBrandingResponse>
{
    public async Task<Result<DoctorPracticeBrandingResponse>> Handle(
        UpdateDoctorPracticeBrandingCommand request,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadBrandingAsync(
            dataStore, currentUser, request.PracticeId, request.RowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<DoctorPracticeBrandingResponse>.Fail(loaded.Errors);
        }

        var updated = loaded.Value.Branding.UpdateTheme(
            request.PrimaryColor,
            request.SecondaryColor,
            request.BackgroundColor,
            request.TextColor,
            loaded.Value.ActorId);
        if (updated.IsFailure)
        {
            return Result<DoctorPracticeBrandingResponse>.Fail(updated.Errors);
        }

        dataStore.SetOriginalRowVersion(loaded.Value.Branding, loaded.Value.Supplied);
        await dataStore.SaveChangesAsync(cancellationToken);
        return Result<DoctorPracticeBrandingResponse>.Ok(DoctorPracticeMapper.Map(loaded.Value.Branding));
    }

    internal static async Task<Result<(DoctorPractice Practice, DoctorPracticeBranding Branding, Guid ActorId, byte[] Supplied)>>
        LoadBrandingAsync(
            IWaslaDataStore dataStore,
            ICurrentUser currentUser,
            Guid practiceId,
            string rowVersion,
            CancellationToken cancellationToken)
    {
        var access = await DoctorPracticeAccess.LoadOwnedPracticeAsync(
            dataStore, currentUser, practiceId, cancellationToken);
        if (access.IsFailure)
        {
            return Result<(DoctorPractice, DoctorPracticeBranding, Guid, byte[])>.Fail(access.Errors);
        }

        var branding = await dataStore.FindDoctorPracticeBrandingAsync(practiceId, cancellationToken);
        if (branding is null)
        {
            return Result<(DoctorPractice, DoctorPracticeBranding, Guid, byte[])>.Fail(
                DoctorPracticeBrandingErrors.NotFound);
        }

        var supplied = DoctorPracticeAccess.VerifyRowVersion(
            branding.RowVersion, rowVersion, DoctorPracticeBrandingErrors.ConcurrencyConflict);
        return supplied.IsFailure
            ? Result<(DoctorPractice, DoctorPracticeBranding, Guid, byte[])>.Fail(supplied.Errors)
            : Result<(DoctorPractice, DoctorPracticeBranding, Guid, byte[])>.Ok((
                access.Value.Practice, branding, access.Value.ActorId, supplied.Value));
    }
}

internal sealed class ReplaceDoctorPracticeLogoCommandHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser,
    IMediaService mediaService)
    : ICommandHandler<ReplaceDoctorPracticeLogoCommand, DoctorPracticeBrandingResponse>
{
    public async Task<Result<DoctorPracticeBrandingResponse>> Handle(
        ReplaceDoctorPracticeLogoCommand request,
        CancellationToken cancellationToken)
    {
        var loaded = await UpdateDoctorPracticeBrandingCommandHandler.LoadBrandingAsync(
            dataStore, currentUser, request.PracticeId, request.RowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<DoctorPracticeBrandingResponse>.Fail(loaded.Errors);
        }

        string? newKey = null;
        var oldKey = loaded.Value.Branding.LogoMediaKey;
        try
        {
            newKey = (await mediaService.SaveAsync(
                request.Logo,
                new MediaStorageRequest($"DoctorPractices/{request.PracticeId}/Logo"),
                cancellationToken)).Key;
            var replaced = loaded.Value.Branding.ReplaceLogo(newKey, loaded.Value.ActorId);
            if (replaced.IsFailure)
            {
                await CleanupAsync(newKey);
                return Result<DoctorPracticeBrandingResponse>.Fail(replaced.Errors);
            }

            dataStore.SetOriginalRowVersion(loaded.Value.Branding, loaded.Value.Supplied);
            await dataStore.SaveChangesAsync(cancellationToken);
            if (oldKey is not null && !string.Equals(oldKey, newKey, StringComparison.Ordinal))
            {
                await CleanupAsync(oldKey);
            }

            return Result<DoctorPracticeBrandingResponse>.Ok(DoctorPracticeMapper.Map(loaded.Value.Branding));
        }
        catch
        {
            await CleanupAsync(newKey);
            throw;
        }

        async Task CleanupAsync(string? key)
        {
            if (key is null)
            {
                return;
            }

            try
            {
                await mediaService.DeleteAsync(key, CancellationToken.None);
            }
            catch
            {
                // Best-effort media compensation must not hide the original operation result.
            }
        }
    }
}

internal sealed class RemoveDoctorPracticeLogoCommandHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser,
    IMediaService mediaService)
    : ICommandHandler<RemoveDoctorPracticeLogoCommand, DoctorPracticeBrandingResponse>
{
    public async Task<Result<DoctorPracticeBrandingResponse>> Handle(
        RemoveDoctorPracticeLogoCommand request,
        CancellationToken cancellationToken)
    {
        var loaded = await UpdateDoctorPracticeBrandingCommandHandler.LoadBrandingAsync(
            dataStore, currentUser, request.PracticeId, request.RowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<DoctorPracticeBrandingResponse>.Fail(loaded.Errors);
        }

        var oldKey = loaded.Value.Branding.LogoMediaKey;
        var removed = loaded.Value.Branding.RemoveLogo(loaded.Value.Practice.IsActive, loaded.Value.ActorId);
        if (removed.IsFailure)
        {
            return Result<DoctorPracticeBrandingResponse>.Fail(removed.Errors);
        }

        dataStore.SetOriginalRowVersion(loaded.Value.Branding, loaded.Value.Supplied);
        await dataStore.SaveChangesAsync(cancellationToken);
        if (oldKey is not null)
        {
            try
            {
                await mediaService.DeleteAsync(oldKey, CancellationToken.None);
            }
            catch
            {
                // The database is authoritative; orphan cleanup can be retried independently.
            }
        }

        return Result<DoctorPracticeBrandingResponse>.Ok(DoctorPracticeMapper.Map(loaded.Value.Branding));
    }
}

internal static class DoctorPracticeAccess
{
    public static async Task<Result<(Doctor Doctor, Guid ActorId)>> ResolveDoctorAsync(
        IWaslaDataStore dataStore,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } actorId)
        {
            return Result<(Doctor, Guid)>.Fail(Error.Unauthorized(
                "Auth.AuthenticationRequired", ErrorMessage.AuthenticationRequired));
        }

        var doctor = await dataStore.FindDoctorByUserIdAsync(actorId, cancellationToken);
        if (doctor is null)
        {
            return Result<(Doctor, Guid)>.Fail(DoctorErrors.NotFound);
        }

        return doctor.ApprovalStatus == DoctorApprovalStatus.Approved
            ? Result<(Doctor, Guid)>.Ok((doctor, actorId))
            : Result<(Doctor, Guid)>.Fail(Error.Security(
                "Doctor.PracticeManagementUnavailable", ErrorMessage.DoctorOnboardingUnavailable));
    }

    public static async Task<Result<(Doctor Doctor, DoctorPractice Practice, Guid ActorId)>> LoadOwnedPracticeAsync(
        IWaslaDataStore dataStore,
        ICurrentUser currentUser,
        Guid practiceId,
        CancellationToken cancellationToken)
    {
        var doctor = await ResolveDoctorAsync(dataStore, currentUser, cancellationToken);
        if (doctor.IsFailure)
        {
            return Result<(Doctor, DoctorPractice, Guid)>.Fail(doctor.Errors);
        }

        var practice = await dataStore.FindDoctorPracticeAsync(practiceId, cancellationToken);
        if (practice is null)
        {
            return Result<(Doctor, DoctorPractice, Guid)>.Fail(DoctorPracticeErrors.NotFound);
        }

        return practice.DoctorId == doctor.Value.Doctor.Id
            ? Result<(Doctor, DoctorPractice, Guid)>.Ok((doctor.Value.Doctor, practice, doctor.Value.ActorId))
            : Result<(Doctor, DoctorPractice, Guid)>.Fail(DoctorPracticeErrors.NotOwned);
    }

    public static async Task<Result> ValidateLocationAsync(
        IWaslaDataStore dataStore,
        int governorateId,
        int cityId,
        int areaId,
        CancellationToken cancellationToken)
    {
        var hierarchy = await dataStore.FindLocationHierarchyAsync(areaId, cancellationToken);
        if (hierarchy is null)
        {
            return Result.Fail(LocationErrors.AreaNotFound);
        }

        if (hierarchy.CityId != cityId || hierarchy.GovernorateId != governorateId)
        {
            return Result.Fail(LocationErrors.InvalidHierarchy);
        }

        return hierarchy.AreaIsActive && hierarchy.CityIsActive && hierarchy.GovernorateIsActive
            ? Result.Ok()
            : Result.Fail(LocationErrors.Inactive);
    }

    public static Result<byte[]> VerifyRowVersion(byte[] actual, string encoded, Error concurrencyError)
    {
        var supplied = RowVersionCodec.Decode(encoded);
        return supplied is not null && actual.AsSpan().SequenceEqual(supplied)
            ? Result<byte[]>.Ok(supplied)
            : Result<byte[]>.Fail(concurrencyError);
    }
}

internal static class DoctorPracticeCreation
{
    public static void AddDefaults(IWaslaDataStore dataStore, Guid practiceId, Guid actorId)
    {
        dataStore.Add(DoctorPracticeConfiguration.CreateDefault(Guid.NewGuid(), practiceId, actorId).Value);
        dataStore.Add(DoctorPracticeBranding.CreateDefault(Guid.NewGuid(), practiceId, actorId).Value);
        dataStore.Add(DoctorPracticeSegment.CreateDefault(Guid.NewGuid(), practiceId, actorId).Value);
        dataStore.Add(DoctorPracticeVisitType.CreateDefault(
            Guid.NewGuid(), practiceId, DoctorPracticeVisitTypeCode.NewConsultation, actorId).Value);
        dataStore.Add(DoctorPracticeVisitType.CreateDefault(
            Guid.NewGuid(), practiceId, DoctorPracticeVisitTypeCode.FollowUp, actorId).Value);
    }
}

internal static class DoctorPracticeMapper
{
    public static DoctorPracticeResponse Map(DoctorPracticeViewRecord item, bool hasLogo)
        => new(
            item.Practice.Id,
            item.Practice.NameAr,
            item.Practice.NameEn,
            new PracticeLocationResponse(
                item.Governorate.Id,
                item.Governorate.NameAr,
                item.Governorate.NameEn,
                item.City.Id,
                item.City.NameAr,
                item.City.NameEn,
                item.Area.Id,
                item.Area.NameAr,
                item.Area.NameEn,
                item.Practice.DetailedAddress,
                item.Practice.Latitude,
                item.Practice.Longitude),
            item.Practice.IsActive,
            hasLogo,
            RowVersionCodec.Encode(item.Practice.RowVersion));

    public static DoctorPracticeConfigurationResponse Map(DoctorPracticeConfiguration item)
        => new(
            item.Id,
            item.DoctorPracticeId,
            item.AllowOnlineBooking,
            item.AllowWalkIn,
            item.DefaultSlotDurationMinutes,
            item.CheckInGracePeriodMinutes,
            item.PatientSelfCancellationCutoffMinutes,
            item.MaximumDailyPatients,
            item.MaximumTicketCallAttempts,
            item.TimeZoneId,
            RowVersionCodec.Encode(item.RowVersion));

    public static DoctorPracticeBrandingResponse Map(DoctorPracticeBranding item)
        => new(
            item.Id,
            item.DoctorPracticeId,
            item.LogoMediaKey is not null,
            item.PrimaryColor,
            item.SecondaryColor,
            item.BackgroundColor,
            item.TextColor,
            RowVersionCodec.Encode(item.RowVersion));

    public static async Task<Result<DoctorPracticeResponse>> ReloadAsync(
        IWaslaDataStore dataStore,
        Guid practiceId,
        CancellationToken cancellationToken)
    {
        var view = await dataStore.GetDoctorPracticeAsync(practiceId, cancellationToken);
        if (view is null)
        {
            return Result<DoctorPracticeResponse>.Fail(DoctorPracticeErrors.NotFound);
        }

        var branding = await dataStore.FindDoctorPracticeBrandingAsync(practiceId, cancellationToken);
        return Result<DoctorPracticeResponse>.Ok(Map(view, branding?.LogoMediaKey is not null));
    }
}
