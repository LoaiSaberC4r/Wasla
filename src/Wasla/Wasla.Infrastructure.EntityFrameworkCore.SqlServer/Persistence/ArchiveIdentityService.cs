using Microsoft.EntityFrameworkCore;
using BuildingBlock.Application.Time;
using Wasla.Application.Features.Archives;
using Wasla.Domain.Archives;
using Wasla.Domain.Tickets;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class ArchiveLocationResolver(WaslaDbContext db, IDateTimeProvider clock) : IArchiveLocationResolver
{
    public Task<ArchiveLocation?> TicketAsync(Guid id, CancellationToken ct) => db.TicketRegistry.AsNoTracking()
        .Where(x => x.Id == id).Select(x => new ArchiveLocation(x.IsArchived, x.ArchivedAtUtc, x.DoctorPracticeId, x.PatientId)).SingleOrDefaultAsync(ct);
    public Task<ArchiveLocation?> ReservationAsync(Guid id, CancellationToken ct) => db.ReservationRegistry.AsNoTracking()
        .Where(x => x.Id == id).Select(x => new ArchiveLocation(x.IsArchived, x.ArchivedAtUtc, x.DoctorPracticeId, x.PatientId)).SingleOrDefaultAsync(ct);
    public Task<Guid?> LinkedTicketAsync(Guid reservation, CancellationToken ct) => db.TicketRegistry.AsNoTracking()
        .Where(x => x.ReservationId == reservation).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
    public async Task<bool> IsNoShowDayFinalOrUnverifiableAsync(Guid practice, DateOnly date, string originalTimeZoneId, CancellationToken ct)
    {
        var day = await db.PracticeOperationalDays.AsNoTracking().SingleOrDefaultAsync(x => x.DoctorPracticeId == practice && x.BusinessDate == date, ct);
        if (day != null) return !day.EvidenceVerified || day.ClosedAtUtc != null || day.EndsAtUtc <= clock.UtcNow;
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(originalTimeZoneId, out var zone)) return true;
        return date < DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc), zone));
    }
    public Task<Ticket?> ReadTicketForFinanceAsync(Guid id, CancellationToken ct) => db.AllTickets.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> EverStartedAsync(Guid id, CancellationToken ct) => db.AllTicketHistories.AnyAsync(x => x.TicketId == id && x.EventType == TicketHistoryEventType.InProgress, ct);
    public async Task AuditStaffTicketReadAsync(Guid ticket, Guid practice, Guid actor, CancellationToken ct)
    {
        db.ArchiveAuditEvents.Add(new("ArchivedTicketOpened", "Ticket", ticket, practice, actor, clock.UtcNow));
        await db.SaveChangesAsync(ct); // Fail closed; do not release a sensitive response if required evidence fails.
    }
}

public sealed class ArchiveRegistryPopulation(WaslaDbContext db)
{
    public async Task<int> PopulateAsync(int batchSize = 250, CancellationToken ct = default)
    {
        if (batchSize is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(batchSize));
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (db.Database.IsSqlServer())
            await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r = sp_getapplock @Resource=N'Wasla:ArchiveRegistryPopulation', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=1000; IF @r < 0 THROW 51000, 'Archive.RegistryPopulationBusy', 1;", ct);
        // Explicit initial-schema columns make this tool usable between expansion and FK cutover.
        // It does not require later liveness/cursor columns or run hosted services.
        var reservations = await db.Reservations.AsNoTracking().Where(x => !db.ReservationRegistry.Any(r => r.Id == x.Id))
            .OrderBy(x => x.Id).Take(batchSize).Select(x => new { x.Id, x.DoctorId, x.DoctorPracticeId, x.PatientId, x.ReservationReference }).ToArrayAsync(ct);
        foreach (var r in reservations)
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO ReservationRegistry (Id,DoctorId,DoctorPracticeId,PatientId,ReservationReference,IsArchived) VALUES ({r.Id},{r.DoctorId},{r.DoctorPracticeId},{r.PatientId},{r.ReservationReference},0)", ct);
        var tickets = await db.Tickets.AsNoTracking().Where(x => !db.TicketRegistry.Any(r => r.Id == x.Id) &&
            (x.ReservationId == null || db.ReservationRegistry.Any(r => r.Id == x.ReservationId)))
            .OrderBy(x => x.Id).Take(batchSize).Select(x => new { x.Id, x.DoctorId, x.DoctorPracticeId, x.PatientId, x.ReservationId, x.BusinessDate, x.TicketNumber }).ToArrayAsync(ct);
        foreach (var t in tickets)
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO TicketRegistry (Id,DoctorId,DoctorPracticeId,PatientId,ReservationId,BusinessDate,TicketNumber,IsArchived) VALUES ({t.Id},{t.DoctorId},{t.DoctorPracticeId},{t.PatientId},{t.ReservationId},{t.BusinessDate},{t.TicketNumber},0)", ct);
        await transaction.CommitAsync(ct);
        return reservations.Length + tickets.Length;
    }
}
