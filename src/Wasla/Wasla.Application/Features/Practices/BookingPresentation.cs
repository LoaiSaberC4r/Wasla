using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Features.Subscriptions;
using Wasla.Application.Features.Doctors;
using Wasla.Application.Persistence;
using Wasla.Domain.Practices;
using Wasla.Domain.Subscriptions;

namespace Wasla.Application.Features.Practices;

// A narrow projection over the existing default segment/visit price, without operational segment controls.
[Wasla.Application.Features.Subscriptions.PracticeCommercialPermission("DoctorPractices.ManageOwn")]
public sealed record SetBookingPresentationPriceCommand(Guid PracticeId, decimal NewConsultationPrice, string? RowVersion)
    : ICommand<DoctorPracticePriceResponse>, ITransactionalCommand<WaslaWritePersistence>, Wasla.Application.Features.Subscriptions.IPracticeCommercialRequest;
internal sealed class SetBookingPresentationPriceCommandValidator : AbstractValidator<SetBookingPresentationPriceCommand>
{
    public SetBookingPresentationPriceCommandValidator()
    { RuleFor(x => x.PracticeId).NotEmpty(); RuleFor(x => x.NewConsultationPrice).GreaterThan(0).PrecisionScale(18, 2, true); RuleFor(x => x.RowVersion).Must(v => v is null || RowVersionCodec.IsValid(v)); }
}
internal sealed class SetBookingPresentationPriceCommandHandler(IWaslaDataStore store, ICurrentUser current,
    ISubscriptionEntitlementService entitlements) : ICommandHandler<SetBookingPresentationPriceCommand, DoctorPracticePriceResponse>
{
    public async Task<Result<DoctorPracticePriceResponse>> Handle(SetBookingPresentationPriceCommand request, CancellationToken cancellationToken)
    {
        var owned = await DoctorPracticeAccess.LoadOwnedPracticeAsync(store, current, request.PracticeId, cancellationToken);
        if (owned.IsFailure) return Result<DoctorPracticePriceResponse>.Fail(owned.Errors);
        var access = await entitlements.EvaluateForPracticeAsync(owned.Value.Practice.DoctorId, owned.Value.Practice.Id, "PRACTICE_MANAGEMENT", FeatureAccessLevel.Full, cancellationToken);
        if (access.IsFailure) return Result<DoctorPracticePriceResponse>.Fail(access.Errors);
        await store.AcquireDoctorScheduleLockAsync(owned.Value.Practice.DoctorId, cancellationToken);
        var segment = (await store.ListDoctorPracticeSegmentsAsync(request.PracticeId, cancellationToken)).Single(s => s.IsDefault);
        var visit = (await store.ListDoctorPracticeVisitTypesAsync(request.PracticeId, cancellationToken)).Single(v => v.Type == DoctorPracticeVisitTypeCode.NewConsultation);
        var price = (await store.ListDoctorPracticePricesAsync(request.PracticeId, cancellationToken)).SingleOrDefault(p => p.SegmentId == segment.Id && p.VisitTypeId == visit.Id);
        if (price is null)
        {
            if (request.RowVersion is not null) return Result<DoctorPracticePriceResponse>.Fail(SubscriptionErrors.Conflict());
            var created = DoctorPracticeSegmentVisitTypePrice.Create(Guid.NewGuid(), request.PracticeId, segment.Id, visit.Id, request.NewConsultationPrice, owned.Value.ActorId);
            if (created.IsFailure) return Result<DoctorPracticePriceResponse>.Fail(created.Errors);
            price = created.Value; store.Add(price);
        }
        else
        {
            var supplied = RowVersionCodec.Decode(request.RowVersion ?? "");
            if (supplied is null || !price.RowVersion.AsSpan().SequenceEqual(supplied)) return Result<DoctorPracticePriceResponse>.Fail(SubscriptionErrors.Conflict());
            // List queries are tracked by the existing store. Use its normal concurrency contract.
            var tracked = await store.FindDoctorPracticePriceAsync(price.Id, cancellationToken);
            if (tracked is null) return Result<DoctorPracticePriceResponse>.Fail(SubscriptionErrors.NotFound);
            price = tracked;
            var update = price.Update(request.NewConsultationPrice, owned.Value.ActorId);
            if (update.IsFailure) return Result<DoctorPracticePriceResponse>.Fail(update.Errors);
            store.SetOriginalRowVersion(price, supplied);
        }
        await store.SaveChangesAsync(cancellationToken);
        return Result<DoctorPracticePriceResponse>.Ok(DoctorPracticeCatalogMapper.Map(price));
    }
}
