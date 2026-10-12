using Wasla.Domain.Clinical;
using Wasla.Domain.Tickets;

namespace Wasla.Tests.Unit;

public sealed class Phase16VisibilityTests
{
    internal static (Ticket Ticket, MedicalEncounter Encounter, Guid Actor, DateTime Now) Visit(bool completed = false)
    {
        var actor = Guid.NewGuid(); var now = new DateTime(2026, 10, 12, 9, 0, 0, DateTimeKind.Utc);
        var ticket = Ticket.CreateWalkIn(new TicketCreationSnapshot(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), null, DateOnly.FromDateTime(now), 1, TicketSource.WalkIn, Guid.NewGuid(), "عادي", "Normal", 0,
            Guid.NewGuid(), "NewConsultation", "كشف", "Consultation", 300, "Africa/Cairo", now, now, CheckInMode.WalkIn, actor, null)).Value;
        ticket.Call(actor, now); ticket.StartVisit(actor, now);
        var e = MedicalEncounter.Start(ticket, actor, now).Value;
        if (completed) { e.UpdateClinicalNotes("Private clinical note", actor, now); e.Complete(ticket, actor, now); }
        return (ticket, e, actor, now);
    }
    [Fact]
    public void Visibility_is_independent_from_lifecycle_and_requires_owner_and_practice()
    {
        var (_, e, actor, now) = Visit();
        Assert.Equal(EncounterVisibility.Visible, e.Visibility);
        Assert.True(e.ChangeVisibility(EncounterVisibility.Hidden, e.DoctorId, e.DoctorPracticeId, actor, now).IsFailure);
        var (_, done, _, _) = Visit(true);
        Assert.True(done.ChangeVisibility(EncounterVisibility.Hidden, Guid.NewGuid(), done.DoctorPracticeId, actor, now).IsFailure);
        Assert.True(done.ChangeVisibility(EncounterVisibility.Hidden, done.DoctorId, Guid.NewGuid(), actor, now).IsFailure);
        Assert.True(done.ChangeVisibility((EncounterVisibility)99, done.DoctorId, done.DoctorPracticeId, actor, now).IsFailure);
    }
    [Fact]
    public void Completion_selects_hidden_and_toggles_increment_once_without_changing_content()
    {
        var (ticket, e, actor, now) = Visit(); e.UpdateClinicalNotes("Private note", actor, now);
        Assert.True(e.Complete(ticket, actor, now, EncounterVisibility.Hidden).IsSuccess);
        Assert.Equal(EncounterVisibility.Hidden, e.Visibility); Assert.Equal(1, e.VisibilityRevision);
        Assert.Equal("Completion", Assert.Single(e.VisibilityHistory).Source);
        e.ChangeVisibility(EncounterVisibility.Hidden, e.DoctorId, e.DoctorPracticeId, actor, now);
        Assert.Equal(1, e.VisibilityRevision);
        e.ChangeVisibility(EncounterVisibility.Visible, e.DoctorId, e.DoctorPracticeId, actor, now);
        e.ChangeVisibility(EncounterVisibility.Hidden, e.DoctorId, e.DoctorPracticeId, actor, now);
        Assert.Equal(3, e.VisibilityRevision); Assert.Equal(3, e.VisibilityHistory.Count);
        Assert.Equal("Private note", e.ClinicalNotes);
    }
}
