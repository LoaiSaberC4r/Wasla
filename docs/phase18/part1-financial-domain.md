# Operating financial domain

Sources: `Wasla.Domain/OperatingFinance`, `Features/OperatingFinance/OperatingFinanceContracts.cs`, SQL `OperatingFinanceService*`, `OperatingObligationGenerator`, and `OperatingFinanceConfigurations`. Patient Payment/Refund/Revenue aggregates retain their Phase12 semantics and routes.

## Aggregates and immutable facts

| Record | Role | Change policy |
|---|---|---|
| OperatingFinancialPlan | Salary or recurring-expense lifecycle and current revision number | Versioned terms/lifecycle revisions; no hard delete |
| OperatingPlanRevision / allocation | Terms, effective interval, due day, cadence, amount policy and percentages | Append only; historical obligations are not recalculated |
| OperatingObligation / revision | Generated salary/bill or one-time outgoing liability | Unpaid correction/cancellation only; immutable revision/allocation snapshots |
| OperatingSettlement | Full manual payment record referencing one obligation revision | Immutable; one active settlement per obligation |
| OperatingSettlementReversal | Full compensating movement referencing original settlement | Immutable; one reversal per original settlement |
| OperatingFinanceHistory | Actual actor or explicit system-generated event, operation, reason, time and revision ID | Append only with reconstructible revision payload |
| OperatingGenerationRun | Measured job execution status/count/failure code | Operational run evidence, no salary payment side effect |

EGP only; amount >0, ≤99,999,999,999,999.99 with at most two decimals. Movement storage uses signed/reportable integer piastres; the persisted settlement/reversal magnitudes are positive and report signs are explicit. A recorded BankTransfer payment method records a fact only.

Allocation contains1–100 distinct owned Practices, positive percentages≤100 with at most four decimals and an exact100 sum. Salary additionally requires same-Doctor active Reception account and active assignments to every allocated Practice. The largest-remainder algorithm floors each exact piastre share, gives remaining piastres to greatest fractional remainders and breaks ties by ascending Guid ordering. Zero rounded amounts are valid. `10.01 × 60/40` gives6.01/4.00. A one-piastre shared cost remains exactly one piastre; reporting never charges the whole shared cost to each Practice.

## Salary and recurring generation

Salary covers complete calendar months only. Day-one start can cover that month; another start moves FirstCoveredMonth to next month. DueDay1–31 clamps to the month's last date, including leap February. No automatic proration. Ending/revoking an assignment stops future generation and preserves existing obligations; disputed unpaid obligations use an explicit correction/cancellation with reason. An end before a complete covered month creates no new entitlement automatically.

Generation creates eligible current-calendar-cycle obligations, including Upcoming obligations before their due day, and catches up missed eligible cycles. It generates no future calendar cycles and never pays. Each plan/occurrence is unique. At most24 new occurrences per plan per pass; later passes continue from persisted occurrences. Plans are scanned in batches of100. SQL application locks serialize finance/generation for the owning Doctor; database uniqueness is the final duplicate barrier. Multi-instance/restart retries observe committed occurrences.

Recurring cadence is Monthly, Quarterly or Yearly anchored to effective terms. Fixed bills snapshot the planned amount; Variable bills remain Unpaid and unconfirmed until actual amount confirmation. Correction needs a reason and a new immutable revision. Pause/resume/end are audited lifecycle changes; future revisions require a future effective date. No historical obligation or receipt is rewritten by a new plan revision.

## Mutations and dates

One-time expense creation includes business date, due date, category, bilingual description, allocations and reason. PaidNow requires a valid full settlement payload and commits root/revision/history/settlement/idempotency together. A failed payment rolls back creation and permits a corrected retry because no successful idempotency record was committed.

An Unpaid confirmed obligation permits full settlement equal to its entire outstanding amount. No partial/duplicate settlement. Paid facts cannot be edited or cancelled. A full reversal requires the original settlement, current obligation RowVersion and reason; it clears the active settlement, appends the compensating movement and makes the obligation unpaid again. Re-settlement creates a new receipt, preserving the original receipt and its reversal status. No double reversal.

PaidBusinessDate/reversal BusinessDate express effective cash date in the selected report zone. RecordedAtUtc always expresses when the platform actually recorded the fact. Future dates are rejected; a past manual payment date requires a backdate reason. Reports include a backdated movement only when RecordedAtUtc≤AsOfUtc. Idempotency is scoped to actor/operation/key and fingerprinted input. RowVersion guards state changes even when the same UI button remains visible.

## Reporting equations

| Metric | Equation |
|---|---|
| GrossCollected | Existing patient collections in report-zone period |
| Refunds | Existing positive refund magnitudes in period |
| NetCollected | GrossCollected − Refunds |
| PaidOperatingExpenses | Salary + one-time + recurring settlements minus reversals, effective date in period and recorded by AsOfUtc |
| OutstandingDueInPeriod | Unsettled, noncancelled latest-known obligation revision amounts whose due date is in period |
| AllOutstandingAsOf | All unsettled noncancelled liabilities known at AsOfUtc, independent of selected due window |
| OverdueAsOf | All outstanding whose due date is before the AsOf-local date |
| NetOperatingCashResult | NetCollected − PaidOperatingExpenses |
| ProjectedResultForPeriod | NetCollected − PaidOperatingExpenses − OutstandingDueInPeriod |

Unconfirmed variable amounts remain planned liabilities and mark relevant totals Partial. Cash and due measures are deliberately distinct. Per-Practice totals sum only immutable allocated shares; whole-Doctor totals count the liability once. Ratios are recomputed from true denominators. Historical detail may link to a current authoritative entity, while history/export preserves the AsOf snapshots and warns about missing historical sources.

Validation: `Phase18OperatingFinanceTests` covers money, allocation, full payment/reversal, due days/leap years and plan rules. `Phase18OperatingFinanceApiTests` covers persistence, PaidNow rollback, generation, actual confirmation, concurrency and scope. `Phase18DoctorReportingApiTests` verifies historical correction/settlement cutoffs. `Phase18ReconciliationTests` reconciles allocated lists/report/drilldown and directly exercises SQL history immutability and salary overlap.
