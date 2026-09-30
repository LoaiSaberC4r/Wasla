using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Features.Finance.Common;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Application.Persistence;
using Wasla.Domain.Payments;
using Wasla.Domain.Practices;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;

namespace Wasla.Application.Features.Finance;

public sealed record RefundPaymentCommand(
    Guid PracticeId,
    Guid PaymentId,
    PaymentMethod RefundMethod,
    RefundReasonCode RefundReasonCode,
    string? Reason,
    string? ReferenceNumber,
    string? Notes,
    string IdempotencyKey)
    : ICommand<RefundPaymentResponse>, ITransactionalCommand<WaslaWritePersistence>;

public sealed record RefundPaymentResponse(
    Guid RefundId,
    string TransactionNumber,
    string OriginalPaymentTransactionNumber,
    decimal Amount,
    string CurrencyCode,
    DateTime RefundedOnUtc,
    DateOnly BusinessDate);

internal sealed class RefundPaymentCommandValidator : AbstractValidator<RefundPaymentCommand>
{
    public RefundPaymentCommandValidator()
    {
        RuleFor(command => command.PracticeId).NotEmpty();
        RuleFor(command => command.PaymentId).NotEmpty();
        RuleFor(command => command.RefundMethod).Must(FinancialPolicy.IsSelectableMethod);
        RuleFor(command => command.RefundReasonCode).IsInEnum();
        RuleFor(command => command.Reason).MaximumLength(FinancialPolicy.ReasonMaxLength);
        RuleFor(command => command.ReferenceNumber).MaximumLength(FinancialPolicy.ReferenceNumberMaxLength);
        RuleFor(command => command.Notes).MaximumLength(FinancialPolicy.NotesMaxLength);
        RuleFor(command => command.IdempotencyKey).NotEmpty().MaximumLength(200);
        When(command => command.RefundReasonCode == RefundReasonCode.Other,
            () => RuleFor(command => command.Reason).NotEmpty());
    }
}

