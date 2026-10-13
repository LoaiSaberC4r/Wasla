using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.OperatingFinance;
using Wasla.Application.Features.Reporting;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Domain.OperatingFinance;
using Wasla.Domain.Payments;
using Wasla.Domain.Security;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed partial class OperatingFinanceService(WaslaDbContext db, IReportingAccessGuard guard,
    ITicketQueueLock coordination, IDateTimeProvider clock, IOptions<Phase18Options> options) : IOperatingFinanceService
{
    public async Task<Result<OperatingFinanceWriteResponse>> ExecuteAsync(WriteOperatingFinanceCommand command, CancellationToken ct)
    {
        var permission = WritePermission(command);
        var access = await guard.AuthorizeAsync(ReportActor.Doctor, permission, null, null, ct);
        if (access.IsFailure) return Result<OperatingFinanceWriteResponse>.Fail(access.Errors);
        if (!Enum.IsDefined(command.Operation) || string.IsNullOrWhiteSpace(command.IdempotencyKey) || command.IdempotencyKey.Length > 200)
            return Result<OperatingFinanceWriteResponse>.Fail(OperatingFinanceErrors.Invalid("OperatingFinance.IdempotencyKeyRequired"));
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Operating finance writes require the application transaction pipeline.");
        var scope = access.Value;
        var doctorId = scope.DoctorId!.Value;
        await coordination.AcquireIdempotencyAsync(Guid.Empty, "OperatingFinanceDoctor", doctorId.ToString("N"), ct);
        var operation = "P18." + command.Operation;
        var key = command.IdempotencyKey.Trim();
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(command with { IdempotencyKey = string.Empty }))));
        var previous = await db.FinancialIdempotencyRecords.SingleOrDefaultAsync(r => r.ActorApplicationUserId == scope.ActorId &&
            r.Operation == operation && r.IdempotencyKey == key, ct);
        if (previous is not null)
        {
            if (!string.Equals(previous.RequestFingerprint, fingerprint, StringComparison.Ordinal))
                return Result<OperatingFinanceWriteResponse>.Fail(OperatingFinanceErrors.State("OperatingFinance.IdempotencyKeyReused"));
            if (previous.ResultId is not { } resultId) return Result<OperatingFinanceWriteResponse>.Fail(ReportingErrors.Conflict);
            return Result<OperatingFinanceWriteResponse>.Ok(await ReplayAsync(command, resultId, ct));
        }
        var now = clock.UtcNow;
        var today = Today(now, options.Value.DefaultReportTimeZoneId);
        Result<OperatingFinanceWriteResponse> result;
        if (command.Operation is OperatingFinanceOperation.CreateSalaryPlan or OperatingFinanceOperation.CreateRecurringPlan)
            result = await CreatePlanAsync(command, scope, now, ct);
        else if (command.Operation is OperatingFinanceOperation.ReviseSalaryPlan or OperatingFinanceOperation.ReviseRecurringPlan or
            OperatingFinanceOperation.EndSalaryPlan or OperatingFinanceOperation.PauseRecurringPlan or
            OperatingFinanceOperation.ResumeRecurringPlan or OperatingFinanceOperation.EndRecurringPlan)
            result = await ChangePlanAsync(command, scope, today, now, ct);
        else if (command.Operation == OperatingFinanceOperation.CreateExpense)
            result = await CreateExpenseAsync(command, scope, today, now, ct);
        else if (command.Operation is OperatingFinanceOperation.ReverseSalary or OperatingFinanceOperation.ReverseExpense)
            result = await ReverseAsync(command, scope, now, ct);
        else result = await ChangeObligationAsync(command, scope, today, now, ct);
        if (result.IsFailure) return result;
        var idempotency = new FinancialIdempotencyRecord(Guid.NewGuid(), scope.ActorId, operation, key, fingerprint, now);
        idempotency.Complete(result.Value.Id, now); db.FinancialIdempotencyRecords.Add(idempotency);
        await db.SaveChangesAsync(ct);
        return result;
    }

    private async Task<Result<OperatingFinanceWriteResponse>> CreatePlanAsync(WriteOperatingFinanceCommand command,
        ReportScope scope, DateTime now, CancellationToken ct)
    {
        if (command.Plan is not { } request || request.Allocations is null) return Fail();
        var kind = command.Operation == OperatingFinanceOperation.CreateSalaryPlan ? OperatingPlanKind.Salary : OperatingPlanKind.RecurringExpense;
        if (!await ValidAllocationsAsync(scope.DoctorId!.Value, request.ReceptionId, request.Allocations, kind == OperatingPlanKind.Salary, ct)) return Fail();
        if (kind == OperatingPlanKind.Salary && await SalaryOverlapAsync(scope.DoctorId.Value, request.ReceptionId!.Value,
            request.EffectiveFrom, request.EffectiveTo, null, ct))
            return Result<OperatingFinanceWriteResponse>.Fail(OperatingFinanceErrors.State("OperatingFinance.OverlappingSalaryPlan"));
        var created = OperatingFinancialPlan.Create(Guid.NewGuid(), scope.DoctorId.Value, request.ReceptionId,
            kind, Terms(request), scope.ActorId, now);
        if (created.IsFailure) return Result<OperatingFinanceWriteResponse>.Fail(created.Errors);
        db.OperatingFinancialPlans.Add(created.Value);
        History(scope, "Plan", created.Value.Id, "Created", request.Reason, now);
        await db.SaveChangesAsync(ct);
        return Result<OperatingFinanceWriteResponse>.Ok(PlanWriteResponse(created.Value));
    }
    private async Task<Result<OperatingFinanceWriteResponse>> ChangePlanAsync(WriteOperatingFinanceCommand command,
        ReportScope scope, DateOnly today, DateTime now, CancellationToken ct)
    {
        var salary = command.Operation is OperatingFinanceOperation.ReviseSalaryPlan or OperatingFinanceOperation.EndSalaryPlan;
        var plan = await db.OperatingFinancialPlans.Include(p => p.Revisions).ThenInclude(r => r.Allocations)
            .SingleOrDefaultAsync(p => p.Id == command.ResourceId && p.DoctorId == scope.DoctorId &&
                p.Kind == (salary ? OperatingPlanKind.Salary : OperatingPlanKind.RecurringExpense), ct);
        if (plan is null) return Missing();
        if (!Version(plan, command.Plan?.RowVersion ?? command.Transition?.RowVersion)) return Conflict();
        Result changed;
        if (command.Operation is OperatingFinanceOperation.ReviseSalaryPlan or OperatingFinanceOperation.ReviseRecurringPlan)
        {
            if (command.Plan is not { } r || r.Allocations is null || r.ReceptionId != plan.ReceptionId ||
                !await ValidAllocationsAsync(plan.DoctorId, plan.ReceptionId, r.Allocations, salary, ct)) return Fail();
            if (salary && await SalaryOverlapAsync(plan.DoctorId, plan.ReceptionId!.Value,
                plan.EffectiveFrom, r.EffectiveTo, plan.Id, ct))
                return Result<OperatingFinanceWriteResponse>.Fail(OperatingFinanceErrors.State("OperatingFinance.OverlappingSalaryPlan"));
            changed = plan.Revise(Terms(r), today, scope.ActorId, now, r.Reason ?? string.Empty);
        }
        else
        {
            var target = command.Operation == OperatingFinanceOperation.PauseRecurringPlan ? OperatingPlanStatus.Paused
                : command.Operation == OperatingFinanceOperation.ResumeRecurringPlan ? OperatingPlanStatus.Active : OperatingPlanStatus.Ended;
            changed = plan.ChangeLifecycle(target, today, scope.ActorId, now, command.Transition?.Reason ?? string.Empty);
        }
        if (changed.IsFailure) return Result<OperatingFinanceWriteResponse>.Fail(changed.Errors);
        db.OperatingPlanRevisions.Add(plan.Revisions.Single(r => r.RevisionNumber == plan.CurrentRevisionNumber));
        History(scope, "Plan", plan.Id, command.Operation.ToString(), command.Plan?.Reason ?? command.Transition?.Reason, now);
        await db.SaveChangesAsync(ct);
        return Result<OperatingFinanceWriteResponse>.Ok(PlanWriteResponse(plan));
    }
    private async Task<Result<OperatingFinanceWriteResponse>> CreateExpenseAsync(WriteOperatingFinanceCommand command,
        ReportScope scope, DateOnly today, DateTime now, CancellationToken ct)
    {
        if (command.Expense is not { } r || r.Allocations is null || !OperatingFinancialPolicy.HasReason(r.Reason) ||
            r.BusinessDate > today || r.PaidNow != (r.Payment is not null) ||
            !await ValidAllocationsAsync(scope.DoctorId!.Value, null, r.Allocations, false, ct)) return Fail();
        if (r.PaidNow && !scope.Permissions.Contains(Phase18PermissionNames.ExpensesSettle))
            return Result<OperatingFinanceWriteResponse>.Fail(ReportingErrors.Denied);
        var created = OperatingObligation.Create(Guid.NewGuid(), scope.DoctorId!.Value, OperatingObligationKind.OneTimeExpense,
            null, null, null, r.BusinessDate, r.DueBusinessDate, r.Amount, ExpenseAmountPolicy.Fixed,
            r.Category, r.DescriptionAr, r.DescriptionEn, r.Allocations, scope.ActorId, now);
        if (created.IsFailure) return Result<OperatingFinanceWriteResponse>.Fail(created.Errors);
        var obligation = created.Value;
        db.OperatingObligations.Add(obligation); History(scope, "Obligation", obligation.Id, "Created", r.Reason, now);
        // Insert the unpaid root before setting its settlement FK; both saves belong to the same transaction.
        await db.SaveChangesAsync(ct);
        Guid? settlementId = null;
        if (r.Payment is { } payment)
        {
            var paid = await SettleAsync(obligation, payment, scope, now, ct);
            if (paid.IsFailure) return Result<OperatingFinanceWriteResponse>.Fail(paid.Errors);
            settlementId = paid.Value.Id;
        }
        return Result<OperatingFinanceWriteResponse>.Ok(new(obligation.Id, "Obligation", ObligationRoute(obligation),
            Convert.ToBase64String(obligation.RowVersion), obligation.Id, settlementId));
    }
    private async Task<Result<OperatingFinanceWriteResponse>> ChangeObligationAsync(WriteOperatingFinanceCommand command,
        ReportScope scope, DateOnly today, DateTime now, CancellationToken ct)
    {
        var salary = command.Operation == OperatingFinanceOperation.SettleSalary || command.SalaryObligation;
        var obligation = await db.OperatingObligations.Include(o => o.Revisions).ThenInclude(r => r.Allocations)
            .SingleOrDefaultAsync(o => o.Id == command.ResourceId && o.DoctorId == scope.DoctorId &&
                (o.Kind == OperatingObligationKind.Salary) == salary, ct);
        if (obligation is null) return Missing();
        var token = command.Settlement?.RowVersion ?? command.Correction?.RowVersion ?? command.Actual?.RowVersion ?? command.Transition?.RowVersion;
        if (!Version(obligation, token)) return Conflict();
        if (command.Operation is OperatingFinanceOperation.SettleSalary or OperatingFinanceOperation.SettleExpense)
        {
            if (command.Settlement is null) return Fail();
            var settlement = await SettleAsync(obligation, command.Settlement, scope, now, ct);
            return settlement.IsFailure ? Result<OperatingFinanceWriteResponse>.Fail(settlement.Errors)
                : Result<OperatingFinanceWriteResponse>.Ok(new(settlement.Value.Id, "Settlement", SettlementRoute(obligation.Kind, settlement.Value.Id),
                    Convert.ToBase64String(obligation.RowVersion), obligation.Id, settlement.Value.Id));
        }
        Result changed;
        if (command.Operation == OperatingFinanceOperation.CorrectObligation)
        {
            if (command.Correction is not { } r || r.Allocations is null ||
                !await ValidAllocationsAsync(obligation.DoctorId, obligation.ReceptionId, r.Allocations, salary, ct)) return Fail();
            changed = obligation.Correct(r.Amount, r.DueBusinessDate, r.Category, r.DescriptionAr,
                r.DescriptionEn, r.Allocations, r.Reason, scope.ActorId, now);
        }
        else if (command.Operation == OperatingFinanceOperation.ConfirmActual && command.Actual is { } actual)
            changed = obligation.ConfirmActual(actual.Amount, actual.Reason, scope.ActorId, now);
        else if (command.Operation == OperatingFinanceOperation.CancelObligation && command.Transition is { } transition)
            changed = obligation.Cancel(transition.Reason, scope.ActorId, now);
        else return Fail();
        if (changed.IsFailure) return Result<OperatingFinanceWriteResponse>.Fail(changed.Errors);
        db.OperatingObligationRevisions.Add(obligation.Revisions.Single(r => r.RevisionNumber == obligation.CurrentRevisionNumber));
        History(scope, "Obligation", obligation.Id, command.Operation.ToString(),
            command.Correction?.Reason ?? command.Actual?.Reason ?? command.Transition?.Reason, now);
        await db.SaveChangesAsync(ct);
        return Result<OperatingFinanceWriteResponse>.Ok(new(obligation.Id, "Obligation", ObligationRoute(obligation), Convert.ToBase64String(obligation.RowVersion)));
    }
    private async Task<Result<OperatingSettlement>> SettleAsync(OperatingObligation obligation,
        OperatingSettlementRequest request, ReportScope scope, DateTime now, CancellationToken ct)
    {
        var zone = request.ReportTimeZoneId ?? options.Value.DefaultReportTimeZoneId;
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(zone, out _)) return Result<OperatingSettlement>.Fail(ReportingErrors.InvalidFilter);
        var created = OperatingSettlement.Create(Guid.NewGuid(), obligation, request.Amount, request.PaidBusinessDate,
            Today(now, zone), zone, request.PaymentMethod, request.Reference, request.BackdateReason, scope.ActorId, now);
        if (created.IsFailure) return created;
        db.OperatingSettlements.Add(created.Value);
        History(scope, "Obligation", obligation.Id, "Settled", request.BackdateReason, now);
        History(scope, "Settlement", created.Value.Id, "Recorded", request.BackdateReason, now);
        await db.SaveChangesAsync(ct);
        return created;
    }
    private async Task<Result<OperatingFinanceWriteResponse>> ReverseAsync(WriteOperatingFinanceCommand command,
        ReportScope scope, DateTime now, CancellationToken ct)
    {
        if (command.Reversal is not { } r) return Fail();
        var original = await db.OperatingSettlements.SingleOrDefaultAsync(s => s.Id == command.ResourceId && s.DoctorId == scope.DoctorId, ct);
        if (original is null) return Missing();
        var obligation = await db.OperatingObligations.SingleAsync(o => o.Id == original.ObligationId, ct);
        if ((obligation.Kind == OperatingObligationKind.Salary) != (command.Operation == OperatingFinanceOperation.ReverseSalary)) return Missing();
        if (!Version(obligation, r.RowVersion)) return Conflict();
        if (await db.OperatingSettlementReversals.AnyAsync(x => x.OriginalSettlementId == original.Id, ct))
            return Result<OperatingFinanceWriteResponse>.Fail(OperatingFinanceErrors.State("OperatingFinance.AlreadyReversed"));
        var zone = r.ReportTimeZoneId ?? options.Value.DefaultReportTimeZoneId;
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(zone, out _)) return Fail();
        var created = OperatingSettlementReversal.Create(Guid.NewGuid(), original, obligation, r.BusinessDate, Today(now, zone), zone, r.Reason, scope.ActorId, now);
        if (created.IsFailure) return Result<OperatingFinanceWriteResponse>.Fail(created.Errors);
        db.OperatingSettlementReversals.Add(created.Value);
        History(scope, "Settlement", original.Id, "Reversed", r.Reason, now);
        History(scope, "Obligation", obligation.Id, "SettlementReversed", r.Reason, now);
        await db.SaveChangesAsync(ct);
        return Result<OperatingFinanceWriteResponse>.Ok(new(created.Value.Id, "Reversal", SettlementRoute(obligation.Kind, original.Id),
            Convert.ToBase64String(obligation.RowVersion), obligation.Id, original.Id));
    }
    private async Task<bool> ValidAllocationsAsync(Guid doctor, Guid? reception, IReadOnlyCollection<PracticeAllocationPercentage> allocations,
        bool salary, CancellationToken ct)
    {
        if (allocations.Count is 0 or > 100) return false;
        var ids = allocations.Select(a => a.PracticeId).Distinct().ToArray();
        if (ids.Length != allocations.Count || await db.DoctorPractices.CountAsync(p => ids.Contains(p.Id) && p.DoctorId == doctor, ct) != ids.Length) return false;
        if (!salary) return reception is null;
        return reception is { } r && await db.Receptions.AnyAsync(x => x.Id == r && x.OwnerDoctorId == doctor &&
                db.ApplicationUsers.Any(u => u.Id == x.ApplicationUserId && u.IsActive), ct) &&
            await db.ReceptionPracticeAssignments.CountAsync(a => a.ReceptionId == r && a.IsActive && ids.Contains(a.DoctorPracticeId), ct) == ids.Length;
    }
    private Task<bool> SalaryOverlapAsync(Guid doctor, Guid reception, DateOnly from, DateOnly? to, Guid? except, CancellationToken ct)
        => db.OperatingFinancialPlans.AnyAsync(p => p.DoctorId == doctor && p.ReceptionId == reception && p.Kind == OperatingPlanKind.Salary &&
            p.Id != except && !(p.Status == OperatingPlanStatus.Ended && p.EffectiveTo == p.EffectiveFrom &&
                !db.OperatingObligations.Any(o => o.PlanId == p.Id)) &&
            (to == null || p.EffectiveFrom <= to) && (p.EffectiveTo == null || p.EffectiveTo >= from), ct);
    private bool Version<TEntity>(TEntity entity, string? token) where TEntity : class
    {
        byte[] bytes;
        try { bytes = Convert.FromBase64String(token ?? string.Empty); } catch (FormatException) { return false; }
        if (bytes.Length == 0 || !bytes.AsSpan().SequenceEqual((byte[])db.Entry(entity).Property("RowVersion").CurrentValue!)) return false;
        db.Entry(entity).Property("RowVersion").OriginalValue = bytes;
        return true;
    }
    private void History(ReportScope scope, string type, Guid id, string operation, string? reason, DateTime now)
        => db.OperatingFinanceHistories.Add(new(Guid.NewGuid(), scope.DoctorId!.Value, type, id, operation, reason, scope.ActorId, now));
    private static DateOnly Today(DateTime now, string zone) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now, TimeZoneInfo.FindSystemTimeZoneById(zone)));
    private static OperatingPlanTerms Terms(OperatingPlanRequest r) => new(r.Amount, r.DueDay, r.EffectiveFrom, r.EffectiveTo,
        r.Cadence, r.AmountPolicy, r.Category, r.DescriptionAr, r.DescriptionEn, r.Allocations);
    private static OperatingFinanceWriteResponse PlanWriteResponse(OperatingFinancialPlan p) => new(p.Id, "Plan",
        $"/api/v1/doctors/me/{(p.Kind == OperatingPlanKind.Salary ? "reception-salary-plans" : "recurring-expense-plans")}/{p.Id}", Convert.ToBase64String(p.RowVersion));
    private static string ObligationRoute(OperatingObligation o) => $"/api/v1/doctors/me/{(o.Kind == OperatingObligationKind.Salary ? "salary-obligations" : "expenses/obligations")}/{o.Id}";
    private static string SettlementRoute(OperatingObligationKind kind, Guid id) => $"/api/v1/doctors/me/{(kind == OperatingObligationKind.Salary ? "salary-settlements" : "expense-settlements")}/{id}";
    private static Result<OperatingFinanceWriteResponse> Fail() => Result<OperatingFinanceWriteResponse>.Fail(OperatingFinanceErrors.Invalid());
    private static Result<OperatingFinanceWriteResponse> Missing() => Result<OperatingFinanceWriteResponse>.Fail(ReportingErrors.NotFound);
    private static Result<OperatingFinanceWriteResponse> Conflict() => Result<OperatingFinanceWriteResponse>.Fail(ReportingErrors.Conflict);
    private static string WritePermission(WriteOperatingFinanceCommand c) => c.Operation switch
    {
        OperatingFinanceOperation.CreateSalaryPlan or OperatingFinanceOperation.ReviseSalaryPlan or OperatingFinanceOperation.EndSalaryPlan => Phase18PermissionNames.SalaryPlansManage,
        OperatingFinanceOperation.SettleSalary => Phase18PermissionNames.SalarySettle,
        OperatingFinanceOperation.ReverseSalary => Phase18PermissionNames.SalaryReverse,
        OperatingFinanceOperation.SettleExpense => Phase18PermissionNames.ExpensesSettle,
        OperatingFinanceOperation.ReverseExpense => Phase18PermissionNames.ExpensesReverse,
        _ => c.SalaryObligation ? Phase18PermissionNames.SalaryPlansManage : Phase18PermissionNames.ExpensesManage
    };
    private async Task<OperatingFinanceWriteResponse> ReplayAsync(WriteOperatingFinanceCommand c, Guid id, CancellationToken ct)
    {
        if (c.Operation is >= OperatingFinanceOperation.CreateSalaryPlan and <= OperatingFinanceOperation.EndRecurringPlan)
            return PlanWriteResponse(await db.OperatingFinancialPlans.AsNoTracking().SingleAsync(p => p.Id == id, ct));
        if (c.Operation is OperatingFinanceOperation.ReverseSalary or OperatingFinanceOperation.ReverseExpense)
        {
            var reversal = await db.OperatingSettlementReversals.AsNoTracking().SingleAsync(r => r.Id == id, ct);
            return new(id, "Reversal", SettlementRoute(c.Operation == OperatingFinanceOperation.ReverseSalary ? OperatingObligationKind.Salary : OperatingObligationKind.OneTimeExpense, reversal.OriginalSettlementId),
                ObligationId: reversal.ObligationId, SettlementId: reversal.OriginalSettlementId);
        }
        if (c.Operation is OperatingFinanceOperation.SettleSalary or OperatingFinanceOperation.SettleExpense)
        {
            var s = await db.OperatingSettlements.AsNoTracking().SingleAsync(s => s.Id == id, ct);
            return new(id, "Settlement", SettlementRoute(c.Operation == OperatingFinanceOperation.SettleSalary ? OperatingObligationKind.Salary : OperatingObligationKind.OneTimeExpense, id), ObligationId: s.ObligationId, SettlementId: id);
        }
        var o = await db.OperatingObligations.AsNoTracking().SingleAsync(o => o.Id == id, ct);
        var paid = c.Expense?.PaidNow == true ? await db.OperatingSettlements.AsNoTracking().Where(s => s.ObligationId == id).OrderBy(s => s.RecordedAtUtc).Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct) : null;
        return new(id, "Obligation", ObligationRoute(o), Convert.ToBase64String(o.RowVersion), o.Id, paid);
    }
}
