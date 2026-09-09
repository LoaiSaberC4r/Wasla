using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Encryption;
using BuildingBlock.Application.Abstraction.Media;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Email;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Doctors;
using Wasla.Domain.Patients;
using Wasla.Domain.Resources;
using Wasla.Domain.Security;

namespace Wasla.Application.Features.Registration;

public sealed record RegisterDoctorCommand(
    string UserName,
    string Email,
    string PhoneNumber,
    string Password,
    string ConfirmPassword,
    string NameAr,
    string? NameEn,
    DateOnly DateOfBirth,
    Gender Gender,
    MediaUpload? ProfileImage,
    MediaUpload? PersonalIdFrontImage,
    MediaUpload? PersonalIdBackImage,
    MediaUpload? SyndicateCardFrontImage,
    MediaUpload? SyndicateCardBackImage)
    : ICommand<RegisterDoctorResponse>, ITransactionalCommand<WaslaWritePersistence>;

public sealed record RegisterDoctorResponse(
    Guid DoctorId,
    Guid ApplicationUserId,
    DoctorApprovalStatus ApprovalStatus);

public sealed record RegisterPatientCommand(
    string UserName,
    string Email,
    string PhoneNumber,
    string Password,
    string ConfirmPassword,
    string NameAr,
    string? NameEn,
    DateOnly DateOfBirth,
    Gender Gender,
    MediaUpload? ProfileImage,
    MediaUpload? PersonalIdFrontImage,
    MediaUpload? PersonalIdBackImage)
    : ICommand<RegisterPatientResponse>, ITransactionalCommand<WaslaWritePersistence>;

public sealed record RegisterPatientResponse(Guid PatientId, Guid ApplicationUserId);

internal sealed class RegisterDoctorCommandValidator : AbstractValidator<RegisterDoctorCommand>
{
    public RegisterDoctorCommandValidator(IDateTimeProvider clock)
    {
        AddAccountRules();
        RuleFor(command => command.NameAr).NotEmpty().WithMessage(ErrorMessage.NameArRequired).MaximumLength(200);
        RuleFor(command => command.NameEn).MaximumLength(200);
        RuleFor(command => command.DateOfBirth)
            .NotEmpty().WithMessage(ErrorMessage.DateOfBirthRequired)
            .LessThanOrEqualTo(DateOnly.FromDateTime(clock.UtcNow)).WithMessage(ErrorMessage.DateOfBirthFuture);
        RuleFor(command => command.Gender).IsInEnum().Must(gender => gender is Gender.Male or Gender.Female)
            .WithMessage(ErrorMessage.GenderInvalid);
        RuleFor(command => command.PersonalIdFrontImage).NotNull().WithMessage(ErrorMessage.RequiredMediaMissing);
        RuleFor(command => command.PersonalIdBackImage).NotNull().WithMessage(ErrorMessage.RequiredMediaMissing);
        RuleFor(command => command.SyndicateCardFrontImage).NotNull().WithMessage(ErrorMessage.RequiredMediaMissing);
    }

    private void AddAccountRules()
    {
        RuleFor(command => command.UserName).NotEmpty().WithMessage(ErrorMessage.UserNameRequired).MaximumLength(100);
        RuleFor(command => command.Email).NotEmpty().WithMessage(ErrorMessage.EmailRequired).EmailAddress().WithMessage(ErrorMessage.EmailInvalid).MaximumLength(200);
        RuleFor(command => command.PhoneNumber).NotEmpty().WithMessage(ErrorMessage.PhoneNumberRequired).MaximumLength(30);
        RuleFor(command => command.Password).NotEmpty().WithMessage(ErrorMessage.PasswordRequired);
        RuleFor(command => command.ConfirmPassword).Equal(command => command.Password).WithMessage(ErrorMessage.PasswordConfirmationMismatch);
    }
}

