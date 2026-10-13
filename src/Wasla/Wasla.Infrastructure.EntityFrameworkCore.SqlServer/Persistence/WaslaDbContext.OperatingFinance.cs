using Microsoft.EntityFrameworkCore;
using Wasla.Domain.OperatingFinance;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

public sealed partial class WaslaDbContext
{
    private void EnsureOperatingFinanceIsAppendOnly()
    {
        foreach (var e in ChangeTracker.Entries())
        {
            if (e.State is EntityState.Modified or EntityState.Deleted && e.Entity is OperatingPlanRevision or
                OperatingPlanAllocation or OperatingObligationRevision or OperatingCostAllocationSnapshot or
                OperatingSettlement or OperatingSettlementReversal or OperatingFinanceHistory)
                throw new InvalidOperationException("Operating finance snapshots, movements and histories are immutable.");
            if (e.State == EntityState.Deleted && e.Entity is OperatingFinancialPlan or OperatingObligation)
                throw new InvalidOperationException("Operating financial records cannot be deleted.");
        }
    }
}
