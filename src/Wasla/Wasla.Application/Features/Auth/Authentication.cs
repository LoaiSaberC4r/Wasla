using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Encryption;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Persistence;
using Wasla.Application.Security;
using Wasla.Domain.Common;
using Wasla.Domain.Resources;
using Wasla.Domain.Security;

namespace Wasla.Application.Features.Auth;

public sealed record LoginCommand(string Identifier, string Password) : ICommand<LoginResponse>;

public sealed record LoginResponse(
    string AccessToken,
    DateTime ExpiresOnUtc,
    bool PasswordChangeRequired);

public sealed record MeQuery : IQuery<CurrentUserResponse>;

public sealed record CurrentUserResponse(
    Guid ApplicationUserId,
    string UserName,
    string Email,
    string? PhoneNumber,
    UserType UserType,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    bool IsFirstLogin,
    Guid? DoctorId,
    Guid? PatientId);

public sealed record ChangePasswordCommand(
    string CurrentPassword,
    string NewPassword,
    string ConfirmPassword)
    : ICommand, ITransactionalCommand<WaslaWritePersistence>;

internal sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(command => command.Identifier).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Password).NotEmpty().MaximumLength(4096);
    }
}

internal sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(command => command.CurrentPassword).NotEmpty().WithMessage(ErrorMessage.PasswordRequired);
        RuleFor(command => command.NewPassword).NotEmpty().WithMessage(ErrorMessage.PasswordRequired);
        RuleFor(command => command.ConfirmPassword)
            .Equal(command => command.NewPassword)
            .WithMessage(ErrorMessage.PasswordConfirmationMismatch);
    }
}

internal sealed class LoginCommandHandler(
    IWaslaDataStore dataStore,
    IPasswordService passwordService,
    IAccessTokenService tokenService)
    : ICommandHandler<LoginCommand, LoginResponse>
{
    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await dataStore.FindUserByIdentifierAsync(request.Identifier.Trim(), cancellationToken);
        if (user is null || !await passwordService.VerifyAsync(request.Password, user.PasswordHash, cancellationToken))
        {
            return Result<LoginResponse>.Fail(AuthErrors.InvalidCredentials);
        }

        if (!user.IsActive)
        {
            return Result<LoginResponse>.Fail(AuthErrors.AccountInactive);
        }

        var snapshot = await dataStore.GetAccessSnapshotAsync(user.Id, cancellationToken);
        if (snapshot is null || !HasMatchingProfile(snapshot))
        {
            return Result<LoginResponse>.Fail(AuthErrors.InvalidCredentials);
        }

        var permissions = EffectivePermissions(snapshot);
        var token = tokenService.Create(new AccessTokenDescriptor(
            user.Id,
            user.UserName,
            user.Email,
            user.UserType,
            snapshot.Roles,
            permissions,
            user.IsFirstLogin,
            snapshot.DoctorId,
            snapshot.PatientId));

        return Result<LoginResponse>.Ok(new LoginResponse(
            token.AccessToken,
            token.ExpiresOnUtc,
            user.IsFirstLogin));
    }

    private static bool HasMatchingProfile(UserAccessSnapshot snapshot)
        => snapshot.User.UserType switch
        {
            UserType.SuperAdmin => snapshot.SuperAdminId.HasValue,
            UserType.Doctor => snapshot.DoctorId.HasValue,
            UserType.Patient => snapshot.PatientId.HasValue,
            UserType.Reception => true,
            _ => false
        };

    internal static IReadOnlyList<string> EffectivePermissions(UserAccessSnapshot snapshot)
    {
        if (snapshot.User.IsFirstLogin)
        {
            return [];
        }

        IEnumerable<string> permissions = snapshot.Permissions;
        if (snapshot.User.UserType == UserType.Doctor &&
            snapshot.DoctorStatus == DoctorApprovalStatus.Pending)
        {
            permissions = permissions.Where(PermissionNames.PendingDoctorOnboarding.Contains);
        }
        else if (snapshot.User.UserType == UserType.Doctor &&
                 snapshot.DoctorStatus != DoctorApprovalStatus.Approved)
        {
            permissions = permissions.Where(permission => string.Equals(
                permission,
                PermissionNames.DoctorOnboardingViewOwn,
                StringComparison.OrdinalIgnoreCase));
        }

        if (snapshot.IsRootSuperAdmin)
        {
            permissions = permissions.Concat(PermissionNames.RootOnly);
        }

        return permissions
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

internal sealed class MeQueryHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser)
    : IQueryHandler<MeQuery, CurrentUserResponse>
{
    public async Task<Result<CurrentUserResponse>> Handle(MeQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return Result<CurrentUserResponse>.Fail(AuthErrors.AuthenticationRequired);
        }

        var snapshot = await dataStore.GetAccessSnapshotAsync(userId, cancellationToken);
        if (snapshot is null)
        {
            return Result<CurrentUserResponse>.Fail(AuthErrors.UserNotFound);
        }

        if (!snapshot.User.IsActive)
        {
            return Result<CurrentUserResponse>.Fail(AuthErrors.AccountInactive);
        }

        var user = snapshot.User;
        return Result<CurrentUserResponse>.Ok(new CurrentUserResponse(
            user.Id,
            user.UserName,
            user.Email,
            user.PhoneNumber,
            user.UserType,
            snapshot.Roles,
            LoginCommandHandler.EffectivePermissions(snapshot),
            user.IsFirstLogin,
            snapshot.DoctorId,
            snapshot.PatientId));
    }
}

