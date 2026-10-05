using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Clinical;
using Wasla.Domain.Tickets;
using static Wasla.Tests.Integration.Phase13ApiFixture;

namespace Wasla.Tests.Integration;

[Trait("Category", "SQLServerConcurrency")]
public sealed class Phase13SqlServerTests
{
    [Fact]
    public async Task Eligibility_uniqueness_stale_claim_and_completion_consumption_are_atomic()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(true);
        using var doctor = await app.ClientAsync("doctor"); using var reception = await app.ClientAsync("reception"); using var patient = await app.ClientAsync("patient");
        var source = await app.CompleteAsync(doctor, await app.StartAsync(doctor, await app.WalkInAsync(reception)));
        var sourceId = source.Encounter.GetProperty("encounterId").GetGuid();
        var creations = await Task.WhenAll(PostAsync(doctor, app.EncounterUrl(sourceId) + "/follow-up-eligibility", new { validUntil = app.Today.AddDays(2) }),
            PostAsync(doctor, app.EncounterUrl(sourceId) + "/follow-up-eligibility", new { validUntil = app.Today.AddDays(2) }));
        System.Text.Json.JsonElement eligibility;
        try
        {
            Assert.Single(creations, r => r.StatusCode == HttpStatusCode.Created); Assert.Single(creations, r => r.StatusCode == HttpStatusCode.Conflict);
            eligibility = await JsonAsync(creations.Single(r => r.StatusCode == HttpStatusCode.Created));
        }
        finally { foreach (var response in creations) response.Dispose(); }
        var id = eligibility.GetProperty("eligibilityId").GetGuid(); var oldVersion = eligibility.GetProperty("rowVersion").GetString();
        using var booking = await PostAsync(patient, "/api/v1/reservations", new { patientId = app.PatientId, doctorPracticeId = app.PracticeId,
            businessDate = app.Today.AddDays(1), slotStartTime = "18:00", segmentId = app.SegmentId, visitTypeId = app.FollowUpTypeId, followUpEligibilityId = id });
        Assert.Equal(HttpStatusCode.Created, booking.StatusCode); var reservation = await JsonAsync(booking);
        using var cancellation = await PostAsync(patient, $"/api/v1/reservations/mine/{reservation.GetProperty("reservationId").GetGuid()}/cancel", new
            { reasonCode = "PatientChangedPlans", rowVersion = reservation.GetProperty("rowVersion").GetString() });
        Assert.Equal(HttpStatusCode.OK, cancellation.StatusCode);
        using var staleClaim = await PostAsync(reception, $"/api/v1/practices/{app.PracticeId}/tickets/walk-in", new { patientId = app.PatientId,
            segmentId = app.SegmentId, visitTypeId = app.FollowUpTypeId, followUpEligibilityId = id, followUpEligibilityRowVersion = oldVersion, paidAmount = 100, paymentMethod = "Cash" });
        Assert.Equal(HttpStatusCode.Conflict, staleClaim.StatusCode);
        Assert.Contains("FollowUpEligibility.ConcurrencyConflict", await staleClaim.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        var ticket = await app.StartAsync(doctor, await app.WalkInAsync(reception, id)); var ticketId = ticket.GetProperty("ticketId").GetGuid();
        var encounterId = ticket.GetProperty("medicalEncounterId").GetGuid(); var encounter = await GetAsync(doctor, app.EncounterUrl(encounterId));
        var oldEncounterVersion = encounter.GetProperty("rowVersion").GetString();
        using var notes = await doctor.PatchAsJsonAsync(app.EncounterUrl(encounterId) + "/clinical-notes", new { clinicalNotes = "Follow-up documentation", rowVersion = oldEncounterVersion }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, notes.StatusCode); encounter = await JsonAsync(notes);
        using var staleComplete = await PostAsync(doctor, app.TicketUrl(ticketId) + "/complete", new
            { ticketRowVersion = ticket.GetProperty("rowVersion").GetString(), encounterRowVersion = oldEncounterVersion });
        Assert.Equal(HttpStatusCode.Conflict, staleComplete.StatusCode);
        Assert.Contains("MedicalEncounter.ConcurrencyConflict", await staleComplete.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        await app.WithDbAsync(db => db.Database.ExecuteSqlRawAsync("ALTER TABLE [FollowUpEligibilities] ADD CONSTRAINT [CK_Phase13ForcedConsumeFailure] CHECK ([Status] <> 3)", TestContext.Current.CancellationToken));
        var body = new { ticketRowVersion = ticket.GetProperty("rowVersion").GetString(), encounterRowVersion = encounter.GetProperty("rowVersion").GetString() };
        var key = Guid.NewGuid().ToString("N");
        using var failed = await PostAsync(doctor, app.TicketUrl(ticketId) + "/complete", body, key);
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        await app.WithDbAsync(async db =>
        {
            Assert.Equal(TicketStatus.InProgress, (await db.Tickets.SingleAsync(t => t.Id == ticketId, TestContext.Current.CancellationToken)).Status);
            Assert.Equal(EncounterStatus.InProgress, (await db.MedicalEncounters.SingleAsync(e => e.Id == encounterId, TestContext.Current.CancellationToken)).Status);
            Assert.Equal(FollowUpEligibilityStatus.Reserved, (await db.FollowUpEligibilities.SingleAsync(e => e.Id == id, TestContext.Current.CancellationToken)).Status);
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE [FollowUpEligibilities] DROP CONSTRAINT [CK_Phase13ForcedConsumeFailure]", TestContext.Current.CancellationToken);
        });
        using var completed = await PostAsync(doctor, app.TicketUrl(ticketId) + "/complete", body, key);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        using var replay = await PostAsync(doctor, app.TicketUrl(ticketId) + "/complete", body, key); Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        await app.WithDbAsync(async db =>
        {
            Assert.Equal(FollowUpEligibilityStatus.Consumed, (await db.FollowUpEligibilities.SingleAsync(e => e.Id == id, TestContext.Current.CancellationToken)).Status);
            Assert.Equal(1, await db.FollowUpEligibilityHistories.CountAsync(h => h.FollowUpEligibilityId == id && h.Action == "Consumed", TestContext.Current.CancellationToken));
        });
    }

    [Fact]
    public async Task Start_and_complete_roll_back_every_write_and_preserve_retry_idempotency()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(true);
        using var doctor = await app.ClientAsync("doctor"); using var reception = await app.ClientAsync("reception");
        var ticket = await app.WalkInAsync(reception); var ticketId = ticket.GetProperty("ticketId").GetGuid();
        using var call = await PostAsync(doctor, $"/api/v1/practices/{app.PracticeId}/queue/call-next", new { });
        Assert.Equal(HttpStatusCode.OK, call.StatusCode); ticket = await GetAsync(doctor, app.TicketUrl(ticketId));
        // Failure is injected at the database boundary, after domain transitions have happened.
        await app.WithDbAsync(db => db.Database.ExecuteSqlRawAsync("ALTER TABLE [MedicalEncounters] ADD CONSTRAINT [CK_Phase13ForcedStartFailure] CHECK (1=0)", TestContext.Current.CancellationToken));
        var startBody = new { rowVersion = ticket.GetProperty("rowVersion").GetString() }; var startKey = Guid.NewGuid().ToString("N");
        using var failedStart = await PostAsync(doctor, app.TicketUrl(ticketId) + "/start", startBody, startKey);
        Assert.Equal(HttpStatusCode.InternalServerError, failedStart.StatusCode);
        await app.WithDbAsync(async db =>
        {
            Assert.Equal(TicketStatus.Called, (await db.Tickets.SingleAsync(t => t.Id == ticketId)).Status);
            Assert.Empty(await db.MedicalEncounters.ToArrayAsync());
            Assert.False(await db.TicketIdempotencyRecords.AnyAsync(i => i.IdempotencyKey == startKey));
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE [MedicalEncounters] DROP CONSTRAINT [CK_Phase13ForcedStartFailure]");
        });
        using var started = await PostAsync(doctor, app.TicketUrl(ticketId) + "/start", startBody, startKey);
        Assert.True(started.StatusCode == HttpStatusCode.OK, await started.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)); ticket = await JsonAsync(started);
        using var replay = await PostAsync(doctor, app.TicketUrl(ticketId) + "/start", startBody, startKey);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var encounterId = ticket.GetProperty("medicalEncounterId").GetGuid(); var encounter = await GetAsync(doctor, app.EncounterUrl(encounterId));
        using var notes = await doctor.PatchAsJsonAsync(app.EncounterUrl(encounterId) + "/clinical-notes", new
            { clinicalNotes = "Required notes", rowVersion = encounter.GetProperty("rowVersion").GetString() }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, notes.StatusCode); encounter = await JsonAsync(notes);
        await app.WithDbAsync(db => db.Database.ExecuteSqlRawAsync("ALTER TABLE [MedicalEncounters] ADD CONSTRAINT [CK_Phase13ForcedCompleteFailure] CHECK ([Status] <> 2)", TestContext.Current.CancellationToken));
        var body = new { ticketRowVersion = ticket.GetProperty("rowVersion").GetString(), encounterRowVersion = encounter.GetProperty("rowVersion").GetString() };
        var key = Guid.NewGuid().ToString("N");
        using var failure = await PostAsync(doctor, app.TicketUrl(ticketId) + "/complete", body, key);
        Assert.Equal(HttpStatusCode.InternalServerError, failure.StatusCode);
        await app.WithDbAsync(async db =>
        {
            Assert.Equal(TicketStatus.InProgress, (await db.Tickets.SingleAsync(t => t.Id == ticketId)).Status);
            Assert.Equal(EncounterStatus.InProgress, (await db.MedicalEncounters.SingleAsync(e => e.Id == encounterId)).Status);
            Assert.False(await db.TicketIdempotencyRecords.AnyAsync(i => i.IdempotencyKey == key));
            Assert.False(await db.EncounterAuditEvents.AnyAsync(e => e.Action == "Completed"));
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE [MedicalEncounters] DROP CONSTRAINT [CK_Phase13ForcedCompleteFailure]");
        });
        using var complete = await PostAsync(doctor, app.TicketUrl(ticketId) + "/complete", body, key);
        Assert.True(complete.StatusCode == HttpStatusCode.OK, await complete.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        using var completeReplay = await PostAsync(doctor, app.TicketUrl(ticketId) + "/complete", body, key);
        Assert.Equal(HttpStatusCode.OK, completeReplay.StatusCode);
        await app.WithDbAsync(async db => Assert.Equal(1, await db.MedicalEncounters.CountAsync()));
    }

