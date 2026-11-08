using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Clinical;
using Wasla.Domain.Families;
using Wasla.Domain.Patients;
using static Wasla.Tests.Integration.Phase13ApiFixture;
namespace Wasla.Tests.Integration;

public sealed class DependentBirthdayBoundaryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cairo_medical_sixteenth_birthday_stops_files_at_midnight_but_operational_access_continues_until_eighteen(bool sql)
    {
        await using var f = await Phase16FamilyFixture.CreateAsync(sql);
        var options = f.App.Services.GetRequiredService<IOptions<HistoricalDocumentOptions>>().Value; options.Enabled = true; options.RequireRealScanner = false;
        DateOnly born = default; await f.App.WithDbAsync(async db => born = (await db.Patients.SingleAsync(p => p.Id == f.ChildId)).DateOfBirth);
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        var birthday16 = TimeZoneInfo.ConvertTimeToUtc(born.AddYears(16).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), zone);
        f.App.Clock.UtcNow = birthday16.AddSeconds(-1);
        var path = $"/api/v1/families/mine/dependents/{f.ChildId}/historical-documents";
        using var form = new MultipartFormDataContent(); form.Add(new StringContent("HistoricalLabReport"), "category"); form.Add(new StringContent("true"), "dateUnknown");
        var file = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.4\nprivate historic source\n%%EOF")); file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf"); form.Add(file, "files", "source.pdf");
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = form }; request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var created = await f.Father.SendAsync(request, TestContext.Current.CancellationToken); Assert.True(created.IsSuccessStatusCode, await created.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var id = (await JsonAsync(created)).GetProperty("documentId").GetGuid(); var doc = await GetAsync(f.Father, path + $"/{id}");
        var attachment = Assert.Single(doc.GetProperty("currentVersion").GetProperty("attachments").EnumerateArray()).GetProperty("attachmentId").GetGuid();
        using var before = await f.Father.GetAsync(path + $"/{id}/attachments/{attachment}/content", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        f.App.Clock.UtcNow = birthday16;
        using var atBoundary = await f.Father.GetAsync(path + $"/{id}/attachments/{attachment}/content", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.NotFound, atBoundary.StatusCode);
        using var listing = await f.Father.GetAsync(path, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.NotFound, listing.StatusCode);
        var bookable = await GetAsync(f.Father, "/api/v1/reservations/bookable-patients"); Assert.Contains(bookable.EnumerateArray(), p => p.GetProperty("patientId").GetGuid() == f.ChildId);
        var workspace = await GetAsync(f.Father, "/api/v1/families/mine/workspace"); var child = Assert.Single(workspace.GetProperty("dependents").EnumerateArray(), p => p.GetProperty("patientId").GetGuid() == f.ChildId);
        Assert.True(child.GetProperty("canBook").GetBoolean()); Assert.False(child.GetProperty("canUploadHistoricalDocument").GetBoolean()); Assert.False(child.GetProperty("canViewMedicalRecord").GetBoolean());
        f.App.Clock.UtcNow = TimeZoneInfo.ConvertTimeToUtc(born.AddYears(18).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), zone);
        using var adultProfile = await f.Father.GetAsync($"/api/v1/families/mine/dependents/{f.ChildId}/profile", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.NotFound, adultProfile.StatusCode);
        bookable = await GetAsync(f.Father, "/api/v1/reservations/bookable-patients"); Assert.DoesNotContain(bookable.EnumerateArray(), p => p.GetProperty("patientId").GetGuid() == f.ChildId);
        await f.App.WithDbAsync(async db => { Assert.Single(await db.Set<PatientHistoricalDocument>().Where(d => d.Id == id).ToArrayAsync()); Assert.Equal(2, await db.Set<ParentChildOperationalAuthority>().CountAsync(p => p.ChildPatientId == f.ChildId && p.IsActive)); Assert.True(await db.FamilyMembers.AnyAsync(m => m.PatientId == f.ChildId && m.IsActive)); });
    }
}
