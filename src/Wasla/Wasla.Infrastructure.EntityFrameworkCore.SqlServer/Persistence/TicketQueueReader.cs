using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Domain.Tickets;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class TicketQueueReader(WaslaDbContext dbContext) : ITicketQueueReader
{
    public Task<bool> HasOpenTicketAsync(
        Guid patientId,
        Guid doctorPracticeId,
        CancellationToken cancellationToken = default)
        => dbContext.Tickets.AsNoTracking().AnyAsync(
            item => item.PatientId == patientId &&
                    item.DoctorPracticeId == doctorPracticeId &&
                    (item.Status == TicketStatus.Waiting ||
                     item.Status == TicketStatus.Called ||
                     item.Status == TicketStatus.InProgress ||
                     item.Status == TicketStatus.NoShow),
            cancellationToken);

    public Task<bool> HasCalledOrInProgressAsync(
        Guid doctorPracticeId,
        CancellationToken cancellationToken = default)
        => dbContext.Tickets.AsNoTracking().AnyAsync(
            item => item.DoctorPracticeId == doctorPracticeId &&
                    (item.Status == TicketStatus.Called || item.Status == TicketStatus.InProgress),
            cancellationToken);

    public Task<bool> HasInProgressAsync(
        Guid doctorPracticeId,
        Guid? exceptTicketId,
        CancellationToken cancellationToken = default)
        => dbContext.Tickets.AsNoTracking().AnyAsync(
            item => item.DoctorPracticeId == doctorPracticeId &&
                    item.Status == TicketStatus.InProgress &&
                    (!exceptTicketId.HasValue || item.Id != exceptTicketId.Value),
            cancellationToken);

    public Task<Guid?> FindNextWaitingTicketIdAsync(
        Guid doctorPracticeId,
        DateOnly businessDate,
        CancellationToken cancellationToken = default)
        => OrderedWaiting(doctorPracticeId, businessDate)
            .Select(item => (Guid?)item.Ticket.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<int> CountInProgressTransitionsAfterAsync(
        Guid doctorPracticeId,
        DateOnly businessDate,
        DateTime afterUtc,
        CancellationToken cancellationToken = default)
        => dbContext.Tickets.AsNoTracking().CountAsync(
            item => item.DoctorPracticeId == doctorPracticeId &&
                    item.BusinessDate == businessDate &&
                    item.InProgressOnUtc.HasValue &&
                    item.InProgressOnUtc.Value > afterUtc,
            cancellationToken);

    public async Task<TicketDetailsResponse?> GetDetailsAsync(
        Guid ticketId,
        CancellationToken cancellationToken = default)
    {
        var ticket = await dbContext.Tickets.AsNoTracking()
            .Include(item => item.CallAttempts)
            .SingleOrDefaultAsync(item => item.Id == ticketId, cancellationToken);
        if (ticket is null)
        {
            return null;
        }

        var names = await (from patient in dbContext.Patients.AsNoTracking()
            join practice in dbContext.DoctorPractices.AsNoTracking()
                on ticket.DoctorPracticeId equals practice.Id
            join doctor in dbContext.Doctors.AsNoTracking()
                on ticket.DoctorId equals doctor.Id
            where patient.Id == ticket.PatientId
            select new
            {
                PatientNameAr = patient.NameAr,
                PatientNameEn = patient.NameEn,
                PracticeNameAr = practice.NameAr,
                PracticeNameEn = practice.NameEn,
                DoctorNameAr = doctor.NameAr,
                DoctorNameEn = doctor.NameEn
            }).SingleAsync(cancellationToken);
        var patientsAhead = await CountPatientsAheadAsync(ticket, cancellationToken);
        return new TicketDetailsResponse(
            ticket.Id,
            ticket.TicketNumber,
            ticket.Status,
            ticket.Source,
            ticket.DoctorId,
            names.DoctorNameAr,
            names.DoctorNameEn,
            ticket.DoctorPracticeId,
            names.PracticeNameAr,
            names.PracticeNameEn,
            ticket.PatientId,
            names.PatientNameAr,
            names.PatientNameEn,
            ticket.ReservationId,
            ticket.BusinessDate,
            ticket.SegmentId,
            ticket.SegmentNameArSnapshot,
            ticket.SegmentNameEnSnapshot,
            ticket.SegmentPrioritySnapshot,
            ticket.VisitTypeId,
            ticket.VisitTypeCodeSnapshot,
            ticket.VisitTypeNameArSnapshot,
            ticket.VisitTypeNameEnSnapshot,
            ticket.PriceSnapshot,
            ticket.CheckInTimeUtc,
            ticket.QueueOrderTimeUtc,
            patientsAhead,
            ticket.IsFastTrack,
            ticket.LastUpdatedOnUtc,
            ticket.CallAttempts
                .OrderBy(item => item.CallCycle)
                .ThenBy(item => item.AttemptNumber)
                .Select(item => new TicketAttemptResponse(
                    item.CallCycle,
                    item.AttemptNumber,
                    item.Outcome,
                    item.CalledOnUtc,
                    item.OutcomeRecordedOnUtc))
                .ToArray(),
            TicketRowVersion(ticket.RowVersion));
    }

    public async Task<PracticeQueueResponse> GetPracticeQueueAsync(
        Guid doctorPracticeId,
        DateOnly businessDate,
        CancellationToken cancellationToken = default)
    {
        var active = await (from ticket in dbContext.Tickets.AsNoTracking()
                .Include(item => item.CallAttempts)
            join patient in dbContext.Patients.AsNoTracking()
                on ticket.PatientId equals patient.Id
            where ticket.DoctorPracticeId == doctorPracticeId &&
                  (ticket.Status == TicketStatus.Called ||
                   ticket.Status == TicketStatus.InProgress)
            select new QueueProjection(ticket, patient.NameAr, patient.NameEn))
            .ToArrayAsync(cancellationToken);
        var waiting = await OrderedWaiting(doctorPracticeId, businessDate)
            .ToArrayAsync(cancellationToken);
        var noShow = await (from ticket in dbContext.Tickets.AsNoTracking()
                .Include(item => item.CallAttempts)
            join patient in dbContext.Patients.AsNoTracking()
                on ticket.PatientId equals patient.Id
            where ticket.DoctorPracticeId == doctorPracticeId &&
                  ticket.BusinessDate == businessDate &&
                  ticket.Status == TicketStatus.NoShow
            orderby ticket.NoShowOnUtc descending
            select new QueueProjection(ticket, patient.NameAr, patient.NameEn))
            .ToArrayAsync(cancellationToken);

        return new PracticeQueueResponse(
            doctorPracticeId,
            businessDate,
            active.Where(item => item.Ticket.Status == TicketStatus.InProgress)
                .Select(item => MapQueue(item.Ticket, item.PatientNameAr, item.PatientNameEn))
                .SingleOrDefault(),
            active.Where(item => item.Ticket.Status == TicketStatus.Called)
                .Select(item => MapQueue(item.Ticket, item.PatientNameAr, item.PatientNameEn))
                .SingleOrDefault(),
            waiting.Select(item => MapQueue(
                item.Ticket, item.PatientNameAr, item.PatientNameEn)).ToArray(),
            noShow.Select(item => MapQueue(
                item.Ticket, item.PatientNameAr, item.PatientNameEn)).ToArray());
    }

    public async Task<IReadOnlyList<MyActiveTicketResponse>> ListPatientActiveAsync(
        Guid patientId,
        CancellationToken cancellationToken = default)
    {
        var tickets = await (from ticket in dbContext.Tickets.AsNoTracking()
            join practice in dbContext.DoctorPractices.AsNoTracking()
                on ticket.DoctorPracticeId equals practice.Id
            join doctor in dbContext.Doctors.AsNoTracking()
                on ticket.DoctorId equals doctor.Id
            where ticket.PatientId == patientId &&
                  (ticket.Status == TicketStatus.Waiting ||
                   ticket.Status == TicketStatus.Called ||
                   ticket.Status == TicketStatus.InProgress ||
                   ticket.Status == TicketStatus.NoShow)
            orderby ticket.LastUpdatedOnUtc descending
            select new ActiveTicketProjection(
                ticket,
                practice.NameAr,
                practice.NameEn,
                doctor.NameAr,
                doctor.NameEn)).ToArrayAsync(cancellationToken);

        var results = new List<MyActiveTicketResponse>(tickets.Length);
        foreach (var item in tickets)
        {
            var patientsAhead = await CountPatientsAheadAsync(
                item.Ticket, cancellationToken);
            results.Add(new MyActiveTicketResponse(
                item.Ticket.Id,
                item.Ticket.TicketNumber,
                item.Ticket.Status,
                item.Ticket.DoctorPracticeId,
                item.PracticeNameAr,
                item.PracticeNameEn,
                item.Ticket.DoctorId,
                item.DoctorNameAr,
                item.DoctorNameEn,
                patientsAhead,
                item.Ticket.LastUpdatedOnUtc));
        }

        return results;
    }

    private IQueryable<QueueProjection> OrderedWaiting(
        Guid doctorPracticeId,
        DateOnly businessDate)
        => (from ticket in dbContext.Tickets.AsNoTracking().Include(item => item.CallAttempts)
            join patient in dbContext.Patients.AsNoTracking()
                on ticket.PatientId equals patient.Id
            where ticket.DoctorPracticeId == doctorPracticeId &&
                  ticket.BusinessDate == businessDate &&
                  ticket.Status == TicketStatus.Waiting
            orderby ticket.IsFastTrack descending,
                ticket.IsFastTrack
                    ? ticket.FastTrackGrantedOnUtc
                    : null,
                ticket.IsFastTrack
                    ? int.MinValue
                    : ticket.SegmentPrioritySnapshot descending,
                ticket.IsFastTrack
                    ? DateTime.MinValue
                    : ticket.QueueOrderTimeUtc,
                ticket.IsFastTrack ? string.Empty : patient.NameAr,
                ticket.TicketNumber
            select new QueueProjection(ticket, patient.NameAr, patient.NameEn));

    private async Task<int> CountPatientsAheadAsync(
        Ticket ticket,
        CancellationToken cancellationToken)
    {
        if (ticket.Status != TicketStatus.Waiting)
        {
            return 0;
        }

        var orderedIds = await OrderedWaiting(ticket.DoctorPracticeId, ticket.BusinessDate)
            .Select(item => item.Ticket.Id)
            .ToArrayAsync(cancellationToken);
        var index = Array.IndexOf(orderedIds, ticket.Id);
        if (index < 0)
        {
            return 0;
        }

        var activeConsultation = await dbContext.Tickets.AsNoTracking().CountAsync(
            item => item.DoctorPracticeId == ticket.DoctorPracticeId &&
                    (item.Status == TicketStatus.Called || item.Status == TicketStatus.InProgress),
            cancellationToken);
        return index + activeConsultation;
    }

    private static PracticeQueueTicketResponse MapQueue(
        Ticket ticket,
        string patientNameAr,
        string? patientNameEn)
    {
        var currentAttempts = ticket.CallAttempts
            .Where(item => item.CallCycle == ticket.CallCycle)
            .OrderBy(item => item.AttemptNumber)
            .ToArray();
        return new PracticeQueueTicketResponse(
            ticket.Id,
            ticket.TicketNumber,
            ticket.Status,
            ticket.PatientId,
            patientNameAr,
            patientNameEn,
            ticket.SegmentNameArSnapshot,
            ticket.SegmentNameEnSnapshot,
            ticket.SegmentPrioritySnapshot,
            ticket.VisitTypeCodeSnapshot,
            ticket.VisitTypeNameArSnapshot,
            ticket.VisitTypeNameEnSnapshot,
            ticket.CheckInTimeUtc,
            ticket.QueueOrderTimeUtc,
            ticket.IsFastTrack,
            ticket.CallCycle,
            currentAttempts.Length,
            currentAttempts.LastOrDefault()?.Outcome,
            TicketRowVersion(ticket.RowVersion));
    }

    private static string TicketRowVersion(byte[] value) => Convert.ToBase64String(value);

    private sealed record QueueProjection(
        Ticket Ticket,
        string PatientNameAr,
        string? PatientNameEn);

    private sealed record ActiveTicketProjection(
        Ticket Ticket,
        string PracticeNameAr,
        string? PracticeNameEn,
        string DoctorNameAr,
        string? DoctorNameEn);
}
