using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Email;
using Wasla.Application.Features.Doctors;
using Wasla.Application.Features.Practices;
using Wasla.Application.Features.PublicDiscovery;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Doctors;
using Wasla.Domain.Families;
using Wasla.Domain.Patients;
using Wasla.Domain.Practices;
using Wasla.Domain.Reservations;
using Wasla.Domain.Security;

namespace Wasla.Application.Features.Reservations;

public sealed record ReservationMetadataItem(string Code, string NameAr, string NameEn);
public sealed record ReservationReasonMetadataItem(
    string Code,
    string NameAr,
    string NameEn,
    bool RequiresComment);
public sealed record ReservationMetadataResponse(
    IReadOnlyList<ReservationMetadataItem> Statuses,
    IReadOnlyList<ReservationMetadataItem> BookingSources,
    IReadOnlyList<ReservationReasonMetadataItem> PatientCancellationReasons,
    IReadOnlyList<ReservationReasonMetadataItem> ProviderCancellationReasons,
    int MaximumAdvanceBookingDays,
    int MaxPatientReschedulesPerReservation);
public sealed record BookablePatientResponse(
    Guid PatientId,
    string NameAr,
    string? NameEn,
    DateOnly DateOfBirth,
    Gender Gender,
    bool IsSelf,
    string? RelationshipType);
public sealed record ReservationPartyResponse(Guid Id, string NameAr, string? NameEn);
public sealed record ReservationPracticeResponse(
    Guid Id,
    string NameAr,
    string? NameEn,
    string Address);
public sealed record ReservationAppointmentResponse(
    DateOnly BusinessDate,
    TimeOnly LocalTime,
    DateTime StartUtc,
    int DurationMinutes,
    string TimeZoneId,
    DateTime GraceEndsAtUtc);
public sealed record ReservationSnapshotResponse(
    Guid SegmentId,
    string SegmentNameAr,
    string? SegmentNameEn,
    int SegmentPriority,
    Guid VisitTypeId,
    string VisitTypeCode,
    string VisitTypeNameAr,
    string? VisitTypeNameEn,
    decimal Price);
public sealed record ReservationCapabilitiesResponse(
    bool CanCancel,
    string? CancelBlockedReason,
    bool CanReschedule,
    string? RescheduleBlockedReason,
    bool CanRestoreFromNoShow,
    string? RestoreBlockedReason,
    DateTime? CancellationAllowedUntilUtc,
    DateTime? RescheduleAllowedUntilUtc,
    int PatientReschedulesUsed,
    int PatientReschedulesRemaining);
public sealed record ReservationTimelineItemResponse(
    string EventType,
    string NameAr,
    string NameEn,
    DateTime OccurredOnUtc,
    string ActionInitiator,
    Guid? PerformedByApplicationUserId,
    string? ReasonCode,
    string? Reason,
    DateOnly? OldBusinessDate,
    TimeOnly? OldLocalTime,
    DateOnly? NewBusinessDate,
    TimeOnly? NewLocalTime,
    bool? PatientConsentConfirmed);
public sealed record ReservationDetailsResponse(
    Guid ReservationId,
    string ReservationReference,
    string Status,
    bool IsLate,
    ReservationPartyResponse Patient,
    ReservationPartyResponse Doctor,
    ReservationPracticeResponse Practice,
    ReservationAppointmentResponse Appointment,
    ReservationSnapshotResponse Snapshot,
    string BookingSource,
    string? BookingNote,
    DateTime CreatedOnUtc,
    DateTime? LastRescheduledOnUtc,
    string? CancellationReasonCode,
    string? CancellationComment,
    DateTime? CancelledOnUtc,
    ReservationCapabilitiesResponse Capabilities,
    IReadOnlyList<ReservationTimelineItemResponse> Timeline,
    string RowVersion);
public sealed record ReservationListItemResponse(
    Guid ReservationId,
    string ReservationReference,
    string Status,
    bool IsLate,
    Guid PatientId,
    string PatientNameAr,
    string? PatientNameEn,
    Guid DoctorPracticeId,
    string PracticeNameAr,
    string? PracticeNameEn,
    DateOnly BusinessDate,
    TimeOnly LocalTime,
    string SegmentNameAr,
    string? SegmentNameEn,
    string BookingSource,
    decimal Price,
    string RowVersion);
public sealed record ReservationListSummary(
    long Total,
    long Active,
    long Late,
    long NoShow,
    long Cancelled,
    long ConvertedToTicket,
    long Expired);
public sealed record ReservationPagedResponse(
    IReadOnlyList<ReservationListItemResponse> Items,
    long TotalCount,
    int PageNumber,
    int PageSize,
    ReservationListSummary Summary);
public sealed record ReservationFilterOptionsResponse(
    IReadOnlyList<ReservationMetadataItem> Statuses,
    IReadOnlyList<ReservationMetadataItem> BookingSources,
    IReadOnlyList<ReservationMetadataItem> Segments);
public sealed record ReservationAvailableDateResponse(DateOnly Date, bool IsAvailable);
public sealed record ReservationAvailableSlotResponse(DateOnly Date, TimeOnly Time, int DurationMinutes);
public sealed record ReservationBookingPriceResponse(
    Guid VisitTypeId,
    string VisitTypeCode,
    string VisitTypeNameAr,
    string? VisitTypeNameEn,
    decimal Price);
public sealed record ReservationBookingSegmentOptionResponse(
    Guid SegmentId,
    string NameAr,
    string? NameEn,
    int Priority,
    IReadOnlyList<ReservationBookingPriceResponse> VisitTypes);
public sealed record ReservationBookingOptionsResponse(
    Guid PracticeId,
    DateOnly Date,
    TimeOnly Time,
    IReadOnlyList<ReservationBookingSegmentOptionResponse> Segments);
public enum ReservationAvailabilityChannel
{
    Patient = 1,
    Reception = 2,
    Doctor = 3
}

public sealed record GetReservationMetadataQuery : IQuery<ReservationMetadataResponse>;
public sealed record ListBookablePatientsQuery : IQuery<IReadOnlyList<BookablePatientResponse>>;
public sealed record CreateReservationCommand(
    Guid PatientId,
    Guid DoctorPracticeId,
    DateOnly BusinessDate,
    TimeOnly SlotStartTime,
    Guid SegmentId,
    Guid VisitTypeId,
    string? BookingNote,
    string IdempotencyKey)
    : ICommand<ReservationDetailsResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record CreatePracticeReservationCommand(
    Guid PracticeId,
    Guid PatientId,
    DateOnly BusinessDate,
    TimeOnly SlotStartTime,
    Guid SegmentId,
    Guid VisitTypeId,
    string? BookingNote,
    string IdempotencyKey)
    : ICommand<ReservationDetailsResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record CancelMineReservationCommand(
    Guid ReservationId,
    string ReasonCode,
    string? Comment,
    string RowVersion,
    string IdempotencyKey)
    : ICommand<ReservationDetailsResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record CancelPracticeReservationCommand(
    Guid PracticeId,
    Guid ReservationId,
    string ReasonCode,
    string? Comment,
    string RowVersion,
    string IdempotencyKey,
    bool IsDoctor)
    : ICommand<ReservationDetailsResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record RescheduleMineReservationCommand(
    Guid ReservationId,
    DateOnly BusinessDate,
    TimeOnly SlotStartTime,
    string RowVersion,
    string IdempotencyKey)
    : ICommand<ReservationDetailsResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record ReschedulePracticeReservationCommand(
    Guid PracticeId,
    Guid ReservationId,
    DateOnly BusinessDate,
    TimeOnly SlotStartTime,
    bool PatientConsentConfirmed,
    string Reason,
    string RowVersion,
    string IdempotencyKey,
    bool IsDoctor)
    : ICommand<ReservationDetailsResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record RestorePracticeNoShowReservationCommand(
    Guid PracticeId,
    Guid ReservationId,
    string RowVersion,
    string IdempotencyKey)
    : ICommand<ReservationDetailsResponse>, ITransactionalCommand<WaslaWritePersistence>;
// Internal operational-day expiration seam. Reservation-to-Ticket conversion and runtime NoShow
// now occur only inside the atomic Phase 11 Ticket workflows.
internal sealed record ExpireReservationCommand(Guid ReservationId)
    : ICommand, ITransactionalCommand<WaslaWritePersistence>;
public sealed record ListMineReservationsQuery(
    Guid? PatientId,
    string View,
    ReservationStatus? Status,
    DateOnly? FromDate,
    DateOnly? ToDate,
    int PageNumber,
    int PageSize) : IQuery<ReservationPagedResponse>;
public sealed record GetMineReservationQuery(Guid ReservationId) : IQuery<ReservationDetailsResponse>;
public sealed record ListPracticeReservationsQuery(
    Guid PracticeId,
    DateOnly? FromDate,
    DateOnly? ToDate,
    ReservationStatus? Status,
    Guid? SegmentId,
    ReservationBookingSource? BookingSource,
    bool? IsLate,
    string? Search,
    int PageNumber,
    int PageSize,
    bool IsDoctor) : IQuery<ReservationPagedResponse>;
public sealed record GetPracticeReservationQuery(Guid PracticeId, Guid ReservationId, bool IsDoctor)
    : IQuery<ReservationDetailsResponse>;
public sealed record ListAdministrativeReservationsQuery(
    string? Search,
    Guid? DoctorId,
    Guid? PracticeId,
    ReservationStatus? Status,
    ReservationBookingSource? BookingSource,
    DateOnly? FromDate,
    DateOnly? ToDate,
    int PageNumber,
    int PageSize) : IQuery<ReservationPagedResponse>;
public sealed record GetAdministrativeReservationQuery(Guid ReservationId) : IQuery<ReservationDetailsResponse>;
public sealed record GetReservationFilterOptionsQuery(Guid PracticeId, bool IsDoctor)
    : IQuery<ReservationFilterOptionsResponse>;
public sealed record GetBookingAvailableDatesQuery(
    Guid PracticeId,
    Guid? ReservationId,
    ReservationAvailabilityChannel Channel)
    : IQuery<IReadOnlyList<ReservationAvailableDateResponse>>;
public sealed record GetBookingAvailableSlotsQuery(
    Guid PracticeId,
    DateOnly Date,
    Guid? ReservationId,
    ReservationAvailabilityChannel Channel)
    : IQuery<IReadOnlyList<ReservationAvailableSlotResponse>>;
public sealed record GetReservationBookingOptionsQuery(
    Guid PracticeId,
    DateOnly Date,
    TimeOnly Time,
    ReservationAvailabilityChannel Channel)
    : IQuery<ReservationBookingOptionsResponse>;

internal sealed class ReservationMutationValidator : AbstractValidator<CreateReservationCommand>
{
    public ReservationMutationValidator()
    {
        RuleFor(item => item.PatientId).NotEmpty();
        RuleFor(item => item.DoctorPracticeId).NotEmpty();
        RuleFor(item => item.BusinessDate).NotEmpty();
        RuleFor(item => item.SegmentId).NotEmpty();
        RuleFor(item => item.VisitTypeId).NotEmpty();
        RuleFor(item => item.BookingNote).MaximumLength(ReservationPolicy.BookingNoteMaxLength);
    }
}

internal sealed class CreatePracticeReservationCommandValidator : AbstractValidator<CreatePracticeReservationCommand>
{
    public CreatePracticeReservationCommandValidator()
    {
        RuleFor(item => item.PracticeId).NotEmpty();
        RuleFor(item => item.PatientId).NotEmpty();
        RuleFor(item => item.SegmentId).NotEmpty();
        RuleFor(item => item.VisitTypeId).NotEmpty();
        RuleFor(item => item.BookingNote).MaximumLength(ReservationPolicy.BookingNoteMaxLength);
    }
}

internal sealed class ReservationRowVersionValidator : AbstractValidator<CancelMineReservationCommand>
{
    public ReservationRowVersionValidator()
    {
        RuleFor(item => item.ReservationId).NotEmpty();
        RuleFor(item => item.ReasonCode).NotEmpty().MaximumLength(100);
        RuleFor(item => item.Comment).MaximumLength(ReservationPolicy.ReasonMaxLength);
        RuleFor(item => item.RowVersion).Must(RowVersionCodec.IsValid);
    }
}

internal sealed class CancelPracticeReservationCommandValidator
    : AbstractValidator<CancelPracticeReservationCommand>
{
    public CancelPracticeReservationCommandValidator()
    {
        RuleFor(item => item.PracticeId).NotEmpty();
        RuleFor(item => item.ReservationId).NotEmpty();
        RuleFor(item => item.ReasonCode).NotEmpty().MaximumLength(100);
        RuleFor(item => item.Comment).MaximumLength(ReservationPolicy.ReasonMaxLength);
        RuleFor(item => item.RowVersion).Must(RowVersionCodec.IsValid);
    }
}

internal sealed class RescheduleMineReservationCommandValidator
    : AbstractValidator<RescheduleMineReservationCommand>
{
    public RescheduleMineReservationCommandValidator()
    {
        RuleFor(item => item.ReservationId).NotEmpty();
        RuleFor(item => item.BusinessDate).NotEmpty();
        RuleFor(item => item.RowVersion).Must(RowVersionCodec.IsValid);
    }
}

internal sealed class ReschedulePracticeReservationCommandValidator
    : AbstractValidator<ReschedulePracticeReservationCommand>
{
    public ReschedulePracticeReservationCommandValidator()
    {
        RuleFor(item => item.PracticeId).NotEmpty();
        RuleFor(item => item.ReservationId).NotEmpty();
        RuleFor(item => item.BusinessDate).NotEmpty();
        RuleFor(item => item.Reason).MaximumLength(ReservationPolicy.ReasonMaxLength);
        RuleFor(item => item.RowVersion).Must(RowVersionCodec.IsValid);
    }
}

