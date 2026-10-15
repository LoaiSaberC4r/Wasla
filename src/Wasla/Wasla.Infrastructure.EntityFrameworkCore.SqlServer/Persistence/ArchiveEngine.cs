using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using BuildingBlock.Application.Time;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Domain.Archives;
using Wasla.Domain.Clinical;
using Wasla.Domain.Payments;
using Wasla.Domain.Reservations;
using Wasla.Domain.Tickets;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Options;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

public sealed class ArchiveEngine(WaslaDbContext db, ITicketQueueLock queueLock, IDateTimeProvider clock, IOptions<ArchiveOptions> options)
{
    private bool _cycleReady;
    public async Task<bool> ReadyAsync(CancellationToken ct)
    {
        if (!options.Value.ReadModelsVerified || !db.Database.IsSqlServer()) return false;
        if (await db.Tickets.AnyAsync(x => !db.TicketRegistry.Any(r => r.Id == x.Id), ct) ||
            await db.Reservations.AnyAsync(x => !db.ReservationRegistry.Any(r => r.Id == x.Id), ct)) return false;
        var opened = db.Database.GetDbConnection().State != ConnectionState.Open;
        if (opened) await db.Database.OpenConnectionAsync(ct);
        using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = ReadinessSql();
        if (db.Database.CurrentTransaction is { } current) command.Transaction = current.GetDbTransaction();
        var valid = Convert.ToInt32(await command.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture) == 1;
        if (opened) await db.Database.CloseConnectionAsync();
        if (!valid) return false;
        if (await db.TicketRegistry.AnyAsync(x => x.IsArchived ? x.ArchiveSequence == null || x.ArchiveSequence <= 0 : x.ArchiveSequence != null, ct) ||
            await db.ReservationRegistry.AnyAsync(x => x.IsArchived ? x.ArchiveSequence == null || x.ArchiveSequence <= 0 : x.ArchiveSequence != null, ct)) return false;
        if (await db.TicketRegistry.AnyAsync(t => t.ReservationId != null && db.ReservationRegistry.Any(r => r.Id == t.ReservationId &&
            (r.IsArchived != t.IsArchived || r.DoctorPracticeId != t.DoctorPracticeId || r.DoctorId != t.DoctorId || r.PatientId != t.PatientId)), ct)) return false;
        return !await db.TicketRegistry.AnyAsync(r =>
            r.IsArchived && (!db.Set<ArchivedTicket>().Any(t => t.Id == r.Id) || db.Tickets.Any(t => t.Id == r.Id)) ||
            !r.IsArchived && (!db.Tickets.Any(t => t.Id == r.Id) || db.Set<ArchivedTicket>().Any(t => t.Id == r.Id)), ct) &&
            !await db.ReservationRegistry.AnyAsync(r =>
                r.IsArchived && (!db.Set<ArchivedReservation>().Any(t => t.Id == r.Id) || db.Reservations.Any(t => t.Id == r.Id)) ||
                !r.IsArchived && (!db.Reservations.Any(t => t.Id == r.Id) || db.Set<ArchivedReservation>().Any(t => t.Id == r.Id)), ct);
    }