internal sealed class ChangePasswordCommandHandler(
    IWaslaDataStore dataStore,
    IPasswordService passwordService,
    IDateTimeProvider clock,
    ICurrentUser currentUser)
    : ICommandHandler<ChangePasswordCommand>
{
    public async Task<Result> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return Result.Fail(AuthErrors.AuthenticationRequired);
        }

        var user = await dataStore.FindUserByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result.Fail(AuthErrors.UserNotFound);
        }

        if (!user.IsActive)
        {
            return Result.Fail(AuthErrors.AccountInactive);
        }

        if (!await passwordService.VerifyAsync(request.CurrentPassword, user.PasswordHash, cancellationToken))
        {
            return Result.Fail(AuthErrors.CurrentPasswordInvalid);
        }

        if (await passwordService.VerifyAsync(request.NewPassword, user.PasswordHash, cancellationToken))
        {
            return Result.Fail(AuthErrors.PasswordMustBeDifferent);
        }

        if (!passwordService.IsStrongPassword(request.NewPassword))
        {
            return Result.Fail(AuthErrors.PasswordInvalid);
        }

        user.ChangePassword(
            await passwordService.HashAsync(request.NewPassword, cancellationToken),
            clock.UtcNow);
        await dataStore.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }
}

internal static class AuthErrors
{
    public static Error InvalidCredentials => Error.Unauthorized("Auth.InvalidCredentials", ErrorMessage.InvalidCredentials);
    public static Error AccountInactive => Error.Unauthorized("Auth.AccountInactive", ErrorMessage.AccountInactive);
    public static Error AuthenticationRequired => Error.Unauthorized("Auth.AuthenticationRequired", ErrorMessage.AuthenticationRequired);
    public static Error UserNotFound => Error.NotFound("Identity.ApplicationUserNotFound", ErrorMessage.ApplicationUserNotFound);
    public static Error CurrentPasswordInvalid => Error.Validation("Auth.CurrentPasswordInvalid", ErrorMessage.CurrentPasswordInvalid);
    public static Error PasswordMustBeDifferent => Error.Validation("Auth.PasswordMustBeDifferent", ErrorMessage.PasswordMustBeDifferent);
    public static Error PasswordInvalid => Error.Validation("Auth.PasswordInvalid", ErrorMessage.PasswordInvalid);
}
