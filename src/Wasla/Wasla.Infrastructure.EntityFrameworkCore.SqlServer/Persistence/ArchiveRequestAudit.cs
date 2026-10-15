using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Time;
using Wasla.Application.Features.Archives;
using Wasla.Domain.Archives;
using Microsoft.Extensions.DependencyInjection;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class ArchiveRequestAudit(IServiceScopeFactory scopes, ICurrentUser user, IDateTimeProvider clock) : IArchiveRequestAudit
{
    public async Task RejectedAsync(bool controlRequest, int statusCode, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.UserId is not { } actor) return;
        // A failed command may leave rolled-back tracked mutations in its scope.
        // The security event must never save those pending business changes.
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WaslaDbContext>();
        // A rejected probe proves no target ownership or Practice relationship.
        // Preserve only the authenticated actor and safe outcome, never its input.
        db.ArchiveAuditEvents.Add(new(controlRequest ? "ArchiveControlRejected" : "ArchiveLookupDenied",
            controlRequest ? "ArchiveControl" : "ArchiveLookup", null, null, actor, clock.UtcNow,
            outcome: "Denied", failureCode: statusCode == 409 ? "Archive.Conflict" : "Archive.AccessDenied"));
        await db.SaveChangesAsync(ct);
    }
}
