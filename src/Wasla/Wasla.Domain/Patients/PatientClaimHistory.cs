using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
namespace Wasla.Domain.Patients;
public enum PatientClaimAction { Submitted = 1, ModificationRequested = 2, Resubmitted = 3, Approved = 4, Rejected = 5, Activated = 6, Cancelled = 7, Expired = 8, EvidencePurged = 9, LegalHoldPlaced = 10, LegalHoldReleased = 11 }
public sealed class PatientClaimHistory : Entity<Guid>
{
    private PatientClaimHistory() { }
    public PatientClaimHistory(Guid id, Guid claimId, int revision, PatientClaimStatus? from, PatientClaimStatus to,
        PatientClaimAction action, Guid? actor, DateTime now, string? reason = null, string? safeMetadataJson = null) : base(id)
    {
        ClaimRequestId = claimId; RevisionNumber = revision; FromStatus = from; ToStatus = to; Action = action;
        ActorApplicationUserId = actor; OccurredOnUtc = now; Reason = reason; SafeMetadataJson = safeMetadataJson;
    }
    public Guid ClaimRequestId { get; private set; }
    public int RevisionNumber { get; private set; }
    public PatientClaimStatus? FromStatus { get; private set; }
    public PatientClaimStatus ToStatus { get; private set; }
    public PatientClaimAction Action { get; private set; }
    public Guid? ActorApplicationUserId { get; private set; }
    public DateTime OccurredOnUtc { get; private set; }
    public string? Reason { get; private set; }
    public string? SafeMetadataJson { get; private set; }
}