internal sealed class RefundPaymentCommandHandler(
    TicketAccessService access,
    IUnitOfWork<WaslaWritePersistence> unitOfWork,
    ITicketQueueLock queueLock,
    IFinancialNumberAllocator numberAllocator,
    IDateTimeProvider clock)
    : ICommandHandler<RefundPaymentCommand, RefundPaymentResponse>
{
    public async Task<Result<RefundPaymentResponse>> Handle(
        RefundPaymentCommand request,
        CancellationToken cancellationToken)
    {
        var actor = await access.AuthorizeDoctorOrReceptionAsync(
            request.PracticeId,
            PermissionNames.DoctorPracticePaymentsRefundOwn,
            PermissionNames.PracticePaymentsRefund,
            cancellationToken);
        if (actor.IsFailure)
        {
            return Result<RefundPaymentResponse>.Fail(FinancialWriteErrors.AccessDenied);
        }

        await queueLock.AcquireIdempotencyAsync(
            Guid.Empty, "FinancialPayment", request.PaymentId.ToString("N"), cancellationToken);
        await queueLock.AcquireIdempotencyAsync(
            actor.Value.ApplicationUserId, "RefundPayment", request.IdempotencyKey.Trim(), cancellationToken);
        var nowUtc = FinancialPolicy.EnsureUtc(clock.UtcNow);
        var fingerprint = FinancialWriteIdempotency.Fingerprint(
            request.PracticeId, request.PaymentId, request.RefundMethod,
            request.RefundReasonCode, FinancialPolicy.Normalize(request.Reason),
            FinancialPolicy.Normalize(request.ReferenceNumber), FinancialPolicy.Normalize(request.Notes));
        var idempotency = await FinancialWriteIdempotency.BeginAsync(
            unitOfWork.WriteRepository<FinancialIdempotencyRecord>(),
            actor.Value.ApplicationUserId, "RefundPayment", request.IdempotencyKey,
            fingerprint, nowUtc, cancellationToken);
        if (idempotency.IsFailure)
        {
            return Result<RefundPaymentResponse>.Fail(idempotency.Errors);
        }

        var payment = await unitOfWork.WriteRepository<Payment>().FirstOrDefaultAsync(
            new PaymentForFinancialWriteSpecification(request.PaymentId), cancellationToken);
        if (payment is null || payment.DoctorPracticeId != request.PracticeId)
        {
            return Result<RefundPaymentResponse>.Fail(FinancialWriteErrors.NotFound);
        }

        if (idempotency.Value.ResultId is { } existingRefundId)
        {
            var existingRefund = payment.Refund;
            return existingRefund is not null && existingRefund.Id == existingRefundId
                ? Result<RefundPaymentResponse>.Ok(ToResponse(existingRefund, payment))
                : Result<RefundPaymentResponse>.Fail(FinancialWriteErrors.ReplayUnavailable);
        }

        if (payment.Refund is not null)
        {
            return Result<RefundPaymentResponse>.Fail(FinancialWriteErrors.AlreadyRefunded);
        }

        var ticket = await unitOfWork.WriteRepository<Ticket>().FirstOrDefaultAsync(
            new TicketByIdForUpdateSpecification(payment.TicketId), cancellationToken);
        if (ticket is null || ticket.DoctorPracticeId != request.PracticeId ||
            ticket.DoctorId != payment.DoctorId || ticket.PatientId != payment.PatientId ||
            ticket.PriceSnapshot != payment.Amount || payment.CurrencyCode != FinancialPolicy.CurrencyCode ||
            payment.Amount <= 0)
        {
            return Result<RefundPaymentResponse>.Fail(FinancialWriteErrors.InvalidPayment);
        }

        if (ticket.Status != TicketStatus.Cancelled)
        {
            return Result<RefundPaymentResponse>.Fail(FinancialWriteErrors.TicketMustBeCancelled);
        }

        if (ticket.InProgressOnUtc.HasValue ||
            ticket.History.Any(history => history.EventType == TicketHistoryEventType.InProgress))
        {
            return Result<RefundPaymentResponse>.Fail(FinancialWriteErrors.ServiceAlreadyStarted);
        }

        var configuration = await unitOfWork.WriteRepository<DoctorPracticeConfiguration>()
            .GetByPropertyAsync(item => item.DoctorPracticeId == request.PracticeId, cancellationToken);
        if (configuration is null)
        {
            return Result<RefundPaymentResponse>.Fail(FinancialWriteErrors.InvalidPayment);
        }

        var businessDate = TicketBusinessClock.CurrentBusinessDate(nowUtc, configuration.TimeZoneId);
        var number = await numberAllocator.AllocateNextAsync(
            request.PracticeId, businessDate, FinancialTransactionType.Refund, cancellationToken);
        var result = Refund.Record(
            Guid.NewGuid(), payment, number, businessDate, request.RefundMethod,
            request.RefundReasonCode, request.Reason, request.ReferenceNumber,
            request.Notes, actor.Value.ApplicationUserId, nowUtc);
        if (result.IsFailure)
        {
            return Result<RefundPaymentResponse>.Fail(result.Errors);
        }

        await unitOfWork.WriteRepository<Refund>().AddAsync(result.Value, cancellationToken);
        idempotency.Value.Complete(result.Value.Id, nowUtc);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<RefundPaymentResponse>.Ok(ToResponse(result.Value, payment));
    }

    private static RefundPaymentResponse ToResponse(Refund refund, Payment payment)
        => new(refund.Id, refund.TransactionNumber, payment.TransactionNumber,
            refund.Amount, refund.CurrencyCode, refund.RefundedOnUtc, refund.BusinessDate);
}

public sealed record CorrectPaymentCommand(
    Guid PracticeId,
    Guid PaymentId,
    PaymentMethod PaymentMethod,
    string? ReferenceNumber,
    string? Notes,
    string CorrectionReason,
    string RowVersion,
    string IdempotencyKey)
    : ICommand<FinancialCorrectionResponse>, ITransactionalCommand<WaslaWritePersistence>;

public sealed record FinancialCorrectionResponse(
    Guid CorrectionId,
    Guid TransactionId,
    string TransactionNumber,
    DateTime CorrectedOnUtc);

internal sealed class CorrectPaymentCommandValidator : AbstractValidator<CorrectPaymentCommand>
{
    public CorrectPaymentCommandValidator()
    {
        RuleFor(command => command.PracticeId).NotEmpty();
        RuleFor(command => command.PaymentId).NotEmpty();
        RuleFor(command => command.PaymentMethod).Must(FinancialPolicy.IsSelectableMethod);
        RuleFor(command => command.ReferenceNumber).MaximumLength(FinancialPolicy.ReferenceNumberMaxLength);
        RuleFor(command => command.Notes).MaximumLength(FinancialPolicy.NotesMaxLength);
        RuleFor(command => command.CorrectionReason).NotEmpty().MaximumLength(FinancialPolicy.CorrectionReasonMaxLength);
        RuleFor(command => command.RowVersion).Must(TicketRowVersion.IsValid);
        RuleFor(command => command.IdempotencyKey).NotEmpty().MaximumLength(200);
    }
}