    [Fact]
    public async Task Concurrent_start_creates_one_encounter_and_concurrent_note_edits_do_not_lose_updates()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(true);
        using var doctor = await app.ClientAsync("doctor"); using var reception = await app.ClientAsync("reception");
        var ticket = await app.WalkInAsync(reception); var id = ticket.GetProperty("ticketId").GetGuid();
        using var call = await PostAsync(doctor, $"/api/v1/practices/{app.PracticeId}/queue/call-next", new { });
        Assert.Equal(HttpStatusCode.OK, call.StatusCode); ticket = await GetAsync(doctor, app.TicketUrl(id));
        var startBody = new { rowVersion = ticket.GetProperty("rowVersion").GetString() };
        var starts = await Task.WhenAll(PostAsync(doctor, app.TicketUrl(id) + "/start", startBody), PostAsync(doctor, app.TicketUrl(id) + "/start", startBody));
        try
        {
            Assert.Single(starts, r => r.StatusCode == HttpStatusCode.OK);
            Assert.Single(starts, r => r.StatusCode == HttpStatusCode.Conflict);
        }
        finally { foreach (var response in starts) response.Dispose(); }
        ticket = await GetAsync(doctor, app.TicketUrl(id)); var encounterId = ticket.GetProperty("medicalEncounterId").GetGuid();
        var encounter = await GetAsync(doctor, app.EncounterUrl(encounterId)); var rowVersion = encounter.GetProperty("rowVersion").GetString();
        var edits = await Task.WhenAll(doctor.PatchAsJsonAsync(app.EncounterUrl(encounterId) + "/clinical-notes", new { clinicalNotes = "Writer one", rowVersion }, TestContext.Current.CancellationToken),
            doctor.PatchAsJsonAsync(app.EncounterUrl(encounterId) + "/clinical-notes", new { clinicalNotes = "Writer two", rowVersion }, TestContext.Current.CancellationToken));
        try
        {
            Assert.Single(edits, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(edits, r => r.StatusCode == HttpStatusCode.Conflict);
            Assert.Contains("MedicalEncounter.ConcurrencyConflict", await edits.Single(r => r.StatusCode == HttpStatusCode.Conflict).Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        }
        finally { foreach (var response in edits) response.Dispose(); }
        await app.WithDbAsync(async db => Assert.Equal(1, await db.MedicalEncounters.CountAsync()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Concurrent_reservations_and_reservation_vs_walk_in_have_one_eligibility_claim(bool walkIn)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(true);
        using var doctor = await app.ClientAsync("doctor"); using var reception = await app.ClientAsync("reception"); using var patient = await app.ClientAsync("patient");
        var source = await app.CompleteAsync(doctor, await app.StartAsync(doctor, await app.WalkInAsync(reception)));
        var eligibility = await app.EligibilityAsync(doctor, source.Encounter.GetProperty("encounterId").GetGuid()); var id = eligibility.GetProperty("eligibilityId").GetGuid();
        object ReservationBody() => new { patientId = app.PatientId, doctorPracticeId = app.PracticeId, businessDate = app.Today.AddDays(1),
            slotStartTime = "18:00", segmentId = app.SegmentId, visitTypeId = app.FollowUpTypeId, followUpEligibilityId = id };
        var other = walkIn ? PostAsync(reception, $"/api/v1/practices/{app.PracticeId}/tickets/walk-in", new { patientId = app.PatientId,
            segmentId = app.SegmentId, visitTypeId = app.FollowUpTypeId, followUpEligibilityId = id, paidAmount = 100, paymentMethod = "Cash" })
            : PostAsync(reception, $"/api/v1/reception/practices/{app.PracticeId}/reservations", ReservationBody());
        var responses = await Task.WhenAll(PostAsync(patient, "/api/v1/reservations", ReservationBody()), other);
        try
        {
            Assert.True(responses.Count(r => r.StatusCode == HttpStatusCode.Created) == 1,
                string.Join("\n", await Task.WhenAll(responses.Select(r => r.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)))));
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        }
        finally { foreach (var response in responses) response.Dispose(); }
        await app.WithDbAsync(async db =>
        {
            var claim = await db.FollowUpEligibilities.SingleAsync(e => e.Id == id);
            Assert.Equal(FollowUpEligibilityStatus.Reserved, claim.Status);
            Assert.True(claim.ReservedReservationId.HasValue ^ claim.ReservedTicketId.HasValue);
            Assert.Equal(1, await db.Reservations.CountAsync(r => r.FollowUpEligibilityId == id) + await db.Tickets.CountAsync(t => t.FollowUpEligibilityId == id));
        });
    }

    [Fact]
    public async Task Amendment_replaces_primary_in_one_transaction_without_destroying_previous_diagnosis()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(true);
        using var doctor = await app.ClientAsync("doctor"); using var reception = await app.ClientAsync("reception");
        var ticket = await app.StartAsync(doctor, await app.WalkInAsync(reception)); var encounterId = ticket.GetProperty("medicalEncounterId").GetGuid();
        var encounter = await GetAsync(doctor, app.EncounterUrl(encounterId));
        using var added = await PostAsync(doctor, app.EncounterUrl(encounterId) + "/diagnoses", new { type = "Primary", displayText = "Initial primary",
            encounterRowVersion = encounter.GetProperty("rowVersion").GetString() });
        Assert.Equal(HttpStatusCode.Created, added.StatusCode); encounter = await JsonAsync(added);
        var diagnosisId = encounter.GetProperty("diagnoses")[0].GetProperty("diagnosisId").GetGuid();
        var completed = await app.CompleteAsync(doctor, ticket);
        using var amendment = await PostAsync(doctor, app.EncounterUrl(encounterId) + "/amendments", new { reason = "Primary corrected", encounterRowVersion = completed.Encounter.GetProperty("rowVersion").GetString(),
            changes = new object[] { new { target = "Diagnosis", changeType = "Removed", diagnosisId },
                new { target = "Diagnosis", changeType = "Added", type = "Primary", displayText = "Replacement primary" } } });
        Assert.True(amendment.StatusCode == HttpStatusCode.Created, await amendment.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        await app.WithDbAsync(async db =>
        {
            Assert.True((await db.Diagnoses.SingleAsync(d => d.Id == diagnosisId)).IsVoided);
            Assert.Equal(1, await db.Diagnoses.CountAsync(d => d.MedicalEncounterId == encounterId && !d.IsVoided && d.Type == DiagnosisType.Primary));
            Assert.Equal(2, await db.EncounterAmendmentChanges.CountAsync());
        });
    }
}
