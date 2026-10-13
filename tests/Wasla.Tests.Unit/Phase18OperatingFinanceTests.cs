using Wasla.Domain.OperatingFinance;

namespace Wasla.Tests.Unit;

public sealed class Phase18OperatingFinanceTests
{
    private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid C = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static readonly DateTime Now = new(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 10, 13);
    [Theory]
    [InlineData(2027, 2, 28)] [InlineData(2028, 2, 29)] [InlineData(2026, 4, 30)]
    public void Salary_due_day_is_clamped_to_real_month(int year, int month, int day)
        => Assert.Equal(new DateOnly(year, month, day), OperatingFinancialPolicy.DueDate(year, month, 31));
    [Fact]
    public void Full_calendar_months_have_no_automatic_proration()
    {
        Assert.Equal(new DateOnly(2026, 11, 1), OperatingFinancialPolicy.FirstCoveredMonth(Today));
        Assert.Equal(new DateOnly(2026, 10, 1), OperatingFinancialPolicy.FirstCoveredMonth(new(2026, 10, 1)));
    }
    [Theory]
    [InlineData(3000, 60, 40, 180000, 120000)] [InlineData(0.01, 50, 50, 1, 0)]
    public void Allocation_reconciles_to_every_piastre(decimal amount, decimal a, decimal b, long expectedA, long expectedB)
    {
        var shares = OperatingFinancialPolicy.Allocate(amount, [new(A, a), new(B, b)]).Value;
        Assert.Equal(expectedA, shares.Single(s => s.PracticeId == A).AmountMinorUnits);
        Assert.Equal(expectedB, shares.Single(s => s.PracticeId == B).AmountMinorUnits);
        Assert.Equal((long)(amount * 100), shares.Sum(s => s.AmountMinorUnits));
    }
    [Fact]
    public void Fractional_allocation_remainder_is_deterministic_and_input_order_independent()
    {
        PracticeAllocationPercentage[] p = [new(A, 33.3333m), new(B, 33.3333m), new(C, 33.3334m)];
        var shares = OperatingFinancialPolicy.Allocate(0.01m, p).Value;
        Assert.Equal(1, shares.Single(s => s.PracticeId == C).AmountMinorUnits);
        Assert.Equal(shares, OperatingFinancialPolicy.Allocate(0.01m, p.Reverse().ToArray()).Value);
        Assert.True(OperatingFinancialPolicy.Allocate(1, [new(A, 99.9999m)]).IsFailure);
        Assert.True(OperatingFinancialPolicy.MinorUnits(decimal.MaxValue).IsFailure);
    }
    [Fact]
    public void Variable_bill_requires_confirmation_and_full_payment_and_paid_data_is_immutable()
    {
        var o = Obligation(ExpenseAmountPolicy.Variable);
        Assert.True(o.Settle(A, 500).IsFailure);
        Assert.True(o.ConfirmActual(650, null, A, Now).IsFailure);
        Assert.True(o.ConfirmActual(650, "invoice", A, Now).IsSuccess);
        Assert.True(o.Settle(A, 500).IsFailure);
        Assert.True(o.Settle(A, 650).IsSuccess);
        Assert.True(o.Settle(B, 650).IsFailure);
        Assert.True(o.ConfirmActual(700, "correction", A, Now).IsFailure);
        Assert.True(o.Cancel("mistake", A, Now).IsFailure);
        Assert.True(o.Reverse(B).IsFailure);
        Assert.True(o.Reverse(A).IsSuccess);
        Assert.True(o.Reverse(A).IsFailure);
        Assert.True(o.Settle(B, 650).IsSuccess);
        Assert.Equal(50000, o.PlannedAmountMinorUnits);
        Assert.Equal(50000, o.Revisions.First().AmountMinorUnits);
    }
    [Fact]
    public void Equal_variable_amount_still_needs_explicit_confirmation()
    {
        var o = Obligation(ExpenseAmountPolicy.Variable);
        Assert.False(o.ActualAmountConfirmed);
        Assert.True(o.ConfirmActual(500, null, A, Now).IsSuccess);
        Assert.True(o.Settle(B, 500).IsSuccess);
    }
    [Fact]
    public void Movement_dates_reject_future_and_require_backdate_reason()
    {
        Assert.True(OperatingFinancialPolicy.ValidateMovementDate(Today.AddDays(1), Today, "future").IsFailure);
        Assert.True(OperatingFinancialPolicy.ValidateMovementDate(Today.AddDays(-1), Today, null).IsFailure);
        Assert.True(OperatingFinancialPolicy.ValidateMovementDate(Today.AddDays(-1), Today, "late recording").IsSuccess);
    }
    [Fact]
    public void Future_terms_do_not_rewrite_old_plan_allocations_or_advance_on_pause()
    {
        var terms = new OperatingPlanTerms(500, 31, new(2026, 10, 1), null, ExpenseCadence.Monthly,
            ExpenseAmountPolicy.Fixed, ExpenseCategoryCode.Utilities, "فاتورة", "Bill", [new(A, 60), new(B, 40)]);
        var p = OperatingFinancialPlan.Create(C, A, null, OperatingPlanKind.RecurringExpense, terms, B, Now).Value;
        Assert.True(p.Revise(terms with { Amount = 650, EffectiveFrom = new(2026, 11, 1), Allocations = [new(A, 100)] }, Today, B, Now, "future bill").IsSuccess);
        Assert.True(p.ChangeLifecycle(OperatingPlanStatus.Paused, Today, B, Now, "temporary pause").IsSuccess);
        Assert.Equal(500, p.Revisions.Last().Amount);
        Assert.Equal(2, p.Revisions.First().Allocations.Count);
        Assert.Equal(650, p.Revisions.Single(r => r.RevisionNumber == 2).Amount);
    }
    private static OperatingObligation Obligation(ExpenseAmountPolicy policy) => OperatingObligation.Create(Guid.NewGuid(), A,
        OperatingObligationKind.OneTimeExpense, null, null, null, Today, Today, 500, policy,
        ExpenseCategoryCode.Utilities, "فاتورة", "Bill", [new(A, 100)], A, Now).Value;
}
