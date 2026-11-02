using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using System.Data.Common;
using System.Reflection;
using BuildingBlock.Domain.Results;
using Wasla.Application.Features.Reporting;
using Wasla.Domain.Operations;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;
using static Wasla.Tests.Integration.Phase13ApiFixture;

namespace Wasla.Tests.Integration;

public sealed class Phase18DoctorReportingApiTests
{
    private const string Base = "/api/v1/doctors/me";
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task ReportingCritical_Financial_asof_preserves_old_amounts_and_excludes_later_settlements(bool sql)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sql); using var doctor = await app.ClientAsync("doctor");
        var recorded = app.Clock.UtcNow;
        using var created = await PostAsync(doctor, Base + "/expenses", new
        { amount = 500, businessDate = app.Today, dueBusinessDate = app.Today, category = "Utilities", descriptionAr = "فاتورة", reason = "invoice",
            allocations = new[] { new { practiceId = app.PracticeId, percentage = 100 } } }, "historical");
        Assert.True(created.StatusCode == HttpStatusCode.OK, await created.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var id = (await JsonAsync(created)).GetProperty("id").GetGuid();
        var original = await GetAsync(doctor, Base + $"/expenses/obligations/{id}");
        app.Clock.UtcNow = app.Clock.UtcNow.AddDays(1);
        using var correctionRequest = new HttpRequestMessage(HttpMethod.Patch, Base + $"/expenses/obligations/{id}")
        { Content = JsonContent.Create(new { amount = 650, dueBusinessDate = app.Today.AddDays(-1), category = "Utilities", descriptionAr = "فاتورة مصححة",
            reason = "correct invoice", rowVersion = original.GetProperty("rowVersion").GetString(), allocations = new[] { new { practiceId = app.PracticeId, percentage = 100 } } }) };
        correctionRequest.Headers.Add("Idempotency-Key", "correct");
        using var correction = await doctor.SendAsync(correctionRequest, TestContext.Current.CancellationToken);
        Assert.True(correction.StatusCode == HttpStatusCode.OK, await correction.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var query = $"?fromDate={app.Today.AddDays(-1):yyyy-MM-dd}&toDate={app.Today:yyyy-MM-dd}";
        var old = await GetAsync(doctor, Base + "/reports/operating-finance" + query + "&asOfUtc=" + Uri.EscapeDataString(recorded.ToString("O")));
        var now = await GetAsync(doctor, Base + "/reports/operating-finance" + query);
        Assert.Equal(500, Value(old, "AllOutstandingAsOf")); Assert.Equal(650, Value(now, "AllOutstandingAsOf"));
        Assert.Equal(-650, Value(now, "ProjectedResultForPeriod"));
        var history = await GetAsync(doctor, Base + $"/expenses/obligations/{id}/history");
        Assert.Contains(history.GetProperty("items").EnumerateArray(), h => h.GetProperty("obligationSnapshot").GetProperty("amount").GetDecimal() == 500);
        var current = await GetAsync(doctor, Base + $"/expenses/obligations/{id}");
        using var settled = await PostAsync(doctor, Base + $"/expenses/obligations/{id}/settle", new
        { amount = 650, paidBusinessDate = app.Today, paymentMethod = "Cash", rowVersion = current.GetProperty("rowVersion").GetString() }, "paid");
        Assert.Equal(HttpStatusCode.OK, settled.StatusCode);
        now = await GetAsync(doctor, Base + "/reports/operating-finance" + query);
        Assert.Equal(650, Value(now, "PaidOperatingExpenses")); Assert.Equal(0, Value(now, "AllOutstandingAsOf"));
        var detail = await GetAsync(doctor, Base + "/reports/operating-finance/drilldown" + query + "&metricCode=NetOperatingCashResult");
        Assert.Equal(1, detail.GetProperty("totalCount").GetInt64());
        Assert.Equal(-650, detail.GetProperty("items")[0].GetProperty("metricValue").GetDecimal());
        using var foreign = await doctor.GetAsync(Base + $"/reports/queue?practiceId={app.OtherPracticeId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Calculated_insights_are_not_persisted_until_acknowledged_and_keep_immutable_evidence(bool sql)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sql); using var doctor = await app.ClientAsync("doctor");
        var asOf = app.Clock.UtcNow;
        await app.WithDbAsync(async db =>
        {
            for (var period = 0; period < 2; ++period)
            for (var index = 0; index < 20; ++index)
            {
                var at = asOf.AddDays(period == 0 ? -10 : -3).AddMinutes(index);
                var ticket = Ticket.CreateWalkIn(new(Guid.NewGuid(), app.DoctorId, app.PracticeId, app.PatientId, null,
                    DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(at, TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"))), index + 1,
                    TicketSource.WalkIn, app.SegmentId, "عادي", "Normal", 0, app.ConsultationTypeId, "NewConsultation", "كشف", "Visit", 300,
                    "Africa/Cairo", at, at, CheckInMode.WalkIn, app.DoctorUserId, null)).Value;
                var wait = period == 0 ? 10 : 15;
                Assert.True(ticket.Call(app.DoctorUserId, at.AddMinutes(wait)).IsSuccess);
                Assert.True(ticket.StartVisit(app.DoctorUserId, at.AddMinutes(wait + 1)).IsSuccess);
                Assert.True(ticket.Complete(app.DoctorUserId, at.AddMinutes(wait + 10)).IsSuccess);
                db.Tickets.Add(ticket);
            }
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        var list = await GetAsync(doctor, Base + "/insights");
        var signal = list.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("metricCode").GetString() == "WaitToFirstCall");
        Assert.False(signal.GetProperty("isPersisted").GetBoolean());
        var id = signal.GetProperty("insightId").GetGuid();
        await app.WithDbAsync(async db => Assert.Empty(await db.Set<Wasla.Domain.Reporting.DoctorInsightFollowUp>().ToArrayAsync(TestContext.Current.CancellationToken)));
        using var ack = await PostAsync(doctor, Base + $"/insights/{id}/acknowledge", new { reason = "review queue workflow" }, "ack");
        Assert.True(ack.StatusCode == HttpStatusCode.OK, await ack.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var persisted = await JsonAsync(ack); Assert.True(persisted.GetProperty("isPersisted").GetBoolean());
        using var resolved = await PostAsync(doctor, Base + $"/insights/{id}/resolve", new
        { rowVersion = persisted.GetProperty("rowVersion").GetString(), reason = "workflow reviewed" }, "resolved");
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
        var detail = await GetAsync(doctor, Base + $"/insights/{id}");
        Assert.Equal("Resolved", detail.GetProperty("state").GetString()); Assert.Equal(15, detail.GetProperty("currentValue").GetDecimal());
        var history = await GetAsync(doctor, Base + $"/insights/{id}/history"); Assert.Equal(3, history.GetProperty("totalCount").GetInt64());
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task ReportingCritical_Pages_archive_and_drilldown_preserve_scope_and_source_reads(bool sql)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sql);
        using var doctor = await app.ClientAsync("doctor");
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).Order().ToArray();
        var date = app.Today.AddDays(-2);
        await app.WithDbAsync(async db =>
        {
            for (var index = 0; index < ids.Length; ++index)
            {
                var at = app.Clock.UtcNow.AddDays(-2).AddMinutes(index);
                var ticket = Ticket.CreateWalkIn(new(ids[index], app.DoctorId, app.PracticeId, app.PatientId, null,
                    date, index + 1, TicketSource.WalkIn, app.SegmentId, "عادي", "Normal", 0, app.ConsultationTypeId,
                    "NewConsultation", "كشف", "Visit", 300, "Africa/Cairo", at, at, CheckInMode.WalkIn, app.DoctorUserId, null)).Value;
                Assert.True(ticket.Call(app.DoctorUserId, at.AddMinutes(5)).IsSuccess);
                Assert.True(ticket.StartVisit(app.DoctorUserId, at.AddMinutes(6)).IsSuccess);
                Assert.True(ticket.Complete(app.DoctorUserId, at.AddMinutes(15)).IsSuccess);
                db.Tickets.Add(ticket);
            }
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            foreach (var identity in await db.TicketRegistry.Where(t => ids.Contains(t.Id)).ToArrayAsync(TestContext.Current.CancellationToken))
                identity.MarkArchived(app.Clock.UtcNow, "ReportingCritical", 1);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        var query = $"?fromDate={date:yyyy-MM-dd}&toDate={date:yyyy-MM-dd}&pageSize=2&sortDirection=asc";
        List<Guid> returned = [];
        for (var page = 1; page <= 3; ++page)
        {
            var report = await GetAsync(doctor, Base + "/reports/queue" + query + "&pageNumber=" + page);
            Assert.Equal(5, report.GetProperty("detailRowCount").GetInt64());
            Assert.Equal(5, Value(report, "CheckIns"));
            foreach (var row in report.GetProperty("records").EnumerateArray())
            {
                returned.Add(row.GetProperty("id").GetGuid());
                Assert.True(row.GetProperty("isArchived").GetBoolean());
                Assert.Contains("/tickets/", row.GetProperty("detailRoute").GetString());
                Assert.True(row.GetProperty("availableActions")[0].GetProperty("allowed").GetBoolean());
            }
        }
        Assert.Equal(ids, returned);
        var empty = await GetAsync(doctor, Base + "/reports/queue" + query + "&pageNumber=4");
        Assert.Empty(empty.GetProperty("records").EnumerateArray());
        Assert.Equal(5, empty.GetProperty("detailRowCount").GetInt64());
        await app.WithDbAsync(async db =>
        {
            var commands = new ReportingCommands();
            await using var observed = Observe(db, app, commands);
            var scope = new ReportScope(ReportActor.Doctor, app.DoctorUserId, app.DoctorId, null, [app.PracticeId],
                [Phase18PermissionNames.DoctorReports, PermissionNames.DoctorPracticeTicketsViewOwn, Phase18PartTwoPermissionNames.TicketsPractice]);
            var time = new ReportingTimeScope(app.Clock, Options.Create(new Phase18Options()));
            var service = QueryService(observed, new FixedReportingGuard(scope), time);
            var filter = new ReportFilter(date, date, PageNumber: 2, PageSize: 2, SortDirection: "asc");
            var result = await service.DrilldownAsync(ReportActor.Doctor, "queue", "CheckIns", filter, TestContext.Current.CancellationToken);
            Assert.True(result.IsSuccess);
            Assert.Equal(5, result.Value.TotalCount);
            Assert.Equal(ids.Skip(2).Take(2), result.Value.Items.Select(r => r.Id));
            Assert.All(result.Value.Items, r => { Assert.True(r.IsArchived); Assert.NotNull(r.DetailRoute); });
            // Only the operational source query selects the ticket's time-zone snapshot.
            Assert.Single(commands.Texts.Where(s => s.Contains("PracticeTimeZoneIdSnapshot", StringComparison.Ordinal)));
            var denied = QueryService(observed, new FixedReportingGuard(scope with
                { Permissions = [Phase18PermissionNames.DoctorReports, PermissionNames.DoctorPracticeTicketsViewOwn] }), time);
            result = await denied.DrilldownAsync(ReportActor.Doctor, "queue", "CheckIns", filter, TestContext.Current.CancellationToken);
            Assert.True(result.IsSuccess);
            Assert.All(result.Value.Items, r => { Assert.True(r.IsArchived); Assert.Null(r.DetailRoute);
                Assert.All(r.AvailableActions!, a => Assert.False(a.Allowed)); });
        });
        using var foreign = await doctor.GetAsync(Base + "/reports/queue/drilldown" + query + $"&metricCode=CheckIns&practiceId={app.OtherPracticeId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        foreach (var suffix in new[] { "&metricCode=UnknownMetric", "&metricCode=CheckIns&expenseCategory=Utilities" })
        {
            using var invalid = await doctor.GetAsync(Base + "/reports/queue/drilldown" + query + suffix, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
        }
        using var reception = await app.ClientAsync("reception");
        using var financial = await reception.GetAsync("/api/v1/reception/reports/queue/drilldown" + query + "&metricCode=NetCollected", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, financial.StatusCode);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task ReportingCritical_Investigation_history_is_permission_gated_and_counted_asof(bool sql)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sql);
        await app.WithDbAsync(async db =>
        {
            var admin = await db.SuperAdmins.SingleAsync(TestContext.Current.CancellationToken);
            var now = app.Clock.UtcNow;
            var investigation = AdministrativeInvestigation.Create(Guid.NewGuid(), "مراجعة", "Review", "Administrative review",
                InvestigationIssueType.DataQuality, InvestigationPriority.Normal, InvestigationResourceType.None, null, admin.ApplicationUserId, now.AddDays(-5)).Value;
            Assert.True(investigation.Assign(admin.Id, true, admin.ApplicationUserId, now.AddDays(-5).AddMinutes(1)).IsSuccess);
            Assert.True(investigation.StartReview(true, admin.ApplicationUserId, now.AddDays(-4)).IsSuccess);
            Assert.True(investigation.Resolve(InvestigationResolutionType.Verified, "Verified", true, admin.ApplicationUserId, now.AddDays(-3)).IsSuccess);
            Assert.True(investigation.Close("Closed", admin.ApplicationUserId, now.AddDays(-2)).IsSuccess);
            Assert.True(investigation.Reopen("Review again", true, admin.ApplicationUserId, now.AddDays(-1)).IsSuccess);
            Assert.True(investigation.Resolve(InvestigationResolutionType.Verified, "Verified again", true, admin.ApplicationUserId, now.AddDays(-1).AddHours(1)).IsSuccess);
            Assert.True(investigation.Close("Future closure", admin.ApplicationUserId, now.AddDays(1)).IsSuccess);
            var closed = AdministrativeInvestigation.Create(Guid.NewGuid(), "مراجعة أخرى", "Other review", "Administrative review",
                InvestigationIssueType.DataQuality, InvestigationPriority.Normal, InvestigationResourceType.None, null, admin.ApplicationUserId, now.AddDays(-5)).Value;
            Assert.True(closed.Close("Withdrawn", admin.ApplicationUserId, now.AddDays(-2)).IsSuccess);
            db.Set<AdministrativeInvestigation>().AddRange(investigation, closed);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            var commands = new ReportingCommands();
            await using var observed = Observe(db, app, commands);
            var scope = new ReportScope(ReportActor.SuperAdmin, admin.ApplicationUserId, null, null, [app.PracticeId, app.OtherPracticeId], []);
            var window = new ReportingWindow(app.Today.AddDays(-7), app.Today, now, now, "Africa/Cairo");
            var service = QueryService(observed, new FixedReportingGuard(scope), new ReportingTimeScope(app.Clock, Options.Create(new Phase18Options())));
            var method = service.GetType().GetMethod("AdditionalPlatformMetricsAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
            async Task<IReadOnlyList<ReportMetric>> Metrics(ReportScope authorized, ReportingWindow w)
                => await (Task<IReadOnlyList<ReportMetric>>)method.Invoke(service, [authorized, "adoption", w, TestContext.Current.CancellationToken])!;
            var metrics = await Metrics(scope, window);
            Assert.DoesNotContain(metrics, m => m.MetricCode is "OpenInvestigations" or "InvestigationResolutions");
            Assert.DoesNotContain(commands.Texts, s => s.Contains("InvestigationHistor", StringComparison.Ordinal));
            commands.Texts.Clear();
            scope = scope with { Permissions = [Phase18PermissionNames.InvestigationsView] };
            metrics = await Metrics(scope, window);
            Assert.Equal(1, metrics.Single(m => m.MetricCode == "OpenInvestigations").Value);
            Assert.Equal(2, metrics.Single(m => m.MetricCode == "InvestigationResolutions").Value);
            var counts = commands.Texts.Where(s => s.Contains("InvestigationHistor", StringComparison.Ordinal)).ToArray();
            Assert.Equal(2, counts.Length);
            Assert.All(counts, s => Assert.True(s.Contains("COUNT(", StringComparison.Ordinal) || s.Contains("COUNT_BIG(", StringComparison.Ordinal)));
            metrics = await Metrics(scope, window with { AsOfUtc = now.AddDays(-2).AddHours(1) });
            Assert.Equal(0, metrics.Single(m => m.MetricCode == "OpenInvestigations").Value);
            Assert.Equal(1, metrics.Single(m => m.MetricCode == "InvestigationResolutions").Value);
            metrics = await Metrics(scope, window with { AsOfUtc = now.AddDays(-4).AddHours(1) });
            Assert.Equal(2, metrics.Single(m => m.MetricCode == "OpenInvestigations").Value);
            Assert.Equal(0, metrics.Single(m => m.MetricCode == "InvestigationResolutions").Value);
        });
    }

    private static WaslaDbContext Observe(WaslaDbContext db, Phase13ApiFixture app, ReportingCommands commands)
    {
        var options = new DbContextOptionsBuilder<WaslaDbContext>();
        if (db.Database.IsSqlServer()) options.UseSqlServer(db.Database.GetDbConnection());
        else options.UseSqlite(db.Database.GetDbConnection());
        return new(options.AddInterceptors(commands).Options, app.Clock);
    }
    private static IReportingQueryService QueryService(WaslaDbContext db, IReportingAccessGuard guard, IReportingTimeScope time)
        => (IReportingQueryService)Activator.CreateInstance(typeof(WaslaDbContext).Assembly.GetType(
            "Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.ReportingQueryService")!, [db, guard, time, new KpiDefinitionRegistry()])!;
    private sealed class FixedReportingGuard(ReportScope scope) : IReportingAccessGuard
    {
        public Task<Result<ReportScope>> AuthorizeAsync(ReportActor actor, string permission, Guid? practiceId, Guid? doctorId, CancellationToken cancellationToken)
            => Task.FromResult(Result<ReportScope>.Ok(scope));
    }
    private sealed class ReportingCommands : DbCommandInterceptor
    {
        public List<string> Texts { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Texts.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
    private static decimal Value(JsonElement report, string code) => report.GetProperty("metrics").EnumerateArray()
        .Single(m => m.GetProperty("metricCode").GetString() == code).GetProperty("value").GetDecimal();
}
