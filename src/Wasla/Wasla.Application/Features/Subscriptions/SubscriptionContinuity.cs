using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Subscriptions;

namespace Wasla.Application.Features.Subscriptions;

public enum SubscriptionResourceKind { Reservation = 1, Ticket = 2, Encounter = 3, Prescription = 4, LabRequest = 5, RadiologyRequest = 6, LabResult = 7, RadiologyResult = 8, LabSubmission = 9, RadiologySubmission = 10, Payment = 11, Refund = 12, Practice = 13, AuthorizationPreflight = 14 }
public sealed record SubscriptionResource(SubscriptionResourceKind Kind, Guid Id);
public sealed record SubscriptionCommitment(Guid DoctorId, DateTime CreatedOnUtc, Guid? PracticeId = null, Guid? ReservationId = null, bool Confirmed = false, Guid? TicketId = null);
public interface ISubscriptionOperationalContinuityGuard
{
    Task<Result> EvaluateAsync(Guid doctorId, string featureCode, SubscriptionResource resource, CancellationToken ct);
}
public sealed class SubscriptionOperationalContinuityGuard(ISubscriptionDataStore store, IWaslaDataStore doctors,
    IDateTimeProvider clock) : ISubscriptionOperationalContinuityGuard
{
    public async Task<Result> EvaluateAsync(Guid doctorId, string featureCode, SubscriptionResource resource, CancellationToken ct)
    {
        var doctor = await doctors.FindDoctorByIdAsync(doctorId, ct);
        var user = doctor is null ? null : await doctors.FindUserByIdAsync(doctor.ApplicationUserId, ct);
        if (doctor?.ApprovalStatus != DoctorApprovalStatus.Approved || user is not { IsActive: true }) return Result.Fail(SubscriptionErrors.Denied("DoctorNotApproved"));
        var commitment = await store.CommitmentAsync(resource, ct);
        if (commitment?.DoctorId != doctorId || commitment.PracticeId is null || (commitment.ReservationId is null && commitment.TicketId is null) || !commitment.Confirmed)
            return Result.Fail(SubscriptionErrors.Denied("OriginalEntitlementNotProven"));
        var origin = await store.ProvenanceAsync((commitment.ReservationId ?? commitment.TicketId)!.Value, ct);
        var period = origin is null ? null : await store.FindAsync(origin.SubscriptionId, ct);
        if (period is not null && (origin!.AcceptedAtUtc < period.StartsAtUtc || origin.AcceptedAtUtc >= period.EndsAtUtc))
            return Result.Fail(SubscriptionErrors.Denied("Expired"));
        var expectedKind = commitment.ReservationId.HasValue ? CommercialCommitmentKind.Reservation : CommercialCommitmentKind.WalkInTicket;
        var allowed = origin?.DoctorId == doctorId && period?.DoctorId == doctorId && period.SubscriptionContractId == origin.SubscriptionContractId && origin.OriginKind == expectedKind && origin.DoctorPracticeId == commitment.PracticeId && origin.AcceptedAtUtc <= clock.UtcNow && origin.Grants(featureCode, FeatureAccessLevel.Full)
            ;
        CommercialSubscriptionDiagnostics.Continuity(featureCode, allowed);
        return allowed ? Result.Ok() : Result.Fail(SubscriptionErrors.Denied("OriginalEntitlementNotProven"));
    }
}
