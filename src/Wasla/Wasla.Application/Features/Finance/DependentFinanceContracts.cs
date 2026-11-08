using BuildingBlock.Domain.Results;

namespace Wasla.Application.Features.Finance;

public interface IDependentFinanceReadService
{
    Task<Result<FinancialPage<PatientFinancialTransactionItem>>> TransactionsAsync(Guid child, FinancialTransactionFilter filter, CancellationToken ct);
    Task<Result<PaymentReceiptData>> PaymentAsync(Guid child, Guid payment, CancellationToken ct);
    Task<Result<RefundReceiptData>> RefundAsync(Guid child, Guid refund, CancellationToken ct);
}
