using Wasla.Domain.Subscriptions;

namespace Wasla.Application.Persistence;

public sealed record SubscriptionCatalogData(IReadOnlyList<CommercialProduct> Products, IReadOnlyList<CommercialPlan> Plans,
    IReadOnlyList<CommercialFeature> Features, IReadOnlyList<PlanFeatureEntitlement> Entitlements);
public sealed record SubscriptionUsage(int PracticeCount, int ReceptionCount);
public sealed record SubscriptionListFilter(int PageNumber = 1, int PageSize = 20, string? Search = null,
    string? ProductCode = null, string? PlanCode = null, SubscriptionStatus? Status = null,
    DateTime? CreatedFromUtc = null, DateTime? CreatedToUtc = null, DateTime? EndsFromUtc = null, DateTime? EndsToUtc = null);
public interface ISubscriptionDataStore
{
    Task<IReadOnlyList<DoctorSubscription>> PeriodsAsync(Guid doctorId, CancellationToken ct);
    Task<SubscriptionContract?> ContractAsync(Guid id, CancellationToken ct);
    Task<DoctorSubscription?> EffectivePeriodAsync(Guid contractId, DateTime now, CancellationToken ct);
    Task<DoctorSubscription?> EffectivePracticePeriodAsync(Guid doctorId, Guid practiceId, DateTime now, CancellationToken ct);
    Task<PracticeSubscriptionAllocation?> AllocationAsync(Guid practiceId, CancellationToken ct);
    Task<IReadOnlyList<PracticeSubscriptionAllocation>> AllocationsAsync(Guid contractId, CancellationToken ct);
    Task<SubscriptionUsage> ContractUsageAsync(Guid contractId, CancellationToken ct);
    Task<IReadOnlyList<Wasla.Domain.Practices.ReceptionPracticeAssignment>> AssignmentsAsync(Guid practiceId, CancellationToken ct);
    Task<IReadOnlyList<Guid>> ActiveReceptionIdsAsync(Guid contractId, Guid? exceptPracticeId, CancellationToken ct);
    Task<SubscriptionMutationReceipt?> ReceiptAsync(string key, CancellationToken ct);
    Task<ReservationCommercialProvenance?> ProvenanceAsync(Guid reservationId, CancellationToken ct);
    Task<SubscriptionAllocationSelection?> AllocationSelectionAsync(Guid periodId, CancellationToken ct);
    Task ApplyDueSelectionsAsync(Guid doctorId, DateTime now, Guid actorId, CancellationToken ct);
    Task<bool> ReceptionRetainedAsync(Guid practiceId, Guid receptionId, DateTime now, CancellationToken ct);
    Task<IReadOnlyList<Wasla.Application.Features.Subscriptions.SubscriptionPermissionRule>> PermissionRulesAsync(IReadOnlyCollection<string> permissions, CancellationToken ct);
    Task<IReadOnlyList<Wasla.Application.Features.Subscriptions.SubscriptionReadWindow>> ReadWindowsAsync(Guid doctorId, DateTime now, CancellationToken ct);
    Task<Wasla.Application.Features.Subscriptions.SubscriptionCommitment?> ReadCommitmentAsync(Wasla.Application.Features.Subscriptions.SubscriptionResource resource, CancellationToken ct);
    Task<Wasla.Application.Features.Subscriptions.SubscriptionCommitment?> CommitmentAsync(Wasla.Application.Features.Subscriptions.SubscriptionResource resource, CancellationToken ct);
    Task<SubscriptionCatalogData> CatalogAsync(CancellationToken ct);
    Task<DoctorSubscription?> FindAsync(Guid id, CancellationToken ct);
    Task<Guid?> DoctorIdAsync(Guid id, CancellationToken ct);
    Task<DoctorSubscription?> ScheduledAsync(Guid doctorId, CancellationToken ct);
    Task<DoctorSubscription?> CurrentAsync(Guid doctorId, CancellationToken ct);
    Task<DoctorSubscription?> PendingAsync(Guid doctorId, CancellationToken ct);
    Task<DoctorSubscription?> LatestEndedAsync(Guid doctorId, CancellationToken ct);
    Task<DoctorSubscription?> LatestSelectionAsync(Guid doctorId, CancellationToken ct);
    Task<IReadOnlyList<SubscriptionFeatureSnapshot>> FeaturesAsync(Guid id, CancellationToken ct);
    Task<FeaturePermissionMapping?> PermissionMappingAsync(string permission, CancellationToken ct);
    Task<SubscriptionUsage> UsageAsync(Guid doctorId, CancellationToken ct);
    Task AcquireSubscriptionCapacityLockAsync(Guid doctorId, CancellationToken ct);
    Task AcquireCommercialReadLockAsync(Guid doctorId, CancellationToken ct);
    Task AcquireReminderLockAsync(Guid contractId, CancellationToken ct);
    Task<SubscriptionPayment?> PaymentByKeyAsync(string key, CancellationToken ct);
    Task<IReadOnlyList<SubscriptionPayment>> PaymentsAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<SubscriptionHistory>> HistoryAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<SubscriptionChangeRequest>> ChangesAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<SubscriptionReminderSchedule>> RemindersAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Guid>> DueSubscriptionsAsync(DateTime now, int count, CancellationToken ct);
    Task<(IReadOnlyList<DoctorSubscription> Items, int Total)> ListAsync(SubscriptionListFilter f, CancellationToken ct);
    Task<LegacyTransitionEnrollmentBatch?> BatchAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Guid>> LegacyCandidatesAsync(DateTime cutoff, int count, CancellationToken ct);
    void Add<T>(T entity) where T : class;
    void RemoveFeatures(Guid subscriptionId);
    Task<int> SaveChangesAsync(CancellationToken ct);
}
