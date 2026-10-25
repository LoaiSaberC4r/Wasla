using System.Net;
using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Common;
using Wasla.Domain.Doctors;
using Wasla.Domain.Security;
using static Wasla.Tests.Integration.Phase13ApiFixture;
using static Wasla.Tests.Integration.Phase17NotificationFixture;

namespace Wasla.Tests.Integration;

public sealed class Phase17GovernanceNotificationTests
{
    [Fact]
    public async Task E06_governance_keeps_existing_email_and_adds_only_eligible_own_inbox()
    {
        await using var app = await Phase14MedicationApiTests.CreateAsync(false); using var admin = await app.ClientAsync("admin");
        Guid doctorId = default; string version = "";
        await app.WithDbAsync(async db =>
        {
            var original = await db.ApplicationUsers.SingleAsync(u => u.Id == app.DoctorUserId);
            var user = ApplicationUser.Create(Guid.NewGuid(), "notification-doctor", "notification-doctor@example.test", null, original.PasswordHash, UserType.Doctor, false, app.Clock.UtcNow).Value;
            var doctor = Doctor.Create(Guid.NewGuid(), user.Id, "طبيب", "Doctor", new DateOnly(1985, 1, 1), Gender.Male, null, "front", "back", "card", null, app.Today).Value;
            db.ApplicationUsers.Add(user); db.UserRoles.Add(new(Guid.NewGuid(), user.Id, SystemRoleIds.Doctor)); db.Doctors.Add(doctor); await db.SaveChangesAsync();
            doctorId = doctor.Id; version = Convert.ToBase64String(doctor.RowVersion);
        });
        using (var approved = await PostAsync(admin, $"/api/v1/admin/doctors/{doctorId}/approve", new { nationalId = "28501011234567", rowVersion = version })) Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        await DispatchAsync(app); await DispatchAsync(app);
        await app.WithDbAsync(async db =>
        {
            Assert.Single(await db.EmailOutboxMessages.Where(m => m.IdempotencyKey.StartsWith("doctor-approved:")).ToArrayAsync());
            var e = await db.NotificationEvents.SingleAsync(e => e.SourceId == doctorId && e.EventType == "Doctor.Approved");
            Assert.Single(await db.UserNotifications.Where(n => db.NotificationDeliveries.Any(d => d.Id == n.NotificationDeliveryId && d.NotificationEventId == e.Id)).ToArrayAsync());
        });
        var reservation = await ReservationAsync(app);
        await app.WithDbAsync(async db =>
        {
            var patient = await db.Patients.SingleAsync(p => p.Id == app.PatientId); patient.UpdateProfile(patient.NameAr, patient.NameEn, patient.PhoneNumber, "patient@example.test");
            version = Convert.ToBase64String((await db.Doctors.SingleAsync(d => d.Id == app.DoctorId)).RowVersion); await db.SaveChangesAsync();
        });
        using (var suspended = await PostAsync(admin, $"/api/v1/admin/doctors/{app.DoctorId}/suspend", new { reason = "PRIVATE_GOVERNANCE_REASON", rowVersion = version })) Assert.Equal(HttpStatusCode.OK, suspended.StatusCode);
        await DispatchAsync(app); await DispatchAsync(app);
        await app.WithDbAsync(async db =>
        {
            Assert.Single(await db.EmailOutboxMessages.Where(m => m.IdempotencyKey.StartsWith("doctor-suspended:")).ToArrayAsync());
            var cancellation = Assert.Single(await db.EmailOutboxMessages.Where(m => m.IdempotencyKey.StartsWith("reservation-doctor-suspended:")).ToArrayAsync());
            Assert.DoesNotContain("PRIVATE_GOVERNANCE_REASON", cancellation.HtmlBody, StringComparison.Ordinal);
            Assert.Single(await db.NotificationEvents.Where(e => e.SourceId == reservation && e.EventType == "Reservation.Cancelled").ToArrayAsync());
            Assert.Single(await db.NotificationEvents.Where(e => e.SourceId == app.DoctorId && e.EventType == "Doctor.Suspended").ToArrayAsync());
        });
    }
}
