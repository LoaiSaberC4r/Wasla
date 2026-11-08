using BuildingBlock.Domain.Results;
using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Finance;
using Wasla.Domain.Security;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed partial class FinanceReadService
{
    public async Task<Result<FinancialPage<PatientFinancialTransactionItem>>> TransactionsAsync(Guid child, FinancialTransactionFilter filter, CancellationToken ct)
    {
        var scope = await dependents.AuthorizeAsync(child, FamilyExtensionPermissionNames.Transactions, ct);
        if (scope.IsFailure) return Result<FinancialPage<PatientFinancialTransactionItem>>.Fail(scope.Errors);
        if (!ValidFilter(filter)) return Result<FinancialPage<PatientFinancialTransactionItem>>.Fail(InvalidFilter);
        var rows = await PageAsync(Transactions(filter with { PatientId = child }, null, child), filter.PageNumber, filter.PageSize, ct);
        if ((await dependents.AuthorizeAsync(child, FamilyExtensionPermissionNames.Transactions, ct)).IsFailure)
            return Result<FinancialPage<PatientFinancialTransactionItem>>.Fail(NotFound);
        return Result<FinancialPage<PatientFinancialTransactionItem>>.Ok(new(rows.Items.Select(r => ToPatientItem(r) with { RefundReason = null }).ToArray(), rows.TotalCount, rows.PageNumber, rows.PageSize));
    }
    public async Task<Result<PaymentReceiptData>> PaymentAsync(Guid child, Guid payment, CancellationToken ct)
    {
        var scope = await dependents.AuthorizeAsync(child, FamilyExtensionPermissionNames.Receipts, ct);
        if (scope.IsFailure) return Result<PaymentReceiptData>.Fail(scope.Errors);
        var receipt = await PaymentReceiptQuery(payment, null, child, includeStaff: false).SingleOrDefaultAsync(ct);
        if (receipt is null || (await dependents.AuthorizeAsync(child, FamilyExtensionPermissionNames.Receipts, ct)).IsFailure)
            return Result<PaymentReceiptData>.Fail(NotFound);
        return Result<PaymentReceiptData>.Ok(receipt);
    }
    public async Task<Result<RefundReceiptData>> RefundAsync(Guid child, Guid refund, CancellationToken ct)
    {
        var scope = await dependents.AuthorizeAsync(child, FamilyExtensionPermissionNames.Receipts, ct);
        if (scope.IsFailure) return Result<RefundReceiptData>.Fail(scope.Errors);
        var receipt = await RefundReceiptQuery(refund, null, child, includeStaff: false).SingleOrDefaultAsync(ct);
        if (receipt is null || (await dependents.AuthorizeAsync(child, FamilyExtensionPermissionNames.Receipts, ct)).IsFailure)
            return Result<RefundReceiptData>.Fail(NotFound);
        return Result<RefundReceiptData>.Ok(receipt with { RefundReason = null });
    }
}
