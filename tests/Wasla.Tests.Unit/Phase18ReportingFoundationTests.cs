using Microsoft.Extensions.Options;
using Wasla.Application.Features.Reporting;
using Wasla.Domain.Security;
using BuildingBlock.Application.Time;

namespace Wasla.Tests.Unit;

public sealed class Phase18ReportingFoundationTests
{
    [Fact]
    public void Overlapping_intervals_are_unioned_and_invalid_intervals_excluded()
    {
        var start = new DateTime(2026, 10, 13, 10, 0, 0, DateTimeKind.Utc);
        Assert.Equal(90, ReportingTimeScope.UnionMinutes([(start, start.AddHours(1)),
            (start.AddMinutes(30), start.AddMinutes(90)), (start, start.AddMinutes(-5))]));
    }

    [Fact]
    public void Inclusive_window_allows_366_days_but_rejects_future_asof_and_unknown_zone()
    {
        var time = new ReportingTimeScope(new Clock(), Options.Create(new Phase18Options()));
        Assert.True(time.Resolve(new(new DateOnly(2025, 10, 13), new DateOnly(2026, 10, 13))).IsSuccess);
        Assert.True(time.Resolve(new(new DateOnly(2025, 10, 12), new DateOnly(2026, 10, 13))).IsFailure);
        Assert.True(time.Resolve(new(AsOfUtc: new DateTime(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc))).IsFailure);
        Assert.True(time.Resolve(new(ReportTimeZoneId: "not-a-zone")).IsFailure);
    }

    [Fact]
    public void Registry_has_unique_codes_and_distinguishes_operational_and_clinical_completion()
    {
        var registry = new KpiDefinitionRegistry();
        Assert.Equal(registry.Definitions.Count, registry.Definitions.Select(d => d.MetricCode).Distinct().Count());
        Assert.Contains("not clinical", registry.FindDefinition("TicketsCompleted").Numerator);
        Assert.Contains("MedicalEncounter", registry.FindDefinition("ClinicalWorkflowCompleted").Sources);
        Assert.Contains("original", registry.FindDefinition("BookingLeadTimeMinutes").Numerator);
        Assert.NotEqual(registry.FindDefinition("ReservationNoShowRate").Denominator, registry.FindDefinition("TicketNoShowRate").Denominator);
    }

    [Fact]
    public void New_permissions_are_appended_and_private_finance_is_never_assignment_scoped()
    {
        Assert.Equal(Phase18PermissionNames.All, PermissionNames.All.Take(PermissionNames.All.Count - Phase18PartTwoPermissionNames.All.Count).TakeLast(Phase18PermissionNames.All.Count));
        Assert.All(Phase18PermissionNames.ReceptionDefaults, p => Assert.Contains(p, PermissionNames.ReceptionAssignmentScoped));
        Assert.DoesNotContain(Phase18PermissionNames.ExpensesView, PermissionNames.ReceptionAssignmentScoped);
        Assert.DoesNotContain(Phase18PermissionNames.DoctorExportFinancial, Phase18PermissionNames.PlatformDefaults);
    }

    private sealed class Clock : IDateTimeProvider { public DateTime UtcNow => new(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc); }
}
