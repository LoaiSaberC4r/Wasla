using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Subscriptions;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

public sealed partial class WaslaDbContext
{
    public DbSet<CommercialFeature> CommercialFeatures => Set<CommercialFeature>();
    public DbSet<CommercialPlan> CommercialPlans => Set<CommercialPlan>();
    public DbSet<CommercialProduct> CommercialProducts => Set<CommercialProduct>();
    public DbSet<DoctorSubscription> DoctorSubscriptions => Set<DoctorSubscription>();
    public DbSet<FeaturePermissionMapping> FeaturePermissionMappings => Set<FeaturePermissionMapping>();
    public DbSet<LegacyTransitionEnrollmentBatch> LegacyTransitionEnrollmentBatches => Set<LegacyTransitionEnrollmentBatch>();
    public DbSet<PlanFeatureEntitlement> PlanFeatureEntitlements => Set<PlanFeatureEntitlement>();
    public DbSet<SubscriptionChangeRequest> SubscriptionChangeRequests => Set<SubscriptionChangeRequest>();
    public DbSet<SubscriptionFeatureSnapshot> SubscriptionFeatureSnapshots => Set<SubscriptionFeatureSnapshot>();
    public DbSet<SubscriptionHistory> SubscriptionHistories => Set<SubscriptionHistory>();
    public DbSet<SubscriptionPayment> SubscriptionPayments => Set<SubscriptionPayment>();
    public DbSet<SubscriptionReminderSchedule> SubscriptionReminderSchedules => Set<SubscriptionReminderSchedule>();
}
