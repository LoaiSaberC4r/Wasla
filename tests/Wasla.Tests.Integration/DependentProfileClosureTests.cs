using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Wasla.Application.Media;
using Wasla.Domain.Families;
using static Wasla.Tests.Integration.Phase13ApiFixture;
using static Wasla.Tests.Integration.FamilyClosureRegressionTests;

namespace Wasla.Tests.Integration;

public sealed class DependentProfileClosureTests
{
    [Fact, Trait("Category", "SQLServerConcurrency")]
    public async Task Photo_failure_preserves_previous_image_retry_is_idempotent_and_phone_contact_audit_is_retained()
    {
        var failure = new AuditFailure(); await using var f = await CreateAsync(true, failure); Enable(f);
        using var parent = await f.ClientAsync("patient"); using var admin = await f.ClientAsync("root"); var child = await Child(f, parent, admin, "طفل الملف");
        var profile = await GetAsync(parent, $"/api/v1/families/mine/dependents/{child}/profile");
        using var first = await Photo(parent, child, profile.GetProperty("rowVersion").GetString()!, Guid.NewGuid().ToString("N")); Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        string? previous = null; await f.WithDbAsync(async db => previous = (await db.Patients.SingleAsync(p => p.Id == child)).ProfileImageMediaKey);
        profile = await GetAsync(parent, $"/api/v1/families/mine/dependents/{child}/profile"); var version = profile.GetProperty("rowVersion").GetString()!; var key = Guid.NewGuid().ToString("N"); failure.Armed = true;
        using var broken = await Photo(parent, child, version, key); Assert.Equal(HttpStatusCode.InternalServerError, broken.StatusCode); Assert.True(failure.Fired);
        await f.WithDbAsync(async db => Assert.Equal(previous, (await db.Patients.SingleAsync(p => p.Id == child)).ProfileImageMediaKey)); Assert.True(File.Exists(Path.Combine(f.MedicalMediaRoot, previous!.Replace('/', Path.DirectorySeparatorChar))));
        using var retry = await Photo(parent, child, version, key); Assert.Equal(HttpStatusCode.OK, retry.StatusCode); var receipt = await JsonAsync(retry);
        using var replay = await Photo(parent, child, version, key); Assert.Equal(receipt.GetProperty("changeId").GetGuid(), (await JsonAsync(replay)).GetProperty("changeId").GetGuid());
        using var image = await parent.GetAsync($"/api/v1/families/mine/dependents/{child}/profile-image", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        profile = await GetAsync(parent, $"/api/v1/families/mine/dependents/{child}/profile");
        using var phoneRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/families/mine/dependents/{child}/phone") { Content = JsonContent.Create(new { rowVersion = profile.GetProperty("rowVersion").GetString(), phoneNumber = "01012345678" }) }; phoneRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var phone = await parent.SendAsync(phoneRequest, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.OK, phone.StatusCode);
        await f.WithDbAsync(async db => { var audit = await db.Set<DependentProfileChange>().Where(r => r.ChildPatientId == child).OrderBy(r => r.OccurredAtUtc).ToArrayAsync(); Assert.Equal(3, audit.Length); Assert.All(audit, a => { Assert.NotNull(a.BeforeSnapshot); Assert.NotNull(a.AfterSnapshot); }); Assert.Equal("01012345678", (await db.Patients.SingleAsync(p => p.Id == child)).PhoneNumber); });
        var files = Directory.GetFiles(Path.Combine(f.MedicalMediaRoot, "Patients"), "*", SearchOption.AllDirectories); Assert.Equal(3, files.Length); foreach (var file in files) File.SetLastWriteTimeUtc(file, f.Clock.UtcNow.AddDays(-2));
        await using var scope = f.Services.CreateAsyncScope(); var report = await scope.ServiceProvider.GetRequiredService<IPrivateMediaReconciliation>().InspectAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, report.Modules!.Single(m => m.Module == "PatientProfilePhotos").StaleUnreferencedCandidates); Assert.Equal(0, report.MissingReferencedFiles);
    }
    private static async Task<HttpResponseMessage> Photo(HttpClient client, Guid child, string version, string key)
    {
        using var form = new MultipartFormDataContent(); form.Add(new StringContent(version), "rowVersion");
        var file = new ByteArrayContent(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/l9sAAAAASUVORK5CYII=")); file.Headers.ContentType = new MediaTypeHeaderValue("image/png"); form.Add(file, "photo", "synthetic.png");
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/families/mine/dependents/{child}/profile") { Content = form }; request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
    private sealed class AuditFailure : DbCommandInterceptor
    {
        internal bool Armed, Fired;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (Armed && !Fired && command.CommandText.Contains("INSERT INTO [DependentProfileChanges]", StringComparison.Ordinal)) { Fired = true; throw new DbUpdateException("Synthetic profile audit failure"); }
            return ValueTask.FromResult(result);
        }
    }
}
