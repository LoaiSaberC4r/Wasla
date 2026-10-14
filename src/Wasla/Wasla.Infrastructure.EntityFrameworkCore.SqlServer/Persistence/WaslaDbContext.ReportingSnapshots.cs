using System.Text.Json;
using BuildingBlock.Domain.EntitiesHelper;
using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Reporting;
using Wasla.Domain.Practices;
using Wasla.Domain.Reporting;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

public sealed partial class WaslaDbContext
{
    public async Task<int> ReconcileReportingPoliciesAsync(IReadOnlyList<Guid> practiceIds, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(practiceIds);
        if (ChangeTracker.HasChanges()) throw new InvalidOperationException("Policy reconciliation requires a clean unit of work.");
        if (practiceIds.Count > 100) throw new ArgumentOutOfRangeException(nameof(practiceIds), "A policy reconciliation batch is limited to 100 practices.");
        await using var transaction = Database.CurrentTransaction == null ? await Database.BeginTransactionAsync(ct) : null;
        await CaptureReportingPoliciesAsync(practiceIds.Distinct().Order().ToArray(), ct);
        var count = ChangeTracker.Entries<PracticeReportingPolicySnapshot>().Count(e => e.State == EntityState.Added);
        await SaveChangesAsync(ct); if (transaction != null) await transaction.CommitAsync(ct); return count;
    }
    private Guid[] ChangedReportingPractices() => ChangeTracker.Entries()
        .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted &&
            e.Entity is DoctorPractice or DoctorPracticeConfiguration or DoctorPracticeSchedulePeriod or
                DoctorPracticeScheduleException or DoctorPracticeSegment or DoctorPracticeVisitType)
        .Select(e => e.Entity is DoctorPractice practice ? practice.Id : (Guid)e.Property("DoctorPracticeId").CurrentValue!)
        .Distinct().Order().ToArray();

    private async Task CaptureReportingPoliciesAsync(Guid[] practiceIds, CancellationToken ct)
    {
        foreach (var id in practiceIds)
        {
            if (Database.IsSqlServer())
            {
                var resource = $"Wasla:ReportingPolicy:{id:N}";
                await Database.ExecuteSqlInterpolatedAsync($"DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000; IF @result < 0 THROW 51019, 'Reporting policy coordination is unavailable.', 1;", ct);
            }
            var practice = ChangeTracker.Entries<DoctorPractice>().Select(e => e.Entity).SingleOrDefault(p => p.Id == id)
                ?? await DoctorPractices.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct);
            if (practice is null || Entry(practice).State == EntityState.Deleted) continue;
            var configuration = (await MergedReportingEntitiesAsync<DoctorPracticeConfiguration>(id, ct)).SingleOrDefault();
            if (configuration is null) continue;
            var periods = await MergedReportingEntitiesAsync<DoctorPracticeSchedulePeriod>(id, ct);
            var exceptions = await MergedReportingEntitiesAsync<DoctorPracticeScheduleException>(id, ct);
            var segments = await MergedReportingEntitiesAsync<DoctorPracticeSegment>(id, ct);
            var types = await MergedReportingEntitiesAsync<DoctorPracticeVisitType>(id, ct);
            var owner = ChangeTracker.Entries<Wasla.Domain.Doctors.Doctor>().Select(e => e.Entity).SingleOrDefault(d => d.Id == practice.DoctorId)?.ApplicationUserId
                ?? await Doctors.AsNoTracking().Where(d => d.Id == practice.DoctorId).Select(d => d.ApplicationUserId).SingleAsync(ct);
            var policy = new PracticeReportingPolicy(id, practice.DoctorId, owner, practice.IsActive, configuration.TimeZoneId,
                configuration.MaximumDailyPatients, periods.OrderBy(p => p.Id).Select(p => new ReportingSchedulePeriod(p.Id, p.DayOfWeek, p.StartTime, p.EndTime, p.SlotDurationMinutes)).ToArray(),
                exceptions.OrderBy(e => e.Id).Select(e => new ReportingScheduleOverride(e.Id, e.Date, e.Type, e.StartTime, e.EndTime, e.SlotDurationMinutes)).ToArray(),
                segments.OrderBy(s => s.Id).Select(s => new ReportingSegmentPolicy(s.Id, s.IsActive, s.ReservedDailyQuota)).ToArray(),
                types.OrderBy(t => t.Id).Select(t => new ReportingVisitTypePolicy(t.Id, t.IsActive)).ToArray());
            var json = JsonSerializer.Serialize(policy);
            var last = await Set<PracticeReportingPolicySnapshot>().AsNoTracking().Where(s => s.PracticeId == id).OrderByDescending(s => s.RevisionNumber).FirstOrDefaultAsync(ct);
            if (last?.PolicyJson == json) continue;
            var pending = ChangeTracker.Entries<PracticeReportingPolicySnapshot>().Where(e => e.State == EntityState.Added && e.Entity.PracticeId == id).Select(e => e.Entity.RevisionNumber).DefaultIfEmpty(0).Max();
            var revision = Math.Max(last?.RevisionNumber ?? 0, pending) + 1;
            Set<PracticeReportingPolicySnapshot>().Add(new(Guid.NewGuid(), practice.DoctorId, id, revision, json, reportingClock?.UtcNow ?? DateTime.UtcNow));
        }
    }
    private async Task<TEntity[]> MergedReportingEntitiesAsync<TEntity>(Guid practiceId, CancellationToken ct) where TEntity : Entity<Guid>
    {
        var rows = (await Set<TEntity>().AsNoTracking().Where(e => EF.Property<Guid>(e, "DoctorPracticeId") == practiceId).ToArrayAsync(ct)).ToDictionary(e => e.Id);
        foreach (var entry in ChangeTracker.Entries<TEntity>().Where(e => (Guid)e.Property("DoctorPracticeId").CurrentValue! == practiceId))
        {
            if (entry.State == EntityState.Deleted) rows.Remove(entry.Entity.Id);
            else if (entry.State is EntityState.Added or EntityState.Modified) rows[entry.Entity.Id] = entry.Entity;
        }
        return rows.Values.ToArray();
    }
}
