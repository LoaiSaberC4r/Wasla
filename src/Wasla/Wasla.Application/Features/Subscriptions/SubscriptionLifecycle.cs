using System.Globalization;
using System.Text.Json;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using Wasla.Application.Email;
using Wasla.Application.Persistence;
using Wasla.Application.Features.Reservations;
using Wasla.Domain.Doctors;
using Wasla.Domain.Subscriptions;

namespace Wasla.Application.Features.Subscriptions;

public sealed class SubscriptionLifecycle(ISubscriptionDataStore store, IWaslaDataStore doctors,
    IDateTimeProvider clock, IEmailNotificationFactory factory, IEmailOutbox outbox, ISubscriptionCatalogResolver resolver, IReservationProjectionInvalidationOutbox projections)
{
    public Result<DoctorSubscription> Select(Guid doctorId, SubscriptionTerms terms, Guid? contractId = null)
    {
        var result = DoctorSubscription.Select(doctorId, terms, clock.UtcNow, contractId);
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
        // Approval grants eligibility only; pending selections never start the lifetime trial.
        if (!approved)
            foreach (var subscription in (await store.PeriodsAsync(doctor.Id, ct)).Where(s => s.Status == SubscriptionStatus.Pending))
            {
                subscription.Reject(clock.UtcNow);
                History(subscription, "DoctorRejected", SubscriptionStatus.Pending, actor, null, $"doctor-rejected:{subscription.Id}");
            }
        return Result.Ok();
    }
    public async Task ScheduleRemindersAsync(DoctorSubscription subscription, CancellationToken ct)
    {
        foreach (var old in await store.RemindersAsync(subscription.Id, ct)) old.Cancel();
        var offsets = subscription.SubscriptionKind == SubscriptionKind.Paid ? new[] { 7, 3, 1, 0 } : subscription.Terms.ReminderDaysBeforeEnd;
        foreach (var days in offsets)
            store.Add(new SubscriptionReminderSchedule(subscription.Id, "D" + days.ToString(CultureInfo.InvariantCulture),
                subscription.EndsAtUtc!.Value.AddDays(-days), subscription.TrialExtensionCount, clock.UtcNow));
    }
    public void History(DoctorSubscription subscription, string eventType, SubscriptionStatus? previous, Guid? actor,
        string? reason, string key, string? previousTerms = null, object? metadata = null)
        => store.Add(new SubscriptionHistory(Guid.NewGuid(), subscription.Id, eventType, previous, subscription.Status, actor,
            clock.UtcNow, reason, previousTerms, subscription.TermsSnapshotJson, metadata is null ? null : JsonSerializer.Serialize(metadata), key, clock.UtcNow));
    public async Task NotifyAsync(DoctorSubscription subscription, string eventType, string key, CancellationToken ct)
    {
        if (eventType is "Activated" or "TrialStarted" or "Expired" or "TrialExpired" or "TrialExtended")
            await QueueProjectionAsync(subscription, key, ct);
        // A renewal can reserve a future period while the current one is still active.
        if (eventType == "Activated" && subscription.Status == SubscriptionStatus.Scheduled) return;
        var doctor = await doctors.FindDoctorByIdAsync(subscription.DoctorId, ct);
        var user = doctor is null ? null : await doctors.FindUserByIdAsync(doctor.ApplicationUserId, ct);
        if (user is null) return;
        var names = new List<EmailName>();
        if (eventType is "Activated" or "PaidReminder")
        {
            foreach (var allocation in await store.AllocationsAsync(subscription.SubscriptionContractId, ct))
                if (await doctors.FindDoctorPracticeAsync(allocation.DoctorPracticeId, ct) is { } practice)
                    names.Add(new(practice.NameAr, practice.NameEn));
        }
        decimal? renewalPrice = null;
        string? currency = null;
        if (eventType == "PaidReminder" && subscription.SubscriptionKind == SubscriptionKind.Paid)
        {
            var renewal = await resolver.ResolveAsync(new(subscription.Terms.ProductCode, subscription.Terms.PlanCode, subscription.BillingCycle, SelectedPracticeCount: subscription.SelectedPracticeCount), ct);
            if (renewal.IsSuccess)
            {
                renewalPrice = renewal.Value.PriceAmount;
                currency = renewal.Value.Currency;
            }
        }
        var name = new EmailName(doctor!.NameAr, doctor.NameEn);
        var plan = subscription.Terms.PlanCode;
        var ends = subscription.EndsAtUtc;
        SubscriptionEmail model = eventType switch
        {
            "TrialStarted" => new TrialStartedEmail(name, plan, ends, subscription.StartsAtUtc, subscription.PracticeLimit, subscription.ReceptionLimit),
            "TrialReminder" => new TrialReminderEmail(name, plan, ends),
            "TrialExpired" => new TrialExpiredEmail(name, plan, ends),
            "TrialExtended" => new TrialExtendedEmail(name, plan, ends),
            "Activated" => new PaidSubscriptionActivatedEmail(name, plan, ends, subscription.StartsAtUtc, subscription.PracticeLimit, subscription.ReceptionLimit, names),
            "PaidReminder" => new PaidSubscriptionReminderEmail(name, plan, ends, names, renewalPrice, currency),
            "Expired" => new SubscriptionExpiredEmail(name, plan, ends),
            _ => throw new InvalidOperationException("Unsupported subscription email event.")
        };
        var email = await factory.CreateAsync(model, ct);
        await outbox.QueueAsync(new QueueEmailMessage(key, user.Email, email.Subject, email.HtmlBody, email.TextBody), ct);
    }
    public async Task QueueProjectionAsync(DoctorSubscription subscription, string key, CancellationToken ct)
    {
        foreach (var allocation in await store.AllocationsAsync(subscription.SubscriptionContractId, ct))
            await projections.QueueAsync(new("commercial:" + key + ":" + allocation.DoctorPracticeId.ToString("N"), allocation.DoctorPracticeId,
                subscription.DoctorId, RefreshAvailability: true, RefreshPopularity: true), ct);
    }
    public async Task SuppressPaidRemindersAsync(DoctorSubscription period, CancellationToken ct)
    {
        foreach (var reminder in await store.RemindersAsync(period.Id, ct)) reminder.Cancel();
        await outbox.SuppressPaidRemindersAsync(period.Id, ct);
    }
}
