using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Encryption;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Microsoft.Extensions.Options;
using Wasla.Application.Email;
using Wasla.Application.Persistence;
using Wasla.Application.Security;
using Wasla.Domain.Resources;
using Wasla.Domain.Security;

namespace Wasla.Application.Features.Auth;

public sealed record RequestPasswordResetOtpCommand(string Email)
    : ICommand<PasswordResetRequestResponse>, ITransactionalCommand<WaslaWritePersistence>;

public sealed record PasswordResetRequestResponse(Guid RequestId, string Message);

public sealed record VerifyPasswordResetOtpCommand(Guid RequestId, string Otp)
    : ICommand<VerifyPasswordResetOtpResponse>;

public sealed record VerifyPasswordResetOtpResponse(Guid RequestId, string ResetToken, DateTime ExpiresOnUtc);

public sealed record ResetPasswordCommand(
    Guid RequestId,
    string ResetToken,
    string NewPassword,
    string ConfirmPassword)
    : ICommand, ITransactionalCommand<WaslaWritePersistence>;

internal sealed class RequestPasswordResetOtpCommandValidator : AbstractValidator<RequestPasswordResetOtpCommand>
{
    public RequestPasswordResetOtpCommandValidator()
        => RuleFor(command => command.Email)
            .NotEmpty().WithMessage(ErrorMessage.EmailRequired)
            .EmailAddress().WithMessage(ErrorMessage.EmailInvalid)
            .MaximumLength(200);
}

internal sealed class VerifyPasswordResetOtpCommandValidator : AbstractValidator<VerifyPasswordResetOtpCommand>
{
    public VerifyPasswordResetOtpCommandValidator(IOptions<PasswordResetOptions> options)
    {
        RuleFor(command => command.RequestId).NotEmpty();
        RuleFor(command => command.Otp).NotEmpty().Length(options.Value.OtpLength);
    }
}

internal sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(command => command.RequestId).NotEmpty();
        RuleFor(command => command.ResetToken).NotEmpty().MaximumLength(500);
        RuleFor(command => command.NewPassword).NotEmpty().WithMessage(ErrorMessage.PasswordRequired);
        RuleFor(command => command.ConfirmPassword)
            .Equal(command => command.NewPassword)
            .WithMessage(ErrorMessage.PasswordConfirmationMismatch);
    }
}

internal sealed class RequestPasswordResetOtpCommandHandler(
    IWaslaDataStore dataStore,
    IPasswordResetProtector protector,
    IEmailNotificationFactory emailFactory,
    IEmailOutbox emailOutbox,
    IDateTimeProvider clock,
    IOptions<PasswordResetOptions> options)
    : ICommandHandler<RequestPasswordResetOtpCommand, PasswordResetRequestResponse>
{
    public async Task<Result<PasswordResetRequestResponse>> Handle(
        RequestPasswordResetOtpCommand request,
        CancellationToken cancellationToken)
    {
        var opaqueRequestId = Guid.NewGuid();
        var user = await dataStore.FindUserByIdentifierAsync(
            request.Email.Trim().ToLowerInvariant(),
            cancellationToken);
        if (user is null)
        {
            return Accepted(opaqueRequestId);
        }

        var now = clock.UtcNow;
        var latest = await dataStore.FindLatestChallengeAsync(user.Id, cancellationToken);
        if (latest is not null &&
            !latest.IsConsumed &&
            !latest.IsInvalidated &&
            latest.CreatedOnUtc.AddSeconds(options.Value.ResendCooldownSeconds) > now)
        {
            return Accepted(latest.Id);
        }

        foreach (var active in await dataStore.ListActiveChallengesAsync(user.Id, cancellationToken))
        {
            var invalidation = active.Invalidate(now);
            if (invalidation.IsFailure)
            {
                return Result<PasswordResetRequestResponse>.Fail(invalidation.Errors);
            }
        }

        var otp = protector.GenerateOtp(options.Value.OtpLength);
        var challengeResult = PasswordResetChallenge.Create(
            opaqueRequestId,
            user.Id,
            protector.HashOtp(otp),
            now,
            now.AddMinutes(options.Value.OtpExpirationMinutes));
        if (challengeResult.IsFailure)
        {
            return Result<PasswordResetRequestResponse>.Fail(challengeResult.Errors);
        }

        dataStore.Add(challengeResult.Value);
        var email = emailFactory.PasswordResetOtp(otp, options.Value.OtpExpirationMinutes);
        await emailOutbox.QueueAsync(new QueueEmailMessage(
            $"password-reset-otp:{opaqueRequestId}",
            user.Email,
            email.Subject,
            email.HtmlBody,
            email.TextBody), cancellationToken);
        await dataStore.SaveChangesAsync(cancellationToken);
        return Accepted(opaqueRequestId);
    }

    private static Result<PasswordResetRequestResponse> Accepted(Guid requestId)
        => Result<PasswordResetRequestResponse>.Ok(new PasswordResetRequestResponse(
            requestId,
            ErrorMessage.PasswordResetRequestAccepted));
}

