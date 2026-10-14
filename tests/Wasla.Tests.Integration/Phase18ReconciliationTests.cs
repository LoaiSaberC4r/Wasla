using System.Net;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Wasla.Domain.OperatingFinance;
using Wasla.Domain.Practices;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;
using static Wasla.Tests.Integration.Phase13ApiFixture;

namespace Wasla.Tests.Integration;

public sealed class Phase18ReconciliationTests
{
    private const string Doctor = "/api/v1/doctors/me";
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Shared_cost_reports_movement_lists_and_drilldown_reconcile_to_minor_units(bool sql)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sql); using var doctor = await app.ClientAsync("doctor");
        var otherOwned = Guid.NewGuid();
        await app.WithDbAsync(async db =>
        {
            db.DoctorPractices.Add(DoctorPractice.Create(otherOwned, app.DoctorId, "عيادة ثانية", "Second practice", 1, 1, 1, "Address", 30, 31, app.DoctorUserId).Value);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using var created = await PostAsync(doctor, Doctor + "/expenses", new
        {
            amount = 10.01m, businessDate = app.Today, dueBusinessDate = app.Today, category = "Utilities", descriptionAr = "فاتورة",
            allocations = new[] { new { practiceId = app.PracticeId, percentage = 60 }, new { practiceId = otherOwned, percentage = 40 } },
            reason = "invoice", paidNow = true, payment = new { amount = 10.01m, paidBusinessDate = app.Today, paymentMethod = "Cash" }
        }, "allocate");
        Assert.True(created.StatusCode == HttpStatusCode.OK, await created.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var query = $"?fromDate={app.Today:yyyy-MM-dd}&toDate={app.Today:yyyy-MM-dd}";
        var whole = await GetAsync(doctor, Doctor + "/reports/operating-finance" + query);
        Assert.Equal(10.01m, Value(whole, "PaidOperatingExpenses"));
        var catalog = await GetAsync(doctor, Doctor + "/reports/catalog");
        var previewRoute = catalog.EnumerateArray().Single(c => c.GetProperty("reportCode").GetString() == "expenses").GetProperty("previewRoute").GetString()!;
        var expenses = await GetAsync(doctor, previewRoute + query); Assert.Equal(10.01m, Value(expenses, "PaidOperatingExpenses"));
        decimal allocated = 0;
        foreach (var practice in new[] { app.PracticeId, otherOwned })
        {
            var report = await GetAsync(doctor, Doctor + "/reports/operating-finance" + query + "&practiceId=" + practice);
            var paid = Value(report, "PaidOperatingExpenses"); allocated += paid;
            var movements = await GetAsync(doctor, Doctor + "/expenses/transactions" + query + "&practiceId=" + practice);
            Assert.Equal(paid, movements.GetProperty("items").EnumerateArray().Sum(m => m.GetProperty("amount").GetDecimal()));
            var action = Metric(report, "NetOperatingCashResult").GetProperty("availableActions")[0];
            Assert.True(action.GetProperty("allowed").GetBoolean()); Assert.Equal(System.Text.Json.JsonValueKind.Null, action.GetProperty("unavailableReasonCode").ValueKind);
            var route = action.GetProperty("route").GetString()!; Assert.Contains("practiceId=" + practice, route);
            var drilldown = await GetAsync(doctor, route);
            Assert.Equal(-paid, drilldown.GetProperty("items").EnumerateArray().Sum(r => r.GetProperty("metricValue").GetDecimal()));
        }
        Assert.Equal(10.01m, allocated);
        var overview = await GetAsync(doctor, Doctor + "/dashboard/overview" + query);
        var link = Metric(overview, "PaidOperatingExpenses").GetProperty("availableActions")[0].GetProperty("route").GetString()!;
        var detail = await GetAsync(doctor, link); Assert.Equal(1, detail.GetProperty("totalCount").GetInt64());
        var write = await JsonAsync(created); var obligationId = write.GetProperty("id").GetGuid();
        var settlementId = write.GetProperty("settlementId").GetGuid();
        var obligation = await GetAsync(doctor, Doctor + $"/expenses/obligations/{obligationId}");
        app.Clock.UtcNow = app.Clock.UtcNow.AddMinutes(1);
        using var reversed = await PostAsync(doctor, Doctor + $"/expense-settlements/{settlementId}/reverse", new
        { businessDate = app.Today, rowVersion = obligation.GetProperty("rowVersion").GetString(), reason = "correct category" }, "reverse-category");
        Assert.Equal(HttpStatusCode.OK, reversed.StatusCode);
        obligation = await GetAsync(doctor, Doctor + $"/expenses/obligations/{obligationId}");
        app.Clock.UtcNow = app.Clock.UtcNow.AddMinutes(1);
        using var change = new HttpRequestMessage(HttpMethod.Patch, Doctor + $"/expenses/obligations/{obligationId}")
        { Content = System.Net.Http.Json.JsonContent.Create(new { amount = 10.01m, dueBusinessDate = app.Today, category = "Rent", descriptionAr = "إيجار",
            reason = "correct category", rowVersion = obligation.GetProperty("rowVersion").GetString(),
            allocations = new[] { new { practiceId = app.PracticeId, percentage = 60 }, new { practiceId = otherOwned, percentage = 40 } } }) };
        change.Headers.Add("Idempotency-Key", "new-category");
        using var corrected = await doctor.SendAsync(change, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.OK, corrected.StatusCode);
        var priorCategory = await GetAsync(doctor, Doctor + "/reports/expenses" + query + "&expenseCategory=Utilities");
        Assert.Equal(0, Value(priorCategory, "PaidOperatingExpenses")); Assert.Equal(2, priorCategory.GetProperty("detailRowCount").GetInt64());
        var oldMovements = await GetAsync(doctor, Doctor + "/expenses/transactions" + query + "&category=Utilities");
        Assert.Equal(2, oldMovements.GetProperty("totalCount").GetInt64());
        var currentCategory = await GetAsync(doctor, Doctor + "/reports/expenses" + query + "&expenseCategory=Rent");
        Assert.Equal(10.01m, Value(currentCategory, "AllOutstandingAsOf"));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Comparison_and_metric_drilldown_use_equal_windows_and_current_permission_revocation(bool sql)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sql); using var doctor = await app.ClientAsync("doctor"); using var reception = await app.ClientAsync("reception");
        await app.WithDbAsync(async db =>
        {
            for (var day = 0; day < 2; ++day)
            for (var i = 0; i < (day == 0 ? 2 : 1); ++i)
            {
                var at = app.Clock.UtcNow.AddDays(-day).AddHours(-1).AddMinutes(i);
                var ticket = Ticket.CreateWalkIn(new(Guid.NewGuid(), app.DoctorId, app.PracticeId, app.PatientId, null,
                    app.Today.AddDays(-day), i + 1, TicketSource.WalkIn, app.SegmentId, "عادي", "Normal", 0, app.ConsultationTypeId,
                    "NewConsultation", "كشف", "Visit", 300, "Africa/Cairo", at, at, CheckInMode.WalkIn, app.DoctorUserId, null)).Value;
                Assert.True(ticket.Call(app.DoctorUserId, at.AddMinutes(1)).IsSuccess);
                Assert.True(ticket.StartVisit(app.DoctorUserId, at.AddMinutes(2)).IsSuccess);
                Assert.True(ticket.Complete(app.DoctorUserId, at.AddMinutes(3)).IsSuccess);
                db.Tickets.Add(ticket);
            }
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        var query = $"?fromDate={app.Today:yyyy-MM-dd}&toDate={app.Today:yyyy-MM-dd}&comparePreviousPeriod=true&practiceId={app.PracticeId}";
        var report = await GetAsync(doctor, Doctor + "/reports/queue" + query);
        Assert.Equal(2, Value(report, "CheckIns"));
        var comparison = report.GetProperty("previousPeriodComparisons").EnumerateArray().Single(c => c.GetProperty("metricCode").GetString() == "CheckIns");
        Assert.Equal(1, comparison.GetProperty("previousValue").GetDecimal()); Assert.Equal(100, comparison.GetProperty("relativeChangePercent").GetDecimal());
        var route = Metric(report, "CheckIns").GetProperty("availableActions")[0].GetProperty("route").GetString()!;
        var drilldown = await GetAsync(doctor, route); Assert.Equal(2, drilldown.GetProperty("totalCount").GetInt64());
        Assert.Equal(2, drilldown.GetProperty("items").EnumerateArray().Sum(r => r.GetProperty("metricValue").GetDecimal()));
        var before = await GetAsync(reception, "/api/v1/reception/reports/queue" + query);
        Assert.Contains(before.GetProperty("records").EnumerateArray(), r => r.GetProperty("patientId").ValueKind != System.Text.Json.JsonValueKind.Null);
        await app.WithDbAsync(async db => await db.RolePermissions.Where(r => r.RoleId == SystemRoleIds.Reception &&
            r.PermissionId == SystemPermissionIds.For(PermissionNames.PracticeTicketsView)).ExecuteDeleteAsync(TestContext.Current.CancellationToken));
        var after = await GetAsync(reception, "/api/v1/reception/reports/queue" + query);
        Assert.All(after.GetProperty("records").EnumerateArray(), r => { Assert.Equal(System.Text.Json.JsonValueKind.Null, r.GetProperty("patientId").ValueKind);
            Assert.Equal(System.Text.Json.JsonValueKind.Null, r.GetProperty("detailRoute").ValueKind); });
        await app.WithDbAsync(async db =>
        {
            var snapshot = await db.Set<Wasla.Domain.Reporting.PracticeReportingPolicySnapshot>().Where(s => s.PracticeId == app.PracticeId)
                .OrderByDescending(s => s.RevisionNumber).FirstAsync(TestContext.Current.CancellationToken);
            var laterPolicy = System.Text.Json.Nodes.JsonNode.Parse(snapshot.PolicyJson)!; laterPolicy["MaximumDailyPatients"] = 20;
            db.Add(new Wasla.Domain.Reporting.PracticeReportingPolicySnapshot(Guid.NewGuid(), app.DoctorId, app.PracticeId,
                snapshot.RevisionNumber + 1, laterPolicy.ToJsonString(), app.Clock.UtcNow.AddDays(1)));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        var forecast = await GetAsync(doctor, Doctor + $"/reports/queue?fromDate={app.Today.AddDays(7):yyyy-MM-dd}&toDate={app.Today.AddDays(7):yyyy-MM-dd}&asOfUtc={Uri.EscapeDataString(app.Clock.UtcNow.ToString("O"))}");
        Assert.Equal(900, Value(forecast, "CapacityMinutes"));
        Assert.Equal("Report.CurrentCapacityForecastAsOfSnapshot", Metric(forecast, "CapacityMinutes").GetProperty("warningCode").GetString());
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Platform_small_patient_cohorts_suppress_counts_samples_and_complementary_breakdowns(bool sql)
    {
        await using var app = await Phase15DiagnosticApiTests.CreateAsync(sql); using var root = await app.ClientAsync("root"); using var reception = await app.ClientAsync("reception");
        await app.WalkInAsync(reception);
        var report = await GetAsync(root, "/api/v1/admin/dashboard/overview");
        var checkins = Metric(report, "CheckIns");
        Assert.Equal(System.Text.Json.JsonValueKind.Null, checkins.GetProperty("value").ValueKind);
        Assert.Equal(0, checkins.GetProperty("eligibleSampleCount").GetInt64()); Assert.Equal(0, report.GetProperty("eligibleSampleCount").GetInt64());
        Assert.Empty(report.GetProperty("series").EnumerateArray()); Assert.Empty(report.GetProperty("breakdowns").EnumerateArray());
        Assert.DoesNotContain(report.GetProperty("records").EnumerateArray(), r => r.GetProperty("resourceType").GetString() is "Ticket" or "Reservation");
        var attention = await GetAsync(root, "/api/v1/admin/attention?pageSize=1");
        Assert.True(attention.GetProperty("items").GetProperty("items").GetArrayLength() <= 1);
    }
    [Fact]
    public async Task Sql_rejects_raw_financial_history_mutations_and_overlapping_salary_roots()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(true); using var doctor = await app.ClientAsync("doctor");
        Guid reception = Guid.Empty;
        await app.WithDbAsync(async db => reception = await db.Receptions.Where(r => r.OwnerDoctorId == app.DoctorId).Select(r => r.Id).SingleAsync(TestContext.Current.CancellationToken));
        using var created = await PostAsync(doctor, Doctor + "/reception-salary-plans", new { amount = 3000, dueDay = 31,
            effectiveFrom = new DateOnly(app.Today.Year, app.Today.Month, 1), receptionId = reception, descriptionAr = "راتب",
            allocations = new[] { new { practiceId = app.PracticeId, percentage = 100 } } }, "salary-trigger");
        Assert.Equal(HttpStatusCode.OK, created.StatusCode); var id = (await JsonAsync(created)).GetProperty("id").GetGuid();
        await app.WithDbAsync(async db =>
        {
            var exception = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE dbo.OperatingFinanceHistories SET Reason = Reason WHERE ResourceId = {id}", TestContext.Current.CancellationToken));
            Assert.Equal(51018, exception.Number);
        });
        await app.WithDbAsync(async db =>
        {
            var plan = await db.OperatingFinancialPlans.AsNoTracking().Include(p => p.Revisions).ThenInclude(r => r.Allocations).SingleAsync(p => p.Id == id, TestContext.Current.CancellationToken);
            db.OperatingFinancialPlans.Add(OperatingFinancialPlan.Create(Guid.NewGuid(), app.DoctorId, reception, OperatingPlanKind.Salary,
                plan.Revisions.First().ToTerms(), app.DoctorUserId, app.Clock.UtcNow).Value);
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
            Assert.Equal(51018, Assert.IsType<SqlException>(error.InnerException).Number);
        });
    }
    private static System.Text.Json.JsonElement Metric(System.Text.Json.JsonElement report, string code) => report.GetProperty("metrics").EnumerateArray().Single(m => m.GetProperty("metricCode").GetString() == code);
    private static decimal Value(System.Text.Json.JsonElement report, string code) => Metric(report, code).GetProperty("value").GetDecimal();
}
