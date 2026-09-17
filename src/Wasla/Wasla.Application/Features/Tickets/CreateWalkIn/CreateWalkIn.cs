using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Doctors;
using Wasla.Domain.Patients;
using Wasla.Domain.Payments;
using Wasla.Domain.Practices;
using Wasla.Domain.Reservations;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;

namespace Wasla.Application.Features.Tickets.CreateWalkIn;

public sealed record CreateWalkInCommand(
    Guid PracticeId,
    Guid PatientId,
    Guid SegmentId,
    Guid VisitTypeId,
    decimal PaidAmount,
    string IdempotencyKey)
    : ICommand<TicketDetailsResponse>, ITransactionalCommand<WaslaWritePersistence>;

internal sealed class CreateWalkInCommandValidator : AbstractValidator<CreateWalkInCommand>
{
    public CreateWalkInCommandValidator()
    {
        RuleFor(command => command.PracticeId).NotEmpty();
        RuleFor(command => command.PatientId).NotEmpty();
        RuleFor(command => command.SegmentId).NotEmpty();
        RuleFor(command => command.VisitTypeId).NotEmpty();
        RuleFor(command => command.PaidAmount).GreaterThan(0);
        RuleFor(command => command.IdempotencyKey).NotEmpty().MaximumLength(200);
    }
}

