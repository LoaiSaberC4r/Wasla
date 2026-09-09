using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Media;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Features.Doctors;
using Wasla.Application.Media;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Patients;
using Wasla.Domain.Resources;

namespace Wasla.Application.Features.Patients;

public sealed record PatientContactInput(
    string NameAr,
    string? NameEn,
    string PhoneNumber,
    PatientContactRelationshipType RelationshipType,
    Guid? LinkedPatientId,
    bool IsPrimary);

public sealed record CreateReceptionPatientCommand(
    string NameAr,
    string? NameEn,
    DateOnly DateOfBirth,
    Gender Gender,
    string? PhoneNumber,
    string? Email,
    MediaUpload? ProfileImage,
    PatientContactInput? PrimaryContact)
    : ICommand<CreateReceptionPatientResponse>, ITransactionalCommand<WaslaWritePersistence>;

public sealed record CreateReceptionPatientResponse(Guid PatientId);

public sealed record GetMyPatientProfileQuery : IQuery<PatientProfileResponse>;
public sealed record GetMyPatientProfileImageQuery : IQuery<PrivateMedia>;
public sealed record PatientProfileResponse(
    Guid PatientId,
    string NameAr,
    string? NameEn,
    DateOnly DateOfBirth,
    Gender Gender,
    string? PhoneNumber,
    string? Email,
    bool HasProfileImage,
    string RowVersion);

public sealed record UpdateMyPatientProfileCommand(
    string NameAr,
    string? NameEn,
    string? PhoneNumber,
    string? Email,
    MediaUpload? ProfileImage,
    string RowVersion)
    : ICommand<PatientProfileResponse>, ITransactionalCommand<WaslaWritePersistence>;

public sealed record GetMyPatientContactsQuery : IQuery<IReadOnlyList<PatientContactResponse>>;
public sealed record AddMyPatientContactCommand(PatientContactInput Contact)
    : ICommand<PatientContactResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record UpdateMyPatientContactCommand(Guid ContactId, PatientContactInput Contact)
    : ICommand<PatientContactResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record DeactivateMyPatientContactCommand(Guid ContactId)
    : ICommand, ITransactionalCommand<WaslaWritePersistence>;
public sealed record PatientContactResponse(
    Guid ContactId,
    string NameAr,
    string? NameEn,
    string PhoneNumber,
    PatientContactRelationshipType RelationshipType,
    Guid? LinkedPatientId,
    bool IsPrimary);

public sealed record SearchPatientsQuery(
    string? PhoneNumber,
    string? Name,
    DateOnly? DateOfBirth,
    int PageNumber = 1,
    int PageSize = 20) : IQuery<PagedResponse<PatientSearchResponse>>;
public sealed record PatientSearchResponse(
    Guid PatientId,
    string NameAr,
    string? NameEn,
    DateOnly DateOfBirth,
    Gender Gender,
    string? PhoneNumber,
    bool HasContactPhone);

internal sealed class PatientContactInputValidator : AbstractValidator<PatientContactInput>
{
    public PatientContactInputValidator()
    {
        RuleFor(item => item.NameAr).NotEmpty().WithMessage(ErrorMessage.NameArRequired).MaximumLength(200);
        RuleFor(item => item.NameEn).MaximumLength(200);
        RuleFor(item => item.PhoneNumber).NotEmpty().WithMessage(ErrorMessage.PhoneNumberRequired).MaximumLength(30);
        RuleFor(item => item.RelationshipType).IsInEnum();
        RuleFor(item => item.LinkedPatientId).NotEqual(Guid.Empty).When(item => item.LinkedPatientId.HasValue);
    }
}