internal sealed class RestorePracticeNoShowReservationCommandValidator
    : AbstractValidator<RestorePracticeNoShowReservationCommand>
{
    public RestorePracticeNoShowReservationCommandValidator()
    {
        RuleFor(item => item.PracticeId).NotEmpty();
        RuleFor(item => item.ReservationId).NotEmpty();
        RuleFor(item => item.RowVersion).Must(RowVersionCodec.IsValid);
    }
}

internal sealed class ListMineReservationsQueryValidator : AbstractValidator<ListMineReservationsQuery>
{
    public ListMineReservationsQueryValidator()
    {
        RuleFor(item => item.View).Must(value =>
            string.Equals(value, "Upcoming", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "History", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "All", StringComparison.OrdinalIgnoreCase));
        RuleFor(item => item.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(item => item.PageSize).InclusiveBetween(1, 100);
        RuleFor(item => item).Must(item => !item.FromDate.HasValue || !item.ToDate.HasValue ||
                                              item.FromDate.Value <= item.ToDate.Value);
    }
}

internal sealed class ListPracticeReservationsQueryValidator
    : AbstractValidator<ListPracticeReservationsQuery>
{
    public ListPracticeReservationsQueryValidator()
    {
        RuleFor(item => item.PracticeId).NotEmpty();
        RuleFor(item => item.Search).MaximumLength(200);
        RuleFor(item => item.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(item => item.PageSize).InclusiveBetween(1, 100);
        RuleFor(item => item).Must(item => !item.FromDate.HasValue || !item.ToDate.HasValue ||
                                              item.FromDate.Value <= item.ToDate.Value);
    }
}

internal sealed class ListAdministrativeReservationsQueryValidator
    : AbstractValidator<ListAdministrativeReservationsQuery>
{
    public ListAdministrativeReservationsQueryValidator()
    {
        RuleFor(item => item.Search).MaximumLength(200);
        RuleFor(item => item.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(item => item.PageSize).InclusiveBetween(1, 100);
        RuleFor(item => item).Must(item => !item.FromDate.HasValue || !item.ToDate.HasValue ||
                                              item.FromDate.Value <= item.ToDate.Value);
    }
}

internal sealed class GetReservationMetadataQueryHandler
    : IQueryHandler<GetReservationMetadataQuery, ReservationMetadataResponse>
{
    public Task<Result<ReservationMetadataResponse>> Handle(
        GetReservationMetadataQuery request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Result<ReservationMetadataResponse>.Ok(ReservationMetadata.Create()));
    }
}

internal sealed class ListBookablePatientsQueryHandler(ReservationApplicationService service)
    : IQueryHandler<ListBookablePatientsQuery, IReadOnlyList<BookablePatientResponse>>
{
    public Task<Result<IReadOnlyList<BookablePatientResponse>>> Handle(
        ListBookablePatientsQuery request,
        CancellationToken cancellationToken)
        => service.ListBookablePatientsAsync(cancellationToken);
}

internal sealed class CreateReservationCommandHandler(ReservationApplicationService service)
    : ICommandHandler<CreateReservationCommand, ReservationDetailsResponse>
{
    public Task<Result<ReservationDetailsResponse>> Handle(
        CreateReservationCommand request,
        CancellationToken cancellationToken)
        => service.CreateAsync(
            request.PatientId,
            request.DoctorPracticeId,
            request.BusinessDate,
            request.SlotStartTime,
            request.SegmentId,
            request.VisitTypeId,
            request.BookingNote,
            request.IdempotencyKey,
            ReservationOperationScope.Patient,
            cancellationToken);
}

internal sealed class CreatePracticeReservationCommandHandler(ReservationApplicationService service)
    : ICommandHandler<CreatePracticeReservationCommand, ReservationDetailsResponse>
{
    public Task<Result<ReservationDetailsResponse>> Handle(
        CreatePracticeReservationCommand request,
        CancellationToken cancellationToken)
        => service.CreateAsync(
            request.PatientId,
            request.PracticeId,
            request.BusinessDate,
            request.SlotStartTime,
            request.SegmentId,
            request.VisitTypeId,
            request.BookingNote,
            request.IdempotencyKey,
            ReservationOperationScope.Reception,
            cancellationToken);
}

internal sealed class CancelMineReservationCommandHandler(ReservationApplicationService service)
    : ICommandHandler<CancelMineReservationCommand, ReservationDetailsResponse>
{
    public Task<Result<ReservationDetailsResponse>> Handle(
        CancelMineReservationCommand request,
        CancellationToken cancellationToken)
        => service.CancelAsync(
            request.ReservationId,
            null,
            request.ReasonCode,
            request.Comment,
            request.RowVersion,
            request.IdempotencyKey,
            ReservationOperationScope.Patient,
            cancellationToken);
}

internal sealed class CancelPracticeReservationCommandHandler(ReservationApplicationService service)
    : ICommandHandler<CancelPracticeReservationCommand, ReservationDetailsResponse>
{
    public Task<Result<ReservationDetailsResponse>> Handle(
        CancelPracticeReservationCommand request,
        CancellationToken cancellationToken)
        => service.CancelAsync(
            request.ReservationId,
            request.PracticeId,
            request.ReasonCode,
            request.Comment,
            request.RowVersion,
            request.IdempotencyKey,
            request.IsDoctor ? ReservationOperationScope.Doctor : ReservationOperationScope.Reception,
            cancellationToken);
}

internal sealed class RescheduleMineReservationCommandHandler(ReservationApplicationService service)
    : ICommandHandler<RescheduleMineReservationCommand, ReservationDetailsResponse>
{
    public Task<Result<ReservationDetailsResponse>> Handle(
        RescheduleMineReservationCommand request,
        CancellationToken cancellationToken)
        => service.RescheduleAsync(
            request.ReservationId,
            null,
            request.BusinessDate,
            request.SlotStartTime,
            true,
            null,
            request.RowVersion,
            request.IdempotencyKey,
            ReservationOperationScope.Patient,
            cancellationToken);
}

internal sealed class ReschedulePracticeReservationCommandHandler(ReservationApplicationService service)
    : ICommandHandler<ReschedulePracticeReservationCommand, ReservationDetailsResponse>
{
    public Task<Result<ReservationDetailsResponse>> Handle(
        ReschedulePracticeReservationCommand request,
        CancellationToken cancellationToken)
        => service.RescheduleAsync(
            request.ReservationId,
            request.PracticeId,
            request.BusinessDate,
            request.SlotStartTime,
            request.PatientConsentConfirmed,
            request.Reason,
            request.RowVersion,
            request.IdempotencyKey,
            request.IsDoctor ? ReservationOperationScope.Doctor : ReservationOperationScope.Reception,
            cancellationToken);
}

internal sealed class RestorePracticeNoShowReservationCommandHandler(ReservationApplicationService service)
    : ICommandHandler<RestorePracticeNoShowReservationCommand, ReservationDetailsResponse>
{
    public Task<Result<ReservationDetailsResponse>> Handle(
        RestorePracticeNoShowReservationCommand request,
        CancellationToken cancellationToken)
        => service.RestoreNoShowAsync(
            request.ReservationId,
            request.PracticeId,
            request.RowVersion,
            request.IdempotencyKey,
            cancellationToken);
}

internal sealed class ExpireReservationCommandHandler(
    IWaslaDataStore dataStore,
    IDateTimeProvider clock,
    IReservationProjectionInvalidationOutbox projectionOutbox)
    : ICommandHandler<ExpireReservationCommand>
{
    public async Task<Result> Handle(
        ExpireReservationCommand request,
        CancellationToken cancellationToken)
    {
        var reservation = await dataStore.FindReservationAsync(request.ReservationId, cancellationToken);
        if (reservation is null)
        {
            return Result.Fail(ReservationErrors.NotFound);
        }

        var configuration = await dataStore.FindDoctorPracticeConfigurationAsync(
            reservation.DoctorPracticeId, cancellationToken);
        var periods = await dataStore.ListDoctorPracticeSchedulePeriodsAsync(
            reservation.DoctorPracticeId, cancellationToken);
        var exceptions = await dataStore.ListDoctorPracticeScheduleExceptionsAsync(
            reservation.DoctorPracticeId, cancellationToken);
        if (configuration is null)
        {
            return Result.Fail(ReservationErrors.InvalidState);
        }

        var effective = DoctorPracticeAvailabilityCalculator.GetEffectiveWorkingPeriods(
            reservation.BusinessDate, periods, exceptions);
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(reservation.TimeZoneIdSnapshot);
        var localNow = TimeZoneInfo.ConvertTime(new DateTimeOffset(clock.UtcNow), timeZone);
        if (effective.Count == 0 || localNow.DateTime <
            reservation.BusinessDate.ToDateTime(effective.Max(item => item.EndTime)))
        {
            return Result.Fail(ReservationErrors.InvalidState);
        }

        var result = reservation.Expire(clock.UtcNow);
        if (result.IsSuccess)
        {
            await ReservationProjectionInvalidations.QueueAsync(
                projectionOutbox, reservation, "expired", refreshAvailability: true, cancellationToken);
            await dataStore.SaveChangesAsync(cancellationToken);
        }

        return result;
    }
}

internal static class ReservationProjectionInvalidations
{
    public static Task QueueAsync(
        IReservationProjectionInvalidationOutbox outbox,
        Reservation reservation,
        string eventName,
        bool refreshAvailability,
        CancellationToken cancellationToken)
        => outbox.QueueAsync(new QueueReservationProjectionInvalidation(
            $"reservation-{eventName}:{reservation.Id:N}:{reservation.History.Last().Id:N}",
            reservation.DoctorPracticeId,
            reservation.DoctorId,
            refreshAvailability,
            RefreshPopularity: true), cancellationToken);
}

internal enum ReservationOperationScope
{
    Patient,
    Reception,
    Doctor,
    Administrative
}

internal sealed record ReservationActorAccess(
    Guid ActorApplicationUserId,
    ReservationBookingSource BookingSource,
    ReservationActionInitiator ActionInitiator,
    string? RelationshipSnapshot,
    bool IsSelf);

internal sealed record ResolvedReservationSlot(
    DateTime StartUtc,
    DateTime LocalDateTime,
    int DurationMinutes,
    TimeZoneInfo TimeZone,
    IReadOnlyList<PracticeWorkingPeriod> EffectivePeriods,
    int DailyCapacity);

internal sealed class ReservationApplicationService(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    IPatientAccessPolicy patientAccessPolicy,
    IReceptionPracticeAuthorizationService receptionAuthorization,
    IEmailOutbox emailOutbox,
    IPracticeReservationOccupancyReader occupancyReader,
    IReservationProjectionInvalidationOutbox projectionOutbox,
    IReservationNotificationRecipientResolver recipientResolver,
    IReservationReferenceGenerator referenceGenerator)
{
    public async Task<Result<IReadOnlyList<BookablePatientResponse>>> ListBookablePatientsAsync(
        CancellationToken cancellationToken)
    {
        var accessible = await ResolveAccessiblePatientsAsync(
            PermissionNames.ReservationsCreateOwn,
            PermissionNames.ReservationsCreateDependents,
            cancellationToken);
        return accessible.IsFailure
            ? Result<IReadOnlyList<BookablePatientResponse>>.Fail(accessible.Errors)
            : Result<IReadOnlyList<BookablePatientResponse>>.Ok(accessible.Value);
    }

    public async Task<Result<ReservationDetailsResponse>> CreateAsync(
        Guid patientId,
        Guid practiceId,
        DateOnly businessDate,
        TimeOnly slotStartTime,
        Guid segmentId,
        Guid visitTypeId,
        string? bookingNote,
        string idempotencyKey,
        ReservationOperationScope scope,
        CancellationToken cancellationToken)
    {
        var validatedKey = ValidateIdempotencyKey(idempotencyKey);
        if (validatedKey.IsFailure)
        {
            return Result<ReservationDetailsResponse>.Fail(validatedKey.Errors);
        }

        idempotencyKey = validatedKey.Value;
        var actor = await ResolveCreateActorAsync(patientId, practiceId, scope, cancellationToken);
        if (actor.IsFailure)
        {
            return Result<ReservationDetailsResponse>.Fail(actor.Errors);
        }

        var context = await LoadBookingContextAsync(practiceId, segmentId, visitTypeId, cancellationToken);
        if (context.IsFailure)
        {
            return Result<ReservationDetailsResponse>.Fail(context.Errors);
        }

        var (practice, doctor, configuration, segment, visitType, price) = context.Value;
        if (scope == ReservationOperationScope.Patient && !configuration.AllowOnlineBooking)
        {
            return Result<ReservationDetailsResponse>.Fail(ReservationErrors.OnlineBookingDisabled);
        }

        var slot = await ResolveSlotAsync(practiceId, configuration, businessDate, slotStartTime, cancellationToken);
        if (slot.IsFailure)
        {
            return Result<ReservationDetailsResponse>.Fail(slot.Errors);
        }

        await dataStore.AcquireReservationLocksAsync(patientId, practiceId, businessDate, cancellationToken);
        await dataStore.AcquireReservationIdempotencyLockAsync(
            actor.Value.ActorApplicationUserId, "Create", idempotencyKey, cancellationToken);
        var fingerprint = Fingerprint(
            patientId, practiceId, businessDate, slotStartTime, segmentId, visitTypeId, bookingNote);
        var idempotency = await BeginIdempotentOperationAsync(
            actor.Value.ActorApplicationUserId,
            "Create",
            idempotencyKey,
            fingerprint,
            cancellationToken);
        if (idempotency.IsFailure)
        {
            return Result<ReservationDetailsResponse>.Fail(idempotency.Errors);
        }

        if (idempotency.Value.ExistingReservationId is { } existingId)
        {
            return await GetDetailsForActorAsync(existingId, scope, practiceId, includeBookingNote: true, cancellationToken);
        }

        var conflicts = await ValidateCapacityAndConflictsAsync(
            patientId,
            doctor.Id,
            practiceId,
            segment,
            businessDate,
            slot.Value,
            configuration,
            excludingReservationId: null,
            cancellationToken);
        if (conflicts.IsFailure)
        {
            return Result<ReservationDetailsResponse>.Fail(conflicts.Errors);
        }

        var reference = await CreateUniqueReferenceAsync(cancellationToken);
        if (reference is null)
        {
            return Result<ReservationDetailsResponse>.Fail(ReservationErrors.ReferenceConflict);
        }

        var created = Reservation.Create(new ReservationCreationSnapshot(
            Guid.NewGuid(),
            reference,
            doctor.Id,
            practice.Id,
            patientId,
            segment.Id,
            visitType.Id,
            actor.Value.BookingSource,
            actor.Value.ActorApplicationUserId,
            actor.Value.RelationshipSnapshot,
            slot.Value.StartUtc,
            slot.Value.LocalDateTime,
            businessDate,
            configuration.TimeZoneId,
            slot.Value.DurationMinutes,
            segment.NameAr,
            segment.NameEn,
            segment.Priority,
            visitType.Type.ToString(),
            visitType.NameAr,
            visitType.NameEn,
            price.Price,
            bookingNote,
            clock.UtcNow));
        if (created.IsFailure)
        {
            return Result<ReservationDetailsResponse>.Fail(created.Errors);
        }

        dataStore.Add(created.Value);
        idempotency.Value.Record!.Complete(created.Value.Id, created.Value.ReservationReference, clock.UtcNow);
        await QueueNotificationAsync(created.Value, "created", cancellationToken);
        await QueueProjectionInvalidationAsync(created.Value, "created", cancellationToken);
        await dataStore.SaveChangesAsync(cancellationToken);
        return await GetDetailsForActorAsync(
            created.Value.Id, scope, practiceId, includeBookingNote: true, cancellationToken);
    }

    public async Task<Result<ReservationDetailsResponse>> CancelAsync(
        Guid reservationId,
        Guid? routePracticeId,
        string reasonCode,
        string? comment,
        string rowVersion,
        string idempotencyKey,
        ReservationOperationScope scope,
        CancellationToken cancellationToken)
    {
        var validatedKey = ValidateIdempotencyKey(idempotencyKey);
        if (validatedKey.IsFailure)
        {
            return Result<ReservationDetailsResponse>.Fail(validatedKey.Errors);
        }

        idempotencyKey = validatedKey.Value;
        var reservation = await dataStore.FindReservationAsync(reservationId, cancellationToken);
        if (reservation is null || routePracticeId.HasValue && reservation.DoctorPracticeId != routePracticeId.Value)
        {
            return Result<ReservationDetailsResponse>.Fail(ReservationErrors.NotFound);
        }

        var actor = await AuthorizeExistingAsync(
            reservation,
            scope,
            PermissionNames.PracticeReservationsCancel,
            PermissionNames.DoctorPracticeReservationsCancelOwn,
            PermissionNames.ReservationsCancelOwn,
            PermissionNames.ReservationsCancelDependents,
            cancellationToken);
        if (actor.IsFailure)
        {
            return Result<ReservationDetailsResponse>.Fail(actor.Errors);
        }

        var configuration = await dataStore.FindDoctorPracticeConfigurationAsync(
            reservation.DoctorPracticeId, cancellationToken);
        if (configuration is null)
        {
            return Result<ReservationDetailsResponse>.Fail(ReservationErrors.InvalidState);
        }

        await dataStore.AcquireReservationLocksAsync(
            reservation.PatientId, reservation.DoctorPracticeId, reservation.BusinessDate, cancellationToken);
        await dataStore.AcquireReservationIdempotencyLockAsync(
            actor.Value.ActorApplicationUserId, "Cancel", idempotencyKey, cancellationToken);
        var fingerprint = Fingerprint(reservationId, reasonCode, comment, rowVersion);
        var idempotency = await BeginIdempotentOperationAsync(
            actor.Value.ActorApplicationUserId, "Cancel", idempotencyKey, fingerprint, cancellationToken);
        if (idempotency.IsFailure)
        {
            return Result<ReservationDetailsResponse>.Fail(idempotency.Errors);
        }

        if (idempotency.Value.ExistingReservationId is { } existingId)
        {
            return await GetDetailsForActorAsync(
                existingId, scope, reservation.DoctorPracticeId, includeBookingNote: true, cancellationToken);
        }

        var supplied = VerifyRowVersion(reservation, rowVersion);
        if (supplied.IsFailure)
        {
            return Result<ReservationDetailsResponse>.Fail(supplied.Errors);
        }

        var patientAction = scope == ReservationOperationScope.Patient;
        if (patientAction && clock.UtcNow > reservation.ScheduledStartUtc
                .AddMinutes(-configuration.PatientSelfCancellationCutoffMinutes))
        {
            return Result<ReservationDetailsResponse>.Fail(ReservationErrors.CancellationCutoffReached);
        }

        var allowedReasons = patientAction
            ? ReservationCancellationReasons.PatientReasons
            : ReservationCancellationReasons.ProviderReasons;
        if (!allowedReasons.Contains(reasonCode) ||
            string.Equals(reasonCode, ReservationCancellationReasons.Other, StringComparison.Ordinal) &&
            string.IsNullOrWhiteSpace(comment))
        {
            return Result<ReservationDetailsResponse>.Fail(ReservationErrors.InvalidState);
        }

        var cancellationInitiator = actor.Value.ActionInitiator switch
        {
            ReservationActionInitiator.Patient => ReservationCancellationInitiator.Patient,
            ReservationActionInitiator.Guardian => ReservationCancellationInitiator.Guardian,
            ReservationActionInitiator.Reception => ReservationCancellationInitiator.Reception,
            ReservationActionInitiator.Doctor => ReservationCancellationInitiator.Doctor,
            _ => ReservationCancellationInitiator.System
        };
        var transition = reservation.Cancel(
            cancellationInitiator,
            actor.Value.ActionInitiator,
            actor.Value.ActorApplicationUserId,
            reasonCode,
            comment,
            clock.UtcNow);
        if (transition.IsFailure)
        {
            return Result<ReservationDetailsResponse>.Fail(transition.Errors);
        }

        dataStore.SetOriginalRowVersion(reservation, supplied.Value);
        idempotency.Value.Record!.Complete(reservation.Id, reservation.ReservationReference, clock.UtcNow);
        await QueueNotificationAsync(reservation, "cancelled", cancellationToken);
        await QueueProjectionInvalidationAsync(reservation, "cancelled", cancellationToken);
        await dataStore.SaveChangesAsync(cancellationToken);
        return await GetDetailsForActorAsync(
            reservation.Id, scope, reservation.DoctorPracticeId, includeBookingNote: true, cancellationToken);
    }

    public async Task<Result<ReservationDetailsResponse>> RescheduleAsync(
        Guid reservationId,
        Guid? routePracticeId,
        DateOnly businessDate,
        TimeOnly slotStartTime,
        bool patientConsentConfirmed,
        string? reason,
        string rowVersion,
        string idempotencyKey,
        ReservationOperationScope scope,
        CancellationToken cancellationToken)
    {
        var validatedKey = ValidateIdempotencyKey(idempotencyKey);
        if (validatedKey.IsFailure)
        {
            return Result<ReservationDetailsResponse>.Fail(validatedKey.Errors);
        }

        idempotencyKey = validatedKey.Value;
        var reservation = await dataStore.FindReservationAsync(reservationId, cancellationToken);
        if (reservation is null || routePracticeId.HasValue && reservation.DoctorPracticeId != routePracticeId.Value)
        {
            return Result<ReservationDetailsResponse>.Fail(ReservationErrors.NotFound);
        }

        var actor = await AuthorizeExistingAsync(
            reservation,
            scope,
            PermissionNames.PracticeReservationsReschedule,
            PermissionNames.DoctorPracticeReservationsRescheduleOwn,
            PermissionNames.ReservationsRescheduleOwn,
            PermissionNames.ReservationsRescheduleDependents,
            cancellationToken);
        if (actor.IsFailure)
        {
            return Result<ReservationDetailsResponse>.Fail(actor.Errors);
        }

        var catalog = await LoadBookingContextAsync(
            reservation.DoctorPracticeId,
            reservation.SegmentId,
            reservation.VisitTypeId,
            cancellationToken);
        if (catalog.IsFailure)
        {
            return Result<ReservationDetailsResponse>.Fail(ReservationErrors.CurrentCatalogInvalid);
        }

        var (_, _, configuration, segment, _, _) = catalog.Value;
        if (scope == ReservationOperationScope.Patient && !configuration.AllowOnlineBooking)
        {
            return Result<ReservationDetailsResponse>.Fail(ReservationErrors.OnlineBookingDisabled);
        }

        var targetSlot = await ResolveSlotAsync(
            reservation.DoctorPracticeId, configuration, businessDate, slotStartTime, cancellationToken);
        if (targetSlot.IsFailure)
        {
            return Result<ReservationDetailsResponse>.Fail(targetSlot.Errors);
        }

        await dataStore.AcquireReservationLocksAsync(
            reservation.PatientId, reservation.DoctorPracticeId, businessDate, cancellationToken);
        await dataStore.AcquireReservationIdempotencyLockAsync(
            actor.Value.ActorApplicationUserId, "Reschedule", idempotencyKey, cancellationToken);
        var fingerprint = Fingerprint(
            reservationId, businessDate, slotStartTime, patientConsentConfirmed, reason, rowVersion);
        var idempotency = await BeginIdempotentOperationAsync(
            actor.Value.ActorApplicationUserId, "Reschedule", idempotencyKey, fingerprint, cancellationToken);
        if (idempotency.IsFailure)
        {
            return Result<ReservationDetailsResponse>.Fail(idempotency.Errors);
        }

        if (idempotency.Value.ExistingReservationId is { } existingId)
        {
            return await GetDetailsForActorAsync(
                existingId, scope, reservation.DoctorPracticeId, includeBookingNote: true, cancellationToken);
        }

        var supplied = VerifyRowVersion(reservation, rowVersion);
        if (supplied.IsFailure)
        {
            return Result<ReservationDetailsResponse>.Fail(supplied.Errors);
        }

        if (scope == ReservationOperationScope.Patient)
        {
            if (clock.UtcNow > reservation.ScheduledStartUtc
                    .AddMinutes(-configuration.PatientSelfCancellationCutoffMinutes) ||
                reservation.IsLate(clock.UtcNow, configuration.CheckInGracePeriodMinutes))
            {
                return Result<ReservationDetailsResponse>.Fail(ReservationErrors.RescheduleCutoffReached);
            }

            if (reservation.PatientInitiatedRescheduleCount >= ReservationPolicy.MaxPatientReschedulesPerReservation)
            {
                return Result<ReservationDetailsResponse>.Fail(ReservationErrors.RescheduleLimitReached);
            }
        }
        else if (!patientConsentConfirmed || string.IsNullOrWhiteSpace(reason))
        {
            return Result<ReservationDetailsResponse>.Fail(ReservationErrors.PatientConsentRequired);
        }

        var capacity = await ValidateCapacityAndConflictsAsync(
            reservation.PatientId,
            reservation.DoctorId,
            reservation.DoctorPracticeId,
            segment,
            businessDate,
            targetSlot.Value,
            configuration,
            reservation.Id,
            cancellationToken);
        if (capacity.IsFailure)
        {
            return Result<ReservationDetailsResponse>.Fail(capacity.Errors);
        }

        var transition = reservation.Reschedule(
            targetSlot.Value.StartUtc,
            targetSlot.Value.LocalDateTime,
            businessDate,
            targetSlot.Value.DurationMinutes,
            configuration.TimeZoneId,
            actor.Value.ActionInitiator,
            actor.Value.ActorApplicationUserId,
            patientConsentConfirmed,
            reason,
            clock.UtcNow);
        if (transition.IsFailure)
        {
            return Result<ReservationDetailsResponse>.Fail(transition.Errors);
        }

        dataStore.SetOriginalRowVersion(reservation, supplied.Value);
        idempotency.Value.Record!.Complete(reservation.Id, reservation.ReservationReference, clock.UtcNow);
        await QueueNotificationAsync(reservation, "rescheduled", cancellationToken);
        await QueueProjectionInvalidationAsync(reservation, "rescheduled", cancellationToken);
        await dataStore.SaveChangesAsync(cancellationToken);
        return await GetDetailsForActorAsync(
            reservation.Id, scope, reservation.DoctorPracticeId, includeBookingNote: true, cancellationToken);
    }

    public async Task<Result<ReservationDetailsResponse>> RestoreNoShowAsync(
        Guid reservationId,
        Guid practiceId,
        string rowVersion,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var validatedKey = ValidateIdempotencyKey(idempotencyKey);
        if (validatedKey.IsFailure)
        {
            return Result<ReservationDetailsResponse>.Fail(validatedKey.Errors);
        }

        idempotencyKey = validatedKey.Value;
        var reservation = await dataStore.FindReservationAsync(reservationId, cancellationToken);
        if (reservation is null || reservation.DoctorPracticeId != practiceId)
        {
            return Result<ReservationDetailsResponse>.Fail(ReservationErrors.NotFound);
        }

        var actor = await AuthorizeExistingAsync(
            reservation,
            ReservationOperationScope.Reception,
            PermissionNames.PracticeReservationsRestoreNoShow,
            PermissionNames.DoctorPracticeReservationsRescheduleOwn,
            PermissionNames.ReservationsRescheduleOwn,
            PermissionNames.ReservationsRescheduleDependents,
            cancellationToken);
        if (actor.IsFailure)
        {
            return Result<ReservationDetailsResponse>.Fail(actor.Errors);
        }

        var practice = await dataStore.FindDoctorPracticeAsync(practiceId, cancellationToken);
        var configuration = await dataStore.FindDoctorPracticeConfigurationAsync(practiceId, cancellationToken);
        var segment = await dataStore.FindDoctorPracticeSegmentAsync(reservation.SegmentId, cancellationToken);
        if (practice is null || configuration is null || segment is null)
        {
            return Result<ReservationDetailsResponse>.Fail(ReservationErrors.NoShowRestoreNotAllowed);
        }

        var restoreEligibility = await EvaluateRestoreEligibilityAsync(
            reservation, practice, configuration, segment, includeCapacity: false, cancellationToken);
        if (restoreEligibility.IsFailure)
        {
            return Result<ReservationDetailsResponse>.Fail(restoreEligibility.Errors);
        }

        await dataStore.AcquireReservationLocksAsync(
            reservation.PatientId, practiceId, reservation.BusinessDate, cancellationToken);
        await dataStore.AcquireReservationIdempotencyLockAsync(
            actor.Value.ActorApplicationUserId, "RestoreNoShow", idempotencyKey, cancellationToken);
        var fingerprint = Fingerprint(reservationId, rowVersion);
        var idempotency = await BeginIdempotentOperationAsync(
            actor.Value.ActorApplicationUserId, "RestoreNoShow", idempotencyKey, fingerprint, cancellationToken);
        if (idempotency.IsFailure)
        {
            return Result<ReservationDetailsResponse>.Fail(idempotency.Errors);
        }

        if (idempotency.Value.ExistingReservationId is { } existingId)
        {
            return await GetDetailsForActorAsync(
                existingId, ReservationOperationScope.Reception, practiceId, true, cancellationToken);
        }

        var supplied = VerifyRowVersion(reservation, rowVersion);
        if (supplied.IsFailure)
        {
            return Result<ReservationDetailsResponse>.Fail(supplied.Errors);
        }

        var capacity = await ValidateCapacityAndConflictsAsync(
            reservation.PatientId,
            reservation.DoctorId,
            practiceId,
            segment,
            reservation.BusinessDate,
            restoreEligibility.Value,
            configuration,
            reservation.Id,
            cancellationToken);
        if (capacity.IsFailure)
        {
            return Result<ReservationDetailsResponse>.Fail(capacity.Errors);
        }

        var transition = reservation.RestoreFromNoShow(actor.Value.ActorApplicationUserId, clock.UtcNow);
        if (transition.IsFailure)
        {
            return Result<ReservationDetailsResponse>.Fail(transition.Errors);
        }

        dataStore.SetOriginalRowVersion(reservation, supplied.Value);
        idempotency.Value.Record!.Complete(reservation.Id, reservation.ReservationReference, clock.UtcNow);
        await QueueProjectionInvalidationAsync(reservation, "restored", cancellationToken);
        await dataStore.SaveChangesAsync(cancellationToken);
        return await GetDetailsForActorAsync(
            reservation.Id, ReservationOperationScope.Reception, practiceId, true, cancellationToken);
    }

    public async Task<Result<ReservationPagedResponse>> ListMineAsync(
        ListMineReservationsQuery request,
        CancellationToken cancellationToken)
    {
        var accessible = await ResolveAccessiblePatientsAsync(
            PermissionNames.ReservationsViewOwn,
            PermissionNames.ReservationsViewDependents,
            cancellationToken);
        if (accessible.IsFailure)
        {
            return Result<ReservationPagedResponse>.Fail(accessible.Errors);
        }

        var accessibleIds = accessible.Value.Select(item => item.PatientId).ToArray();
        if (request.PatientId.HasValue && !accessibleIds.Contains(request.PatientId.Value))
        {
            return Result<ReservationPagedResponse>.Fail(ReservationErrors.AccessDenied);
        }

        var patientIds = request.PatientId.HasValue ? [request.PatientId.Value] : accessibleIds;
        var fromDate = request.FromDate;
        var toDate = request.ToDate;
        DateTime? scheduledFromUtc = null;
        DateTime? scheduledBeforeUtc = null;
        if (string.Equals(request.View, "Upcoming", StringComparison.OrdinalIgnoreCase))
        {
            scheduledFromUtc = clock.UtcNow;
        }
        else if (string.Equals(request.View, "History", StringComparison.OrdinalIgnoreCase))
        {
            scheduledBeforeUtc = clock.UtcNow;
        }

        var records = await dataStore.ListReservationViewsAsync(
            patientIds,
            null,
            null,
            request.Status,
            null,
            fromDate,
            toDate,
            scheduledFromUtc,
            scheduledBeforeUtc,
            null,
            null,
            clock.UtcNow,
            null,
            request.PageNumber,
            request.PageSize,
            cancellationToken);
        return Result<ReservationPagedResponse>.Ok(MapPage(records, request.PageNumber, request.PageSize));
    }

    public Task<Result<ReservationDetailsResponse>> GetMineAsync(
        Guid reservationId,
        CancellationToken cancellationToken)
        => GetDetailsForActorAsync(
            reservationId, ReservationOperationScope.Patient, null, includeBookingNote: true, cancellationToken);

    public async Task<Result<ReservationPagedResponse>> ListPracticeAsync(
        ListPracticeReservationsQuery request,
        CancellationToken cancellationToken)
    {
        var scope = request.IsDoctor ? ReservationOperationScope.Doctor : ReservationOperationScope.Reception;
        var access = await AuthorizePracticeAsync(
            request.PracticeId,
            scope,
            PermissionNames.PracticeReservationsView,
            PermissionNames.DoctorPracticeReservationsViewOwn,
            cancellationToken);
        if (access.IsFailure)
        {
            return Result<ReservationPagedResponse>.Fail(access.Errors);
        }

        var records = await dataStore.ListReservationViewsAsync(
            null,
            null,
            request.PracticeId,
            request.Status,
            request.BookingSource,
            request.FromDate,
            request.ToDate,
            null,
            null,
            request.SegmentId,
            request.IsLate,
            clock.UtcNow,
            request.Search,
            request.PageNumber,
            request.PageSize,
            cancellationToken);
        return Result<ReservationPagedResponse>.Ok(
            MapPage(records, request.PageNumber, request.PageSize));
    }

    public Task<Result<ReservationDetailsResponse>> GetPracticeAsync(
        Guid practiceId,
        Guid reservationId,
        bool isDoctor,
        CancellationToken cancellationToken)
        => GetDetailsForActorAsync(
            reservationId,
            isDoctor ? ReservationOperationScope.Doctor : ReservationOperationScope.Reception,
            practiceId,
            includeBookingNote: true,
            cancellationToken);

    public async Task<Result<ReservationPagedResponse>> ListAdministrativeAsync(
        ListAdministrativeReservationsQuery request,
        CancellationToken cancellationToken)
    {
        var access = await RequirePermissionAsync(PermissionNames.ReservationsViewAdministrative, cancellationToken);
        if (access.IsFailure)
        {
            return Result<ReservationPagedResponse>.Fail(access.Errors);
        }

        var records = await dataStore.ListReservationViewsAsync(
            null,
            request.DoctorId,
            request.PracticeId,
            request.Status,
            request.BookingSource,
            request.FromDate,
            request.ToDate,
            null,
            null,
            null,
            null,
            clock.UtcNow,
            request.Search,
            request.PageNumber,
            request.PageSize,
            cancellationToken);
        return Result<ReservationPagedResponse>.Ok(MapPage(records, request.PageNumber, request.PageSize));
    }

    public async Task<Result<ReservationDetailsResponse>> GetAdministrativeAsync(
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        var access = await RequirePermissionAsync(PermissionNames.ReservationsViewAdministrative, cancellationToken);
        if (access.IsFailure)
        {
            return Result<ReservationDetailsResponse>.Fail(access.Errors);
        }

        var record = await dataStore.GetReservationViewAsync(reservationId, cancellationToken);
        return record is null
            ? Result<ReservationDetailsResponse>.Fail(ReservationErrors.NotFound)
            : Result<ReservationDetailsResponse>.Ok(await MapDetailsAsync(
                record, ReservationOperationScope.Administrative, false, cancellationToken));
    }

    public async Task<Result<ReservationFilterOptionsResponse>> GetFilterOptionsAsync(
        Guid practiceId,
        bool isDoctor,
        CancellationToken cancellationToken)
    {
        var scope = isDoctor ? ReservationOperationScope.Doctor : ReservationOperationScope.Reception;
        var access = await AuthorizePracticeAsync(
            practiceId,
            scope,
            PermissionNames.PracticeReservationsView,
            PermissionNames.DoctorPracticeReservationsViewOwn,
            cancellationToken);
        if (access.IsFailure)
        {
            return Result<ReservationFilterOptionsResponse>.Fail(access.Errors);
        }

        var segments = await dataStore.ListDoctorPracticeSegmentsAsync(practiceId, cancellationToken);
        return Result<ReservationFilterOptionsResponse>.Ok(new ReservationFilterOptionsResponse(
            ReservationMetadata.Statuses,
            ReservationMetadata.BookingSources,
            segments.Select(item => new ReservationMetadataItem(
                item.Id.ToString("D"), item.NameAr, item.NameEn ?? item.NameAr)).ToArray()));
    }

    public async Task<Result<IReadOnlyList<ReservationAvailableDateResponse>>> AvailableDatesAsync(
        Guid practiceId,
        Guid? reservationId,
        ReservationAvailabilityChannel channel,
        CancellationToken cancellationToken)
    {
        if (reservationId.HasValue)
        {
            var availability = await BuildRescheduleAvailabilityAsync(
                practiceId, reservationId.Value, channel, cancellationToken);
            return availability.IsFailure
                ? Result<IReadOnlyList<ReservationAvailableDateResponse>>.Fail(availability.Errors)
                : Result<IReadOnlyList<ReservationAvailableDateResponse>>.Ok(
                    availability.Value.Select(item => new ReservationAvailableDateResponse(
                        item.Key, item.Value.Count > 0)).ToArray());
        }

        if (channel is ReservationAvailabilityChannel.Reception or ReservationAvailabilityChannel.Doctor)
        {
            var access = await AuthorizePracticeAsync(
                practiceId,
                channel == ReservationAvailabilityChannel.Doctor
                    ? ReservationOperationScope.Doctor
                    : ReservationOperationScope.Reception,
                PermissionNames.PracticeReservationsCreate,
                PermissionNames.DoctorPracticeReservationsViewOwn,
                cancellationToken);
            if (access.IsFailure)
            {
                return Result<IReadOnlyList<ReservationAvailableDateResponse>>.Fail(access.Errors);
            }
        }

        var practice = await dataStore.FindDoctorPracticeAsync(practiceId, cancellationToken);
        var configuration = await dataStore.FindDoctorPracticeConfigurationAsync(practiceId, cancellationToken);
        if (practice is null || !practice.IsActive || configuration is null ||
            channel == ReservationAvailabilityChannel.Patient && !configuration.AllowOnlineBooking)
        {
            return Result<IReadOnlyList<ReservationAvailableDateResponse>>.Fail(
                ReservationErrors.OnlineBookingDisabled);
        }

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(configuration.TimeZoneId);
        var localNow = TimeZoneInfo.ConvertTime(new DateTimeOffset(EnsureUtc(clock.UtcNow)), timeZone);
        var today = DateOnly.FromDateTime(localNow.DateTime);
        var through = today.AddDays(ReservationPolicy.MaximumAdvanceBookingDays - 1);
        var periods = await dataStore.ListDoctorPracticeSchedulePeriodsAsync(practiceId, cancellationToken);
        var exceptions = await dataStore.ListDoctorPracticeScheduleExceptionsAsync(practiceId, cancellationToken);
        var occupancy = await occupancyReader.ReadAsync([practiceId], today, through, cancellationToken);
        var response = new List<ReservationAvailableDateResponse>(ReservationPolicy.MaximumAdvanceBookingDays);
        for (var date = today; date <= through; date = date.AddDays(1))
        {
            var generated = DoctorPracticeAvailabilityCalculator.CalculateAvailableSlotStarts(date, periods, exceptions);
            var snapshot = occupancy.GetValueOrDefault((practiceId, date), PracticeOccupancySnapshot.Empty);
            var capacity = configuration.EffectiveDailyCapacity(generated.Count);
            var hasFutureSlot = generated.Any(time =>
                (date != today || time > TimeOnly.FromDateTime(localNow.DateTime)) &&
                !snapshot.OccupiedSlots.Contains(time));
            response.Add(new ReservationAvailableDateResponse(
                date, hasFutureSlot && snapshot.TotalReservations < capacity));
        }

        return Result<IReadOnlyList<ReservationAvailableDateResponse>>.Ok(response);
    }

    public async Task<Result<IReadOnlyList<ReservationAvailableSlotResponse>>> AvailableSlotsAsync(
        Guid practiceId,
        DateOnly date,
        Guid? reservationId,
        ReservationAvailabilityChannel channel,
        CancellationToken cancellationToken)
    {
        if (reservationId.HasValue)
        {
            var availability = await BuildRescheduleAvailabilityAsync(
                practiceId, reservationId.Value, channel, cancellationToken);
            if (availability.IsFailure)
            {
                return Result<IReadOnlyList<ReservationAvailableSlotResponse>>.Fail(availability.Errors);
            }

            return availability.Value.TryGetValue(date, out var slots)
                ? Result<IReadOnlyList<ReservationAvailableSlotResponse>>.Ok(slots)
                : Result<IReadOnlyList<ReservationAvailableSlotResponse>>.Fail(
                    ReservationErrors.BookingHorizonExceeded);
        }

        var dates = await AvailableDatesAsync(
            practiceId, null, channel, cancellationToken);
        if (dates.IsFailure)
        {
            return Result<IReadOnlyList<ReservationAvailableSlotResponse>>.Fail(dates.Errors);
        }

        if (!dates.Value.Any(item => item.Date == date))
        {
            return Result<IReadOnlyList<ReservationAvailableSlotResponse>>.Fail(
                ReservationErrors.BookingHorizonExceeded);
        }

        var configuration = await dataStore.FindDoctorPracticeConfigurationAsync(practiceId, cancellationToken);
        var periods = await dataStore.ListDoctorPracticeSchedulePeriodsAsync(practiceId, cancellationToken);
        var exceptions = await dataStore.ListDoctorPracticeScheduleExceptionsAsync(practiceId, cancellationToken);
        var occupancy = await occupancyReader.ReadAsync([practiceId], date, date, cancellationToken);
        var snapshot = occupancy.GetValueOrDefault((practiceId, date), PracticeOccupancySnapshot.Empty);
        var effective = DoctorPracticeAvailabilityCalculator.GetEffectiveWorkingPeriods(date, periods, exceptions);
        var generated = effective.SelectMany(period =>
            DoctorPracticeAvailabilityCalculator.CalculateSlotStarts(period)
                .Select(time => new ReservationAvailableSlotResponse(date, time, period.SlotDurationMinutes)))
            .DistinctBy(item => item.Time)
            .OrderBy(item => item.Time)
            .ToArray();
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(configuration!.TimeZoneId);
        var localNow = TimeZoneInfo.ConvertTime(new DateTimeOffset(EnsureUtc(clock.UtcNow)), timeZone);
        var today = DateOnly.FromDateTime(localNow.DateTime);
        var result = generated.Where(item =>
                (date != today || item.Time > TimeOnly.FromDateTime(localNow.DateTime)) &&
                !snapshot.OccupiedSlots.Contains(item.Time))
            .ToArray();
        return Result<IReadOnlyList<ReservationAvailableSlotResponse>>.Ok(result);
    }

    private async Task<Result<IReadOnlyDictionary<DateOnly, IReadOnlyList<ReservationAvailableSlotResponse>>>>
        BuildRescheduleAvailabilityAsync(
            Guid practiceId,
            Guid reservationId,
            ReservationAvailabilityChannel channel,
            CancellationToken cancellationToken)
    {
        var reservation = await dataStore.FindReservationAsync(reservationId, cancellationToken);
        if (reservation is null || reservation.DoctorPracticeId != practiceId)
        {
            return Result<IReadOnlyDictionary<DateOnly, IReadOnlyList<ReservationAvailableSlotResponse>>>.Fail(
                ReservationErrors.NotFound);
        }

        var scope = channel switch
        {
            ReservationAvailabilityChannel.Reception => ReservationOperationScope.Reception,
            ReservationAvailabilityChannel.Doctor => ReservationOperationScope.Doctor,
            _ => ReservationOperationScope.Patient
        };
        var access = await AuthorizeExistingAsync(
            reservation,
            scope,
            PermissionNames.PracticeReservationsReschedule,
            PermissionNames.DoctorPracticeReservationsRescheduleOwn,
            PermissionNames.ReservationsRescheduleOwn,
            PermissionNames.ReservationsRescheduleDependents,
            cancellationToken);
        if (access.IsFailure)
        {
            return Result<IReadOnlyDictionary<DateOnly, IReadOnlyList<ReservationAvailableSlotResponse>>>.Fail(
                access.Errors);
        }

        if (reservation.Status != ReservationStatus.Active)
        {
            return Result<IReadOnlyDictionary<DateOnly, IReadOnlyList<ReservationAvailableSlotResponse>>>.Fail(
                ReservationErrors.InvalidState);
        }

        var catalog = await LoadBookingContextAsync(
            practiceId, reservation.SegmentId, reservation.VisitTypeId, cancellationToken);
        if (catalog.IsFailure)
        {
            return Result<IReadOnlyDictionary<DateOnly, IReadOnlyList<ReservationAvailableSlotResponse>>>.Fail(
                ReservationErrors.CurrentCatalogInvalid);
        }

        var (_, _, configuration, segment, _, _) = catalog.Value;
        if (scope == ReservationOperationScope.Patient)
        {
            if (!configuration.AllowOnlineBooking)
            {
                return Result<IReadOnlyDictionary<DateOnly, IReadOnlyList<ReservationAvailableSlotResponse>>>.Fail(
                    ReservationErrors.OnlineBookingDisabled);
            }

            if (clock.UtcNow > reservation.ScheduledStartUtc
                    .AddMinutes(-configuration.PatientSelfCancellationCutoffMinutes) ||
                reservation.IsLate(clock.UtcNow, configuration.CheckInGracePeriodMinutes))
            {
                return Result<IReadOnlyDictionary<DateOnly, IReadOnlyList<ReservationAvailableSlotResponse>>>.Fail(
                    ReservationErrors.RescheduleCutoffReached);
            }

            if (reservation.PatientInitiatedRescheduleCount >=
                ReservationPolicy.MaxPatientReschedulesPerReservation)
            {
                return Result<IReadOnlyDictionary<DateOnly, IReadOnlyList<ReservationAvailableSlotResponse>>>.Fail(
                    ReservationErrors.RescheduleLimitReached);
            }
        }

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(configuration.TimeZoneId);
        var localNow = TimeZoneInfo.ConvertTime(new DateTimeOffset(EnsureUtc(clock.UtcNow)), timeZone);
        var today = DateOnly.FromDateTime(localNow.DateTime);
        var through = today.AddDays(ReservationPolicy.MaximumAdvanceBookingDays - 1);
        var periods = await dataStore.ListDoctorPracticeSchedulePeriodsAsync(practiceId, cancellationToken);
        var exceptions = await dataStore.ListDoctorPracticeScheduleExceptionsAsync(practiceId, cancellationToken);
        var occupancy = await occupancyReader.ReadAsync([practiceId], today, through, cancellationToken);
        var conflicts = await dataStore.ListReservationAvailabilityConflictsAsync(
            reservation.PatientId, today, through, reservation.Id, cancellationToken);
        var segments = await dataStore.ListDoctorPracticeSegmentsAsync(practiceId, cancellationToken);
        var activeSegments = segments.Where(item => item.IsActive).ToArray();
        var hasOtherFutureConsultation = conflicts.Any(item =>
            item.Status == ReservationStatus.Active && item.DoctorId == reservation.DoctorId &&
            item.ScheduledStartUtc > clock.UtcNow &&
            item.VisitTypeCodeSnapshot == DoctorPracticeVisitTypeCode.NewConsultation.ToString());
        var result = new SortedDictionary<DateOnly, IReadOnlyList<ReservationAvailableSlotResponse>>();

        for (var date = today; date <= through; date = date.AddDays(1))
        {
            var effective = DoctorPracticeAvailabilityCalculator.GetEffectiveWorkingPeriods(
                date, periods, exceptions);
            var generated = effective.SelectMany(period =>
                    DoctorPracticeAvailabilityCalculator.CalculateSlotStarts(period)
                        .Select(time => new ReservationAvailableSlotResponse(
                            date, time, period.SlotDurationMinutes)))
                .DistinctBy(item => item.Time)
                .OrderBy(item => item.Time)
                .ToArray();
            var dayOccupancy = occupancy.GetValueOrDefault(
                (practiceId, date), PracticeOccupancySnapshot.Empty);
            var movingOnDate = reservation.BusinessDate == date;
            var occupiedCount = dayOccupancy.TotalReservations - (movingOnDate ? 1 : 0);
            var segmentCounts = dayOccupancy.SegmentReservationCounts.ToDictionary(item => item.Key, item => item.Value);
            if (movingOnDate && segmentCounts.TryGetValue(reservation.SegmentId, out var movingSegmentCount))
            {
                segmentCounts[reservation.SegmentId] = Math.Max(0, movingSegmentCount - 1);
            }

            var dailyCapacity = configuration.EffectiveDailyCapacity(generated.Length);
            var protectedUnused = activeSegments.Where(item => !item.IsDefault).Sum(item =>
                item.ProtectedCapacity(
                    segmentCounts.GetValueOrDefault(item.Id),
                    date,
                    effective,
                    new DateTimeOffset(EnsureUtc(clock.UtcNow)),
                    timeZone));
            var generalRemaining = Math.Max(0, dailyCapacity - occupiedCount - protectedUnused);
            var selectedProtected = segment.ProtectedCapacity(
                segmentCounts.GetValueOrDefault(segment.Id),
                date,
                effective,
                new DateTimeOffset(EnsureUtc(clock.UtcNow)),
                timeZone);
            var capacityAvailable = occupiedCount < dailyCapacity &&
                                    (selectedProtected > 0 || generalRemaining > 0);
            var dateBlocked = hasOtherFutureConsultation || conflicts.Any(item =>
                item.DoctorPracticeId == practiceId && item.BusinessDate == date &&
                item.Status is ReservationStatus.Active or ReservationStatus.NoShow);

            var valid = capacityAvailable && !dateBlocked
                ? generated.Where(candidate =>
                {
                    if (date == today && candidate.Time <= TimeOnly.FromDateTime(localNow.DateTime))
                    {
                        return false;
                    }

                    DateTime candidateStartUtc;
                    try
                    {
                        candidateStartUtc = TimeZoneInfo.ConvertTimeToUtc(
                            DateTime.SpecifyKind(date.ToDateTime(candidate.Time), DateTimeKind.Unspecified),
                            timeZone);
                    }
                    catch (ArgumentException)
                    {
                        return false;
                    }

                    if (candidateStartUtc == reservation.ScheduledStartUtc ||
                        dayOccupancy.OccupiedSlots.Contains(candidate.Time) &&
                        !(movingOnDate && TimeOnly.FromDateTime(
                            reservation.ScheduledLocalDateTime) == candidate.Time))
                    {
                        return false;
                    }

                    var candidateEndUtc = candidateStartUtc.AddMinutes(candidate.DurationMinutes);
                    return conflicts.Where(item => item.Status == ReservationStatus.Active).All(item =>
                        item.ScheduledStartUtc >= candidateEndUtc || candidateStartUtc >= item.ScheduledEndUtc);
                }).ToArray()
                : [];
            result[date] = valid;
        }

        return Result<IReadOnlyDictionary<DateOnly, IReadOnlyList<ReservationAvailableSlotResponse>>>.Ok(result);
    }

    public async Task<Result<ReservationBookingOptionsResponse>> BookingOptionsAsync(
        Guid practiceId,
        DateOnly date,
        TimeOnly time,
        ReservationAvailabilityChannel channel,
        CancellationToken cancellationToken)
    {
        var slots = await AvailableSlotsAsync(practiceId, date, null, channel, cancellationToken);
        if (slots.IsFailure || !slots.Value.Any(item => item.Time == time))
        {
            return Result<ReservationBookingOptionsResponse>.Fail(
                slots.IsFailure ? slots.Errors : [ReservationErrors.SlotNotAvailable]);
        }

        var segments = await dataStore.ListDoctorPracticeSegmentsAsync(practiceId, cancellationToken);
        var visitTypes = await dataStore.ListDoctorPracticeVisitTypesAsync(practiceId, cancellationToken);
        var prices = await dataStore.ListDoctorPracticePricesAsync(practiceId, cancellationToken);
        var configuration = await dataStore.FindDoctorPracticeConfigurationAsync(practiceId, cancellationToken);
        var periods = await dataStore.ListDoctorPracticeSchedulePeriodsAsync(practiceId, cancellationToken);
        var exceptions = await dataStore.ListDoctorPracticeScheduleExceptionsAsync(practiceId, cancellationToken);
        if (configuration is null)
        {
            return Result<ReservationBookingOptionsResponse>.Fail(ReservationErrors.InvalidState);
        }

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(configuration.TimeZoneId);
        var effectivePeriods = DoctorPracticeAvailabilityCalculator.GetEffectiveWorkingPeriods(
            date, periods, exceptions);
        var slotCount = effectivePeriods.Sum(item =>
            DoctorPracticeAvailabilityCalculator.CalculateSlotStarts(item).Count);
        var effectiveCapacity = configuration.EffectiveDailyCapacity(slotCount);
        var occupancy = await occupancyReader.ReadAsync([practiceId], date, date, cancellationToken);
        var dayOccupancy = occupancy.GetValueOrDefault(
            (practiceId, date), PracticeOccupancySnapshot.Empty);
        var now = new DateTimeOffset(EnsureUtc(clock.UtcNow));
        var activeSegments = segments.Where(item => item.IsActive).ToArray();
        var protectedCapacity = activeSegments
            .Where(item => !item.IsDefault)
            .Sum(item => item.ProtectedCapacity(
                dayOccupancy.SegmentReservationCounts.GetValueOrDefault(item.Id),
                date,
                effectivePeriods,
                now,
                timeZone));
        var generalRemaining = Math.Max(
            0,
            effectiveCapacity - dayOccupancy.TotalReservations - protectedCapacity);
        var consultationIds = visitTypes
            .Where(item => item.IsActive && item.Type == DoctorPracticeVisitTypeCode.NewConsultation)
            .Select(item => item.Id)
            .ToHashSet();
        var result = activeSegments
            .Where(segment =>
                segment.ReservedDailyQuota.HasValue &&
                segment.ProtectedCapacity(
                    dayOccupancy.SegmentReservationCounts.GetValueOrDefault(segment.Id),
                    date,
                    effectivePeriods,
                    now,
                    timeZone) > 0 ||
                generalRemaining > 0)
            .Select(segment => new ReservationBookingSegmentOptionResponse(
                segment.Id,
                segment.NameAr,
                segment.NameEn,
                segment.Priority,
                prices.Where(price => price.SegmentId == segment.Id && consultationIds.Contains(price.VisitTypeId))
                    .Join(visitTypes, price => price.VisitTypeId, visitType => visitType.Id,
                        (price, visitType) => new ReservationBookingPriceResponse(
                            visitType.Id,
                            visitType.Type.ToString(),
                            visitType.NameAr,
                            visitType.NameEn,
                            price.Price))
                    .ToArray()))
            .Where(item => item.VisitTypes.Count > 0)
            .ToArray();
        return Result<ReservationBookingOptionsResponse>.Ok(
            new ReservationBookingOptionsResponse(practiceId, date, time, result));
    }

    private async Task<Result<(
        DoctorPractice Practice,
        Doctor Doctor,
        DoctorPracticeConfiguration Configuration,
        DoctorPracticeSegment Segment,
        DoctorPracticeVisitType VisitType,
        DoctorPracticeSegmentVisitTypePrice Price)>> LoadBookingContextAsync(
        Guid practiceId,
        Guid segmentId,
        Guid visitTypeId,
        CancellationToken cancellationToken)
    {
        var practice = await dataStore.FindDoctorPracticeAsync(practiceId, cancellationToken);
        if (practice is null || !practice.IsActive)
        {
            return Result<(DoctorPractice, Doctor, DoctorPracticeConfiguration, DoctorPracticeSegment,
                DoctorPracticeVisitType, DoctorPracticeSegmentVisitTypePrice)>.Fail(ReservationErrors.InvalidState);
        }

        var doctor = await dataStore.FindDoctorByIdAsync(practice.DoctorId, cancellationToken);
        var doctorUser = doctor is null
            ? null
            : await dataStore.FindUserByIdAsync(doctor.ApplicationUserId, cancellationToken);
        if (doctor is null || doctor.ApprovalStatus != DoctorApprovalStatus.Approved ||
            doctorUser is null || !doctorUser.IsActive)
        {
            return Result<(DoctorPractice, Doctor, DoctorPracticeConfiguration, DoctorPracticeSegment,
                DoctorPracticeVisitType, DoctorPracticeSegmentVisitTypePrice)>.Fail(ReservationErrors.InvalidState);
        }

        var configuration = await dataStore.FindDoctorPracticeConfigurationAsync(practiceId, cancellationToken);
        var segment = await dataStore.FindDoctorPracticeSegmentAsync(segmentId, cancellationToken);
        var visitType = await dataStore.FindDoctorPracticeVisitTypeAsync(visitTypeId, cancellationToken);
        var prices = await dataStore.ListDoctorPracticePricesAsync(practiceId, cancellationToken);
        var price = prices.SingleOrDefault(item => item.SegmentId == segmentId && item.VisitTypeId == visitTypeId);
        if (configuration is null || segment is null || !segment.IsActive || segment.DoctorPracticeId != practiceId ||
            visitType is null || !visitType.IsActive || visitType.DoctorPracticeId != practiceId || price is null)
        {
            return Result<(DoctorPractice, Doctor, DoctorPracticeConfiguration, DoctorPracticeSegment,
                DoctorPracticeVisitType, DoctorPracticeSegmentVisitTypePrice)>.Fail(ReservationErrors.InvalidState);
        }

        if (visitType.Type == DoctorPracticeVisitTypeCode.FollowUp)
        {
            return Result<(DoctorPractice, Doctor, DoctorPracticeConfiguration, DoctorPracticeSegment,
                DoctorPracticeVisitType, DoctorPracticeSegmentVisitTypePrice)>.Fail(
                    ReservationErrors.FollowUpNotBookable);
        }

        return Result<(DoctorPractice, Doctor, DoctorPracticeConfiguration, DoctorPracticeSegment,
            DoctorPracticeVisitType, DoctorPracticeSegmentVisitTypePrice)>.Ok((
                practice, doctor, configuration, segment, visitType, price));
    }

    private async Task<Result<ResolvedReservationSlot>> ResolveSlotAsync(
        Guid practiceId,
        DoctorPracticeConfiguration configuration,
        DateOnly businessDate,
        TimeOnly slotStartTime,
        CancellationToken cancellationToken)
    {
        TimeZoneInfo timeZone;
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(configuration.TimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return Result<ResolvedReservationSlot>.Fail(ReservationErrors.InvalidSlot);
        }
        catch (InvalidTimeZoneException)
        {
            return Result<ResolvedReservationSlot>.Fail(ReservationErrors.InvalidSlot);
        }

        var utcNow = EnsureUtc(clock.UtcNow);
        var localNow = TimeZoneInfo.ConvertTime(new DateTimeOffset(utcNow), timeZone);
        var today = DateOnly.FromDateTime(localNow.DateTime);
        if (businessDate < today ||
            businessDate > today.AddDays(ReservationPolicy.MaximumAdvanceBookingDays - 1))
        {
            return Result<ResolvedReservationSlot>.Fail(ReservationErrors.BookingHorizonExceeded);
        }

        var localDateTime = DateTime.SpecifyKind(
            businessDate.ToDateTime(slotStartTime), DateTimeKind.Unspecified);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(localDateTime, timeZone);
        if (startUtc <= utcNow)
        {
            return Result<ResolvedReservationSlot>.Fail(ReservationErrors.InvalidSlot);
        }

        var periods = await dataStore.ListDoctorPracticeSchedulePeriodsAsync(practiceId, cancellationToken);
        var exceptions = await dataStore.ListDoctorPracticeScheduleExceptionsAsync(practiceId, cancellationToken);
        var effective = DoctorPracticeAvailabilityCalculator.GetEffectiveWorkingPeriods(
            businessDate, periods, exceptions);
        var matching = effective.FirstOrDefault(period =>
            DoctorPracticeAvailabilityCalculator.CalculateSlotStarts(period).Contains(slotStartTime));
        if (!matching.IsValid)
        {
            return Result<ResolvedReservationSlot>.Fail(ReservationErrors.InvalidSlot);
        }

        var generatedCount = effective.Sum(period =>
            DoctorPracticeAvailabilityCalculator.CalculateSlotStarts(period).Count);
        return Result<ResolvedReservationSlot>.Ok(new ResolvedReservationSlot(
            startUtc,
            localDateTime,
            matching.SlotDurationMinutes,
            timeZone,
            effective,
            configuration.EffectiveDailyCapacity(generatedCount)));
    }

    private async Task<Result> ValidateCapacityAndConflictsAsync(
        Guid patientId,
        Guid doctorId,
        Guid practiceId,
        DoctorPracticeSegment segment,
        DateOnly businessDate,
        ResolvedReservationSlot slot,
        DoctorPracticeConfiguration configuration,
        Guid? excludingReservationId,
        CancellationToken cancellationToken)
    {
        var snapshot = await dataStore.GetReservationConflictSnapshotAsync(
            patientId,
            doctorId,
            practiceId,
            segment.Id,
            businessDate,
            slot.StartUtc,
            slot.StartUtc.AddMinutes(slot.DurationMinutes),
            EnsureUtc(clock.UtcNow),
            excludingReservationId,
            cancellationToken);
        if (snapshot.SlotOccupied)
        {
            return Result.Fail(ReservationErrors.SlotNotAvailable);
        }

        if (snapshot.PatientPracticeDateConflict)
        {
            return Result.Fail(ReservationErrors.PatientPracticeDateConflict);
        }

        if (snapshot.PatientAppointmentOverlap)
        {
            return Result.Fail(ReservationErrors.PatientAppointmentOverlap);
        }

        if (snapshot.FutureConsultationAlreadyExists)
        {
            return Result.Fail(ReservationErrors.FutureConsultationAlreadyExists);
        }

        if (snapshot.SameDayNoShowExists)
        {
            return Result.Fail(ReservationErrors.SameDayNoShowBookingBlocked);
        }

        if (snapshot.ConsumedDailyCapacity >= slot.DailyCapacity)
        {
            return Result.Fail(ReservationErrors.DailyCapacityExceeded);
        }

        var segments = await dataStore.ListDoctorPracticeSegmentsAsync(practiceId, cancellationToken);
        var now = new DateTimeOffset(EnsureUtc(clock.UtcNow));
        var protectedUnused = segments
            .Where(item => item.IsActive && !item.IsDefault)
            .Sum(item => item.ProtectedCapacity(
                snapshot.SegmentConsumedCounts.GetValueOrDefault(item.Id),
                businessDate,
                slot.EffectivePeriods,
                now,
                slot.TimeZone));
        var generalRemaining = Math.Max(
            0, slot.DailyCapacity - snapshot.ConsumedDailyCapacity - protectedUnused);
        var selectedProtected = segment.ProtectedCapacity(
            snapshot.SegmentConsumedCounts.GetValueOrDefault(segment.Id),
            businessDate,
            slot.EffectivePeriods,
            now,
            slot.TimeZone);
        if (selectedProtected <= 0 && generalRemaining <= 0)
        {
            return Result.Fail(ReservationErrors.SegmentCapacityExceeded);
        }

        _ = configuration;
        return Result.Ok();
    }

    private async Task<Result<ReservationActorAccess>> ResolveCreateActorAsync(
        Guid patientId,
        Guid practiceId,
        ReservationOperationScope scope,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } actorId)
        {
            return Result<ReservationActorAccess>.Fail(ReservationErrors.AccessDenied);
        }

        if (scope == ReservationOperationScope.Reception)
        {
            var access = await receptionAuthorization.AuthorizeAsync(
                practiceId, PermissionNames.PracticeReservationsCreate, cancellationToken);
            if (access.IsFailure)
            {
                return Result<ReservationActorAccess>.Fail(ReservationErrors.AccessDenied);
            }

            return await dataStore.FindPatientByIdAsync(patientId, cancellationToken) is null
                ? Result<ReservationActorAccess>.Fail(ReservationErrors.NotFound)
                : Result<ReservationActorAccess>.Ok(new ReservationActorAccess(
                    actorId,
                    ReservationBookingSource.Reception,
                    ReservationActionInitiator.Reception,
                    null,
                    false));
        }

        return await ResolvePatientActorAsync(
            patientId,
            PermissionNames.ReservationsCreateOwn,
            PermissionNames.ReservationsCreateDependents,
            cancellationToken);
    }

