using System.Globalization;
using System.Text.Json;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using Wasla.Application.Email;
using Wasla.Application.Persistence;
using Wasla.Domain.Doctors;
using Wasla.Domain.Subscriptions;

namespace Wasla.Application.Features.Subscriptions;

public sealed class SubscriptionLifecycle(ISubscriptionDataStore store, IWaslaDataStore doctors,
    IDateTimeProvider clock, IEmailNotificationFactory factory, IEmailOutbox outbox)
{
    public Result<DoctorSubscription> Select(Guid doctorId, SubscriptionTerms terms)
    {
        var result = DoctorSubscription.Select(doctorId, terms, clock.UtcNow);
        if (result.IsFailure) return result;
        store.Add(result.Value);
        AddFeatures(result.Value);
        History(result.Value, "Selected", null, null, null, $"selected:{result.Value.Id}");
        return result;
    }
    public void AddFeatures(DoctorSubscription subscription)
    {
        foreach (var feature in subscription.Terms.Features)
            store.Add(new SubscriptionFeatureSnapshot(Guid.NewGuid(), subscription.Id, feature.FeatureId, feature.Code, feature.AccessLevel, clock.UtcNow));
    }
    public async Task<Result> DoctorDecisionAsync(Doctor doctor, Guid actor, bool approved, CancellationToken ct)
    {
        await store.AcquireSubscriptionCapacityLockAsync(doctor.Id, ct);
        var subscription = await store.PendingAsync(doctor.Id, ct);
        // Legacy doctors without a selection are enrolled only through the explicit transition operation.
        if (subscription is null) return Result.Ok();
        if (!approved)
        {
            subscription.Reject(clock.UtcNow);
            History(subscription, "DoctorRejected", SubscriptionStatus.Pending, actor, null, $"doctor-rejected:{subscription.Id}");
        }
        else if (subscription.SubscriptionKind == SubscriptionKind.Trial)
        {
            if (doctor.ApprovedOnUtc is not { } approvedAt) return Result.Fail(SubscriptionErrors.Invalid("DoctorNotApproved"));
            var activation = subscription.Activate(approvedAt, actor);
            if (activation.IsFailure) return activation;
            History(subscription, "TrialStarted", SubscriptionStatus.Pending, actor, null, $"trial-started:{subscription.Id}");
            await ScheduleRemindersAsync(subscription, ct);
            await NotifyAsync(subscription, "TrialStarted", $"subscription-trial-started:{subscription.Id}", ct);
        }
        return Result.Ok();
    }
    public async Task ScheduleRemindersAsync(DoctorSubscription subscription, CancellationToken ct)
    {
        foreach (var old in await store.RemindersAsync(subscription.Id, ct)) old.Cancel();
        foreach (var days in subscription.Terms.ReminderDaysBeforeEnd)
            store.Add(new SubscriptionReminderSchedule(subscription.Id, "D" + days.ToString(CultureInfo.InvariantCulture),
                subscription.EndsAtUtc!.Value.AddDays(-days), subscription.TrialExtensionCount, clock.UtcNow));
    }
    public void History(DoctorSubscription subscription, string eventType, SubscriptionStatus? previous, Guid? actor,
        string? reason, string key, string? previousTerms = null, object? metadata = null)
        => store.Add(new SubscriptionHistory(Guid.NewGuid(), subscription.Id, eventType, previous, subscription.Status, actor,
            clock.UtcNow, reason, previousTerms, subscription.TermsSnapshotJson, metadata is null ? null : JsonSerializer.Serialize(metadata), key, clock.UtcNow));
    public async Task NotifyAsync(DoctorSubscription subscription, string eventType, string key, CancellationToken ct)
    {
        var doctor = await doctors.FindDoctorByIdAsync(subscription.DoctorId, ct);
        var user = doctor is null ? null : await doctors.FindUserByIdAsync(doctor.ApplicationUserId, ct);
        if (user is null) return;
        var email = factory.SubscriptionLifecycle(eventType, doctor!.NameAr, subscription.EndsAtUtc,
            subscription.StartsAtUtc, subscription.Terms.PlanCode, subscription.PracticeLimit, subscription.ReceptionLimit);
        await outbox.QueueAsync(new QueueEmailMessage(key, user.Email, email.Subject, email.HtmlBody, email.TextBody), ct);
    }
}
