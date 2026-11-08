using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Clinical;
using Wasla.Application.Media;
using Wasla.Domain.Patients;
using static Wasla.Tests.Integration.Phase13ApiFixture;
namespace Wasla.Tests.Integration;

public sealed class HistoricalDocumentFailureTests
{
    [Fact]
    public async Task Rolled_back_metadata_leaves_only_private_staging_and_reconciliation_never_deletes_it()
    {
        var failure = new AttachmentWriteFailure(); await using var f = await CreateAsync(commands: failure); Enable(f);
        using var owner = await f.ClientAsync("patient"); using var response = await Upload(owner, Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode); Assert.True(failure.Fired);
        await f.WithDbAsync(async db => { Assert.Empty(await db.Set<PatientHistoricalDocument>().ToArrayAsync()); Assert.Empty(await db.Set<PatientHistoricalDocumentAttachment>().ToArrayAsync()); });
        var staged = Assert.Single(Directory.GetFiles(Path.Combine(f.MedicalMediaRoot, "HistoricalDocuments"), "*", SearchOption.AllDirectories));
        File.SetLastWriteTimeUtc(staged, f.Clock.UtcNow.AddDays(-2));
        await using var scope = f.Services.CreateAsyncScope(); var report = await scope.ServiceProvider.GetRequiredService<IPrivateMediaReconciliation>().InspectAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, report.StaleUnreferencedCandidates); Assert.Equal(0, report.MissingReferencedFiles); Assert.True(File.Exists(staged));
    }
    [Fact]
    public async Task Lost_commit_acknowledgement_keeps_retained_bytes_and_retry_replays_one_document()
    {
        var failure = new LostCommitAcknowledgement(); await using var f = await CreateAsync(commands: failure); Enable(f);
        using var owner = await f.ClientAsync("patient"); var key = Guid.NewGuid().ToString("N"); using var first = await Upload(owner, key);
        Assert.Equal(HttpStatusCode.InternalServerError, first.StatusCode); Assert.True(failure.Fired);
        Guid id = default; await f.WithDbAsync(async db => { var doc = Assert.Single(await db.Set<PatientHistoricalDocument>().ToArrayAsync()); id = doc.Id; Assert.Single(await db.Set<HistoricalDocumentOperation>().ToArrayAsync()); });
        using var replay = await Upload(owner, key); Assert.True(replay.IsSuccessStatusCode, await replay.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)); Assert.Equal(id, (await JsonAsync(replay)).GetProperty("documentId").GetGuid());
        var detail = await GetAsync(owner, $"/api/v1/patients/me/historical-documents/{id}"); var attachment = Assert.Single(detail.GetProperty("currentVersion").GetProperty("attachments").EnumerateArray()).GetProperty("attachmentId").GetGuid();
        using var content = await owner.GetAsync($"/api/v1/patients/me/historical-documents/{id}/attachments/{attachment}/content", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.OK, content.StatusCode);
        Assert.Single(Directory.GetFiles(Path.Combine(f.MedicalMediaRoot, "HistoricalDocuments"), "*", SearchOption.AllDirectories));
        await using var scope = f.Services.CreateAsyncScope(); var report = await scope.ServiceProvider.GetRequiredService<IPrivateMediaReconciliation>().InspectAsync(TestContext.Current.CancellationToken); Assert.Equal(0, report.StaleUnreferencedCandidates); Assert.Equal(0, report.MissingReferencedFiles); Assert.Equal(1, report.ExaminedReferences);
    }
    [Fact]
    public async Task Missing_retained_file_is_detected_without_changing_document_or_hard_deleting_history()
    {
        await using var f = await CreateAsync(); Enable(f); using var owner = await f.ClientAsync("patient"); using var created = await Upload(owner, Guid.NewGuid().ToString("N")); Assert.True(created.IsSuccessStatusCode);
        var path = Assert.Single(Directory.GetFiles(Path.Combine(f.MedicalMediaRoot, "HistoricalDocuments"), "*", SearchOption.AllDirectories)); File.Delete(path);
        await using var scope = f.Services.CreateAsyncScope(); var report = await scope.ServiceProvider.GetRequiredService<IPrivateMediaReconciliation>().InspectAsync(TestContext.Current.CancellationToken); Assert.Equal(1, report.MissingReferencedFiles);
        await f.WithDbAsync(async db => { Assert.Equal(HistoricalDocumentState.AvailableUnverified, Assert.Single(await db.Set<PatientHistoricalDocument>().ToArrayAsync()).State); Assert.Single(await db.Set<PatientHistoricalDocumentHistory>().ToArrayAsync()); });
    }
    private static void Enable(Phase13ApiFixture f) { var o = f.Services.GetRequiredService<IOptions<HistoricalDocumentOptions>>().Value; o.Enabled = true; o.RequireRealScanner = false; }
    private static async Task<HttpResponseMessage> Upload(HttpClient client, string key)
    {
        using var form = new MultipartFormDataContent(); form.Add(new StringContent("OtherMedicalDocument"), "category"); form.Add(new StringContent("true"), "dateUnknown");
        var file = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.4\nretained private source\n%%EOF")); file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf"); form.Add(file, "files", "source.pdf");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/patients/me/historical-documents") { Content = form }; request.Headers.Add("Idempotency-Key", key); return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
    private sealed class AttachmentWriteFailure : DbCommandInterceptor
    {
        public bool Fired { get; private set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (!Fired && command.CommandText.Contains("INSERT INTO \"PatientHistoricalDocumentAttachments\"", StringComparison.Ordinal)) { Fired = true; throw new DbUpdateException("Synthetic attachment metadata failure"); }
            return ValueTask.FromResult(result);
        }
    }
    private sealed class LostCommitAcknowledgement : DbTransactionInterceptor
    {
        public bool Fired { get; private set; }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            if (!Fired && eventData.Context?.ChangeTracker.Entries<HistoricalDocumentOperation>().Any() == true) { Fired = true; throw new DbUpdateException("Synthetic lost commit acknowledgement"); }
            return Task.CompletedTask;
        }
    }
}
