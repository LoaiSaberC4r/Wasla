using Asp.Versioning;
using BuildingBlock.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wasla.Api.Security;
using Wasla.Application.Features.Finance;
using Wasla.Domain.Security;

namespace Wasla.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/practices/{practiceId:guid}")]
[Authorize(Roles = SystemRoleNames.Doctor + "," + SystemRoleNames.Reception)]
public sealed class PracticeFinanceReadController(IFinanceReadService finance) : ControllerBase
{
    [HttpGet("financial-transactions")]
    public async Task<IActionResult> Transactions(Guid practiceId,
        [FromQuery] FinancialTransactionFilter filter, CancellationToken cancellationToken)
        => (await finance.ListPracticeTransactionsAsync(practiceId, filter, cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpGet("payments/{paymentId:guid}")]
    public async Task<IActionResult> PaymentDetail(Guid practiceId, Guid paymentId,
        CancellationToken cancellationToken)
        => (await finance.GetPracticePaymentDetailAsync(practiceId, paymentId, cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpGet("payments/{paymentId:guid}/receipt")]
    public async Task<IActionResult> PaymentReceipt(Guid practiceId, Guid paymentId,
        CancellationToken cancellationToken)
        => (await finance.GetPracticePaymentReceiptAsync(practiceId, paymentId, cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpGet("refunds/{refundId:guid}/receipt")]
    public async Task<IActionResult> RefundReceipt(Guid practiceId, Guid refundId,
        CancellationToken cancellationToken)
        => (await finance.GetPracticeRefundReceiptAsync(practiceId, refundId, cancellationToken))
            .ToIActionResult(cancellationToken);
}

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/doctors/me")]
[Authorize(Roles = SystemRoleNames.Doctor)]
public sealed class DoctorFinanceReadController(IFinanceReadService finance) : ControllerBase
{
    [HttpGet("financial-transactions")]
    [Permission(PermissionNames.DoctorPracticePaymentsViewOwn)]
    public async Task<IActionResult> Transactions([FromQuery] FinancialTransactionFilter filter,
        CancellationToken cancellationToken)
        => (await finance.ListDoctorTransactionsAsync(filter, cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpGet("revenue/dashboard")]
    [Permission(PermissionNames.DoctorRevenueViewOwn)]
    public async Task<IActionResult> Dashboard([FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate, [FromQuery] Guid? practiceId,
        CancellationToken cancellationToken)
        => (await finance.GetDoctorDashboardAsync(fromDate, toDate, practiceId, cancellationToken))
            .ToIActionResult(cancellationToken);
}

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/patients/me")]
[Authorize(Roles = SystemRoleNames.Patient)]
[Permission(PermissionNames.PaymentsViewOwn)]
public sealed class PatientFinanceReadController(IFinanceReadService finance) : ControllerBase
{
    [HttpGet("financial-transactions")]
    public async Task<IActionResult> Transactions([FromQuery] FinancialTransactionFilter filter,
        CancellationToken cancellationToken)
        => (await finance.ListOwnTransactionsAsync(filter, cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpGet("payments/{paymentId:guid}/receipt")]
    public async Task<IActionResult> PaymentReceipt(Guid paymentId, CancellationToken cancellationToken)
        => (await finance.GetOwnPaymentReceiptAsync(paymentId, cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpGet("refunds/{refundId:guid}/receipt")]
    public async Task<IActionResult> RefundReceipt(Guid refundId, CancellationToken cancellationToken)
        => (await finance.GetOwnRefundReceiptAsync(refundId, cancellationToken))
            .ToIActionResult(cancellationToken);
}

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/revenue")]
[Authorize(Roles = SystemRoleNames.SuperAdmin)]
[Permission(PermissionNames.PlatformRevenueViewAggregates)]
public sealed class PlatformRevenueReadController(IFinanceReadService finance) : ControllerBase
{
    [HttpGet("aggregates")]
    public async Task<IActionResult> Aggregates([FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate, [FromQuery] Guid? doctorId,
        [FromQuery] Guid? practiceId, CancellationToken cancellationToken)
        => (await finance.GetPlatformDashboardAsync(fromDate, toDate, doctorId, practiceId,
            cancellationToken)).ToIActionResult(cancellationToken);
}
