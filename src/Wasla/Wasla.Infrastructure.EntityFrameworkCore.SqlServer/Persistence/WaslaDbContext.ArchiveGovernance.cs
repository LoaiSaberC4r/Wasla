using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Archives;
using Wasla.Domain.Practices;
using Wasla.Domain.Security;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

public sealed partial class WaslaDbContext
{
    private async Task CaptureArchiveGovernanceAsync(CancellationToken ct)
    {
        // Only new, authenticated governance writes are captured. No inferred
        // historical permission history or second copy of existing histories.
        if (governanceActor?.IsAuthenticated != true || governanceActor.UserId is not { } actor) return;
        var now = reportingClock?.UtcNow ?? DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<RolePermission>().Where(x => x.State is EntityState.Added or EntityState.Deleted).ToArray())
            ArchiveAuditEvents.Add(new(entry.State == EntityState.Added ? "PermissionGranted" : "PermissionRevoked", "Governance", entry.Entity.RoleId, null, actor, now));
        foreach (var entry in ChangeTracker.Entries<ReceptionPracticeAssignment>().Where(x => x.State is EntityState.Added or EntityState.Modified).ToArray())
            ArchiveAuditEvents.Add(new(entry.State == EntityState.Added ? "DelegationCreated" : entry.Entity.IsActive ? "DelegationChanged" : "DelegationDeactivated", "Governance", entry.Entity.Id, entry.Entity.DoctorPracticeId, actor, now));
        foreach (var entry in ChangeTracker.Entries<ReceptionPracticeAssignmentPermission>().Where(x => x.State is EntityState.Added or EntityState.Deleted).ToArray())
        {
            var assignment = ReceptionPracticeAssignments.Local.SingleOrDefault(x => x.Id == entry.Entity.AssignmentId) ??
                await ReceptionPracticeAssignments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == entry.Entity.AssignmentId, ct);
            if (assignment != null) ArchiveAuditEvents.Add(new(entry.State == EntityState.Added ? "DelegationGranted" : "DelegationRevoked", "Governance", assignment.Id, assignment.DoctorPracticeId, actor, now));
        }
    }
}
