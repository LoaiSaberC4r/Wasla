using System.Text.Json;
using Wasla.Domain.Media;
using Wasla.Application.Features.Tickets.Common;
using System.Diagnostics.Metrics;
using BuildingBlock.Application.Time;
using BuildingBlock.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wasla.Application.Media;
using Wasla.Domain.Families;
using Wasla.Domain.Patients;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Media;

public sealed class PrivateMediaReconciliationOptions
{
    public bool Enabled { get; set; }
    public int IntervalHours { get; set; } = 24;
    public int StaleStagingHours { get; set; } = 24;
    public int MaxFilesPerModule { get; set; } = 10000;
    public bool IsValid => IntervalHours is >= 1 and <= 168 && StaleStagingHours is >= 1 and <= 720 && MaxFilesPerModule is >= 1 and <= 100000;
}

internal sealed class PrivateMediaReconciliation(WaslaDbContext db, IOptions<MediaStorageOptions> storage,
    IOptions<PrivateMediaReconciliationOptions> options, IDateTimeProvider clock, ITicketQueueLock locks) : IPrivateMediaReconciliation
{
    private static readonly string[] Modules = ["HistoricalDocuments", "FamilyGovernance", "ChildOnboarding", "FamilyRelationshipRequests", "FamilyMedicalRequests", "PatientProfilePhotos"];
    private IQueryable<string> Keys(string module) => module switch {
        "HistoricalDocuments" => db.Set<PatientHistoricalDocumentAttachment>().IgnoreQueryFilters().AsNoTracking().Select(a => a.MediaKey),
        "FamilyGovernance" => db.Set<FamilyGovernanceEvidence>().IgnoreQueryFilters().AsNoTracking().Select(a => a.MediaKey),
        "ChildOnboarding" => db.Set<ChildOnboardingEvidence>().IgnoreQueryFilters().AsNoTracking().Select(a => a.MediaKey),
        "FamilyRelationshipRequests" => db.FamilyRelationshipDocuments.IgnoreQueryFilters().AsNoTracking().Select(a => a.MediaKey),
        "FamilyMedicalRequests" => db.Set<FamilyMedicalAccessRequestDocument>().IgnoreQueryFilters().AsNoTracking().Select(a => a.MediaKey),
        _ => db.Patients.IgnoreQueryFilters().AsNoTracking().Where(p => p.ProfileImageMediaKey != null).Select(p => p.ProfileImageMediaKey!) };
    public async Task<PrivateMediaReconciliationReport> InspectAsync(CancellationToken ct)
    {
        if (!options.Value.IsValid) throw new InvalidOperationException("Invalid media reconciliation limits.");
        var configured = storage.Value;
        var root = Path.GetFullPath(Path.IsPathRooted(configured.RootPath) ? configured.RootPath :
            Path.Combine(configured.ContentRootPath ?? AppContext.BaseDirectory, configured.RootPath));
        var max = options.Value.MaxFilesPerModule; var cutoff = clock.UtcNow.AddHours(-options.Value.StaleStagingHours);
        var reports = new List<PrivateMediaModuleReport>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await locks.AcquireIdempotencyAsync(Guid.Empty, "PrivateMedia:Reconciliation", "Checkpoint", ct);
        foreach (var module in Modules)
        {
            ct.ThrowIfCancellationRequested();
            var state = await db.Set<PrivateMediaReconciliationCheckpoint>().SingleOrDefaultAsync(r => r.Module == module, ct);
            if (state is null) { state = new(module); db.Add(state); }
            var prefix = module == "PatientProfilePhotos" ? "Patients" : module;
            var folder = Path.Combine(root, prefix);
            var files = (Directory.Exists(folder) ? Enumerate(folder) : []).Select(p => Path.GetRelativePath(root, p).Replace('\\', '/'))
                .Where(k => module != "PatientProfilePhotos" || k.Contains("/Profile/", StringComparison.Ordinal))
                .Where(k => state.LastFileKey == null || string.CompareOrdinal(k, state.LastFileKey) > 0)
                .Order(StringComparer.Ordinal).Take(max + 1).ToArray();
            var moreFiles = files.Length > max; files = files.Take(max).ToArray();
            var orphanKeys = new List<string>(); var missingKeys = new List<string>();
            var old = files.Where(k => TryResolve(root, prefix, k, out var path) && File.GetLastWriteTimeUtc(path) < cutoff).ToArray();
            foreach (var chunk in old.Chunk(500))
            {
                var retained = await Keys(module).Where(key => chunk.Contains(key)).ToArrayAsync(ct);
                orphanKeys.AddRange(chunk.Except(retained, StringComparer.Ordinal));
            }
            var references = Keys(module).Distinct();
            // CompareTo translates to the database collation, matching OrderBy and the durable cursor.
            if (state.LastReferenceKey is { } cursor) references = references.Where(k => k.CompareTo(cursor) > 0);
            var keys = await references.OrderBy(k => k).Take(max + 1).ToArrayAsync(ct);
            var moreReferences = keys.Length > max; keys = keys.Take(max).ToArray();
            foreach (var key in keys)
            {
                ct.ThrowIfCancellationRequested();
                if (!TryResolve(root, prefix, key, out var path) || !File.Exists(path)) missingKeys.Add(key);
            }
            // Hold the completed side at its last key until the entire module's
            // cycle finishes. Independent resets would repeatedly scan page one.
            var continuing = moreFiles || moreReferences;
            state.Advance(continuing ? files.LastOrDefault() ?? state.LastFileKey : null,
                continuing ? keys.LastOrDefault() ?? state.LastReferenceKey : null, clock.UtcNow);
            var run = new PrivateMediaReconciliationRun(module, clock.UtcNow, files.Length, keys.Length, orphanKeys.Count, missingKeys.Count,
                moreFiles || moreReferences, JsonSerializer.Serialize(new { OrphanCandidates = orphanKeys, MissingReferences = missingKeys }));
            db.Add(run); reports.Add(new(module, run.Id, files.Length, keys.Length, orphanKeys.Count, missingKeys.Count, run.HasMore));
        }
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return new(reports.Sum(r => r.ExaminedFiles), reports.Sum(r => r.StaleUnreferencedCandidates), reports.Sum(r => r.ExaminedReferences),
            reports.Sum(r => r.MissingReferencedFiles), reports.Any(r => r.HasMore), reports);
    }
    private static IEnumerable<string> Enumerate(string folder)
    {
        if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0) throw new IOException("Private storage module cannot be a link.");
        return Directory.EnumerateFiles(folder, "*", new EnumerationOptions { RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint, MaxRecursionDepth = 6, IgnoreInaccessible = false });
    }
    private static bool TryResolve(string root, string module, string key, out string path)
    {
        path = string.Empty; var normalized = key.Replace('\\', '/');
        if (!normalized.StartsWith(module + "/", StringComparison.Ordinal) || normalized.Contains(':', StringComparison.Ordinal) ||
            normalized.Split('/').Any(p => string.IsNullOrWhiteSpace(p) || p is "." or "..")) return false;
        path = Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = Path.Combine(root, module) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) return false;
        // Do not follow a substituted storage directory or attachment link, including on missing-file checks.
        for (var current = path; current.Length >= prefix.Length; current = Path.GetDirectoryName(current) ?? string.Empty)
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return false;
        return !Directory.Exists(Path.Combine(root, module)) || (File.GetAttributes(Path.Combine(root, module)) & FileAttributes.ReparsePoint) == 0;
    }
}

