using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wasla.Application.Media;
using Wasla.Domain.Media;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Media;
using static Wasla.Tests.Integration.Phase13ApiFixture;

namespace Wasla.Tests.Integration;

public sealed class PrivateMediaContinuationTests
{
    [Fact, Trait("Category", "SQLServerConcurrency")]
    public async Task Six_modules_resume_after_restart_and_report_each_orphan_without_deletion()
    {
        await using var f = await CreateAsync(true);
        f.Services.GetRequiredService<IOptions<PrivateMediaReconciliationOptions>>().Value.MaxFilesPerModule = 2;
        var modules = new[] { "HistoricalDocuments", "FamilyGovernance", "ChildOnboarding", "FamilyRelationshipRequests", "FamilyMedicalRequests", "Patients" };
        var paths = new List<string>();
        foreach (var module in modules)
        {
            var folder = Path.Combine(f.MedicalMediaRoot, module, "synthetic", module == "Patients" ? "Profile" : "evidence"); Directory.CreateDirectory(folder);
            for (var i = 0; i < 3; i++) { var file = Path.Combine(folder, i + ".pdf"); await File.WriteAllTextAsync(file, "synthetic orphan candidate", TestContext.Current.CancellationToken); File.SetLastWriteTimeUtc(file, f.Clock.UtcNow.AddDays(-2)); paths.Add(file); }
        }
        PrivateMediaReconciliationReport first;
        await using (var scope = f.Services.CreateAsyncScope()) first = await scope.ServiceProvider.GetRequiredService<IPrivateMediaReconciliation>().InspectAsync(TestContext.Current.CancellationToken);
        Assert.True(first.HasMore); Assert.Equal(12, first.ExaminedFiles); Assert.Equal(12, first.StaleUnreferencedCandidates); Assert.Equal(6, first.Modules!.Count);
        await f.WithDbAsync(async db => Assert.Equal(6, await db.Set<PrivateMediaReconciliationCheckpoint>().CountAsync(r => r.LastFileKey != null)));
        // A fresh service/context resumes durable checkpoints, with no in-memory cursor.
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var second = await scope.ServiceProvider.GetRequiredService<IPrivateMediaReconciliation>().InspectAsync(TestContext.Current.CancellationToken);
            Assert.False(second.HasMore); Assert.Equal(6, second.ExaminedFiles); Assert.Equal(6, second.StaleUnreferencedCandidates);
        }
        await f.WithDbAsync(async db => {
            Assert.Equal(6, await db.Set<PrivateMediaReconciliationCheckpoint>().CountAsync(r => r.LastFileKey == null && r.LastReferenceKey == null));
            var reports = await db.Set<PrivateMediaReconciliationRun>().ToArrayAsync(); Assert.Equal(12, reports.Length);
            Assert.Equal(18, reports.Sum(r => r.OrphanCandidates)); Assert.Equal(6, reports.Select(r => r.Module).Distinct().Count());
        });
        Assert.All(paths, p => Assert.True(File.Exists(p)));
    }
}
