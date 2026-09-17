using Wasla.Domain.Payments;
using Wasla.Domain.Tickets;

namespace Wasla.Tests.Unit;

public sealed class Phase11TicketDomainTests
{
    private static readonly DateTime Now = new(2026, 9, 17, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Reservation_ticket_starts_waiting_and_preserves_commercial_snapshots()
    {
        var ticket = CreateReservationTicket().Value;

        Assert.Equal(TicketStatus.Waiting, ticket.Status);
        Assert.Equal(TicketSource.Reservation, ticket.Source);
        Assert.Equal("شريحة", ticket.SegmentNameArSnapshot);
        Assert.Equal(7, ticket.SegmentPrioritySnapshot);
        Assert.Equal("NewConsultation", ticket.VisitTypeCodeSnapshot);
        Assert.Equal(250m, ticket.PriceSnapshot);
        Assert.Equal(CheckInMode.Normal, ticket.CheckInMode);
        Assert.Contains(ticket.History, item =>
            item.EventType == TicketHistoryEventType.ReservationCheckedIn);
    }

    [Fact]
    public void Waiting_ticket_cannot_start_directly()
    {
        var ticket = CreateReservationTicket().Value;

        var result = ticket.StartVisit(Guid.NewGuid(), Now.AddMinutes(1));

        Assert.True(result.IsFailure);
        Assert.Equal(TicketStatus.Waiting, ticket.Status);
    }

    [Fact]
    public void Valid_call_start_complete_path_is_domain_owned()
    {
        var ticket = CreateReservationTicket().Value;
        var actor = Guid.NewGuid();

        Assert.True(ticket.Call(actor, Now.AddMinutes(1)).IsSuccess);
        Assert.Equal(TicketStatus.Called, ticket.Status);
        Assert.True(ticket.StartVisit(actor, Now.AddMinutes(2)).IsSuccess);
        Assert.Equal(TicketStatus.InProgress, ticket.Status);
        Assert.True(ticket.Complete(actor, Now.AddMinutes(20)).IsSuccess);
        Assert.Equal(TicketStatus.Completed, ticket.Status);
        Assert.Contains(ticket.History, item => item.EventType == TicketHistoryEventType.Completed);
    }

    [Fact]
    public void Completed_ticket_is_terminal()
    {
        var ticket = CreateReservationTicket().Value;
        var actor = Guid.NewGuid();
        ticket.Call(actor, Now.AddMinutes(1));
        ticket.StartVisit(actor, Now.AddMinutes(2));
        ticket.Complete(actor, Now.AddMinutes(3));

        Assert.True(ticket.Cancel(actor, "wrong patient", Now.AddMinutes(4)).IsFailure);
        Assert.True(ticket.RestoreNoShow(actor, true, Now.AddMinutes(4)).IsFailure);
        Assert.Equal(TicketStatus.Completed, ticket.Status);
    }

    [Fact]
    public void Final_confirmed_no_response_automatically_marks_no_show()
    {
        var ticket = CreateReservationTicket().Value;
        var actor = Guid.NewGuid();
        ticket.Call(actor, Now.AddMinutes(1));

        Assert.True(ticket.ConfirmNoResponse(actor, 3, Now.AddMinutes(2)).IsSuccess);
        Assert.Equal(TicketStatus.Called, ticket.Status);
        Assert.True(ticket.Recall(actor, 3, Now.AddMinutes(3)).IsSuccess);
        Assert.True(ticket.ConfirmNoResponse(actor, 3, Now.AddMinutes(4)).IsSuccess);
        Assert.Equal(TicketStatus.Called, ticket.Status);
        Assert.True(ticket.Recall(actor, 3, Now.AddMinutes(5)).IsSuccess);
        Assert.True(ticket.ConfirmNoResponse(actor, 3, Now.AddMinutes(6)).IsSuccess);

        Assert.Equal(TicketStatus.NoShow, ticket.Status);
        Assert.Equal(3, ticket.CallAttempts.Count);
        Assert.All(ticket.CallAttempts, attempt =>
            Assert.Equal(TicketCallAttemptOutcome.NoResponse, attempt.Outcome));
        Assert.Contains(ticket.History, item => item.EventType == TicketHistoryEventType.AutoNoShow);
    }

    [Fact]
    public void Restore_keeps_identity_and_snapshots_and_can_grant_one_time_fast_track()
    {
        var ticket = MoveToNoShow();
        var originalId = ticket.Id;
        var originalNumber = ticket.TicketNumber;
        var originalCheckIn = ticket.CheckInTimeUtc;

        Assert.True(ticket.RestoreNoShow(Guid.NewGuid(), true, Now.AddMinutes(10)).IsSuccess);

        Assert.Equal(originalId, ticket.Id);
        Assert.Equal(originalNumber, ticket.TicketNumber);
        Assert.Equal(originalCheckIn, ticket.CheckInTimeUtc);
        Assert.Equal(Now.AddMinutes(10), ticket.QueueOrderTimeUtc);
        Assert.True(ticket.IsFastTrack);
        Assert.Equal(2, ticket.CallCycle);
        Assert.Contains(ticket.History, item => item.EventType == TicketHistoryEventType.FastTrackGranted);
        ticket.Call(Guid.NewGuid(), Now.AddMinutes(11));
        Assert.False(ticket.IsFastTrack);
    }

    [Fact]
    public void Restore_beyond_threshold_returns_to_normal_waiting_order()
    {
        var ticket = MoveToNoShow();

        Assert.True(ticket.RestoreNoShow(Guid.NewGuid(), false, Now.AddMinutes(10)).IsSuccess);

        Assert.Equal(TicketStatus.Waiting, ticket.Status);
        Assert.False(ticket.IsFastTrack);
        Assert.Equal(Now.AddMinutes(10), ticket.QueueOrderTimeUtc);
    }

    [Fact]
    public void In_progress_ticket_cannot_be_cancelled()
    {
        var ticket = CreateReservationTicket().Value;
        var actor = Guid.NewGuid();
        ticket.Call(actor, Now.AddMinutes(1));
        ticket.StartVisit(actor, Now.AddMinutes(2));

        var result = ticket.Cancel(actor, "cancel", Now.AddMinutes(3));

        Assert.True(result.IsFailure);
        Assert.Equal(TicketStatus.InProgress, ticket.Status);
    }

    [Fact]
    public void Operational_day_end_cancels_waiting_but_not_in_progress()
    {
        var waiting = CreateReservationTicket().Value;
        var inProgress = CreateReservationTicket().Value;
        var actor = Guid.NewGuid();
        inProgress.Call(actor, Now.AddMinutes(1));
        inProgress.StartVisit(actor, Now.AddMinutes(2));

        Assert.True(waiting.CancelForOperationalDay(Now.AddHours(10)).IsSuccess);
        Assert.Equal(TicketCancellationReasons.OperationalDayEnded, waiting.CancellationReasonCode);
        Assert.True(inProgress.CancelForOperationalDay(Now.AddHours(10)).IsFailure);
        Assert.Equal(TicketStatus.InProgress, inProgress.Status);
    }

    [Fact]
    public void Payment_requires_exact_ticket_price_and_remains_paid()
    {
        var ticket = CreateReservationTicket().Value;
        var payment = Payment.RecordPaid(
            Guid.NewGuid(), ticket.DoctorId, ticket.DoctorPracticeId, ticket.PatientId,
            ticket.ReservationId, ticket.Id, 250m, ticket.PriceSnapshot,
            Guid.NewGuid(), Now);
        var wrongAmount = Payment.RecordPaid(
            Guid.NewGuid(), ticket.DoctorId, ticket.DoctorPracticeId, ticket.PatientId,
            ticket.ReservationId, ticket.Id, 200m, ticket.PriceSnapshot,
            Guid.NewGuid(), Now);

        Assert.True(payment.IsSuccess);
        Assert.Equal(PaymentStatus.Paid, payment.Value.Status);
        Assert.True(wrongAmount.IsFailure);
        ticket.Cancel(Guid.NewGuid(), "cancel", Now.AddMinutes(1));
        Assert.Equal(PaymentStatus.Paid, payment.Value.Status);
    }

    private static Ticket MoveToNoShow()
    {
        var ticket = CreateReservationTicket().Value;
        var actor = Guid.NewGuid();
        ticket.Call(actor, Now.AddMinutes(1));
        ticket.ConfirmNoResponse(actor, 1, Now.AddMinutes(2));
        return ticket;
    }

    private static BuildingBlock.Domain.Results.Result<Ticket> CreateReservationTicket()
        => Ticket.CreateFromReservation(new TicketCreationSnapshot(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 9, 17),
            4,
            TicketSource.Reservation,
            Guid.NewGuid(),
            "شريحة",
            "Segment",
            7,
            Guid.NewGuid(),
            "NewConsultation",
            "كشف جديد",
            "New consultation",
            250m,
            "Africa/Cairo",
            Now,
            Now,
            CheckInMode.Normal,
            Guid.NewGuid(),
            null));
}
