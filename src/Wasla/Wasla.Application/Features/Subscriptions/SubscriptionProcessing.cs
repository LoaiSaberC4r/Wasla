using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using Wasla.Application.Persistence;
using Wasla.Domain.Subscriptions;

namespace Wasla.Application.Features.Subscriptions;

public sealed record ProcessSubscriptionLifecycleCommand(Guid SubscriptionId) : ICommand<bool>, ITransactionalCommand<WaslaWritePersistence>;
internal sealed class ProcessSubscriptionLifecycleCommandHandler(ISubscriptionDataStore store, SubscriptionLifecycle lifecycle, IDateTimeProvider clock)
    : ICommandHandler<ProcessSubscriptionLifecycleCommand, bool>
{
    public async Task<Result<bool>> Handle(ProcessSubscriptionLifecycleCommand request, CancellationToken cancellationToken)
    {
        var doctorId = await store.DoctorIdAsync(request.SubscriptionId, cancellationToken);
        if (doctorId is null) return Result<bool>.Ok(false);
        await store.AcquireSubscriptionCapacityLockAsync(doctorId.Value, cancellationToken);
        var s = (await store.FindAsync(request.SubscriptionId, cancellationToken))!;
        var now = clock.UtcNow;
        if (s.Status == SubscriptionStatus.Scheduled && s.EndsAtUtc <= now)
        {
            s.Expire(now);
            lifecycle.History(s, "Expired", SubscriptionStatus.Scheduled, null, null, "expired:" + s.Id);
            await lifecycle.NotifyAsync(s, "Expired", "subscription-expired:" + s.Id, cancellationToken);
            await store.SaveChangesAsync(cancellationToken);
            return Result<bool>.Ok(true);
        }
        if (s.Status == SubscriptionStatus.Scheduled && s.StartsAtUtc <= now)
        {
            var previous = await store.CurrentAsync(s.DoctorId, cancellationToken);
            if (previous is not null)
            {
                previous.Expire(now);
                if (previous.Status == SubscriptionStatus.Active) return Result<bool>.Fail(SubscriptionErrors.Invalid());
                lifecycle.History(previous, "Expired", SubscriptionStatus.Active, null, null, "expired:" + previous.Id);
                await store.SaveChangesAsync(cancellationToken);
            }
            s.BeginScheduledPeriod(now);
            lifecycle.History(s, "PeriodStarted", SubscriptionStatus.Scheduled, null, null, "period-started:" + s.Id);
        }
        if (s.Status != SubscriptionStatus.Active) return Result<bool>.Ok(false);
        var reminders = s.SubscriptionKind == SubscriptionKind.Trial ? await store.RemindersAsync(s.Id, cancellationToken) : [];
        if (s.SubscriptionKind == SubscriptionKind.Trial)
            foreach (var reminder in reminders.Where(r => r.Status == SubscriptionReminderStatus.Pending && r.DueAtUtc <= now))
            {
                if (now < s.EndsAtUtc || reminder.ReminderCode == "D0")
                    await lifecycle.NotifyAsync(s, reminder.ReminderCode == "D0" ? "TrialExpired" : "TrialReminder",
                        $"subscription-trial-reminder:{s.Id}:{reminder.TrialRevision}:{reminder.ReminderCode}", cancellationToken);
                reminder.Queue(now);
            }
        if (s.EndsAtUtc <= now)
        {
            s.Expire(now);
            lifecycle.History(s, "Expired", SubscriptionStatus.Active, null, null, "expired:" + s.Id);
            // D0 is the single trial expiration notification; paid and transition expiry use the lifecycle key.
            if (s.SubscriptionKind != SubscriptionKind.Trial) await lifecycle.NotifyAsync(s, "Expired", "subscription-expired:" + s.Id, cancellationToken);
            else if (!reminders.Any(r => r.TrialRevision == s.TrialExtensionCount && r.ReminderCode == "D0" && r.Status == SubscriptionReminderStatus.Queued))
                await lifecycle.NotifyAsync(s, "TrialExpired", "subscription-expired:" + s.Id, cancellationToken);
        }
        await store.SaveChangesAsync(cancellationToken);
        return Result<bool>.Ok(true);
    }
}
