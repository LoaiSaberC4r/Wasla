using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Patients;
using Wasla.Domain.Security;
using Wasla.Domain.Practices;
using Wasla.Domain.Common;
using Microsoft.Extensions.DependencyInjection;
using BuildingBlock.Application.Abstraction.Encryption;
using static Wasla.Tests.Integration.Phase13ApiFixture;
namespace Wasla.Tests.Integration;
public sealed class Phase5FinalIdentitySecurityTests
{
    [Fact, Trait("Category", "SQLServerConcurrency")]
    public async Task Reception_at_second_doctor_discovers_and_books_original_basic_patient_without_clinical_access()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(true);
        var hash = await app.Services.GetRequiredService<IPasswordService>().HashAsync("Phase13TestPass123!", TestContext.Current.CancellationToken);
        await app.WithDbAsync(async db =>
        {
            var doctor = await db.Doctors.SingleAsync(d => d.Id != app.DoctorId);
            var user = ApplicationUser.Create(Guid.NewGuid(), "reception-a", "reception-a@example.test", null, hash, UserType.Reception, false, app.Clock.UtcNow).Value;
            var reception = Reception.Create(Guid.NewGuid(), user.Id, doctor.Id, "استقبال تجريبي", null, doctor.ApplicationUserId).Value;
            var assignment = ReceptionPracticeAssignment.Create(Guid.NewGuid(), reception.Id, app.OtherPracticeId, doctor.ApplicationUserId).Value;
            db.ApplicationUsers.Add(user); db.UserRoles.Add(new(Guid.NewGuid(), user.Id, SystemRoleIds.Reception)); db.Receptions.Add(reception); db.ReceptionPracticeAssignments.Add(assignment);
            db.ReceptionPracticeAssignmentPermissions.AddRange(PermissionNames.ReceptionAssignmentScoped.Select(p => new ReceptionPracticeAssignmentPermission(Guid.NewGuid(), assignment.Id, SystemPermissionIds.For(p), doctor.ApplicationUserId, app.Clock.UtcNow)));
            await db.SaveChangesAsync();
        });
        using var receptionA = await app.ClientAsync("reception-a"); using var receptionB = await app.ClientAsync("reception");
        var fields = Phase5IdentityApi.Profile(app, "visitor@example.test"); fields["doctorPracticeId"] = app.OtherPracticeId.ToString();
        using var created = await Phase5IdentityApi.FormAsync(receptionA, "/api/v1/patients", fields); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var patient = (await JsonAsync(created)).GetProperty("patientId").GetGuid();
        using var search = await PostAsync(receptionB, "/api/v1/patients/registration-matches", new { mode = "Search", doctorPracticeId = app.PracticeId, email = " VISITOR@Example.Test " }); Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        var candidate = Assert.Single((await JsonAsync(search)).GetProperty("candidates").EnumerateArray()); Assert.Equal(patient, candidate.GetProperty("patientId").GetGuid()); Assert.False(candidate.TryGetProperty("email", out _));
        using var confirmation = await PostAsync(receptionB, "/api/v1/patients/registration-matches", new { mode = "ConfirmReuse", doctorPracticeId = app.PracticeId, patientId = patient, verifiedInPerson = true, verificationBasis = "GovernmentIdSeen" }); Assert.Equal(HttpStatusCode.OK, confirmation.StatusCode);
        var attestation = (await JsonAsync(confirmation)).GetProperty("attestationId").GetGuid();
        using var booking = await PostAsync(receptionB, $"/api/v1/reception/practices/{app.PracticeId}/reservations", new { patientId = patient, businessDate = app.Today.AddDays(1), slotStartTime = "18:00", segmentId = app.SegmentId, visitTypeId = app.ConsultationTypeId, patientIdentityAttestationId = attestation }); Assert.True(booking.StatusCode == HttpStatusCode.Created, await booking.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        await app.WithDbAsync(async db => { Assert.Equal(patient, (await db.Reservations.SingleAsync()).PatientId); Assert.Equal(3, await db.Patients.CountAsync()); Assert.False(await db.PatientAccountLinks.AnyAsync(l => l.PatientId == patient)); Assert.NotNull((await db.PatientIdentityAttestations.SingleAsync(a => a.Id == attestation)).RelatedReservationId); });
        using var clinical = await receptionB.GetAsync("/api/v1/patients/me/medical-record/overview", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.NotFound, clinical.StatusCode);
    }
    [Fact] public async Task Neutral_start_attempt_accounting_bounded_search_and_minors_are_enforced_by_real_middleware()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(); using var client = app.CreateClient(); using var staff = await app.ClientAsync("reception");
        using var unauthenticatedChange = await client.PostAsJsonAsync("/api/v1/patient-onboarding/start", new { email = "change@example.test", purpose = "EmailChange" }, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.Unauthorized, unauthenticatedChange.StatusCode);
        using var wrongRoleChange = await staff.PostAsJsonAsync("/api/v1/patient-onboarding/start", new { email = "change@example.test", purpose = "EmailChange" }, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.Forbidden, wrongRoleChange.StatusCode);
        foreach (var email in new[] { "patient@example.test", "unregistered@example.test", "doctor@example.test" })
        {
            using var r = await client.PostAsJsonAsync("/api/v1/patient-onboarding/start", new { email, purpose = "Register" }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Accepted, r.StatusCode); var body = await JsonAsync(r); Assert.Equal(2, body.EnumerateObject().Count()); Assert.False(body.TryGetProperty("patientId", out _));
        }
        Guid challenge = default; await app.WithDbAsync(async db => challenge = (await db.PatientOnboardingChallenges.SingleAsync(c => c.NormalizedEmail == "unregistered@example.test")).Id);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var wrong = await client.PostAsJsonAsync("/api/v1/patient-onboarding/verify-email", new { requestId = challenge, otp = "invalid" }, TestContext.Current.CancellationToken);
            Assert.Equal(attempt == 4 ? HttpStatusCode.TooManyRequests : HttpStatusCode.BadRequest, wrong.StatusCode);
        }
        await app.WithDbAsync(async db => { var c = await db.PatientOnboardingChallenges.SingleAsync(c => c.Id == challenge); Assert.Equal(5, c.AttemptCount); Assert.NotNull(c.InvalidatedOnUtc); });
        using var directory = await staff.GetAsync($"/api/v1/patients/search?doctorPracticeId={app.PracticeId}", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.BadRequest, directory.StatusCode);
        using var protectedAdmin = await staff.GetAsync("/api/v1/admin/patient-claims", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.Forbidden, protectedAdmin.StatusCode);
        var child = Phase5IdentityApi.Profile(app, "minor@example.test", true); child["dateOfBirth"] = "2012-01-01";
        using var minor = await Phase5IdentityApi.FormAsync(staff, "/api/v1/patients/full", child); Assert.Equal(HttpStatusCode.UnprocessableEntity, minor.StatusCode);
    }
    [Fact] public async Task Foreign_patient_reuse_requires_current_bound_attestation_in_existing_walk_in()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(); using var staff = await app.ClientAsync("reception");
        var url = $"/api/v1/practices/{app.PracticeId}/tickets/walk-in";
        using var denied = await PostAsync(staff, url, new { patientId = app.OtherPatientId, segmentId = app.SegmentId, visitTypeId = app.ConsultationTypeId, paidAmount = 300, paymentMethod = "Cash" }); Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
        using var confirm = await PostAsync(staff, "/api/v1/patients/registration-matches", new { mode = "ConfirmReuse", doctorPracticeId = app.PracticeId, patientId = app.OtherPatientId, verifiedInPerson = true, verificationBasis = "GovernmentIdSeen" }); Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        var attestation = (await JsonAsync(confirm)).GetProperty("attestationId").GetGuid();
        using var created = await PostAsync(staff, url, new { patientId = app.OtherPatientId, segmentId = app.SegmentId, visitTypeId = app.ConsultationTypeId, paidAmount = 300, paymentMethod = "Cash", patientIdentityAttestationId = attestation }); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        await app.WithDbAsync(async db => { var a = await db.PatientIdentityAttestations.SingleAsync(a => a.Id == attestation); Assert.NotNull(a.ConsumedOnUtc); Assert.NotNull(a.RelatedTicketId); Assert.Equal(2, await db.Patients.CountAsync()); });
    }
}
