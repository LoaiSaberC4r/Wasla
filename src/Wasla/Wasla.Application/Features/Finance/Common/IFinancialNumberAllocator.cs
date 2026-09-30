using Wasla.Domain.Payments;

namespace Wasla.Application.Features.Finance.Common;

public interface IFinancialNumberAllocator
{
    Task<FinancialTransactionNumber> AllocateNextAsync(
        Guid doctorPracticeId,
        DateOnly businessDate,
        FinancialTransactionType transactionType,
        CancellationToken cancellationToken = default);
}