internal sealed class RegisterPatientCommandValidator : AbstractValidator<RegisterPatientCommand>
{
    public RegisterPatientCommandValidator(IDateTimeProvider clock)
    {
        RuleFor(command => command.UserName).NotEmpty().WithMessage(ErrorMessage.UserNameRequired).MaximumLength(100);
        RuleFor(command => command.Email).NotEmpty().WithMessage(ErrorMessage.EmailRequired).EmailAddress().WithMessage(ErrorMessage.EmailInvalid).MaximumLength(200);
        RuleFor(command => command.PhoneNumber).NotEmpty().WithMessage(ErrorMessage.PhoneNumberRequired).MaximumLength(30);
        RuleFor(command => command.Password).NotEmpty().WithMessage(ErrorMessage.PasswordRequired);
        RuleFor(command => command.ConfirmPassword).Equal(command => command.Password).WithMessage(ErrorMessage.PasswordConfirmationMismatch);
        RuleFor(command => command.NameAr).NotEmpty().WithMessage(ErrorMessage.NameArRequired).MaximumLength(200);
        RuleFor(command => command.NameEn).MaximumLength(200);
        RuleFor(command => command.DateOfBirth)
            .NotEmpty().WithMessage(ErrorMessage.DateOfBirthRequired)
            .LessThanOrEqualTo(DateOnly.FromDateTime(clock.UtcNow)).WithMessage(ErrorMessage.DateOfBirthFuture);
        RuleFor(command => command.Gender).IsInEnum().Must(gender => gender is Gender.Male or Gender.Female)
            .WithMessage(ErrorMessage.GenderInvalid);
    }
}

