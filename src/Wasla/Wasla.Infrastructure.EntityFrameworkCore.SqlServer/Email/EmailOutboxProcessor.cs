using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Options;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;
using BuildingBlock.Application.Email;
using BuildingBlock.Application.Time;
using BuildingBlock.Infrastructure.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Email;

public sealed partial class EmailOutboxProcessor(
    WaslaDbContext dbContext,
    IEmailSender emailSender,
    IDateTimeProvider clock,
    IOptions<EmailOutboxOptions> options,
    ILogger<EmailOutboxProcessor> logger,
    Notifications.NotificationEmailGuard? notificationGuard = null)
{
    private readonly EmailOutboxOptions _options = options.Value;

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var nowUtc = RequireUtc(clock.UtcNow);
        var candidateIds = await dbContext.EmailOutboxMessages
            .AsNoTracking()
            .Where(message =>
                message.Status == EmailOutboxStatus.Pending &&
                (message.NextAttemptOnUtc == null || message.NextAttemptOnUtc <= nowUtc) ||
                message.Status == EmailOutboxStatus.Processing &&
                message.NextAttemptOnUtc != null &&
                message.NextAttemptOnUtc <= nowUtc)
            .OrderBy(message => message.CreatedOnUtc)
            .Select(message => message.Id)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);

        if (candidateIds.Count == 0)
        {
            return 0;
        }

        var claimedOnUtc = RequireUtc(clock.UtcNow);
        var leaseUntilUtc = claimedOnUtc.AddSeconds(_options.ClaimLeaseSeconds);
        var processingToken = Guid.NewGuid();

        await dbContext.EmailOutboxMessages
            .Where(message =>
                candidateIds.Contains(message.Id) &&
                (message.Status == EmailOutboxStatus.Pending &&
                 (message.NextAttemptOnUtc == null || message.NextAttemptOnUtc <= claimedOnUtc) ||
                 message.Status == EmailOutboxStatus.Processing &&
                 message.NextAttemptOnUtc != null &&
                 message.NextAttemptOnUtc <= claimedOnUtc))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(message => message.Status, EmailOutboxStatus.Processing)
                    .SetProperty(message => message.AttemptCount, message => message.AttemptCount + 1)
                    .SetProperty(message => message.LastAttemptOnUtc, claimedOnUtc)
                    .SetProperty(message => message.NextAttemptOnUtc, leaseUntilUtc)
                    .SetProperty(message => message.LastError, (string?)null)
                    .SetProperty(message => message.ProcessingToken, processingToken),
                cancellationToken);

        var claimedMessages = await dbContext.EmailOutboxMessages
            .AsNoTracking()
            .Where(message =>
                message.Status == EmailOutboxStatus.Processing &&
                message.ProcessingToken == processingToken)
            .OrderBy(message => message.CreatedOnUtc)
            .ToListAsync(cancellationToken);

        foreach (var message in claimedMessages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ProcessClaimedAsync(message, processingToken, cancellationToken);
        }

        return claimedMessages.Count;
    }

    private async Task ProcessClaimedAsync(
        EmailOutboxMessage message,
        Guid processingToken,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var admission = message.NotificationDeliveryId is { } deliveryId && notificationGuard is not null
                ? await notificationGuard.AcquireAsync(deliveryId, cancellationToken) : null;
            if (message.NotificationDeliveryId is { } correlated)
            {
                // A session application lock serializes SMTP admission with Reservation commits;
                // no SQL transaction is held across network transport.
                var denial = notificationGuard is null ? "Delivery.GuardUnavailable" :
                    await notificationGuard.DenialAsync(correlated, message.RecipientEmail, cancellationToken);
                var ownsClaim = await dbContext.EmailOutboxMessages.AsNoTracking().AnyAsync(m => m.Id == message.Id &&
                    m.Status == EmailOutboxStatus.Processing && m.ProcessingToken == processingToken && m.NextAttemptOnUtc > clock.UtcNow, cancellationToken);
                if (!ownsClaim) return;
                if (denial is not null)
                {
                    await dbContext.EmailOutboxMessages.Where(m => m.Id == message.Id && m.Status == EmailOutboxStatus.Processing && m.ProcessingToken == processingToken)
                        .ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, EmailOutboxStatus.Suppressed)
                            .SetProperty(m => m.ProcessedOnUtc, clock.UtcNow).SetProperty(m => m.ProcessingToken, (Guid?)null)
                            .SetProperty(m => m.NextAttemptOnUtc, (DateTime?)null).SetProperty(m => m.LastError, denial), cancellationToken);
                    if (notificationGuard is not null) await notificationGuard.SuppressAsync(correlated, cancellationToken);
                    return;
                }
            }
            await emailSender.SendAsync(
                new EmailMessage
                {
                    To = [message.RecipientEmail],
                    Subject = message.Subject,
                    HtmlBody = message.HtmlBody,
                    TextBody = message.TextBody
                },
                cancellationToken);

            var sentOnUtc = RequireUtc(clock.UtcNow);
            await using var completionTransaction = message.NotificationDeliveryId.HasValue
                ? await dbContext.Database.BeginTransactionAsync(cancellationToken) : null;
            await dbContext.EmailOutboxMessages
                .Where(item =>
                    item.Id == message.Id &&
                    item.Status == EmailOutboxStatus.Processing &&
                    item.ProcessingToken == processingToken)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(item => item.Status, EmailOutboxStatus.Sent)
                        .SetProperty(item => item.ProcessedOnUtc, sentOnUtc)
                        .SetProperty(item => item.NextAttemptOnUtc, (DateTime?)null)
                        .SetProperty(item => item.LastError, (string?)null)
                        .SetProperty(item => item.ProcessingToken, (Guid?)null),
                    cancellationToken);

            if (message.NotificationDeliveryId is { } sentDelivery && notificationGuard is not null)
                await notificationGuard.RecordRetryOutcomeAsync(sentDelivery, true, sentOnUtc, cancellationToken);
            if (completionTransaction is not null) await completionTransaction.CommitAsync(cancellationToken);
            if (message.NotificationDeliveryId is { } delivered && notificationGuard is not null)
                await notificationGuard.RecordTransportAsync(delivered, true, cancellationToken);
            MessageSent(logger, message.Id);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await RecordFailureAsync(message, processingToken, exception, cancellationToken);
        }
    }

    private async Task RecordFailureAsync(
        EmailOutboxMessage message,
        Guid processingToken,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var isPermanent = IsPermanent(exception);
        var exhausted = message.AttemptCount >= _options.MaxAttempts;
        var status = isPermanent || exhausted
            ? EmailOutboxStatus.Failed
            : EmailOutboxStatus.Pending;
        var nextAttemptOnUtc = status == EmailOutboxStatus.Pending
            ? RequireUtc(clock.UtcNow).Add(CalculateRetryDelay(message.AttemptCount))
            : (DateTime?)null;

        await using var failureTransaction = message.NotificationDeliveryId.HasValue
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken) : null;

        await dbContext.EmailOutboxMessages
            .Where(item =>
                item.Id == message.Id &&
                item.Status == EmailOutboxStatus.Processing &&
                item.ProcessingToken == processingToken)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.Status, status)
                    .SetProperty(item => item.NextAttemptOnUtc, nextAttemptOnUtc)
                    .SetProperty(item => item.LastError, SafeErrorCode(exception))
                    .SetProperty(item => item.ProcessingToken, (Guid?)null),
                cancellationToken);

        if (status == EmailOutboxStatus.Failed && message.NotificationDeliveryId is { } failedDelivery && notificationGuard is not null)
            await notificationGuard.RecordRetryOutcomeAsync(failedDelivery, false, RequireUtc(clock.UtcNow), cancellationToken);
        if (failureTransaction is not null) await failureTransaction.CommitAsync(cancellationToken);
        MessageFailed(logger, message.Id, message.AttemptCount, status);
    }

    private static bool IsPermanent(Exception exception)
        => exception is EmailServiceException emailException &&
           emailException.Error.Code is
               ExternalServiceErrorCodes.Email.InvalidMessage or
               ExternalServiceErrorCodes.Email.AuthenticationFailed;

    private static string SafeErrorCode(Exception exception)
        => exception is EmailServiceException emailException
            ? emailException.Error.Code
            : ExternalServiceErrorCodes.Email.SendFailed;

    private static TimeSpan CalculateRetryDelay(int attemptCount)
        => TimeSpan.FromMinutes(Math.Min(Math.Pow(2, Math.Max(0, attemptCount - 1)), 60));

    private static DateTime RequireUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();

    [LoggerMessage(
        EventId = 4310,
        Level = LogLevel.Information,
        Message = "Email outbox message {MessageId} sent successfully.")]
    private static partial void MessageSent(ILogger logger, Guid messageId);

    [LoggerMessage(
        EventId = 4311,
        Level = LogLevel.Warning,
        Message = "Email outbox message {MessageId} failed attempt {AttemptCount}; status is {Status}.")]
    private static partial void MessageFailed(
        ILogger logger,
        Guid messageId,
        int attemptCount,
        EmailOutboxStatus status);
}
