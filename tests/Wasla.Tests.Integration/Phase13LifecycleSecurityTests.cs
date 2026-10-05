using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using BuildingBlock.Api.Logging;
using Wasla.Domain.Common;
using Wasla.Domain.Families;
using Wasla.Domain.Patients;
using Wasla.Domain.Reservations;
using static Wasla.Tests.Integration.Phase13ApiFixture;

namespace Wasla.Tests.Integration;

public sealed class Phase13LifecycleSecurityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Runtime_reservation_no_show_releases_and_reception_restore_reacquires(bool sqlServer)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sqlServer);
        using var doctor = await app.ClientAsync("doctor"); using var reception = await app.ClientAsync("reception"); using var patient = await app.ClientAsync("patient");
        var source = await app.CompleteAsync(doctor, await app.StartAsync(doctor, await app.WalkInAsync(reception)));
        var eligibility = await app.EligibilityAsync(doctor, source.Encounter.GetProperty("encounterId").GetGuid()); var id = eligibility.GetProperty("eligibilityId").GetGuid();
        using var booked = await PostAsync(patient, "/api/v1/reservations", new { patientId = app.PatientId, doctorPracticeId = app.PracticeId,
            businessDate = app.Today, slotStartTime = "18:00", segmentId = app.SegmentId, visitTypeId = app.FollowUpTypeId, followUpEligibilityId = id });
        Assert.True(booked.StatusCode == HttpStatusCode.Created, await booked.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)); var reservation = await JsonAsync(booked);
        var reservationId = reservation.GetProperty("reservationId").GetGuid();
        // Test data establishes three other patients; all lifecycle transitions use the public APIs.
        var others = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToArray();
        await app.WithDbAsync(async db =>
        {
            foreach (var other in others) db.Patients.Add(Patient.Create(other, "مريض", "Another patient", new DateOnly(1992, 1, 1), Gender.Male,
                null, null, null, null, null, app.Today).Value);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        app.Clock.UtcNow = app.Clock.UtcNow.Date.AddHours(15).AddMinutes(20); // 18:20 Cairo, after the 15-minute grace period.
        foreach (var other in others)
        {
            await app.CompleteAsync(doctor, await app.StartAsync(doctor, await app.WalkInAsync(reception, patientId: other)));
            app.Clock.UtcNow = app.Clock.UtcNow.AddMinutes(1);
        }
        var url = $"/api/v1/reception/practices/{app.PracticeId}/reservations/{reservationId}";
        reservation = await GetAsync(reception, url); Assert.Equal("NoShow", reservation.GetProperty("status").GetString());
        var available = await GetAsync(patient, $"/api/v1/follow-up-eligibilities/mine/{id}"); Assert.Equal("Available", available.GetProperty("status").GetString());
        using var restore = await PostAsync(reception, url + "/restore-no-show", new { rowVersion = reservation.GetProperty("rowVersion").GetString() });
        Assert.True(restore.StatusCode == HttpStatusCode.OK, await restore.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var reserved = await GetAsync(patient, $"/api/v1/follow-up-eligibilities/mine/{id}");
        Assert.Equal("Reserved", reserved.GetProperty("status").GetString()); Assert.Equal(reservationId, reserved.GetProperty("reservedReservationId").GetGuid());
        using var stale = await PostAsync(reception, url + "/restore-no-show", new { rowVersion = reservation.GetProperty("rowVersion").GetString() });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        await app.WithDbAsync(async db =>
        {
            Assert.Equal(ReservationStatus.Active, (await db.Reservations.SingleAsync(r => r.Id == reservationId, TestContext.Current.CancellationToken)).Status);
            Assert.Null((await db.FollowUpEligibilities.SingleAsync(e => e.Id == id, TestContext.Current.CancellationToken)).ConsumedEncounterId);
        });
    }

    [Fact]
    public async Task Family_booking_does_not_grant_family_clinical_read_access()
    {
        await using var app = await Phase13ApiFixture.CreateAsync();
        using var doctor = await app.ClientAsync("doctor"); using var reception = await app.ClientAsync("reception"); using var parent = await app.ClientAsync("patient");
        var childId = Guid.NewGuid();
        await app.WithDbAsync(async db =>
        {
            db.Patients.Add(Patient.Create(childId, "طفل", "Child", new DateOnly(2020, 1, 1), Gender.Male, null, null, null, null, null, app.Today).Value);
            var family = Family.Create(Guid.NewGuid(), app.DoctorUserId).Value;
            var request = FamilyRelationshipRequest.Create(Guid.NewGuid(), FamilyRelationshipRequestType.CreateFamily, null,
                app.PatientId, childId, FamilyMemberRole.Father, FamilyMemberRole.Child, app.DoctorUserId, app.Clock.UtcNow).Value;
            db.FamilyRelationshipRequests.Add(request); db.Families.Add(family);
            family.AddMember(Guid.NewGuid(), app.PatientId, FamilyMemberRole.Father, app.Clock.UtcNow, app.DoctorUserId, request.Id);
            family.AddMember(Guid.NewGuid(), childId, FamilyMemberRole.Child, app.Clock.UtcNow, app.DoctorUserId, request.Id);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        var source = await app.CompleteAsync(doctor, await app.StartAsync(doctor, await app.WalkInAsync(reception, patientId: childId)));
        var encounterId = source.Encounter.GetProperty("encounterId").GetGuid(); var eligibility = await app.EligibilityAsync(doctor, encounterId);
        var id = eligibility.GetProperty("eligibilityId").GetGuid();
        using var denied = await parent.GetAsync($"/api/v1/encounters/mine/{encounterId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        var myList = await GetAsync(parent, "/api/v1/encounters/mine"); Assert.Equal(0, myList.GetProperty("totalCount").GetInt64());
        var eligible = await GetAsync(parent, $"/api/v1/follow-up-eligibilities/mine?patientId={childId}"); Assert.Equal(1, eligible.GetProperty("totalCount").GetInt64());
        using var booking = await PostAsync(parent, "/api/v1/reservations", new { patientId = childId, doctorPracticeId = app.PracticeId,
            businessDate = app.Today.AddDays(1), slotStartTime = "18:00", segmentId = app.SegmentId, visitTypeId = app.FollowUpTypeId, followUpEligibilityId = id });
        Assert.True(booking.StatusCode == HttpStatusCode.Created, await booking.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Expiry_is_checked_at_request_time_and_clinical_payload_fields_are_redacted()
    {
        await using var app = await Phase13ApiFixture.CreateAsync();
        using var doctor = await app.ClientAsync("doctor"); using var reception = await app.ClientAsync("reception"); using var patient = await app.ClientAsync("patient");
        var source = await app.CompleteAsync(doctor, await app.StartAsync(doctor, await app.WalkInAsync(reception)));
        var eligibility = await app.EligibilityAsync(doctor, source.Encounter.GetProperty("encounterId").GetGuid(), 0); var id = eligibility.GetProperty("eligibilityId").GetGuid();
        app.Clock.UtcNow = app.Clock.UtcNow.AddDays(1);
        var list = await GetAsync(patient, "/api/v1/follow-up-eligibilities/mine?status=Expired"); Assert.Equal(1, list.GetProperty("totalCount").GetInt64());
        Assert.False(list.GetProperty("items")[0].GetProperty("canBook").GetBoolean());
        var messages = new List<string>();
        foreach (var language in new[] { "en", "ar" })
        {
            patient.DefaultRequestHeaders.AcceptLanguage.Clear(); patient.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);
            using var expired = await patient.GetAsync($"/api/v1/reservations/follow-up-eligibilities/{id}/available-dates", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Conflict, expired.StatusCode); var json = await JsonAsync(expired);
            Assert.Contains("FollowUpEligibility.Expired", json.GetRawText(), StringComparison.Ordinal); messages.Add(json.GetRawText());
        }
        Assert.NotEqual(messages[0], messages[1]);
        using var scope = app.Services.CreateScope(); var policy = scope.ServiceProvider.GetRequiredService<ILogRedactionPolicy>();
        foreach (var field in new[] { "ClinicalNotes", "DisplayText", "Notes", "BeforeSnapshot", "AfterSnapshot", "Reason", "Changes", "Search" })
            Assert.Equal("***", policy.Redact(field, "Private clinical payload").Value);
    }
}
