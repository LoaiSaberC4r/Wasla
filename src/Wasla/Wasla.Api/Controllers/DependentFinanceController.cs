using Asp.Versioning;
using BuildingBlock.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wasla.Api.Security;
using Wasla.Application.Features.Finance;
using Wasla.Domain.Security;

namespace Wasla.Api.Controllers;

[ApiController, ApiVersion("1.0"), Authorize, ResponseCache(NoStore = true)]
[Route("api/v{version:apiVersion}/families/mine/dependents/{childPatientId:guid}")]
public sealed class DependentFinanceController(IDependentFinanceReadService finance) : ControllerBase
{
    [HttpGet("financial-transactions"), Permission(FamilyExtensionPermissionNames.Transactions)]
    public async Task<IActionResult> Transactions(Guid childPatientId, [FromQuery] FinancialTransactionFilter filter, CancellationToken ct)
        => (await finance.TransactionsAsync(childPatientId, filter, ct)).ToIActionResult(ct);
    [HttpGet("payments/{paymentId:guid}/receipt"), Permission(FamilyExtensionPermissionNames.Receipts)]
    public async Task<IActionResult> Payment(Guid childPatientId, Guid paymentId, CancellationToken ct)
        => (await finance.PaymentAsync(childPatientId, paymentId, ct)).ToIActionResult(ct);
    [HttpGet("refunds/{refundId:guid}/receipt"), Permission(FamilyExtensionPermissionNames.Receipts)]
    public async Task<IActionResult> Refund(Guid childPatientId, Guid refundId, CancellationToken ct)
        => (await finance.RefundAsync(childPatientId, refundId, ct)).ToIActionResult(ct);
}