    private string ReadinessSql()
    {
        // Compare the deployed constraints and indexes with the final EF model. A
        // dropped constraint must fail readiness just as a disabled one does.
        static string Literal(string value) => "N'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
        var foreignKeys = db.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()).Where(k =>
            k.PrincipalEntityType.ClrType == typeof(TicketRegistry) || k.PrincipalEntityType.ClrType == typeof(ReservationRegistry));
        var keys = foreignKeys.Select(k => "(" + string.Join(',', Literal(k.GetConstraintName()!),
            Literal((k.DeclaringEntityType.GetSchema() ?? "dbo") + "." + k.DeclaringEntityType.GetTableName()),
            Literal("dbo." + k.PrincipalEntityType.GetTableName()),
            Literal(string.Join(',', k.Properties.Select(p => p.GetColumnName()))),
            Literal(string.Join(',', k.PrincipalKey.Properties.Select(p => p.GetColumnName())))) + ")");
        var indexes = db.Model.GetEntityTypes().Where(e => e.ClrType == typeof(TicketRegistry) || e.ClrType == typeof(ReservationRegistry) ||
            e.GetSchema() == "archive" || e.ClrType == typeof(Ticket)).SelectMany(e => e.GetIndexes()
            .Where(i => i.IsUnique || e.GetSchema() == "archive").Select(i => "(" + Literal((e.GetSchema() ?? "dbo") + "." + e.GetTableName()) + "," +
                Literal(i.GetDatabaseName()!) + "," + (i.IsUnique ? "1" : "0") + "," + Literal(string.Join(',', i.Properties.Select(p => p.GetColumnName()))) + ")"));
        var checks = db.GetService<IDesignTimeModel>().Model.GetEntityTypes().Where(e => e.ClrType == typeof(TicketRegistry) || e.ClrType == typeof(ReservationRegistry) ||
            e.GetSchema() == "archive" || e.ClrType == typeof(Ticket)).SelectMany(e => e.GetCheckConstraints().Select(c =>
                "(" + Literal((e.GetSchema() ?? "dbo") + "." + e.GetTableName()) + "," + Literal(c.Name!) + ")"))
            .Concat(["(N'archive.Tickets',N'CK_ArchiveTickets_Terminal')", "(N'archive.Reservations',N'CK_ArchiveReservations_Terminal')"]);
        return $"""
            SELECT CASE WHEN EXISTS (
              SELECT 1 FROM (VALUES {string.Join(',', keys)}) expected(name,child,parent,columns,principalColumns)
              WHERE NOT EXISTS (SELECT 1 FROM sys.foreign_keys fk WHERE fk.name=expected.name
                AND fk.parent_object_id=OBJECT_ID(expected.child) AND fk.referenced_object_id=OBJECT_ID(expected.parent)
                AND fk.is_disabled=0 AND fk.is_not_trusted=0 AND fk.delete_referential_action=0
                AND (SELECT STRING_AGG(CONVERT(nvarchar(max),COL_NAME(fkc.parent_object_id,fkc.parent_column_id)),',') WITHIN GROUP (ORDER BY fkc.constraint_column_id) FROM sys.foreign_key_columns fkc WHERE fkc.constraint_object_id=fk.object_id)=expected.columns
                AND (SELECT STRING_AGG(CONVERT(nvarchar(max),COL_NAME(fkc.referenced_object_id,fkc.referenced_column_id)),',') WITHIN GROUP (ORDER BY fkc.constraint_column_id) FROM sys.foreign_key_columns fkc WHERE fkc.constraint_object_id=fk.object_id)=expected.principalColumns))
            OR EXISTS (SELECT 1 FROM (VALUES {string.Join(',', indexes)}) expected(owner,name,isUnique,columns)
                WHERE NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id=OBJECT_ID(expected.owner) AND i.name=expected.name AND i.is_disabled=0 AND i.is_unique=expected.isUnique
                AND (SELECT STRING_AGG(CONVERT(nvarchar(max),COL_NAME(ic.object_id,ic.column_id)),',') WITHIN GROUP (ORDER BY ic.key_ordinal) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0)=expected.columns))
            OR EXISTS (SELECT 1 FROM (VALUES {string.Join(',', checks)}) expected(owner,name)
                WHERE NOT EXISTS (SELECT 1 FROM sys.check_constraints c WHERE c.parent_object_id=OBJECT_ID(expected.owner) AND c.name=expected.name AND c.is_disabled=0 AND c.is_not_trusted=0))
            OR EXISTS (SELECT 1 FROM sys.check_constraints WHERE (parent_object_id IN (OBJECT_ID('dbo.TicketRegistry'),OBJECT_ID('dbo.ReservationRegistry'),OBJECT_ID('dbo.Tickets')) OR OBJECT_SCHEMA_NAME(parent_object_id)='archive') AND (is_disabled=1 OR is_not_trusted=1))
            OR EXISTS (SELECT 1 FROM sys.foreign_keys WHERE referenced_object_id IN (OBJECT_ID('dbo.Tickets'),OBJECT_ID('dbo.Reservations')) AND parent_object_id NOT IN (OBJECT_ID('dbo.TicketHistories'),OBJECT_ID('dbo.TicketCallAttempts'),OBJECT_ID('dbo.ReservationHistories')))
            THEN 0 ELSE 1 END
            """;
    }

    public async Task<ArchiveRun?> ProcessAsync(CancellationToken ct)
    {
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        if (!options.Value.Enabled || !await ReadyAsync(ct)) return null;
        await db.Database.OpenConnectionAsync(ct);
        using var lease = db.Database.GetDbConnection().CreateCommand();
        lease.CommandText = "DECLARE @r int; EXEC @r=sp_getapplock @Resource=N'Wasla:ArchiveWorker', @LockMode='Exclusive', @LockOwner='Session', @LockTimeout=0; SELECT @r";
        if (Convert.ToInt32(await lease.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture) < 0) return null;
        try
        {
            _cycleReady = true;
            var control = await db.ArchiveControlStates.SingleOrDefaultAsync(ct);
            if (control?.Paused == true) return null;
            foreach (var interrupted in await db.ArchiveRuns.Where(x => x.EndedAtUtc == null).ToArrayAsync(ct)) interrupted.Finish(clock.UtcNow, "Failed");
            var cycle = await ProgressAsync("Cycle", ct);
            var normal = cycle.Mode != "NormalEndOfDay" && (await db.Tickets.AnyAsync(x => (x.Status == TicketStatus.Completed || x.Status == TicketStatus.Cancelled || x.Status == TicketStatus.NoShow || (x.Status == TicketStatus.Waiting || x.Status == TicketStatus.Called) && db.MedicalEncounters.Any(e => e.TicketId == x.Id)) && db.PracticeOperationalDays.Any(d => d.DoctorPracticeId == x.DoctorPracticeId && d.BusinessDate == x.BusinessDate && d.EndsAtUtc <= clock.UtcNow), ct) ||
                await db.Reservations.AnyAsync(x => (x.Status == ReservationStatus.Cancelled || x.Status == ReservationStatus.Expired || x.Status == ReservationStatus.NoShow || x.Status == ReservationStatus.ConvertedToTicket && !db.TicketRegistry.Any(t => t.ReservationId == x.Id)) && db.PracticeOperationalDays.Any(d => d.DoctorPracticeId == x.DoctorPracticeId && d.BusinessDate == x.BusinessDate && d.EndsAtUtc <= clock.UtcNow), ct));
            var mode = normal ? "NormalEndOfDay" : "HistoricalBackfill";
            cycle.SetMode(mode);
            var run = new ArchiveRun(clock.UtcNow, mode); db.ArchiveRuns.Add(run); await db.SaveChangesAsync(ct);
            var ticketProgress = await ProgressAsync(mode + ":Ticket", ct);
            var reservationProgress = await ProgressAsync(mode + ":Reservation", ct);
            var ticketQuery = db.Tickets.AsNoTracking().Where(x => (x.Status == TicketStatus.Completed || x.Status == TicketStatus.Cancelled || x.Status == TicketStatus.NoShow || (x.Status == TicketStatus.Waiting || x.Status == TicketStatus.Called) && db.MedicalEncounters.Any(e => e.TicketId == x.Id)) &&
                (normal ? db.PracticeOperationalDays.Any(d => d.DoctorPracticeId == x.DoctorPracticeId && d.BusinessDate == x.BusinessDate && d.EndsAtUtc <= clock.UtcNow) : x.BusinessDate < DateOnly.FromDateTime(clock.UtcNow) && !db.PracticeOperationalDays.Any(d => d.DoctorPracticeId == x.DoctorPracticeId && d.BusinessDate == x.BusinessDate)));
            if (ticketProgress.LastDate is { } ticketDate && ticketProgress.LastId is { } ticketLast)
                ticketQuery = ticketQuery.Where(x => x.BusinessDate > ticketDate || x.BusinessDate == ticketDate && x.Id.CompareTo(ticketLast) > 0);
            var ticketRows = await ticketQuery.OrderBy(x => x.BusinessDate).ThenBy(x => x.Id).Take(options.Value.BatchSize).Select(x => new { x.Id, x.BusinessDate }).ToArrayAsync(ct);
            var ticketIds = ticketRows.Select(x => x.Id).ToArray();
            if (ticketIds.Length == 0) ticketProgress.Advance(null, null);
            foreach (var id in ticketIds)
            {
                if (await PausedAsync(ct)) break;
                var moved = await AttemptAsync(id, run.Id, true, ct);
                ticketProgress.Advance(ticketRows.Single(x => x.Id == id).BusinessDate, id);
                if (!moved) run.Count(false, false, true);
                await db.SaveChangesAsync(ct);
            }
            var reservationQuery = db.Reservations.AsNoTracking().Where(x =>
                (x.Status == ReservationStatus.Cancelled || x.Status == ReservationStatus.Expired || x.Status == ReservationStatus.NoShow || x.Status == ReservationStatus.ConvertedToTicket && !db.TicketRegistry.Any(t => t.ReservationId == x.Id)) && !db.TicketRegistry.Any(t => t.ReservationId == x.Id) &&
                (normal ? db.PracticeOperationalDays.Any(d => d.DoctorPracticeId == x.DoctorPracticeId && d.BusinessDate == x.BusinessDate && d.EndsAtUtc <= clock.UtcNow) : x.BusinessDate < DateOnly.FromDateTime(clock.UtcNow) && !db.PracticeOperationalDays.Any(d => d.DoctorPracticeId == x.DoctorPracticeId && d.BusinessDate == x.BusinessDate)));
            if (reservationProgress.LastDate is { } reservationDate && reservationProgress.LastId is { } reservationLast)
                reservationQuery = reservationQuery.Where(x => x.BusinessDate > reservationDate || x.BusinessDate == reservationDate && x.Id.CompareTo(reservationLast) > 0);
            var reservationRows = await reservationQuery.OrderBy(x => x.BusinessDate).ThenBy(x => x.Id).Take(options.Value.BatchSize).Select(x => new { x.Id, x.BusinessDate }).ToArrayAsync(ct);
            var reservationIds = reservationRows.Select(x => x.Id).ToArray();
            if (reservationIds.Length == 0) reservationProgress.Advance(null, null);
            foreach (var id in reservationIds)
            {
                if (await PausedAsync(ct)) break;
                var moved = await AttemptAsync(id, run.Id, false, ct); reservationProgress.Advance(reservationRows.Single(x => x.Id == id).BusinessDate, id); if (!moved) run.Count(false, false, true); await db.SaveChangesAsync(ct);
            }
            run.Finish(clock.UtcNow, await PausedAsync(ct) ? "Paused" : null); await db.SaveChangesAsync(ct); ArchiveTelemetry.Batch(run, elapsed.Elapsed); return run;
        }
        finally
        {
            _cycleReady = false;
            using var release = db.Database.GetDbConnection().CreateCommand();
            release.CommandText = "EXEC sp_releaseapplock @Resource=N'Wasla:ArchiveWorker', @LockOwner='Session'";
            await release.ExecuteNonQueryAsync(CancellationToken.None);
            await db.Database.CloseConnectionAsync();
        }
    }

    private async Task<ArchiveProgress> ProgressAsync(string id, CancellationToken ct)
    {
        var progress = await db.ArchiveProgress.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (progress != null) return progress;
        progress = new(id); db.ArchiveProgress.Add(progress); await db.SaveChangesAsync(ct); return progress;
    }
    private async Task<bool> AttemptAsync(Guid id, Guid run, bool ticket, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try { return ticket ? await MoveTicketAsync(id, run, ct) : await MoveReservationAsync(id, run, ct); }
            catch (Exception exception) when (exception is Microsoft.Data.SqlClient.SqlException or DbUpdateException)
            {
                foreach (var entry in db.ChangeTracker.Entries().Where(x => x.Entity is TicketRegistry or ReservationRegistry or ArchiveAuditEvent).ToArray()) entry.State = EntityState.Detached;
                foreach (var entry in db.ChangeTracker.Entries().Where(x => x.Entity is ArchiveRun or ArchiveProgress).ToArray()) await entry.ReloadAsync(ct);
                // A lost acknowledgement may occur after COMMIT succeeded. Resolve
                // the durable identity before retrying or recording a false failure.
                var committed = ticket
                    ? await db.TicketRegistry.AsNoTracking().AnyAsync(x => x.Id == id && x.IsArchived && db.Set<ArchivedTicket>().Any(a => a.Id == id) && !db.Tickets.Any(a => a.Id == id), ct)
                    : await db.ReservationRegistry.AsNoTracking().AnyAsync(x => x.Id == id && x.IsArchived && db.Set<ArchivedReservation>().Any(a => a.Id == id) && !db.Reservations.Any(a => a.Id == id), ct);
                if (committed) return true;
                var sql = exception as Microsoft.Data.SqlClient.SqlException ?? exception.InnerException as Microsoft.Data.SqlClient.SqlException;
                if (attempt < 2 && sql?.Number is 1205 or -2 or 1222) continue;
                var location = ticket ? await db.TicketRegistry.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.DoctorPracticeId, x.RowVersion }).SingleAsync(ct) :
                    await db.ReservationRegistry.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.DoctorPracticeId, x.RowVersion }).SingleAsync(ct);
                await RecordExceptionAsync(id, location.DoctorPracticeId, run, ticket ? "Ticket" : "Reservation", "DatabaseFailure", Convert.ToHexString(location.RowVersion), ct);
                return false;
            }
        }
        return false;
    }

    private Task<bool> PausedAsync(CancellationToken ct) => db.ArchiveControlStates.AsNoTracking().AnyAsync(x => x.Paused, ct);

    public async Task<bool> MoveTicketAsync(Guid id, Guid runId, CancellationToken ct)
    {
        if (!options.Value.Enabled || !_cycleReady && !await ReadyAsync(ct)) return false;
        var identity = await db.TicketRegistry.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (identity == null || identity.IsArchived) return false;
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await AssertWorkerLeaseAsync(ct);
        await queueLock.AcquirePatientPracticeAsync(identity.PatientId, identity.DoctorPracticeId, ct);
        await queueLock.AcquirePracticeDayAsync(identity.DoctorPracticeId, identity.BusinessDate, ct);
        if (identity.ReservationId != null) await LockReservationScopeAsync(identity.PatientId, identity.DoctorPracticeId, identity.BusinessDate, ct);
        if (await db.TicketRegistry.AsNoTracking().AnyAsync(x => x.Id == id && x.IsArchived, ct)) return false;
        await LockControlAsync(ct);
        if (await PausedAsync(ct)) return false;
        var ticket = await db.Tickets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (ticket == null) return false;
        if (!ArchiveEligibility.Terminal(ticket.Status))
        {
            if (ticket.Status is TicketStatus.Waiting or TicketStatus.Called && await db.MedicalEncounters.AnyAsync(e => e.TicketId == id, ct))
            { await transaction.RollbackAsync(ct); await RecordExceptionAsync(id, ticket.DoctorPracticeId, runId, "Ticket", "EncounterMismatch", Convert.ToHexString(ticket.RowVersion), ct); }
            return false;
        }
        var provenance = await ProvenanceAsync(ticket.DoctorPracticeId, ticket.BusinessDate, ticket.PracticeTimeZoneIdSnapshot, ct);
        if (provenance == null && ticket.BusinessDate >= DateOnly.FromDateTime(clock.UtcNow)) return false;
        var code = provenance == null ? "UnverifiedDayClosure" : null;
        var payments = await db.Payments.AsNoTracking().Where(x => x.TicketId == id).Take(2).ToArrayAsync(ct);
        var payment = payments.FirstOrDefault();
        if (payments.Length == 0) code = "MissingPayment";
        else if (payments.Length != 1 || payment!.Status != PaymentStatus.Paid || payment.Amount <= 0 || payment.Amount != ticket.PriceSnapshot || payment.CurrencyCode != "EGP" ||
            payment.PatientId != ticket.PatientId || payment.DoctorId != ticket.DoctorId || payment.DoctorPracticeId != ticket.DoctorPracticeId || payment.ReservationId != ticket.ReservationId) code = "PaymentMismatch";
        var encounter = await db.MedicalEncounters.AsNoTracking().SingleOrDefaultAsync(x => x.TicketId == id, ct);
        if (ticket.Status == TicketStatus.Completed && (encounter == null || encounter.Status != EncounterStatus.Completed || encounter.CompletedAtUtc == null || ticket.CompletedOnUtc == null)) code = "EncounterMismatch";
        if (encounter != null && (ticket.Status != TicketStatus.Completed || encounter.Status == EncounterStatus.InProgress || encounter.PatientId != ticket.PatientId || encounter.DoctorId != ticket.DoctorId || encounter.DoctorPracticeId != ticket.DoctorPracticeId)) code = "EncounterMismatch";
        if (await db.TicketHistories.CountAsync(x => x.TicketId == id && x.EventType == TicketHistoryEventType.TicketCreated, ct) != 1 ||
            await db.TicketHistories.AnyAsync(x => x.TicketId == id && (x.OccurredOnUtc < ticket.CheckInTimeUtc || x.OccurredOnUtc > clock.UtcNow), ct) ||
            !await db.TicketHistories.AnyAsync(x => x.TicketId == id && x.ToStatus == ticket.Status && (ticket.Status == TicketStatus.Completed ? x.EventType == TicketHistoryEventType.Completed :
                ticket.Status == TicketStatus.NoShow ? x.EventType == TicketHistoryEventType.AutoNoShow : x.EventType == TicketHistoryEventType.Cancelled || x.EventType == TicketHistoryEventType.OperationalDayEnded), ct)) code = "HistoryMismatch";
        if (ticket.DoctorId != identity.DoctorId || ticket.DoctorPracticeId != identity.DoctorPracticeId || ticket.PatientId != identity.PatientId ||
            ticket.ReservationId != identity.ReservationId || ticket.BusinessDate != identity.BusinessDate || ticket.TicketNumber != identity.TicketNumber) code = "IdentityMismatch";
        Reservation? reservation = null;
        if (ticket.ReservationId is { } reservationId)
        {
            reservation = await db.Reservations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == reservationId, ct);
            if (reservation == null || reservation.Status != ReservationStatus.ConvertedToTicket || reservation.PatientId != ticket.PatientId ||
                reservation.DoctorPracticeId != ticket.DoctorPracticeId || reservation.DoctorId != ticket.DoctorId ||
                reservation.BusinessDate != ticket.BusinessDate ||
                !await ReservationIntegrityAsync(reservation, ct)) code = "ReservationMismatch";
        }
        if (code != null)
        {
            await transaction.RollbackAsync(ct); await RecordExceptionAsync(id, ticket.DoctorPracticeId, runId, "Ticket", code, Convert.ToHexString(ticket.RowVersion), ct); return false;
        }
        if (ticket.Status == TicketStatus.NoShow && ticket.IsOperationallyOpen)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.Tickets SET IsOperationallyOpen=0, OperationalFinalizedAtUtc={clock.UtcNow} WHERE Id={id} AND Status=4", ct);
            db.ArchiveAuditEvents.Add(new("NoShowFinalized", "Ticket", id, ticket.DoctorPracticeId, null, clock.UtcNow, runId));
        }
        if (reservation != null) { await CopyAsync<Reservation>(reservation.Id, ct); await CopyChildrenAsync<ReservationHistory>("ReservationId", reservation.Id, ct); }
        await CopyAsync<Ticket>(id, ct); await CopyChildrenAsync<TicketHistory>("TicketId", id, ct); await CopyChildrenAsync<TicketCallAttempt>("TicketId", id, ct);
        if (reservation != null) await DeleteChildrenAsync<ReservationHistory>("ReservationId", reservation.Id, ct);
        await DeleteChildrenAsync<TicketHistory>("TicketId", id, ct); await DeleteChildrenAsync<TicketCallAttempt>("TicketId", id, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dbo.Tickets WHERE Id={id}", ct);
        if (reservation != null) await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dbo.Reservations WHERE Id={reservation.Id}", ct);
        var sequence = await NextSequenceAsync(ct);
        var pointer = await db.TicketRegistry.SingleAsync(x => x.Id == id, ct); pointer.MarkArchived(clock.UtcNow, provenance!, sequence);
        if (reservation != null) (await db.ReservationRegistry.SingleAsync(x => x.Id == reservation.Id, ct)).MarkArchived(clock.UtcNow, provenance!, sequence);
        db.ArchiveAuditEvents.Add(new("TicketArchived", "Ticket", id, ticket.DoctorPracticeId, null, clock.UtcNow, runId));
        await CountMovedAsync(runId, ticket.BusinessDate, id, true, reservation != null, ct);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return true;
    }

    public async Task<bool> MoveReservationAsync(Guid id, Guid runId, CancellationToken ct)
    {
        if (!options.Value.Enabled || !_cycleReady && !await ReadyAsync(ct)) return false;
        var reservation = await db.Reservations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (reservation?.Status == ReservationStatus.ConvertedToTicket && !await db.TicketRegistry.AnyAsync(t => t.ReservationId == id, ct))
        { await RecordExceptionAsync(id, reservation.DoctorPracticeId, runId, "Reservation", "ReservationMismatch", Convert.ToHexString(reservation.RowVersion), ct); return false; }
        if (reservation == null || !ArchiveEligibility.Standalone(reservation.Status)) return false;
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await AssertWorkerLeaseAsync(ct);
        await queueLock.AcquirePatientPracticeAsync(reservation.PatientId, reservation.DoctorPracticeId, ct);
        await queueLock.AcquirePracticeDayAsync(reservation.DoctorPracticeId, reservation.BusinessDate, ct);
        await LockReservationScopeAsync(reservation.PatientId, reservation.DoctorPracticeId, reservation.BusinessDate, ct);
        reservation = await db.Reservations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (reservation == null || !ArchiveEligibility.Standalone(reservation.Status)) return false;
        await LockControlAsync(ct); if (await PausedAsync(ct)) return false;
        var provenance = await ProvenanceAsync(reservation.DoctorPracticeId, reservation.BusinessDate, reservation.TimeZoneIdSnapshot, ct);
        var failure = provenance == null ? "UnverifiedDayClosure" :
            await db.TicketRegistry.AnyAsync(x => x.ReservationId == id, ct) || !await ReservationIntegrityAsync(reservation, ct) ? "ReservationMismatch" : null;
        if (failure != null)
        { await transaction.RollbackAsync(ct); await RecordExceptionAsync(id, reservation.DoctorPracticeId, runId, "Reservation", failure, Convert.ToHexString(reservation.RowVersion), ct); return false; }
        await CopyAsync<Reservation>(id, ct); await CopyChildrenAsync<ReservationHistory>("ReservationId", id, ct);
        await DeleteChildrenAsync<ReservationHistory>("ReservationId", id, ct); await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dbo.Reservations WHERE Id={id}", ct);
        (await db.ReservationRegistry.SingleAsync(x => x.Id == id, ct)).MarkArchived(clock.UtcNow, provenance!, await NextSequenceAsync(ct));
        db.ArchiveAuditEvents.Add(new("ReservationArchived", "Reservation", id, reservation.DoctorPracticeId, null, clock.UtcNow, runId));
        await CountMovedAsync(runId, reservation.BusinessDate, id, false, true, ct);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return true;
    }

    private async Task<bool> ReservationIntegrityAsync(Reservation reservation, CancellationToken ct)
    {
        if (!await db.ReservationRegistry.AnyAsync(r => r.Id == reservation.Id && !r.IsArchived &&
            r.DoctorId == reservation.DoctorId && r.DoctorPracticeId == reservation.DoctorPracticeId &&
            r.PatientId == reservation.PatientId && r.ReservationReference == reservation.ReservationReference, ct)) return false;
        if (await db.ReservationHistories.CountAsync(h => h.ReservationId == reservation.Id && h.EventType == ReservationHistoryEventType.Created, ct) != 1 ||
            await db.ReservationHistories.AnyAsync(h => h.ReservationId == reservation.Id && (h.OccurredOnUtc < reservation.CreatedOnUtc || h.OccurredOnUtc > clock.UtcNow), ct)) return false;
        return await db.ReservationHistories.AnyAsync(h => h.ReservationId == reservation.Id && h.ToStatus == reservation.Status &&
            (reservation.Status == ReservationStatus.Cancelled ? h.EventType == ReservationHistoryEventType.Cancelled :
             reservation.Status == ReservationStatus.NoShow ? h.EventType == ReservationHistoryEventType.MarkedNoShow :
             reservation.Status == ReservationStatus.Expired ? h.EventType == ReservationHistoryEventType.Expired :
             reservation.Status == ReservationStatus.ConvertedToTicket && h.EventType == ReservationHistoryEventType.ConvertedToTicket), ct);
    }

    private async Task<string?> ProvenanceAsync(Guid practice, DateOnly businessDate, string zoneId, CancellationToken ct)
    {
        var day = await db.PracticeOperationalDays.AsNoTracking().SingleOrDefaultAsync(x => x.DoctorPracticeId == practice && x.BusinessDate == businessDate, ct);
        if (day != null) return day.ClosedAtUtc != null && day.EvidenceVerified && day.EndsAtUtc <= clock.UtcNow ? "NormalEndOfDay" : null;
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(zoneId, out var zone)) return null;
        return businessDate < DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc), zone)) ? "HistoricalBackfill" : null;
    }
    private async Task CountMovedAsync(Guid runId, DateOnly businessDate, Guid id, bool ticket, bool reservation, CancellationToken ct)
    {
        var run = await db.ArchiveRuns.SingleAsync(x => x.Id == runId, ct);
        run.Count(ticket, reservation, false);
        var progress = await ProgressAsync(run.Mode + (ticket ? ":Ticket" : ":Reservation"), ct);
        progress.Advance(businessDate, id);
    }
    private async Task<long> NextSequenceAsync(CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync("SELECT Id FROM dbo.ArchiveProgress WITH (UPDLOCK,HOLDLOCK) WHERE Id=N'CommittedSequence'", ct);
        return (await ProgressAsync("CommittedSequence", ct)).NextSequence();
    }
    private Task<int> LockControlAsync(CancellationToken ct) => db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sp_getapplock @Resource=N'Wasla:ArchiveControl', @LockMode='Shared', @LockOwner='Transaction', @LockTimeout=1000; IF @r < 0 THROW 51000, 'Archive.ControlBusy', 1;", ct);
    private async Task LockReservationScopeAsync(Guid patient, Guid practice, DateOnly date, CancellationToken ct)
    {
        // The same resources and patient-before-day order used by WaslaDataStore.
        var patientResource = $"Wasla:Reservation:Patient:{patient:N}";
        var practiceResource = $"Wasla:Reservation:Practice:{practice:N}:{date:yyyyMMdd}";
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sp_getapplock @Resource={patientResource},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51000,'Archive.ReservationBusy',1;", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sp_getapplock @Resource={practiceResource},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51000,'Archive.ReservationBusy',1;", ct);
    }
    private async Task AssertWorkerLeaseAsync(CancellationToken ct)
    {
        if (!_cycleReady) return;
        using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = "SELECT APPLOCK_MODE('public','Wasla:ArchiveWorker','Session')";
        if (!string.Equals((string?)await command.ExecuteScalarAsync(ct), "Exclusive", StringComparison.Ordinal))
            throw new InvalidOperationException("Archive.WorkerLeaseLost");
    }
    private async Task RecordExceptionAsync(Guid id, Guid practice, Guid run, string type, string code, string fingerprint, CancellationToken ct)
    {
        if (!await db.ArchiveIntegrityIssues.AnyAsync(x => x.EntityId == id && x.EntityType == type && x.FailureCode == code && x.StateFingerprint == fingerprint, ct))
        { db.ArchiveIntegrityIssues.Add(new(id, practice, run, type, code, fingerprint, clock.UtcNow)); db.ArchiveAuditEvents.Add(new("ArchiveFailed", type, id, practice, null, clock.UtcNow, run, outcome: "Failed", failureCode: code)); await db.SaveChangesAsync(ct); ArchiveTelemetry.Integrity(code); }
    }
    private Task<int> CopyAsync<T>(Guid id, CancellationToken ct) where T : class => CopyRowsAsync<T>("Id", id, ct);
    private Task<int> CopyChildrenAsync<T>(string foreignKey, Guid id, CancellationToken ct) where T : class => CopyRowsAsync<T>(foreignKey, id, ct);
    private async Task<int> CopyRowsAsync<T>(string key, Guid id, CancellationToken ct) where T : class
    {
        var entity = db.Model.FindEntityType(typeof(T))!; var table = entity.GetTableName()!;
        var store = StoreObjectIdentifier.Table(table, entity.GetSchema());
        var columns = string.Join(',', entity.GetProperties().Select(x => "[" + x.GetColumnName(store) + "]"));
        // All identifiers are trusted EF metadata/constants; the sole data value is a SQL parameter.
        var sql = $"INSERT INTO [archive].[{table}] ({columns}) SELECT {columns} FROM [dbo].[{table}] WHERE [{key}]={{0}}; IF (SELECT COUNT_BIG(*) FROM [archive].[{table}] WHERE [{key}]={{0}}) <> (SELECT COUNT_BIG(*) FROM [dbo].[{table}] WHERE [{key}]={{0}}) THROW 51000, 'Archive.CopyCountMismatch', 1;";
        return await db.Database.ExecuteSqlRawAsync(sql, [id], ct);
    }
    private Task<int> DeleteChildrenAsync<T>(string key, Guid id, CancellationToken ct) where T : class
    {
        var table = db.Model.FindEntityType(typeof(T))!.GetTableName();
        var sql = $"DELETE FROM [dbo].[{table}] WHERE [{key}]={{0}}";
        return db.Database.ExecuteSqlRawAsync(sql, [id], ct);
    }
}