internal sealed class PrivateMediaReconciliationWorker(IServiceScopeFactory scopes, IOptions<PrivateMediaReconciliationOptions> options,
    ILogger<PrivateMediaReconciliationWorker> logger) : BackgroundService
{
    private static readonly Meter Meter = new("Wasla.PrivateMedia", "1.0");
    private static readonly Counter<long> Runs = Meter.CreateCounter<long>("wasla.private_media.reconciliation.runs");
    private static readonly Counter<long> Failures = Meter.CreateCounter<long>("wasla.private_media.reconciliation.failures");
    private static readonly Histogram<long> Orphans = Meter.CreateHistogram<long>("wasla.private_media.stale_unreferenced_candidates");
    private static readonly Histogram<long> Missing = Meter.CreateHistogram<long>("wasla.private_media.missing_references");
    private static readonly Action<ILogger, Exception?> Failed = LoggerMessage.Define(LogLevel.Error, new EventId(4830, "PrivateMediaReconciliationFailed"), "Private media reconciliation failed. Investigate private storage and database health.");
    private static readonly Action<ILogger, long, long, bool, Exception?> Completed = LoggerMessage.Define<long, long, bool>(LogLevel.Information,
        new EventId(4831, "PrivateMediaReconciliationCompleted"), "Private media reconciliation completed. Stale candidates: {StaleCandidates}; missing references: {MissingReferences}; bounded scan incomplete: {HasMore}.");
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            var hasMore = false;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var report = await scope.ServiceProvider.GetRequiredService<IPrivateMediaReconciliation>().InspectAsync(stoppingToken);
                Runs.Add(1); hasMore = report.HasMore;
                foreach (var module in report.Modules ?? [])
                {
                    var tag = new KeyValuePair<string, object?>("module", module.Module);
                    Orphans.Record(module.StaleUnreferencedCandidates, tag); Missing.Record(module.MissingReferencedFiles, tag);
                }
                Completed(logger, report.StaleUnreferencedCandidates, report.MissingReferencedFiles, report.HasMore, null);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Data.Common.DbException or InvalidOperationException)
            { Failures.Add(1); Failed(logger, null); }
            await Task.Delay(hasMore ? TimeSpan.FromSeconds(1) : TimeSpan.FromHours(options.Value.IntervalHours), stoppingToken);
        }
    }
}