internal sealed class CorrectPaymentCommandHandler(
    TicketAccessService access,
    IUnitOfWork<WaslaWritePersistence> unitOfWork,
    ITicketQueueLock queueLock,
    IDateTimeProvider clock)
    : ICommandHandler<CorrectPaymentCommand, FinancialCorrectionResponse>
{
    public async Task<Result<FinancialCorrectionResponse>> Handle(
        CorrectPaymentCommand request,
        CancellationToken cancellationToken)
    {
        var actor = await access.AuthorizeDoctorOrReceptionAsync(
            request.PracticeId,
            PermissionNames.DoctorPracticePaymentsCorrectOwn,
            PermissionNames.PracticePaymentsCorrect,
            cancellationToken);
        if (actor.IsFailure)
        {
            return Result<FinancialCorrectionResponse>.Fail(FinancialWriteErrors.AccessDenied);
        }

        await queueLock.AcquireIdempotencyAsync(
            Guid.Empty, "FinancialPayment", request.PaymentId.ToString("N"), cancellationToken);
        await queueLock.AcquireIdempotencyAsync(
            actor.Value.ApplicationUserId, "CorrectPayment", request.IdempotencyKey.Trim(), cancellationToken);
        var nowUtc = FinancialPolicy.EnsureUtc(clock.UtcNow);
        var fingerprint = FinancialWriteIdempotency.Fingerprint(
            request.PracticeId, request.PaymentId, request.PaymentMethod,
            FinancialPolicy.Normalize(request.ReferenceNumber), FinancialPolicy.Normalize(request.Notes),
            request.CorrectionReason.Trim(), request.RowVersion);
        var idempotency = await FinancialWriteIdempotency.BeginAsync(
            unitOfWork.WriteRepository<FinancialIdempotencyRecord>(),
            actor.Value.ApplicationUserId, "CorrectPayment", request.IdempotencyKey,
            fingerprint, nowUtc, cancellationToken);
        if (idempotency.IsFailure)
        {
            return Result<FinancialCorrectionResponse>.Fail(idempotency.Errors);
        }

        var payment = await unitOfWork.WriteRepository<Payment>().FirstOrDefaultAsync(
            new PaymentForFinancialWriteSpecification(request.PaymentId), cancellationToken);
        if (payment is null || payment.DoctorPracticeId != request.PracticeId)
        {
            return Result<FinancialCorrectionResponse>.Fail(FinancialWriteErrors.NotFound);
        }

        if (idempotency.Value.ResultId is { } existingHistoryId)
        {
            var existing = payment.CorrectionHistory.SingleOrDefault(history => history.Id == existingHistoryId);
            return existing is null
                ? Result<FinancialCorrectionResponse>.Fail(FinancialWriteErrors.ReplayUnavailable)
                : Result<FinancialCorrectionResponse>.Ok(new(
                    existing.Id, payment.Id, payment.TransactionNumber, existing.CorrectedOnUtc));
        }

        if (!TicketRowVersion.Matches(payment.RowVersion, request.RowVersion))
        {
            return Result<FinancialCorrectionResponse>.Fail(FinancialWriteErrors.ConcurrentModification);
        }

        var result = payment.CorrectMetadata(
            request.PaymentMethod, request.ReferenceNumber, request.Notes,
            request.CorrectionReason, actor.Value.ApplicationUserId, nowUtc);
        if (result.IsFailure)
        {
            return Result<FinancialCorrectionResponse>.Fail(result.Errors);
        }

        idempotency.Value.Complete(result.Value.Id, nowUtc);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<FinancialCorrectionResponse>.Ok(new(
            result.Value.Id, payment.Id, payment.TransactionNumber, result.Value.CorrectedOnUtc));
    }
}

public sealed record CorrectRefundCommand(
    Guid PracticeId,
    Guid RefundId,
    PaymentMethod RefundMethod,
    RefundReasonCode RefundReasonCode,
    string? Reason,
    string? ReferenceNumber,
    string? Notes,
    string CorrectionReason,
    string RowVersion,
    string IdempotencyKey)
    : ICommand<FinancialCorrectionResponse>, ITransactionalCommand<WaslaWritePersistence>;