internal sealed class VerifyPasswordResetOtpCommandHandler(
    IWaslaDataStore dataStore,
    IPasswordResetProtector protector,
    IDateTimeProvider clock,
    IOptions<PasswordResetOptions> options)
    : ICommandHandler<VerifyPasswordResetOtpCommand, VerifyPasswordResetOtpResponse>
{
    public async Task<Result<VerifyPasswordResetOtpResponse>> Handle(
        VerifyPasswordResetOtpCommand request,
        CancellationToken cancellationToken)
    {
        var challenge = await dataStore.FindChallengeAsync(request.RequestId, cancellationToken);
        if (challenge is null)
        {
            return Result<VerifyPasswordResetOtpResponse>.Fail(PasswordResetErrors.OtpInvalid);
        }

        var now = clock.UtcNow;
        if (challenge.IsExpired(now))
        {
            return Result<VerifyPasswordResetOtpResponse>.Fail(PasswordResetErrors.OtpExpired);
        }

        if (!challenge.CanVerify(now, options.Value.MaximumVerificationAttempts))
        {
            return Result<VerifyPasswordResetOtpResponse>.Fail(PasswordResetErrors.TooManyAttempts);
        }

        if (!protector.VerifyOtp(request.Otp, challenge.OtpHash))
        {
            var failedAttempt = challenge.RegisterFailedAttempt(now, options.Value.MaximumVerificationAttempts);
            await dataStore.SaveChangesAsync(cancellationToken);
            return Result<VerifyPasswordResetOtpResponse>.Fail(
                challenge.IsInvalidated ? PasswordResetErrors.TooManyAttempts :
                failedAttempt.IsFailure ? failedAttempt.Errors[0] : PasswordResetErrors.OtpInvalid);
        }

        var verified = challenge.Verify(now, options.Value.MaximumVerificationAttempts);
        if (verified.IsFailure)
        {
            return Result<VerifyPasswordResetOtpResponse>.Fail(verified.Errors);
        }

        var resetToken = protector.GenerateResetToken();
        var expiresOnUtc = now.AddMinutes(options.Value.ResetTokenExpirationMinutes);
        var issued = challenge.IssueResetToken(
            protector.HashResetToken(resetToken),
            expiresOnUtc,
            now);
        if (issued.IsFailure)
        {
            return Result<VerifyPasswordResetOtpResponse>.Fail(issued.Errors);
        }

        await dataStore.SaveChangesAsync(cancellationToken);
        return Result<VerifyPasswordResetOtpResponse>.Ok(new VerifyPasswordResetOtpResponse(
            challenge.Id,
            resetToken,
            expiresOnUtc));
    }
}

internal sealed class ResetPasswordCommandHandler(
    IWaslaDataStore dataStore,
    IPasswordResetProtector protector,
    IPasswordService passwordService,
    IDateTimeProvider clock)
    : ICommandHandler<ResetPasswordCommand>
{
    public async Task<Result> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var challenge = await dataStore.FindChallengeAsync(request.RequestId, cancellationToken);
        if (challenge is null || challenge.ResetTokenHash is null)
        {
            return Result.Fail(PasswordResetErrors.ResetTokenInvalid);
        }

        var now = clock.UtcNow;
        if (challenge.ResetTokenExpiresOnUtc <= now)
        {
            return Result.Fail(PasswordResetErrors.ResetTokenExpired);
        }

        if (!challenge.CanReset(now) ||
            !protector.VerifyResetToken(request.ResetToken, challenge.ResetTokenHash))
        {
            return Result.Fail(PasswordResetErrors.ResetTokenInvalid);
        }

        var user = await dataStore.FindUserByIdAsync(challenge.ApplicationUserId, cancellationToken);
        if (user is null)
        {
            return Result.Fail(AuthErrors.UserNotFound);
        }

        if (!passwordService.IsStrongPassword(request.NewPassword))
        {
            return Result.Fail(AuthErrors.PasswordInvalid);
        }

        if (await passwordService.VerifyAsync(request.NewPassword, user.PasswordHash, cancellationToken))
        {
            return Result.Fail(AuthErrors.PasswordMustBeDifferent);
        }

        var consumed = challenge.Consume(now);
        if (consumed.IsFailure)
        {
            return consumed;
        }

        user.ChangePassword(
            await passwordService.HashAsync(request.NewPassword, cancellationToken),
            now);
        foreach (var active in await dataStore.ListActiveChallengesAsync(user.Id, cancellationToken))
        {
            if (active.Id != challenge.Id)
            {
                _ = active.Invalidate(now);
            }
        }

        await dataStore.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }
}
