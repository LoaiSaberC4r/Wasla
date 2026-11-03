using Microsoft.EntityFrameworkCore;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Subscriptions;
using Wasla.Application.Features.Subscriptions;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

public sealed class SubscriptionDataStore(WaslaDbContext db) : ISubscriptionDataStore
{
    public async Task<IReadOnlyList<SubscriptionPermissionRule>> PermissionRulesAsync(IReadOnlyCollection<string> permissions, CancellationToken ct) =>
        await (from mapping in db.FeaturePermissionMappings.AsNoTracking()
               join permission in db.Permissions on mapping.PermissionId equals permission.Id
               join feature in db.CommercialFeatures on mapping.FeatureId equals feature.Id
               where permissions.Contains(permission.Name)
               select new SubscriptionPermissionRule(permission.Name, feature.Code, mapping.RequiredAccessLevel)).ToArrayAsync(ct);

    public async Task<IReadOnlyList<SubscriptionReadWindow>> ReadWindowsAsync(Guid doctorId, DateTime now, CancellationToken ct) =>
        await (from subscription in db.DoctorSubscriptions.AsNoTracking()
               join feature in db.SubscriptionFeatureSnapshots.AsNoTracking() on subscription.Id equals feature.SubscriptionId
               where subscription.DoctorId == doctorId && subscription.StartsAtUtc != null && subscription.StartsAtUtc <= now &&
                   subscription.EndsAtUtc != null && subscription.Status != SubscriptionStatus.Pending && subscription.Status != SubscriptionStatus.Cancelled &&
                   feature.AccessLevel >= FeatureAccessLevel.ReadOnly
               orderby feature.FeatureCodeSnapshot, subscription.StartsAtUtc, subscription.Id
               select new SubscriptionReadWindow(feature.FeatureCodeSnapshot,
                   subscription.SubscriptionKind == SubscriptionKind.LegacyTransition ? null : subscription.StartsAtUtc,
                   subscription.Status == SubscriptionStatus.Superseded && subscription.ModifiedOnUtc != null && subscription.ModifiedOnUtc < subscription.EndsAtUtc
                        ? subscription.ModifiedOnUtc.Value : subscription.EndsAtUtc!.Value)).ToArrayAsync(ct);