    private async Task<Result<ReservationActorAccess>> ResolvePatientActorAsync(
        Guid patientId,
        string ownPermission,
        string dependentPermission,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } actorId)
        {
            return Result<ReservationActorAccess>.Fail(ReservationErrors.AccessDenied);
        }

        var snapshot = await dataStore.GetAccessSnapshotAsync(actorId, cancellationToken);
        var actorPatient = await dataStore.FindPatientAccountLinkAsync(actorId, cancellationToken);
        if (snapshot is null || !snapshot.User.IsActive || actorPatient is null)
        {
            return Result<ReservationActorAccess>.Fail(ReservationErrors.AccessDenied);
        }

        if (actorPatient.PatientId == patientId)
        {
            return snapshot.Permissions.Contains(ownPermission, StringComparer.OrdinalIgnoreCase)
                ? Result<ReservationActorAccess>.Ok(new ReservationActorAccess(
                    actorId,
                    ReservationBookingSource.Patient,
                    ReservationActionInitiator.Patient,
                    null,
                    true))
                : Result<ReservationActorAccess>.Fail(ReservationErrors.AccessDenied);
        }

        if (!snapshot.Permissions.Contains(dependentPermission, StringComparer.OrdinalIgnoreCase))
        {
            return Result<ReservationActorAccess>.Fail(ReservationErrors.AccessDenied);
        }

        var actorMember = await dataStore.FindActiveFamilyMemberByPatientIdAsync(
            actorPatient.PatientId, cancellationToken);
        var subjectPatient = await dataStore.FindPatientByIdAsync(patientId, cancellationToken);
        if (actorMember is null || subjectPatient is null)
        {
            return Result<ReservationActorAccess>.Fail(ReservationErrors.AccessDenied);
        }

        var members = await dataStore.ListActiveFamilyMembersAsync(actorMember.FamilyId, cancellationToken);
        var subject = members.SingleOrDefault(item => item.Member.PatientId == patientId)?.Member;
        var today = DateOnly.FromDateTime(clock.UtcNow);
        if (subject is null || !patientAccessPolicy.CanBookReservation(
                actorMember, subject, subjectPatient, today))
        {
            return Result<ReservationActorAccess>.Fail(ReservationErrors.AccessDenied);
        }

        return Result<ReservationActorAccess>.Ok(new ReservationActorAccess(
            actorId,
            ReservationBookingSource.FamilyMember,
            ReservationActionInitiator.Guardian,
            actorMember.Role.ToString(),
            false));
    }

    private async Task<Result<ReservationActorAccess>> AuthorizeExistingAsync(
        Reservation reservation,
        ReservationOperationScope scope,
        string receptionPermission,
        string doctorPermission,
        string patientOwnPermission,
        string patientDependentPermission,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } actorId)
        {
            return Result<ReservationActorAccess>.Fail(ReservationErrors.AccessDenied);
        }

        if (scope == ReservationOperationScope.Patient)
        {
            return await ResolvePatientActorAsync(
                reservation.PatientId,
                patientOwnPermission,
                patientDependentPermission,
                cancellationToken);
        }

        if (scope == ReservationOperationScope.Reception)
        {
            var access = await receptionAuthorization.AuthorizeAsync(
                reservation.DoctorPracticeId, receptionPermission, cancellationToken);
            return access.IsFailure
                ? Result<ReservationActorAccess>.Fail(ReservationErrors.AccessDenied)
                : Result<ReservationActorAccess>.Ok(new ReservationActorAccess(
                    actorId,
                    ReservationBookingSource.Reception,
                    ReservationActionInitiator.Reception,
                    null,
                    false));
        }

        if (scope == ReservationOperationScope.Doctor)
        {
            var permission = await RequirePermissionAsync(doctorPermission, cancellationToken);
            var doctor = await dataStore.FindDoctorByUserIdAsync(actorId, cancellationToken);
            var practice = await dataStore.FindDoctorPracticeAsync(
                reservation.DoctorPracticeId, cancellationToken);
            return permission.IsFailure || doctor is null ||
                   doctor.ApprovalStatus != DoctorApprovalStatus.Approved ||
                   practice is null || !practice.IsActive || practice.DoctorId != doctor.Id
                ? Result<ReservationActorAccess>.Fail(ReservationErrors.AccessDenied)
                : Result<ReservationActorAccess>.Ok(new ReservationActorAccess(
                    actorId,
                    ReservationBookingSource.Patient,
                    ReservationActionInitiator.Doctor,
                    null,
                    false));
        }

        return Result<ReservationActorAccess>.Fail(ReservationErrors.AccessDenied);
    }

    private async Task<Result> AuthorizePracticeAsync(
        Guid practiceId,
        ReservationOperationScope scope,
        string receptionPermission,
        string doctorPermission,
        CancellationToken cancellationToken)
    {
        if (scope == ReservationOperationScope.Reception)
        {
            var access = await receptionAuthorization.AuthorizeAsync(
                practiceId, receptionPermission, cancellationToken);
            return access.IsSuccess ? Result.Ok() : Result.Fail(ReservationErrors.AccessDenied);
        }

        if (scope == ReservationOperationScope.Doctor && currentUser.UserId is { } actorId)
        {
            var permission = await RequirePermissionAsync(doctorPermission, cancellationToken);
            var doctor = await dataStore.FindDoctorByUserIdAsync(actorId, cancellationToken);
            var practice = await dataStore.FindDoctorPracticeAsync(practiceId, cancellationToken);
            return permission.IsSuccess && doctor is not null &&
                   doctor.ApprovalStatus == DoctorApprovalStatus.Approved &&
                   practice is { IsActive: true } && practice.DoctorId == doctor.Id
                ? Result.Ok()
                : Result.Fail(ReservationErrors.AccessDenied);
        }

        return Result.Fail(ReservationErrors.AccessDenied);
    }

    private async Task<Result> RequirePermissionAsync(
        string permission,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } actorId)
        {
            return Result.Fail(ReservationErrors.AccessDenied);
        }

        var snapshot = await dataStore.GetAccessSnapshotAsync(actorId, cancellationToken);
        return snapshot is not null && snapshot.User.IsActive && !snapshot.User.IsFirstLogin &&
               (snapshot.IsRootSuperAdmin || snapshot.Permissions.Contains(
                   permission, StringComparer.OrdinalIgnoreCase))
            ? Result.Ok()
            : Result.Fail(ReservationErrors.AccessDenied);
    }

    private async Task<Result<IReadOnlyList<BookablePatientResponse>>> ResolveAccessiblePatientsAsync(
        string ownPermission,
        string dependentPermission,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } actorId)
        {
            return Result<IReadOnlyList<BookablePatientResponse>>.Fail(ReservationErrors.AccessDenied);
        }

        var snapshot = await dataStore.GetAccessSnapshotAsync(actorId, cancellationToken);
        var link = await dataStore.FindPatientAccountLinkAsync(actorId, cancellationToken);
        var patient = link is null
            ? null
            : await dataStore.FindPatientByIdAsync(link.PatientId, cancellationToken);
        if (snapshot is null || !snapshot.User.IsActive || link is null || patient is null ||
            !snapshot.Permissions.Contains(ownPermission, StringComparer.OrdinalIgnoreCase))
        {
            return Result<IReadOnlyList<BookablePatientResponse>>.Fail(ReservationErrors.AccessDenied);
        }

        var result = new List<BookablePatientResponse>
        {
            new(patient.Id, patient.NameAr, patient.NameEn, patient.DateOfBirth, patient.Gender, true, null)
        };
        if (!snapshot.Permissions.Contains(dependentPermission, StringComparer.OrdinalIgnoreCase))
        {
            return Result<IReadOnlyList<BookablePatientResponse>>.Ok(result);
        }

        var actorMember = await dataStore.FindActiveFamilyMemberByPatientIdAsync(patient.Id, cancellationToken);
        if (actorMember is null)
        {
            return Result<IReadOnlyList<BookablePatientResponse>>.Ok(result);
        }

        var members = await dataStore.ListActiveFamilyMembersAsync(actorMember.FamilyId, cancellationToken);
        var today = DateOnly.FromDateTime(clock.UtcNow);
        foreach (var member in members.Where(item => item.Member.PatientId != patient.Id))
        {
            if (patientAccessPolicy.CanBookReservation(actorMember, member.Member, member.Patient, today))
            {
                result.Add(new BookablePatientResponse(
                    member.Patient.Id,
                    member.Patient.NameAr,
                    member.Patient.NameEn,
                    member.Patient.DateOfBirth,
                    member.Patient.Gender,
                    false,
                    actorMember.Role.ToString()));
            }
        }

        return Result<IReadOnlyList<BookablePatientResponse>>.Ok(result);
    }

    private async Task<Result<ReservationDetailsResponse>> GetDetailsForActorAsync(
        Guid reservationId,
        ReservationOperationScope scope,
        Guid? routePracticeId,
        bool includeBookingNote,
        CancellationToken cancellationToken)
    {
        var record = await dataStore.GetReservationViewAsync(reservationId, cancellationToken);
        if (record is null || routePracticeId.HasValue && record.Reservation.DoctorPracticeId != routePracticeId.Value)
        {
            return Result<ReservationDetailsResponse>.Fail(ReservationErrors.NotFound);
        }

        var access = await AuthorizeExistingAsync(
            record.Reservation,
            scope,
            PermissionNames.PracticeReservationsView,
            PermissionNames.DoctorPracticeReservationsViewOwn,
            PermissionNames.ReservationsViewOwn,
            PermissionNames.ReservationsViewDependents,
            cancellationToken);
        if (access.IsFailure)
        {
            return Result<ReservationDetailsResponse>.Fail(access.Errors);
        }

        return Result<ReservationDetailsResponse>.Ok(await MapDetailsAsync(
            record, scope, includeBookingNote, cancellationToken));
    }

    private async Task<ReservationDetailsResponse> MapDetailsAsync(
        ReservationViewRecord record,
        ReservationOperationScope scope,
        bool includeBookingNote,
        CancellationToken cancellationToken)
    {
        var reservation = record.Reservation;
        var configuration = record.Configuration;
        var grace = configuration.CheckInGracePeriodMinutes;
        var cutoff = configuration.PatientSelfCancellationCutoffMinutes;
        var isLate = reservation.IsLate(clock.UtcNow, grace);
        var currentCatalog = await LoadBookingContextAsync(
            reservation.DoctorPracticeId,
            reservation.SegmentId,
            reservation.VisitTypeId,
            cancellationToken);
        var restoreEligibility = scope == ReservationOperationScope.Reception &&
                                 reservation.Status == ReservationStatus.NoShow
            ? await EvaluateRestoreEligibilityAsync(
                reservation,
                record.Practice,
                configuration,
                await dataStore.FindDoctorPracticeSegmentAsync(reservation.SegmentId, cancellationToken),
                includeCapacity: true,
                cancellationToken)
            : Result<ResolvedReservationSlot>.Fail(ReservationErrors.InvalidState);
        var capabilities = BuildCapabilities(
            reservation,
            scope,
            cutoff,
            grace,
            configuration.AllowOnlineBooking,
            currentCatalog.IsSuccess,
            restoreEligibility.IsSuccess,
            restoreEligibility.IsFailure ? restoreEligibility.Errors[0].Code : null);
        var timeline = reservation.History
            .OrderBy(item => item.OccurredOnUtc)
            .ThenBy(item => item.Id)
            .Select(item =>
            {
                var label = ReservationMetadata.HistoryLabel(item.EventType);
                return new ReservationTimelineItemResponse(
                    item.EventType.ToString(),
                    label.NameAr,
                    label.NameEn,
                    item.OccurredOnUtc,
                    item.ActionInitiator.ToString(),
                    item.PerformedByApplicationUserId,
                    item.ReasonCode,
                    item.Reason,
                    item.OldBusinessDate,
                    item.OldScheduledLocalDateTime.HasValue
                        ? TimeOnly.FromDateTime(item.OldScheduledLocalDateTime.Value)
                        : null,
                    item.NewBusinessDate,
                    item.NewScheduledLocalDateTime.HasValue
                        ? TimeOnly.FromDateTime(item.NewScheduledLocalDateTime.Value)
                        : null,
                    item.PatientConsentConfirmed);
            })
            .ToArray();
        return new ReservationDetailsResponse(
            reservation.Id,
            reservation.ReservationReference,
            reservation.Status.ToString(),
            isLate,
            new ReservationPartyResponse(record.Patient.Id, record.Patient.NameAr, record.Patient.NameEn),
            new ReservationPartyResponse(record.Doctor.Id, record.Doctor.NameAr, record.Doctor.NameEn),
            new ReservationPracticeResponse(
                record.Practice.Id, record.Practice.NameAr, record.Practice.NameEn, record.Practice.DetailedAddress),
            new ReservationAppointmentResponse(
                reservation.BusinessDate,
                TimeOnly.FromDateTime(reservation.ScheduledLocalDateTime),
                reservation.ScheduledStartUtc,
                reservation.SlotDurationMinutesSnapshot,
                reservation.TimeZoneIdSnapshot,
                reservation.ScheduledStartUtc.AddMinutes(grace)),
            new ReservationSnapshotResponse(
                reservation.SegmentId,
                reservation.SegmentNameArSnapshot,
                reservation.SegmentNameEnSnapshot,
                reservation.SegmentPrioritySnapshot,
                reservation.VisitTypeId,
                reservation.VisitTypeCodeSnapshot,
                reservation.VisitTypeNameArSnapshot,
                reservation.VisitTypeNameEnSnapshot,
                reservation.PriceSnapshot),
            reservation.BookingSource.ToString(),
            includeBookingNote ? reservation.BookingNote : null,
            reservation.CreatedOnUtc,
            reservation.LastRescheduledOnUtc,
            reservation.CancellationReasonCode,
            reservation.CancellationComment,
            reservation.CancelledOnUtc,
            capabilities,
            timeline,
            RowVersionCodec.Encode(reservation.RowVersion));
    }

    private ReservationCapabilitiesResponse BuildCapabilities(
        Reservation reservation,
        ReservationOperationScope scope,
        int cutoffMinutes,
        int graceMinutes,
        bool allowOnlineBooking,
        bool currentCatalogValid,
        bool restoreEligible,
        string? restoreBlockedReason)
    {
        var active = reservation.Status == ReservationStatus.Active;
        var patient = scope == ReservationOperationScope.Patient;
        var provider = scope is ReservationOperationScope.Reception or ReservationOperationScope.Doctor;
        var beforeCutoff = clock.UtcNow <= reservation.ScheduledStartUtc.AddMinutes(-cutoffMinutes);
        var late = reservation.IsLate(clock.UtcNow, graceMinutes);
        var canCancel = active && (provider || patient && beforeCutoff);
        var canReschedule = active && currentCatalogValid &&
            (provider || patient && allowOnlineBooking && beforeCutoff && !late &&
             reservation.PatientInitiatedRescheduleCount < ReservationPolicy.MaxPatientReschedulesPerReservation);
        var canRestore = scope == ReservationOperationScope.Reception && restoreEligible;
        return new ReservationCapabilitiesResponse(
            canCancel,
            canCancel ? null : active ? "Reservation.CancellationCutoffReached" : "Reservation.InvalidState",
            canReschedule,
            canReschedule ? null : !active ? "Reservation.InvalidState" :
                !currentCatalogValid ? "Reservation.CurrentCatalogInvalid" :
                patient && !allowOnlineBooking ? "Reservation.OnlineBookingDisabled" :
                reservation.PatientInitiatedRescheduleCount >= ReservationPolicy.MaxPatientReschedulesPerReservation
                    ? "Reservation.RescheduleLimitReached"
                    : "Reservation.RescheduleCutoffReached",
            canRestore,
            canRestore ? null : restoreBlockedReason ?? "Reservation.NoShowRestoreNotAllowed",
            patient ? reservation.ScheduledStartUtc.AddMinutes(-cutoffMinutes) : null,
            patient ? reservation.ScheduledStartUtc.AddMinutes(-cutoffMinutes) : null,
            reservation.PatientInitiatedRescheduleCount,
            Math.Max(0, ReservationPolicy.MaxPatientReschedulesPerReservation -
                        reservation.PatientInitiatedRescheduleCount));
    }

    private async Task<Result<ResolvedReservationSlot>> EvaluateRestoreEligibilityAsync(
        Reservation reservation,
        DoctorPractice practice,
        DoctorPracticeConfiguration configuration,
        DoctorPracticeSegment? segment,
        bool includeCapacity,
        CancellationToken cancellationToken)
    {
        if (reservation.Status != ReservationStatus.NoShow)
        {
            return Result<ResolvedReservationSlot>.Fail(ReservationErrors.InvalidState);
        }

        if (!practice.IsActive || segment is null || segment.DoctorPracticeId != practice.Id)
        {
            return Result<ResolvedReservationSlot>.Fail(ReservationErrors.NoShowRestoreNotAllowed);
        }

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(reservation.TimeZoneIdSnapshot);
        var localNow = TimeZoneInfo.ConvertTime(new DateTimeOffset(EnsureUtc(clock.UtcNow)), timeZone);
        var periods = await dataStore.ListDoctorPracticeSchedulePeriodsAsync(practice.Id, cancellationToken);
        var exceptions = await dataStore.ListDoctorPracticeScheduleExceptionsAsync(practice.Id, cancellationToken);
        var effective = DoctorPracticeAvailabilityCalculator.GetEffectiveWorkingPeriods(
            reservation.BusinessDate, periods, exceptions);
        if (DateOnly.FromDateTime(localNow.DateTime) != reservation.BusinessDate ||
            effective.Count == 0 ||
            localNow.TimeOfDay >= effective.Max(item => item.EndTime).ToTimeSpan())
        {
            return Result<ResolvedReservationSlot>.Fail(ReservationErrors.RestoreBusinessDateEnded);
        }

        var generatedCount = effective.Sum(item =>
            DoctorPracticeAvailabilityCalculator.CalculateSlotStarts(item).Count);
        var slot = new ResolvedReservationSlot(
            reservation.ScheduledStartUtc,
            reservation.ScheduledLocalDateTime,
            reservation.SlotDurationMinutesSnapshot,
            timeZone,
            effective,
            configuration.EffectiveDailyCapacity(generatedCount));
        if (!includeCapacity)
        {
            return Result<ResolvedReservationSlot>.Ok(slot);
        }

        var capacity = await ValidateCapacityAndConflictsAsync(
            reservation.PatientId,
            reservation.DoctorId,
            practice.Id,
            segment,
            reservation.BusinessDate,
            slot,
            configuration,
            reservation.Id,
            cancellationToken);
        return capacity.IsFailure
            ? Result<ResolvedReservationSlot>.Fail(capacity.Errors)
            : Result<ResolvedReservationSlot>.Ok(slot);
    }

    private ReservationPagedResponse MapPage(
        ReservationViewPage records,
        int pageNumber,
        int pageSize)
    {
        var items = records.Items.Select(record =>
        {
            var reservation = record.Reservation;
            var isLate = reservation.Status == ReservationStatus.Active &&
                         clock.UtcNow > reservation.ScheduledStartUtc.AddMinutes(
                             record.Configuration.CheckInGracePeriodMinutes);
            return new ReservationListItemResponse(
                reservation.Id,
                reservation.ReservationReference,
                reservation.Status.ToString(),
                isLate,
                record.Patient.Id,
                record.Patient.NameAr,
                record.Patient.NameEn,
                record.Practice.Id,
                record.Practice.NameAr,
                record.Practice.NameEn,
                reservation.BusinessDate,
                TimeOnly.FromDateTime(reservation.ScheduledLocalDateTime),
                reservation.SegmentNameArSnapshot,
                reservation.SegmentNameEnSnapshot,
                reservation.BookingSource.ToString(),
                reservation.PriceSnapshot,
                RowVersionCodec.Encode(reservation.RowVersion));
        }).ToArray();
        var summary = new ReservationListSummary(
            records.TotalCount,
            records.ActiveCount,
            records.LateCount,
            records.NoShowCount,
            records.CancelledCount,
            records.ConvertedToTicketCount,
            records.ExpiredCount);
        return new ReservationPagedResponse(items, records.TotalCount, pageNumber, pageSize, summary);
    }

    private static Result<byte[]> VerifyRowVersion(Reservation reservation, string encoded)
    {
        var supplied = RowVersionCodec.Decode(encoded);
        return supplied is not null && reservation.RowVersion.AsSpan().SequenceEqual(supplied)
            ? Result<byte[]>.Ok(supplied)
            : Result<byte[]>.Fail(ReservationErrors.ConcurrencyConflict);
    }

    private async Task<Result<IdempotencyStart>> BeginIdempotentOperationAsync(
        Guid actorApplicationUserId,
        string operation,
        string idempotencyKey,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        var normalizedKey = idempotencyKey.Trim();

        var existing = await dataStore.FindReservationIdempotencyAsync(
            actorApplicationUserId, operation, normalizedKey, cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.RequestFingerprint, fingerprint, StringComparison.Ordinal))
            {
                return Result<IdempotencyStart>.Fail(ReservationErrors.IdempotencyKeyReused);
            }

            if (existing.ReservationId.HasValue)
            {
                return Result<IdempotencyStart>.Ok(new IdempotencyStart(null, existing.ReservationId));
            }

            return Result<IdempotencyStart>.Fail(ReservationErrors.ConcurrencyConflict);
        }

        var record = new ReservationIdempotencyRecord(
            Guid.NewGuid(),
            actorApplicationUserId,
            operation,
            normalizedKey,
            fingerprint,
            clock.UtcNow);
        dataStore.Add(record);
        return Result<IdempotencyStart>.Ok(new IdempotencyStart(record, null));
    }

    private async Task<string?> CreateUniqueReferenceAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var reference = referenceGenerator.Generate();
            await dataStore.AcquireReservationReferenceLockAsync(reference, cancellationToken);
            if (!await dataStore.ReservationReferenceExistsAsync(reference, cancellationToken))
            {
                return reference;
            }
        }

        return null;
    }

    private async Task QueueNotificationAsync(
        Reservation reservation,
        string eventName,
        CancellationToken cancellationToken)
    {
        var localTime = TimeOnly.FromDateTime(reservation.ScheduledLocalDateTime);
        var subject = eventName switch
        {
            "created" => "Wasla | تم تأكيد الحجز | Reservation Confirmed",
            "rescheduled" => "Wasla | تمت إعادة جدولة الحجز | Reservation Rescheduled",
            "cancelled" => "Wasla | تم إلغاء الحجز | Reservation Cancelled",
            _ => "Wasla | تحديث الحجز | Reservation Update"
        };
        var safeReference = WebUtility.HtmlEncode(reservation.ReservationReference);
        var safeDate = WebUtility.HtmlEncode(reservation.BusinessDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        var safeTime = WebUtility.HtmlEncode(localTime.ToString("HH:mm", CultureInfo.InvariantCulture));
        var html = $"<p dir=\"rtl\">مرجع الحجز: {safeReference}<br>الموعد: {safeDate} {safeTime}</p>" +
                   $"<p>Reservation reference: {safeReference}<br>Appointment: {safeDate} {safeTime}</p>";
        var historyId = reservation.History.OrderBy(item => item.OccurredOnUtc).Last().Id;
        var resolved = await recipientResolver.ResolveAsync([reservation], cancellationToken);
        var recipients = resolved.GetValueOrDefault(reservation.Id) ?? [];

        foreach (var recipient in recipients)
        {
            await emailOutbox.QueueAsync(new QueueEmailMessage(
                $"reservation-{eventName}:{reservation.Id:N}:{historyId:N}:{Fingerprint(recipient)[..12]}",
                recipient,
                subject,
                html,
                $"{reservation.ReservationReference} - {reservation.BusinessDate:yyyy-MM-dd} {localTime:HH:mm}"),
                cancellationToken);
        }
    }

    private Task QueueProjectionInvalidationAsync(
        Reservation reservation,
        string eventName,
        CancellationToken cancellationToken)
        => projectionOutbox.QueueAsync(new QueueReservationProjectionInvalidation(
            $"reservation-{eventName}:{reservation.Id:N}:{reservation.History.Last().Id:N}",
            reservation.DoctorPracticeId,
            reservation.DoctorId,
            RefreshAvailability: true,
            RefreshPopularity: true), cancellationToken);

    private static string Fingerprint(params object?[] values)
    {
        var canonical = string.Join('\u001F', values.Select(value => value switch
        {
            null => "<null>",
            DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            TimeOnly time => time.ToString("HH:mm:ss.fffffff", CultureInfo.InvariantCulture),
            bool flag => flag ? "true" : "false",
            _ => value.ToString()?.Trim() ?? string.Empty
        }));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static Result<string> ValidateIdempotencyKey(string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Result<string>.Fail(ReservationErrors.IdempotencyKeyRequired);
        }

        var normalized = idempotencyKey.Trim();
        if (normalized.Length > 200 || normalized.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_' or '.' or ':')))
        {
            return Result<string>.Fail(ReservationErrors.IdempotencyKeyInvalid);
        }

        return Result<string>.Ok(normalized);
    }

    private static DateTime EnsureUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();

    private sealed record IdempotencyStart(
        ReservationIdempotencyRecord? Record,
        Guid? ExistingReservationId);
}

internal static class ReservationMetadata
{
    public static readonly IReadOnlyList<ReservationMetadataItem> Statuses =
    [
        new(nameof(ReservationStatus.Active), "نشط", "Active"),
        new(nameof(ReservationStatus.Cancelled), "ملغي", "Cancelled"),
        new(nameof(ReservationStatus.NoShow), "لم يحضر", "No show"),
        new(nameof(ReservationStatus.Expired), "منتهي", "Expired"),
        new(nameof(ReservationStatus.ConvertedToTicket), "تم تحويله إلى تذكرة", "Converted to ticket")
    ];

    public static readonly IReadOnlyList<ReservationMetadataItem> BookingSources =
    [
        new(nameof(ReservationBookingSource.Patient), "المريض", "Patient"),
        new(nameof(ReservationBookingSource.FamilyMember), "فرد من الأسرة", "Family member"),
        new(nameof(ReservationBookingSource.Reception), "الاستقبال", "Reception")
    ];

    public static ReservationMetadataResponse Create() => new(
        Statuses,
        BookingSources,
        [
            new(ReservationCancellationReasons.PatientChangedPlans, "تغيير خطط المريض", "Patient changed plans", false),
            new(ReservationCancellationReasons.PatientUnavailable, "المريض غير متاح", "Patient unavailable", false),
            new(ReservationCancellationReasons.DuplicateBooking, "حجز مكرر", "Duplicate booking", false),
            new(ReservationCancellationReasons.Other, "سبب آخر", "Other", true)
        ],
        [
            new(ReservationCancellationReasons.ProviderUnavailable, "مقدم الخدمة غير متاح", "Provider unavailable", false),
            new(ReservationCancellationReasons.ScheduleChanged, "تم تغيير الجدول", "Schedule changed", false),
            new(ReservationCancellationReasons.DoctorSuspended, "تم تعليق الطبيب", "Doctor suspended", false),
            new(ReservationCancellationReasons.Other, "سبب آخر", "Other", true)
        ],
        ReservationPolicy.MaximumAdvanceBookingDays,
        ReservationPolicy.MaxPatientReschedulesPerReservation);

    public static ReservationMetadataItem HistoryLabel(ReservationHistoryEventType eventType)
        => eventType switch
        {
            ReservationHistoryEventType.Created => new(eventType.ToString(), "تم إنشاء الحجز", "Reservation created"),
            ReservationHistoryEventType.Rescheduled => new(eventType.ToString(), "تمت إعادة الجدولة", "Reservation rescheduled"),
            ReservationHistoryEventType.Cancelled => new(eventType.ToString(), "تم الإلغاء", "Reservation cancelled"),
            ReservationHistoryEventType.MarkedNoShow => new(eventType.ToString(), "تم تسجيل عدم الحضور", "Marked no show"),
            ReservationHistoryEventType.RestoredFromNoShow => new(eventType.ToString(), "تمت الاستعادة من عدم الحضور", "Restored from no show"),
            ReservationHistoryEventType.Expired => new(eventType.ToString(), "انتهى الحجز", "Reservation expired"),
            ReservationHistoryEventType.ConvertedToTicket => new(eventType.ToString(), "تم التحويل إلى تذكرة", "Converted to ticket"),
            _ => new(eventType.ToString(), eventType.ToString(), eventType.ToString())
        };
}

internal sealed class ListMineReservationsQueryHandler(ReservationApplicationService service)
    : IQueryHandler<ListMineReservationsQuery, ReservationPagedResponse>
{
    public Task<Result<ReservationPagedResponse>> Handle(
        ListMineReservationsQuery request,
        CancellationToken cancellationToken)
        => service.ListMineAsync(request, cancellationToken);
}

internal sealed class GetMineReservationQueryHandler(ReservationApplicationService service)
    : IQueryHandler<GetMineReservationQuery, ReservationDetailsResponse>
{
    public Task<Result<ReservationDetailsResponse>> Handle(
        GetMineReservationQuery request,
        CancellationToken cancellationToken)
        => service.GetMineAsync(request.ReservationId, cancellationToken);
}

internal sealed class ListPracticeReservationsQueryHandler(ReservationApplicationService service)
    : IQueryHandler<ListPracticeReservationsQuery, ReservationPagedResponse>
{
    public Task<Result<ReservationPagedResponse>> Handle(
        ListPracticeReservationsQuery request,
        CancellationToken cancellationToken)
        => service.ListPracticeAsync(request, cancellationToken);
}

internal sealed class GetPracticeReservationQueryHandler(ReservationApplicationService service)
    : IQueryHandler<GetPracticeReservationQuery, ReservationDetailsResponse>
{
    public Task<Result<ReservationDetailsResponse>> Handle(
        GetPracticeReservationQuery request,
        CancellationToken cancellationToken)
        => service.GetPracticeAsync(
            request.PracticeId, request.ReservationId, request.IsDoctor, cancellationToken);
}

internal sealed class ListAdministrativeReservationsQueryHandler(ReservationApplicationService service)
    : IQueryHandler<ListAdministrativeReservationsQuery, ReservationPagedResponse>
{
    public Task<Result<ReservationPagedResponse>> Handle(
        ListAdministrativeReservationsQuery request,
        CancellationToken cancellationToken)
        => service.ListAdministrativeAsync(request, cancellationToken);
}

internal sealed class GetAdministrativeReservationQueryHandler(ReservationApplicationService service)
    : IQueryHandler<GetAdministrativeReservationQuery, ReservationDetailsResponse>
{
    public Task<Result<ReservationDetailsResponse>> Handle(
        GetAdministrativeReservationQuery request,
        CancellationToken cancellationToken)
        => service.GetAdministrativeAsync(request.ReservationId, cancellationToken);
}

internal sealed class GetReservationFilterOptionsQueryHandler(ReservationApplicationService service)
    : IQueryHandler<GetReservationFilterOptionsQuery, ReservationFilterOptionsResponse>
{
    public Task<Result<ReservationFilterOptionsResponse>> Handle(
        GetReservationFilterOptionsQuery request,
        CancellationToken cancellationToken)
        => service.GetFilterOptionsAsync(request.PracticeId, request.IsDoctor, cancellationToken);
}

internal sealed class GetBookingAvailableDatesQueryHandler(ReservationApplicationService service)
    : IQueryHandler<GetBookingAvailableDatesQuery, IReadOnlyList<ReservationAvailableDateResponse>>
{
    public Task<Result<IReadOnlyList<ReservationAvailableDateResponse>>> Handle(
        GetBookingAvailableDatesQuery request,
        CancellationToken cancellationToken)
        => service.AvailableDatesAsync(
            request.PracticeId, request.ReservationId, request.Channel, cancellationToken);
}

internal sealed class GetBookingAvailableSlotsQueryHandler(ReservationApplicationService service)
    : IQueryHandler<GetBookingAvailableSlotsQuery, IReadOnlyList<ReservationAvailableSlotResponse>>
{
    public Task<Result<IReadOnlyList<ReservationAvailableSlotResponse>>> Handle(
        GetBookingAvailableSlotsQuery request,
        CancellationToken cancellationToken)
        => service.AvailableSlotsAsync(
            request.PracticeId, request.Date, request.ReservationId, request.Channel, cancellationToken);
}

internal sealed class GetReservationBookingOptionsQueryHandler(ReservationApplicationService service)
    : IQueryHandler<GetReservationBookingOptionsQuery, ReservationBookingOptionsResponse>
{
    public Task<Result<ReservationBookingOptionsResponse>> Handle(
        GetReservationBookingOptionsQuery request,
        CancellationToken cancellationToken)
        => service.BookingOptionsAsync(
            request.PracticeId, request.Date, request.Time, request.Channel, cancellationToken);
}
