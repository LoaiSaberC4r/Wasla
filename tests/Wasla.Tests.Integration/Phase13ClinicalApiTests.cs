using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Clinical;
using Wasla.Domain.Reservations;
using Wasla.Domain.Security;
using static Wasla.Tests.Integration.Phase13ApiFixture;

namespace Wasla.Tests.Integration;

public sealed class Phase13ClinicalApiTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Consultation_diagnosis_amendment_and_patient_current_view_work_end_to_end(bool sqlServer)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sqlServer);
        using var doctor = await app.ClientAsync("doctor"); using var reception = await app.ClientAsync("reception");
        using var patient = await app.ClientAsync("patient");
        var ticket = await app.StartAsync(doctor, await app.WalkInAsync(reception));
        var encounterId = ticket.GetProperty("medicalEncounterId").GetGuid(); var url = app.EncounterUrl(encounterId);
        var encounter = await GetAsync(doctor, $"/api/v1/doctors/me/practices/{app.PracticeId}/tickets/{ticket.GetProperty("ticketId").GetGuid()}/encounter");
        Assert.Equal(encounterId, encounter.GetProperty("encounterId").GetGuid());
        Assert.True(encounter.GetProperty("capabilities").GetProperty("canEditClinicalNotes").GetBoolean());
        using (var draft = await patient.GetAsync($"/api/v1/encounters/mine/{encounterId}", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, draft.StatusCode);
        var staleVersion = encounter.GetProperty("rowVersion").GetString();
        using var notes = await doctor.PatchAsJsonAsync(url + "/clinical-notes", new { clinicalNotes = "Private notes", rowVersion = staleVersion }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, notes.StatusCode); encounter = await JsonAsync(notes);
        Assert.NotEqual(staleVersion, encounter.GetProperty("rowVersion").GetString());
        using (var stale = await doctor.PatchAsJsonAsync(url + "/clinical-notes", new { clinicalNotes = "Lost edit", rowVersion = staleVersion }, TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            Assert.Contains("MedicalEncounter.ConcurrencyConflict", await stale.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        }
        using var primary = await PostAsync(doctor, url + "/diagnoses", new { type = "Primary", displayText = "Initial diagnosis", notes = (string?)null,
            encounterRowVersion = encounter.GetProperty("rowVersion").GetString() });
        Assert.Equal(HttpStatusCode.Created, primary.StatusCode); encounter = await JsonAsync(primary);
        var primaryId = encounter.GetProperty("diagnoses")[0].GetProperty("diagnosisId").GetGuid();
        using var secondary = await PostAsync(doctor, url + "/diagnoses", new { type = "Secondary", displayText = "Temporary diagnosis", notes = (string?)null,
            encounterRowVersion = encounter.GetProperty("rowVersion").GetString() });
        Assert.Equal(HttpStatusCode.Created, secondary.StatusCode); encounter = await JsonAsync(secondary);
        var secondaryId = encounter.GetProperty("diagnoses")[1].GetProperty("diagnosisId").GetGuid();
        using var updated = await doctor.PutAsJsonAsync(url + $"/diagnoses/{secondaryId}", new { type = "Secondary", displayText = "Edited secondary",
            notes = "detail", encounterRowVersion = encounter.GetProperty("rowVersion").GetString() }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode); encounter = await JsonAsync(updated);
        using var delete = new HttpRequestMessage(HttpMethod.Delete, url + $"/diagnoses/{secondaryId}")
            { Content = JsonContent.Create(new { rowVersion = encounter.GetProperty("rowVersion").GetString() }) };
        using var deleted = await doctor.SendAsync(delete, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode); var deletedJson = await JsonAsync(deleted);
        Assert.NotEqual(encounter.GetProperty("rowVersion").GetString(), deletedJson.GetProperty("rowVersion").GetString()); encounter = deletedJson;
        using var complete = await PostAsync(doctor, app.TicketUrl(ticket.GetProperty("ticketId").GetGuid()) + "/complete", new
        {
            ticketRowVersion = ticket.GetProperty("rowVersion").GetString(), encounterRowVersion = encounter.GetProperty("rowVersion").GetString()
        });
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode); ticket = await JsonAsync(complete);
        encounter = await GetAsync(doctor, url);
        Assert.Equal("Completed", encounter.GetProperty("status").GetString());
        Assert.Equal(encounter.GetProperty("rowVersion").GetString(), ticket.GetProperty("medicalEncounterRowVersion").GetString());
        using (var ordinary = await doctor.PatchAsJsonAsync(url + "/clinical-notes", new { clinicalNotes = "ordinary change", rowVersion = encounter.GetProperty("rowVersion").GetString() }, TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.Conflict, ordinary.StatusCode);
        var discovered = await GetAsync(doctor, $"/api/v1/doctors/me/practices/{app.PracticeId}/encounters?status=Completed");
        Assert.Equal(encounterId, discovered.GetProperty("items")[0].GetProperty("encounterId").GetGuid());
        encounter = await GetAsync(doctor, app.EncounterUrl(discovered.GetProperty("items")[0].GetProperty("encounterId").GetGuid()));
        using var amend = await PostAsync(doctor, url + "/amendments", new { reason = "Reviewed documentation", encounterRowVersion = encounter.GetProperty("rowVersion").GetString(),
            changes = new object[] { new { target = "ClinicalNotes", changeType = "Updated", clinicalNotes = "Corrected private notes" },
                new { target = "Diagnosis", changeType = "Updated", diagnosisId = primaryId, type = "Primary", displayText = "Corrected diagnosis", notes = (string?)null } } });
        Assert.True(amend.StatusCode == HttpStatusCode.Created, await amend.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var mine = await GetAsync(patient, $"/api/v1/encounters/mine/{encounterId}");
        Assert.Equal("Corrected diagnosis", mine.GetProperty("currentActiveDiagnoses")[0].GetProperty("displayText").GetString());
        Assert.False(mine.TryGetProperty("clinicalNotes", out _)); Assert.False(mine.TryGetProperty("amendments", out _));
        Assert.DoesNotContain("Reviewed documentation", mine.GetRawText(), StringComparison.Ordinal);
        var amendments = await GetAsync(doctor, url + "/amendments"); Assert.Equal(1, amendments.GetArrayLength());
        Assert.Contains("Private notes", amendments.GetRawText(), StringComparison.Ordinal);
        var list = await GetAsync(doctor, $"/api/v1/doctors/me/practices/{app.PracticeId}/encounters?search=Patient&status=Completed&pageSize=1");
        Assert.Equal(1, list.GetProperty("totalCount").GetInt64()); Assert.Equal(encounterId, list.GetProperty("items")[0].GetProperty("encounterId").GetGuid());
        await app.WithDbAsync(async db =>
        {
            var amendment = await db.EncounterAmendments.SingleAsync(TestContext.Current.CancellationToken);
            db.Entry(amendment).Property(a => a.Reason).CurrentValue = "overwrite";
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        });
        await app.WithDbAsync(async db =>
        {
            db.Diagnoses.Remove(await db.Diagnoses.SingleAsync(TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Patient_and_reception_follow_up_reservation_check_in_and_consumption_work(bool sqlServer)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sqlServer);
        using var doctor = await app.ClientAsync("doctor"); using var reception = await app.ClientAsync("reception"); using var patient = await app.ClientAsync("patient");
        // Flow A starts with an ordinary patient reservation and full paid admission.
        using var created = await PostAsync(patient, "/api/v1/reservations", new { patientId = app.PatientId, doctorPracticeId = app.PracticeId,
            businessDate = app.Today, slotStartTime = "18:00", segmentId = app.SegmentId, visitTypeId = app.ConsultationTypeId, bookingNote = (string?)null });
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var reservation = await JsonAsync(created); app.Clock.UtcNow = app.Clock.UtcNow.Date.AddHours(14).AddMinutes(40);
        using var admission = await PostAsync(reception, $"/api/v1/practices/{app.PracticeId}/reservations/{reservation.GetProperty("reservationId").GetGuid()}/check-in", new { paidAmount = 300, paymentMethod = "Cash" });
        Assert.Equal(HttpStatusCode.Created, admission.StatusCode);
        var sourceTicket = await app.StartAsync(doctor, await JsonAsync(admission));
        var sourceEncounterId = sourceTicket.GetProperty("medicalEncounterId").GetGuid();
        var sourceEncounter = await GetAsync(doctor, app.EncounterUrl(sourceEncounterId));
        using var sourceDiagnosis = await PostAsync(doctor, app.EncounterUrl(sourceEncounterId) + "/diagnoses", new { type = "Primary", displayText = "Consultation diagnosis", encounterRowVersion = sourceEncounter.GetProperty("rowVersion").GetString() });
        Assert.Equal(HttpStatusCode.Created, sourceDiagnosis.StatusCode);
        var source = await app.CompleteAsync(doctor, sourceTicket);
        var eligibility = await app.EligibilityAsync(doctor, source.Encounter.GetProperty("encounterId").GetGuid(), 1);
        var id = eligibility.GetProperty("eligibilityId").GetGuid();
        var mine = await GetAsync(patient, "/api/v1/follow-up-eligibilities/mine?status=Available"); Assert.Equal(1, mine.GetProperty("totalCount").GetInt64());
        var dates = await GetAsync(patient, $"/api/v1/reservations/follow-up-eligibilities/{id}/available-dates");
        Assert.All(dates.EnumerateArray(), d => Assert.True(DateOnly.Parse(d.GetProperty("date").GetString()!, System.Globalization.CultureInfo.InvariantCulture) <= app.Today.AddDays(1)));
        var date = app.Today.AddDays(1);
        var slots = await GetAsync(patient, $"/api/v1/reservations/follow-up-eligibilities/{id}/available-slots?date={date:yyyy-MM-dd}"); Assert.True(slots.GetArrayLength() > 0);
        var options = await GetAsync(patient, $"/api/v1/reservations/follow-up-eligibilities/{id}/booking-options?date={date:yyyy-MM-dd}&time=18:00");
        Assert.Equal("FollowUp", options.GetProperty("segments")[0].GetProperty("visitTypes")[0].GetProperty("visitTypeCode").GetString());
        Assert.Equal(100m, options.GetProperty("segments")[0].GetProperty("visitTypes")[0].GetProperty("price").GetDecimal());
        var receptionOptions = await GetAsync(reception, $"/api/v1/reception/practices/{app.PracticeId}/booking/options?patientId={app.PatientId}&followUpEligibilityId={id}&date={date:yyyy-MM-dd}&time=18:00");
        Assert.Equal("FollowUp", receptionOptions.GetProperty("segments")[0].GetProperty("visitTypes")[0].GetProperty("visitTypeCode").GetString());
        using var followBooking = await PostAsync(patient, "/api/v1/reservations", new { patientId = app.PatientId, doctorPracticeId = app.PracticeId,
            businessDate = date, slotStartTime = "18:00", segmentId = app.SegmentId, visitTypeId = app.FollowUpTypeId, followUpEligibilityId = id });
        Assert.True(followBooking.StatusCode == HttpStatusCode.Created, await followBooking.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        reservation = await JsonAsync(followBooking); Assert.Equal(id, reservation.GetProperty("followUpEligibilityId").GetGuid());
        Assert.Equal("Reserved", reservation.GetProperty("followUpEligibilityStatus").GetString());
        using (var beyond = await PostAsync(patient, $"/api/v1/reservations/mine/{reservation.GetProperty("reservationId").GetGuid()}/reschedule", new
        { businessDate = date.AddDays(1), slotStartTime = "18:00", rowVersion = reservation.GetProperty("rowVersion").GetString() }))
            Assert.Equal(HttpStatusCode.Conflict, beyond.StatusCode);
        app.Clock.UtcNow = app.Clock.UtcNow.AddDays(1);
        using var checkIn = await PostAsync(reception, $"/api/v1/practices/{app.PracticeId}/reservations/{reservation.GetProperty("reservationId").GetGuid()}/check-in", new { paidAmount = 100, paymentMethod = "Card" });
        Assert.True(checkIn.StatusCode == HttpStatusCode.Created, await checkIn.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var followTicket = await JsonAsync(checkIn);
        var reserved = await GetAsync(patient, $"/api/v1/follow-up-eligibilities/mine/{id}");
        Assert.Equal("Reserved", reserved.GetProperty("status").GetString()); Assert.Equal(JsonValueKind.Null, reserved.GetProperty("reservedReservationId").ValueKind);
        Assert.Equal(followTicket.GetProperty("ticketId").GetGuid(), reserved.GetProperty("reservedTicketId").GetGuid());
        var followComplete = await app.CompleteAsync(doctor, await app.StartAsync(doctor, followTicket));
        var consumed = await GetAsync(patient, $"/api/v1/follow-up-eligibilities/mine/{id}");
        Assert.Equal("Consumed", consumed.GetProperty("status").GetString()); Assert.False(consumed.GetProperty("canBook").GetBoolean());
        Assert.Equal(followComplete.Encounter.GetProperty("encounterId").GetGuid(), consumed.GetProperty("consumedEncounterId").GetGuid());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancellation_releases_eligibility_reception_can_rebook_and_walk_in_consumes(bool sqlServer)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sqlServer);
        using var doctor = await app.ClientAsync("doctor"); using var reception = await app.ClientAsync("reception"); using var patient = await app.ClientAsync("patient");
        var source = await app.CompleteAsync(doctor, await app.StartAsync(doctor, await app.WalkInAsync(reception)));
        var eligibility = await app.EligibilityAsync(doctor, source.Encounter.GetProperty("encounterId").GetGuid()); var id = eligibility.GetProperty("eligibilityId").GetGuid();
        using var booking = await PostAsync(reception, $"/api/v1/reception/practices/{app.PracticeId}/reservations", new { patientId = app.PatientId,
            businessDate = app.Today.AddDays(1), slotStartTime = "18:00", segmentId = app.SegmentId, visitTypeId = app.FollowUpTypeId, followUpEligibilityId = id });
        Assert.True(booking.StatusCode == HttpStatusCode.Created, await booking.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)); var reservation = await JsonAsync(booking);
        using var cancelled = await PostAsync(patient, $"/api/v1/reservations/mine/{reservation.GetProperty("reservationId").GetGuid()}/cancel", new
        { reasonCode = "PatientChangedPlans", rowVersion = reservation.GetProperty("rowVersion").GetString() });
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        var released = await GetAsync(patient, $"/api/v1/follow-up-eligibilities/mine/{id}"); Assert.Equal("Available", released.GetProperty("status").GetString());
        var lookup = await GetAsync(reception, $"/api/v1/reception/practices/{app.PracticeId}/patients/{app.PatientId}/follow-up-eligibilities"); Assert.Equal(1, lookup.GetArrayLength());
        Assert.False(lookup[0].TryGetProperty("clinicalNotes", out _)); Assert.False(lookup[0].TryGetProperty("diagnoses", out _)); Assert.False(lookup[0].TryGetProperty("sourceEncounterId", out _));
        var options = await GetAsync(reception, $"/api/v1/reception/practices/{app.PracticeId}/walk-in/options?patientId={app.PatientId}&followUpEligibilityId={id}");
        Assert.Equal("FollowUp", options.GetProperty("segments")[0].GetProperty("visitTypes")[0].GetProperty("code").GetString());
        var completed = await app.CompleteAsync(doctor, await app.StartAsync(doctor, await app.WalkInAsync(reception, id)));
        var consumed = await GetAsync(patient, $"/api/v1/follow-up-eligibilities/mine/{id}"); Assert.Equal("Consumed", consumed.GetProperty("status").GetString());
        Assert.Equal(completed.Encounter.GetProperty("encounterId").GetGuid(), consumed.GetProperty("consumedEncounterId").GetGuid());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Clinical_scope_and_delegated_operational_permissions_are_enforced(bool sqlServer)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sqlServer);
        using var doctor = await app.ClientAsync("doctor"); using var reception = await app.ClientAsync("reception"); using var patient = await app.ClientAsync("patient");
        using var otherDoctor = await app.ClientAsync("other-doctor"); using var otherPatient = await app.ClientAsync("other-patient"); using var root = await app.ClientAsync("root");
        var completed = await app.CompleteAsync(doctor, await app.StartAsync(doctor, await app.WalkInAsync(reception))); var encounterId = completed.Encounter.GetProperty("encounterId").GetGuid();
        foreach (var denied in new[] { reception, otherDoctor, root })
        {
            using var response = await denied.GetAsync(app.EncounterUrl(encounterId), TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            using var mutation = await denied.PatchAsJsonAsync(app.EncounterUrl(encounterId) + "/clinical-notes", new { clinicalNotes = "unauthorized", rowVersion = completed.Encounter.GetProperty("rowVersion").GetString() }, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.Forbidden, mutation.StatusCode);
            using var amendments = await denied.GetAsync(app.EncounterUrl(encounterId) + "/amendments", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.Forbidden, amendments.StatusCode);
        }
        using (var wrongPractice = await doctor.GetAsync($"/api/v1/doctors/me/practices/{app.OtherPracticeId}/encounters/{encounterId}", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.Forbidden, wrongPractice.StatusCode);
        using (var otherView = await otherPatient.GetAsync($"/api/v1/encounters/mine/{encounterId}", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, otherView.StatusCode);
        await app.EligibilityAsync(doctor, encounterId);
        await app.WithDbAsync(async db =>
        {
            var delegated = await db.ReceptionPracticeAssignmentPermissions.SingleAsync(p => p.AssignmentId == app.ReceptionAssignmentId &&
                p.PermissionId == SystemPermissionIds.For(PermissionNames.FollowUpEligibilityViewBookingEligibility), TestContext.Current.CancellationToken);
            db.Remove(delegated); await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using var missingPermission = await reception.GetAsync($"/api/v1/reception/practices/{app.PracticeId}/patients/{app.PatientId}/follow-up-eligibilities", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, missingPermission.StatusCode);
        using var unassigned = await reception.GetAsync($"/api/v1/reception/practices/{app.OtherPracticeId}/patients/{app.PatientId}/follow-up-eligibilities", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, unassigned.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Ticket_no_show_releases_claim_and_restore_reacquires_without_consuming(bool sqlServer)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sqlServer);
        using var doctor = await app.ClientAsync("doctor"); using var reception = await app.ClientAsync("reception"); using var patient = await app.ClientAsync("patient");
        var source = await app.CompleteAsync(doctor, await app.StartAsync(doctor, await app.WalkInAsync(reception)));
        var eligibility = await app.EligibilityAsync(doctor, source.Encounter.GetProperty("encounterId").GetGuid()); var id = eligibility.GetProperty("eligibilityId").GetGuid();
        var ticket = await app.WalkInAsync(reception, id); var ticketId = ticket.GetProperty("ticketId").GetGuid();
        using (var call = await PostAsync(doctor, $"/api/v1/practices/{app.PracticeId}/queue/call-next", new { })) Assert.Equal(HttpStatusCode.OK, call.StatusCode);
        ticket = await GetAsync(doctor, app.TicketUrl(ticketId));
        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var noResponse = await PostAsync(doctor, app.TicketUrl(ticketId) + "/confirm-no-response", new { rowVersion = ticket.GetProperty("rowVersion").GetString() });
            Assert.True(noResponse.StatusCode == HttpStatusCode.OK, await noResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)); ticket = await JsonAsync(noResponse);
            if (attempt < 2)
            {
                using var recall = await PostAsync(doctor, app.TicketUrl(ticketId) + "/recall", new { rowVersion = ticket.GetProperty("rowVersion").GetString() });
                Assert.Equal(HttpStatusCode.OK, recall.StatusCode); ticket = await JsonAsync(recall);
            }
        }
        Assert.Equal("NoShow", ticket.GetProperty("status").GetString());
        var available = await GetAsync(patient, $"/api/v1/follow-up-eligibilities/mine/{id}"); Assert.Equal("Available", available.GetProperty("status").GetString());
        using var restored = await PostAsync(reception, app.TicketUrl(ticketId) + "/restore-no-show", new { rowVersion = ticket.GetProperty("rowVersion").GetString() });
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        var reserved = await GetAsync(patient, $"/api/v1/follow-up-eligibilities/mine/{id}"); Assert.Equal("Reserved", reserved.GetProperty("status").GetString());
        Assert.Equal(ticketId, reserved.GetProperty("reservedTicketId").GetGuid());
    }
}
