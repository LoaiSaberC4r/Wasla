using BuildingBlock.Application.Abstraction.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wasla.Application.Features.Families;
using Wasla.Domain.Families;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class FamilyMedicalMediaCompensation(IMediaService media, IServiceScopeFactory scopes, ILogger<FamilyMedicalMediaCompensation> logger) : IFamilyMedicalMediaCompensation
{
    private readonly List<string> _keys = [];
    private static readonly Action<ILogger, Exception?> CleanupFailed = LoggerMessage.Define(LogLevel.Error, new(1616, "FamilyMedicalCleanupFailed"),
        "Family medical evidence compensation requires retained-file reconciliation.");
    public void Track(string key) => _keys.Add(key);
    public void Clear() => _keys.Clear();
    public async Task CleanAsync()
    {
        if (_keys.Count == 0) return;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await using var scope = scopes.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<WaslaDbContext>();
            var retained = await db.Set<FamilyMedicalAccessRequestDocument>().AsNoTracking().Where(d => _keys.Contains(d.MediaKey)).Select(d => d.MediaKey).ToArrayAsync(timeout.Token);
            await media.DeleteRangeAsync(_keys.Except(retained, StringComparer.Ordinal).ToArray(), timeout.Token); _keys.Clear();
        }
        catch (Exception) { CleanupFailed(logger, null); }
    }
}
