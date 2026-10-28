using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Security;

namespace Wasla.Tests.Integration;

public sealed class DoctorPatientsApiTests
{
    private const string Root = "/api/v1/doctors/me/patients";
    [Fact]
    public async Task Membership_begins_at_real_admission_and_all_seven_patient_reads_are_scoped()
    {
        await using var f = await Phase13ApiFixture.CreateAsync();
        using var doctor = await f.ClientAsync("doctor"); using var reception = await f.ClientAsync("reception");
        var empty = await Phase13ApiFixture.GetAsync(doctor, Root);
        Assert.Equal(0, empty.GetProperty("totalCount").GetInt32());
        using var unrelated = await doctor.GetAsync($"{Root}/{f.PatientId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, unrelated.StatusCode);
        var ticket = await f.WalkInAsync(reception);
        var directory = await Phase13ApiFixture.GetAsync(doctor, Root);
        Assert.Equal(1, directory.GetProperty("totalCount").GetInt32());
        var item = directory.GetProperty("items")[0];
        Assert.Equal(f.PatientId, item.GetProperty("patientId").GetGuid());
        Assert.Equal(1, item.GetProperty("attendanceCount").GetInt32());
        Assert.EndsWith("Z", item.GetProperty("lastAttendanceAtUtc").GetString());
        Assert.Equal(0, item.GetProperty("completedEncounterCount").GetInt32());
        foreach (var suffix in new[] { "", "/attendances", "/diagnoses", "/prescriptions", "/appointments", "/follow-ups" })
        {
            using var response = await doctor.GetAsync($"{Root}/{f.PatientId}{suffix}", TestContext.Current.CancellationToken);
            Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.True(response.Headers.CacheControl?.NoStore); Assert.Contains("nosniff", response.Headers.GetValues("X-Content-Type-Options"));
            using var denied = await doctor.GetAsync($"{Root}/{f.OtherPatientId}{suffix}", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        }
        var attendance = await Phase13ApiFixture.GetAsync(doctor, $"{Root}/{f.PatientId}/attendances");
        Assert.Equal(ticket.GetProperty("ticketId").GetGuid(), attendance.GetProperty("items")[0].GetProperty("ticketId").GetGuid());
        Assert.Equal(JsonValueKind.Null, attendance.GetProperty("items")[0].GetProperty("encounterId").ValueKind);
    }
    [Fact]
    public async Task Current_database_permission_revocation_hides_counts_and_denies_patient_reads()
    {
        await using var f = await Phase13ApiFixture.CreateAsync();
        using var doctor = await f.ClientAsync("doctor"); using var reception = await f.ClientAsync("reception");
        await f.WalkInAsync(reception);
        await RemoveGrants(f, PermissionNames.MedicalEncountersViewOwn, PermissionNames.DoctorPracticeReservationsViewOwn);
        var directory = await Phase13ApiFixture.GetAsync(doctor, Root);
        var item = directory.GetProperty("items")[0];
        Assert.Equal(JsonValueKind.Null, item.GetProperty("completedEncounterCount").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("hasUpcomingAppointment").ValueKind);
        using var filter = await doctor.GetAsync(Root + "?hasUpcomingAppointment=true", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, filter.StatusCode);
        await RemoveGrants(f, PermissionNames.DoctorPatientsViewOwn);
        using var revoked = await doctor.GetAsync(Root, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
    }
    [Theory]
    [InlineData("patient")]
    [InlineData("reception")]
    [InlineData("other-doctor")]
    public async Task Other_actors_cannot_enumerate_patient_relationships(string actor)
    {
        await using var f = await Phase13ApiFixture.CreateAsync();
        using var reception = await f.ClientAsync("reception"); await f.WalkInAsync(reception);
        using var client = await f.ClientAsync(actor);
        using var response = await client.GetAsync($"{Root}/{f.PatientId}", TestContext.Current.CancellationToken);
        Assert.Equal(actor == "other-doctor" ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden, response.StatusCode);
    }
    [Theory]
    [InlineData("?pageSize=101")]
    [InlineData("?pageNumber=0")]
    [InlineData("?lastAttendanceFrom=2026-10-20&lastAttendanceTo=2026-10-01")]
    [InlineData("?practiceScope=unknown")]
    [InlineData("?asOfUtc=0001-01-02T00:00:00Z")]
    public async Task Invalid_directory_filters_use_validation_problem_details(string query)
    {
        await using var f = await Phase13ApiFixture.CreateAsync(); using var doctor = await f.ClientAsync("doctor");
        using var response = await doctor.GetAsync(Root + query, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
    [Fact]
    public async Task Existing_ticket_detail_keeps_clinical_and_financial_source_permissions_separate()
    {
        await using var f = await Phase13ApiFixture.CreateAsync(); using var doctor = await f.ClientAsync("doctor"); using var reception = await f.ClientAsync("reception");
        var ticket = await f.StartAsync(doctor, await f.WalkInAsync(reception)); var id = ticket.GetProperty("ticketId").GetGuid();
        await RemoveGrants(f, PermissionNames.MedicalEncountersViewOwn, PermissionNames.DoctorPracticePaymentsViewOwn, PermissionNames.DoctorPracticePaymentsRefundOwn);
        var details = await Phase13ApiFixture.GetAsync(doctor, f.TicketUrl(id));
        Assert.Equal(JsonValueKind.Null, details.GetProperty("medicalEncounterId").ValueKind);
        Assert.Equal(JsonValueKind.Null, details.GetProperty("medicalEncounterRowVersion").ValueKind);
        Assert.Equal(JsonValueKind.Null, details.GetProperty("paymentId").ValueKind);
        Assert.Equal(0, details.GetProperty("refundableAmount").GetDecimal());
    }
    internal static Task RemoveGrants(Phase13ApiFixture f, params string[] names) => f.WithDbAsync(async db =>
    {
        var ids = names.Select(SystemPermissionIds.For).ToArray();
        db.RolePermissions.RemoveRange(await db.RolePermissions.Where(r => r.RoleId == SystemRoleIds.Doctor && ids.Contains(r.PermissionId)).ToArrayAsync(TestContext.Current.CancellationToken));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    });
}
