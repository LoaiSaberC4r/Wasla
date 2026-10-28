using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Security;
using static Wasla.Tests.Integration.Phase13ApiFixture;

namespace Wasla.Tests.Integration;

public sealed class DoctorPatientsHistoricalTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Closed_practice_own_reads_require_history_grant_and_existing_writers_remain_denied(bool sql)
    {
        await using var f = await Phase13ApiFixture.CreateAsync(sql);
        using var doctor = await f.ClientAsync("doctor"); using var reception = await f.ClientAsync("reception"); using var patient = await f.ClientAsync("patient");
        var ticket = await f.StartAsync(doctor, await f.WalkInAsync(reception));
        var encounterId = ticket.GetProperty("medicalEncounterId").GetGuid();
        var encounter = await GetAsync(doctor, f.EncounterUrl(encounterId));
        using (var diagnosis = await PostAsync(doctor, f.EncounterUrl(encounterId) + "/diagnoses", new {
            type = "Primary", displayText = "Own diagnosis", encounterRowVersion = encounter.GetProperty("rowVersion").GetString() }))
            Assert.Equal(HttpStatusCode.Created, diagnosis.StatusCode);
        var completed = await f.CompleteAsync(doctor, ticket);
        await f.EligibilityAsync(doctor, encounterId);
        using var booking = await PostAsync(patient, "/api/v1/reservations", new { patientId = f.PatientId, doctorPracticeId = f.PracticeId,
            businessDate = f.Today, slotStartTime = "18:00", segmentId = f.SegmentId, visitTypeId = f.ConsultationTypeId });
        Assert.Equal(HttpStatusCode.Created, booking.StatusCode); var reservation = await JsonAsync(booking);
        await f.WithDbAsync(async db => { (await db.DoctorPractices.SingleAsync(p => p.Id == f.PracticeId)).Deactivate(f.DoctorUserId); await db.SaveChangesAsync(); });
        var root = $"/api/v1/doctors/me/patients/{f.PatientId}";
        var directory = await GetAsync(doctor, "/api/v1/doctors/me/patients");
        Assert.True(directory.GetProperty("items")[0].GetProperty("historicalOnly").GetBoolean());
        foreach (var suffix in new[] { "", "/attendances", "/diagnoses", "/prescriptions", "/appointments", "/follow-ups" }) await GetAsync(doctor, root + suffix);
        encounter = await GetAsync(doctor, f.EncounterUrl(encounterId));
        Assert.All(encounter.GetProperty("capabilities").EnumerateObject(), p => Assert.False(p.Value.GetBoolean()));
        Assert.False(encounter.GetProperty("canChangeVisibility").GetBoolean());
        await GetAsync(doctor, f.EncounterUrl(encounterId) + "/amendments");
        var historicalTicket = await GetAsync(doctor, f.TicketUrl(ticket.GetProperty("ticketId").GetGuid()));
        Assert.All(historicalTicket.GetProperty("operationalCapabilities").EnumerateObject(), p => Assert.False(p.Value.GetBoolean()));
        Assert.False(historicalTicket.GetProperty("canRefund").GetBoolean());
        var reservationId = reservation.GetProperty("reservationId").GetGuid();
        var reservationUrl = $"/api/v1/doctors/me/practices/{f.PracticeId}/reservations/{reservationId}";
        var historicalReservation = await GetAsync(doctor, reservationUrl);
        Assert.False(historicalReservation.GetProperty("capabilities").GetProperty("canCancel").GetBoolean());
        var token = encounter.GetProperty("rowVersion").GetString();
        using (var notes = await doctor.PatchAsJsonAsync(f.EncounterUrl(encounterId) + "/clinical-notes", new { clinicalNotes = "Forbidden", rowVersion = token }, TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.Forbidden, notes.StatusCode);
        foreach (var (url, body) in new (string, object)[] {
            (f.EncounterUrl(encounterId) + "/diagnoses", new { type = "Secondary", displayText = "Forbidden", encounterRowVersion = token }),
            (f.EncounterUrl(encounterId) + "/amendments", new { reason = "Forbidden", encounterRowVersion = token, changes = new[] { new { target = "ClinicalNotes", changeType = "Updated", clinicalNotes = "Forbidden" } } }),
            (f.EncounterUrl(encounterId) + "/follow-up-eligibility", new { validUntil = f.Today.AddDays(5) }),
            (f.TicketUrl(ticket.GetProperty("ticketId").GetGuid()) + "/start", new { rowVersion = completed.Ticket.GetProperty("rowVersion").GetString() }),
            (f.TicketUrl(ticket.GetProperty("ticketId").GetGuid()) + "/complete", new { ticketRowVersion = completed.Ticket.GetProperty("rowVersion").GetString(), encounterRowVersion = token }),
            (reservationUrl + "/cancel", new { rowVersion = reservation.GetProperty("rowVersion").GetString(), reasonCode = "ProviderUnavailable" }),
            (reservationUrl + "/reschedule", new { rowVersion = reservation.GetProperty("rowVersion").GetString(), businessDate = f.Today.AddDays(1), slotStartTime = "18:00", patientConsentConfirmed = true, reason = "Forbidden" }) })
        {
            using var denied = await PostAsync(doctor, url, body);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        await DoctorPatientsApiTests.RemoveGrants(f, PermissionNames.DoctorPatientsViewHistoricalOwn);
        directory = await GetAsync(doctor, "/api/v1/doctors/me/patients"); Assert.Equal(1, directory.GetProperty("totalCount").GetInt32());
        Assert.False(directory.GetProperty("items")[0].GetProperty("capabilities").GetProperty("canViewEncounters").GetBoolean());
        Assert.Equal(JsonValueKind.Null, directory.GetProperty("items")[0].GetProperty("completedEncounterCount").ValueKind);
        Assert.Equal(JsonValueKind.Null, directory.GetProperty("items")[0].GetProperty("hasUpcomingAppointment").ValueKind);
        using var noHistory = await doctor.GetAsync(f.EncounterUrl(encounterId), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, noHistory.StatusCode);
        var unavailable = await GetAsync(doctor, root + "/diagnoses"); Assert.Equal(0, unavailable.GetProperty("totalCount").GetInt32());
    }
}
