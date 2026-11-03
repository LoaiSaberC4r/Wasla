using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using Microsoft.Extensions.Options;
using Wasla.Application.Persistence;
using Wasla.Domain.Subscriptions;

namespace Wasla.Application.Features.Subscriptions;

public sealed record EnrollLegacyTransitionCommand : ICommand<LegacyTransitionEnrollmentBatch>, ITransactionalCommand<WaslaWritePersistence>;
internal sealed class EnrollLegacyTransitionCommandHandler(ISubscriptionDataStore store, SubscriptionManager manager,
    SubscriptionLifecycle lifecycle, IDateTimeProvider clock, IOptionsMonitor<SubscriptionCatalogOptions> options)
    : ICommandHandler<EnrollLegacyTransitionCommand, LegacyTransitionEnrollmentBatch>
{
    // One launch batch, stable across retries and process restarts.
    private static readonly Guid LaunchBatchId = Guid.Parse("c754a583-264d-4dcc-b409-d76798141d20");
    public async Task<Result<LegacyTransitionEnrollmentBatch>> Handle(EnrollLegacyTransitionCommand request, CancellationToken cancellationToken)
    {
        var actor = await manager.AdminAsync(SubscriptionPermissionNames.EnrollLegacyTransition, cancellationToken);
        if (actor is null) return Result<LegacyTransitionEnrollmentBatch>.Fail(SubscriptionErrors.Denied());
        var o = options.CurrentValue;
        if (!o.LegacyTransition.Enabled) return Result<LegacyTransitionEnrollmentBatch>.Fail(SubscriptionErrors.Invalid("ProductNotAvailable"));
        await store.AcquireSubscriptionCapacityLockAsync(LaunchBatchId, cancellationToken);
        var batch = await store.BatchAsync(LaunchBatchId, cancellationToken);
        if (batch?.Status == "Completed") return Result<LegacyTransitionEnrollmentBatch>.Ok(batch);
        if (batch is null)
        {
            batch = new(LaunchBatchId, clock.UtcNow, o.LegacyTransition.DurationDays, actor.Value);
            store.Add(batch);
        }
        var catalog = await store.CatalogAsync(cancellationToken);
        var plan = catalog.Plans.SingleOrDefault(p => p.Code == "LEGACY_TRANSITION" && p.IsActive);
        if (plan is null) return Result<LegacyTransitionEnrollmentBatch>.Fail(SubscriptionErrors.Invalid("ConfigurationInvalid"));
        var product = catalog.Products.Single(p => p.Id == plan.ProductId);
        // The first catalog seed predates every registration using commercial selections. Batch execution time alone
        // would incorrectly grant transition access to newly registered, approved doctors awaiting their first payment.
        var eligibilityCutoff = product.CreatedOnUtc < batch.StartedAtUtc ? product.CreatedOnUtc : batch.StartedAtUtc;
        var ids = await store.LegacyCandidatesAsync(eligibilityCutoff, o.BackgroundProcessing.BatchSize, cancellationToken);
        foreach (var doctorId in ids)
        {
            await store.AcquireSubscriptionCapacityLockAsync(doctorId, cancellationToken);
            if (await store.CurrentAsync(doctorId, cancellationToken) is not null || (await store.PendingAsync(doctorId, cancellationToken))?.SubscriptionKind == SubscriptionKind.Trial) continue;
            var usage = await store.UsageAsync(doctorId, cancellationToken);
            var features = (from e in catalog.Entitlements join f in catalog.Features on e.FeatureId equals f.Id
                            where e.PlanId == plan.Id select new SubscriptionFeatureTerms(f.Id, f.Code, e.AccessLevel)).ToArray();
            var terms = new SubscriptionTerms(product.Id, product.Code, plan.Id, plan.Code, SubscriptionKind.LegacyTransition,
                null, null, 0, o.Currency, Math.Max(o.LegacyTransition.MinimumPracticeLimit, usage.PracticeCount),
                Math.Max(o.LegacyTransition.MinimumReceptionLimit, usage.ReceptionCount), o.CatalogVersion, batch.DurationDays, [], features);
            var selection = lifecycle.Select(doctorId, terms);
            if (selection.IsFailure) return Result<LegacyTransitionEnrollmentBatch>.Fail(selection.Errors);
            selection.Value.Activate(batch.StartedAtUtc, actor.Value);
            lifecycle.History(selection.Value, "LegacyEnrolled", SubscriptionStatus.Pending, actor, null,
                "legacy-enrolled:" + doctorId, metadata: new { BatchId = batch.Id, EligibilityCutoffUtc = eligibilityCutoff });
        }
        if (ids.Count < o.BackgroundProcessing.BatchSize) batch.Complete(clock.UtcNow);
        await store.SaveChangesAsync(cancellationToken);
        return Result<LegacyTransitionEnrollmentBatch>.Ok(batch);
    }
}