internal sealed class CreateWalkInCommandHandler(
    TicketAccessService access,
    IUnitOfWork<WaslaWritePersistence> unitOfWork,
    ITicketQueueLock queueLock,
    ITicketNumberAllocator numberAllocator,
    ITicketQueueReader queueReader,
    IReservationNoShowRuntimeReader reservationNoShowReader,
    IDateTimeProvider clock)
    : ICommandHandler<CreateWalkInCommand, TicketDetailsResponse>
{
    public async Task<Result<TicketDetailsResponse>> Handle(
        CreateWalkInCommand request,
        CancellationToken cancellationToken)
    {
        var actor = await access.AuthorizeReceptionAsync(
            request.PracticeId, PermissionNames.PracticeTicketsCreateWalkIn, cancellationToken);
        if (actor.IsFailure)
        {
            return Result<TicketDetailsResponse>.Fail(actor.Errors);
        }

        var paymentAccess = await access.AuthorizeReceptionAsync(
            request.PracticeId, PermissionNames.PracticeTicketsRecordPayment, cancellationToken);
        if (paymentAccess.IsFailure)
        {
            return Result<TicketDetailsResponse>.Fail(paymentAccess.Errors);
        }

        var key = TicketIdempotency.ValidateKey(request.IdempotencyKey);
        if (key.IsFailure)
        {
            return Result<TicketDetailsResponse>.Fail(key.Errors);
        }

        var practice = await unitOfWork.WriteRepository<DoctorPractice>()
            .GetByIdAsync(request.PracticeId, cancellationToken);
        var configuration = await unitOfWork.WriteRepository<DoctorPracticeConfiguration>()
            .GetByPropertyAsync(item => item.DoctorPracticeId == request.PracticeId, cancellationToken);
        var doctor = practice is null
            ? null
            : await unitOfWork.WriteRepository<Doctor>().GetByIdAsync(practice.DoctorId, cancellationToken);
        if (practice is null || !practice.IsActive || configuration is null || !configuration.AllowWalkIn ||
            doctor is null || doctor.ApprovalStatus != DoctorApprovalStatus.Approved)
        {
            return Result<TicketDetailsResponse>.Fail(
                configuration is { AllowWalkIn: false }
                    ? TicketErrors.WalkInNotAllowed
                    : TicketErrors.PracticeUnavailable);
        }

        var patient = await unitOfWork.WriteRepository<Patient>()
            .GetByIdAsync(request.PatientId, cancellationToken);
        var segment = await unitOfWork.WriteRepository<DoctorPracticeSegment>()
            .GetByIdAsync(request.SegmentId, cancellationToken);
        var visitType = await unitOfWork.WriteRepository<DoctorPracticeVisitType>()
            .GetByIdAsync(request.VisitTypeId, cancellationToken);
        var price = await unitOfWork.WriteRepository<DoctorPracticeSegmentVisitTypePrice>()
            .GetByPropertyAsync(item => item.DoctorPracticeId == request.PracticeId &&
                item.SegmentId == request.SegmentId && item.VisitTypeId == request.VisitTypeId,
                cancellationToken);
        if (patient is null || segment is null || !segment.IsActive ||
            segment.DoctorPracticeId != request.PracticeId || visitType is null || !visitType.IsActive ||
            visitType.DoctorPracticeId != request.PracticeId ||
            visitType.Type != DoctorPracticeVisitTypeCode.NewConsultation || price is null)
        {
            return Result<TicketDetailsResponse>.Fail(TicketErrors.InvalidCatalog);
        }

        if (request.PaidAmount != price.Price)
        {
            return Result<TicketDetailsResponse>.Fail(TicketErrors.PaymentRequired);
        }

        var nowUtc = EnsureUtc(clock.UtcNow);
        var businessDate = TicketBusinessClock.CurrentBusinessDate(nowUtc, configuration.TimeZoneId);
        await queueLock.AcquirePatientPracticeAsync(
            request.PatientId, request.PracticeId, cancellationToken);
        await queueLock.AcquirePracticeDayAsync(
            request.PracticeId, businessDate, cancellationToken);
        await queueLock.AcquireIdempotencyAsync(
            actor.Value.ApplicationUserId, "CreateWalkIn", key.Value, cancellationToken);

        var idempotencyRepository = unitOfWork.WriteRepository<TicketIdempotencyRecord>();
        var idempotency = await TicketIdempotency.BeginAsync(
            idempotencyRepository,
            actor.Value.ApplicationUserId,
            "CreateWalkIn",
            key.Value,
            TicketIdempotency.Fingerprint(
                request.PracticeId, request.PatientId, request.SegmentId,
                request.VisitTypeId, request.PaidAmount),
            nowUtc,
            cancellationToken);
        if (idempotency.IsFailure)
        {
            return Result<TicketDetailsResponse>.Fail(idempotency.Errors);
        }

        if (idempotency.Value.ExistingTicketId is { } existingId)
        {
            return await LoadDetailsAsync(existingId, cancellationToken);
        }

        if (await reservationNoShowReader.HasSameDayNoShowAsync(
                request.PatientId, request.PracticeId, businessDate, cancellationToken))
        {
            return Result<TicketDetailsResponse>.Fail(ReservationErrors.SameDayNoShowBookingBlocked);
        }

        if (await queueReader.HasOpenTicketAsync(
                request.PatientId, request.PracticeId, cancellationToken))
        {
            return Result<TicketDetailsResponse>.Fail(TicketErrors.OpenTicketExists);
        }

        var ticketNumber = await numberAllocator.AllocateNextAsync(
            request.PracticeId, businessDate, cancellationToken);
        var ticketId = Guid.NewGuid();
        var ticket = Ticket.CreateWalkIn(new TicketCreationSnapshot(
            ticketId,
            practice.DoctorId,
            request.PracticeId,
            request.PatientId,
            null,
            businessDate,
            ticketNumber,
            TicketSource.WalkIn,
            segment.Id,
            segment.NameAr,
            segment.NameEn,
            segment.Priority,
            visitType.Id,
            visitType.Type.ToString(),
            visitType.NameAr,
            visitType.NameEn,
            price.Price,
            configuration.TimeZoneId,
            nowUtc,
            nowUtc,
            CheckInMode.WalkIn,
            actor.Value.ApplicationUserId,
            null));
        if (ticket.IsFailure)
        {
            return Result<TicketDetailsResponse>.Fail(ticket.Errors);
        }

        var payment = Payment.RecordPaid(
            Guid.NewGuid(), practice.DoctorId, request.PracticeId, request.PatientId,
            null, ticketId, request.PaidAmount, price.Price,
            actor.Value.ApplicationUserId, nowUtc);
        if (payment.IsFailure)
        {
            return Result<TicketDetailsResponse>.Fail(payment.Errors);
        }

        await unitOfWork.WriteRepository<Ticket>().AddAsync(ticket.Value, cancellationToken);
        await unitOfWork.WriteRepository<Payment>().AddAsync(payment.Value, cancellationToken);
        idempotency.Value.Record!.Complete(ticketId, nowUtc);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await LoadDetailsAsync(ticketId, cancellationToken);
    }

    private async Task<Result<TicketDetailsResponse>> LoadDetailsAsync(
        Guid ticketId,
        CancellationToken cancellationToken)
    {
        var details = await queueReader.GetDetailsAsync(ticketId, cancellationToken);
        return details is null
            ? Result<TicketDetailsResponse>.Fail(TicketErrors.NotFound)
            : Result<TicketDetailsResponse>.Ok(details);
    }

    private static DateTime EnsureUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}
