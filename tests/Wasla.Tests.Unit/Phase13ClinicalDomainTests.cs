using System.Text.Json;
using Wasla.Domain.Clinical;
using Wasla.Domain.Tickets;

namespace Wasla.Tests.Unit;

public sealed class Phase13ClinicalDomainTests
{
    private static readonly DateTime Now = new(2026, 10, 4, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 10, 4);
    private static readonly Guid Actor = Guid.NewGuid();
    private static Ticket Ticket(bool start = true)
    {
        var ticket = Wasla.Domain.Tickets.Ticket.CreateWalkIn(new TicketCreationSnapshot(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), null, Today, 1, TicketSource.WalkIn, Guid.NewGuid(), "عادي", "Normal", 0,
            Guid.NewGuid(), "NewConsultation", "كشف", "Consultation", 300, "Africa/Cairo", Now, Now, CheckInMode.WalkIn, Actor, null)).Value;
        if (start) { ticket.Call(Actor, Now); ticket.StartVisit(Actor, Now); }
        return ticket;
    }
    private static (Ticket Ticket, MedicalEncounter Encounter) Encounter(bool complete = false)
    {
        var ticket = Ticket(); var encounter = MedicalEncounter.Start(ticket, Actor, Now).Value;
        if (complete) { encounter.UpdateClinicalNotes("Original clinical notes", Actor, Now); encounter.Complete(ticket, Actor, Now); }
        return (ticket, encounter);
    }
    [Fact]
    public void Encounter_requires_started_ticket_and_copies_trusted_relationships()
    {
        Assert.True(MedicalEncounter.Start(Ticket(false), Actor, Now).IsFailure);
        var (ticket, e) = Encounter();
        Assert.Equal(ticket.Id, e.TicketId); Assert.Equal(ticket.PatientId, e.PatientId);
        Assert.Equal(ticket.DoctorId, e.DoctorId); Assert.Equal(ticket.DoctorPracticeId, e.DoctorPracticeId);
        Assert.Equal(EncounterStatus.InProgress, e.Status); Assert.Null(e.ClinicalNotes); Assert.Null(e.CompletedAtUtc);
    }
    [Fact]
    public void Completion_requires_notes_and_matching_in_progress_ticket_but_no_diagnosis()
    {
        var (ticket, e) = Encounter();
        Assert.Equal("MedicalEncounter.ClinicalNotesRequired", e.Complete(ticket, Actor, Now).Errors[0].Code);
        Assert.True(e.UpdateClinicalNotes("  ", Actor, Now).IsSuccess);
        Assert.True(e.Complete(ticket, Actor, Now).IsFailure);
        Assert.True(e.UpdateClinicalNotes("Documented consultation", Actor, Now).IsSuccess);
        Assert.True(e.Complete(Ticket(), Actor, Now).IsFailure);
        Assert.True(e.Complete(ticket, Actor, Now).IsSuccess); Assert.Equal(Now, e.CompletedAtUtc);
        Assert.True(e.Complete(ticket, Actor, Now).IsFailure);
        Assert.True(e.UpdateClinicalNotes("ordinary edit", Actor, Now).IsFailure);
        Assert.True(e.AddDiagnosis(Guid.NewGuid(), DiagnosisType.Primary, "late diagnosis", null, Actor, Now).IsFailure);
    }
    [Theory]
    [InlineData(8000, true)]
    [InlineData(8001, false)]
    public void Clinical_notes_enforce_length(int length, bool accepted)
        => Assert.Equal(accepted, Encounter().Encounter.UpdateClinicalNotes(new string('x', length), Actor, Now).IsSuccess);
    [Fact]
    public void Diagnosis_invariants_hold_after_each_draft_mutation()
    {
        var e = Encounter().Encounter; var primary = Guid.NewGuid(); var secondary = Guid.NewGuid();
        Assert.Equal("Diagnosis.PrimaryRequired", e.AddDiagnosis(secondary, DiagnosisType.Secondary, "secondary", null, Actor, Now).Errors[0].Code);
        Assert.True(e.AddDiagnosis(primary, DiagnosisType.Primary, " Primary ", "notes", Actor, Now).IsSuccess);
        Assert.Equal("Diagnosis.Duplicate", e.AddDiagnosis(Guid.NewGuid(), DiagnosisType.Secondary, "primary", null, Actor, Now).Errors[0].Code);
        Assert.Equal("Diagnosis.MultiplePrimary", e.AddDiagnosis(Guid.NewGuid(), DiagnosisType.Primary, "Another", null, Actor, Now).Errors[0].Code);
        Assert.True(e.AddDiagnosis(secondary, DiagnosisType.Secondary, "Secondary", null, Actor, Now).IsSuccess);
        Assert.True(e.RemoveDiagnosis(primary, Actor, Now).IsFailure);
        Assert.True(e.UpdateDiagnosis(primary, DiagnosisType.Secondary, "Primary", null, Actor, Now).IsFailure);
        Assert.True(e.UpdateDiagnosis(secondary, DiagnosisType.Secondary, "Corrected secondary", null, Actor, Now).IsSuccess);
        Assert.True(e.RemoveDiagnosis(secondary, Actor, Now).IsSuccess);
        Assert.True(e.RemoveDiagnosis(primary, Actor, Now).IsSuccess); Assert.Empty(e.Diagnoses);
    }
    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(500, 2000, true)]
    [InlineData(501, 0, false)]
    [InlineData(1, 2001, false)]
    public void Diagnosis_text_limits_are_enforced(int textLength, int notesLength, bool accepted)
        => Assert.Equal(accepted, Encounter().Encounter.AddDiagnosis(Guid.NewGuid(), DiagnosisType.Primary,
            new string('x', textLength), new string('n', notesLength), Actor, Now).IsSuccess);
    [Fact]
    public void Amendment_stages_all_changes_preserves_before_state_and_voids_instead_of_deleting()
    {
        var (ticket, e) = Encounter(); var original = Guid.NewGuid();
        e.UpdateClinicalNotes("Original notes", Actor, Now);
        e.AddDiagnosis(original, DiagnosisType.Primary, "Original diagnosis", null, Actor, Now);
        e.Complete(ticket, Actor, Now);
        var originalCreated = e.Diagnoses.Single().CreatedOnUtc;
        var amendedAt = Now.AddHours(1);
        var changes = new EncounterCorrection[]
        {
            new(AmendmentTarget.ClinicalNotes, AmendmentChangeType.Updated, ClinicalNotes: "Corrected notes"),
            new(AmendmentTarget.Diagnosis, AmendmentChangeType.Removed, original),
            new(AmendmentTarget.Diagnosis, AmendmentChangeType.Added, Type: DiagnosisType.Primary, DisplayText: "Corrected primary"),
            new(AmendmentTarget.Diagnosis, AmendmentChangeType.Added, Type: DiagnosisType.Secondary, DisplayText: "Secondary")
        };
        Assert.True(e.Amend("Clinical correction", changes, e.DoctorId, Actor, amendedAt, Serialize).IsSuccess);
        Assert.Equal("Corrected notes", e.ClinicalNotes); Assert.Equal(3, e.Diagnoses.Count);
        var voided = e.Diagnoses.Single(d => d.Id == original);
        Assert.True(voided.IsVoided); Assert.Equal(originalCreated, voided.CreatedOnUtc); Assert.Equal(Actor, voided.CreatedByApplicationUserId);
        var amendment = Assert.Single(e.Amendments); Assert.Equal(1, amendment.SequenceNumber); Assert.Equal(4, amendment.Changes.Count);
        Assert.Contains(amendment.Changes, c => c.BeforeSnapshot != null && c.BeforeSnapshot.Contains("Original notes", StringComparison.Ordinal));
        var currentPrimary = e.Diagnoses.Single(d => !d.IsVoided && d.Type == DiagnosisType.Primary);
        Assert.True(e.Amend("Further correction", [new(AmendmentTarget.Diagnosis, AmendmentChangeType.Updated,
            currentPrimary.Id, DiagnosisType.Primary, "Final diagnosis")], e.DoctorId, Actor, amendedAt.AddHours(1), Serialize).IsSuccess);
        Assert.Equal([1, 2], e.Amendments.Select(a => a.SequenceNumber));
        Assert.Equal("Original diagnosis", voided.DisplayText);
    }
    [Fact]
    public void Invalid_amendments_do_not_partially_mutate_aggregate()
    {
        var e = Encounter(true).Encounter;
        Assert.True(e.Amend(" ", [new(AmendmentTarget.ClinicalNotes, AmendmentChangeType.Updated, ClinicalNotes: "change")], e.DoctorId, Actor, Now, Serialize).IsFailure);
        Assert.True(e.Amend("reason", [new(AmendmentTarget.ClinicalNotes, AmendmentChangeType.Updated, ClinicalNotes: "changed"),
            new(AmendmentTarget.Diagnosis, AmendmentChangeType.Added, Type: DiagnosisType.Secondary, DisplayText: "secondary")], e.DoctorId, Actor, Now, Serialize).IsFailure);
        Assert.Equal("Original clinical notes", e.ClinicalNotes); Assert.Empty(e.Amendments); Assert.Empty(e.Diagnoses);
        var draft = Encounter().Encounter;
        Assert.True(draft.Amend("reason", [new(AmendmentTarget.ClinicalNotes, AmendmentChangeType.Updated, ClinicalNotes: "changed")], draft.DoctorId, Actor, Now, Serialize).IsFailure);
    }
    [Fact]
    public void Eligibility_requires_completed_source_and_business_date()
    {
        Assert.True(FollowUpEligibility.Create(Encounter().Encounter, Today, Today, Actor, Now).IsFailure);
        var source = Encounter(true).Encounter;
        Assert.True(FollowUpEligibility.Create(source, default, Today, Actor, Now).IsFailure);
        Assert.True(FollowUpEligibility.Create(source, Today.AddDays(-1), Today, Actor, Now).IsFailure);
        Assert.True(FollowUpEligibility.Create(source, Today, Today, Actor, Now).IsSuccess);
    }
    [Theory]
    [InlineData(0, FollowUpEligibilityStatus.Available)]
    [InlineData(1, FollowUpEligibilityStatus.Expired)]
    public void Release_returns_available_or_expired_according_to_business_date(int days, FollowUpEligibilityStatus expected)
    {
        var source = Encounter(true).Encounter; var f = FollowUpEligibility.Create(source, Today, Today, Actor, Now).Value;
        var reservationId = Guid.NewGuid();
        Assert.True(f.Reserve(reservationId, null, Today, Today, Actor, Now).IsSuccess);
        Assert.True(f.Reserve(null, Guid.NewGuid(), Today, Today, Actor, Now).IsFailure);
        Assert.True(f.Release(reservationId, null, Today.AddDays(days), Actor, Now).IsSuccess);
        Assert.Equal(expected, f.Status); Assert.Null(f.ReservedReservationId); Assert.Null(f.ReservedTicketId);
    }
    [Fact]
    public void Eligibility_transfer_and_consumption_are_single_use()
    {
        var source = Encounter(true).Encounter; var f = FollowUpEligibility.Create(source, Today.AddDays(3), Today, Actor, Now).Value;
        var ticket = Ticket();
        // Match the workflow's trusted relationships for a later encounter.
        var snapshot = new TicketCreationSnapshot(Guid.NewGuid(), source.DoctorId, source.DoctorPracticeId, source.PatientId,
            Guid.NewGuid(), Today, 2, TicketSource.Reservation, ticket.SegmentId, "عادي", "Normal", 0, ticket.VisitTypeId,
            "FollowUp", "متابعة", "Follow-up", 100, "Africa/Cairo", Now, Now, CheckInMode.Normal, Actor, null, f.Id);
        ticket = Wasla.Domain.Tickets.Ticket.CreateFromReservation(snapshot).Value;
        f.Reserve(ticket.ReservationId, null, Today, Today, Actor, Now);
        Assert.True(f.TransferToTicket(ticket.ReservationId!.Value, ticket.Id, Today, Actor, Now).IsSuccess);
        Assert.Null(f.ReservedReservationId); Assert.Equal(ticket.Id, f.ReservedTicketId);
        ticket.Call(Actor, Now); ticket.StartVisit(Actor, Now);
        var e = MedicalEncounter.Start(ticket, Actor, Now).Value;
        Assert.True(f.Consume(e, Today, Actor, Now).IsFailure);
        e.UpdateClinicalNotes("Follow-up complete", Actor, Now); e.Complete(ticket, Actor, Now);
        Assert.True(f.Consume(e, Today, Actor, Now).IsSuccess); Assert.Equal(e.Id, f.ConsumedEncounterId);
        Assert.Equal(FollowUpEligibilityStatus.Consumed, f.Status);
        Assert.True(f.Reserve(null, Guid.NewGuid(), Today, Today, Actor, Now).IsFailure);
    }
    [Fact]
    public void Expiry_and_appointment_deadline_do_not_depend_on_scheduler()
    {
        var f = FollowUpEligibility.Create(Encounter(true).Encounter, Today, Today, Actor, Now).Value;
        Assert.Equal("FollowUpEligibility.DateOutsideEligibility", f.Reserve(Guid.NewGuid(), null, Today, Today.AddDays(1), Actor, Now).Errors[0].Code);
        Assert.Equal("FollowUpEligibility.Expired", f.Reserve(Guid.NewGuid(), null, Today.AddDays(1), Today.AddDays(1), Actor, Now).Errors[0].Code);
        Assert.Equal(FollowUpEligibilityStatus.Expired, f.EffectiveStatus(Today.AddDays(1)));
        Assert.True(f.Reserve(Guid.NewGuid(), Guid.NewGuid(), Today, Today, Actor, Now).IsFailure);
    }
    [Fact]
    public void No_response_uses_attempt_number_even_when_database_materializes_children_out_of_order()
    {
        var ticket = Ticket(false);
        ticket.Call(Actor, Now); ticket.ConfirmNoResponse(Actor, 3, Now); ticket.Recall(Actor, 3, Now);
        var field = typeof(Ticket).GetField("_callAttempts", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        ((List<TicketCallAttempt>)field.GetValue(ticket)!).Reverse();
        Assert.True(ticket.ConfirmNoResponse(Actor, 3, Now).IsSuccess);
        Assert.True(ticket.Recall(Actor, 3, Now).IsSuccess);
        Assert.True(ticket.ConfirmNoResponse(Actor, 3, Now).IsSuccess);
        Assert.Equal(TicketStatus.NoShow, ticket.Status);
    }

    private static string Serialize(object value) => JsonSerializer.Serialize(value);
}