internal sealed class CorrectRefundCommandValidator : AbstractValidator<CorrectRefundCommand>
{
    public CorrectRefundCommandValidator()
    {
        RuleFor(command => command.PracticeId).NotEmpty();
        RuleFor(command => command.RefundId).NotEmpty();
        RuleFor(command => command.RefundMethod).Must(FinancialPolicy.IsSelectableMethod);
        RuleFor(command => command.RefundReasonCode).IsInEnum();
        RuleFor(command => command.Reason).MaximumLength(FinancialPolicy.ReasonMaxLength);
        RuleFor(command => command.ReferenceNumber).MaximumLength(FinancialPolicy.ReferenceNumberMaxLength);
        RuleFor(command => command.Notes).MaximumLength(FinancialPolicy.NotesMaxLength);
        RuleFor(command => command.CorrectionReason).NotEmpty().MaximumLength(FinancialPolicy.CorrectionReasonMaxLength);
        RuleFor(command => command.RowVersion).Must(TicketRowVersion.IsValid);
        RuleFor(command => command.IdempotencyKey).NotEmpty().MaximumLength(200);
        When(command => command.RefundReasonCode == RefundReasonCode.Other,
            () => RuleFor(command => command.Reason).NotEmpty());
    }
}

internal sealed class CorrectRefundCommandHandler(
    TicketAccessService access,
    IUnitOfWork<WaslaWritePersistence> unitOfWork,
    ITicketQueueLock queueLock,
    IDateTimeProvider clock)
    : ICommandHandler<CorrectRefundCommand, FinancialCorrectionResponse>
{
    public async Task<Result<FinancialCorrectionResponse>> Handle(
        CorrectRefundCommand request,
        CancellationToken cancellationToken)
    {
        var actor = await access.AuthorizeDoctorOrReceptionAsync(
            request.PracticeId,
            PermissionNames.DoctorPracticePaymentsCorrectOwn,
            PermissionNames.PracticePaymentsCorrect,
            cancellationToken);
        if (actor.IsFailure)
        {
            return Result<FinancialCorrectionResponse>.Fail(FinancialWriteErrors.AccessDenied);
        }

        await queueLock.AcquireIdempotencyAsync(
            Guid.Empty, "FinancialRefund", request.RefundId.ToString("N"), cancellationToken);
        await queueLock.AcquireIdempotencyAsync(
            actor.Value.ApplicationUserId, "CorrectRefund", request.IdempotencyKey.Trim(), cancellationToken);
        var nowUtc = FinancialPolicy.EnsureUtc(clock.UtcNow);
        var fingerprint = FinancialWriteIdempotency.Fingerprint(
            request.PracticeId, request.RefundId, request.RefundMethod,
            request.RefundReasonCode, FinancialPolicy.Normalize(request.Reason),
            FinancialPolicy.Normalize(request.ReferenceNumber), FinancialPolicy.Normalize(request.Notes),
            request.CorrectionReason.Trim(), request.RowVersion);
        var idempotency = await FinancialWriteIdempotency.BeginAsync(
            unitOfWork.WriteRepository<FinancialIdempotencyRecord>(),
            actor.Value.ApplicationUserId, "CorrectRefund", request.IdempotencyKey,
            fingerprint, nowUtc, cancellationToken);
        if (idempotency.IsFailure)
        {
            return Result<FinancialCorrectionResponse>.Fail(idempotency.Errors);
        }

        var refund = await unitOfWork.WriteRepository<Refund>().FirstOrDefaultAsync(
            new RefundForFinancialWriteSpecification(request.RefundId), cancellationToken);
        if (refund is null || refund.DoctorPracticeId != request.PracticeId)
        {
            return Result<FinancialCorrectionResponse>.Fail(FinancialWriteErrors.NotFound);
        }

        if (idempotency.Value.ResultId is { } existingHistoryId)
        {
            var existing = refund.CorrectionHistory.SingleOrDefault(history => history.Id == existingHistoryId);
            return existing is null
                ? Result<FinancialCorrectionResponse>.Fail(FinancialWriteErrors.ReplayUnavailable)
                : Result<FinancialCorrectionResponse>.Ok(new(
                    existing.Id, refund.Id, refund.TransactionNumber, existing.CorrectedOnUtc));
        }

        if (!TicketRowVersion.Matches(refund.RowVersion, request.RowVersion))
        {
            return Result<FinancialCorrectionResponse>.Fail(FinancialWriteErrors.ConcurrentModification);
        }

        var result = refund.CorrectMetadata(
            request.RefundMethod, request.RefundReasonCode, request.Reason,
            request.ReferenceNumber, request.Notes, request.CorrectionReason,
            actor.Value.ApplicationUserId, nowUtc);
        if (result.IsFailure)
        {
            return Result<FinancialCorrectionResponse>.Fail(result.Errors);
        }

        idempotency.Value.Complete(result.Value.Id, nowUtc);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<FinancialCorrectionResponse>.Ok(new(
            result.Value.Id, refund.Id, refund.TransactionNumber, result.Value.CorrectedOnUtc));
    }
}
