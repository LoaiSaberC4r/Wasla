using Wasla.Application.Features.PatientOnboarding;
using Wasla.Domain.Security;
using Wasla.Domain.Archives;
using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Media;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Features.Doctors;
using Wasla.Application.Features.Practices;
using Wasla.Application.Media;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Patients;
using Wasla.Domain.Practices;
using Wasla.Domain.Resources;

namespace Wasla.Application.Features.Patients;

public sealed record PatientContactInput(
    string NameAr,
    string? NameEn,
    string PhoneNumber,
    PatientContactRelationshipType RelationshipType,
    Guid? LinkedPatientId,
    bool IsPrimary, string? ContactEmail = null);

public sealed record CreateReceptionPatientCommand(
    string NameAr,
    string? NameEn,
    DateOnly DateOfBirth,
    Gender Gender,
    string? PhoneNumber,
    string? Email,
    MediaUpload? ProfileImage,
    PatientContactInput? PrimaryContact,
    Guid? DoctorPracticeId = null, string? MatchReviewOutcome = null, string? MatchReviewReason = null,
    PatientIdentityVerificationBasis? MatchingAttestationBasis = null, string? IdempotencyKey = null)
    : ICommand<CreateReceptionPatientResponse>, ITransactionalCommand<WaslaWritePersistence>;

public sealed record CreateReceptionPatientResponse(Guid PatientId, string ProfileType = "Basic", bool AccountLinked = false);

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
    string RowVersion, string AccountStatus = "Active", bool HasVerifiedIdentityEmail = true, string? IdentityEmailMasked = null);

public sealed record UpdateMyPatientProfileCommand(
    string NameAr,
    string? NameEn,
    string? PhoneNumber,
    string? Email,
    MediaUpload? ProfileImage,
    string RowVersion, string? EmailVerificationToken = null)
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
    bool IsPrimary, string? ContactEmail = null);

public sealed record SearchPatientsQuery(
    string? PhoneNumber,
    string? Name,
    DateOnly? DateOfBirth,
    int PageNumber = 1,
    int PageSize = 20,
    Guid? DoctorPracticeId = null, string? Email = null) : IQuery<PagedResponse<PatientSearchResponse>>;
public sealed record PatientSearchResponse(
    Guid PatientId,
    string NameAr,
    string? NameEn,
    DateOnly DateOfBirth,
    Gender Gender,
    string? PhoneNumberMasked,
    bool HasContactPhone, string? EmailMasked = null);

