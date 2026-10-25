using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Notifications;
using Wasla.Domain.Patients;
using Wasla.Domain.Common;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

public sealed partial class WaslaDbContext
{
    private async Task CaptureVerifiedNotificationContactsAsync(CancellationToken ct)
    {
        var proofs = ChangeTracker.Entries<PatientOnboardingChallenge>().Where(e => e.State is EntityState.Added or EntityState.Modified &&
            e.Entity.VerifiedOnUtc != null && e.Entity.ConsumedOnUtc != null && e.Entity.InvalidatedOnUtc == null &&
            (e.State == EntityState.Added || e.Property(p => p.ConsumedOnUtc).OriginalValue == null)).Select(e => e.Entity).ToArray();
        foreach (var proof in proofs)
        {
            var user = proof.ApplicationUserId;
            if (proof.Purpose == PatientOnboardingPurpose.Register && proof.MutationResponseJson is { } response)
            {
                try { using var json = JsonDocument.Parse(response); if (json.RootElement.TryGetProperty("applicationUserId", out var id) && id.TryGetGuid(out var parsed)) user = parsed; }
                catch (JsonException) { continue; }
            }
            if (user is { } userId) await CaptureVerifiedContactAsync(userId, proof.NormalizedEmail, proof.Id, proof.VerifiedOnUtc!.Value, ct);
        }
        foreach (var invitation in ChangeTracker.Entries<PatientActivationInvitation>().Where(e => e.State == EntityState.Modified &&
            e.Entity.ConsumedOnUtc != null && e.Property(p => p.ConsumedOnUtc).OriginalValue == null).Select(e => e.Entity).ToArray())
        {
            var user = invitation.ApplicationUserId;
            if (user is null && invitation.ClaimRequestId is { } claim)
                user = PatientClaimRequests.Local.SingleOrDefault(c => c.Id == claim)?.LinkedApplicationUserId ??
                    await PatientClaimRequests.Where(c => c.Id == claim).Select(c => c.LinkedApplicationUserId).SingleOrDefaultAsync(ct);
            if (user is { } userId) await CaptureVerifiedContactAsync(userId, invitation.NormalizedEmail, invitation.Id, invitation.ConsumedOnUtc!.Value, ct);
        }
    }
    private async Task CaptureVerifiedContactAsync(Guid userId, string email, Guid evidence, DateTime verified, CancellationToken ct)
    {
        var user = ApplicationUsers.Local.SingleOrDefault(u => u.Id == userId) ?? await ApplicationUsers.SingleOrDefaultAsync(u => u.Id == userId, ct);
        var link = PatientAccountLinks.Local.SingleOrDefault(l => l.ApplicationUserId == userId) ?? await PatientAccountLinks.SingleOrDefaultAsync(l => l.ApplicationUserId == userId, ct);
        if (user is not { UserType: UserType.Patient, IsActive: true, IsFirstLogin: false } || link is null || !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)) return;
        var fingerprint = NotificationVerifiedContact.Fingerprint(email);
        if (Set<NotificationVerifiedContact>().Local.Any(c => c.ApplicationUserId == userId && c.PatientId == link.PatientId && c.AddressFingerprint == fingerprint) ||
            await Set<NotificationVerifiedContact>().AnyAsync(c => c.ApplicationUserId == userId && c.PatientId == link.PatientId && c.AddressFingerprint == fingerprint, ct)) return;
        Set<NotificationVerifiedContact>().Add(new(userId, link.PatientId, email, evidence, DateTime.SpecifyKind(verified, DateTimeKind.Utc)));
    }
}