internal sealed class RegisterDoctorCommandHandler(
    IWaslaDataStore dataStore,
    IPasswordService passwordService,
    IMediaService mediaService,
    IEmailNotificationFactory emailFactory,
    IEmailOutbox emailOutbox,
    IDateTimeProvider clock)
    : ICommandHandler<RegisterDoctorCommand, RegisterDoctorResponse>
{
    public async Task<Result<RegisterDoctorResponse>> Handle(
        RegisterDoctorCommand request,
        CancellationToken cancellationToken)
    {
        var duplicate = await RegistrationChecks.CheckDuplicatesAsync(
            dataStore,
            request.UserName,
            request.Email,
            cancellationToken);
        if (duplicate is not null)
        {
            return Result<RegisterDoctorResponse>.Fail(duplicate);
        }

        if (!passwordService.IsStrongPassword(request.Password))
        {
            return Result<RegisterDoctorResponse>.Fail(AuthRegistrationErrors.PasswordInvalid);
        }

        var role = await dataStore.FindRoleByNameAsync(SystemRoleNames.Doctor, cancellationToken);
        if (role is null)
        {
            return Result<RegisterDoctorResponse>.Fail(AuthRegistrationErrors.SecurityDataMissing);
        }

        var doctorId = Guid.NewGuid();
        var applicationUserId = Guid.NewGuid();
        var storedKeys = new List<string>(5);
        try
        {
            var profile = await SaveOptionalAsync(request.ProfileImage, $"Doctors/{doctorId}/Profile");
            var personalFront = await SaveRequiredAsync(request.PersonalIdFrontImage!, $"Doctors/{doctorId}/Verification/PersonalIdentity/Front");
            var personalBack = await SaveRequiredAsync(request.PersonalIdBackImage!, $"Doctors/{doctorId}/Verification/PersonalIdentity/Back");
            var syndicateFront = await SaveRequiredAsync(request.SyndicateCardFrontImage!, $"Doctors/{doctorId}/Verification/SyndicateCard/Front");
            var syndicateBack = await SaveOptionalAsync(request.SyndicateCardBackImage, $"Doctors/{doctorId}/Verification/SyndicateCard/Back");

            var now = clock.UtcNow;
            var userResult = ApplicationUser.Create(
                applicationUserId,
                request.UserName,
                request.Email,
                request.PhoneNumber,
                await passwordService.HashAsync(request.Password, cancellationToken),
                UserType.Doctor,
                isFirstLogin: false,
                now);
            if (userResult.IsFailure)
            {
                await CleanupAsync();
                return Result<RegisterDoctorResponse>.Fail(userResult.Errors);
            }

            var doctorResult = Doctor.Create(
                doctorId,
                applicationUserId,
                request.NameAr,
                request.NameEn,
                request.DateOfBirth,
                request.Gender,
                profile,
                personalFront,
                personalBack,
                syndicateFront,
                syndicateBack,
                DateOnly.FromDateTime(now));
            if (doctorResult.IsFailure)
            {
                await CleanupAsync();
                return Result<RegisterDoctorResponse>.Fail(doctorResult.Errors);
            }

            dataStore.Add(userResult.Value);
            dataStore.Add(doctorResult.Value);
            dataStore.Add(new UserRole(Guid.NewGuid(), applicationUserId, role.Id));
            dataStore.Add(new DoctorStatusHistory(
                Guid.NewGuid(),
                doctorId,
                null,
                DoctorApprovalStatus.Pending,
                null,
                null,
                now));

            var email = emailFactory.DoctorLifecycle(
                DoctorEmailEvent.RegistrationReceived,
                doctorResult.Value.NameAr);
            await emailOutbox.QueueAsync(new QueueEmailMessage(
                $"doctor-registration-received:{doctorId}",
                userResult.Value.Email,
                email.Subject,
                email.HtmlBody,
                email.TextBody), cancellationToken);
            await dataStore.SaveChangesAsync(cancellationToken);

            return Result<RegisterDoctorResponse>.Ok(new RegisterDoctorResponse(
                doctorId,
                applicationUserId,
                DoctorApprovalStatus.Pending));
        }
        catch
        {
            await CleanupAsync();
            throw;
        }

        async Task<string?> SaveOptionalAsync(MediaUpload? upload, string folder)
        {
            if (upload is null)
            {
                return null;
            }

            return await SaveRequiredAsync(upload, folder);
        }

        async Task<string> SaveRequiredAsync(MediaUpload upload, string folder)
        {
            var stored = await mediaService.SaveAsync(
                upload,
                new MediaStorageRequest(folder),
                cancellationToken);
            storedKeys.Add(stored.Key);
            return stored.Key;
        }

        async Task CleanupAsync()
        {
            if (storedKeys.Count == 0)
            {
                return;
            }

            try
            {
                await mediaService.DeleteRangeAsync(storedKeys, CancellationToken.None);
            }
            catch
            {
                // Best-effort compensation must not hide the original failure.
            }
        }
    }
}

