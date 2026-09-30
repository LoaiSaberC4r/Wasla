using Asp.Versioning;
using BuildingBlock.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wasla.Application.Features.Finance;
using Wasla.Domain.Payments;
using Wasla.Domain.Security;

namespace Wasla.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/practices/{practiceId:guid}")]
[Authorize(Roles = SystemRoleNames.Doctor + "," + SystemRoleNames.Reception)]
public sealed class FinanceMutationsController(ISender sender) : ControllerBase
{
    [HttpPost("payments/{paymentId:guid}/refund")]
    public async Task<IActionResult> Refund(
        Guid practiceId,
        Guid paymentId,
        RefundPaymentRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
        => (await sender.Send(new RefundPaymentCommand(
            practiceId, paymentId, request.RefundMethod, request.RefundReasonCode,
            request.Reason, request.ReferenceNumber, request.Notes,
            idempotencyKey ?? string.Empty), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPost("payments/{paymentId:guid}/correct")]
    public async Task<IActionResult> CorrectPayment(
        Guid practiceId,
        Guid paymentId,
        CorrectPaymentRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
        => (await sender.Send(new CorrectPaymentCommand(
            practiceId, paymentId, request.PaymentMethod, request.ReferenceNumber,
            request.Notes, request.CorrectionReason, request.RowVersion,
            idempotencyKey ?? string.Empty), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPost("refunds/{refundId:guid}/correct")]
    public async Task<IActionResult> CorrectRefund(
        Guid practiceId,
        Guid refundId,
        CorrectRefundRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
        => (await sender.Send(new CorrectRefundCommand(
            practiceId, refundId, request.RefundMethod, request.RefundReasonCode,
            request.Reason, request.ReferenceNumber, request.Notes,
            request.CorrectionReason, request.RowVersion,
            idempotencyKey ?? string.Empty), cancellationToken)).ToIActionResult(cancellationToken);
}

public sealed record RefundPaymentRequest(
    PaymentMethod RefundMethod,
    RefundReasonCode RefundReasonCode,
    string? Reason,
    string? ReferenceNumber,
    string? Notes);

public sealed record CorrectPaymentRequest(
    PaymentMethod PaymentMethod,
    string? ReferenceNumber,
    string? Notes,
    string CorrectionReason,
    string RowVersion);

public sealed record CorrectRefundRequest(
    PaymentMethod RefundMethod,
    RefundReasonCode RefundReasonCode,
    string? Reason,
    string? ReferenceNumber,
    string? Notes,
    string CorrectionReason,
    string RowVersion);
