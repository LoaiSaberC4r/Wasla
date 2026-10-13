using BuildingBlock.Domain.Results;
using Wasla.Domain.Resources;

namespace Wasla.Domain.OperatingFinance;

public enum OperatingPlanKind { Salary = 1, RecurringExpense = 2 }
public enum OperatingPlanStatus { Active = 1, Paused = 2, Ended = 3 }
public enum ExpenseCadence { Monthly = 1, Quarterly = 3, Yearly = 12 }
public enum ExpenseAmountPolicy { Fixed = 1, Variable = 2 }
public enum OperatingObligationKind { Salary = 1, OneTimeExpense = 2, RecurringExpense = 3 }
public enum OperatingObligationStatus { Unpaid = 1, Paid = 2, Cancelled = 3 }
public enum OperatingPaymentMethod { Cash = 1, Card = 2, Wallet = 3, BankTransfer = 4 }
public enum ExpenseCategoryCode { Rent = 1, Utilities = 2, Supplies = 3, Maintenance = 4, Transport = 5, Other = 6 }
public sealed record PracticeAllocationPercentage(Guid PracticeId, decimal Percentage);
public sealed record AllocatedOperatingCost(Guid PracticeId, decimal Percentage, long AmountMinorUnits)
{
    public decimal Amount => AmountMinorUnits / 100m;
}

public static class OperatingFinanceErrors
{
    public static Error Invalid(string code = "OperatingFinance.Invalid")
        => Error.Domain(code, ErrorMessage.GetString("Phase18Invalid"));
    public static Error State(string code = "OperatingFinance.InvalidState")
        => Error.Conflict(code, ErrorMessage.GetString("Phase18Conflict"));
}

public static class OperatingFinancialPolicy
{
    public const string CurrencyCode = "EGP";
    public const decimal MaximumAmount = 99_999_999_999_999.99m;

    public static Result<long> MinorUnits(decimal amount)
        => amount <= 0 || amount > MaximumAmount || decimal.Truncate(amount * 100) != amount * 100
            ? Result<long>.Fail(OperatingFinanceErrors.Invalid("OperatingFinance.InvalidAmount"))
            : Result<long>.Ok((long)(amount * 100));

    public static Result<IReadOnlyList<AllocatedOperatingCost>> Allocate(decimal amount,
        IReadOnlyCollection<PracticeAllocationPercentage> percentages)
    {
        var minor = MinorUnits(amount);
        if (minor.IsFailure) return Result<IReadOnlyList<AllocatedOperatingCost>>.Fail(minor.Errors);
        if (percentages.Count is 0 or > 100 || percentages.Select(p => p.PracticeId).Distinct().Count() != percentages.Count ||
            percentages.Any(p => p.PracticeId == Guid.Empty || p.Percentage <= 0 || p.Percentage > 100 ||
                decimal.Truncate(p.Percentage * 10_000) != p.Percentage * 10_000) || percentages.Sum(p => p.Percentage) != 100)
            return Result<IReadOnlyList<AllocatedOperatingCost>>.Fail(OperatingFinanceErrors.Invalid("OperatingFinance.InvalidAllocation"));
        var shares = percentages.Select(p => new
        {
            p.PracticeId, p.Percentage, Raw = minor.Value * p.Percentage / 100,
            Floor = (long)decimal.Floor(minor.Value * p.Percentage / 100)
        }).ToArray();
        var remainder = minor.Value - shares.Sum(p => p.Floor);
        var winners = shares.OrderByDescending(p => p.Raw - p.Floor).ThenBy(p => p.PracticeId)
            .Take((int)remainder).Select(p => p.PracticeId).ToHashSet();
        return Result<IReadOnlyList<AllocatedOperatingCost>>.Ok(shares.OrderBy(p => p.PracticeId)
            .Select(p => new AllocatedOperatingCost(p.PracticeId, p.Percentage, p.Floor + (winners.Contains(p.PracticeId) ? 1 : 0))).ToArray());
    }

    public static DateOnly FirstCoveredMonth(DateOnly effectiveFrom)
        => effectiveFrom.Day == 1 ? effectiveFrom : new DateOnly(effectiveFrom.Year, effectiveFrom.Month, 1).AddMonths(1);
    public static DateOnly DueDate(int year, int month, int dueDay)
        => new(year, month, Math.Min(dueDay, DateTime.DaysInMonth(year, month)));
    public static bool HasReason(string? reason) => !string.IsNullOrWhiteSpace(reason) && reason.Trim().Length <= 2000;
    public static bool ValidUtc(DateTime value) => value != default && value.Kind == DateTimeKind.Utc;
    public static Result ValidateMovementDate(DateOnly date, DateOnly today, string? backdateReason)
        => date > today || date == DateOnly.MinValue || date < today && !HasReason(backdateReason)
            ? Result.Fail(OperatingFinanceErrors.Invalid("OperatingFinance.InvalidMovementDate")) : Result.Ok();
}
