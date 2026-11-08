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
    IOptions<PrivateMediaReconciliationOptions> options, IDateTimeProvider clock) : IPrivateMediaReconciliation
{
    private static readonly string[] Modules = ["HistoricalDocuments", "FamilyGovernance", "ChildOnboarding"];
    private IQueryable<string> Keys(string module) => module switch {
        "HistoricalDocuments" => db.Set<PatientHistoricalDocumentAttachment>().AsNoTracking().OrderBy(a => a.Id).Select(a => a.MediaKey),
        "FamilyGovernance" => db.Set<FamilyGovernanceEvidence>().AsNoTracking().OrderBy(a => a.Id).Select(a => a.MediaKey),
        _ => db.Set<ChildOnboardingEvidence>().AsNoTracking().OrderBy(a => a.Id).Select(a => a.MediaKey) };
    public async Task<PrivateMediaReconciliationReport> InspectAsync(CancellationToken ct)
    {
        if (!options.Value.IsValid) throw new InvalidOperationException("Invalid media reconciliation limits.");
        var configured = storage.Value;
        var root = Path.GetFullPath(Path.IsPathRooted(configured.RootPath) ? configured.RootPath :
            Path.Combine(configured.ContentRootPath ?? AppContext.BaseDirectory, configured.RootPath));
        var max = options.Value.MaxFilesPerModule; var cutoff = clock.UtcNow.AddHours(-options.Value.StaleStagingHours);
        long examined = 0, orphans = 0, references = 0, missing = 0; var hasMore = false;
        foreach (var module in Modules)
        {
            ct.ThrowIfCancellationRequested(); var folder = Path.Combine(root, module);
            var files = Directory.Exists(folder) ? Enumerate(folder).Take(max + 1).ToArray() : [];
            hasMore |= files.Length > max; files = files.Take(max).ToArray(); examined += files.Length;
            var old = files.Where(p => File.GetLastWriteTimeUtc(p) < cutoff).Select(p => Path.GetRelativePath(root, p).Replace('\\', '/')).ToArray();
            foreach (var chunk in old.Chunk(500))
            {
                var retained = await Keys(module).Where(key => chunk.Contains(key)).ToArrayAsync(ct);
                orphans += chunk.Except(retained, StringComparer.Ordinal).LongCount();
            }
            var keys = await Keys(module).Take(max + 1).ToArrayAsync(ct); hasMore |= keys.Length > max;
            foreach (var key in keys.Take(max))
            {
                ct.ThrowIfCancellationRequested(); references++;
                if (!TryResolve(root, module, key, out var path) || !File.Exists(path)) missing++;
            }
        }
        return new(examined, orphans, references, missing, hasMore);
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
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var report = await scope.ServiceProvider.GetRequiredService<IPrivateMediaReconciliation>().InspectAsync(stoppingToken);
                Runs.Add(1); Orphans.Record(report.StaleUnreferencedCandidates); Missing.Record(report.MissingReferencedFiles);
                Completed(logger, report.StaleUnreferencedCandidates, report.MissingReferencedFiles, report.HasMore, null);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Data.Common.DbException or InvalidOperationException)
            { Failures.Add(1); Failed(logger, null); }
            await Task.Delay(TimeSpan.FromHours(options.Value.IntervalHours), stoppingToken);
        }
    }
}
