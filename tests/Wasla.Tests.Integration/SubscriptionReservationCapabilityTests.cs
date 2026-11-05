using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Reservations;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;
using static Wasla.Tests.Integration.Phase13ApiFixture;

namespace Wasla.Tests.Integration;

public sealed class SubscriptionReservationCapabilityTests
{
    internal static object Booking(Phase13ApiFixture app) => new { patientId = app.PatientId, doctorPracticeId = app.PracticeId,
        businessDate = app.Today, slotStartTime = "18:00", segmentId = app.SegmentId, visitTypeId = app.ConsultationTypeId };
    internal static string DoctorUrl(Phase13ApiFixture app, Guid id) => $"/api/v1/doctors/me/practices/{app.PracticeId}/reservations/{id}";

    [Theory][InlineData(false)][InlineData(true)]
    public async Task Booking_only_returns_effective_readonly_flags_denies_mutations_hides_linked_ticket_and_preserves_patient_actions(bool sql)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sql); await SubscriptionTestData.BookingOnlyAsync(app);
        using var patient = await app.ClientAsync("patient"); using var doctor = await app.ClientAsync("doctor");
        using var created = await PostAsync(patient, "/api/v1/reservations", Booking(app)); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var original = await JsonAsync(created); var id = original.GetProperty("reservationId").GetGuid();
        Assert.True(original.GetProperty("capabilities").GetProperty("canCancel").GetBoolean());
        Assert.True(original.GetProperty("capabilities").GetProperty("canReschedule").GetBoolean());
        var detail = await GetAsync(doctor, DoctorUrl(app, id)); var capabilities = detail.GetProperty("capabilities");
        Assert.False(capabilities.GetProperty("canCancel").GetBoolean()); Assert.False(capabilities.GetProperty("canReschedule").GetBoolean());
        Assert.False(capabilities.GetProperty("canRestoreFromNoShow").GetBoolean());
        Assert.Equal("Subscription.AccessLevelInsufficient", capabilities.GetProperty("cancelBlockedReason").GetString());
        Assert.Equal("Subscription.AccessLevelInsufficient", capabilities.GetProperty("rescheduleBlockedReason").GetString());
        using var cancel = await PostAsync(doctor, DoctorUrl(app, id) + "/cancel", new { reasonCode = "ProviderUnavailable", rowVersion = detail.GetProperty("rowVersion").GetString() });
        Assert.Equal(HttpStatusCode.Forbidden, cancel.StatusCode);
        using var move = await PostAsync(doctor, DoctorUrl(app, id) + "/reschedule", new { businessDate = app.Today.AddDays(1), slotStartTime = "18:00", patientConsentConfirmed = true, reason = "Synthetic", rowVersion = detail.GetProperty("rowVersion").GetString() });
        Assert.Equal(HttpStatusCode.Forbidden, move.StatusCode);
        await app.WithDbAsync(async db =>
        {
            var reservation = await db.Reservations.Include(r => r.History).SingleAsync(r => r.Id == id);
            Assert.Equal(ReservationStatus.Active, reservation.Status); Assert.Single(reservation.History);
            Assert.Equal(original.GetProperty("appointment").GetProperty("startUtc").GetDateTime(), reservation.ScheduledStartUtc);
            // A persisted linked Ticket is deliberately not a grant to Booking Only.
            var at = app.Clock.UtcNow;
            var ticket = Ticket.CreateFromReservation(new(Guid.NewGuid(), app.DoctorId, app.PracticeId, app.PatientId, id, app.Today,
                100, TicketSource.Reservation, app.SegmentId, "Synthetic", "Synthetic", 0, app.ConsultationTypeId,
                "NewConsultation", "Synthetic", "Synthetic", 300, "Africa/Cairo", at, at, CheckInMode.Normal, app.DoctorUserId, null)).Value;
            db.Add(ticket); await db.SaveChangesAsync();
        });
        var linked = await GetAsync(doctor, DoctorUrl(app, id)); Assert.False(linked.GetProperty("canViewLinkedTicket").GetBoolean());
        Assert.Equal(JsonValueKind.Null, linked.GetProperty("linkedTicketId").ValueKind);
        var own = await GetAsync(patient, "/api/v1/reservations/mine/" + id);
        Assert.True(own.GetProperty("canViewLinkedTicket").GetBoolean()); Assert.True(own.GetProperty("capabilities").GetProperty("canCancel").GetBoolean());
        Guid receptionId = default; string receptionVersion = "";
        await app.WithDbAsync(async db => { var staff = await db.Receptions.SingleAsync(r => r.OwnerDoctorId == app.DoctorId); receptionId = staff.Id; receptionVersion = Convert.ToBase64String(staff.RowVersion); });
        using var accountState = await doctor.PutAsJsonAsync($"/api/v1/doctors/me/receptions/{receptionId}/account-state", new { isActive = false, rowVersion = receptionVersion }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, accountState.StatusCode); // Managing an owned identity is independent of operational practice assignments.
    }

    [Theory]
    [InlineData("STARTER", false)][InlineData("PROFESSIONAL", false)][InlineData("PREMIUM", false)]
    [InlineData("STARTER", true)][InlineData("PROFESSIONAL", true)][InlineData("PREMIUM", true)]
    public async Task Full_platform_capabilities_preserve_workflows_and_cancel_replays_remain_lifecycle_correct(string plan, bool sql)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sql); await SubscriptionTestData.ReplaceAsync(app, plan);
        using var patient = await app.ClientAsync("patient"); using var doctor = await app.ClientAsync("doctor");
        using var booked = await PostAsync(patient, "/api/v1/reservations", Booking(app)); Assert.Equal(HttpStatusCode.Created, booked.StatusCode);
        var id = (await JsonAsync(booked)).GetProperty("reservationId").GetGuid();
        var detail = await GetAsync(doctor, DoctorUrl(app, id));
        Assert.True(detail.GetProperty("capabilities").GetProperty("canCancel").GetBoolean()); Assert.True(detail.GetProperty("capabilities").GetProperty("canReschedule").GetBoolean());
        var body = new { reasonCode = "ProviderUnavailable", rowVersion = detail.GetProperty("rowVersion").GetString() }; var key = Guid.NewGuid().ToString("N");
        using var cancelled = await PostAsync(doctor, DoctorUrl(app, id) + "/cancel", body, key); Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        using var replay = await PostAsync(doctor, DoctorUrl(app, id) + "/cancel", body, key); Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        foreach (var response in new[] { await JsonAsync(cancelled), await JsonAsync(replay) })
        {
            Assert.False(response.GetProperty("capabilities").GetProperty("canCancel").GetBoolean());
            Assert.False(response.GetProperty("capabilities").GetProperty("canReschedule").GetBoolean());
            Assert.Equal("Reservation.InvalidState", response.GetProperty("capabilities").GetProperty("cancelBlockedReason").GetString());
        }
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task Role_and_reception_assignment_revocation_are_reflected_without_plan_grants_enlarging_scope(bool sql)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sql);
        using var patient = await app.ClientAsync("patient"); using var doctor = await app.ClientAsync("doctor"); using var reception = await app.ClientAsync("reception");
        var booking = Booking(app); var key = Guid.NewGuid().ToString("N");
        using var booked = await PostAsync(patient, "/api/v1/reservations", booking, key); var id = (await JsonAsync(booked)).GetProperty("reservationId").GetGuid();
        await app.WithDbAsync(async db =>
        {
            await db.RolePermissions.Where(p => p.RoleId == SystemRoleIds.Doctor && p.PermissionId == SystemPermissionIds.For(PermissionNames.DoctorPracticeReservationsCancelOwn)).ExecuteDeleteAsync();
            await db.ReceptionPracticeAssignmentPermissions.Where(p => p.AssignmentId == app.ReceptionAssignmentId && p.PermissionId == SystemPermissionIds.For(PermissionNames.PracticeReservationsCancel)).ExecuteDeleteAsync();
        });
        var own = await GetAsync(doctor, DoctorUrl(app, id)); Assert.False(own.GetProperty("capabilities").GetProperty("canCancel").GetBoolean());
        Assert.True(own.GetProperty("capabilities").GetProperty("canReschedule").GetBoolean());
        var staff = await GetAsync(reception, $"/api/v1/reception/practices/{app.PracticeId}/reservations/{id}");
        Assert.False(staff.GetProperty("capabilities").GetProperty("canCancel").GetBoolean()); Assert.True(staff.GetProperty("capabilities").GetProperty("canReschedule").GetBoolean());
        using var unrelated = await reception.GetAsync($"/api/v1/reception/practices/{app.OtherPracticeId}/reservations/{id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, unrelated.StatusCode);
        await app.WithDbAsync(db => db.RolePermissions.Where(p => p.RoleId == SystemRoleIds.Patient && p.PermissionId == SystemPermissionIds.For(PermissionNames.ReservationsCancelOwn)).ExecuteDeleteAsync());
        using var replay = await PostAsync(patient, "/api/v1/reservations", booking, key); Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.False((await JsonAsync(replay)).GetProperty("capabilities").GetProperty("canCancel").GetBoolean());
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task Expired_subscription_preserves_proven_reservation_continuity_but_not_new_bookings(bool sql)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sql); var subscription = await SubscriptionTestData.ReplaceAsync(app);
        using var patient = await app.ClientAsync("patient"); using var doctor = await app.ClientAsync("doctor");
        using var booked = await PostAsync(patient, "/api/v1/reservations", Booking(app)); var id = (await JsonAsync(booked)).GetProperty("reservationId").GetGuid();
        await app.WithDbAsync(async db => { var row = await db.DoctorSubscriptions.SingleAsync(s => s.Id == subscription); db.Entry(row).Property(s => s.EndsAtUtc).CurrentValue = app.Clock.UtcNow.AddSeconds(1); await db.SaveChangesAsync(); });
        app.Clock.UtcNow = app.Clock.UtcNow.AddSeconds(2); await SubscriptionApiTests.Process(app, subscription);
        var details = await GetAsync(doctor, DoctorUrl(app, id)); Assert.True(details.GetProperty("capabilities").GetProperty("canCancel").GetBoolean());
        using var cancel = await PostAsync(doctor, DoctorUrl(app, id) + "/cancel", new { reasonCode = "ProviderUnavailable", rowVersion = details.GetProperty("rowVersion").GetString() }); Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        using var next = await PostAsync(patient, "/api/v1/reservations", Booking(app)); Assert.Equal(HttpStatusCode.Forbidden, next.StatusCode);
    }
}
