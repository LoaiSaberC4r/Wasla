using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Clinical;
using Wasla.Domain.Tickets;
using Wasla.Application.Features.Clinical;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

public sealed partial class WaslaDbContext
{
    private async Task SynchronizeMedicalRecordExpiryAsync(CancellationToken ct)
    {
        var ended = ChangeTracker.Entries<Ticket>().Where(e => e.State == EntityState.Modified && e.Entity.Status != TicketStatus.InProgress &&
            e.Property(t => t.Status).OriginalValue == TicketStatus.InProgress).Select(e => e.Entity).ToArray();
        foreach (var ticket in ended)
        {
            var requests = await Set<HiddenEncounterAccessRequest>().Where(r => r.RequestingTicketId == ticket.Id &&
                (r.Status == HiddenAccessRequestStatus.Pending || r.Status == HiddenAccessRequestStatus.Approved)).ToArrayAsync(ct);
            foreach (var r in requests) r.Expire(ticket.ModifiedByApplicationUserId ?? ticket.CreatedByApplicationUserId,
                ticket.ModifiedOnUtc ?? DateTime.UtcNow);
        }
        var tracked = ChangeTracker.Entries<HiddenEncounterAccessRequestHistory>().Select(e => e.Entity.Id).ToHashSet();
        Set<HiddenEncounterAccessRequestHistory>().AddRange(ChangeTracker.Entries<HiddenEncounterAccessRequest>().SelectMany(e => e.Entity.History).Where(h => !tracked.Contains(h.Id)).ToArray());
        if (ChangeTracker.Entries().Any(e => e.State is EntityState.Modified or EntityState.Deleted && e.Entity is HiddenEncounterAccessRequestHistory))
            throw new InvalidOperationException("Access request history is immutable.");
        if (ChangeTracker.Entries<HiddenEncounterAccessRequest>().Any(e => e.State == EntityState.Deleted))
            throw new InvalidOperationException("Access requests cannot be deleted.");
        foreach (var entry in ChangeTracker.Entries<HiddenEncounterAccessRequest>().Where(e => e.State == EntityState.Modified))
            if (entry.Properties.Any(p => p.IsModified && p.Metadata.Name is not ("Status" or "DecidedAtUtc" or "RevokedAtUtc" or
                "DecidedByApplicationUserId" or "RevokedByApplicationUserId" or "VisibilityRevisionAtApproval" or "RowVersion")))
                throw new InvalidOperationException("Access request relationship snapshots and reasons are immutable.");
        if (ChangeTracker.Entries<MedicalRecordAccessAudit>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Medical record access audits are immutable.");
    }
}
