using Asp.Versioning;
using BuildingBlock.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wasla.Api.Security;
using Wasla.Application.Features.OperatingFinance;
using Wasla.Application.Features.Reporting;
using Wasla.Domain.OperatingFinance;
using Wasla.Domain.Security;

namespace Wasla.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/doctors/me")]
[Authorize(Roles = SystemRoleNames.Doctor)]
public sealed class OperatingFinanceController(IOperatingFinanceService finance, ISender sender) : ControllerBase
{
    [HttpGet("finance/filter-options")]
    [ProducesResponseType<OperatingFinanceOptions>(StatusCodes.Status200OK)]
    public async Task<IActionResult> FilterOptions([FromQuery] ReportOptionsFilter filter, CancellationToken ct)
        => (await finance.OptionsAsync(ct, filter)).ToIActionResult(ct);

    [HttpGet("expenses/categories")]
    [Permission(Phase18PermissionNames.ExpensesView)]
    [ProducesResponseType<IReadOnlyList<ReportOption>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Categories(CancellationToken ct)
    {
        var result = await finance.OptionsAsync(ct);
        return result.IsFailure ? result.ToIActionResult(ct) : Ok(result.Value.Categories);
    }

    [HttpGet("reception-salary-plans")]
    [Permission(Phase18PermissionNames.SalaryPlansView)]
    [ProducesResponseType<ReportPage<OperatingPlanResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReceptionSalaryPlans([FromQuery] OperatingFinanceFilter filter, CancellationToken ct)
        => (await finance.ListPlansAsync(OperatingPlanKind.Salary, filter, ct)).ToIActionResult(ct);

    [HttpGet("reception-salary-plans/{id:guid}")]
    [Permission(Phase18PermissionNames.SalaryPlansView)]
    [ProducesResponseType<OperatingPlanResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReceptionSalaryPlansById(Guid id, CancellationToken ct)
        => (await finance.PlanAsync(id, OperatingPlanKind.Salary, ct)).ToIActionResult(ct);

    [HttpGet("reception-salary-plans/{id:guid}/history")]
    [Permission(Phase18PermissionNames.SalaryPlansView)]
    [ProducesResponseType<ReportPage<OperatingHistoryResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReceptionSalaryPlansByIdHistory(Guid id, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await finance.HistoryAsync(id, "Plan", true, pageNumber, pageSize, ct)).ToIActionResult(ct);

    [HttpPost("reception-salary-plans")]
    [Permission(Phase18PermissionNames.SalaryPlansManage)]
    [ProducesResponseType<OperatingFinanceWriteResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PostReceptionSalaryPlans([FromBody] OperatingPlanRequest request,
        [FromHeader(Name = "Idempotency-Key"), System.ComponentModel.DataAnnotations.Required, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] string idempotencyKey, CancellationToken ct)
        => (await sender.Send(new WriteOperatingFinanceCommand(OperatingFinanceOperation.CreateSalaryPlan, null, idempotencyKey,
            Plan: request), ct)).ToIActionResult(ct);

    [HttpPut("reception-salary-plans/{id:guid}")]
    [Permission(Phase18PermissionNames.SalaryPlansManage)]
    [ProducesResponseType<OperatingFinanceWriteResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PutReceptionSalaryPlansById(Guid id, [FromBody] OperatingPlanRequest request,
        [FromHeader(Name = "Idempotency-Key"), System.ComponentModel.DataAnnotations.Required, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] string idempotencyKey, CancellationToken ct)
        => (await sender.Send(new WriteOperatingFinanceCommand(OperatingFinanceOperation.ReviseSalaryPlan, id, idempotencyKey,
            Plan: request), ct)).ToIActionResult(ct);

    [HttpPost("reception-salary-plans/{id:guid}/end")]
    [Permission(Phase18PermissionNames.SalaryPlansManage)]
    [ProducesResponseType<OperatingFinanceWriteResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PostReceptionSalaryPlansByIdEnd(Guid id, [FromBody] OperatingTransitionRequest request,
        [FromHeader(Name = "Idempotency-Key"), System.ComponentModel.DataAnnotations.Required, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] string idempotencyKey, CancellationToken ct)
        => (await sender.Send(new WriteOperatingFinanceCommand(OperatingFinanceOperation.EndSalaryPlan, id, idempotencyKey,
            Transition: request), ct)).ToIActionResult(ct);

    [HttpGet("recurring-expense-plans")]
    [Permission(Phase18PermissionNames.ExpensesView)]
    [ProducesResponseType<ReportPage<OperatingPlanResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecurringExpensePlans([FromQuery] OperatingFinanceFilter filter, CancellationToken ct)
        => (await finance.ListPlansAsync(OperatingPlanKind.RecurringExpense, filter, ct)).ToIActionResult(ct);

    [HttpGet("recurring-expense-plans/{id:guid}")]
    [Permission(Phase18PermissionNames.ExpensesView)]
    [ProducesResponseType<OperatingPlanResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecurringExpensePlansById(Guid id, CancellationToken ct)
        => (await finance.PlanAsync(id, OperatingPlanKind.RecurringExpense, ct)).ToIActionResult(ct);

    [HttpGet("recurring-expense-plans/{id:guid}/history")]
    [Permission(Phase18PermissionNames.ExpensesView)]
    [ProducesResponseType<ReportPage<OperatingHistoryResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecurringExpensePlansByIdHistory(Guid id, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await finance.HistoryAsync(id, "Plan", false, pageNumber, pageSize, ct)).ToIActionResult(ct);

    [HttpPost("recurring-expense-plans")]
    [Permission(Phase18PermissionNames.ExpensesManage)]
    [ProducesResponseType<OperatingFinanceWriteResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PostRecurringExpensePlans([FromBody] OperatingPlanRequest request,
        [FromHeader(Name = "Idempotency-Key"), System.ComponentModel.DataAnnotations.Required, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] string idempotencyKey, CancellationToken ct)
        => (await sender.Send(new WriteOperatingFinanceCommand(OperatingFinanceOperation.CreateRecurringPlan, null, idempotencyKey,
            Plan: request), ct)).ToIActionResult(ct);

    [HttpPut("recurring-expense-plans/{id:guid}")]
    [Permission(Phase18PermissionNames.ExpensesManage)]
    [ProducesResponseType<OperatingFinanceWriteResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PutRecurringExpensePlansById(Guid id, [FromBody] OperatingPlanRequest request,
        [FromHeader(Name = "Idempotency-Key"), System.ComponentModel.DataAnnotations.Required, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] string idempotencyKey, CancellationToken ct)
        => (await sender.Send(new WriteOperatingFinanceCommand(OperatingFinanceOperation.ReviseRecurringPlan, id, idempotencyKey,
            Plan: request), ct)).ToIActionResult(ct);

    [HttpPost("recurring-expense-plans/{id:guid}/end")]
    [Permission(Phase18PermissionNames.ExpensesManage)]
    [ProducesResponseType<OperatingFinanceWriteResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PostRecurringExpensePlansByIdEnd(Guid id, [FromBody] OperatingTransitionRequest request,
        [FromHeader(Name = "Idempotency-Key"), System.ComponentModel.DataAnnotations.Required, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] string idempotencyKey, CancellationToken ct)
        => (await sender.Send(new WriteOperatingFinanceCommand(OperatingFinanceOperation.EndRecurringPlan, id, idempotencyKey,
            Transition: request), ct)).ToIActionResult(ct);

    [HttpPost("recurring-expense-plans/{id:guid}/pause")]
    [Permission(Phase18PermissionNames.ExpensesManage)]
    [ProducesResponseType<OperatingFinanceWriteResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PostRecurringExpensePlansByIdPause(Guid id, [FromBody] OperatingTransitionRequest request,
        [FromHeader(Name = "Idempotency-Key"), System.ComponentModel.DataAnnotations.Required, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] string idempotencyKey, CancellationToken ct)
        => (await sender.Send(new WriteOperatingFinanceCommand(OperatingFinanceOperation.PauseRecurringPlan, id, idempotencyKey,
            Transition: request), ct)).ToIActionResult(ct);

    [HttpPost("recurring-expense-plans/{id:guid}/resume")]
    [Permission(Phase18PermissionNames.ExpensesManage)]
    [ProducesResponseType<OperatingFinanceWriteResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PostRecurringExpensePlansByIdResume(Guid id, [FromBody] OperatingTransitionRequest request,
        [FromHeader(Name = "Idempotency-Key"), System.ComponentModel.DataAnnotations.Required, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] string idempotencyKey, CancellationToken ct)
        => (await sender.Send(new WriteOperatingFinanceCommand(OperatingFinanceOperation.ResumeRecurringPlan, id, idempotencyKey,
            Transition: request), ct)).ToIActionResult(ct);

    [HttpGet("recurring-expense-plans/{id:guid}/obligations")]
    [Permission(Phase18PermissionNames.ExpensesView)]
    [ProducesResponseType<ReportPage<OperatingObligationResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecurringExpensePlansByIdObligations(Guid id, [FromQuery] OperatingFinanceFilter filter, CancellationToken ct)
        => (await finance.ListObligationsAsync(false, false, filter with { PlanId = id }, ct)).ToIActionResult(ct);

    [HttpPost("expenses")]
    [Permission(Phase18PermissionNames.ExpensesManage)]
    [ProducesResponseType<OperatingFinanceWriteResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PostExpenses([FromBody] OperatingExpenseRequest request,
        [FromHeader(Name = "Idempotency-Key"), System.ComponentModel.DataAnnotations.Required, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] string idempotencyKey, CancellationToken ct)
        => (await sender.Send(new WriteOperatingFinanceCommand(OperatingFinanceOperation.CreateExpense, null, idempotencyKey,
            Expense: request), ct)).ToIActionResult(ct);

    [HttpGet("expenses")]
    [Permission(Phase18PermissionNames.ExpensesView)]
    [ProducesResponseType<ReportPage<OperatingObligationResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExpenses([FromQuery] OperatingFinanceFilter filter, CancellationToken ct)
        => (await finance.ListObligationsAsync(false, true, filter, ct)).ToIActionResult(ct);

    [HttpGet("expenses/{id:guid}")]
    [Permission(Phase18PermissionNames.ExpensesView)]
    [ProducesResponseType<OperatingObligationResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExpensesById(Guid id, CancellationToken ct)
        => (await finance.ObligationAsync(id, false, ct)).ToIActionResult(ct);

    [HttpGet("expenses/{id:guid}/history")]
    [Permission(Phase18PermissionNames.ExpensesView)]
    [ProducesResponseType<ReportPage<OperatingHistoryResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExpensesByIdHistory(Guid id, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await finance.HistoryAsync(id, "Obligation", false, pageNumber, pageSize, ct)).ToIActionResult(ct);

    [HttpGet("expenses/transactions")]
    [Permission(Phase18PermissionNames.ExpensesView)]
    [ProducesResponseType<ReportPage<OperatingMovementResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExpensesTransactions([FromQuery] OperatingFinanceFilter filter, CancellationToken ct)
        => (await finance.MovementsAsync(filter, ct)).ToIActionResult(ct);

    [HttpGet("salary-obligations")]
    [Permission(Phase18PermissionNames.SalaryObligationsView)]
    [ProducesResponseType<ReportPage<OperatingObligationResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSalaryObligations([FromQuery] OperatingFinanceFilter filter, CancellationToken ct)
        => (await finance.ListObligationsAsync(true, false, filter, ct)).ToIActionResult(ct);

    [HttpGet("salary-obligations/{id:guid}")]
    [Permission(Phase18PermissionNames.SalaryObligationsView)]
    [ProducesResponseType<OperatingObligationResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSalaryObligationsById(Guid id, CancellationToken ct)
        => (await finance.ObligationAsync(id, true, ct)).ToIActionResult(ct);

    [HttpGet("salary-obligations/{id:guid}/history")]
    [Permission(Phase18PermissionNames.SalaryObligationsView)]
    [ProducesResponseType<ReportPage<OperatingHistoryResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSalaryObligationsByIdHistory(Guid id, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await finance.HistoryAsync(id, "Obligation", true, pageNumber, pageSize, ct)).ToIActionResult(ct);

    [HttpPost("salary-obligations/{id:guid}/settle")]
    [Permission(Phase18PermissionNames.SalarySettle)]
    [ProducesResponseType<OperatingFinanceWriteResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PostSalaryObligationsByIdSettle(Guid id, [FromBody] OperatingSettlementRequest request,
        [FromHeader(Name = "Idempotency-Key"), System.ComponentModel.DataAnnotations.Required, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] string idempotencyKey, CancellationToken ct)
        => (await sender.Send(new WriteOperatingFinanceCommand(OperatingFinanceOperation.SettleSalary, id, idempotencyKey,
            Settlement: request), ct)).ToIActionResult(ct);

    [HttpPatch("salary-obligations/{id:guid}")]
    [Permission(Phase18PermissionNames.SalaryPlansManage)]
    [ProducesResponseType<OperatingFinanceWriteResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PatchSalaryObligationsById(Guid id, [FromBody] OperatingCorrectionRequest request,
        [FromHeader(Name = "Idempotency-Key"), System.ComponentModel.DataAnnotations.Required, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] string idempotencyKey, CancellationToken ct)
        => (await sender.Send(new WriteOperatingFinanceCommand(OperatingFinanceOperation.CorrectObligation, id, idempotencyKey,
            Correction: request, SalaryObligation: true), ct)).ToIActionResult(ct);

    [HttpPost("salary-obligations/{id:guid}/cancel")]
    [Permission(Phase18PermissionNames.SalaryPlansManage)]
    [ProducesResponseType<OperatingFinanceWriteResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PostSalaryObligationsByIdCancel(Guid id, [FromBody] OperatingTransitionRequest request,
        [FromHeader(Name = "Idempotency-Key"), System.ComponentModel.DataAnnotations.Required, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] string idempotencyKey, CancellationToken ct)
        => (await sender.Send(new WriteOperatingFinanceCommand(OperatingFinanceOperation.CancelObligation, id, idempotencyKey,
            Transition: request, SalaryObligation: true), ct)).ToIActionResult(ct);

    [HttpGet("expenses/obligations")]
    [Permission(Phase18PermissionNames.ExpensesView)]
    [ProducesResponseType<ReportPage<OperatingObligationResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExpensesObligations([FromQuery] OperatingFinanceFilter filter, CancellationToken ct)
        => (await finance.ListObligationsAsync(false, false, filter, ct)).ToIActionResult(ct);

    [HttpGet("expenses/obligations/{id:guid}")]
    [Permission(Phase18PermissionNames.ExpensesView)]
    [ProducesResponseType<OperatingObligationResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExpensesObligationsById(Guid id, CancellationToken ct)
        => (await finance.ObligationAsync(id, false, ct)).ToIActionResult(ct);

    [HttpGet("expenses/obligations/{id:guid}/history")]
    [Permission(Phase18PermissionNames.ExpensesView)]
    [ProducesResponseType<ReportPage<OperatingHistoryResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExpensesObligationsByIdHistory(Guid id, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await finance.HistoryAsync(id, "Obligation", false, pageNumber, pageSize, ct)).ToIActionResult(ct);

    [HttpPost("expenses/obligations/{id:guid}/settle")]
    [Permission(Phase18PermissionNames.ExpensesSettle)]
    [ProducesResponseType<OperatingFinanceWriteResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PostExpensesObligationsByIdSettle(Guid id, [FromBody] OperatingSettlementRequest request,
        [FromHeader(Name = "Idempotency-Key"), System.ComponentModel.DataAnnotations.Required, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] string idempotencyKey, CancellationToken ct)
        => (await sender.Send(new WriteOperatingFinanceCommand(OperatingFinanceOperation.SettleExpense, id, idempotencyKey,
            Settlement: request), ct)).ToIActionResult(ct);

    [HttpPatch("expenses/obligations/{id:guid}")]
    [Permission(Phase18PermissionNames.ExpensesManage)]
    [ProducesResponseType<OperatingFinanceWriteResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PatchExpensesObligationsById(Guid id, [FromBody] OperatingCorrectionRequest request,
        [FromHeader(Name = "Idempotency-Key"), System.ComponentModel.DataAnnotations.Required, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] string idempotencyKey, CancellationToken ct)
        => (await sender.Send(new WriteOperatingFinanceCommand(OperatingFinanceOperation.CorrectObligation, id, idempotencyKey,
            Correction: request, SalaryObligation: false), ct)).ToIActionResult(ct);

    [HttpPost("expenses/obligations/{id:guid}/cancel")]
    [Permission(Phase18PermissionNames.ExpensesManage)]
    [ProducesResponseType<OperatingFinanceWriteResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PostExpensesObligationsByIdCancel(Guid id, [FromBody] OperatingTransitionRequest request,
        [FromHeader(Name = "Idempotency-Key"), System.ComponentModel.DataAnnotations.Required, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] string idempotencyKey, CancellationToken ct)
        => (await sender.Send(new WriteOperatingFinanceCommand(OperatingFinanceOperation.CancelObligation, id, idempotencyKey,
            Transition: request, SalaryObligation: false), ct)).ToIActionResult(ct);

    [HttpPost("expenses/obligations/{id:guid}/confirm-actual")]
    [Permission(Phase18PermissionNames.ExpensesManage)]
    [ProducesResponseType<OperatingFinanceWriteResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PostExpensesObligationsByIdConfirmActual(Guid id, [FromBody] OperatingActualAmountRequest request,
        [FromHeader(Name = "Idempotency-Key"), System.ComponentModel.DataAnnotations.Required, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] string idempotencyKey, CancellationToken ct)
        => (await sender.Send(new WriteOperatingFinanceCommand(OperatingFinanceOperation.ConfirmActual, id, idempotencyKey,
            Actual: request), ct)).ToIActionResult(ct);

    [HttpGet("salary-settlements/{id:guid}")]
    [Permission(Phase18PermissionNames.SalaryObligationsView)]
    [ProducesResponseType<OperatingSettlementResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSalarySettlementsById(Guid id, CancellationToken ct)
        => (await finance.SettlementAsync(id, true, ct)).ToIActionResult(ct);

    [HttpGet("salary-settlements/{id:guid}/receipt")]
    [Permission(Phase18PermissionNames.SalaryObligationsView)]
    [ProducesResponseType<OperatingSettlementResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSalarySettlementsByIdReceipt(Guid id, CancellationToken ct)
        => (await finance.SettlementAsync(id, true, ct)).ToIActionResult(ct);

    [HttpGet("salary-settlements/{id:guid}/history")]
    [Permission(Phase18PermissionNames.SalaryObligationsView)]
    [ProducesResponseType<ReportPage<OperatingHistoryResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSalarySettlementsByIdHistory(Guid id, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await finance.HistoryAsync(id, "Settlement", true, pageNumber, pageSize, ct)).ToIActionResult(ct);

    [HttpPost("salary-settlements/{id:guid}/reverse")]
    [Permission(Phase18PermissionNames.SalaryReverse)]
    [ProducesResponseType<OperatingFinanceWriteResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PostSalarySettlementsByIdReverse(Guid id, [FromBody] OperatingReversalRequest request,
        [FromHeader(Name = "Idempotency-Key"), System.ComponentModel.DataAnnotations.Required, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] string idempotencyKey, CancellationToken ct)
        => (await sender.Send(new WriteOperatingFinanceCommand(OperatingFinanceOperation.ReverseSalary, id, idempotencyKey,
            Reversal: request), ct)).ToIActionResult(ct);

    [HttpGet("expense-settlements/{id:guid}")]
    [Permission(Phase18PermissionNames.ExpensesView)]
    [ProducesResponseType<OperatingSettlementResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExpenseSettlementsById(Guid id, CancellationToken ct)
        => (await finance.SettlementAsync(id, false, ct)).ToIActionResult(ct);

    [HttpGet("expense-settlements/{id:guid}/receipt")]
    [Permission(Phase18PermissionNames.ExpensesView)]
    [ProducesResponseType<OperatingSettlementResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExpenseSettlementsByIdReceipt(Guid id, CancellationToken ct)
        => (await finance.SettlementAsync(id, false, ct)).ToIActionResult(ct);

    [HttpGet("expense-settlements/{id:guid}/history")]
    [Permission(Phase18PermissionNames.ExpensesView)]
    [ProducesResponseType<ReportPage<OperatingHistoryResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExpenseSettlementsByIdHistory(Guid id, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await finance.HistoryAsync(id, "Settlement", false, pageNumber, pageSize, ct)).ToIActionResult(ct);

    [HttpPost("expense-settlements/{id:guid}/reverse")]
    [Permission(Phase18PermissionNames.ExpensesReverse)]
    [ProducesResponseType<OperatingFinanceWriteResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PostExpenseSettlementsByIdReverse(Guid id, [FromBody] OperatingReversalRequest request,
        [FromHeader(Name = "Idempotency-Key"), System.ComponentModel.DataAnnotations.Required, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] string idempotencyKey, CancellationToken ct)
        => (await sender.Send(new WriteOperatingFinanceCommand(OperatingFinanceOperation.ReverseExpense, id, idempotencyKey,
            Reversal: request), ct)).ToIActionResult(ct);
}

