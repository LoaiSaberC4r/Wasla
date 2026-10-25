using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Notifications;
using Wasla.Domain.Common;
using Wasla.Domain.Families;
using Wasla.Domain.Notifications;
using Wasla.Domain.Patients;
using static Wasla.Tests.Integration.Phase13ApiFixture;
using static Wasla.Tests.Integration.Phase17NotificationFixture;

namespace Wasla.Tests.Integration;

public sealed class Phase17RecipientNotificationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task R06_R07_R09_R11_family_membership_and_current_pairwise_authority_are_distinct(bool verified)
    {
        await using var family = await Phase16FamilyFixture.CreateAsync(verifyParents: verified);
        var app = family.App;
        await app.WithDbAsync(async db =>
        {
            var user = await db.PatientAccountLinks.SingleAsync(l => l.PatientId == app.PatientId); await TrustEmailAsync(db, user.ApplicationUserId, app.Clock.UtcNow); await db.SaveChangesAsync();
            // Crossing the clinical age cutoff does not remove the independent under-18 booking authority.
            var child = await db.Patients.SingleAsync(p => p.Id == family.ChildId);
            child.UpdateProfile(child.NameAr, child.NameEn, child.PhoneNumber, null);
        });
        app.Clock.UtcNow = app.Clock.UtcNow.AddDays(7);
        await app.WithDbAsync(async db =>
        {
            var parent = await db.FamilyMembers.SingleAsync(m => m.PatientId == app.PatientId);
            var childMember = await db.FamilyMembers.SingleAsync(m => m.PatientId == family.ChildId);
            var child = await db.Patients.SingleAsync(p => p.Id == family.ChildId);
            var policy = new PatientAccessPolicy(); var today = DateOnly.FromDateTime(app.Clock.UtcNow);
            Assert.True(policy.CanBookReservation(parent, childMember, child, today));
            Assert.False(policy.CanAccessDependentMedicalData(parent, childMember, child, today));
        });
        var id = await ReservationAsync(app, patientId: family.ChildId);
        if (verified)
        {
            await using var diagnosticScope = app.Services.CreateAsyncScope();
            var db = diagnosticScope.ServiceProvider.GetRequiredService<Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.WaslaDbContext>();
            var source = await db.NotificationEvents.SingleAsync(e => e.SourceId == id && e.EventType == "Reservation.Created", TestContext.Current.CancellationToken);
            var recipients = await diagnosticScope.ServiceProvider.GetRequiredService<Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Notifications.NotificationEligibility>().ResolveAsync(source, TestContext.Current.CancellationToken);
            var user = (await db.PatientAccountLinks.SingleAsync(l => l.PatientId == app.PatientId, TestContext.Current.CancellationToken)).ApplicationUserId;
            Assert.Contains(recipients, r => r.UserId == user);
        }
        await DispatchAsync(app);
        await app.WithDbAsync(async db =>
        {
            var e = await db.NotificationEvents.SingleAsync(e => e.SourceId == id && e.EventType == "Reservation.Created");
            var user = (await db.PatientAccountLinks.SingleAsync(l => l.PatientId == app.PatientId)).ApplicationUserId;
            Assert.Equal(verified, await db.NotificationDeliveries.AnyAsync(d => d.NotificationEventId == e.Id && d.RecipientApplicationUserId == user && d.Channel == NotificationChannel.InApp && d.Status == NotificationDeliveryStatus.Queued));
            if (verified)
            {
                var proof = await db.Set<VerifiedParentChildRelationship>().SingleAsync(p => p.ChildPatientId == family.ChildId && p.ParentPatientId == app.PatientId);
                proof.Revoke(app.Clock.UtcNow); await db.SaveChangesAsync();
            }
        });
        app.Clock.UtcNow = app.Clock.UtcNow.AddHours(2); await DispatchAsync(app);
        await app.WithDbAsync(async db =>
        {
            var e = await db.NotificationEvents.SingleAsync(e => e.SourceId == id && e.EventType == "Appointment.ReminderDue");
            var user = (await db.PatientAccountLinks.SingleAsync(l => l.PatientId == app.PatientId)).ApplicationUserId;
            Assert.False(await db.NotificationDeliveries.AnyAsync(d => d.NotificationEventId == e.Id && d.RecipientApplicationUserId == user));
        });
    }
    [Fact]
    public async Task R02_verified_reviewed_global_contact_gets_email_without_an_invented_inbox()
    {
        await using var app = await Phase14MedicationApiTests.CreateAsync(false); var patient = Guid.NewGuid();
        await app.WithDbAsync(async db =>
        {
            var email = "global-verified@example.test"; var admin = await db.ApplicationUsers.SingleAsync(u => u.UserName == "admin");
            db.Patients.Add(Patient.Create(patient, "مريض", "Global", new DateOnly(1990, 1, 1), Gender.Male, null, email, null, null, null, app.Today).Value);
            var proof = new PatientOnboardingChallenge(Guid.NewGuid(), PatientOnboardingPurpose.Claim, email, null, patient, "test-otp", app.Clock.UtcNow, app.Clock.UtcNow.AddMinutes(10), 5);
            proof.Verify("test-token", PatientOnboardingAction.Claim, app.Clock.UtcNow, app.Clock.UtcNow.AddMinutes(5)); proof.Consume(app.Clock.UtcNow, "fingerprint"); db.PatientOnboardingChallenges.Add(proof);
            var claim = new PatientClaimRequest(Guid.NewGuid(), patient, email, patient, "مريض", new DateOnly(1990, 1, 1), Gender.Male, null, proof.Id, "test-key", "test-fingerprint", app.Clock.UtcNow);
            Assert.True(claim.Decide(PatientClaimStatus.ApprovedPendingActivation, admin.Id, "Reviewed identity evidence", patient, app.Clock.UtcNow).IsSuccess); db.PatientClaimRequests.Add(claim);
            await db.SaveChangesAsync();
        });
        var id = await ReservationAsync(app, patientId: patient); app.Clock.UtcNow = app.Clock.UtcNow.AddHours(2); await DispatchAsync(app);
        await using (var diagnosticScope = app.Services.CreateAsyncScope())
        {
            var db = diagnosticScope.ServiceProvider.GetRequiredService<Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.WaslaDbContext>();
            var source = await db.NotificationEvents.SingleAsync(e => e.SourceId == id && e.EventType == "Appointment.ReminderDue", TestContext.Current.CancellationToken);
            var recipients = await diagnosticScope.ServiceProvider.GetRequiredService<Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Notifications.NotificationEligibility>().ResolveAsync(source, TestContext.Current.CancellationToken);
            Assert.Contains(recipients, r => r.TrustedEmail != null);
        }
        await app.WithDbAsync(async db =>
        {
            var e = await db.NotificationEvents.SingleAsync(e => e.SourceId == id && e.EventType == "Appointment.ReminderDue");
            Assert.True(await db.NotificationDeliveries.AnyAsync(d => d.NotificationEventId == e.Id && d.Channel == NotificationChannel.Email && d.Status == NotificationDeliveryStatus.Queued));
            Assert.False(await db.UserNotifications.AnyAsync(n => db.NotificationDeliveries.Any(d => d.Id == n.NotificationDeliveryId && d.NotificationEventId == e.Id)));
            Assert.False(await db.PatientAccountLinks.AnyAsync(l => l.PatientId == patient));
        });
    }
    [Fact]
    public async Task R01_R04_R12_trust_survives_otp_cleanup_but_does_not_follow_email_reassignment_or_account_closure()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(); var id = await ReservationAsync(app, trusted: true);
        await app.WithDbAsync(async db => { Assert.NotEmpty(await db.Set<NotificationVerifiedContact>().ToArrayAsync()); await db.PatientOnboardingChallenges.ExecuteDeleteAsync(); });
        app.Clock.UtcNow = app.Clock.UtcNow.AddHours(2); await DispatchAsync(app);
        await app.WithDbAsync(async db =>
        {
            Assert.True(await db.EmailOutboxMessages.AnyAsync(m => m.NotificationDeliveryId != null));
            var userId = (await db.PatientAccountLinks.SingleAsync(l => l.PatientId == app.PatientId)).ApplicationUserId;
            var user = await db.ApplicationUsers.SingleAsync(u => u.Id == userId); user.Deactivate(); await db.SaveChangesAsync();
        });
        await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.WaslaDbContext>();
        var delivery = await db.NotificationDeliveries.FirstAsync(d => d.Channel == NotificationChannel.Email && d.Status == NotificationDeliveryStatus.Queued, TestContext.Current.CancellationToken);
        var message = await db.EmailOutboxMessages.SingleAsync(m => m.NotificationDeliveryId == delivery.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(await scope.ServiceProvider.GetRequiredService<Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Notifications.NotificationEmailGuard>().DenialAsync(delivery.Id, message.RecipientEmail, TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task R08_revoked_guardian_authority_prevents_dependent_reminder_delivery()
    {
        await using var family = await Phase16FamilyFixture.CreateAsync(adultRole: FamilyMemberRole.LegalGuardian);
        var app = family.App; var id = await ReservationAsync(app, patientId: family.ChildId); await DispatchAsync(app);
        Guid user = default;
        await app.WithDbAsync(async db =>
        {
            user = (await db.PatientAccountLinks.SingleAsync(l => l.PatientId == app.PatientId)).ApplicationUserId;
            var review = await db.Set<FamilyMedicalEligibilityReview>().SingleAsync(r => r.AdultPatientId == app.PatientId && r.ChildPatientId == family.ChildId);
            var admin = await db.ApplicationUsers.SingleAsync(u => u.UserName == "admin");
            Assert.True(review.Decide("revoke", admin.Id, app.Clock.UtcNow, null, null, "Revoked authority evidence").IsSuccess);
            await db.SaveChangesAsync();
        });
        app.Clock.UtcNow = app.Clock.UtcNow.AddHours(2); await DispatchAsync(app);
        await app.WithDbAsync(async db =>
        {
            var e = await db.NotificationEvents.SingleAsync(e => e.SourceId == id && e.EventType == "Appointment.ReminderDue");
            Assert.False(await db.NotificationDeliveries.AnyAsync(d => d.NotificationEventId == e.Id && d.RecipientApplicationUserId == user));
        });
    }
    [Fact]
    public async Task R14_deactivated_reception_assignment_denies_operational_inbox_access()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(); using var reception = await app.ClientAsync("reception");
        await GetAsync(reception, "/api/v1/notifications/mine");
        await app.WithDbAsync(async db => { var assignment = await db.ReceptionPracticeAssignments.SingleAsync(a => a.Id == app.ReceptionAssignmentId); assignment.Deactivate(app.DoctorUserId); await db.SaveChangesAsync(); });
        using var denied = await reception.GetAsync("/api/v1/notifications/mine", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }
}
