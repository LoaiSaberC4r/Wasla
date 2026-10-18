using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wasla.Application.Email;
using static Wasla.Tests.Integration.Phase13ApiFixture;
namespace Wasla.Tests.Integration;
public sealed class Phase5FinalTransactionTests
{
    [Fact, Trait("Category", "SQLServerConcurrency")]
    public async Task Outbox_persistence_failure_rolls_back_full_identity_and_compensates_uploaded_media()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(true); using var authorized = await app.ClientAsync("reception");
        RejectOutbox.Calls = 0;
        await using var failedNode = app.WithWebHostBuilder(b => b.ConfigureServices(s => { s.RemoveAll<IEmailOutbox>(); s.AddScoped<IEmailOutbox, RejectOutbox>(); }));
        using var client = failedNode.CreateClient(); client.DefaultRequestHeaders.Authorization = authorized.DefaultRequestHeaders.Authorization;
        var before = Directory.Exists(app.MedicalMediaRoot) ? Directory.GetFiles(app.MedicalMediaRoot, "*", SearchOption.AllDirectories).Length : 0;
        using var form = new MultipartFormDataContent(); foreach (var (key, value) in Phase5IdentityApi.Profile(app, "rollback-full@example.test", true)) form.Add(new StringContent(value), key);
        var image = new ByteArrayContent(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=")); image.Headers.ContentType = new("image/png"); form.Add(image, "profileImage", "synthetic.png");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/patients/full") { Content = form }; request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(1, RejectOutbox.Calls);
        await app.WithDbAsync(async db => { Assert.False(await db.Patients.AnyAsync(p => p.Email == "rollback-full@example.test")); Assert.False(await db.ApplicationUsers.AnyAsync(u => u.Email == "rollback-full@example.test")); Assert.False(await db.PatientActivationInvitations.AnyAsync()); Assert.False(await db.PatientIdentityAttestations.AnyAsync()); Assert.Equal(2, await db.PatientAccountLinks.CountAsync()); });
        Assert.Equal(before, Directory.Exists(app.MedicalMediaRoot) ? Directory.GetFiles(app.MedicalMediaRoot, "*", SearchOption.AllDirectories).Length : 0);
    }
    private sealed class RejectOutbox : IEmailOutbox
    {
        internal static int Calls;
        public Task QueueAsync(QueueEmailMessage message, CancellationToken cancellationToken = default)
        { Interlocked.Increment(ref Calls); throw new IOException("Synthetic transactional outbox persistence failure"); }
    }
}
