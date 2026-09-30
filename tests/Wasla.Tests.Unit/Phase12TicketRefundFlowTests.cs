using Wasla.Domain.Tickets;

namespace Wasla.Tests.Unit;

public sealed class Phase12TicketRefundFlowTests
{
    [Fact]
    public void No_show_ticket_can_be_cancelled_before_refund_and_cannot_be_restored_after_cancel()
    {
        var now = new DateTime(2026, 9, 30, 8, 0, 0, DateTimeKind.Utc);
        var actor = Guid.NewGuid();
        var ticket = Ticket.CreateWalkIn(new TicketCreationSnapshot(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            new DateOnly(2026, 9, 30), 1, TicketSource.WalkIn, Guid.NewGuid(),
            "شريحة", "Segment", 1, Guid.NewGuid(), "NewConsultation",
            "كشف جديد", "New consultation", 500m, "Africa/Cairo",
            now, now, CheckInMode.WalkIn, actor, null)).Value;

        Assert.True(ticket.Call(actor, now.AddMinutes(1)).IsSuccess);
        Assert.True(ticket.ConfirmNoResponse(actor, 1, now.AddMinutes(2)).IsSuccess);
        Assert.Equal(TicketStatus.NoShow, ticket.Status);

        Assert.True(ticket.Cancel(actor, "Patient requested cancellation", now.AddMinutes(3)).IsSuccess);
        Assert.Equal(TicketStatus.Cancelled, ticket.Status);
        Assert.Null(ticket.InProgressOnUtc);
        Assert.Contains(ticket.History, item =>
            item.EventType == TicketHistoryEventType.Cancelled &&
            item.FromStatus == TicketStatus.NoShow);
        Assert.True(ticket.RestoreNoShow(actor, false, now.AddMinutes(4)).IsFailure);
    }
}
