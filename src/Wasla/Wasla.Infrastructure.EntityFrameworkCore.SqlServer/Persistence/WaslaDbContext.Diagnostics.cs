using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Wasla.Domain.Diagnostics;
using Wasla.Domain.Labs;
using Wasla.Domain.Radiology;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

public sealed partial class WaslaDbContext
{
    private static void OnlyChanges(EntityEntry entry, params string[] fields)
    {
        if (entry.Properties.Any(p => p.IsModified && !fields.Contains(p.Metadata.Name)))
            throw new InvalidOperationException("Retained diagnostic content is immutable.");
    }

    private async Task GuardDiagnosticContentAsync(CancellationToken ct)
    {
        foreach (var entry in ChangeTracker.Entries<LabRequest>().Where(e => e.State == EntityState.Modified))
            if (entry.Property(x => x.Status).OriginalValue != DiagnosticRequestStatus.Draft)
                OnlyChanges(entry, "Status", "Revision", "RowVersion");
            else OnlyChanges(entry, "Status", "PatientInstructions", "RequestedAtUtc", "Revision", "RowVersion");
        foreach (var entry in ChangeTracker.Entries<RadiologyRequest>().Where(e => e.State == EntityState.Modified))
            if (entry.Property(x => x.Status).OriginalValue != DiagnosticRequestStatus.Draft)
                OnlyChanges(entry, "Status", "Revision", "RowVersion");
            else OnlyChanges(entry, "Status", "PatientInstructions", "RequestedAtUtc", "Revision", "RowVersion");

        foreach (var entry in ChangeTracker.Entries<LabRequestItem>().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            var parent = ChangeTracker.Entries<LabRequest>().SingleOrDefault(e => e.Entity.Id == entry.Entity.RequestId);
            if (parent?.State == EntityState.Added) continue;
            var draft = parent is not null ? parent.Property(x => x.Status).OriginalValue == DiagnosticRequestStatus.Draft :
                await LabRequests.AsNoTracking().AnyAsync(x => x.Id == entry.Entity.RequestId && x.Status == DiagnosticRequestStatus.Draft, ct);
            if (entry.State is EntityState.Added or EntityState.Deleted)
            { if (!draft) throw new InvalidOperationException("Issued order items cannot be added or deleted."); continue; }
            if (entry.Property(x => x.Status).OriginalValue == DiagnosticItemStatus.Cancelled)
                throw new InvalidOperationException("Cancelled order items are immutable.");
            if (draft) OnlyChanges(entry, "DoctorInstructions");
            else OnlyChanges(entry, "Status", "CancellationReason", "CancelledByUserId", "CancelledAtUtc");
        }
        foreach (var entry in ChangeTracker.Entries<RadiologyRequestItem>().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            var parent = ChangeTracker.Entries<RadiologyRequest>().SingleOrDefault(e => e.Entity.Id == entry.Entity.RequestId);
            if (parent?.State == EntityState.Added) continue;
            var draft = parent is not null ? parent.Property(x => x.Status).OriginalValue == DiagnosticRequestStatus.Draft :
                await RadiologyRequests.AsNoTracking().AnyAsync(x => x.Id == entry.Entity.RequestId && x.Status == DiagnosticRequestStatus.Draft, ct);
            if (entry.State is EntityState.Added or EntityState.Deleted)
            { if (!draft) throw new InvalidOperationException("Issued order items cannot be added or deleted."); continue; }
            if (entry.Property(x => x.Status).OriginalValue == DiagnosticItemStatus.Cancelled)
                throw new InvalidOperationException("Cancelled order items are immutable.");
            if (draft) OnlyChanges(entry, "DoctorInstructions");
            else OnlyChanges(entry, "Status", "CancellationReason", "CancelledByUserId", "CancelledAtUtc");
        }
        foreach (var entry in ChangeTracker.Entries<LabResult>().Where(e => e.State == EntityState.Modified))
            OnlyChanges(entry, "CurrentVersionNumber", "Revision", "RowVersion");
        foreach (var entry in ChangeTracker.Entries<RadiologyResult>().Where(e => e.State == EntityState.Modified))
            OnlyChanges(entry, "CurrentVersionNumber", "Revision", "RowVersion");
        foreach (var entry in ChangeTracker.Entries<PatientLabResultSubmission>().Where(e => e.State == EntityState.Modified))
        {
            if (entry.Property(x => x.Status).OriginalValue != PatientSubmissionStatus.PendingReview)
                throw new InvalidOperationException("Terminal submissions are immutable.");
            OnlyChanges(entry, "Status", "AcceptedResultId", "PatientVisibleReason", "Revision", "RowVersion");
        }
        foreach (var entry in ChangeTracker.Entries<PatientRadiologyResultSubmission>().Where(e => e.State == EntityState.Modified))
        {
            if (entry.Property(x => x.Status).OriginalValue != PatientSubmissionStatus.PendingReview)
                throw new InvalidOperationException("Terminal submissions are immutable.");
            OnlyChanges(entry, "Status", "AcceptedResultId", "PatientVisibleReason", "Revision", "RowVersion");
        }
        foreach (var entry in ChangeTracker.Entries<LabCatalogImportBatch>().Where(e => e.State == EntityState.Modified))
        {
            if (entry.Property(x => x.Status).OriginalValue != DiagnosticImportStatus.Staged)
                throw new InvalidOperationException("Closed import batches are immutable.");
            OnlyChanges(entry, "Status", "AppliedAtUtc", "AppliedByUserId", "DiscardedAtUtc", "DiscardedByUserId", "RowVersion");
        }
        foreach (var entry in ChangeTracker.Entries<RadiologyCatalogImportBatch>().Where(e => e.State == EntityState.Modified))
        {
            if (entry.Property(x => x.Status).OriginalValue != DiagnosticImportStatus.Staged)
                throw new InvalidOperationException("Closed import batches are immutable.");
            OnlyChanges(entry, "Status", "AppliedAtUtc", "AppliedByUserId", "DiscardedAtUtc", "DiscardedByUserId", "RowVersion");
        }
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State == EntityState.Added))
        {
            // Children may only be appended together with their new immutable owner.
            var parent = entry.Entity switch
            {
                LabResultCoverage x => ChangeTracker.Entries<LabResultVersion>().Any(e => e.Entity.Id == x.ResultVersionId && e.State == EntityState.Added),
                LabResultAttachment x => ChangeTracker.Entries<LabResultVersion>().Any(e => e.Entity.Id == x.ResultVersionId && e.State == EntityState.Added),
                RadiologyResultCoverage x => ChangeTracker.Entries<RadiologyResultVersion>().Any(e => e.Entity.Id == x.ResultVersionId && e.State == EntityState.Added),
                RadiologyResultAttachment x => ChangeTracker.Entries<RadiologyResultVersion>().Any(e => e.Entity.Id == x.ResultVersionId && e.State == EntityState.Added),
                PatientLabResultSubmissionAttachment x => ChangeTracker.Entries<PatientLabResultSubmission>().Any(e => e.Entity.Id == x.SubmissionId && e.State == EntityState.Added),
                PatientRadiologyResultSubmissionAttachment x => ChangeTracker.Entries<PatientRadiologyResultSubmission>().Any(e => e.Entity.Id == x.SubmissionId && e.State == EntityState.Added),
                LabCatalogImportRecord x => ChangeTracker.Entries<LabCatalogImportBatch>().Any(e => e.Entity.Id == x.ImportBatchId && e.State == EntityState.Added),
                RadiologyCatalogImportRecord x => ChangeTracker.Entries<RadiologyCatalogImportBatch>().Any(e => e.Entity.Id == x.ImportBatchId && e.State == EntityState.Added),
                _ => true
            };
            if (!parent) throw new InvalidOperationException("Immutable diagnostic content cannot be extended after persistence.");
        }
    }
}
