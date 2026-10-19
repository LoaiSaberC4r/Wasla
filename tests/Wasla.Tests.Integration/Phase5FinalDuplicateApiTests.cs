using System.Net;
using Microsoft.EntityFrameworkCore;
using static Wasla.Tests.Integration.Phase13ApiFixture;
namespace Wasla.Tests.Integration;
public sealed class Phase5FinalDuplicateApiTests
{
    [Theory, InlineData(false), InlineData(true)]
    public async Task Soft_matches_require_attested_distinct_person_and_classification_never_moves_records(bool sql)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sql); using var staff = await app.ClientAsync("reception"); using var admin = await app.ClientAsync("root");
        using var first = await Phase5IdentityApi.FormAsync(staff, "/api/v1/patients", Phase5IdentityApi.Profile(app, "distinct-one@example.test")); Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var fields = Phase5IdentityApi.Profile(app, "distinct-two@example.test");
        using var unreviewed = await Phase5IdentityApi.FormAsync(staff, "/api/v1/patients", fields); Assert.Equal(HttpStatusCode.Conflict, unreviewed.StatusCode);
        fields["matchReviewOutcome"] = "DistinctPerson"; fields["matchReviewReason"] = "Synthetic separate identities verified"; fields["matchingAttestationBasis"] = "GovernmentIdSeen";
        using var second = await Phase5IdentityApi.FormAsync(staff, "/api/v1/patients", fields); Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var inbox = await GetAsync(admin, "/api/v1/admin/patient-duplicates"); var summary = Assert.Single(inbox.GetProperty("items").EnumerateArray()); var id = summary.GetProperty("caseId").GetGuid();
        var detail = await GetAsync(admin, $"/api/v1/admin/patient-duplicates/{id}"); Assert.False(detail.TryGetProperty("clinical", out _));
        var request = new { resolution = "ConfirmedDuplicate", reason = "Synthetic manual classification only", rowVersion = summary.GetProperty("rowVersion").GetString() };
        var responses = sql ? await Task.WhenAll(PostAsync(admin, $"/api/v1/admin/patient-duplicates/{id}/resolve", request), PostAsync(admin, $"/api/v1/admin/patient-duplicates/{id}/resolve", request)) : [await PostAsync(admin, $"/api/v1/admin/patient-duplicates/{id}/resolve", request)];
        try { Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK); if (sql) Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict); }
        finally { foreach (var response in responses) response.Dispose(); }
        await app.WithDbAsync(async db => { Assert.Equal(4, await db.Patients.CountAsync()); Assert.Equal(2, await db.PatientAccountLinks.CountAsync()); Assert.False(await db.Reservations.AnyAsync()); Assert.False(await db.Tickets.AnyAsync()); });
    }
}
