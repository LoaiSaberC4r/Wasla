using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Buffers.Binary;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Clinical;
using Wasla.Domain.Patients;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Media;
using static Wasla.Tests.Integration.Phase13ApiFixture;

namespace Wasla.Tests.Integration;

[Trait("Category", "SQLServerConcurrency")]
public sealed class HistoricalValidationLifecycleTests
{
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task Rejected_source_has_durable_private_receipt_and_no_published_bytes_or_official_results(bool malware)
    {
        await using var f = await CreateAsync(true, realScanner: true);
        f.Services.GetRequiredService<IOptions<HistoricalDocumentOptions>>().Value.Enabled = true;
        var scanner = f.Services.GetRequiredService<IOptions<ClamAvMalwareScannerOptions>>().Value; scanner.Enabled = true;
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        scanner.Port = malware ? ((IPEndPoint)listener.LocalEndpoint).Port : 1;
        // Deterministic ClamD verdict fixture exercises the durable upload state.
        // Real EICAR detection is independently tested against the actual daemon.
        var verdict = malware ? Task.Run(async () => {
            using var client = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken); await using var stream = client.GetStream();
            var command = new byte[10]; await stream.ReadExactlyAsync(command, TestContext.Current.CancellationToken); Assert.Equal("zINSTREAM\0", Encoding.ASCII.GetString(command));
            var header = new byte[4];
            while (true) { await stream.ReadExactlyAsync(header, TestContext.Current.CancellationToken); var count = BinaryPrimitives.ReadInt32BigEndian(header); if (count == 0) break; var bytes = new byte[count]; await stream.ReadExactlyAsync(bytes, TestContext.Current.CancellationToken); }
            await stream.WriteAsync("stream: Synthetic-Test-Signature FOUND\0"u8.ToArray(), TestContext.Current.CancellationToken);
        }, TestContext.Current.CancellationToken) : Task.CompletedTask;
        using var owner = await f.ClientAsync("patient"); var key = Guid.NewGuid().ToString("N");
        using var result = await Upload(owner, key, malware);
        Assert.Equal(HttpStatusCode.Accepted, result.StatusCode); var receipt = await JsonAsync(result); var doc = receipt.GetProperty("documentId").GetGuid();
        var expected = malware ? "RejectedUnsafe" : "Quarantined";
        Assert.Equal(expected, receipt.GetProperty("validationState").GetString());
        using var replay = await Upload(owner, key, malware); Assert.Equal(doc, (await JsonAsync(replay)).GetProperty("documentId").GetGuid());
        var detail = await GetAsync(owner, $"/api/v1/patients/me/historical-documents/{doc}");
        Assert.Equal(expected, detail.GetProperty("state").GetString()); Assert.Equal(System.Text.Json.JsonValueKind.Null, detail.GetProperty("currentVersion").ValueKind);
        Assert.False(detail.GetProperty("canRevise").GetBoolean()); Assert.False(detail.GetProperty("canWithdraw").GetBoolean());
        await f.WithDbAsync(async db => {
            Assert.Single(await db.Set<PatientHistoricalDocument>().ToArrayAsync()); Assert.Empty(await db.Set<PatientHistoricalDocumentAttachment>().ToArrayAsync());
            Assert.Empty(await db.Set<PatientHistoricalDocumentVersion>().ToArrayAsync()); Assert.Single(await db.Set<PatientHistoricalDocumentHistory>().ToArrayAsync());
            Assert.Single(await db.Set<HistoricalDocumentOperation>().ToArrayAsync()); Assert.Empty(await db.LabResultVersions.ToArrayAsync()); Assert.Empty(await db.RadiologyResultVersions.ToArrayAsync());
        });
        Assert.False(Directory.Exists(Path.Combine(f.MedicalMediaRoot, "HistoricalDocuments")));
        await verdict.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    private static async Task<HttpResponseMessage> Upload(HttpClient client, string key, bool malware)
    {
        using var form = new MultipartFormDataContent(); form.Add(new StringContent("OtherMedicalDocument"), "category"); form.Add(new StringContent("true"), "dateUnknown");
        var test = malware ? "synthetic ClamD rejected-verdict source" : "synthetic clean PDF source";
        var file = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.4\n" + test + "\n%%EOF")); file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf"); form.Add(file, "files", "synthetic.pdf");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/patients/me/historical-documents") { Content = form }; request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
