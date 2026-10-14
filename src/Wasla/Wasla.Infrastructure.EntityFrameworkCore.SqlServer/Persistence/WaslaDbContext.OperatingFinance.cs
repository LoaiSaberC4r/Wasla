using Microsoft.EntityFrameworkCore;
using Wasla.Domain.OperatingFinance;
using Wasla.Domain.Reporting;
using Wasla.Domain.Operations;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

public sealed partial class WaslaDbContext
{
    private void EnsureOperatingFinanceIsAppendOnly()
    {
        foreach (var e in ChangeTracker.Entries())
        {
            if (e.State is EntityState.Modified or EntityState.Deleted && e.Entity is OperatingPlanRevision or
                OperatingPlanAllocation or OperatingObligationRevision or OperatingCostAllocationSnapshot or
                OperatingSettlement or OperatingSettlementReversal or OperatingFinanceHistory or DoctorInsightHistory or PracticeReportingPolicySnapshot or ReceptionHandoverHistory or InvestigationHistory or ReportExportAudit)
                throw new InvalidOperationException("Operating finance snapshots, movements and histories are immutable.");
            if (e.State == EntityState.Deleted && e.Entity is OperatingFinancialPlan or OperatingObligation or DoctorInsightFollowUp or ReceptionHandover or AdministrativeInvestigation)
                throw new InvalidOperationException("Operating financial records cannot be deleted.");
            if (e.State == EntityState.Modified && e.Entity is DoctorInsightFollowUp &&
                (e.Property(nameof(DoctorInsightFollowUp.EvidenceJson)).IsModified || e.Property(nameof(DoctorInsightFollowUp.MetricCode)).IsModified ||
                 e.Property(nameof(DoctorInsightFollowUp.DoctorId)).IsModified || e.Property(nameof(DoctorInsightFollowUp.PracticeId)).IsModified))
                throw new InvalidOperationException("Insight evidence and ownership are immutable.");
        }
    }
}
