using Microsoft.Extensions.Options;
using Wasla.Application.Features.Reporting;
using Wasla.Domain.Security;
using BuildingBlock.Application.Time;
using System.Reflection;
using Wasla.Domain.Reservations;
using Wasla.Domain.Tickets;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

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
        var start = PermissionNames.All.ToList().IndexOf(Phase18PermissionNames.All[0]);
        Assert.Equal(Phase18PermissionNames.All, PermissionNames.All.Skip(start).Take(Phase18PermissionNames.All.Count));
        Assert.All(Phase18PermissionNames.ReceptionDefaults, p => Assert.Contains(p, PermissionNames.ReceptionAssignmentScoped));
        Assert.DoesNotContain(Phase18PermissionNames.ExpensesView, PermissionNames.ReceptionAssignmentScoped);
        Assert.DoesNotContain(Phase18PermissionNames.DoctorExportFinancial, Phase18PermissionNames.PlatformDefaults);
    }

    [Theory]
    [InlineData(ReportGrouping.Day, 0)] [InlineData(ReportGrouping.Day, 1)] [InlineData(ReportGrouping.Day, 2)]
    [InlineData(ReportGrouping.Week, 0)] [InlineData(ReportGrouping.Week, 1)] [InlineData(ReportGrouping.Week, 2)]
    [InlineData(ReportGrouping.Month, 0)] [InlineData(ReportGrouping.Month, 1)] [InlineData(ReportGrouping.Month, 2)]
    public void ReportingCritical_Series_matches_full_source_calculation_across_business_dates(ReportGrouping grouping, int snapshot)
    {
        var registry = new KpiDefinitionRegistry();
        var calculator = new OperationalKpiCalculator(registry);
        var service = new ReportingQueryService(null!, null!, null!, registry);
        var created = new DateTime(2025, 12, 30, 20, 0, 0, DateTimeKind.Utc);
        var scheduled = created.AddDays(32);
        var reservation = new ReservationReportingFact(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), "NewConsultation", ReservationBookingSource.Patient, Guid.NewGuid(), created,
            new(2025, 12, 30), new(2026, 1, 31), scheduled, created.AddDays(1), 10, ReservationStatus.Active, "Africa/Cairo",
            [new(ReservationHistoryEventType.Rescheduled, created.AddDays(1), ReservationStatus.Active, new(2025, 12, 31),
                created.AddDays(1), new(2026, 1, 31), scheduled, default, null, null, null)]);
        ReservationReportingFact[] reservations = [reservation, reservation with { Id = Guid.NewGuid(), Status = null,
            CreatedBusinessDate = new(2025, 12, 29), ScheduledBusinessDate = new(2026, 1, 1), OriginalStartUtc = null }];
        var checkIn = new DateTime(2025, 12, 31, 21, 0, 0, DateTimeKind.Utc);
        var ticket = new TicketReportingFact(Guid.NewGuid(), reservation.DoctorId, reservation.PracticeId, reservation.PatientId,
            reservation.Id, reservation.SegmentId, reservation.VisitTypeId, "NewConsultation", 0, TicketSource.WalkIn,
            new(2025, 12, 31), checkIn, TicketStatus.Completed, "Africa/Cairo", 1,
            [new(TicketHistoryEventType.Called, checkIn.AddMinutes(5), TicketStatus.Called, null),
                new(TicketHistoryEventType.InProgress, checkIn.AddMinutes(10), TicketStatus.InProgress, null),
                new(TicketHistoryEventType.Completed, checkIn.AddHours(2), TicketStatus.Completed, null)]);
        TicketReportingFact[] tickets = [ticket, ticket with { Id = Guid.NewGuid(), ReservationId = null, TicketNumber = 2,
            BusinessDate = new(2026, 1, 31), CheckInAtUtc = checkIn.AddDays(31), Events =
                [new(TicketHistoryEventType.Completed, checkIn.AddDays(31).AddHours(2), TicketStatus.Completed, null)] },
            ticket with { Id = Guid.NewGuid(), ReservationId = null, BusinessDate = new(2026, 1, 1), Status = null, Events = [] }];
        var asOf = new DateTime(2026, 2, 4, 12, 0, 0, DateTimeKind.Utc);
        if (snapshot > 0)
        {
            // Snapshot 1 precedes rescheduling/check-in; snapshot 2 precedes the cross-midnight completion.
            asOf = snapshot == 1 ? created.AddHours(1) : checkIn.AddMinutes(30);
            var cutoff = asOf;
            reservations = reservations.Select(r => r with
            {
                Events = r.Events.Where(e => e.RecordedAtUtc <= cutoff).ToArray(),
                ScheduledBusinessDate = snapshot == 1 ? new(2025, 12, 31) : r.ScheduledBusinessDate,
                ScheduledStartUtc = snapshot == 1 ? r.OriginalStartUtc ?? r.ScheduledStartUtc : r.ScheduledStartUtc
            }).ToArray();
            tickets = tickets.Where(t => t.CheckInAtUtc <= cutoff).Select(t => t with
            {
                Events = t.Events.Where(e => e.RecordedAtUtc <= cutoff).ToArray(),
                Status = t.Status == null ? null : t.Events.Where(e => e.RecordedAtUtc <= cutoff)
                    .OrderBy(e => e.RecordedAtUtc).LastOrDefault()?.ToStatus ?? TicketStatus.Waiting
            }).ToArray();
        }
        var window = new ReportingWindow(new(2025, 12, 30), new(2026, 2, 3), asOf, asOf.AddDays(20), "Africa/Cairo");
        var sourceType = typeof(ReportingQueryService).GetNestedType("OperationalSources", BindingFlags.NonPublic)!;
        var source = Activator.CreateInstance(sourceType, [reservations, tickets, 0L]);
        var build = typeof(ReportingQueryService).GetMethod("BuildSeries", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var actual = (ReportSeriesPoint[])build.Invoke(service, [source, grouping, window])!;
        string Key(DateOnly d) => grouping == ReportGrouping.Month ? d.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture)
            : (grouping == ReportGrouping.Week ? d.AddDays(-((int)d.DayOfWeek + 6) % 7) : d)
                .ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var periods = Enumerable.Range(window.FromDate.DayNumber, window.ToDate.DayNumber - window.FromDate.DayNumber + 1)
            .Select(DateOnly.FromDayNumber).GroupBy(Key).ToArray();
        Assert.Equal(periods.Select(g => g.Key), actual.Select(p => p.Key));
        foreach (var (period, point) in periods.Zip(actual))
        {
            var reference = calculator.Calculate(reservations, tickets, window with { FromDate = period.First(), ToDate = period.Last() })
                .Where(m => m.MetricCode is "BookingsCreated" or "AppointmentsScheduled" or "CheckIns" or "TicketsCompleted" or "WalkInShare" or "WaitToFirstCall");
            // Record equality checks values, numerators, denominators, samples, statuses and warnings.
            Assert.Equal(reference, point.Metrics);
        }
        if (snapshot == 0) Assert.Contains(actual, p => p.Metrics.Any(m => m.MetricCode == "TicketsCompleted" && m.Value > 0));
        else Assert.All(actual, p => Assert.Equal(0, p.Metrics.Single(m => m.MetricCode == "TicketsCompleted").Value));
        Assert.Contains(actual, p => p.Metrics.Any(m => m.Value == 0));
        Assert.Contains(actual, p => p.Metrics.Any(m => m.Value == null));
        Assert.Empty((ReportSeriesPoint[])build.Invoke(service, [source, ReportGrouping.Practice, window])!);
    }

    private sealed class Clock : IDateTimeProvider { public DateTime UtcNow => new(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc); }
}