internal sealed class PatientContactInputValidator : AbstractValidator<PatientContactInput>
{
    public PatientContactInputValidator()
    {
        RuleFor(item => item.NameAr).NotEmpty().WithMessage(ErrorMessage.NameArRequired).MaximumLength(200);
        RuleFor(item => item.NameEn).MaximumLength(200);
        RuleFor(item => item.PhoneNumber).NotEmpty().WithMessage(ErrorMessage.PhoneNumberRequired).MaximumLength(30);
        RuleFor(item => item.ContactEmail).MaximumLength(200).EmailAddress().When(item => item.ContactEmail is not null);
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
        RuleFor(query => query).Must(q => PatientIdentityWorkflow.BoundedSearch(q.Email, q.PhoneNumber, q.Name, q.DateOfBirth));
        RuleFor(query => query.Email).MaximumLength(200).EmailAddress().When(q => q.Email is not null);
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
    IDateTimeProvider clock,
    IReceptionPracticeAuthorizationService practiceAuthorization, IPatientIdentityStore identityStore, PatientIdentityWorkflow identityWorkflow, IPatientIdentityMediaCompensation compensation)
    : ICommandHandler<CreateReceptionPatientCommand, CreateReceptionPatientResponse>
{
    public async Task<Result<CreateReceptionPatientResponse>> Handle(CreateReceptionPatientCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
        {
            return Result<CreateReceptionPatientResponse>.Fail(Error.Unauthorized("Auth.AuthenticationRequired", ErrorMessage.AuthenticationRequired));
        }

        if (request.DoctorPracticeId is not { } practiceId)
        {
            return Result<CreateReceptionPatientResponse>.Fail(ReceptionErrors.AccessDenied);
        }

        var access = await practiceAuthorization.AuthorizeAsync(
            practiceId, Wasla.Domain.Security.PermissionNames.PatientsRegister, cancellationToken);
        if (access.IsFailure)
        {
            return Result<CreateReceptionPatientResponse>.Fail(access.Errors);
        }

        await identityStore.LockAsync(cancellationToken);
        var fingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
            System.Text.Json.JsonSerializer.Serialize(new { request.NameAr, request.NameEn, request.DateOfBirth, request.Gender, request.PhoneNumber, request.Email,
                request.PrimaryContact, request.DoctorPracticeId, request.MatchReviewOutcome, request.MatchReviewReason, request.MatchingAttestationBasis, ImageSha256 = await PatientIdentityUploadFingerprint.HashAsync(request.ProfileImage, cancellationToken) }))));
        if (request.IdempotencyKey is not null)
        {
            if (request.IdempotencyKey.Length > 100 || !Guid.TryParse(request.IdempotencyKey, out _)) return Result<CreateReceptionPatientResponse>.Fail(PatientIdentityErrors.Invalid);
            var memo = await identityStore.OperationAsync(currentUser.UserId.Value, "PatientIdentity:Basic", request.IdempotencyKey, cancellationToken);
            if (memo is not null) return memo.RequestFingerprint == fingerprint && memo.ResponseJson is not null
                ? Result<CreateReceptionPatientResponse>.Ok(System.Text.Json.JsonSerializer.Deserialize<CreateReceptionPatientResponse>(memo.ResponseJson)!)
                : Result<CreateReceptionPatientResponse>.Fail(PatientIdentityErrors.Conflict("PatientIdentity.IdempotencyKeyReused"));
        }

        if (PatientIdentityEmail.Normalize(request.Email) is { } email && await identityStore.PatientByEmailAsync(email, cancellationToken) is not null)
            return Result<CreateReceptionPatientResponse>.Fail(PatientIdentityErrors.Conflict("Patient.AlreadyExists"));
        var possible = await identityStore.MatchAsync(new(request.Email, request.PhoneNumber, request.NameAr, request.DateOfBirth, request.Gender, SecondaryPhoneNumber: request.PrimaryContact?.PhoneNumber), 1, 20, true, cancellationToken);
        if (possible.Total > 0 && (request.MatchReviewOutcome != "DistinctPerson" || string.IsNullOrWhiteSpace(request.MatchReviewReason) || request.MatchReviewReason.Length > 500 || request.MatchingAttestationBasis is null || !Enum.IsDefined(request.MatchingAttestationBasis.Value)))
            return Result<CreateReceptionPatientResponse>.Fail(PatientIdentityErrors.Conflict("Patient.IdentityConflict"));

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
                compensation.Track(storedKey);
            }

            var patientResult = Patient.Create(
                patientId, request.NameAr, request.NameEn, request.DateOfBirth, request.Gender,
                request.PhoneNumber, request.Email, storedKey, null, null, DateOnly.FromDateTime(clock.UtcNow));
            if (patientResult.IsFailure)
            {
                await CleanupAsync(storedKey);
                return Result<CreateReceptionPatientResponse>.Fail(patientResult.Errors);
            }

            patientResult.Value.StampReceptionRegistration(practiceId, currentUser.UserId.Value, clock.UtcNow);
            dataStore.Add(patientResult.Value);
            await identityWorkflow.OpenDuplicatesAsync(patientId, possible.Items, cancellationToken);
            if (possible.Total > 0) dataStore.Add(new PatientIdentityAttestation(Guid.NewGuid(), patientId, practiceId, currentUser.UserId.Value,
                request.MatchingAttestationBasis!.Value, PatientIdentityAttestationPurpose.SoftMatchOverride, clock.UtcNow, null, request.MatchReviewReason));
            if (request.PrimaryContact is { } input)
            {
                var contactResult = PatientContact.Create(
                    Guid.NewGuid(), patientId, input.NameAr, input.NameEn, input.PhoneNumber,
                    input.RelationshipType, input.LinkedPatientId, input.IsPrimary, input.ContactEmail);
                if (contactResult.IsFailure)
                {
                    await CleanupAsync(storedKey);
                    return Result<CreateReceptionPatientResponse>.Fail(contactResult.Errors);
                }
                dataStore.Add(contactResult.Value);
            }

            if (request.IdempotencyKey is not null)
            {
                var memo = Wasla.Domain.Medications.MedicationIdempotencyRecord.Create(currentUser.UserId.Value, "PatientIdentity:Basic", request.IdempotencyKey, fingerprint, clock.UtcNow);
                memo.Complete(System.Text.Json.JsonSerializer.Serialize(new CreateReceptionPatientResponse(patientId))); dataStore.Add(memo);
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
        var patient = await PatientIdentity.ResolveAsync(dataStore, currentUser, cancellationToken, Wasla.Domain.Security.PermissionNames.PatientProfileViewOwn);
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
        var patient = await PatientIdentity.ResolveAsync(dataStore, currentUser, cancellationToken, Wasla.Domain.Security.PermissionNames.PatientProfileViewOwn);
        if (patient.IsFailure) return Result<PrivateMedia>.Fail(patient.Errors);
        if (patient.Value.ProfileImageMediaKey is null) return Result<PrivateMedia>.Fail(PatientErrors.MediaNotFound);
        var media = await mediaReader.OpenAsync(patient.Value.ProfileImageMediaKey, cancellationToken);
        return media is null ? Result<PrivateMedia>.Fail(PatientErrors.MediaNotFound) : Result<PrivateMedia>.Ok(media);
    }
}

internal sealed class UpdateMyPatientProfileCommandHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser,
    IMediaService mediaService, IPatientIdentityStore identityStore, PatientIdentityWorkflow identityWorkflow, IDateTimeProvider clock, IPatientIdentityMediaCompensation compensation)
    : ICommandHandler<UpdateMyPatientProfileCommand, PatientProfileResponse>
{
    public async Task<Result<PatientProfileResponse>> Handle(UpdateMyPatientProfileCommand request, CancellationToken cancellationToken)
    {
        var patientResult = await PatientIdentity.ResolveAsync(dataStore, currentUser, cancellationToken, Wasla.Domain.Security.PermissionNames.PatientProfileUpdateOwn);
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

        await identityStore.LockAsync(cancellationToken);
        var changedEmail = PatientIdentityEmail.Normalize(request.Email) != patient.Email;
        PatientOnboardingChallenge? emailProof = null;
        if (changedEmail)
        {
            emailProof = await identityWorkflow.ProofAsync(request.EmailVerificationToken, PatientOnboardingAction.EmailChange, true, cancellationToken);
            var normalized = PatientIdentityEmail.Normalize(request.Email);
            if (normalized is null || emailProof is null || emailProof.ApplicationUserId != currentUser.UserId || emailProof.NormalizedEmail != normalized)
                return Result<PatientProfileResponse>.Fail(PatientIdentityErrors.Conflict("Patient.EmailVerificationRequired"));
            var existing = await identityStore.PatientByEmailAsync(normalized, cancellationToken);
            if (existing is not null && existing.Id != patient.Id || await dataStore.EmailExistsAsync(normalized, currentUser.UserId, cancellationToken))
                return Result<PatientProfileResponse>.Fail(PatientIdentityErrors.Conflict("Patient.IdentityConflict"));
            var user = await dataStore.FindUserByIdAsync(currentUser.UserId!.Value, cancellationToken);
            if (user is not { IsActive: true, UserType: UserType.Patient }) return Result<PatientProfileResponse>.Fail(PatientIdentityErrors.Denied);
            user.UpdateContact(normalized, request.PhoneNumber);
        }

        string? newKey = null;
        try
        {
            if (request.ProfileImage is not null)
            {
                newKey = (await mediaService.SaveAsync(request.ProfileImage, new MediaStorageRequest($"Patients/{patient.Id}/Profile"), cancellationToken)).Key;
                compensation.Track(newKey);
            }
            var updated = patient.UpdateProfile(request.NameAr, request.NameEn, request.PhoneNumber, request.Email, newKey, request.ProfileImage is not null);
            if (updated.IsFailure)
            {
                await CleanupAsync();
                return Result<PatientProfileResponse>.Fail(updated.Errors);
            }
            emailProof?.Consume(clock.UtcNow, "email-change");
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
        var patient = await PatientIdentity.ResolveAsync(dataStore, currentUser, cancellationToken, Wasla.Domain.Security.PermissionNames.PatientContactsViewOwn);
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
        var created = PatientContact.Create(Guid.NewGuid(), patient.Value.Id, input.NameAr, input.NameEn, input.PhoneNumber, input.RelationshipType, input.LinkedPatientId, input.IsPrimary, input.ContactEmail);
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
        var patientResult = await PatientIdentity.ResolveAsync(dataStore, currentUser, cancellationToken, Wasla.Domain.Security.PermissionNames.PatientContactsManageOwn);
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
        var updated = contact.Update(input.NameAr, input.NameEn, input.PhoneNumber, input.RelationshipType, input.LinkedPatientId, input.IsPrimary, input.ContactEmail);
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

internal sealed class SearchPatientsQueryHandler(
    IWaslaDataStore dataStore,
    IReceptionPracticeAuthorizationService practiceAuthorization, IPatientIdentityStore identityStore, ICurrentUser currentUser, IDateTimeProvider clock)
    : IQueryHandler<SearchPatientsQuery, PagedResponse<PatientSearchResponse>>
{
    public async Task<Result<PagedResponse<PatientSearchResponse>>> Handle(SearchPatientsQuery request, CancellationToken cancellationToken)
    {
        if (request.DoctorPracticeId is not { } practiceId)
        {
            return Result<PagedResponse<PatientSearchResponse>>.Fail(ReceptionErrors.AccessDenied);
        }

        var access = await practiceAuthorization.AuthorizeAsync(
            practiceId, Wasla.Domain.Security.PermissionNames.PatientsSearchBasic, cancellationToken);
        if (access.IsFailure)
        {
            return Result<PagedResponse<PatientSearchResponse>>.Fail(access.Errors);
        }

        var (items, total) = await identityStore.MatchAsync(new(request.Email, request.PhoneNumber, request.Name, request.DateOfBirth), request.PageNumber, request.PageSize, false, cancellationToken);
        dataStore.Add(new ArchiveAuditEvent("PatientSearchBooking", "PatientIdentity", null, practiceId, currentUser.UserId, clock.UtcNow));
        await dataStore.SaveChangesAsync(cancellationToken);
        var contacts = await identityStore.PatientsWithContactPhoneAsync(items.Select(i => i.Id).ToArray(), cancellationToken);
        var responses = new List<PatientSearchResponse>();
        foreach (var item in items)
            responses.Add(new PatientSearchResponse(item.Id, item.NameAr, item.NameEn, item.DateOfBirth, item.Gender,
                PatientIdentityEmail.MaskPhone(item.PhoneNumber), contacts.Contains(item.Id), PatientIdentityEmail.Mask(item.Email)));
        return Result<PagedResponse<PatientSearchResponse>>.Ok(new(responses.ToArray(), total, request.PageNumber, request.PageSize));
    }
}

internal static class PatientIdentity
{
    public static async Task<Result<Patient>> ResolveAsync(IWaslaDataStore dataStore, ICurrentUser currentUser, CancellationToken cancellationToken, string permission = Wasla.Domain.Security.PermissionNames.PatientProfileViewOwn)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
            return Result<Patient>.Fail(Error.Unauthorized("Auth.AuthenticationRequired", ErrorMessage.AuthenticationRequired));
        var access = await dataStore.GetAccessSnapshotAsync(userId, cancellationToken);
        if (access is not { User.IsActive: true, User.IsFirstLogin: false, User.UserType: UserType.Patient, PatientId: not null } || !access.Permissions.Contains(permission)) return Result<Patient>.Fail(PatientIdentityErrors.Denied);
        var patient = await dataStore.FindPatientByUserIdAsync(userId, cancellationToken);
        return patient is null ? Result<Patient>.Fail(PatientErrors.AccountLinkNotFound) : Result<Patient>.Ok(patient);
    }
}

internal static class PatientMapper
{
    public static PatientProfileResponse Profile(Patient patient)
        => new(patient.Id, patient.NameAr, patient.NameEn, patient.DateOfBirth, patient.Gender, patient.PhoneNumber,
            patient.Email, patient.ProfileImageMediaKey is not null, RowVersionCodec.Encode(patient.RowVersion), HasVerifiedIdentityEmail: patient.Email is not null, IdentityEmailMasked: PatientIdentityEmail.Mask(patient.Email));

    public static PatientContactResponse Contact(PatientContact contact)
        => new(contact.Id, contact.NameAr, contact.NameEn, contact.PhoneNumber, contact.RelationshipType, contact.LinkedPatientId, contact.IsPrimary, contact.ContactEmail);
}
