using Wasla.Domain.Clinical;
using Wasla.Domain.Tickets;

namespace Wasla.Tests.Unit;

public sealed class Phase16HiddenAccessTests
{
    [Fact]
    public void Approval_is_scoped_and_terminal_states_cannot_reactivate()
    {
        var (_, source, actor, now) = Phase16VisibilityTests.Visit(true);
        source.ChangeVisibility(EncounterVisibility.Hidden, source.DoctorId, source.DoctorPracticeId, actor, now);
        var t = Ticket.CreateWalkIn(new TicketCreationSnapshot(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), source.PatientId, null,
            DateOnly.FromDateTime(now), 1, TicketSource.WalkIn, Guid.NewGuid(), "عادي", "Normal", 0, Guid.NewGuid(), "NewConsultation", "كشف", "Visit", 300,
            "Africa/Cairo", now, now, CheckInMode.WalkIn, actor, null)).Value;
        t.Call(actor, now); t.StartVisit(actor, now); var e = MedicalEncounter.Start(t, actor, now).Value;
        Assert.True(HiddenEncounterAccessRequest.Create(source, t, e, t.DoctorId, t.DoctorPracticeId, "  ", actor, now).IsFailure);
        var r = HiddenEncounterAccessRequest.Create(source, t, e, t.DoctorId, t.DoctorPracticeId, " Reason ", actor, now).Value;
        Assert.Equal("Reason", r.Reason);
        Assert.True(r.Decide(true, source, t, e, t.DoctorId, actor, now).IsFailure);
        Assert.True(r.Decide(true, source, t, e, source.DoctorId, actor, now).IsSuccess);
        Assert.True(r.ContextLive(source, t, e));
        source.ChangeVisibility(EncounterVisibility.Visible, source.DoctorId, source.DoctorPracticeId, actor, now);
        source.ChangeVisibility(EncounterVisibility.Hidden, source.DoctorId, source.DoctorPracticeId, actor, now);
        Assert.False(r.ContextLive(source, t, e)); Assert.Equal(HiddenAccessRequestStatus.Expired, r.EffectiveStatus(false));
        Assert.True(r.Revoke(source.DoctorId, actor, now).IsSuccess);
        Assert.True(r.Decide(true, source, t, e, source.DoctorId, actor, now).IsFailure);
        r.Expire(actor, now); Assert.Equal(HiddenAccessRequestStatus.Revoked, r.Status);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Treatment_completion_expires_pending_and_approved_requests(bool approve)
    {
        var (_, source, actor, now) = Phase16VisibilityTests.Visit(true);
        source.ChangeVisibility(EncounterVisibility.Hidden, source.DoctorId, source.DoctorPracticeId, actor, now);
        var t = Ticket.CreateWalkIn(new TicketCreationSnapshot(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), source.PatientId, null,
            DateOnly.FromDateTime(now), 1, TicketSource.WalkIn, Guid.NewGuid(), "عادي", "Normal", 0, Guid.NewGuid(), "NewConsultation", "كشف", "Visit", 300,
            "Africa/Cairo", now, now, CheckInMode.WalkIn, actor, null)).Value;
        t.Call(actor, now); t.StartVisit(actor, now); var e = MedicalEncounter.Start(t, actor, now).Value;
        var r = HiddenEncounterAccessRequest.Create(source, t, e, t.DoctorId, t.DoctorPracticeId, "Reason", actor, now).Value;
        if (approve) r.Decide(true, source, t, e, source.DoctorId, actor, now);
        e.UpdateClinicalNotes("note", actor, now); e.Complete(t, actor, now); t.Complete(actor, now);
        Assert.False(r.ContextLive(source, t, e));
        r.Expire(actor, now); Assert.Equal(HiddenAccessRequestStatus.Expired, r.Status);
        Assert.True(r.Decide(true, source, t, e, source.DoctorId, actor, now).IsFailure);
        Assert.Equal(approve ? 3 : 2, r.History.Count);
    }
}
