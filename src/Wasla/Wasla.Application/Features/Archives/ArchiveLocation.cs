using Wasla.Domain.Tickets;

namespace Wasla.Application.Features.Archives;

public sealed record ArchiveLocation(bool IsArchived, DateTime? ArchivedAtUtc, Guid DoctorPracticeId, Guid PatientId);
public interface IArchiveLocationResolver
{
    Task<ArchiveLocation?> TicketAsync(Guid id, CancellationToken ct);
    Task<ArchiveLocation?> ReservationAsync(Guid id, CancellationToken ct);
    Task<Guid?> LinkedTicketAsync(Guid reservation, CancellationToken ct);
    Task<bool> IsNoShowDayFinalOrUnverifiableAsync(Guid practice, DateOnly businessDate, string originalTimeZoneId, CancellationToken ct);
    Task<Ticket?> ReadTicketForFinanceAsync(Guid id, CancellationToken ct);
    Task<bool> EverStartedAsync(Guid id, CancellationToken ct);
    Task AuditStaffTicketReadAsync(Guid ticket, Guid practice, Guid actor, CancellationToken ct);
}