internal sealed class RegisterPatientCommandHandler(
    IWaslaDataStore dataStore,
    IPasswordService passwordService,
    IMediaService mediaService,
    IDateTimeProvider clock)
    : ICommandHandler<RegisterPatientCommand, RegisterPatientResponse>
{
    public async Task<Result<RegisterPatientResponse>> Handle(
        RegisterPatientCommand request,
        CancellationToken cancellationToken)
    {
        var duplicate = await RegistrationChecks.CheckDuplicatesAsync(
            dataStore,
            request.UserName,
            request.Email,
            cancellationToken);
        if (duplicate is not null)
        {
            return Result<RegisterPatientResponse>.Fail(duplicate);
        }

        if (!passwordService.IsStrongPassword(request.Password))
        {
            return Result<RegisterPatientResponse>.Fail(AuthRegistrationErrors.PasswordInvalid);
        }

        var role = await dataStore.FindRoleByNameAsync(SystemRoleNames.Patient, cancellationToken);
        if (role is null)
        {
            return Result<RegisterPatientResponse>.Fail(AuthRegistrationErrors.SecurityDataMissing);
        }

        var patientId = Guid.NewGuid();
        var applicationUserId = Guid.NewGuid();
        var storedKeys = new List<string>(3);
        try
        {
            var profile = await SaveOptionalAsync(request.ProfileImage, $"Patients/{patientId}/Profile");
            var personalFront = await SaveOptionalAsync(request.PersonalIdFrontImage, $"Patients/{patientId}/Verification/PersonalIdentity/Front");
            var personalBack = await SaveOptionalAsync(request.PersonalIdBackImage, $"Patients/{patientId}/Verification/PersonalIdentity/Back");
            var now = clock.UtcNow;
            var userResult = ApplicationUser.Create(
                applicationUserId,
                request.UserName,
                request.Email,
                request.PhoneNumber,
                await passwordService.HashAsync(request.Password, cancellationToken),
                UserType.Patient,
                isFirstLogin: false,
                now);
            var patientResult = Patient.Create(
                patientId,
                request.NameAr,
                request.NameEn,
                request.DateOfBirth,
                request.Gender,
                request.PhoneNumber,
                request.Email,
                profile,
                personalFront,
                personalBack,
                DateOnly.FromDateTime(now));
            var linkResult = PatientAccountLink.Create(
                Guid.NewGuid(),
                applicationUserId,
                patientId,
                PatientAccountLinkSource.SelfRegistration,
                now);
            if (userResult.IsFailure || patientResult.IsFailure || linkResult.IsFailure)
            {
                await CleanupAsync();
                return Result<RegisterPatientResponse>.Fail(
                    userResult.IsFailure ? userResult.Errors :
                    patientResult.IsFailure ? patientResult.Errors : linkResult.Errors);
            }

            dataStore.Add(userResult.Value);
            dataStore.Add(patientResult.Value);
            dataStore.Add(linkResult.Value);
            dataStore.Add(new UserRole(Guid.NewGuid(), applicationUserId, role.Id));
            await dataStore.SaveChangesAsync(cancellationToken);
            return Result<RegisterPatientResponse>.Ok(new RegisterPatientResponse(patientId, applicationUserId));
        }
        catch
        {
            await CleanupAsync();
            throw;
        }

        async Task<string?> SaveOptionalAsync(MediaUpload? upload, string folder)
        {
            if (upload is null)
            {
                return null;
            }

            var stored = await mediaService.SaveAsync(upload, new MediaStorageRequest(folder), cancellationToken);
            storedKeys.Add(stored.Key);
            return stored.Key;
        }

        async Task CleanupAsync()
        {
            if (storedKeys.Count == 0)
            {
                return;
            }

            try
            {
                await mediaService.DeleteRangeAsync(storedKeys, CancellationToken.None);
            }
            catch
            {
                // Best-effort compensation must not hide the original failure.
            }
        }
    }
}

internal static class RegistrationChecks
{
    public static async Task<Error?> CheckDuplicatesAsync(
        IWaslaDataStore dataStore,
        string userName,
        string email,
        CancellationToken cancellationToken)
    {
        if (await dataStore.UserNameExistsAsync(userName.Trim(), null, cancellationToken))
        {
            return AuthRegistrationErrors.UserNameAlreadyExists;
        }

        return await dataStore.EmailExistsAsync(email.Trim().ToLowerInvariant(), null, cancellationToken)
            ? AuthRegistrationErrors.EmailAlreadyExists
            : null;
    }
}

internal static class AuthRegistrationErrors
{
    public static Error UserNameAlreadyExists => Error.Conflict("Identity.UserNameAlreadyExists", ErrorMessage.UserNameAlreadyExists);
    public static Error EmailAlreadyExists => Error.Conflict("Identity.EmailAlreadyExists", ErrorMessage.EmailAlreadyExists);
    public static Error PasswordInvalid => Error.Validation("Auth.PasswordInvalid", ErrorMessage.PasswordInvalid);
    public static Error SecurityDataMissing => Error.Conflict("Identity.SecurityDataMissing", ErrorMessage.RoleNotFound);
}