    public Task<SubscriptionCommitment?> ReadCommitmentAsync(SubscriptionResource resource, CancellationToken ct) => resource.Kind switch
    {
        SubscriptionResourceKind.Reservation => db.AllReservations.AsNoTracking().Where(r => r.Id == resource.Id)
            .Select(r => new SubscriptionCommitment(r.DoctorId, r.CreatedOnUtc)).SingleOrDefaultAsync(ct),
        SubscriptionResourceKind.Ticket => db.AllTickets.AsNoTracking().Where(t => t.Id == resource.Id)
            .Select(t => new SubscriptionCommitment(t.DoctorId, t.CreatedOnUtc)).SingleOrDefaultAsync(ct),
        _ => CommitmentAsync(resource, ct)
    };
    public async Task<SubscriptionCommitment?> CommitmentAsync(SubscriptionResource resource, CancellationToken ct)
    {
        if (resource.Kind == SubscriptionResourceKind.Reservation)
            return await db.Reservations.Where(r => r.Id == resource.Id).Select(r => new SubscriptionCommitment(r.DoctorId, r.CreatedOnUtc)).SingleOrDefaultAsync(ct);
        Guid? ticketId = resource.Kind == SubscriptionResourceKind.Ticket ? resource.Id : null;
        if (resource.Kind == SubscriptionResourceKind.Payment)
            ticketId = await db.Payments.Where(p => p.Id == resource.Id).Select(p => (Guid?)p.TicketId).SingleOrDefaultAsync(ct);
        if (resource.Kind == SubscriptionResourceKind.Refund)
            ticketId = await (from r in db.Refunds join p in db.Payments on r.PaymentId equals p.Id where r.Id == resource.Id select (Guid?)p.TicketId).SingleOrDefaultAsync(ct);
        if (ticketId is null)
        {
            Guid? encounterId = resource.Kind switch
            {
                SubscriptionResourceKind.Encounter => resource.Id,
                SubscriptionResourceKind.Prescription => await db.Prescriptions.Where(p => p.Id == resource.Id).Select(p => (Guid?)p.MedicalEncounterId).SingleOrDefaultAsync(ct),
                SubscriptionResourceKind.LabRequest => await db.LabRequests.Where(p => p.Id == resource.Id).Select(p => (Guid?)p.MedicalEncounterId).SingleOrDefaultAsync(ct),
                SubscriptionResourceKind.RadiologyRequest => await db.RadiologyRequests.Where(p => p.Id == resource.Id).Select(p => (Guid?)p.MedicalEncounterId).SingleOrDefaultAsync(ct),
                SubscriptionResourceKind.LabResult => await (from r in db.LabResults join q in db.LabRequests on r.RequestId equals q.Id where r.Id == resource.Id select (Guid?)q.MedicalEncounterId).SingleOrDefaultAsync(ct),
                SubscriptionResourceKind.RadiologyResult => await (from r in db.RadiologyResults join q in db.RadiologyRequests on r.RequestId equals q.Id where r.Id == resource.Id select (Guid?)q.MedicalEncounterId).SingleOrDefaultAsync(ct),
                SubscriptionResourceKind.LabSubmission => await (from r in db.PatientLabResultSubmissions join q in db.LabRequests on r.RequestId equals q.Id where r.Id == resource.Id select (Guid?)q.MedicalEncounterId).SingleOrDefaultAsync(ct),
                SubscriptionResourceKind.RadiologySubmission => await (from r in db.PatientRadiologyResultSubmissions join q in db.RadiologyRequests on r.RequestId equals q.Id where r.Id == resource.Id select (Guid?)q.MedicalEncounterId).SingleOrDefaultAsync(ct),
                _ => null
            };
            ticketId = await db.MedicalEncounters.Where(e => e.Id == encounterId).Select(e => (Guid?)e.TicketId).SingleOrDefaultAsync(ct);
        }
        return await (from t in db.Tickets
            join r in db.Reservations on t.ReservationId equals r.Id into reservations
            from r in reservations.DefaultIfEmpty()
            where t.Id == ticketId
            select new SubscriptionCommitment(t.DoctorId, r == null ? t.CreatedOnUtc : r.CreatedOnUtc)).SingleOrDefaultAsync(ct);
    }
    public async Task<SubscriptionCatalogData> CatalogAsync(CancellationToken ct) => new(
        await db.CommercialProducts.AsNoTracking().OrderBy(x => x.DisplayOrder).ToArrayAsync(ct),
        await db.CommercialPlans.AsNoTracking().OrderBy(x => x.DisplayOrder).ToArrayAsync(ct),
        await db.CommercialFeatures.AsNoTracking().OrderBy(x => x.DisplayOrder).ToArrayAsync(ct),
        await db.PlanFeatureEntitlements.AsNoTracking().ToArrayAsync(ct));
    public Task<DoctorSubscription?> FindAsync(Guid id, CancellationToken ct) => db.DoctorSubscriptions.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<Guid?> DoctorIdAsync(Guid id, CancellationToken ct) => db.DoctorSubscriptions.AsNoTracking().Where(s => s.Id == id).Select(s => (Guid?)s.DoctorId).SingleOrDefaultAsync(ct);
    public Task<DoctorSubscription?> ScheduledAsync(Guid doctorId, CancellationToken ct) => db.DoctorSubscriptions.SingleOrDefaultAsync(s => s.DoctorId == doctorId && s.Status == SubscriptionStatus.Scheduled, ct);
    public Task<DoctorSubscription?> CurrentAsync(Guid doctorId, CancellationToken ct) => db.DoctorSubscriptions
        .Where(x => x.DoctorId == doctorId && x.Status == SubscriptionStatus.Active).SingleOrDefaultAsync(ct);
    public Task<DoctorSubscription?> PendingAsync(Guid doctorId, CancellationToken ct) => db.DoctorSubscriptions
        .SingleOrDefaultAsync(x => x.DoctorId == doctorId && x.Status == SubscriptionStatus.Pending, ct);
    public Task<DoctorSubscription?> LatestEndedAsync(Guid doctorId, CancellationToken ct) => db.DoctorSubscriptions
        .Where(x => x.DoctorId == doctorId && x.StartsAtUtc != null).OrderByDescending(x => x.StartsAtUtc).ThenByDescending(x => x.CreatedOnUtc).FirstOrDefaultAsync(ct);
    public Task<DoctorSubscription?> LatestSelectionAsync(Guid doctorId, CancellationToken ct) => db.DoctorSubscriptions
        .Where(x => x.DoctorId == doctorId).OrderByDescending(x => x.CreatedOnUtc).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
    public async Task<IReadOnlyList<SubscriptionFeatureSnapshot>> FeaturesAsync(Guid id, CancellationToken ct) =>
        await db.SubscriptionFeatureSnapshots.Where(x => x.SubscriptionId == id).ToArrayAsync(ct);
    public Task<FeaturePermissionMapping?> PermissionMappingAsync(string permission, CancellationToken ct) =>
        (from m in db.FeaturePermissionMappings join p in db.Permissions on m.PermissionId equals p.Id where p.Name == permission select m).SingleOrDefaultAsync(ct);
    public async Task<SubscriptionUsage> UsageAsync(Guid doctorId, CancellationToken ct) => new(
        await db.DoctorPractices.CountAsync(x => x.DoctorId == doctorId, ct),
        await (from r in db.Receptions join u in db.ApplicationUsers on r.ApplicationUserId equals u.Id
               where r.OwnerDoctorId == doctorId && u.IsActive select r.Id).Distinct().CountAsync(ct));
    public async Task AcquireSubscriptionCapacityLockAsync(Guid doctorId, CancellationToken ct)
    {
        // SQLite uses the existing single-writer transaction convention. SQL Server locks live until commit/rollback.
        if (db.Database.IsSqlServer())
        {
            if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Subscription locks require a transaction.");
            var resource = $"Wasla:Subscription:Doctor:{doctorId:N}";
            await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r = sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r < 0 THROW 51060, 'Subscription lock unavailable.', 1;", ct);
        }
        // Authorization runs before MediatR opens the write transaction. Refresh its tracked snapshots after admission.
        // Never overwrite business changes already made within the current transaction (e.g. doctor approval).
        foreach (var entry in db.ChangeTracker.Entries<DoctorSubscription>().Where(e => e.Entity.DoctorId == doctorId && e.State == EntityState.Unchanged).ToArray())
            await entry.ReloadAsync(ct);
        foreach (var entry in db.ChangeTracker.Entries<Wasla.Domain.Doctors.Doctor>().Where(e => e.Entity.Id == doctorId && e.State == EntityState.Unchanged).ToArray())
            await entry.ReloadAsync(ct);
    }
    public async Task<SubscriptionPayment?> PaymentByKeyAsync(string key, CancellationToken ct) =>
        db.SubscriptionPayments.Local.SingleOrDefault(x => x.IdempotencyKey == key) ?? await db.SubscriptionPayments.SingleOrDefaultAsync(x => x.IdempotencyKey == key, ct);
    public async Task<IReadOnlyList<SubscriptionPayment>> PaymentsAsync(Guid id, CancellationToken ct) => await db.SubscriptionPayments.Where(x => x.SubscriptionId == id).OrderBy(x => x.PaidAtUtc).ToArrayAsync(ct);
    public async Task<IReadOnlyList<SubscriptionHistory>> HistoryAsync(Guid id, CancellationToken ct) => await db.SubscriptionHistories.Where(x => x.SubscriptionId == id).OrderBy(x => x.OccurredOnUtc).ToArrayAsync(ct);
    public async Task<IReadOnlyList<SubscriptionChangeRequest>> ChangesAsync(Guid id, CancellationToken ct) => await db.SubscriptionChangeRequests.Where(x => x.SubscriptionId == id).OrderByDescending(x => x.RequestedOnUtc).ToArrayAsync(ct);
    public async Task<IReadOnlyList<SubscriptionReminderSchedule>> RemindersAsync(Guid id, CancellationToken ct) => await db.SubscriptionReminderSchedules.Where(x => x.SubscriptionId == id).ToArrayAsync(ct);
    public async Task<IReadOnlyList<Guid>> DueSubscriptionsAsync(DateTime now, int count, CancellationToken ct) => await db.DoctorSubscriptions
        .Where(s => s.Status == SubscriptionStatus.Scheduled && s.StartsAtUtc <= now || s.Status == SubscriptionStatus.Active && (s.EndsAtUtc <= now || db.SubscriptionReminderSchedules.Any(r => r.SubscriptionId == s.Id && r.Status == SubscriptionReminderStatus.Pending && r.DueAtUtc <= now)))
        .OrderBy(s => s.EndsAtUtc).Take(count).Select(s => s.Id).ToArrayAsync(ct);
    public async Task<(IReadOnlyList<DoctorSubscription> Items, int Total)> ListAsync(SubscriptionListFilter f, CancellationToken ct)
    {
        var query = db.DoctorSubscriptions.AsNoTracking().AsQueryable();
        if (f.Status.HasValue) query = query.Where(s => s.Status == f.Status);
        if (f.ProductCode is not null) query = query.Where(s => db.CommercialProducts.Any(p => p.Id == s.ProductId && p.Code == f.ProductCode));
        if (f.PlanCode is not null) query = query.Where(s => db.CommercialPlans.Any(p => p.Id == s.PlanId && p.Code == f.PlanCode));
        if (!string.IsNullOrWhiteSpace(f.Search)) query = query.Where(s => db.Doctors.Any(d => d.Id == s.DoctorId && (d.NameAr.Contains(f.Search) || d.NameEn != null && d.NameEn.Contains(f.Search) || db.ApplicationUsers.Any(u => u.Id == d.ApplicationUserId && u.Email.Contains(f.Search)))));
        if (f.CreatedFromUtc.HasValue) query = query.Where(s => s.CreatedOnUtc >= f.CreatedFromUtc);
        if (f.CreatedToUtc.HasValue) query = query.Where(s => s.CreatedOnUtc < f.CreatedToUtc);
        if (f.EndsFromUtc.HasValue) query = query.Where(s => s.EndsAtUtc >= f.EndsFromUtc);
        if (f.EndsToUtc.HasValue) query = query.Where(s => s.EndsAtUtc < f.EndsToUtc);
        var total = await query.CountAsync(ct);
        return (await query.OrderByDescending(s => s.CreatedOnUtc).ThenBy(s => s.Id).Skip((f.PageNumber - 1) * f.PageSize).Take(f.PageSize).ToArrayAsync(ct), total);
    }
    public Task<LegacyTransitionEnrollmentBatch?> BatchAsync(Guid id, CancellationToken ct) => db.LegacyTransitionEnrollmentBatches.SingleOrDefaultAsync(b => b.Id == id, ct);
    public async Task<IReadOnlyList<Guid>> LegacyCandidatesAsync(DateTime cutoff, int count, CancellationToken ct) => await db.Doctors
        .Where(d => d.ApprovalStatus == DoctorApprovalStatus.Approved && d.CreatedOnUtc < cutoff &&
            !db.DoctorSubscriptions.Any(s => s.DoctorId == d.Id && (s.SubscriptionKind != SubscriptionKind.Paid || s.Status != SubscriptionStatus.Pending)))
        .OrderBy(d => d.Id).Take(count).Select(d => d.Id).ToArrayAsync(ct);
    public void Add<T>(T entity) where T : class => db.Add(entity);
    public void RemoveFeatures(Guid subscriptionId) => db.SubscriptionFeatureSnapshots.RemoveRange(db.SubscriptionFeatureSnapshots.Where(x => x.SubscriptionId == subscriptionId));
    public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
