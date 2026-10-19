using BuildingBlock.Application.Abstraction.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wasla.Application.Features.PatientOnboarding;
namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;
internal sealed class PatientIdentityMediaCompensation(IMediaService media, IServiceScopeFactory scopes, ILogger<PatientIdentityMediaCompensation> logger) : IPatientIdentityMediaCompensation
{
    private readonly List<string> _keys = [];
    private static readonly Action<ILogger, Exception?> Failed = LoggerMessage.Define(LogLevel.Error, new(5001, "PatientIdentityMediaCleanup"), "Patient identity media compensation needs reconciliation.");
    public void Track(string key) => _keys.Add(key);
    public void Clear() => _keys.Clear();
    public async Task CleanAsync()
    {
        if (_keys.Count == 0) return;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await using var scope = scopes.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<WaslaDbContext>();
            var retained = await db.PatientClaimEvidences.AsNoTracking().Where(e => e.PrivateMediaKey != null && _keys.Contains(e.PrivateMediaKey)).Select(e => e.PrivateMediaKey!).ToListAsync(timeout.Token);
            retained.AddRange(await db.Patients.AsNoTracking().Where(p => p.ProfileImageMediaKey != null && _keys.Contains(p.ProfileImageMediaKey))
                .Select(p => p.ProfileImageMediaKey!).ToArrayAsync(timeout.Token));
            var identities = await db.Patients.AsNoTracking().Where(p => _keys.Contains(p.PersonalIdFrontMediaKey!) || _keys.Contains(p.PersonalIdBackMediaKey!))
                .Select(p => new { p.PersonalIdFrontMediaKey, p.PersonalIdBackMediaKey }).ToArrayAsync(timeout.Token);
            retained.AddRange(identities.SelectMany(p => new[] { p.PersonalIdFrontMediaKey, p.PersonalIdBackMediaKey }).OfType<string>());
            await media.DeleteRangeAsync(_keys.Except(retained, StringComparer.Ordinal).ToArray(), timeout.Token); Clear();
        }
        catch (Exception) { Failed(logger, null); }
    }
}
