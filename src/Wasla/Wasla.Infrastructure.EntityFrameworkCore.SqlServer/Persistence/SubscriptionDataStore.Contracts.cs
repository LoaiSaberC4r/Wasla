using Microsoft.EntityFrameworkCore;
using Wasla.Application.Persistence;
using Wasla.Domain.Practices;
using Wasla.Domain.Subscriptions;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

public sealed partial class SubscriptionDataStore
{
    public async Task<IReadOnlyList<DoctorSubscription>> PeriodsAsync(Guid doctorId, CancellationToken ct) =>
        await db.DoctorSubscriptions.Where(s => s.DoctorId == doctorId).OrderByDescending(s => s.CreatedOnUtc).ThenBy(s => s.Id).ToArrayAsync(ct);
    public Task<SubscriptionContract?> ContractAsync(Guid id, CancellationToken ct) => db.SubscriptionContracts.SingleOrDefaultAsync(s => s.Id == id, ct);
    public Task<DoctorSubscription?> EffectivePeriodAsync(Guid contractId, DateTime now, CancellationToken ct) =>
        db.DoctorSubscriptions.Where(s => s.SubscriptionContractId == contractId &&
            (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Scheduled) && s.StartsAtUtc <= now && now < s.EndsAtUtc &&
            !db.SubscriptionAllocationSelections.Any(selection => selection.SubscriptionId == s.Id && selection.AppliedOnUtc == null &&
                db.SubscriptionRetainedPractices.Any(r => r.SubscriptionId == s.Id && !db.PracticeSubscriptionAllocations.Any(a => a.Id == r.AllocationId && a.ReleasedAtUtc == null && a.SubscriptionContractId == contractId)))).SingleOrDefaultAsync(ct);
    public Task<DoctorSubscription?> EffectivePracticePeriodAsync(Guid doctorId, Guid practiceId, DateTime now, CancellationToken ct) =>
        (from a in db.PracticeSubscriptionAllocations
         join s in db.DoctorSubscriptions on a.SubscriptionContractId equals s.SubscriptionContractId
         where a.DoctorPracticeId == practiceId && a.ReleasedAtUtc == null && a.AllocatedFromUtc <= now && s.DoctorId == doctorId &&
            (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Scheduled) && s.StartsAtUtc <= now && now < s.EndsAtUtc &&
            !db.SubscriptionAllocationSelections.Any(selection => selection.SubscriptionId == s.Id && selection.AppliedOnUtc == null &&
                db.SubscriptionRetainedPractices.Any(r => r.SubscriptionId == s.Id && !db.PracticeSubscriptionAllocations.Any(current => current.Id == r.AllocationId && current.ReleasedAtUtc == null && current.SubscriptionContractId == s.SubscriptionContractId))) &&
            (!db.SubscriptionAllocationSelections.Any(x => x.SubscriptionId == s.Id && x.AppliedOnUtc == null) ||
              db.SubscriptionRetainedPractices.Any(x => x.SubscriptionId == s.Id && x.AllocationId == a.Id))
         select s).SingleOrDefaultAsync(ct);
    public Task<PracticeSubscriptionAllocation?> AllocationAsync(Guid practiceId, CancellationToken ct) =>
        db.PracticeSubscriptionAllocations.SingleOrDefaultAsync(a => a.DoctorPracticeId == practiceId && a.ReleasedAtUtc == null, ct);
    public async Task<IReadOnlyList<PracticeSubscriptionAllocation>> AllocationsAsync(Guid contractId, CancellationToken ct) =>
        await db.PracticeSubscriptionAllocations.Where(a => a.SubscriptionContractId == contractId && a.ReleasedAtUtc == null).ToArrayAsync(ct);
    public async Task<IReadOnlyList<ReceptionPracticeAssignment>> AssignmentsAsync(Guid practiceId, CancellationToken ct) =>
        await db.ReceptionPracticeAssignments.Where(a => a.DoctorPracticeId == practiceId).ToArrayAsync(ct);
    public async Task<IReadOnlyList<Guid>> ActiveReceptionIdsAsync(Guid contractId, Guid? exceptPracticeId, CancellationToken ct) =>
        await (from a in db.PracticeSubscriptionAllocations
               join assignment in db.ReceptionPracticeAssignments on a.DoctorPracticeId equals assignment.DoctorPracticeId
               join reception in db.Receptions on assignment.ReceptionId equals reception.Id
               join user in db.ApplicationUsers on reception.ApplicationUserId equals user.Id
               where a.SubscriptionContractId == contractId && a.ReleasedAtUtc == null && a.DoctorPracticeId != exceptPracticeId && assignment.IsActive && user.IsActive
               select reception.Id).Distinct().ToArrayAsync(ct);
    public async Task<SubscriptionUsage> ContractUsageAsync(Guid contractId, CancellationToken ct) => new(
        await db.PracticeSubscriptionAllocations.CountAsync(a => a.SubscriptionContractId == contractId && a.ReleasedAtUtc == null, ct),
        (await ActiveReceptionIdsAsync(contractId, null, ct)).Count);
    public Task<SubscriptionMutationReceipt?> ReceiptAsync(string key, CancellationToken ct) =>
        db.SubscriptionMutationReceipts.SingleOrDefaultAsync(r => r.IdempotencyKey == key, ct);
    public Task<ReservationCommercialProvenance?> ProvenanceAsync(Guid reservationId, CancellationToken ct) =>
        db.ReservationCommercialProvenances.SingleOrDefaultAsync(p => p.Id == reservationId, ct);
    public Task<SubscriptionAllocationSelection?> AllocationSelectionAsync(Guid periodId, CancellationToken ct) =>
        db.SubscriptionAllocationSelections.SingleOrDefaultAsync(s => s.SubscriptionId == periodId, ct);
    public async Task<bool> ReceptionRetainedAsync(Guid practiceId, Guid receptionId, DateTime now, CancellationToken ct) =>
        !await (from a in db.PracticeSubscriptionAllocations
                join period in db.DoctorSubscriptions on a.SubscriptionContractId equals period.SubscriptionContractId
                join selection in db.SubscriptionAllocationSelections on period.Id equals selection.SubscriptionId
                where a.DoctorPracticeId == practiceId && a.ReleasedAtUtc == null && period.StartsAtUtc <= now && selection.AppliedOnUtc == null &&
                    !db.SubscriptionRetainedReceptions.Any(r => r.SubscriptionId == period.Id && r.DoctorPracticeId == practiceId && r.ReceptionId == receptionId)
                select selection.Id).AnyAsync(ct);
    public async Task ApplyDueSelectionsAsync(Guid doctorId, DateTime now, Guid actorId, CancellationToken ct)
    {
        var due = await (from selection in db.SubscriptionAllocationSelections
                         join period in db.DoctorSubscriptions on selection.SubscriptionId equals period.Id
                         where period.DoctorId == doctorId && period.StartsAtUtc <= now && selection.AppliedOnUtc == null &&
                            (period.Status == SubscriptionStatus.Active || period.Status == SubscriptionStatus.Scheduled || period.Status == SubscriptionStatus.Expired)
                         orderby period.StartsAtUtc
                         select new { Selection = selection, Period = period }).ToArrayAsync(ct);
        foreach (var item in due)
        {
            var allocations = await AllocationsAsync(item.Period.SubscriptionContractId, ct);
            if (item.Selection.Practices.Any(p => !allocations.Any(a => a.Id == p.AllocationId && a.DoctorPracticeId == p.PracticeId)))
            {
                if (!await db.SubscriptionMigrationIssues.AnyAsync(x => x.ResourceId == item.Period.Id && x.IssueCode == "RenewalSelectionConflict", ct))
                    db.Add(new SubscriptionMigrationIssue(item.Period.Id, "RenewalSelectionConflict", now));
                continue; // No partial reconciliation; query-time eligibility also fails closed.
            }
            foreach (var allocation in allocations)
            {
                var selected = item.Selection.Practices.SingleOrDefault(p => p.AllocationId == allocation.Id && p.PracticeId == allocation.DoctorPracticeId);
                // Never reclaim a moved allocation. Selections identify the original allocation, not just a practice.
                if (allocation.AllocatedFromUtc >= item.Period.StartsAtUtc) continue;
                if (selected is null) allocation.Release(item.Period.StartsAtUtc!.Value, actorId, "Renewal allocation selection");
                else allocation.MarkCompliant();
                foreach (var assignment in await AssignmentsAsync(allocation.DoctorPracticeId, ct))
                    if (assignment.IsActive && (selected is null || !selected.ReceptionIds.Contains(assignment.ReceptionId))) assignment.Deactivate(actorId);
            }
            item.Selection.MarkApplied(now);
        }
        if (due.Length != 0) await db.SaveChangesAsync(ct);
    }
}
