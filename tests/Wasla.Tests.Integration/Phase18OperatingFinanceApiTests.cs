using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wasla.Application.Features.OperatingFinance;
using Wasla.Domain.OperatingFinance;
using static Wasla.Tests.Integration.Phase13ApiFixture;

namespace Wasla.Tests.Integration;

public sealed class Phase18OperatingFinanceApiTests
{
    private const string Base = "/api/v1/doctors/me";
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Atomic_paidnow_replay_reversal_resettlement_and_scope_denials(bool sql)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sql);
        using var doctor = await app.ClientAsync("doctor"); using var other = await app.ClientAsync("other-doctor");
        using var reception = await app.ClientAsync("reception"); using var patient = await app.ClientAsync("patient");
        var body = Expense(app, paidNow: true);
        using var created = await PostAsync(doctor, Base + "/expenses", body, "paid-now");
        Assert.True(created.StatusCode == HttpStatusCode.OK, await created.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var id = (await JsonAsync(created)).GetProperty("id").GetGuid();
        var o = await GetAsync(doctor, Base + $"/expenses/obligations/{id}");
        Assert.Equal("Paid", o.GetProperty("status").GetString());
        var settlementId = o.GetProperty("activeSettlementId").GetGuid();
        using var replay = await PostAsync(doctor, Base + "/expenses", body, "paid-now");
        Assert.Equal(id, (await JsonAsync(replay)).GetProperty("id").GetGuid());
        using var changedKey = await PostAsync(doctor, Base + "/expenses", Expense(app, true, 651), "paid-now");
        Assert.Equal(HttpStatusCode.Conflict, changedKey.StatusCode);
        using var otherDetail = await other.GetAsync(Base + $"/expenses/obligations/{id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, otherDetail.StatusCode);
        using var receptionDetail = await reception.GetAsync(Base + $"/expenses/obligations/{id}", TestContext.Current.CancellationToken);
        using var patientDetail = await patient.GetAsync(Base + $"/expenses/obligations/{id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, receptionDetail.StatusCode); Assert.Equal(HttpStatusCode.Forbidden, patientDetail.StatusCode);
        var receipt = await GetAsync(doctor, Base + $"/expense-settlements/{settlementId}/receipt");
        var reverseBody = new { businessDate = app.Today, reason = "wrong recorded method", rowVersion = o.GetProperty("rowVersion").GetString() };
        using var reversed = await PostAsync(doctor, Base + $"/expense-settlements/{settlementId}/reverse", reverseBody, "reverse");
        Assert.True(reversed.StatusCode == HttpStatusCode.OK, await reversed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        o = await GetAsync(doctor, Base + $"/expenses/obligations/{id}");
        using var doubled = await PostAsync(doctor, Base + $"/expense-settlements/{settlementId}/reverse",
            reverseBody with { }, "other-reverse");
        Assert.Equal(HttpStatusCode.Conflict, doubled.StatusCode);
        using var resettled = await PostAsync(doctor, Base + $"/expenses/obligations/{id}/settle", new
        { amount = 650, paidBusinessDate = app.Today, paymentMethod = "Cash", rowVersion = o.GetProperty("rowVersion").GetString() }, "resettle");
        Assert.Equal(HttpStatusCode.OK, resettled.StatusCode);
        var oldReceipt = await GetAsync(doctor, Base + $"/expense-settlements/{settlementId}/receipt");
        Assert.Equal(receipt.GetProperty("receiptNumber").GetString(), oldReceipt.GetProperty("receiptNumber").GetString());
        Assert.True(oldReceipt.GetProperty("isReversed").GetBoolean());
        await app.WithDbAsync(async db =>
        {
            var ct = TestContext.Current.CancellationToken;
            Assert.Equal(1, await db.OperatingObligations.CountAsync(ct));
            Assert.Equal(2, await db.OperatingSettlements.CountAsync(ct));
            Assert.Equal(1, await db.OperatingSettlementReversals.CountAsync(ct));
        });
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Paidnow_failure_rolls_back_root_history_and_idempotency_then_allows_retry(bool sql)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sql); using var doctor = await app.ClientAsync("doctor");
        using var fail = await PostAsync(doctor, Base + "/expenses", new
        {
            amount = 650, businessDate = app.Today, dueBusinessDate = app.Today, category = "Utilities", descriptionAr = "فاتورة",
            allocations = new[] { new { practiceId = app.PracticeId, percentage = 100 } }, reason = "invoice", paidNow = true,
            payment = new { amount = 650, paidBusinessDate = app.Today.AddDays(1), paymentMethod = "Cash" }
        }, "atomic-retry");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, fail.StatusCode);
        await app.WithDbAsync(async db =>
        { var ct = TestContext.Current.CancellationToken; Assert.Empty(await db.OperatingObligations.ToArrayAsync(ct));
            Assert.Empty(await db.OperatingFinanceHistories.ToArrayAsync(ct)); Assert.Empty(await db.FinancialIdempotencyRecords.Where(r => r.Operation.StartsWith("P18.")).ToArrayAsync(ct)); });
        using var retry = await PostAsync(doctor, Base + "/expenses", Expense(app, true), "atomic-retry");
        Assert.True(retry.StatusCode == HttpStatusCode.OK, await retry.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Salary_and_recurring_generation_is_unique_and_variable_bill_is_not_autopaid(bool sql)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sql); using var doctor = await app.ClientAsync("doctor");
        Guid reception = Guid.Empty;
        await app.WithDbAsync(async db => reception = await db.Receptions.Where(r => r.OwnerDoctorId == app.DoctorId).Select(r => r.Id).SingleAsync(TestContext.Current.CancellationToken));
        var start = new DateOnly(app.Today.Year, app.Today.Month, 1);
        using var salary = await PostAsync(doctor, Base + "/reception-salary-plans", new
        { amount = 3000, dueDay = 1, effectiveFrom = start, receptionId = reception, descriptionAr = "راتب", allocations = new[] { new { practiceId = app.PracticeId, percentage = 100 } } }, "salary");
        Assert.True(salary.StatusCode == HttpStatusCode.OK, await salary.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        using var recurring = await PostAsync(doctor, Base + "/recurring-expense-plans", new
        { amount = 500, dueDay = 1, effectiveFrom = start, category = "Utilities", cadence = "Monthly", amountPolicy = "Variable", descriptionAr = "كهرباء", allocations = new[] { new { practiceId = app.PracticeId, percentage = 100 } } }, "recurring");
        Assert.Equal(HttpStatusCode.OK, recurring.StatusCode);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var generator = scope.ServiceProvider.GetRequiredService<IOperatingObligationGenerator>();
            Assert.Equal(2, await generator.GenerateAsync(app.DoctorId, TestContext.Current.CancellationToken));
            Assert.Equal(0, await generator.GenerateAsync(app.DoctorId, TestContext.Current.CancellationToken));
        }
        var list = await GetAsync(doctor, Base + "/expenses/obligations");
        var obligation = list.GetProperty("items")[0]; var id = obligation.GetProperty("id").GetGuid();
        Assert.False(obligation.GetProperty("actualAmountConfirmed").GetBoolean());
        using var premature = await PostAsync(doctor, Base + $"/expenses/obligations/{id}/settle", new
        { amount = 500, paidBusinessDate = app.Today, paymentMethod = "Cash", rowVersion = obligation.GetProperty("rowVersion").GetString() }, "premature");
        Assert.Equal(HttpStatusCode.Conflict, premature.StatusCode);
        using var confirmed = await PostAsync(doctor, Base + $"/expenses/obligations/{id}/confirm-actual", new
        { amount = 650, reason = "actual invoice", rowVersion = obligation.GetProperty("rowVersion").GetString() }, "confirm");
        Assert.True(confirmed.StatusCode == HttpStatusCode.OK, await confirmed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        obligation = await GetAsync(doctor, Base + $"/expenses/obligations/{id}");
        Assert.Equal("Unpaid", obligation.GetProperty("status").GetString()); Assert.Equal(650, obligation.GetProperty("amount").GetDecimal());
    }
    [Fact]
    public async Task Sql_concurrent_full_settlements_have_one_winner_and_cross_doctor_allocations_are_rejected()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(true); using var doctor = await app.ClientAsync("doctor");
        using var created = await PostAsync(doctor, Base + "/expenses", Expense(app, false), "unpaid");
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var id = (await JsonAsync(created)).GetProperty("id").GetGuid();
        var obligation = await GetAsync(doctor, Base + $"/expenses/obligations/{id}");
        var body = new { amount = 650, paidBusinessDate = app.Today, paymentMethod = "Cash", rowVersion = obligation.GetProperty("rowVersion").GetString() };
        var responses = await Task.WhenAll(PostAsync(doctor, Base + $"/expenses/obligations/{id}/settle", body, "race-a"),
            PostAsync(doctor, Base + $"/expenses/obligations/{id}/settle", body, "race-b"));
        try { Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict); }
        finally { foreach (var r in responses) r.Dispose(); }
        using var cross = await PostAsync(doctor, Base + "/expenses", new
        { amount = 1, businessDate = app.Today, dueBusinessDate = app.Today, category = "Other", descriptionAr = "اختبار",
            allocations = new[] { new { practiceId = app.OtherPracticeId, percentage = 100 } }, reason = "scope test" }, "cross");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, cross.StatusCode);
        await app.WithDbAsync(async db =>
        {
            var invalid = OperatingObligation.Create(Guid.NewGuid(), app.DoctorId, OperatingObligationKind.OneTimeExpense,
                null, null, null, app.Today, app.Today, 1, ExpenseAmountPolicy.Fixed, ExpenseCategoryCode.Other,
                "اختبار", null, [new(app.OtherPracticeId, 100)], app.DoctorUserId, app.Clock.UtcNow).Value;
            db.OperatingObligations.Add(invalid);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        });
    }
    private static object Expense(Phase13ApiFixture app, bool paidNow, decimal amount = 650) => new
    {
        amount, businessDate = app.Today, dueBusinessDate = app.Today, category = "Utilities", descriptionAr = "فاتورة كهرباء",
        allocations = new[] { new { practiceId = app.PracticeId, percentage = 100 } }, reason = "invoice", paidNow,
        payment = paidNow ? new { amount, paidBusinessDate = app.Today, paymentMethod = "Cash" } : null
    };
}