internal sealed class CreateReceptionPatientCommandValidator : AbstractValidator<CreateReceptionPatientCommand>
{
    public CreateReceptionPatientCommandValidator(IDateTimeProvider clock)
    {
        RuleFor(command => command.NameAr).NotEmpty().WithMessage(ErrorMessage.NameArRequired).MaximumLength(200);
        RuleFor(command => command.NameEn).MaximumLength(200);
        RuleFor(command => command.DateOfBirth).NotEmpty().WithMessage(ErrorMessage.DateOfBirthRequired)
            .LessThanOrEqualTo(DateOnly.FromDateTime(clock.UtcNow)).WithMessage(ErrorMessage.DateOfBirthFuture);
        RuleFor(command => command.Gender).IsInEnum().Must(gender => gender is Gender.Male or Gender.Female).WithMessage(ErrorMessage.GenderInvalid);
        RuleFor(command => command.PhoneNumber).MaximumLength(30);
        RuleFor(command => command.Email).EmailAddress().WithMessage(ErrorMessage.EmailInvalid).MaximumLength(200)
            .When(command => !string.IsNullOrWhiteSpace(command.Email));
        RuleFor(command => command.PrimaryContact).SetValidator(new PatientContactInputValidator()!)
            .When(command => command.PrimaryContact is not null);
        RuleFor(command => command).Must(command => !string.IsNullOrWhiteSpace(command.PhoneNumber) ||
                                                    command.PrimaryContact is { IsPrimary: true } &&
                                                    !string.IsNullOrWhiteSpace(command.PrimaryContact.PhoneNumber))
            .WithMessage(ErrorMessage.PatientContactRequired);
    }
}

internal sealed class UpdateMyPatientProfileCommandValidator : AbstractValidator<UpdateMyPatientProfileCommand>
{
    public UpdateMyPatientProfileCommandValidator()
    {
        RuleFor(command => command.NameAr).NotEmpty().WithMessage(ErrorMessage.NameArRequired).MaximumLength(200);
        RuleFor(command => command.NameEn).MaximumLength(200);
        RuleFor(command => command.PhoneNumber).MaximumLength(30);
        RuleFor(command => command.Email).EmailAddress().WithMessage(ErrorMessage.EmailInvalid).MaximumLength(200)
            .When(command => !string.IsNullOrWhiteSpace(command.Email));
        RuleFor(command => command.RowVersion).NotEmpty().Must(RowVersionCodec.IsValid).WithMessage(ErrorMessage.InvalidRowVersion);
    }
}

internal sealed class AddMyPatientContactCommandValidator : AbstractValidator<AddMyPatientContactCommand>
{
    public AddMyPatientContactCommandValidator() => RuleFor(command => command.Contact).NotNull().SetValidator(new PatientContactInputValidator());
}

internal sealed class UpdateMyPatientContactCommandValidator : AbstractValidator<UpdateMyPatientContactCommand>
{
    public UpdateMyPatientContactCommandValidator()
    {
        RuleFor(command => command.ContactId).NotEmpty();
        RuleFor(command => command.Contact).NotNull().SetValidator(new PatientContactInputValidator());
    }
}

internal sealed class DeactivateMyPatientContactCommandValidator : AbstractValidator<DeactivateMyPatientContactCommand>
{
    public DeactivateMyPatientContactCommandValidator() => RuleFor(command => command.ContactId).NotEmpty();
}

