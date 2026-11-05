using System.Reflection;
using BuildingBlock.Application.Time;
using Wasla.Application.Features.Subscriptions;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Security;
using Wasla.Domain.Subscriptions;

namespace Wasla.Tests.Unit;

public sealed class CommercialFeaturePermissionCoverageTests
{
    [Fact]
    public void Every_permission_is_explicitly_classified_and_existing_ids_are_unchanged()
    {
        Assert.Equal(PermissionNames.All.Order(StringComparer.Ordinal), CommercialPermissionCatalog.All.Keys.Order(StringComparer.Ordinal));
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "Wasla.sln"))) folder = folder.Parent;
        var inventory = File.ReadAllLines(Path.Combine(folder!.FullName, "docs", "subscriptions", "permission-coverage.csv"));
        Assert.Equal(PermissionNames.All.Count + 1, inventory.Length);
        foreach (var row in inventory.Skip(1))
        {
            var cells = row.Split(',');
            Assert.Equal(Guid.Parse(cells[1]), SystemPermissionIds.For(cells[0]));
            Assert.Equal(CommercialPermissionCatalog.All[cells[0]].Category.ToString(), cells[2]);
            Assert.Equal(CommercialFeatures.FeatureForPermission(cells[0]) ?? "", cells[3]);
        }
        foreach (var (permission, classification) in CommercialPermissionCatalog.All)
        {
            Assert.True(Enum.IsDefined(classification.Category));
            if (classification.Category == SubscriptionPermissionCategory.CommercialFeature)
            {
                Assert.Contains(CommercialFeatures.All, f => f.Code == classification.FeatureCode);
                Assert.NotNull(classification.RequiredAccess);
                Assert.True(Enum.IsDefined(classification.RequiredAccess.Value));
                Assert.Contains(classification.RequiredAccess.Value, new[] { FeatureAccessLevel.ReadOnly, FeatureAccessLevel.Full });
                Assert.Equal(classification.FeatureCode, CommercialFeatures.FeatureForPermission(permission));
                Assert.Equal(classification.RequiredAccess, CommercialFeatures.RequiredAccess(permission));
            }
            else { Assert.Null(classification.FeatureCode); Assert.Null(CommercialFeatures.FeatureForPermission(permission)); }
        }
    }

    [Fact]
    public void Export_account_state_and_shared_patient_permissions_keep_their_distinct_boundaries()
    {
        Assert.Equal("PDF_XLSX_EXPORT", CommercialFeatures.FeatureForPermission(PermissionNames.PrescriptionsExportOwn));
        Assert.Equal("RECEPTION_MANAGEMENT", CommercialFeatures.FeatureForPermission(SubscriptionPermissionNames.ReceptionAccountStateOwn));
        Assert.Equal(FeatureAccessLevel.Full, CommercialFeatures.RequiredAccess(SubscriptionPermissionNames.ReceptionAccountStateOwn));
        Assert.Equal("FOLLOWUP_ELIGIBILITY", CommercialFeatures.FeatureForPermission(PermissionNames.FollowUpEligibilityViewBookingEligibility));
        Assert.Null(CommercialFeatures.FeatureForPermission(Phase18PartTwoPermissionNames.TicketsOwn));
        Assert.Null(CommercialFeatures.FeatureForPermission(Phase18PartTwoPermissionNames.TicketsPlatform));
    }

    [Theory]
    [InlineData(Phase18PartTwoPermissionNames.TicketsPractice, "TICKET_QUEUE")]
    [InlineData(Phase18PartTwoPermissionNames.TicketsAssigned, "TICKET_QUEUE")]
    [InlineData(Phase18PartTwoPermissionNames.ReservationsPractice, "RESERVATION_MANAGEMENT")]
    [InlineData(Phase18PartTwoPermissionNames.ReservationsAssigned, "RESERVATION_MANAGEMENT")]
    public void Archive_permissions_use_exact_readonly_mappings(string permission, string feature)
    {
        Assert.Equal(feature, CommercialFeatures.FeatureForPermission(permission));
        Assert.Equal(FeatureAccessLevel.ReadOnly, CommercialFeatures.RequiredAccess(permission));
    }

    [Theory]
    [InlineData("Archives.Tickets.NewViewPractice")]
    [InlineData("DoctorPracticeTickets.FutureAction")]
    [InlineData("FutureCommercial.Feature.View")]
    public async Task Unknown_permissions_fail_closed_without_inheriting_a_prefix_or_querying_patient_data(string permission)
    {
        var user = ApplicationUser.Create(Guid.NewGuid(), "doctor", "doctor@example.test", null, "hash", UserType.Doctor, false, DateTime.UtcNow).Value;
        var snapshot = new UserAccessSnapshot(user, [SystemRoleNames.Doctor], [permission], Guid.NewGuid(), DoctorApprovalStatus.Approved, null, null, null, false);
        var guard = new SubscriptionPermissionGuard(DispatchProxy.Create<ISubscriptionDataStore, NoCalls>(),
            DispatchProxy.Create<IWaslaDataStore, NoCalls>(),
            DispatchProxy.Create<ISubscriptionOperationalContinuityGuard, NoCalls>(), DispatchProxy.Create<IDateTimeProvider, NoCalls>());
        Assert.False(await guard.AllowedAsync(snapshot, permission, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Historical_read_intervals_are_inclusive_at_start_exclusive_at_end_and_legacy_is_resource_bounded()
    {
        var start = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc); var end = start.AddDays(20);
        var read = new SubscriptionReadScope(false, [new("TICKET_QUEUE", start, end)]);
        Assert.False(read.Covers(start.AddTicks(-1))); Assert.True(read.Covers(start)); Assert.False(read.Covers(end));
        Assert.True(read.Predicate<CreatedRecord>(r => r.CreatedOnUtc).Compile()(new(start)));
        Assert.False(read.Predicate<CreatedRecord>(r => r.CreatedOnUtc).Compile()(new(end)));
        var legacy = new SubscriptionReadScope(false, [new("TICKET_QUEUE", null, end)]);
        Assert.True(legacy.Covers(start.AddYears(-10))); Assert.False(legacy.Covers(end));
    }
    private sealed record CreatedRecord(DateTime CreatedOnUtc);
    public class NoCalls : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => throw new InvalidOperationException(targetMethod?.Name);
    }
}
