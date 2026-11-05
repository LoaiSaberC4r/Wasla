using BuildingBlock.Domain.EntitiesHelper;

namespace Wasla.Domain.Subscriptions;

public enum CommercialCommitmentKind { Reservation = 1, WalkInTicket = 2 }
/// <summary>Write-once grant at booking acceptance; retained when the live reservation is archived.</summary>
public sealed class ReservationCommercialProvenance : AggregateRoot<Guid>
{
    private ReservationCommercialProvenance() { }
    public ReservationCommercialProvenance(Guid reservationId, Guid doctorId, Guid practiceId, Guid allocationId,
        Guid contractId, Guid periodId, DateTime acceptedAtUtc, SubscriptionTerms terms, CommercialCommitmentKind originKind = CommercialCommitmentKind.Reservation)
    {
        Id = reservationId; DoctorId = doctorId; DoctorPracticeId = practiceId; AllocationId = allocationId;
        SubscriptionContractId = contractId; SubscriptionId = periodId; AcceptedAtUtc = acceptedAtUtc;
        TermsSnapshotJson = System.Text.Json.JsonSerializer.Serialize(terms);
        OriginKind = originKind;
    }
    public Guid DoctorId { get; private set; }
    public Guid DoctorPracticeId { get; private set; }
    public Guid AllocationId { get; private set; }
    public Guid SubscriptionContractId { get; private set; }
    public Guid SubscriptionId { get; private set; }
    public DateTime AcceptedAtUtc { get; private set; }
    public string TermsSnapshotJson { get; private set; } = "";
    public CommercialCommitmentKind OriginKind { get; private set; }
    public SubscriptionTerms Terms => System.Text.Json.JsonSerializer.Deserialize<SubscriptionTerms>(TermsSnapshotJson)!;
    public bool Grants(string feature, FeatureAccessLevel access) => Terms.Features.Any(f => f.Code == feature && f.AccessLevel >= access);
}
