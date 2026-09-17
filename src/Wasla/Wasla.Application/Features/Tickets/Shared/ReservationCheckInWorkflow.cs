using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using Wasla.Application.Features.Reservations;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Doctors;
using Wasla.Domain.Payments;
using Wasla.Domain.Practices;
using Wasla.Domain.Reservations;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;

namespace Wasla.Application.Features.Tickets.Common;

internal sealed class ReservationCheckInWorkflow(
    TicketAccessService access,
    IUnitOfWork<WaslaWritePersistence> unitOfWork,
    IReadRepository<Reservation, WaslaReadPersistence> reservationReader,
    ITicketQueueLock queueLock,
    ITicketNumberAllocator numberAllocator,
    ITicketQueueReader queueReader,
    IReservationProjectionInvalidationOutbox projectionOutbox,
    IDateTimeProvider clock)
{
    public async Task<Result<TicketDetailsResponse>> ExecuteAsync(
        Guid practiceId,
        Guid reservationId,
        decimal paidAmount,
        bool force,
        string? reason,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var permission = force
            ? PermissionNames.PracticeTicketsForceCheckIn
            : PermissionNames.PracticeTicketsCheckIn;
        var actor = await access.AuthorizeReceptionAsync(practiceId, permission, cancellationToken);
        if (actor.IsFailure)
        {
            return Result<TicketDetailsResponse>.Fail(actor.Errors);
        }

        var paymentAccess = await access.AuthorizeReceptionAsync(
            practiceId, PermissionNames.PracticeTicketsRecordPayment, cancellationToken);
        if (paymentAccess.IsFailure)
        {
            return Result<TicketDetailsResponse>.Fail(paymentAccess.Errors);
        }

        var validatedKey = TicketIdempotency.ValidateKey(idempotencyKey);
        if (validatedKey.IsFailure)
        {
            return Result<TicketDetailsResponse>.Fail(validatedKey.Errors);
        }

        if (force && string.IsNullOrWhiteSpace(reason))
        {
            return Result<TicketDetailsResponse>.Fail(TicketErrors.InvalidCreation);
        }

        var reservationSnapshot = await reservationReader.GetByIdAsync(reservationId, cancellationToken);
        if (reservationSnapshot is null || reservationSnapshot.DoctorPracticeId != practiceId)
        {
            return Result<TicketDetailsResponse>.Fail(ReservationErrors.NotFound);
        }

        var practiceRepository = unitOfWork.WriteRepository<DoctorPractice>();
        var configurationRepository = unitOfWork.WriteRepository<DoctorPracticeConfiguration>();
        var doctorRepository = unitOfWork.WriteRepository<Doctor>();
        var practice = await practiceRepository.GetByIdAsync(practiceId, cancellationToken);
        var configuration = await configurationRepository.GetByPropertyAsync(
            item => item.DoctorPracticeId == practiceId, cancellationToken);
        var doctor = practice is null
            ? null
            : await doctorRepository.GetByIdAsync(practice.DoctorId, cancellationToken);
        if (practice is null || !practice.IsActive || configuration is null || doctor is null ||
            doctor.ApprovalStatus != DoctorApprovalStatus.Approved)
        {
            return Result<TicketDetailsResponse>.Fail(TicketErrors.PracticeUnavailable);
        }

        var nowUtc = EnsureUtc(clock.UtcNow);
        var businessDate = TicketBusinessClock.CurrentBusinessDate(nowUtc, configuration.TimeZoneId);
        if (reservationSnapshot.BusinessDate != businessDate)
        {
            return Result<TicketDetailsResponse>.Fail(TicketErrors.BusinessDateMismatch);
        }

        if (!force && nowUtc < reservationSnapshot.ScheduledStartUtc
                .AddMinutes(-configuration.CheckInOpenBeforeMinutes))
        {
            return Result<TicketDetailsResponse>.Fail(TicketErrors.CheckInWindowNotOpen);
        }

        if (paidAmount != reservationSnapshot.PriceSnapshot)
        {
            return Result<TicketDetailsResponse>.Fail(TicketErrors.PaymentRequired);
        }

        var operation = force ? "ForceCheckInReservation" : "CheckInReservation";
        var fingerprint = TicketIdempotency.Fingerprint(
            practiceId, reservationId, paidAmount, force, reason?.Trim());
        await queueLock.AcquirePatientPracticeAsync(
            reservationSnapshot.PatientId, practiceId, cancellationToken);
        await queueLock.AcquirePracticeDayAsync(practiceId, businessDate, cancellationToken);
        await queueLock.AcquireIdempotencyAsync(
            actor.Value.ApplicationUserId, operation, validatedKey.Value, cancellationToken);

        var idempotencyRepository = unitOfWork.WriteRepository<TicketIdempotencyRecord>();
        var idempotency = await TicketIdempotency.BeginAsync(
            idempotencyRepository,
            actor.Value.ApplicationUserId,
            operation,
            validatedKey.Value,
            fingerprint,
            nowUtc,
            cancellationToken);
        if (idempotency.IsFailure)
        {
            return Result<TicketDetailsResponse>.Fail(idempotency.Errors);
        }

        if (idempotency.Value.ExistingTicketId is { } existingTicketId)
        {
            return await LoadDetailsAsync(existingTicketId, cancellationToken);
        }

        var reservationRepository = unitOfWork.WriteRepository<Reservation>();
        var reservation = await reservationRepository.FirstOrDefaultAsync(
            new ReservationForTicketSpecification(reservationId), cancellationToken);
        if (reservation is null || reservation.DoctorPracticeId != practiceId)
        {
            return Result<TicketDetailsResponse>.Fail(ReservationErrors.NotFound);
        }

        if (reservation.Status != ReservationStatus.Active)
        {
            return Result<TicketDetailsResponse>.Fail(ReservationErrors.InvalidState);
        }

        var ticketRepository = unitOfWork.WriteRepository<Ticket>();
        if (await ticketRepository.GetByPropertyAsync(
                item => item.ReservationId == reservationId, cancellationToken) is not null)
        {
            return Result<TicketDetailsResponse>.Fail(TicketErrors.DuplicateReservation);
        }

        if (await queueReader.HasOpenTicketAsync(
                reservation.PatientId, practiceId, cancellationToken))
        {
            return Result<TicketDetailsResponse>.Fail(TicketErrors.OpenTicketExists);
        }

        var ticketNumber = await numberAllocator.AllocateNextAsync(
            practiceId, businessDate, cancellationToken);
        var ticketId = Guid.NewGuid();
        var ticketResult = Ticket.CreateFromReservation(new TicketCreationSnapshot(
            ticketId,
            reservation.DoctorId,
            practiceId,
            reservation.PatientId,
            reservation.Id,
            businessDate,
            ticketNumber,
            TicketSource.Reservation,
            reservation.SegmentId,
            reservation.SegmentNameArSnapshot,
            reservation.SegmentNameEnSnapshot,
            reservation.SegmentPrioritySnapshot,
            reservation.VisitTypeId,
            reservation.VisitTypeCodeSnapshot,
            reservation.VisitTypeNameArSnapshot,
            reservation.VisitTypeNameEnSnapshot,
            reservation.PriceSnapshot,
            reservation.TimeZoneIdSnapshot,
            nowUtc,
            nowUtc,
            force ? CheckInMode.Force : CheckInMode.Normal,
            actor.Value.ApplicationUserId,
            reason));
        if (ticketResult.IsFailure)
        {
            return Result<TicketDetailsResponse>.Fail(ticketResult.Errors);
        }

        var paymentResult = Payment.RecordPaid(
            Guid.NewGuid(), reservation.DoctorId, practiceId, reservation.PatientId,
            reservation.Id, ticketId, paidAmount, reservation.PriceSnapshot,
            actor.Value.ApplicationUserId, nowUtc);
        if (paymentResult.IsFailure)
        {
            return Result<TicketDetailsResponse>.Fail(paymentResult.Errors);
        }

        var conversion = reservation.ConvertToTicket(actor.Value.ApplicationUserId, nowUtc);
        if (conversion.IsFailure)
        {
            return Result<TicketDetailsResponse>.Fail(conversion.Errors);
        }

        await ticketRepository.AddAsync(ticketResult.Value, cancellationToken);
        await unitOfWork.WriteRepository<Payment>().AddAsync(paymentResult.Value, cancellationToken);
        idempotency.Value.Record!.Complete(ticketId, nowUtc);
        await projectionOutbox.QueueAsync(new QueueReservationProjectionInvalidation(
            $"reservation-converted:{reservation.Id:N}:{reservation.History.Last().Id:N}",
            reservation.DoctorPracticeId,
            reservation.DoctorId,
            RefreshAvailability: false,
            RefreshPopularity: true), cancellationToken);
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