internal sealed class SearchPatientsQueryValidator : AbstractValidator<SearchPatientsQuery>
{
    public SearchPatientsQueryValidator()
    {
        RuleFor(query => query.PhoneNumber).MaximumLength(30);
        RuleFor(query => query.Name).MaximumLength(200);
        RuleFor(query => query.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

internal sealed class CreateReceptionPatientCommandHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser,
    IMediaService mediaService,
    IDateTimeProvider clock)
    : ICommandHandler<CreateReceptionPatientCommand, CreateReceptionPatientResponse>
{
    public async Task<Result<CreateReceptionPatientResponse>> Handle(CreateReceptionPatientCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
        {
            return Result<CreateReceptionPatientResponse>.Fail(Error.Unauthorized("Auth.AuthenticationRequired", ErrorMessage.AuthenticationRequired));
        }

        if (request.PrimaryContact?.LinkedPatientId is { } linkedId &&
            await dataStore.FindPatientByIdAsync(linkedId, cancellationToken) is null)
        {
            return Result<CreateReceptionPatientResponse>.Fail(PatientErrors.ContactNotFound);
        }

        var patientId = Guid.NewGuid();
        string? storedKey = null;
        try
        {
            if (request.ProfileImage is not null)
            {
                storedKey = (await mediaService.SaveAsync(request.ProfileImage, new MediaStorageRequest($"Patients/{patientId}/Profile"), cancellationToken)).Key;
            }

            var patientResult = Patient.Create(
                patientId, request.NameAr, request.NameEn, request.DateOfBirth, request.Gender,
                request.PhoneNumber, request.Email, storedKey, null, null, DateOnly.FromDateTime(clock.UtcNow));
            if (patientResult.IsFailure)
            {
                await CleanupAsync(storedKey);
                return Result<CreateReceptionPatientResponse>.Fail(patientResult.Errors);
            }

            dataStore.Add(patientResult.Value);
            if (request.PrimaryContact is { } input)
            {
                var contactResult = PatientContact.Create(
                    Guid.NewGuid(), patientId, input.NameAr, input.NameEn, input.PhoneNumber,
                    input.RelationshipType, input.LinkedPatientId, input.IsPrimary);
                if (contactResult.IsFailure)
                {
                    await CleanupAsync(storedKey);
                    return Result<CreateReceptionPatientResponse>.Fail(contactResult.Errors);
                }
                dataStore.Add(contactResult.Value);
            }

            await dataStore.SaveChangesAsync(cancellationToken);
            return Result<CreateReceptionPatientResponse>.Ok(new(patientId));
        }
        catch
        {
            await CleanupAsync(storedKey);
            throw;
        }

        async Task CleanupAsync(string? key)
        {
            if (key is null) return;
            try { await mediaService.DeleteRangeAsync([key], CancellationToken.None); }
            catch { }
        }
    }
}

internal sealed class GetMyPatientProfileQueryHandler(IWaslaDataStore dataStore, ICurrentUser currentUser)
    : IQueryHandler<GetMyPatientProfileQuery, PatientProfileResponse>
{
    public async Task<Result<PatientProfileResponse>> Handle(GetMyPatientProfileQuery request, CancellationToken cancellationToken)
    {
        var patient = await PatientIdentity.ResolveAsync(dataStore, currentUser, cancellationToken);
        return patient.IsFailure
            ? Result<PatientProfileResponse>.Fail(patient.Errors)
            : Result<PatientProfileResponse>.Ok(PatientMapper.Profile(patient.Value));
    }
}

internal sealed class GetMyPatientProfileImageQueryHandler(IWaslaDataStore dataStore, ICurrentUser currentUser, IPrivateMediaReader mediaReader)
    : IQueryHandler<GetMyPatientProfileImageQuery, PrivateMedia>
{
    public async Task<Result<PrivateMedia>> Handle(GetMyPatientProfileImageQuery request, CancellationToken cancellationToken)
    {
        var patient = await PatientIdentity.ResolveAsync(dataStore, currentUser, cancellationToken);
        if (patient.IsFailure) return Result<PrivateMedia>.Fail(patient.Errors);
        if (patient.Value.ProfileImageMediaKey is null) return Result<PrivateMedia>.Fail(PatientErrors.MediaNotFound);
        var media = await mediaReader.OpenAsync(patient.Value.ProfileImageMediaKey, cancellationToken);
        return media is null ? Result<PrivateMedia>.Fail(PatientErrors.MediaNotFound) : Result<PrivateMedia>.Ok(media);
    }
}

internal sealed class UpdateMyPatientProfileCommandHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser,
    IMediaService mediaService)
    : ICommandHandler<UpdateMyPatientProfileCommand, PatientProfileResponse>
{
    public async Task<Result<PatientProfileResponse>> Handle(UpdateMyPatientProfileCommand request, CancellationToken cancellationToken)
    {
        var patientResult = await PatientIdentity.ResolveAsync(dataStore, currentUser, cancellationToken);
        if (patientResult.IsFailure) return Result<PatientProfileResponse>.Fail(patientResult.Errors);
        var patient = patientResult.Value;
        var supplied = RowVersionCodec.Decode(request.RowVersion);
        if (supplied is null || !patient.RowVersion.AsSpan().SequenceEqual(supplied))
        {
            return Result<PatientProfileResponse>.Fail(PatientErrors.ConcurrencyConflict);
        }
        if (string.IsNullOrWhiteSpace(request.PhoneNumber) &&
            !await dataStore.HasUsablePrimaryContactAsync(patient.Id, null, cancellationToken))
        {
            return Result<PatientProfileResponse>.Fail(PatientErrors.ContactRequired);
        }

        string? newKey = null;
        try
        {
            if (request.ProfileImage is not null)
            {
                newKey = (await mediaService.SaveAsync(request.ProfileImage, new MediaStorageRequest($"Patients/{patient.Id}/Profile"), cancellationToken)).Key;
            }
            var updated = patient.UpdateProfile(request.NameAr, request.NameEn, request.PhoneNumber, request.Email, newKey, request.ProfileImage is not null);
            if (updated.IsFailure)
            {
                await CleanupAsync();
                return Result<PatientProfileResponse>.Fail(updated.Errors);
            }
            dataStore.SetOriginalRowVersion(patient, supplied);
            await dataStore.SaveChangesAsync(cancellationToken);
            return Result<PatientProfileResponse>.Ok(PatientMapper.Profile(patient));
        }
        catch
        {
            await CleanupAsync();
            throw;
        }

        async Task CleanupAsync()
        {
            if (newKey is null) return;
            try { await mediaService.DeleteRangeAsync([newKey], CancellationToken.None); }
            catch { }
        }
    }
}

internal sealed class GetMyPatientContactsQueryHandler(IWaslaDataStore dataStore, ICurrentUser currentUser)
    : IQueryHandler<GetMyPatientContactsQuery, IReadOnlyList<PatientContactResponse>>
{
    public async Task<Result<IReadOnlyList<PatientContactResponse>>> Handle(GetMyPatientContactsQuery request, CancellationToken cancellationToken)
    {
        var patient = await PatientIdentity.ResolveAsync(dataStore, currentUser, cancellationToken);
        if (patient.IsFailure) return Result<IReadOnlyList<PatientContactResponse>>.Fail(patient.Errors);
        var contacts = await dataStore.ListPatientContactsAsync(patient.Value.Id, cancellationToken);
        return Result<IReadOnlyList<PatientContactResponse>>.Ok(contacts.Select(PatientMapper.Contact).ToArray());
    }
}

internal sealed class AddMyPatientContactCommandHandler(IWaslaDataStore dataStore, ICurrentUser currentUser)
    : ICommandHandler<AddMyPatientContactCommand, PatientContactResponse>
{
    public async Task<Result<PatientContactResponse>> Handle(AddMyPatientContactCommand request, CancellationToken cancellationToken)
    {
        var patient = await PatientIdentity.ResolveAsync(dataStore, currentUser, cancellationToken);
        if (patient.IsFailure) return Result<PatientContactResponse>.Fail(patient.Errors);
        if (request.Contact.LinkedPatientId is { } linkedId && await dataStore.FindPatientByIdAsync(linkedId, cancellationToken) is null)
            return Result<PatientContactResponse>.Fail(PatientErrors.ContactNotFound);
        var input = request.Contact;
        var created = PatientContact.Create(Guid.NewGuid(), patient.Value.Id, input.NameAr, input.NameEn, input.PhoneNumber, input.RelationshipType, input.LinkedPatientId, input.IsPrimary);
        if (created.IsFailure) return Result<PatientContactResponse>.Fail(created.Errors);
        dataStore.Add(created.Value);
        await dataStore.SaveChangesAsync(cancellationToken);
        return Result<PatientContactResponse>.Ok(PatientMapper.Contact(created.Value));
    }
}

internal sealed class UpdateMyPatientContactCommandHandler(IWaslaDataStore dataStore, ICurrentUser currentUser)
    : ICommandHandler<UpdateMyPatientContactCommand, PatientContactResponse>
{
    public async Task<Result<PatientContactResponse>> Handle(UpdateMyPatientContactCommand request, CancellationToken cancellationToken)
    {
        var patientResult = await PatientIdentity.ResolveAsync(dataStore, currentUser, cancellationToken);
        if (patientResult.IsFailure) return Result<PatientContactResponse>.Fail(patientResult.Errors);
        var patient = patientResult.Value;
        var contact = await dataStore.FindPatientContactAsync(patient.Id, request.ContactId, cancellationToken);
        if (contact is null) return Result<PatientContactResponse>.Fail(PatientErrors.ContactNotFound);
        if (request.Contact.LinkedPatientId is { } linkedId && await dataStore.FindPatientByIdAsync(linkedId, cancellationToken) is null)
            return Result<PatientContactResponse>.Fail(PatientErrors.ContactNotFound);
        if (string.IsNullOrWhiteSpace(patient.PhoneNumber) && contact.IsPrimary && !request.Contact.IsPrimary &&
            !await dataStore.HasUsablePrimaryContactAsync(patient.Id, contact.Id, cancellationToken))
            return Result<PatientContactResponse>.Fail(PatientErrors.LastContactRequired);
        var input = request.Contact;
        var updated = contact.Update(input.NameAr, input.NameEn, input.PhoneNumber, input.RelationshipType, input.LinkedPatientId, input.IsPrimary);
        if (updated.IsFailure) return Result<PatientContactResponse>.Fail(updated.Errors);
        await dataStore.SaveChangesAsync(cancellationToken);
        return Result<PatientContactResponse>.Ok(PatientMapper.Contact(contact));
    }
}

internal sealed class DeactivateMyPatientContactCommandHandler(IWaslaDataStore dataStore, ICurrentUser currentUser)
    : ICommandHandler<DeactivateMyPatientContactCommand>
{
    public async Task<Result> Handle(DeactivateMyPatientContactCommand request, CancellationToken cancellationToken)
    {
        var patientResult = await PatientIdentity.ResolveAsync(dataStore, currentUser, cancellationToken);
        if (patientResult.IsFailure) return Result.Fail(patientResult.Errors);
        var patient = patientResult.Value;
        var contact = await dataStore.FindPatientContactAsync(patient.Id, request.ContactId, cancellationToken);
        if (contact is null) return Result.Fail(PatientErrors.ContactNotFound);
        if (string.IsNullOrWhiteSpace(patient.PhoneNumber) && contact.IsPrimary &&
            !await dataStore.HasUsablePrimaryContactAsync(patient.Id, contact.Id, cancellationToken))
            return Result.Fail(PatientErrors.LastContactRequired);
        dataStore.Remove(contact);
        await dataStore.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }
}

internal sealed class SearchPatientsQueryHandler(IWaslaDataStore dataStore)
    : IQueryHandler<SearchPatientsQuery, PagedResponse<PatientSearchResponse>>
{
    public async Task<Result<PagedResponse<PatientSearchResponse>>> Handle(SearchPatientsQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await dataStore.SearchPatientsAsync(request.PhoneNumber, request.Name, request.DateOfBirth, request.PageNumber, request.PageSize, cancellationToken);
        return Result<PagedResponse<PatientSearchResponse>>.Ok(new(
            items.Select(item => new PatientSearchResponse(item.PatientId, item.NameAr, item.NameEn, item.DateOfBirth, item.Gender, item.PhoneNumber, item.HasContactPhone)).ToArray(),
            total, request.PageNumber, request.PageSize));
    }
}

internal static class PatientIdentity
{
    public static async Task<Result<Patient>> ResolveAsync(IWaslaDataStore dataStore, ICurrentUser currentUser, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
            return Result<Patient>.Fail(Error.Unauthorized("Auth.AuthenticationRequired", ErrorMessage.AuthenticationRequired));
        var patient = await dataStore.FindPatientByUserIdAsync(userId, cancellationToken);
        return patient is null ? Result<Patient>.Fail(PatientErrors.AccountLinkNotFound) : Result<Patient>.Ok(patient);
    }
}

internal static class PatientMapper
{
    public static PatientProfileResponse Profile(Patient patient)
        => new(patient.Id, patient.NameAr, patient.NameEn, patient.DateOfBirth, patient.Gender, patient.PhoneNumber,
            patient.Email, patient.ProfileImageMediaKey is not null, RowVersionCodec.Encode(patient.RowVersion));

    public static PatientContactResponse Contact(PatientContact contact)
        => new(contact.Id, contact.NameAr, contact.NameEn, contact.PhoneNumber, contact.RelationshipType, contact.LinkedPatientId, contact.IsPrimary);
}
