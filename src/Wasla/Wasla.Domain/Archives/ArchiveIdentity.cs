using Wasla.Domain.Tickets;
using Wasla.Domain.Reservations;

namespace Wasla.Domain.Archives;

public sealed class TicketRegistry
{
    public Guid Id { get; private set; }
    public Guid DoctorId { get; private set; }
    public Guid DoctorPracticeId { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid? ReservationId { get; private set; }
    public DateOnly BusinessDate { get; private set; }
    public int TicketNumber { get; private set; }
    public bool IsArchived { get; private set; }
    public DateTime? ArchivedAtUtc { get; private set; }
    public string? Provenance { get; private set; }
    public long? ArchiveSequence { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    private TicketRegistry() { }
    public static TicketRegistry Register(Ticket ticket) => new() { Id = ticket.Id, DoctorId = ticket.DoctorId,
        DoctorPracticeId = ticket.DoctorPracticeId, PatientId = ticket.PatientId, ReservationId = ticket.ReservationId,
        BusinessDate = ticket.BusinessDate, TicketNumber = ticket.TicketNumber };
    public void MarkArchived(DateTime utc, string provenance, long sequence)
    {
        if (IsArchived) throw new InvalidOperationException("Archive.IdentityAlreadyMoved");
        ArgumentOutOfRangeException.ThrowIfLessThan(sequence, 1);
        IsArchived = true; ArchivedAtUtc = utc; Provenance = provenance; ArchiveSequence = sequence;
    }
}

public sealed class ReservationRegistry
{
    public Guid Id { get; private set; }
    public Guid DoctorId { get; private set; }
    public Guid DoctorPracticeId { get; private set; }
    public Guid PatientId { get; private set; }
    public string ReservationReference { get; private set; } = string.Empty;
    public bool IsArchived { get; private set; }
    public DateTime? ArchivedAtUtc { get; private set; }
    public string? Provenance { get; private set; }
    public long? ArchiveSequence { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    private ReservationRegistry() { }
    public static ReservationRegistry Register(Reservation reservation) => new() { Id = reservation.Id, DoctorId = reservation.DoctorId,
        DoctorPracticeId = reservation.DoctorPracticeId, PatientId = reservation.PatientId, ReservationReference = reservation.ReservationReference };
    public void MarkArchived(DateTime utc, string provenance, long sequence)
    {
        if (IsArchived) throw new InvalidOperationException("Archive.IdentityAlreadyMoved");
        ArgumentOutOfRangeException.ThrowIfLessThan(sequence, 1);
        IsArchived = true; ArchivedAtUtc = utc; Provenance = provenance; ArchiveSequence = sequence;
    }
}

public sealed class PracticeOperationalDay
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid DoctorPracticeId { get; private set; }
    public DateOnly BusinessDate { get; private set; }
    public string TimeZoneId { get; private set; } = string.Empty;
    public DateTime EndsAtUtc { get; private set; }
    public DateTime ObservedAtUtc { get; private set; }
    public string EvidenceHash { get; private set; } = string.Empty;
    public int Revision { get; private set; } = 1;
    public bool EvidenceVerified { get; private set; } = true;
    public DateTime? ClosedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    private PracticeOperationalDay() { }
    public static PracticeOperationalDay Observe(Guid practice, DateOnly date, string zone, DateTime end, string hash, DateTime now)
    {
        if (now >= end) throw new InvalidOperationException("Archive.UnobservedHistoricalDay");
        return new() { DoctorPracticeId = practice, BusinessDate = date, TimeZoneId = zone, EndsAtUtc = end, EvidenceHash = hash, ObservedAtUtc = now };
    }
    public bool Revise(string zone, DateTime end, string hash, DateTime now)
    {
        if (ClosedAtUtc != null || now >= EndsAtUtc) return false;
        if (EvidenceVerified && hash == EvidenceHash && zone == TimeZoneId && end == EndsAtUtc) return false;
        TimeZoneId = zone; EndsAtUtc = end; EvidenceHash = hash; ObservedAtUtc = now; EvidenceVerified = true; Revision++; return true;
    }
    public bool Invalidate(DateTime now)
    {
        if (ClosedAtUtc != null || now >= EndsAtUtc || !EvidenceVerified) return false;
        EvidenceVerified = false; ObservedAtUtc = now; Revision++; return true;
    }
    public void Close(DateTime now)
    {
        if (!EvidenceVerified || now < EndsAtUtc) throw new InvalidOperationException("Archive.DayStillOpen");
        ClosedAtUtc ??= now;
    }
}

public static class ArchiveEligibility
{
    public static bool Terminal(TicketStatus status) => status is TicketStatus.Completed or TicketStatus.Cancelled or TicketStatus.NoShow;
    public static bool Standalone(ReservationStatus status) => status is ReservationStatus.Cancelled or ReservationStatus.Expired or ReservationStatus.NoShow;
}
