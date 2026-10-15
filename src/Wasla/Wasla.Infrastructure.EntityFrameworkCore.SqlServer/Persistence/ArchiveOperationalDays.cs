using Wasla.Application.Features.Reservations;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using BuildingBlock.Application.Time;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Domain.Archives;
using Wasla.Domain.Practices;
using Wasla.Domain.Tickets;
using Wasla.Domain.Reservations;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

public sealed class ArchiveOperationalDays(WaslaDbContext db, IDateTimeProvider clock, ITicketQueueLock queueLock, IReservationProjectionInvalidationOutbox projectionOutbox)
{
    public static async Task ObserveAsync(WaslaDbContext db, Guid practiceId, DateOnly businessDate, DateTime now, CancellationToken ct)
    {
        var existing = await db.PracticeOperationalDays.SingleOrDefaultAsync(x => x.DoctorPracticeId == practiceId && x.BusinessDate == businessDate, ct);
        if (existing?.ClosedAtUtc != null || existing != null && now >= existing.EndsAtUtc) return;
        var configuration = await db.DoctorPracticeConfigurations.AsNoTracking().SingleOrDefaultAsync(x => x.DoctorPracticeId == practiceId, ct);
        if (configuration == null || !TimeZoneInfo.TryFindSystemTimeZoneById(configuration.TimeZoneId, out var zone)) return;
        var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(now, DateTimeKind.Utc), zone));
        if (businessDate < localToday && existing == null) return; // Never manufacture historical scheduling evidence.
        var periods = await db.DoctorPracticeSchedulePeriods.AsNoTracking().Where(x => x.DoctorPracticeId == practiceId).ToArrayAsync(ct);
        var exceptions = await db.DoctorPracticeScheduleExceptions.AsNoTracking().Where(x => x.DoctorPracticeId == practiceId && x.Date == businessDate).ToArrayAsync(ct);
        var effective = DoctorPracticeAvailabilityCalculator.GetEffectiveWorkingPeriods(businessDate, periods, exceptions);
        if (effective.Count == 0)
        {
            if (existing?.Invalidate(now) == true) db.ArchiveAuditEvents.Add(new("OperationalDayEvidenceInvalidated", "OperationalDay", existing.Id, practiceId, null, now, outcome: "Failed"));
            return;
        }
        var localEnd = DateTime.SpecifyKind(businessDate.ToDateTime(effective.Max(x => x.EndTime)), DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(localEnd))
        {
            if (existing?.Invalidate(now) == true) db.ArchiveAuditEvents.Add(new("OperationalDayEvidenceInvalidated", "OperationalDay", existing.Id, practiceId, null, now, outcome: "Failed"));
            return;
        }
        var end = TimeZoneInfo.ConvertTimeToUtc(localEnd, zone); // Existing ambiguous-time standard-offset policy.
        if (existing == null && now >= end) return;
        var evidence = "Wasla.OperationalDay.v1|" + configuration.TimeZoneId + "|" + System.Text.Json.JsonSerializer.Serialize(effective);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(evidence)));
        var revised = existing?.Revise(configuration.TimeZoneId, end, hash, now) == true;
        if (existing == null) db.PracticeOperationalDays.Add(PracticeOperationalDay.Observe(practiceId, businessDate, configuration.TimeZoneId, end, hash, now));
        if (existing == null || revised) db.ArchiveAuditEvents.Add(new(existing == null ? "OperationalDayObserved" : "OperationalDayRevised", "OperationalDay", existing?.Id, practiceId, null, now));
    }

    public async Task ProcessAsync(CancellationToken ct)
    {
        var now = DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc);
        await using (var observationTransaction = await db.Database.BeginTransactionAsync(ct))
        {
            if (db.Database.IsSqlServer()) await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sp_getapplock @Resource=N'Wasla:OperationalObservation', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=1000; IF @r<0 THROW 51000, 'Archive.ObservationBusy',1;",ct);
            var progress = await db.ArchiveProgress.SingleOrDefaultAsync(x => x.Id == "OperationalObservation", ct);
            if (progress == null) { progress = new("OperationalObservation"); db.ArchiveProgress.Add(progress); }
            var query = db.DoctorPracticeConfigurations.AsNoTracking();
            if (progress.LastId is { } last) query = query.Where(x => x.DoctorPracticeId.CompareTo(last) > 0);
            var configurations = await query.OrderBy(x => x.DoctorPracticeId).Take(100).ToArrayAsync(ct);
            foreach (var config in configurations)
            {
                progress.Advance(null, config.DoctorPracticeId);
                if (!TimeZoneInfo.TryFindSystemTimeZoneById(config.TimeZoneId, out var zone)) continue;
                var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now, zone));
                await queueLock.AcquirePracticeDayAsync(config.DoctorPracticeId, today, ct);
                await ObserveAsync(db, config.DoctorPracticeId, today, now, ct);
            }
            if (configurations.Length == 0) progress.Advance(null, null);
            await db.SaveChangesAsync(ct); await observationTransaction.CommitAsync(ct); db.ChangeTracker.Clear();
        }
        var legacyProgress = await db.ArchiveProgress.SingleOrDefaultAsync(x => x.Id == "LegacyClosure", ct);
        if (legacyProgress == null) { legacyProgress = new("LegacyClosure"); db.ArchiveProgress.Add(legacyProgress); await db.SaveChangesAsync(ct); }
        var legacyDate = legacyProgress.LastDate; var legacyId = legacyProgress.LastId;
        var legacy = await db.Tickets.AsNoTracking().Where(x => (legacyDate == null || x.BusinessDate > legacyDate || x.BusinessDate == legacyDate && x.Id.CompareTo(legacyId!.Value) > 0)).Where(x => (x.Status == TicketStatus.Waiting || x.Status == TicketStatus.Called || x.Status == TicketStatus.NoShow && x.IsOperationallyOpen) && x.BusinessDate < DateOnly.FromDateTime(now) &&
            !db.PracticeOperationalDays.Any(d => d.DoctorPracticeId == x.DoctorPracticeId && d.BusinessDate == x.BusinessDate))
            .OrderBy(x => x.BusinessDate).ThenBy(x => x.Id).Take(100).Select(x => new { x.Id, x.DoctorPracticeId, x.BusinessDate, x.PracticeTimeZoneIdSnapshot }).ToArrayAsync(ct);
        legacyProgress.Advance(legacy.LastOrDefault()?.BusinessDate, legacy.LastOrDefault()?.Id);
        await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        foreach (var item in legacy)
        {
            if (!TimeZoneInfo.TryFindSystemTimeZoneById(item.PracticeTimeZoneIdSnapshot, out var zone) || item.BusinessDate >= DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now, zone))) continue;
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await queueLock.AcquirePracticeDayAsync(item.DoctorPracticeId, item.BusinessDate, ct);
            var row = await db.Tickets.Include(x => x.History).SingleOrDefaultAsync(x => x.Id == item.Id, ct);
            if (row != null && !await db.MedicalEncounters.AnyAsync(e => e.TicketId == row.Id && e.Status == Wasla.Domain.Clinical.EncounterStatus.InProgress, ct))
            {
                if (row.Status is TicketStatus.Waiting or TicketStatus.Called) row.CancelForOperationalDay(now);
                else if (row.FinalizeNoShowForClosedDay(now)) db.ArchiveAuditEvents.Add(new("NoShowFinalized", "Ticket", row.Id, row.DoctorPracticeId, null, now));
            }
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); db.ChangeTracker.Clear();
        }
        var candidates = await db.PracticeOperationalDays.AsNoTracking().Where(x => x.EvidenceVerified && x.EndsAtUtc <= now &&
            (x.ClosedAtUtc == null || db.Tickets.Any(t => t.DoctorPracticeId == x.DoctorPracticeId && t.BusinessDate == x.BusinessDate &&
                (t.Status == TicketStatus.Waiting || t.Status == TicketStatus.Called || t.Status == TicketStatus.NoShow && t.IsOperationallyOpen) && !db.MedicalEncounters.Any(e => e.TicketId == t.Id)) ||
                db.Reservations.Any(r => r.DoctorPracticeId == x.DoctorPracticeId && r.BusinessDate == x.BusinessDate && r.Status == ReservationStatus.Active)))
            .OrderBy(x => x.EndsAtUtc).ThenBy(x => x.Id).Take(25).Select(x => new { x.DoctorPracticeId, x.BusinessDate }).ToArrayAsync(ct);
        foreach (var candidate in candidates)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await queueLock.AcquirePracticeDayAsync(candidate.DoctorPracticeId, candidate.BusinessDate, ct);
            var day = await db.PracticeOperationalDays.SingleAsync(x => x.DoctorPracticeId == candidate.DoctorPracticeId && x.BusinessDate == candidate.BusinessDate, ct);
            if (!day.EvidenceVerified || day.EndsAtUtc > now) { await transaction.CommitAsync(ct); continue; }
            var tickets = await db.Tickets.Include(x => x.History).Where(x => x.DoctorPracticeId == day.DoctorPracticeId && x.BusinessDate == day.BusinessDate &&
                (x.Status == TicketStatus.Waiting || x.Status == TicketStatus.Called) && !db.MedicalEncounters.Any(e => e.TicketId == x.Id)).OrderBy(x => x.Id).Take(100).ToArrayAsync(ct);
            foreach (var ticket in tickets) ticket.CancelForOperationalDay(now);
            var reservations = await db.Reservations.Include(x => x.History).Where(x => x.DoctorPracticeId == day.DoctorPracticeId && x.BusinessDate == day.BusinessDate && x.Status == ReservationStatus.Active)
                .OrderBy(x => x.Id).Take(100).ToArrayAsync(ct);
            foreach (var reservation in reservations)
                if (reservation.Expire(now).IsSuccess) await QueueExpiryAsync(reservation, ct);
            var noShows = await db.Tickets.Where(x => x.DoctorPracticeId == day.DoctorPracticeId && x.BusinessDate == day.BusinessDate && x.Status == TicketStatus.NoShow && x.IsOperationallyOpen)
                .OrderBy(x => x.Id).Take(100).ToArrayAsync(ct);
            foreach (var noShow in noShows)
                if (noShow.FinalizeNoShowForClosedDay(now)) db.ArchiveAuditEvents.Add(new("NoShowFinalized", "Ticket", noShow.Id, noShow.DoctorPracticeId, null, now));
            // Drain large days over cycles; closed eligibility begins only once cancellations/expiry are complete.
            if (day.ClosedAtUtc == null && tickets.Length < 100 && reservations.Length < 100 && noShows.Length < 100)
            { day.Close(now); db.ArchiveAuditEvents.Add(new("OperationalDayClosed", "OperationalDay", day.Id, day.DoctorPracticeId, null, now)); }
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            db.ChangeTracker.Clear();
        }
    }
    private Task QueueExpiryAsync(Reservation reservation, CancellationToken ct) => projectionOutbox.QueueAsync(new QueueReservationProjectionInvalidation(
        $"reservation-expired:{reservation.Id:N}:{reservation.History.Last().Id:N}", reservation.DoctorPracticeId, reservation.DoctorId, RefreshAvailability:true,RefreshPopularity:true), ct);

}
