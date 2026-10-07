using BuildingBlock.Application.Abstraction.Media;
using BuildingBlock.Domain.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Wasla.Application.Features.Diagnostics;

public interface IDiagnosticMediaReferenceReader
{
    Task<IReadOnlySet<string>> RetainedKeysAsync(IReadOnlyList<string> keys, CancellationToken ct);
}

internal sealed class DiagnosticMediaCompensation(IMediaService media, IDiagnosticMediaReferenceReader references, ILogger<DiagnosticMediaCompensation> logger)
{
    private readonly List<string> _keys = [];
    private static readonly Action<ILogger, Exception?> CleanupFailed = LoggerMessage.Define(LogLevel.Error,
        new EventId(1515, "DiagnosticMediaCleanupFailed"), "Diagnostic attachment compensation could not complete; retained-file reconciliation is required.");
    public void Track(string key) => _keys.Add(key);
    public void Clear() => _keys.Clear();
    public async Task CleanAsync()
    {
        if (_keys.Count == 0) return;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            // A commit failure can have an uncertain outcome. Never remove a file retained by a persisted version/submission.
            var retained = await references.RetainedKeysAsync(_keys, timeout.Token);
            await media.DeleteRangeAsync(_keys.Where(k => !retained.Contains(k)).ToArray(), timeout.Token);
            _keys.Clear();
        }
        catch (Exception) { CleanupFailed(logger, null); }
    }
}

internal sealed class DiagnosticMediaCompensationBehavior(DiagnosticMediaCompensation compensation)
    : IPipelineBehavior<DiagnosticResultCommand, Result<DiagnosticResultMutationResponse>>
{
    public async Task<Result<DiagnosticResultMutationResponse>> Handle(DiagnosticResultCommand request,
        RequestHandlerDelegate<Result<DiagnosticResultMutationResponse>> next, CancellationToken ct)
    {
        var committed = false;
        try
        {
            var response = await next(ct); committed = response.IsSuccess; return response;
        }
        finally
        {
            if (committed) compensation.Clear(); else await compensation.CleanAsync();
        }
    }
}
