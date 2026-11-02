using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Subscriptions;

namespace Wasla.Application.Features.Subscriptions;

public enum SubscriptionResourceKind { Reservation = 1, Ticket = 2, Encounter = 3, Prescription = 4, LabRequest = 5, RadiologyRequest = 6, LabResult = 7, RadiologyResult = 8, LabSubmission = 9, RadiologySubmission = 10, Payment = 11, Refund = 12 }
public sealed record SubscriptionResource(SubscriptionResourceKind Kind, Guid Id);
public sealed record SubscriptionCommitment(Guid DoctorId, DateTime CreatedOnUtc);
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
        var ended = await store.LatestEndedAsync(doctorId, ct);
        if (ended is null || ended.EndsAtUtc > clock.UtcNow || ended.Status is SubscriptionStatus.Cancelled or SubscriptionStatus.Pending or SubscriptionStatus.Scheduled)
            return Result.Fail(SubscriptionErrors.Denied("NotActive"));
        var feature = (await store.FeaturesAsync(ended.Id, ct)).SingleOrDefault(f => f.FeatureCodeSnapshot == featureCode);
        if (feature?.AccessLevel != FeatureAccessLevel.Full) return Result.Fail(SubscriptionErrors.Denied());
        var commitment = await store.CommitmentAsync(resource, ct);
        return commitment?.DoctorId == doctorId && commitment.CreatedOnUtc < ended.EndsAtUtc
            ? Result.Ok() : Result.Fail(SubscriptionErrors.Denied("Expired"));
    }
}
