using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Tickets;
using static Wasla.Tests.Integration.Phase13ApiFixture;

namespace Wasla.Tests.Integration;

public sealed class Phase18DoctorReportingApiTests
{
    private const string Base = "/api/v1/doctors/me";
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Financial_asof_preserves_old_amounts_and_excludes_later_settlements(bool sql)
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
    private static decimal Value(JsonElement report, string code) => report.GetProperty("metrics").EnumerateArray()
        .Single(m => m.GetProperty("metricCode").GetString() == code).GetProperty("value").GetDecimal();
}
