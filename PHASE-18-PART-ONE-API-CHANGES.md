# Phase 18 Part One API changes

109 NEW endpoints generated from the running application's ApiExplorer and Swagger. Existing endpoints are listed in [the reused contract inventory](docs/phase18/part1-reused-api-contracts.md). Business numbering remains the approved task's numbering; P18 IDs identify contracts without renumbering earlier phases.

The [OpenAPI snapshot](docs/phase18/part1-openapi.json) is the field-level request/response authority, including schema required arrays, nullability, enums, date formats and binary exports. [DTO tables](docs/phase18/part1-contract-schemas.md) make those definitions readable. Domain validation and UI behavior below supplement schema-level validation.

## Common behavior

All JSON success responses use the declared DTO directly with camelCase names and string enum codes. UTC timestamps carry UTC semantics; business dates are yyyy-MM-dd. Lists use items/totalCount/pageNumber/pageSize. No-match lists are empty with zero count; unavailable metrics use null plus DataStatus and a warning, never a fabricated zero.

Send bearer authentication. Every new POST/PUT/PATCH requires Idempotency-Key (nonblank, at most 200 characters); preserve it only when retrying the identical command. A changed payload under the same key conflicts. RowVersion is opaque base64 and required for mutations of existing mutable records, except the first persistence of a computed insight. On 409 refresh detail/actions and ask the user to reconcile changes before submitting with a new key. All financial writes, history and idempotency complete in one transaction.

Report filters: inclusive dates, 1–366 days; default previous 30 calendar days ending today. Practice/Doctor/Segment/VisitType IDs must belong to the authorized scope. ComparePreviousPeriod uses the preceding equal-length window at the same AsOfUtc. AsOfUtc must be UTC and no later than generation time. Financial filters (expenseCategory/receptionId/planId) and operational filters are distinct. Report sortBy=Date|Reference|Status|Amount, sortDirection=asc|desc; finance sortBy=Date|Status|Amount. Report pageSize 1–100 (default20), pageNumber 1–1000000. Options pageSize defaults50, max100 and search length max100; each dimension has an independent total. See exact route parameter tables for applicability.

Errors are localized ProblemDetails with errors[].code/message/type and correlation/trace metadata: 400 malformed binding/header/body, 401 unauthenticated, 403 missing role/permission, 404 hidden scoped ID or unsupported report code, 409 stale version/invalid transition/idempotency conflict, 422 invalid business/filter/oversized export, 429 medical rate limit, 503 required runtime/coordination unavailable. Do not branch on translated messages. No new endpoint grants a permission merely because a button was rendered.

Export: GET with format=pdf|xlsx, language=ar|en and optional landscape; exact same report filters/AsOfUtc. Success is binary with Content-Disposition, no-store and nosniff. PDF ≤5000 rows/15MiB, XLSX ≤25000 rows/20MiB, 366 days. Exceeding limits rejects; no truncation/CSV. Preview and export permissions are independent. Doctor overview exports omit financial metrics without financial export permission. Reception and platform exports never include private outgoing finance. Receipts remain immutable JSON DTOs; report PDF/XLSX is available through the catalog.

AvailableActions is authoritative for UI hints, and the destination command revalidates permission, scope, state and version. Metric drilldown routes preserve normalized filters and AsOfUtc. Financial amount decimals are EGP with at most two places; allocations sum exactly100 (at most four decimal places), belong to the Doctor and are rounded in minor units. Required operational summaries/reasons reject clinical/private content. Full forms and transitions are documented in the UI matrix and domain/state-machine documents.

Examples are synthetic schema specimens. Replace reference IDs with values from authorized options/details, RowVersion with the latest returned token, dates with applicable business dates, and choose a valid transition. They illustrate wire shape, not an executable seed scenario.

## Endpoint index

| ID | Rules | Actor | Method / route | Permission |
|---|---|---|---|---|
| [P18-001](#p18-001) | S.01–S.29 / S.31–S.35 | SuperAdmin | `GET /api/v1/admin/attention` | Dashboards.Platform.View |
| [P18-002](#p18-002) | S.01–S.29 / S.31–S.35 | SuperAdmin | `GET /api/v1/admin/dashboard/filter-options` | Dashboards.Platform.View OR Reports.Platform.View (service guard) |
| [P18-003](#p18-003) | S.01–S.29 / S.31–S.35 | SuperAdmin | `GET /api/v1/admin/dashboard/overview` | Dashboards.Platform.View |
| [P18-004](#p18-004) | S.01–S.29 / S.31–S.35 | SuperAdmin | `GET /api/v1/admin/data-quality/summary` | PlatformDataQuality.View |
| [P18-005](#p18-005) | S.31–S.35 | SuperAdmin | `GET /api/v1/admin/investigation-cases` | Investigations.View |
| [P18-006](#p18-006) | S.31–S.35 | SuperAdmin | `POST /api/v1/admin/investigation-cases` | Investigations.Create; Investigations.View |
| [P18-007](#p18-007) | S.31–S.35 | SuperAdmin | `GET /api/v1/admin/investigation-cases/{caseId}` | Investigations.View |
| [P18-008](#p18-008) | S.31–S.35 | SuperAdmin | `POST /api/v1/admin/investigation-cases/{caseId}/assign` | Investigations.Assign; Investigations.View |
| [P18-009](#p18-009) | S.31–S.35 | SuperAdmin | `POST /api/v1/admin/investigation-cases/{caseId}/close` | Investigations.Close; Investigations.View |
| [P18-010](#p18-010) | S.31–S.35 | SuperAdmin | `GET /api/v1/admin/investigation-cases/{caseId}/history` | Investigations.View |
| [P18-011](#p18-011) | S.31–S.35 | SuperAdmin | `POST /api/v1/admin/investigation-cases/{caseId}/reopen` | Investigations.Reopen; Investigations.View |
| [P18-012](#p18-012) | S.31–S.35 | SuperAdmin | `POST /api/v1/admin/investigation-cases/{caseId}/resolve` | Investigations.Resolve; Investigations.View |
| [P18-013](#p18-013) | S.31–S.35 | SuperAdmin | `POST /api/v1/admin/investigation-cases/{caseId}/start-review` | Investigations.StartReview; Investigations.View |
| [P18-014](#p18-014) | S.31–S.35 | SuperAdmin | `GET /api/v1/admin/investigation-cases/filter-options` | Investigations.View |
| [P18-015](#p18-015) | S.01–S.29 / S.31–S.35 | SuperAdmin | `GET /api/v1/admin/operations/exports/health` | PlatformJobs.ViewHealth (service guard) |
| [P18-016](#p18-016) | S.01–S.29 / S.31–S.35 | SuperAdmin | `GET /api/v1/admin/operations/jobs/health` | PlatformJobs.ViewHealth |
| [P18-017](#p18-017) | S.01–S.29 / S.31–S.35 | SuperAdmin | `GET /api/v1/admin/reports/{reportCode}/drilldown` | Reports.Platform.View |
| [P18-018](#p18-018) | S.01–S.29 / S.31–S.35 | SuperAdmin | `GET /api/v1/admin/reports/{reportCode}/export` | Reports.Platform.Export + preview/source metadata permissions |
| [P18-019](#p18-019) | S.01–S.29 / S.31–S.35 | SuperAdmin | `GET /api/v1/admin/reports/adoption` | Reports.Platform.View |
| [P18-020](#p18-020) | S.01–S.29 / S.31–S.35 | SuperAdmin | `GET /api/v1/admin/reports/catalog` | Reports.Platform.View |
| [P18-021](#p18-021) | S.01–S.29 / S.31–S.35 | SuperAdmin | `GET /api/v1/admin/reports/doctor-lifecycle` | Reports.Platform.View |
| [P18-022](#p18-022) | S.01–S.29 / S.31–S.35 | SuperAdmin | `GET /api/v1/admin/reports/geography` | Reports.Platform.View |
| [P18-023](#p18-023) | S.01–S.29 / S.31–S.35 | SuperAdmin | `GET /api/v1/admin/reports/operations` | Reports.Platform.View |
| [P18-024](#p18-024) | S.01–S.29 / S.31–S.35 | SuperAdmin | `GET /api/v1/admin/reports/security-metadata` | PlatformSecurityMetadata.View |
| [P18-025](#p18-025) | D.01–D.36; report catalog | Doctor | `GET /api/v1/doctors/me/dashboard/filter-options` | Dashboards.Doctor.ViewOwn OR Reports.Doctor.ViewOwn (service guard) |
| [P18-026](#p18-026) | D.01–D.36; report catalog | Doctor | `GET /api/v1/doctors/me/dashboard/overview` | Dashboards.Doctor.ViewOwn |
| [P18-027](#p18-027) | D.43–D.48 | Doctor | `GET /api/v1/doctors/me/expense-settlements/{id}` | FinancialExpenses.ViewOwn |
| [P18-028](#p18-028) | D.43–D.48 | Doctor | `GET /api/v1/doctors/me/expense-settlements/{id}/history` | FinancialExpenses.ViewOwn |
| [P18-029](#p18-029) | D.43–D.48 | Doctor | `GET /api/v1/doctors/me/expense-settlements/{id}/receipt` | FinancialExpenses.ViewOwn |
| [P18-030](#p18-030) | D.43–D.48 | Doctor | `POST /api/v1/doctors/me/expense-settlements/{id}/reverse` | FinancialExpenses.ReverseOwn |
| [P18-031](#p18-031) | D.43–D.48 | Doctor | `GET /api/v1/doctors/me/expenses` | FinancialExpenses.ViewOwn |
| [P18-032](#p18-032) | D.43–D.48 | Doctor | `POST /api/v1/doctors/me/expenses` | FinancialExpenses.ManageOwn |
| [P18-033](#p18-033) | D.43–D.48 | Doctor | `GET /api/v1/doctors/me/expenses/{id}` | FinancialExpenses.ViewOwn |
| [P18-034](#p18-034) | D.43–D.48 | Doctor | `GET /api/v1/doctors/me/expenses/{id}/history` | FinancialExpenses.ViewOwn |
| [P18-035](#p18-035) | D.43–D.48 | Doctor | `GET /api/v1/doctors/me/expenses/categories` | FinancialExpenses.ViewOwn |
| [P18-036](#p18-036) | D.01–D.36; report catalog | Doctor | `GET /api/v1/doctors/me/expenses/dashboard` | FinancialExpenses.ViewOwn; Reports.Doctor.ViewOwn (report service) |
| [P18-037](#p18-037) | D.43–D.48 | Doctor | `GET /api/v1/doctors/me/expenses/obligations` | FinancialExpenses.ViewOwn |
| [P18-038](#p18-038) | D.43–D.48 | Doctor | `GET /api/v1/doctors/me/expenses/obligations/{id}` | FinancialExpenses.ViewOwn |
| [P18-039](#p18-039) | D.43–D.48 | Doctor | `PATCH /api/v1/doctors/me/expenses/obligations/{id}` | FinancialExpenses.ManageOwn |
| [P18-040](#p18-040) | D.43–D.48 | Doctor | `POST /api/v1/doctors/me/expenses/obligations/{id}/cancel` | FinancialExpenses.ManageOwn |
| [P18-041](#p18-041) | D.43–D.48 | Doctor | `POST /api/v1/doctors/me/expenses/obligations/{id}/confirm-actual` | FinancialExpenses.ManageOwn |
| [P18-042](#p18-042) | D.43–D.48 | Doctor | `GET /api/v1/doctors/me/expenses/obligations/{id}/history` | FinancialExpenses.ViewOwn |
| [P18-043](#p18-043) | D.43–D.48 | Doctor | `POST /api/v1/doctors/me/expenses/obligations/{id}/settle` | FinancialExpenses.SettleOwn |
| [P18-044](#p18-044) | D.43–D.48 | Doctor | `GET /api/v1/doctors/me/expenses/transactions` | FinancialExpenses.ViewOwn |
| [P18-045](#p18-045) | D.43–D.48 | Doctor | `GET /api/v1/doctors/me/finance/filter-options` | FinancialExpenses.ViewOwn OR SalaryPlans.ViewOwn (service guard) |
| [P18-046](#p18-046) | R.18–R.22; Doctor oversight | Doctor | `GET /api/v1/doctors/me/handovers` | Handovers.Doctor.ViewOwn |
| [P18-047](#p18-047) | D.37–D.42 | Doctor | `GET /api/v1/doctors/me/insights` | Insights.Doctor.ViewOwn |
| [P18-048](#p18-048) | D.37–D.42 | Doctor | `GET /api/v1/doctors/me/insights/{insightId}` | Insights.Doctor.ViewOwn |
| [P18-049](#p18-049) | D.37–D.42 | Doctor | `POST /api/v1/doctors/me/insights/{insightId}/acknowledge` | Insights.Doctor.TrackOwn; Insights.Doctor.ViewOwn |
| [P18-050](#p18-050) | D.37–D.42 | Doctor | `POST /api/v1/doctors/me/insights/{insightId}/dismiss` | Insights.Doctor.TrackOwn; Insights.Doctor.ViewOwn |
| [P18-051](#p18-051) | D.37–D.42 | Doctor | `GET /api/v1/doctors/me/insights/{insightId}/history` | Insights.Doctor.ViewOwn |
| [P18-052](#p18-052) | D.37–D.42 | Doctor | `POST /api/v1/doctors/me/insights/{insightId}/resolve` | Insights.Doctor.TrackOwn; Insights.Doctor.ViewOwn |
| [P18-053](#p18-053) | R.18–R.22; Doctor oversight | Doctor | `POST /api/v1/doctors/me/practices/{practiceId}/handovers` | Handovers.Doctor.ManageOwn; Handovers.Doctor.ViewOwn |
| [P18-054](#p18-054) | R.18–R.22; Doctor oversight | Doctor | `GET /api/v1/doctors/me/practices/{practiceId}/handovers/{handoverId}` | Handovers.Doctor.ViewOwn |
| [P18-055](#p18-055) | R.18–R.22; Doctor oversight | Doctor | `POST /api/v1/doctors/me/practices/{practiceId}/handovers/{handoverId}/acknowledge` | Handovers.Doctor.ManageOwn; Handovers.Doctor.ViewOwn |
| [P18-056](#p18-056) | R.18–R.22; Doctor oversight | Doctor | `POST /api/v1/doctors/me/practices/{practiceId}/handovers/{handoverId}/close` | Handovers.Doctor.ManageOwn; Handovers.Doctor.ViewOwn |
| [P18-057](#p18-057) | R.18–R.22; Doctor oversight | Doctor | `GET /api/v1/doctors/me/practices/{practiceId}/handovers/{handoverId}/history` | Handovers.Doctor.ViewOwn |
| [P18-058](#p18-058) | D.43–D.48 | Doctor | `GET /api/v1/doctors/me/reception-salary-plans` | SalaryPlans.ViewOwn |
| [P18-059](#p18-059) | D.43–D.48 | Doctor | `POST /api/v1/doctors/me/reception-salary-plans` | SalaryPlans.ManageOwn |
| [P18-060](#p18-060) | D.43–D.48 | Doctor | `GET /api/v1/doctors/me/reception-salary-plans/{id}` | SalaryPlans.ViewOwn |
| [P18-061](#p18-061) | D.43–D.48 | Doctor | `PUT /api/v1/doctors/me/reception-salary-plans/{id}` | SalaryPlans.ManageOwn |
| [P18-062](#p18-062) | D.43–D.48 | Doctor | `POST /api/v1/doctors/me/reception-salary-plans/{id}/end` | SalaryPlans.ManageOwn |
| [P18-063](#p18-063) | D.43–D.48 | Doctor | `GET /api/v1/doctors/me/reception-salary-plans/{id}/history` | SalaryPlans.ViewOwn |
| [P18-064](#p18-064) | D.43–D.48 | Doctor | `GET /api/v1/doctors/me/recurring-expense-plans` | FinancialExpenses.ViewOwn |
| [P18-065](#p18-065) | D.43–D.48 | Doctor | `POST /api/v1/doctors/me/recurring-expense-plans` | FinancialExpenses.ManageOwn |
| [P18-066](#p18-066) | D.43–D.48 | Doctor | `GET /api/v1/doctors/me/recurring-expense-plans/{id}` | FinancialExpenses.ViewOwn |
| [P18-067](#p18-067) | D.43–D.48 | Doctor | `PUT /api/v1/doctors/me/recurring-expense-plans/{id}` | FinancialExpenses.ManageOwn |
| [P18-068](#p18-068) | D.43–D.48 | Doctor | `POST /api/v1/doctors/me/recurring-expense-plans/{id}/end` | FinancialExpenses.ManageOwn |
| [P18-069](#p18-069) | D.43–D.48 | Doctor | `GET /api/v1/doctors/me/recurring-expense-plans/{id}/history` | FinancialExpenses.ViewOwn |
| [P18-070](#p18-070) | D.43–D.48 | Doctor | `GET /api/v1/doctors/me/recurring-expense-plans/{id}/obligations` | FinancialExpenses.ViewOwn |
| [P18-071](#p18-071) | D.43–D.48 | Doctor | `POST /api/v1/doctors/me/recurring-expense-plans/{id}/pause` | FinancialExpenses.ManageOwn |
| [P18-072](#p18-072) | D.43–D.48 | Doctor | `POST /api/v1/doctors/me/recurring-expense-plans/{id}/resume` | FinancialExpenses.ManageOwn |
| [P18-073](#p18-073) | D.01–D.36; report catalog | Doctor | `GET /api/v1/doctors/me/reports/{reportCode}` | Reports.Doctor.ViewOwn |
| [P18-074](#p18-074) | D.01–D.36; report catalog | Doctor | `GET /api/v1/doctors/me/reports/{reportCode}/drilldown` | Reports.Doctor.ViewOwn |
| [P18-075](#p18-075) | D.01–D.36; report catalog | Doctor | `GET /api/v1/doctors/me/reports/{reportCode}/export` | Reports.Doctor.ExportOperational OR Reports.Doctor.ExportFinancial (by code); corresponding preview/source read permissions |
| [P18-076](#p18-076) | D.01–D.36; report catalog | Doctor | `GET /api/v1/doctors/me/reports/catalog` | Reports.Doctor.ViewOwn |
| [P18-077](#p18-077) | D.30–D.33 | Doctor | `GET /api/v1/doctors/me/reports/patients` | Reports.Doctor.ViewOwn |
| [P18-078](#p18-078) | D.34–D.36 | Doctor | `GET /api/v1/doctors/me/reports/practice-comparison` | Reports.Doctor.ViewOwn |
| [P18-079](#p18-079) | D.15–D.24 | Doctor | `GET /api/v1/doctors/me/reports/queue` | Reports.Doctor.ViewOwn |
| [P18-080](#p18-080) | D.02–D.14 | Doctor | `GET /api/v1/doctors/me/reports/reservations` | Reports.Doctor.ViewOwn |
| [P18-081](#p18-081) | D.43–D.48 | Doctor | `GET /api/v1/doctors/me/salary-obligations` | SalaryObligations.ViewOwn |
| [P18-082](#p18-082) | D.43–D.48 | Doctor | `GET /api/v1/doctors/me/salary-obligations/{id}` | SalaryObligations.ViewOwn |
| [P18-083](#p18-083) | D.43–D.48 | Doctor | `PATCH /api/v1/doctors/me/salary-obligations/{id}` | SalaryPlans.ManageOwn |
| [P18-084](#p18-084) | D.43–D.48 | Doctor | `POST /api/v1/doctors/me/salary-obligations/{id}/cancel` | SalaryPlans.ManageOwn |
| [P18-085](#p18-085) | D.43–D.48 | Doctor | `GET /api/v1/doctors/me/salary-obligations/{id}/history` | SalaryObligations.ViewOwn |
| [P18-086](#p18-086) | D.43–D.48 | Doctor | `POST /api/v1/doctors/me/salary-obligations/{id}/settle` | SalarySettlements.RecordOwn |
| [P18-087](#p18-087) | D.43–D.48 | Doctor | `GET /api/v1/doctors/me/salary-settlements/{id}` | SalaryObligations.ViewOwn |
| [P18-088](#p18-088) | D.43–D.48 | Doctor | `GET /api/v1/doctors/me/salary-settlements/{id}/history` | SalaryObligations.ViewOwn |
| [P18-089](#p18-089) | D.43–D.48 | Doctor | `GET /api/v1/doctors/me/salary-settlements/{id}/receipt` | SalaryObligations.ViewOwn |
| [P18-090](#p18-090) | D.43–D.48 | Doctor | `POST /api/v1/doctors/me/salary-settlements/{id}/reverse` | SalarySettlements.ReverseOwn |
| [P18-091](#p18-091) | P.01–P.08 | Patient | `GET /api/v1/patients/me/dashboard/capabilities` | Dashboards.Patient.ViewOwn |
| [P18-092](#p18-092) | P.01–P.08 | Patient | `GET /api/v1/patients/me/dashboard/overview` | Dashboards.Patient.ViewOwn |
| [P18-093](#p18-093) | R.01–R.17 | Reception | `GET /api/v1/reception/attention` | Dashboards.Reception.ViewAssigned |
| [P18-094](#p18-094) | R.01–R.17 | Reception | `GET /api/v1/reception/dashboard/filter-options` | Dashboards.Reception.ViewAssigned OR Reports.Reception.ViewAssigned (service guard) |
| [P18-095](#p18-095) | R.01–R.17 | Reception | `GET /api/v1/reception/dashboard/overview` | Dashboards.Reception.ViewAssigned |
| [P18-096](#p18-096) | R.18–R.22; Doctor oversight | Reception | `GET /api/v1/reception/practices/{practiceId}/handovers` | Handovers.ViewAssigned |
| [P18-097](#p18-097) | R.18–R.22; Doctor oversight | Reception | `POST /api/v1/reception/practices/{practiceId}/handovers` | Handovers.CreateAssigned; Handovers.ViewAssigned |
| [P18-098](#p18-098) | R.18–R.22; Doctor oversight | Reception | `GET /api/v1/reception/practices/{practiceId}/handovers/{handoverId}` | Handovers.ViewAssigned |
| [P18-099](#p18-099) | R.18–R.22; Doctor oversight | Reception | `POST /api/v1/reception/practices/{practiceId}/handovers/{handoverId}/acknowledge` | Handovers.AcknowledgeAssigned; Handovers.ViewAssigned |
| [P18-100](#p18-100) | R.18–R.22; Doctor oversight | Reception | `POST /api/v1/reception/practices/{practiceId}/handovers/{handoverId}/annotations` | Handovers.AnnotateAssigned; Handovers.ViewAssigned |
| [P18-101](#p18-101) | R.18–R.22; Doctor oversight | Reception | `POST /api/v1/reception/practices/{practiceId}/handovers/{handoverId}/close` | Handovers.CloseAssigned; Handovers.ViewAssigned |
| [P18-102](#p18-102) | R.18–R.22; Doctor oversight | Reception | `GET /api/v1/reception/practices/{practiceId}/handovers/{handoverId}/history` | Handovers.ViewAssigned |
| [P18-103](#p18-103) | R.01–R.17 | Reception | `GET /api/v1/reception/practices/{practiceId}/worklist` | Dashboards.Reception.ViewAssigned |
| [P18-104](#p18-104) | R.01–R.17 | Reception | `GET /api/v1/reception/reports/{reportCode}/drilldown` | Reports.Reception.ViewAssigned |
| [P18-105](#p18-105) | R.01–R.17 | Reception | `GET /api/v1/reception/reports/{reportCode}/export` | Reports.Reception.ExportOperational + preview/source permissions + exact assignment delegation |
| [P18-106](#p18-106) | R.01–R.17 | Reception | `GET /api/v1/reception/reports/activity` | Reports.Reception.ViewAssigned |
| [P18-107](#p18-107) | R.01–R.17 | Reception | `GET /api/v1/reception/reports/catalog` | Reports.Reception.ViewAssigned |
| [P18-108](#p18-108) | R.01–R.17 | Reception | `GET /api/v1/reception/reports/queue` | Reports.Reception.ViewAssigned |
| [P18-109](#p18-109) | R.01–R.17 | Reception | `GET /api/v1/reception/reports/reservations` | Reports.Reception.ViewAssigned |

## P18-001

**NEW · S.01–S.29 / S.31–S.35 · SuperAdmin · PlatformReporting.Attention**

`GET /api/v1/admin/attention`

Permission: Dashboards.Platform.View. Scope: Current active SuperAdmin with current database role/permission; metadata or privacy-suppressed platform aggregates only. No patient clinical or Doctor salary/expense detail.

UI step: Attention in the PlatformReporting journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| pageNumber | query | False | integer / int32 |
| pageSize | query | False | integer / int32 |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `PlatformAttentionResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18PlatformApiTests; Phase18InvestigationTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/admin/attention
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "generatedAtUtc": "2026-10-14T09:00:00Z",
  "pendingDoctorCount": 0,
  "pendingSpecializationCount": 0,
  "openInvestigationCount": 0,
  "failedOutboxCount": 0,
  "items": {
    "items": null,
    "totalCount": 0,
    "pageNumber": 1,
    "pageSize": 20,
    "dataStatus": "Complete",
    "warnings": null
  },
  "warnings": [
    "Example"
  ]
}
```

## P18-002

**NEW · S.01–S.29 / S.31–S.35 · SuperAdmin · PlatformReporting.FilterOptions**

`GET /api/v1/admin/dashboard/filter-options`

Permission: Dashboards.Platform.View OR Reports.Platform.View (service guard). Scope: Current active SuperAdmin with current database role/permission; metadata or privacy-suppressed platform aggregates only. No patient clinical or Doctor salary/expense detail.

UI step: FilterOptions in the PlatformReporting journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| PracticeId | query | False | string / uuid |
| Search | query | False | string |
| PageNumber | query | False | integer / int32 |
| PageSize | query | False | integer / int32 |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `ReportFilterOptions`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18PlatformApiTests; Phase18InvestigationTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/admin/dashboard/filter-options
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "practices": [
    {}
  ],
  "segments": [
    {}
  ],
  "visitTypes": [
    {}
  ],
  "bookingSources": [
    {}
  ],
  "reservationStatuses": [
    {}
  ],
  "ticketStatuses": [
    {}
  ],
  "groupings": [
    "Day"
  ],
  "defaultReportTimeZoneId": "Example",
  "exportLimits": {
    "maximumCalendarDays": 0,
    "excelMaximumRows": 0,
    "pdfMaximumRows": 0,
    "excelMaximumBytes": 0,
    "pdfMaximumBytes": 0
  },
  "pagination": {
    "pageNumber": 1,
    "pageSize": 20,
    "practiceTotalCount": 0,
    "segmentTotalCount": 0,
    "visitTypeTotalCount": 0,
    "receptionTotalCount": 0
  }
}
```

## P18-003

**NEW · S.01–S.29 / S.31–S.35 · SuperAdmin · PlatformReporting.Overview**

`GET /api/v1/admin/dashboard/overview`

Permission: Dashboards.Platform.View. Scope: Current active SuperAdmin with current database role/permission; metadata or privacy-suppressed platform aggregates only. No patient clinical or Doctor salary/expense detail.

UI step: Overview in the PlatformReporting journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| FromDate | query | False | string / date |
| ToDate | query | False | string / date |
| PracticeId | query | False | string / uuid |
| DoctorId | query | False | string / uuid |
| SegmentId | query | False | string / uuid |
| VisitTypeId | query | False | string / uuid |
| BookingSource | query | False | ReservationBookingSource |
| ReservationStatus | query | False | ReservationStatus |
| TicketStatus | query | False | TicketStatus |
| AsOfUtc | query | False | string / date-time |
| ReportTimeZoneId | query | False | string |
| Grouping | query | False | ReportGrouping |
| ComparePreviousPeriod | query | False | boolean |
| PageNumber | query | False | integer / int32 |
| PageSize | query | False | integer / int32 |
| SortBy | query | False | string |
| SortDirection | query | False | string |
| ExpenseCategory | query | False | ExpenseCategoryCode |
| ReceptionId | query | False | string / uuid |
| PlanId | query | False | string / uuid |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `ReportModel`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18PlatformApiTests; Phase18InvestigationTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/admin/dashboard/overview?fromDate=2026-10-01&toDate=2026-10-14
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "reportCode": "Example",
  "titleAr": "مراجعة تشغيلية",
  "titleEn": "Example",
  "description": "Example",
  "appliedFilters": {
    "fromDate": null,
    "toDate": null,
    "practiceId": null,
    "doctorId": null,
    "segmentId": null,
    "visitTypeId": null,
    "bookingSource": "Patient",
    "reservationStatus": "Active",
    "ticketStatus": "Waiting",
    "asOfUtc": null,
    "reportTimeZoneId": null,
    "grouping": "Day",
    "comparePreviousPeriod": false,
    "pageNumber": 1,
    "pageSize": 20,
    "sortBy": null,
    "sortDirection": null,
    "expenseCategory": "Rent",
    "receptionId": null,
    "planId": null
  },
  "scope": {
    "actor": "Doctor",
    "actorId": "11111111-1111-1111-1111-111111111111",
    "doctorId": null,
    "patientId": null,
    "practiceIds": null,
    "permissions": null
  },
  "window": {
    "fromDate": "2026-10-01",
    "toDate": "2026-10-01",
    "asOfUtc": "2026-10-14T09:00:00Z",
    "generatedAtUtc": "2026-10-14T09:00:00Z",
    "reportTimeZoneId": null
  },
  "dataFreshnessUtc": "2026-10-14T09:00:00Z",
  "dataStatus": "Complete",
  "metrics": [
    {}
  ],
  "series": [
    {}
  ],
  "breakdowns": [
    {}
  ],
  "availableActions": [
    {}
  ],
  "warnings": [
    "Example"
  ],
  "eligibleSampleCount": 0,
  "excludedSampleCount": 0,
  "records": [
    {}
  ],
  "detailRowCount": 0,
  "previousPeriodComparisons": [
    {}
  ]
}
```

## P18-004

**NEW · S.01–S.29 / S.31–S.35 · SuperAdmin · PlatformReporting.DataQuality**

`GET /api/v1/admin/data-quality/summary`

Permission: PlatformDataQuality.View. Scope: Current active SuperAdmin with current database role/permission; metadata or privacy-suppressed platform aggregates only. No patient clinical or Doctor salary/expense detail.

UI step: DataQuality in the PlatformReporting journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `PlatformDataQualityResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18PlatformApiTests; Phase18InvestigationTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/admin/data-quality/summary
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "generatedAtUtc": "2026-10-14T09:00:00Z",
  "items": [
    {}
  ],
  "warnings": [
    "Example"
  ]
}
```

## P18-005

**NEW · S.31–S.35 · SuperAdmin · Investigations.List**

`GET /api/v1/admin/investigation-cases`

Permission: Investigations.View. Scope: Current active SuperAdmin with current database role/permission; metadata or privacy-suppressed platform aggregates only. No patient clinical or Doctor salary/expense detail.

UI step: List in the Investigations journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| Status | query | False | InvestigationStatus |
| IssueType | query | False | InvestigationIssueType |
| Priority | query | False | InvestigationPriority |
| AssignedSuperAdminId | query | False | string / uuid |
| FromDate | query | False | string / date |
| ToDate | query | False | string / date |
| PageNumber | query | False | integer / int32 |
| PageSize | query | False | integer / int32 |
| SortDirection | query | False | string |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `InvestigationResponseReportPage`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18PlatformApiTests; Phase18InvestigationTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/admin/investigation-cases?fromDate=2026-10-01&toDate=2026-10-14
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "items": [
    {}
  ],
  "totalCount": 0,
  "pageNumber": 1,
  "pageSize": 20,
  "dataStatus": "Complete",
  "warnings": [
    "Example"
  ]
}
```

## P18-006

**NEW · S.31–S.35 · SuperAdmin · Investigations.Create**

`POST /api/v1/admin/investigation-cases`

Permission: Investigations.Create; Investigations.View. Scope: Current active SuperAdmin with current database role/permission; metadata or privacy-suppressed platform aggregates only. No patient clinical or Doctor salary/expense detail.

UI step: Create in the Investigations journey. Refresh available actions after success; open returned detailRoute or refresh the entity, then fetch history and the affected dashboard/report.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| Idempotency-Key | header | True | string |
| Accept-Language | header | False | string; ['ar', 'en'] |
| request | Body | False | InvestigationCreateRequest; see DTO tables and domain constraints |

Response: **200** `InvestigationResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Idempotent replay returns the recorded resource; no duplicate movement/history. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18PlatformApiTests; Phase18InvestigationTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
POST /api/v1/admin/investigation-cases
Authorization: Bearer <access-token>
Idempotency-Key: 7ce6cdbd-example-operation
Content-Type: application/json

{
  "titleAr": "مراجعة تشغيلية",
  "titleEn": "Example",
  "description": "Example",
  "issueType": "DoctorVerification",
  "priority": "Normal",
  "linkedResourceType": "None",
  "linkedResourceId": "11111111-1111-1111-1111-111111111111"
}
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "titleAr": "مراجعة تشغيلية",
  "titleEn": "Example",
  "description": "Example",
  "issueType": "DoctorVerification",
  "priority": "Normal",
  "linkedResourceType": "None",
  "linkedResourceId": "11111111-1111-1111-1111-111111111111",
  "createdByApplicationUserId": "11111111-1111-1111-1111-111111111111",
  "assignedSuperAdminId": "11111111-1111-1111-1111-111111111111",
  "status": "Open",
  "resolutionType": "Corrected",
  "resolutionNotes": "Example",
  "closureNotes": "Example",
  "createdOnUtc": "2026-10-14T09:00:00Z",
  "modifiedOnUtc": "2026-10-14T09:00:00Z",
  "resolvedOnUtc": "2026-10-14T09:00:00Z",
  "closedOnUtc": "2026-10-14T09:00:00Z",
  "rowVersion": "AAAAAAAAB9E=",
  "availableActions": [
    {}
  ]
}
```

## P18-007

**NEW · S.31–S.35 · SuperAdmin · Investigations.Detail**

`GET /api/v1/admin/investigation-cases/{caseId}`

Permission: Investigations.View. Scope: Current active SuperAdmin with current database role/permission; metadata or privacy-suppressed platform aggregates only. No patient clinical or Doctor salary/expense detail.

UI step: Detail in the Investigations journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| caseId | path | True | string / uuid |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `InvestigationResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18PlatformApiTests; Phase18InvestigationTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/admin/investigation-cases/11111111-1111-1111-1111-111111111111
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "titleAr": "مراجعة تشغيلية",
  "titleEn": "Example",
  "description": "Example",
  "issueType": "DoctorVerification",
  "priority": "Normal",
  "linkedResourceType": "None",
  "linkedResourceId": "11111111-1111-1111-1111-111111111111",
  "createdByApplicationUserId": "11111111-1111-1111-1111-111111111111",
  "assignedSuperAdminId": "11111111-1111-1111-1111-111111111111",
  "status": "Open",
  "resolutionType": "Corrected",
  "resolutionNotes": "Example",
  "closureNotes": "Example",
  "createdOnUtc": "2026-10-14T09:00:00Z",
  "modifiedOnUtc": "2026-10-14T09:00:00Z",
  "resolvedOnUtc": "2026-10-14T09:00:00Z",
  "closedOnUtc": "2026-10-14T09:00:00Z",
  "rowVersion": "AAAAAAAAB9E=",
  "availableActions": [
    {}
  ]
}
```

## P18-008

**NEW · S.31–S.35 · SuperAdmin · Investigations.Assign**

`POST /api/v1/admin/investigation-cases/{caseId}/assign`

Permission: Investigations.Assign; Investigations.View. Scope: Current active SuperAdmin with current database role/permission; metadata or privacy-suppressed platform aggregates only. No patient clinical or Doctor salary/expense detail.

UI step: Assign in the Investigations journey. Refresh available actions after success; open returned detailRoute or refresh the entity, then fetch history and the affected dashboard/report.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| caseId | path | True | string / uuid |
| Idempotency-Key | header | True | string |
| Accept-Language | header | False | string; ['ar', 'en'] |
| request | Body | False | InvestigationActionRequest; see DTO tables and domain constraints |

Response: **200** `InvestigationResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Idempotent replay returns the recorded resource; no duplicate movement/history. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18PlatformApiTests; Phase18InvestigationTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
POST /api/v1/admin/investigation-cases/11111111-1111-1111-1111-111111111111/assign
Authorization: Bearer <access-token>
Idempotency-Key: 7ce6cdbd-example-operation
Content-Type: application/json

{
  "rowVersion": "AAAAAAAAB9E=",
  "assignedSuperAdminId": "11111111-1111-1111-1111-111111111111",
  "resolutionType": "Corrected",
  "notes": "Operational correction reviewed"
}
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "titleAr": "مراجعة تشغيلية",
  "titleEn": "Example",
  "description": "Example",
  "issueType": "DoctorVerification",
  "priority": "Normal",
  "linkedResourceType": "None",
  "linkedResourceId": "11111111-1111-1111-1111-111111111111",
  "createdByApplicationUserId": "11111111-1111-1111-1111-111111111111",
  "assignedSuperAdminId": "11111111-1111-1111-1111-111111111111",
  "status": "Open",
  "resolutionType": "Corrected",
  "resolutionNotes": "Example",
  "closureNotes": "Example",
  "createdOnUtc": "2026-10-14T09:00:00Z",
  "modifiedOnUtc": "2026-10-14T09:00:00Z",
  "resolvedOnUtc": "2026-10-14T09:00:00Z",
  "closedOnUtc": "2026-10-14T09:00:00Z",
  "rowVersion": "AAAAAAAAB9E=",
  "availableActions": [
    {}
  ]
}
```

## P18-009

**NEW · S.31–S.35 · SuperAdmin · Investigations.Close**

`POST /api/v1/admin/investigation-cases/{caseId}/close`

Permission: Investigations.Close; Investigations.View. Scope: Current active SuperAdmin with current database role/permission; metadata or privacy-suppressed platform aggregates only. No patient clinical or Doctor salary/expense detail.

UI step: Close in the Investigations journey. Refresh available actions after success; open returned detailRoute or refresh the entity, then fetch history and the affected dashboard/report.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| caseId | path | True | string / uuid |
| Idempotency-Key | header | True | string |
| Accept-Language | header | False | string; ['ar', 'en'] |
| request | Body | False | InvestigationActionRequest; see DTO tables and domain constraints |

Response: **200** `InvestigationResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Idempotent replay returns the recorded resource; no duplicate movement/history. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18PlatformApiTests; Phase18InvestigationTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
POST /api/v1/admin/investigation-cases/11111111-1111-1111-1111-111111111111/close
Authorization: Bearer <access-token>
Idempotency-Key: 7ce6cdbd-example-operation
Content-Type: application/json

{
  "rowVersion": "AAAAAAAAB9E=",
  "assignedSuperAdminId": "11111111-1111-1111-1111-111111111111",
  "resolutionType": "Corrected",
  "notes": "Operational correction reviewed"
}
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "titleAr": "مراجعة تشغيلية",
  "titleEn": "Example",
  "description": "Example",
  "issueType": "DoctorVerification",
  "priority": "Normal",
  "linkedResourceType": "None",
  "linkedResourceId": "11111111-1111-1111-1111-111111111111",
  "createdByApplicationUserId": "11111111-1111-1111-1111-111111111111",
  "assignedSuperAdminId": "11111111-1111-1111-1111-111111111111",
  "status": "Open",
  "resolutionType": "Corrected",
  "resolutionNotes": "Example",
  "closureNotes": "Example",
  "createdOnUtc": "2026-10-14T09:00:00Z",
  "modifiedOnUtc": "2026-10-14T09:00:00Z",
  "resolvedOnUtc": "2026-10-14T09:00:00Z",
  "closedOnUtc": "2026-10-14T09:00:00Z",
  "rowVersion": "AAAAAAAAB9E=",
  "availableActions": [
    {}
  ]
}
```

## P18-010

**NEW · S.31–S.35 · SuperAdmin · Investigations.History**

`GET /api/v1/admin/investigation-cases/{caseId}/history`

Permission: Investigations.View. Scope: Current active SuperAdmin with current database role/permission; metadata or privacy-suppressed platform aggregates only. No patient clinical or Doctor salary/expense detail.

UI step: History in the Investigations journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| caseId | path | True | string / uuid |
| pageNumber | query | False | integer / int32 |
| pageSize | query | False | integer / int32 |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `InvestigationHistoryResponseReportPage`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18PlatformApiTests; Phase18InvestigationTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/admin/investigation-cases/11111111-1111-1111-1111-111111111111/history
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "items": [
    {}
  ],
  "totalCount": 0,
  "pageNumber": 1,
  "pageSize": 20,
  "dataStatus": "Complete",
  "warnings": [
    "Example"
  ]
}
```

## P18-011

**NEW · S.31–S.35 · SuperAdmin · Investigations.Reopen**

`POST /api/v1/admin/investigation-cases/{caseId}/reopen`

Permission: Investigations.Reopen; Investigations.View. Scope: Current active SuperAdmin with current database role/permission; metadata or privacy-suppressed platform aggregates only. No patient clinical or Doctor salary/expense detail.

UI step: Reopen in the Investigations journey. Refresh available actions after success; open returned detailRoute or refresh the entity, then fetch history and the affected dashboard/report.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| caseId | path | True | string / uuid |
| Idempotency-Key | header | True | string |
| Accept-Language | header | False | string; ['ar', 'en'] |
| request | Body | False | InvestigationActionRequest; see DTO tables and domain constraints |

Response: **200** `InvestigationResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Idempotent replay returns the recorded resource; no duplicate movement/history. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18PlatformApiTests; Phase18InvestigationTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
POST /api/v1/admin/investigation-cases/11111111-1111-1111-1111-111111111111/reopen
Authorization: Bearer <access-token>
Idempotency-Key: 7ce6cdbd-example-operation
Content-Type: application/json

{
  "rowVersion": "AAAAAAAAB9E=",
  "assignedSuperAdminId": "11111111-1111-1111-1111-111111111111",
  "resolutionType": "Corrected",
  "notes": "Operational correction reviewed"
}
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "titleAr": "مراجعة تشغيلية",
  "titleEn": "Example",
  "description": "Example",
  "issueType": "DoctorVerification",
  "priority": "Normal",
  "linkedResourceType": "None",
  "linkedResourceId": "11111111-1111-1111-1111-111111111111",
  "createdByApplicationUserId": "11111111-1111-1111-1111-111111111111",
  "assignedSuperAdminId": "11111111-1111-1111-1111-111111111111",
  "status": "Open",
  "resolutionType": "Corrected",
  "resolutionNotes": "Example",
  "closureNotes": "Example",
  "createdOnUtc": "2026-10-14T09:00:00Z",
  "modifiedOnUtc": "2026-10-14T09:00:00Z",
  "resolvedOnUtc": "2026-10-14T09:00:00Z",
  "closedOnUtc": "2026-10-14T09:00:00Z",
  "rowVersion": "AAAAAAAAB9E=",
  "availableActions": [
    {}
  ]
}
```

## P18-012

**NEW · S.31–S.35 · SuperAdmin · Investigations.Resolve**

`POST /api/v1/admin/investigation-cases/{caseId}/resolve`

Permission: Investigations.Resolve; Investigations.View. Scope: Current active SuperAdmin with current database role/permission; metadata or privacy-suppressed platform aggregates only. No patient clinical or Doctor salary/expense detail.

UI step: Resolve in the Investigations journey. Refresh available actions after success; open returned detailRoute or refresh the entity, then fetch history and the affected dashboard/report.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| caseId | path | True | string / uuid |
| Idempotency-Key | header | True | string |
| Accept-Language | header | False | string; ['ar', 'en'] |
| request | Body | False | InvestigationActionRequest; see DTO tables and domain constraints |

Response: **200** `InvestigationResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Idempotent replay returns the recorded resource; no duplicate movement/history. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18PlatformApiTests; Phase18InvestigationTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
POST /api/v1/admin/investigation-cases/11111111-1111-1111-1111-111111111111/resolve
Authorization: Bearer <access-token>
Idempotency-Key: 7ce6cdbd-example-operation
Content-Type: application/json

{
  "rowVersion": "AAAAAAAAB9E=",
  "assignedSuperAdminId": "11111111-1111-1111-1111-111111111111",
  "resolutionType": "Corrected",
  "notes": "Operational correction reviewed"
}
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "titleAr": "مراجعة تشغيلية",
  "titleEn": "Example",
  "description": "Example",
  "issueType": "DoctorVerification",
  "priority": "Normal",
  "linkedResourceType": "None",
  "linkedResourceId": "11111111-1111-1111-1111-111111111111",
  "createdByApplicationUserId": "11111111-1111-1111-1111-111111111111",
  "assignedSuperAdminId": "11111111-1111-1111-1111-111111111111",
  "status": "Open",
  "resolutionType": "Corrected",
  "resolutionNotes": "Example",
  "closureNotes": "Example",
  "createdOnUtc": "2026-10-14T09:00:00Z",
  "modifiedOnUtc": "2026-10-14T09:00:00Z",
  "resolvedOnUtc": "2026-10-14T09:00:00Z",
  "closedOnUtc": "2026-10-14T09:00:00Z",
  "rowVersion": "AAAAAAAAB9E=",
  "availableActions": [
    {}
  ]
}
```

## P18-013

**NEW · S.31–S.35 · SuperAdmin · Investigations.StartReview**

`POST /api/v1/admin/investigation-cases/{caseId}/start-review`

Permission: Investigations.StartReview; Investigations.View. Scope: Current active SuperAdmin with current database role/permission; metadata or privacy-suppressed platform aggregates only. No patient clinical or Doctor salary/expense detail.

UI step: StartReview in the Investigations journey. Refresh available actions after success; open returned detailRoute or refresh the entity, then fetch history and the affected dashboard/report.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| caseId | path | True | string / uuid |
| Idempotency-Key | header | True | string |
| Accept-Language | header | False | string; ['ar', 'en'] |
| request | Body | False | InvestigationActionRequest; see DTO tables and domain constraints |

Response: **200** `InvestigationResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Idempotent replay returns the recorded resource; no duplicate movement/history. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18PlatformApiTests; Phase18InvestigationTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
POST /api/v1/admin/investigation-cases/11111111-1111-1111-1111-111111111111/start-review
Authorization: Bearer <access-token>
Idempotency-Key: 7ce6cdbd-example-operation
Content-Type: application/json

{
  "rowVersion": "AAAAAAAAB9E=",
  "assignedSuperAdminId": "11111111-1111-1111-1111-111111111111",
  "resolutionType": "Corrected",
  "notes": "Operational correction reviewed"
}
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "titleAr": "مراجعة تشغيلية",
  "titleEn": "Example",
  "description": "Example",
  "issueType": "DoctorVerification",
  "priority": "Normal",
  "linkedResourceType": "None",
  "linkedResourceId": "11111111-1111-1111-1111-111111111111",
  "createdByApplicationUserId": "11111111-1111-1111-1111-111111111111",
  "assignedSuperAdminId": "11111111-1111-1111-1111-111111111111",
  "status": "Open",
  "resolutionType": "Corrected",
  "resolutionNotes": "Example",
  "closureNotes": "Example",
  "createdOnUtc": "2026-10-14T09:00:00Z",
  "modifiedOnUtc": "2026-10-14T09:00:00Z",
  "resolvedOnUtc": "2026-10-14T09:00:00Z",
  "closedOnUtc": "2026-10-14T09:00:00Z",
  "rowVersion": "AAAAAAAAB9E=",
  "availableActions": [
    {}
  ]
}
```

## P18-014

**NEW · S.31–S.35 · SuperAdmin · Investigations.FilterOptions**

`GET /api/v1/admin/investigation-cases/filter-options`

Permission: Investigations.View. Scope: Current active SuperAdmin with current database role/permission; metadata or privacy-suppressed platform aggregates only. No patient clinical or Doctor salary/expense detail.

UI step: FilterOptions in the Investigations journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| PracticeId | query | False | string / uuid |
| Search | query | False | string |
| PageNumber | query | False | integer / int32 |
| PageSize | query | False | integer / int32 |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `InvestigationFilterOptions`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18PlatformApiTests; Phase18InvestigationTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/admin/investigation-cases/filter-options
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "statuses": [
    {}
  ],
  "issueTypes": [
    {}
  ],
  "priorities": [
    {}
  ],
  "resolutionTypes": [
    {}
  ],
  "linkedResourceTypes": [
    {}
  ],
  "activeSuperAdmins": [
    {}
  ],
  "totalActiveSuperAdmins": 0,
  "pageNumber": 1,
  "pageSize": 20
}
```

## P18-015

**NEW · S.01–S.29 / S.31–S.35 · SuperAdmin · ExportRuntimeHealth.Health**

`GET /api/v1/admin/operations/exports/health`

Permission: PlatformJobs.ViewHealth (service guard). Scope: Current active SuperAdmin with current database role/permission; metadata or privacy-suppressed platform aggregates only. No patient clinical or Doctor salary/expense detail.

UI step: Health in the ExportRuntimeHealth journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `ExportRuntimeHealth`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18ExportApiTests; Phase18ReportExportTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/admin/operations/exports/health?format=xlsx&language=ar
Authorization: Bearer <access-token>
```

Example 200 response shape:

```text
<binary PDF or XLSX bytes; Content-Disposition supplies the filename>
```

## P18-016

**NEW · S.01–S.29 / S.31–S.35 · SuperAdmin · PlatformReporting.Jobs**

`GET /api/v1/admin/operations/jobs/health`

Permission: PlatformJobs.ViewHealth. Scope: Current active SuperAdmin with current database role/permission; metadata or privacy-suppressed platform aggregates only. No patient clinical or Doctor salary/expense detail.

UI step: Jobs in the PlatformReporting journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `PlatformJobHealthResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18PlatformApiTests; Phase18InvestigationTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/admin/operations/jobs/health
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "generatedAtUtc": "2026-10-14T09:00:00Z",
  "generationHealth": "Example",
  "generationEnabled": false,
  "pollingIntervalSeconds": 0,
  "recentRuns": [
    {}
  ],
  "outbox": {
    "pending": 0,
    "processing": 0,
    "sent": 0,
    "failed": 0,
    "oldestPendingAtUtc": null,
    "expiredClaims": 0,
    "dateBasis": null
  },
  "warnings": [
    "Example"
  ],
  "reportingPolicyRefreshHealth": "Example",
  "reportingPolicyRefreshRuns": [
    {}
  ]
}
```

## P18-017

**NEW · S.01–S.29 / S.31–S.35 · SuperAdmin · PlatformReporting.Drilldown**

`GET /api/v1/admin/reports/{reportCode}/drilldown`

Permission: Reports.Platform.View. Scope: Current active SuperAdmin with current database role/permission; metadata or privacy-suppressed platform aggregates only. No patient clinical or Doctor salary/expense detail.

UI step: Drilldown in the PlatformReporting journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| reportCode | path | True | string |
| metricCode | query | False | string |
| FromDate | query | False | string / date |
| ToDate | query | False | string / date |
| PracticeId | query | False | string / uuid |
| DoctorId | query | False | string / uuid |
| SegmentId | query | False | string / uuid |
| VisitTypeId | query | False | string / uuid |
| BookingSource | query | False | ReservationBookingSource |
| ReservationStatus | query | False | ReservationStatus |
| TicketStatus | query | False | TicketStatus |
| AsOfUtc | query | False | string / date-time |
| ReportTimeZoneId | query | False | string |
| Grouping | query | False | ReportGrouping |
| ComparePreviousPeriod | query | False | boolean |
| PageNumber | query | False | integer / int32 |
| PageSize | query | False | integer / int32 |
| SortBy | query | False | string |
| SortDirection | query | False | string |
| ExpenseCategory | query | False | ExpenseCategoryCode |
| ReceptionId | query | False | string / uuid |
| PlanId | query | False | string / uuid |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `ReportRecordReportPage`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18PlatformApiTests; Phase18InvestigationTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/admin/reports/overview/drilldown?fromDate=2026-10-01&toDate=2026-10-14&metricCode=RegisteredDoctors
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "items": [
    {}
  ],
  "totalCount": 0,
  "pageNumber": 1,
  "pageSize": 20,
  "dataStatus": "Complete",
  "warnings": [
    "Example"
  ]
}
```

## P18-018

**NEW · S.01–S.29 / S.31–S.35 · SuperAdmin · SuperAdminReportExports.Export**

`GET /api/v1/admin/reports/{reportCode}/export`

Permission: Reports.Platform.Export + preview/source metadata permissions. Scope: Current active SuperAdmin with current database role/permission; metadata or privacy-suppressed platform aggregates only. No patient clinical or Doctor salary/expense detail.

UI step: Export in the SuperAdminReportExports journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| reportCode | path | True | string |
| FromDate | query | False | string / date |
| ToDate | query | False | string / date |
| PracticeId | query | False | string / uuid |
| DoctorId | query | False | string / uuid |
| SegmentId | query | False | string / uuid |
| VisitTypeId | query | False | string / uuid |
| BookingSource | query | False | ReservationBookingSource |
| ReservationStatus | query | False | ReservationStatus |
| TicketStatus | query | False | TicketStatus |
| AsOfUtc | query | False | string / date-time |
| ReportTimeZoneId | query | False | string |
| Grouping | query | False | ReportGrouping |
| ComparePreviousPeriod | query | False | boolean |
| PageNumber | query | False | integer / int32 |
| PageSize | query | False | integer / int32 |
| SortBy | query | False | string |
| SortDirection | query | False | string |
| ExpenseCategory | query | False | ExpenseCategoryCode |
| ReceptionId | query | False | string / uuid |
| PlanId | query | False | string / uuid |
| format | query | False | string |
| language | query | False | string |
| landscape | query | False | boolean |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `string / binary`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18ExportApiTests; Phase18ReportExportTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/admin/reports/overview/export?fromDate=2026-10-01&toDate=2026-10-14&format=xlsx&language=ar
Authorization: Bearer <access-token>
```

Example 200 response shape:

```text
<binary PDF or XLSX bytes; Content-Disposition supplies the filename>
```

## P18-019

**NEW · S.01–S.29 / S.31–S.35 · SuperAdmin · PlatformReporting.Adoption**

`GET /api/v1/admin/reports/adoption`

Permission: Reports.Platform.View. Scope: Current active SuperAdmin with current database role/permission; metadata or privacy-suppressed platform aggregates only. No patient clinical or Doctor salary/expense detail.

UI step: Adoption in the PlatformReporting journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| FromDate | query | False | string / date |
| ToDate | query | False | string / date |
| PracticeId | query | False | string / uuid |
| DoctorId | query | False | string / uuid |
| SegmentId | query | False | string / uuid |
| VisitTypeId | query | False | string / uuid |
| BookingSource | query | False | ReservationBookingSource |
| ReservationStatus | query | False | ReservationStatus |
| TicketStatus | query | False | TicketStatus |
| AsOfUtc | query | False | string / date-time |
| ReportTimeZoneId | query | False | string |
| Grouping | query | False | ReportGrouping |
| ComparePreviousPeriod | query | False | boolean |
| PageNumber | query | False | integer / int32 |
| PageSize | query | False | integer / int32 |
| SortBy | query | False | string |
| SortDirection | query | False | string |
| ExpenseCategory | query | False | ExpenseCategoryCode |
| ReceptionId | query | False | string / uuid |
| PlanId | query | False | string / uuid |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `ReportModel`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18PlatformApiTests; Phase18InvestigationTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/admin/reports/adoption?fromDate=2026-10-01&toDate=2026-10-14
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "reportCode": "Example",
  "titleAr": "مراجعة تشغيلية",
  "titleEn": "Example",
  "description": "Example",
  "appliedFilters": {
    "fromDate": null,
    "toDate": null,
    "practiceId": null,
    "doctorId": null,
    "segmentId": null,
    "visitTypeId": null,
    "bookingSource": "Patient",
    "reservationStatus": "Active",
    "ticketStatus": "Waiting",
    "asOfUtc": null,
    "reportTimeZoneId": null,
    "grouping": "Day",
    "comparePreviousPeriod": false,
    "pageNumber": 1,
    "pageSize": 20,
    "sortBy": null,
    "sortDirection": null,
    "expenseCategory": "Rent",
    "receptionId": null,
    "planId": null
  },
  "scope": {
    "actor": "Doctor",
    "actorId": "11111111-1111-1111-1111-111111111111",
    "doctorId": null,
    "patientId": null,
    "practiceIds": null,
    "permissions": null
  },
  "window": {
    "fromDate": "2026-10-01",
    "toDate": "2026-10-01",
    "asOfUtc": "2026-10-14T09:00:00Z",
    "generatedAtUtc": "2026-10-14T09:00:00Z",
    "reportTimeZoneId": null
  },
  "dataFreshnessUtc": "2026-10-14T09:00:00Z",
  "dataStatus": "Complete",
  "metrics": [
    {}
  ],
  "series": [
    {}
  ],
  "breakdowns": [
    {}
  ],
  "availableActions": [
    {}
  ],
  "warnings": [
    "Example"
  ],
  "eligibleSampleCount": 0,
  "excludedSampleCount": 0,
  "records": [
    {}
  ],
  "detailRowCount": 0,
  "previousPeriodComparisons": [
    {}
  ]
}
```

## P18-020

**NEW · S.01–S.29 / S.31–S.35 · SuperAdmin · PlatformReporting.Catalog**

`GET /api/v1/admin/reports/catalog`

Permission: Reports.Platform.View. Scope: Current active SuperAdmin with current database role/permission; metadata or privacy-suppressed platform aggregates only. No patient clinical or Doctor salary/expense detail.

UI step: Catalog in the PlatformReporting journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `ReportCatalogEntry[]`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18PlatformApiTests; Phase18InvestigationTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/admin/reports/catalog
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
[
  {
    "reportCode": null,
    "titleAr": null,
    "titleEn": null,
    "description": null,
    "allowedFilters": null,
    "availableGroupings": null,
    "scope": null,
    "canPreview": false,
    "canDrillDown": false,
    "canExportPdf": false,
    "canExportExcel": false,
    "exportLimits": {},
    "requiredPermissions": null,
    "dataStatus": "Complete",
    "exportRequiredPermissions": null,
    "minimumCalendarDays": 0,
    "maximumCalendarDays": 0,
    "dataAvailability": null,
    "previewRoute": null,
    "drilldownRoute": null,
    "exportRoute": null
  }
]
```

## P18-021

**NEW · S.01–S.29 / S.31–S.35 · SuperAdmin · PlatformReporting.DoctorLifecycle**

`GET /api/v1/admin/reports/doctor-lifecycle`

Permission: Reports.Platform.View. Scope: Current active SuperAdmin with current database role/permission; metadata or privacy-suppressed platform aggregates only. No patient clinical or Doctor salary/expense detail.

UI step: DoctorLifecycle in the PlatformReporting journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| FromDate | query | False | string / date |
| ToDate | query | False | string / date |
| PracticeId | query | False | string / uuid |
| DoctorId | query | False | string / uuid |
| SegmentId | query | False | string / uuid |
| VisitTypeId | query | False | string / uuid |
| BookingSource | query | False | ReservationBookingSource |
| ReservationStatus | query | False | ReservationStatus |
| TicketStatus | query | False | TicketStatus |
| AsOfUtc | query | False | string / date-time |
| ReportTimeZoneId | query | False | string |
| Grouping | query | False | ReportGrouping |
| ComparePreviousPeriod | query | False | boolean |
| PageNumber | query | False | integer / int32 |
| PageSize | query | False | integer / int32 |
| SortBy | query | False | string |
| SortDirection | query | False | string |
| ExpenseCategory | query | False | ExpenseCategoryCode |
| ReceptionId | query | False | string / uuid |
| PlanId | query | False | string / uuid |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `ReportModel`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18PlatformApiTests; Phase18InvestigationTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/admin/reports/doctor-lifecycle?fromDate=2026-10-01&toDate=2026-10-14
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "reportCode": "Example",
  "titleAr": "مراجعة تشغيلية",
  "titleEn": "Example",
  "description": "Example",
  "appliedFilters": {
    "fromDate": null,
    "toDate": null,
    "practiceId": null,
    "doctorId": null,
    "segmentId": null,
    "visitTypeId": null,
    "bookingSource": "Patient",
    "reservationStatus": "Active",
    "ticketStatus": "Waiting",
    "asOfUtc": null,
    "reportTimeZoneId": null,
    "grouping": "Day",
    "comparePreviousPeriod": false,
    "pageNumber": 1,
    "pageSize": 20,
    "sortBy": null,
    "sortDirection": null,
    "expenseCategory": "Rent",
    "receptionId": null,
    "planId": null
  },
  "scope": {
    "actor": "Doctor",
    "actorId": "11111111-1111-1111-1111-111111111111",
    "doctorId": null,
    "patientId": null,
    "practiceIds": null,
    "permissions": null
  },
  "window": {
    "fromDate": "2026-10-01",
    "toDate": "2026-10-01",
    "asOfUtc": "2026-10-14T09:00:00Z",
    "generatedAtUtc": "2026-10-14T09:00:00Z",
    "reportTimeZoneId": null
  },
  "dataFreshnessUtc": "2026-10-14T09:00:00Z",
  "dataStatus": "Complete",
  "metrics": [
    {}
  ],
  "series": [
    {}
  ],
  "breakdowns": [
    {}
  ],
  "availableActions": [
    {}
  ],
  "warnings": [
    "Example"
  ],
  "eligibleSampleCount": 0,
  "excludedSampleCount": 0,
  "records": [
    {}
  ],
  "detailRowCount": 0,
  "previousPeriodComparisons": [
    {}
  ]
}
```

## P18-022

**NEW · S.01–S.29 / S.31–S.35 · SuperAdmin · PlatformReporting.Geography**

`GET /api/v1/admin/reports/geography`

Permission: Reports.Platform.View. Scope: Current active SuperAdmin with current database role/permission; metadata or privacy-suppressed platform aggregates only. No patient clinical or Doctor salary/expense detail.

UI step: Geography in the PlatformReporting journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| FromDate | query | False | string / date |
| ToDate | query | False | string / date |
| PracticeId | query | False | string / uuid |
| DoctorId | query | False | string / uuid |
| SegmentId | query | False | string / uuid |
| VisitTypeId | query | False | string / uuid |
| BookingSource | query | False | ReservationBookingSource |
| ReservationStatus | query | False | ReservationStatus |
| TicketStatus | query | False | TicketStatus |
| AsOfUtc | query | False | string / date-time |
| ReportTimeZoneId | query | False | string |
| Grouping | query | False | ReportGrouping |
| ComparePreviousPeriod | query | False | boolean |
| PageNumber | query | False | integer / int32 |
| PageSize | query | False | integer / int32 |
| SortBy | query | False | string |
| SortDirection | query | False | string |
| ExpenseCategory | query | False | ExpenseCategoryCode |
| ReceptionId | query | False | string / uuid |
| PlanId | query | False | string / uuid |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `ReportModel`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18PlatformApiTests; Phase18InvestigationTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/admin/reports/geography?fromDate=2026-10-01&toDate=2026-10-14
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "reportCode": "Example",
  "titleAr": "مراجعة تشغيلية",
  "titleEn": "Example",
  "description": "Example",
  "appliedFilters": {
    "fromDate": null,
    "toDate": null,
    "practiceId": null,
    "doctorId": null,
    "segmentId": null,
    "visitTypeId": null,
    "bookingSource": "Patient",
    "reservationStatus": "Active",
    "ticketStatus": "Waiting",
    "asOfUtc": null,
    "reportTimeZoneId": null,
    "grouping": "Day",
    "comparePreviousPeriod": false,
    "pageNumber": 1,
    "pageSize": 20,
    "sortBy": null,
    "sortDirection": null,
    "expenseCategory": "Rent",
    "receptionId": null,
    "planId": null
  },
  "scope": {
    "actor": "Doctor",
    "actorId": "11111111-1111-1111-1111-111111111111",
    "doctorId": null,
    "patientId": null,
    "practiceIds": null,
    "permissions": null
  },
  "window": {
    "fromDate": "2026-10-01",
    "toDate": "2026-10-01",
    "asOfUtc": "2026-10-14T09:00:00Z",
    "generatedAtUtc": "2026-10-14T09:00:00Z",
    "reportTimeZoneId": null
  },
  "dataFreshnessUtc": "2026-10-14T09:00:00Z",
  "dataStatus": "Complete",
  "metrics": [
    {}
  ],
  "series": [
    {}
  ],
  "breakdowns": [
    {}
  ],
  "availableActions": [
    {}
  ],
  "warnings": [
    "Example"
  ],
  "eligibleSampleCount": 0,
  "excludedSampleCount": 0,
  "records": [
    {}
  ],
  "detailRowCount": 0,
  "previousPeriodComparisons": [
    {}
  ]
}
```

## P18-023

**NEW · S.01–S.29 / S.31–S.35 · SuperAdmin · PlatformReporting.Operations**

`GET /api/v1/admin/reports/operations`

Permission: Reports.Platform.View. Scope: Current active SuperAdmin with current database role/permission; metadata or privacy-suppressed platform aggregates only. No patient clinical or Doctor salary/expense detail.

UI step: Operations in the PlatformReporting journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| FromDate | query | False | string / date |
| ToDate | query | False | string / date |
| PracticeId | query | False | string / uuid |
| DoctorId | query | False | string / uuid |
| SegmentId | query | False | string / uuid |
| VisitTypeId | query | False | string / uuid |
| BookingSource | query | False | ReservationBookingSource |
| ReservationStatus | query | False | ReservationStatus |
| TicketStatus | query | False | TicketStatus |
| AsOfUtc | query | False | string / date-time |
| ReportTimeZoneId | query | False | string |
| Grouping | query | False | ReportGrouping |
| ComparePreviousPeriod | query | False | boolean |
| PageNumber | query | False | integer / int32 |
| PageSize | query | False | integer / int32 |
| SortBy | query | False | string |
| SortDirection | query | False | string |
| ExpenseCategory | query | False | ExpenseCategoryCode |
| ReceptionId | query | False | string / uuid |
| PlanId | query | False | string / uuid |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `ReportModel`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18PlatformApiTests; Phase18InvestigationTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/admin/reports/operations?fromDate=2026-10-01&toDate=2026-10-14
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "reportCode": "Example",
  "titleAr": "مراجعة تشغيلية",
  "titleEn": "Example",
  "description": "Example",
  "appliedFilters": {
    "fromDate": null,
    "toDate": null,
    "practiceId": null,
    "doctorId": null,
    "segmentId": null,
    "visitTypeId": null,
    "bookingSource": "Patient",
    "reservationStatus": "Active",
    "ticketStatus": "Waiting",
    "asOfUtc": null,
    "reportTimeZoneId": null,
    "grouping": "Day",
    "comparePreviousPeriod": false,
    "pageNumber": 1,
    "pageSize": 20,
    "sortBy": null,
    "sortDirection": null,
    "expenseCategory": "Rent",
    "receptionId": null,
    "planId": null
  },
  "scope": {
    "actor": "Doctor",
    "actorId": "11111111-1111-1111-1111-111111111111",
    "doctorId": null,
    "patientId": null,
    "practiceIds": null,
    "permissions": null
  },
  "window": {
    "fromDate": "2026-10-01",
    "toDate": "2026-10-01",
    "asOfUtc": "2026-10-14T09:00:00Z",
    "generatedAtUtc": "2026-10-14T09:00:00Z",
    "reportTimeZoneId": null
  },
  "dataFreshnessUtc": "2026-10-14T09:00:00Z",
  "dataStatus": "Complete",
  "metrics": [
    {}
  ],
  "series": [
    {}
  ],
  "breakdowns": [
    {}
  ],
  "availableActions": [
    {}
  ],
  "warnings": [
    "Example"
  ],
  "eligibleSampleCount": 0,
  "excludedSampleCount": 0,
  "records": [
    {}
  ],
  "detailRowCount": 0,
  "previousPeriodComparisons": [
    {}
  ]
}
```

## P18-024

**NEW · S.01–S.29 / S.31–S.35 · SuperAdmin · PlatformReporting.SecurityMetadata**

`GET /api/v1/admin/reports/security-metadata`

Permission: PlatformSecurityMetadata.View. Scope: Current active SuperAdmin with current database role/permission; metadata or privacy-suppressed platform aggregates only. No patient clinical or Doctor salary/expense detail.

UI step: SecurityMetadata in the PlatformReporting journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| FromDate | query | False | string / date |
| ToDate | query | False | string / date |
| PracticeId | query | False | string / uuid |
| DoctorId | query | False | string / uuid |
| SegmentId | query | False | string / uuid |
| VisitTypeId | query | False | string / uuid |
| BookingSource | query | False | ReservationBookingSource |
| ReservationStatus | query | False | ReservationStatus |
| TicketStatus | query | False | TicketStatus |
| AsOfUtc | query | False | string / date-time |
| ReportTimeZoneId | query | False | string |
| Grouping | query | False | ReportGrouping |
| ComparePreviousPeriod | query | False | boolean |
| PageNumber | query | False | integer / int32 |
| PageSize | query | False | integer / int32 |
| SortBy | query | False | string |
| SortDirection | query | False | string |
| ExpenseCategory | query | False | ExpenseCategoryCode |
| ReceptionId | query | False | string / uuid |
| PlanId | query | False | string / uuid |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `ReportModel`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18PlatformApiTests; Phase18InvestigationTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/admin/reports/security-metadata?fromDate=2026-10-01&toDate=2026-10-14
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "reportCode": "Example",
  "titleAr": "مراجعة تشغيلية",
  "titleEn": "Example",
  "description": "Example",
  "appliedFilters": {
    "fromDate": null,
    "toDate": null,
    "practiceId": null,
    "doctorId": null,
    "segmentId": null,
    "visitTypeId": null,
    "bookingSource": "Patient",
    "reservationStatus": "Active",
    "ticketStatus": "Waiting",
    "asOfUtc": null,
    "reportTimeZoneId": null,
    "grouping": "Day",
    "comparePreviousPeriod": false,
    "pageNumber": 1,
    "pageSize": 20,
    "sortBy": null,
    "sortDirection": null,
    "expenseCategory": "Rent",
    "receptionId": null,
    "planId": null
  },
  "scope": {
    "actor": "Doctor",
    "actorId": "11111111-1111-1111-1111-111111111111",
    "doctorId": null,
    "patientId": null,
    "practiceIds": null,
    "permissions": null
  },
  "window": {
    "fromDate": "2026-10-01",
    "toDate": "2026-10-01",
    "asOfUtc": "2026-10-14T09:00:00Z",
    "generatedAtUtc": "2026-10-14T09:00:00Z",
    "reportTimeZoneId": null
  },
  "dataFreshnessUtc": "2026-10-14T09:00:00Z",
  "dataStatus": "Complete",
  "metrics": [
    {}
  ],
  "series": [
    {}
  ],
  "breakdowns": [
    {}
  ],
  "availableActions": [
    {}
  ],
  "warnings": [
    "Example"
  ],
  "eligibleSampleCount": 0,
  "excludedSampleCount": 0,
  "records": [
    {}
  ],
  "detailRowCount": 0,
  "previousPeriodComparisons": [
    {}
  ]
}
```

## P18-025

**NEW · D.01–D.36; report catalog · Doctor · DoctorReporting.FilterOptions**

`GET /api/v1/doctors/me/dashboard/filter-options`

Permission: Dashboards.Doctor.ViewOwn OR Reports.Doctor.ViewOwn (service guard). Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: FilterOptions in the DoctorReporting journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| PracticeId | query | False | string / uuid |
| Search | query | False | string |
| PageNumber | query | False | integer / int32 |
| PageSize | query | False | integer / int32 |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `ReportFilterOptions`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18DoctorReportingApiTests; Phase18OperationalAnalyticsTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/dashboard/filter-options
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "practices": [
    {}
  ],
  "segments": [
    {}
  ],
  "visitTypes": [
    {}
  ],
  "bookingSources": [
    {}
  ],
  "reservationStatuses": [
    {}
  ],
  "ticketStatuses": [
    {}
  ],
  "groupings": [
    "Day"
  ],
  "defaultReportTimeZoneId": "Example",
  "exportLimits": {
    "maximumCalendarDays": 0,
    "excelMaximumRows": 0,
    "pdfMaximumRows": 0,
    "excelMaximumBytes": 0,
    "pdfMaximumBytes": 0
  },
  "pagination": {
    "pageNumber": 1,
    "pageSize": 20,
    "practiceTotalCount": 0,
    "segmentTotalCount": 0,
    "visitTypeTotalCount": 0,
    "receptionTotalCount": 0
  }
}
```

## P18-026

**NEW · D.01–D.36; report catalog · Doctor · DoctorReporting.Overview**

`GET /api/v1/doctors/me/dashboard/overview`

Permission: Dashboards.Doctor.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: Overview in the DoctorReporting journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| FromDate | query | False | string / date |
| ToDate | query | False | string / date |
| PracticeId | query | False | string / uuid |
| DoctorId | query | False | string / uuid |
| SegmentId | query | False | string / uuid |
| VisitTypeId | query | False | string / uuid |
| BookingSource | query | False | ReservationBookingSource |
| ReservationStatus | query | False | ReservationStatus |
| TicketStatus | query | False | TicketStatus |
| AsOfUtc | query | False | string / date-time |
| ReportTimeZoneId | query | False | string |
| Grouping | query | False | ReportGrouping |
| ComparePreviousPeriod | query | False | boolean |
| PageNumber | query | False | integer / int32 |
| PageSize | query | False | integer / int32 |
| SortBy | query | False | string |
| SortDirection | query | False | string |
| ExpenseCategory | query | False | ExpenseCategoryCode |
| ReceptionId | query | False | string / uuid |
| PlanId | query | False | string / uuid |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `ReportModel`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18DoctorReportingApiTests; Phase18OperationalAnalyticsTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/dashboard/overview?fromDate=2026-10-01&toDate=2026-10-14
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "reportCode": "Example",
  "titleAr": "مراجعة تشغيلية",
  "titleEn": "Example",
  "description": "Example",
  "appliedFilters": {
    "fromDate": null,
    "toDate": null,
    "practiceId": null,
    "doctorId": null,
    "segmentId": null,
    "visitTypeId": null,
    "bookingSource": "Patient",
    "reservationStatus": "Active",
    "ticketStatus": "Waiting",
    "asOfUtc": null,
    "reportTimeZoneId": null,
    "grouping": "Day",
    "comparePreviousPeriod": false,
    "pageNumber": 1,
    "pageSize": 20,
    "sortBy": null,
    "sortDirection": null,
    "expenseCategory": "Rent",
    "receptionId": null,
    "planId": null
  },
  "scope": {
    "actor": "Doctor",
    "actorId": "11111111-1111-1111-1111-111111111111",
    "doctorId": null,
    "patientId": null,
    "practiceIds": null,
    "permissions": null
  },
  "window": {
    "fromDate": "2026-10-01",
    "toDate": "2026-10-01",
    "asOfUtc": "2026-10-14T09:00:00Z",
    "generatedAtUtc": "2026-10-14T09:00:00Z",
    "reportTimeZoneId": null
  },
  "dataFreshnessUtc": "2026-10-14T09:00:00Z",
  "dataStatus": "Complete",
  "metrics": [
    {}
  ],
  "series": [
    {}
  ],
  "breakdowns": [
    {}
  ],
  "availableActions": [
    {}
  ],
  "warnings": [
    "Example"
  ],
  "eligibleSampleCount": 0,
  "excludedSampleCount": 0,
  "records": [
    {}
  ],
  "detailRowCount": 0,
  "previousPeriodComparisons": [
    {}
  ]
}
```

## P18-027

**NEW · D.43–D.48 · Doctor · OperatingFinance.GetExpenseSettlementsById**

`GET /api/v1/doctors/me/expense-settlements/{id}`

Permission: FinancialExpenses.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: GetExpenseSettlementsById in the financial management journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| id | path | True | string / uuid |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `OperatingSettlementResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/expense-settlements/11111111-1111-1111-1111-111111111111
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "obligationId": "11111111-1111-1111-1111-111111111111",
  "obligationRevisionId": "11111111-1111-1111-1111-111111111111",
  "receiptNumber": "Example",
  "amount": 650,
  "currencyCode": "Example",
  "paymentMethod": "Cash",
  "reference": "Example",
  "businessDate": "2026-10-01",
  "reportTimeZoneId": "Example",
  "backdateReason": "Operational correction reviewed",
  "actorApplicationUserId": "11111111-1111-1111-1111-111111111111",
  "recordedAtUtc": "2026-10-14T09:00:00Z",
  "isReversed": false,
  "reversalId": "11111111-1111-1111-1111-111111111111",
  "obligationRowVersion": "AAAAAAAAB9E=",
  "descriptionAr": "مراجعة تشغيلية",
  "descriptionEn": "Example",
  "allocations": [
    {}
  ],
  "availableActions": [
    {}
  ]
}
```

## P18-028

**NEW · D.43–D.48 · Doctor · OperatingFinance.GetExpenseSettlementsByIdHistory**

`GET /api/v1/doctors/me/expense-settlements/{id}/history`

Permission: FinancialExpenses.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: GetExpenseSettlementsByIdHistory in the financial management journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| id | path | True | string / uuid |
| pageNumber | query | False | integer / int32 |
| pageSize | query | False | integer / int32 |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `OperatingHistoryResponseReportPage`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/expense-settlements/11111111-1111-1111-1111-111111111111/history
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "items": [
    {}
  ],
  "totalCount": 0,
  "pageNumber": 1,
  "pageSize": 20,
  "dataStatus": "Complete",
  "warnings": [
    "Example"
  ]
}
```

## P18-029

**NEW · D.43–D.48 · Doctor · OperatingFinance.GetExpenseSettlementsByIdReceipt**

`GET /api/v1/doctors/me/expense-settlements/{id}/receipt`

Permission: FinancialExpenses.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: GetExpenseSettlementsByIdReceipt in the financial management journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| id | path | True | string / uuid |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `OperatingSettlementResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/expense-settlements/11111111-1111-1111-1111-111111111111/receipt
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "obligationId": "11111111-1111-1111-1111-111111111111",
  "obligationRevisionId": "11111111-1111-1111-1111-111111111111",
  "receiptNumber": "Example",
  "amount": 650,
  "currencyCode": "Example",
  "paymentMethod": "Cash",
  "reference": "Example",
  "businessDate": "2026-10-01",
  "reportTimeZoneId": "Example",
  "backdateReason": "Operational correction reviewed",
  "actorApplicationUserId": "11111111-1111-1111-1111-111111111111",
  "recordedAtUtc": "2026-10-14T09:00:00Z",
  "isReversed": false,
  "reversalId": "11111111-1111-1111-1111-111111111111",
  "obligationRowVersion": "AAAAAAAAB9E=",
  "descriptionAr": "مراجعة تشغيلية",
  "descriptionEn": "Example",
  "allocations": [
    {}
  ],
  "availableActions": [
    {}
  ]
}
```

## P18-030

**NEW · D.43–D.48 · Doctor · OperatingFinance.PostExpenseSettlementsByIdReverse**

`POST /api/v1/doctors/me/expense-settlements/{id}/reverse`

Permission: FinancialExpenses.ReverseOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: PostExpenseSettlementsByIdReverse in the financial management journey. Refresh available actions after success; open returned detailRoute or refresh the entity, then fetch history and the affected dashboard/report.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| id | path | True | string / uuid |
| Idempotency-Key | header | True | string |
| Accept-Language | header | False | string; ['ar', 'en'] |
| request | Body | False | OperatingReversalRequest; see DTO tables and domain constraints |

Response: **200** `OperatingFinanceWriteResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Idempotent replay returns the recorded resource; no duplicate movement/history. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
POST /api/v1/doctors/me/expense-settlements/11111111-1111-1111-1111-111111111111/reverse
Authorization: Bearer <access-token>
Idempotency-Key: 7ce6cdbd-example-operation
Content-Type: application/json

{
  "businessDate": "2026-10-01",
  "rowVersion": "AAAAAAAAB9E=",
  "reason": "Operational correction reviewed",
  "reportTimeZoneId": "Example"
}
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "resourceType": "Example",
  "detailRoute": "Example",
  "rowVersion": "AAAAAAAAB9E=",
  "obligationId": "11111111-1111-1111-1111-111111111111",
  "settlementId": "11111111-1111-1111-1111-111111111111"
}
```

## P18-031

**NEW · D.43–D.48 · Doctor · OperatingFinance.GetExpenses**

`GET /api/v1/doctors/me/expenses`

Permission: FinancialExpenses.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: GetExpenses in the financial management journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| FromDate | query | False | string / date |
| ToDate | query | False | string / date |
| PracticeId | query | False | string / uuid |
| ReceptionId | query | False | string / uuid |
| PlanId | query | False | string / uuid |
| PlanStatus | query | False | OperatingPlanStatus |
| ObligationStatus | query | False | OperatingObligationStatus |
| Category | query | False | ExpenseCategoryCode |
| PageNumber | query | False | integer / int32 |
| PageSize | query | False | integer / int32 |
| SortBy | query | False | string |
| SortDirection | query | False | string |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `OperatingObligationResponseReportPage`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/expenses?fromDate=2026-10-01&toDate=2026-10-14
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "items": [
    {}
  ],
  "totalCount": 0,
  "pageNumber": 1,
  "pageSize": 20,
  "dataStatus": "Complete",
  "warnings": [
    "Example"
  ]
}
```

## P18-032

**NEW · D.43–D.48 · Doctor · OperatingFinance.PostExpenses**

`POST /api/v1/doctors/me/expenses`

Permission: FinancialExpenses.ManageOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: PostExpenses in the financial management journey. Refresh available actions after success; open returned detailRoute or refresh the entity, then fetch history and the affected dashboard/report.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| Idempotency-Key | header | True | string |
| Accept-Language | header | False | string; ['ar', 'en'] |
| request | Body | False | OperatingExpenseRequest; see DTO tables and domain constraints |

Response: **200** `OperatingFinanceWriteResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Idempotent replay returns the recorded resource; no duplicate movement/history. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
POST /api/v1/doctors/me/expenses
Authorization: Bearer <access-token>
Idempotency-Key: 7ce6cdbd-example-operation
Content-Type: application/json

{
  "amount": 650,
  "businessDate": "2026-10-01",
  "dueBusinessDate": "2026-10-01",
  "category": "Rent",
  "descriptionAr": "مراجعة تشغيلية",
  "allocations": [
    {}
  ],
  "reason": "Operational correction reviewed",
  "descriptionEn": "Example",
  "paidNow": false,
  "payment": {
    "amount": 650,
    "paidBusinessDate": "2026-10-01",
    "paymentMethod": "Cash",
    "rowVersion": null,
    "reference": null,
    "backdateReason": null,
    "reportTimeZoneId": null
  }
}
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "resourceType": "Example",
  "detailRoute": "Example",
  "rowVersion": "AAAAAAAAB9E=",
  "obligationId": "11111111-1111-1111-1111-111111111111",
  "settlementId": "11111111-1111-1111-1111-111111111111"
}
```

## P18-033

**NEW · D.43–D.48 · Doctor · OperatingFinance.GetExpensesById**

`GET /api/v1/doctors/me/expenses/{id}`

Permission: FinancialExpenses.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: GetExpensesById in the financial management journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| id | path | True | string / uuid |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `OperatingObligationResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/expenses/11111111-1111-1111-1111-111111111111
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "doctorId": "11111111-1111-1111-1111-111111111111",
  "planId": "11111111-1111-1111-1111-111111111111",
  "receptionId": "11111111-1111-1111-1111-111111111111",
  "kind": "Salary",
  "occurrenceKey": "Example",
  "periodYear": 0,
  "periodMonth": 0,
  "businessDate": "2026-10-01",
  "dueBusinessDate": "2026-10-01",
  "plannedAmount": 650,
  "amount": 650,
  "currencyCode": "Example",
  "amountPolicy": "Fixed",
  "actualAmountConfirmed": false,
  "category": "Rent",
  "descriptionAr": "مراجعة تشغيلية",
  "descriptionEn": "Example",
  "status": "Unpaid",
  "dueState": "Example",
  "activeSettlementId": "11111111-1111-1111-1111-111111111111",
  "allocations": [
    {}
  ],
  "recordedAtUtc": "2026-10-14T09:00:00Z",
  "rowVersion": "AAAAAAAAB9E=",
  "availableActions": [
    {}
  ]
}
```

## P18-034

**NEW · D.43–D.48 · Doctor · OperatingFinance.GetExpensesByIdHistory**

`GET /api/v1/doctors/me/expenses/{id}/history`

Permission: FinancialExpenses.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: GetExpensesByIdHistory in the financial management journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| id | path | True | string / uuid |
| pageNumber | query | False | integer / int32 |
| pageSize | query | False | integer / int32 |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `OperatingHistoryResponseReportPage`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/expenses/11111111-1111-1111-1111-111111111111/history
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "items": [
    {}
  ],
  "totalCount": 0,
  "pageNumber": 1,
  "pageSize": 20,
  "dataStatus": "Complete",
  "warnings": [
    "Example"
  ]
}
```

## P18-035

**NEW · D.43–D.48 · Doctor · OperatingFinance.Categories**

`GET /api/v1/doctors/me/expenses/categories`

Permission: FinancialExpenses.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: Categories in the financial management journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `ReportOption[]`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/expenses/categories
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
[
  {
    "code": null,
    "nameAr": null,
    "nameEn": null,
    "id": null
  }
]
```

## P18-036

**NEW · D.01–D.36; report catalog · Doctor · DoctorReporting.Expenses**

`GET /api/v1/doctors/me/expenses/dashboard`

Permission: FinancialExpenses.ViewOwn; Reports.Doctor.ViewOwn (report service). Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: Expenses in the DoctorReporting journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| FromDate | query | False | string / date |
| ToDate | query | False | string / date |
| PracticeId | query | False | string / uuid |
| DoctorId | query | False | string / uuid |
| SegmentId | query | False | string / uuid |
| VisitTypeId | query | False | string / uuid |
| BookingSource | query | False | ReservationBookingSource |
| ReservationStatus | query | False | ReservationStatus |
| TicketStatus | query | False | TicketStatus |
| AsOfUtc | query | False | string / date-time |
| ReportTimeZoneId | query | False | string |
| Grouping | query | False | ReportGrouping |
| ComparePreviousPeriod | query | False | boolean |
| PageNumber | query | False | integer / int32 |
| PageSize | query | False | integer / int32 |
| SortBy | query | False | string |
| SortDirection | query | False | string |
| ExpenseCategory | query | False | ExpenseCategoryCode |
| ReceptionId | query | False | string / uuid |
| PlanId | query | False | string / uuid |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `ReportModel`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18DoctorReportingApiTests; Phase18OperationalAnalyticsTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/expenses/dashboard?fromDate=2026-10-01&toDate=2026-10-14
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "reportCode": "Example",
  "titleAr": "مراجعة تشغيلية",
  "titleEn": "Example",
  "description": "Example",
  "appliedFilters": {
    "fromDate": null,
    "toDate": null,
    "practiceId": null,
    "doctorId": null,
    "segmentId": null,
    "visitTypeId": null,
    "bookingSource": "Patient",
    "reservationStatus": "Active",
    "ticketStatus": "Waiting",
    "asOfUtc": null,
    "reportTimeZoneId": null,
    "grouping": "Day",
    "comparePreviousPeriod": false,
    "pageNumber": 1,
    "pageSize": 20,
    "sortBy": null,
    "sortDirection": null,
    "expenseCategory": "Rent",
    "receptionId": null,
    "planId": null
  },
  "scope": {
    "actor": "Doctor",
    "actorId": "11111111-1111-1111-1111-111111111111",
    "doctorId": null,
    "patientId": null,
    "practiceIds": null,
    "permissions": null
  },
  "window": {
    "fromDate": "2026-10-01",
    "toDate": "2026-10-01",
    "asOfUtc": "2026-10-14T09:00:00Z",
    "generatedAtUtc": "2026-10-14T09:00:00Z",
    "reportTimeZoneId": null
  },
  "dataFreshnessUtc": "2026-10-14T09:00:00Z",
  "dataStatus": "Complete",
  "metrics": [
    {}
  ],
  "series": [
    {}
  ],
  "breakdowns": [
    {}
  ],
  "availableActions": [
    {}
  ],
  "warnings": [
    "Example"
  ],
  "eligibleSampleCount": 0,
  "excludedSampleCount": 0,
  "records": [
    {}
  ],
  "detailRowCount": 0,
  "previousPeriodComparisons": [
    {}
  ]
}
```

## P18-037

**NEW · D.43–D.48 · Doctor · OperatingFinance.GetExpensesObligations**

`GET /api/v1/doctors/me/expenses/obligations`

Permission: FinancialExpenses.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: GetExpensesObligations in the financial management journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| FromDate | query | False | string / date |
| ToDate | query | False | string / date |
| PracticeId | query | False | string / uuid |
| ReceptionId | query | False | string / uuid |
| PlanId | query | False | string / uuid |
| PlanStatus | query | False | OperatingPlanStatus |
| ObligationStatus | query | False | OperatingObligationStatus |
| Category | query | False | ExpenseCategoryCode |
| PageNumber | query | False | integer / int32 |
| PageSize | query | False | integer / int32 |
| SortBy | query | False | string |
| SortDirection | query | False | string |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `OperatingObligationResponseReportPage`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/expenses/obligations?fromDate=2026-10-01&toDate=2026-10-14
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "items": [
    {}
  ],
  "totalCount": 0,
  "pageNumber": 1,
  "pageSize": 20,
  "dataStatus": "Complete",
  "warnings": [
    "Example"
  ]
}
```

## P18-038

**NEW · D.43–D.48 · Doctor · OperatingFinance.GetExpensesObligationsById**

`GET /api/v1/doctors/me/expenses/obligations/{id}`

Permission: FinancialExpenses.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: GetExpensesObligationsById in the financial management journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| id | path | True | string / uuid |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `OperatingObligationResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/expenses/obligations/11111111-1111-1111-1111-111111111111
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "doctorId": "11111111-1111-1111-1111-111111111111",
  "planId": "11111111-1111-1111-1111-111111111111",
  "receptionId": "11111111-1111-1111-1111-111111111111",
  "kind": "Salary",
  "occurrenceKey": "Example",
  "periodYear": 0,
  "periodMonth": 0,
  "businessDate": "2026-10-01",
  "dueBusinessDate": "2026-10-01",
  "plannedAmount": 650,
  "amount": 650,
  "currencyCode": "Example",
  "amountPolicy": "Fixed",
  "actualAmountConfirmed": false,
  "category": "Rent",
  "descriptionAr": "مراجعة تشغيلية",
  "descriptionEn": "Example",
  "status": "Unpaid",
  "dueState": "Example",
  "activeSettlementId": "11111111-1111-1111-1111-111111111111",
  "allocations": [
    {}
  ],
  "recordedAtUtc": "2026-10-14T09:00:00Z",
  "rowVersion": "AAAAAAAAB9E=",
  "availableActions": [
    {}
  ]
}
```

## P18-039

**NEW · D.43–D.48 · Doctor · OperatingFinance.PatchExpensesObligationsById**

`PATCH /api/v1/doctors/me/expenses/obligations/{id}`

Permission: FinancialExpenses.ManageOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: PatchExpensesObligationsById in the financial management journey. Refresh available actions after success; open returned detailRoute or refresh the entity, then fetch history and the affected dashboard/report.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| id | path | True | string / uuid |
| Idempotency-Key | header | True | string |
| Accept-Language | header | False | string; ['ar', 'en'] |
| request | Body | False | OperatingCorrectionRequest; see DTO tables and domain constraints |

Response: **200** `OperatingFinanceWriteResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Idempotent replay returns the recorded resource; no duplicate movement/history. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
PATCH /api/v1/doctors/me/expenses/obligations/11111111-1111-1111-1111-111111111111
Authorization: Bearer <access-token>
Idempotency-Key: 7ce6cdbd-example-operation
Content-Type: application/json

{
  "amount": 650,
  "dueBusinessDate": "2026-10-01",
  "category": "Rent",
  "descriptionAr": "مراجعة تشغيلية",
  "allocations": [
    {}
  ],
  "reason": "Operational correction reviewed",
  "rowVersion": "AAAAAAAAB9E=",
  "descriptionEn": "Example"
}
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "resourceType": "Example",
  "detailRoute": "Example",
  "rowVersion": "AAAAAAAAB9E=",
  "obligationId": "11111111-1111-1111-1111-111111111111",
  "settlementId": "11111111-1111-1111-1111-111111111111"
}
```

## P18-040

**NEW · D.43–D.48 · Doctor · OperatingFinance.PostExpensesObligationsByIdCancel**

`POST /api/v1/doctors/me/expenses/obligations/{id}/cancel`

Permission: FinancialExpenses.ManageOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: PostExpensesObligationsByIdCancel in the financial management journey. Refresh available actions after success; open returned detailRoute or refresh the entity, then fetch history and the affected dashboard/report.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| id | path | True | string / uuid |
| Idempotency-Key | header | True | string |
| Accept-Language | header | False | string; ['ar', 'en'] |
| request | Body | False | OperatingTransitionRequest; see DTO tables and domain constraints |

Response: **200** `OperatingFinanceWriteResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Idempotent replay returns the recorded resource; no duplicate movement/history. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
POST /api/v1/doctors/me/expenses/obligations/11111111-1111-1111-1111-111111111111/cancel
Authorization: Bearer <access-token>
Idempotency-Key: 7ce6cdbd-example-operation
Content-Type: application/json

{
  "rowVersion": "AAAAAAAAB9E=",
  "reason": "Operational correction reviewed"
}
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "resourceType": "Example",
  "detailRoute": "Example",
  "rowVersion": "AAAAAAAAB9E=",
  "obligationId": "11111111-1111-1111-1111-111111111111",
  "settlementId": "11111111-1111-1111-1111-111111111111"
}
```

## P18-041

**NEW · D.43–D.48 · Doctor · OperatingFinance.PostExpensesObligationsByIdConfirmActual**

`POST /api/v1/doctors/me/expenses/obligations/{id}/confirm-actual`

Permission: FinancialExpenses.ManageOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: PostExpensesObligationsByIdConfirmActual in the financial management journey. Refresh available actions after success; open returned detailRoute or refresh the entity, then fetch history and the affected dashboard/report.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| id | path | True | string / uuid |
| Idempotency-Key | header | True | string |
| Accept-Language | header | False | string; ['ar', 'en'] |
| request | Body | False | OperatingActualAmountRequest; see DTO tables and domain constraints |

Response: **200** `OperatingFinanceWriteResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Idempotent replay returns the recorded resource; no duplicate movement/history. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
POST /api/v1/doctors/me/expenses/obligations/11111111-1111-1111-1111-111111111111/confirm-actual
Authorization: Bearer <access-token>
Idempotency-Key: 7ce6cdbd-example-operation
Content-Type: application/json

{
  "amount": 650,
  "rowVersion": "AAAAAAAAB9E=",
  "reason": "Operational correction reviewed"
}
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "resourceType": "Example",
  "detailRoute": "Example",
  "rowVersion": "AAAAAAAAB9E=",
  "obligationId": "11111111-1111-1111-1111-111111111111",
  "settlementId": "11111111-1111-1111-1111-111111111111"
}
```

## P18-042

**NEW · D.43–D.48 · Doctor · OperatingFinance.GetExpensesObligationsByIdHistory**

`GET /api/v1/doctors/me/expenses/obligations/{id}/history`

Permission: FinancialExpenses.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: GetExpensesObligationsByIdHistory in the financial management journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| id | path | True | string / uuid |
| pageNumber | query | False | integer / int32 |
| pageSize | query | False | integer / int32 |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `OperatingHistoryResponseReportPage`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/expenses/obligations/11111111-1111-1111-1111-111111111111/history
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "items": [
    {}
  ],
  "totalCount": 0,
  "pageNumber": 1,
  "pageSize": 20,
  "dataStatus": "Complete",
  "warnings": [
    "Example"
  ]
}
```

## P18-043

**NEW · D.43–D.48 · Doctor · OperatingFinance.PostExpensesObligationsByIdSettle**

`POST /api/v1/doctors/me/expenses/obligations/{id}/settle`

Permission: FinancialExpenses.SettleOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: PostExpensesObligationsByIdSettle in the financial management journey. Refresh available actions after success; open returned detailRoute or refresh the entity, then fetch history and the affected dashboard/report.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| id | path | True | string / uuid |
| Idempotency-Key | header | True | string |
| Accept-Language | header | False | string; ['ar', 'en'] |
| request | Body | False | OperatingSettlementRequest; see DTO tables and domain constraints |

Response: **200** `OperatingFinanceWriteResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Idempotent replay returns the recorded resource; no duplicate movement/history. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
POST /api/v1/doctors/me/expenses/obligations/11111111-1111-1111-1111-111111111111/settle
Authorization: Bearer <access-token>
Idempotency-Key: 7ce6cdbd-example-operation
Content-Type: application/json

{
  "amount": 650,
  "paidBusinessDate": "2026-10-01",
  "paymentMethod": "Cash",
  "rowVersion": "AAAAAAAAB9E=",
  "reference": "Example",
  "backdateReason": "Operational correction reviewed",
  "reportTimeZoneId": "Example"
}
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "resourceType": "Example",
  "detailRoute": "Example",
  "rowVersion": "AAAAAAAAB9E=",
  "obligationId": "11111111-1111-1111-1111-111111111111",
  "settlementId": "11111111-1111-1111-1111-111111111111"
}
```

## P18-044

**NEW · D.43–D.48 · Doctor · OperatingFinance.GetExpensesTransactions**

`GET /api/v1/doctors/me/expenses/transactions`

Permission: FinancialExpenses.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: GetExpensesTransactions in the financial management journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| FromDate | query | False | string / date |
| ToDate | query | False | string / date |
| PracticeId | query | False | string / uuid |
| ReceptionId | query | False | string / uuid |
| PlanId | query | False | string / uuid |
| PlanStatus | query | False | OperatingPlanStatus |
| ObligationStatus | query | False | OperatingObligationStatus |
| Category | query | False | ExpenseCategoryCode |
| PageNumber | query | False | integer / int32 |
| PageSize | query | False | integer / int32 |
| SortBy | query | False | string |
| SortDirection | query | False | string |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `OperatingMovementResponseReportPage`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/expenses/transactions?fromDate=2026-10-01&toDate=2026-10-14
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "items": [
    {}
  ],
  "totalCount": 0,
  "pageNumber": 1,
  "pageSize": 20,
  "dataStatus": "Complete",
  "warnings": [
    "Example"
  ]
}
```

## P18-045

**NEW · D.43–D.48 · Doctor · OperatingFinance.FilterOptions**

`GET /api/v1/doctors/me/finance/filter-options`

Permission: FinancialExpenses.ViewOwn OR SalaryPlans.ViewOwn (service guard). Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: FilterOptions in the financial management journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| PracticeId | query | False | string / uuid |
| Search | query | False | string |
| PageNumber | query | False | integer / int32 |
| PageSize | query | False | integer / int32 |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `OperatingFinanceOptions`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/finance/filter-options
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "practices": [
    {}
  ],
  "receptions": [
    {}
  ],
  "categories": [
    {}
  ],
  "paymentMethods": [
    {}
  ],
  "cadences": [
    {}
  ],
  "amountPolicies": [
    {}
  ],
  "currencyCode": "Example",
  "noProration": false,
  "defaultReportTimeZoneId": "Example",
  "pagination": {
    "pageNumber": 1,
    "pageSize": 20,
    "practiceTotalCount": 0,
    "segmentTotalCount": 0,
    "visitTypeTotalCount": 0,
    "receptionTotalCount": 0
  }
}
```

## P18-046

**NEW · R.18–R.22; Doctor oversight · Doctor · DoctorHandovers.List**

`GET /api/v1/doctors/me/handovers`

Permission: Handovers.Doctor.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: List in the DoctorHandovers journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| PracticeId | query | False | string / uuid |
| Status | query | False | HandoverStatus |
| FromDate | query | False | string / date |
| ToDate | query | False | string / date |
| PageNumber | query | False | integer / int32 |
| PageSize | query | False | integer / int32 |
| SortDirection | query | False | string |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `HandoverResponseReportPage`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18ReceptionApiTests; Phase18HandoverTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/handovers?fromDate=2026-10-01&toDate=2026-10-14
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "items": [
    {}
  ],
  "totalCount": 0,
  "pageNumber": 1,
  "pageSize": 20,
  "dataStatus": "Complete",
  "warnings": [
    "Example"
  ]
}
```

## P18-047

**NEW · D.37–D.42 · Doctor · DoctorInsights.List**

`GET /api/v1/doctors/me/insights`

Permission: Insights.Doctor.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: List in the DoctorInsights journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| practiceId | query | False | string / uuid |
| pageNumber | query | False | integer / int32 |
| pageSize | query | False | integer / int32 |
| state | query | False | DoctorInsightState |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `DoctorInsightResponseReportPage`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18DoctorReportingApiTests; Phase18OperationalAnalyticsTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/insights
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "items": [
    {}
  ],
  "totalCount": 0,
  "pageNumber": 1,
  "pageSize": 20,
  "dataStatus": "Complete",
  "warnings": [
    "Example"
  ]
}
```

## P18-048

**NEW · D.37–D.42 · Doctor · DoctorInsights.Detail**

`GET /api/v1/doctors/me/insights/{insightId}`

Permission: Insights.Doctor.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: Detail in the DoctorInsights journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| insightId | path | True | string / uuid |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `DoctorInsightResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18DoctorReportingApiTests; Phase18OperationalAnalyticsTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/insights/11111111-1111-1111-1111-111111111111
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "insightId": "11111111-1111-1111-1111-111111111111",
  "practiceId": "11111111-1111-1111-1111-111111111111",
  "metricCode": "Example",
  "titleAr": "مراجعة تشغيلية",
  "titleEn": "Example",
  "explanationAr": "مراجعة تشغيلية",
  "explanationEn": "Example",
  "currentWindow": {
    "fromDate": "2026-10-01",
    "toDate": "2026-10-01"
  },
  "previousWindow": {
    "fromDate": "2026-10-01",
    "toDate": "2026-10-01"
  },
  "currentValue": 0,
  "previousValue": 0,
  "numerator": 0,
  "denominator": 0,
  "previousNumerator": 0,
  "previousDenominator": 0,
  "sampleCount": 0,
  "asOfUtc": "2026-10-14T09:00:00Z",
  "evidenceSummaryAr": "مراجعة تشغيلية",
  "evidenceSummaryEn": "Example",
  "dataStatus": "Complete",
  "suggestedActionAr": "مراجعة تشغيلية",
  "suggestedActionEn": "Example",
  "state": "New",
  "isPersisted": false,
  "rowVersion": "AAAAAAAAB9E=",
  "availableActions": [
    {}
  ]
}
```

## P18-049

**NEW · D.37–D.42 · Doctor · DoctorInsights.Acknowledge**

`POST /api/v1/doctors/me/insights/{insightId}/acknowledge`

Permission: Insights.Doctor.TrackOwn; Insights.Doctor.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: Acknowledge in the DoctorInsights journey. Refresh available actions after success; open returned detailRoute or refresh the entity, then fetch history and the affected dashboard/report.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| insightId | path | True | string / uuid |
| Idempotency-Key | header | True | string |
| Accept-Language | header | False | string; ['ar', 'en'] |
| request | Body | False | InsightTransitionRequest; see DTO tables and domain constraints |

Response: **200** `DoctorInsightResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Idempotent replay returns the recorded resource; no duplicate movement/history. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18DoctorReportingApiTests; Phase18OperationalAnalyticsTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
POST /api/v1/doctors/me/insights/11111111-1111-1111-1111-111111111111/acknowledge
Authorization: Bearer <access-token>
Idempotency-Key: 7ce6cdbd-example-operation
Content-Type: application/json

{
  "rowVersion": "AAAAAAAAB9E=",
  "reason": "Operational correction reviewed"
}
```

Example 200 response shape:

```json
{
  "insightId": "11111111-1111-1111-1111-111111111111",
  "practiceId": "11111111-1111-1111-1111-111111111111",
  "metricCode": "Example",
  "titleAr": "مراجعة تشغيلية",
  "titleEn": "Example",
  "explanationAr": "مراجعة تشغيلية",
  "explanationEn": "Example",
  "currentWindow": {
    "fromDate": "2026-10-01",
    "toDate": "2026-10-01"
  },
  "previousWindow": {
    "fromDate": "2026-10-01",
    "toDate": "2026-10-01"
  },
  "currentValue": 0,
  "previousValue": 0,
  "numerator": 0,
  "denominator": 0,
  "previousNumerator": 0,
  "previousDenominator": 0,
  "sampleCount": 0,
  "asOfUtc": "2026-10-14T09:00:00Z",
  "evidenceSummaryAr": "مراجعة تشغيلية",
  "evidenceSummaryEn": "Example",
  "dataStatus": "Complete",
  "suggestedActionAr": "مراجعة تشغيلية",
  "suggestedActionEn": "Example",
  "state": "New",
  "isPersisted": false,
  "rowVersion": "AAAAAAAAB9E=",
  "availableActions": [
    {}
  ]
}
```

## P18-050

**NEW · D.37–D.42 · Doctor · DoctorInsights.Dismiss**

`POST /api/v1/doctors/me/insights/{insightId}/dismiss`

Permission: Insights.Doctor.TrackOwn; Insights.Doctor.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: Dismiss in the DoctorInsights journey. Refresh available actions after success; open returned detailRoute or refresh the entity, then fetch history and the affected dashboard/report.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| insightId | path | True | string / uuid |
| Idempotency-Key | header | True | string |
| Accept-Language | header | False | string; ['ar', 'en'] |
| request | Body | False | InsightTransitionRequest; see DTO tables and domain constraints |

Response: **200** `DoctorInsightResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Idempotent replay returns the recorded resource; no duplicate movement/history. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18DoctorReportingApiTests; Phase18OperationalAnalyticsTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
POST /api/v1/doctors/me/insights/11111111-1111-1111-1111-111111111111/dismiss
Authorization: Bearer <access-token>
Idempotency-Key: 7ce6cdbd-example-operation
Content-Type: application/json

{
  "rowVersion": "AAAAAAAAB9E=",
  "reason": "Operational correction reviewed"
}
```

Example 200 response shape:

```json
{
  "insightId": "11111111-1111-1111-1111-111111111111",
  "practiceId": "11111111-1111-1111-1111-111111111111",
  "metricCode": "Example",
  "titleAr": "مراجعة تشغيلية",
  "titleEn": "Example",
  "explanationAr": "مراجعة تشغيلية",
  "explanationEn": "Example",
  "currentWindow": {
    "fromDate": "2026-10-01",
    "toDate": "2026-10-01"
  },
  "previousWindow": {
    "fromDate": "2026-10-01",
    "toDate": "2026-10-01"
  },
  "currentValue": 0,
  "previousValue": 0,
  "numerator": 0,
  "denominator": 0,
  "previousNumerator": 0,
  "previousDenominator": 0,
  "sampleCount": 0,
  "asOfUtc": "2026-10-14T09:00:00Z",
  "evidenceSummaryAr": "مراجعة تشغيلية",
  "evidenceSummaryEn": "Example",
  "dataStatus": "Complete",
  "suggestedActionAr": "مراجعة تشغيلية",
  "suggestedActionEn": "Example",
  "state": "New",
  "isPersisted": false,
  "rowVersion": "AAAAAAAAB9E=",
  "availableActions": [
    {}
  ]
}
```

## P18-051

**NEW · D.37–D.42 · Doctor · DoctorInsights.History**

`GET /api/v1/doctors/me/insights/{insightId}/history`

Permission: Insights.Doctor.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: History in the DoctorInsights journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| insightId | path | True | string / uuid |
| pageNumber | query | False | integer / int32 |
| pageSize | query | False | integer / int32 |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `DoctorInsightHistoryResponseReportPage`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18DoctorReportingApiTests; Phase18OperationalAnalyticsTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/insights/11111111-1111-1111-1111-111111111111/history
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "items": [
    {}
  ],
  "totalCount": 0,
  "pageNumber": 1,
  "pageSize": 20,
  "dataStatus": "Complete",
  "warnings": [
    "Example"
  ]
}
```

## P18-052

**NEW · D.37–D.42 · Doctor · DoctorInsights.Resolve**

`POST /api/v1/doctors/me/insights/{insightId}/resolve`

Permission: Insights.Doctor.TrackOwn; Insights.Doctor.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: Resolve in the DoctorInsights journey. Refresh available actions after success; open returned detailRoute or refresh the entity, then fetch history and the affected dashboard/report.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| insightId | path | True | string / uuid |
| Idempotency-Key | header | True | string |
| Accept-Language | header | False | string; ['ar', 'en'] |
| request | Body | False | InsightTransitionRequest; see DTO tables and domain constraints |

Response: **200** `DoctorInsightResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Idempotent replay returns the recorded resource; no duplicate movement/history. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18DoctorReportingApiTests; Phase18OperationalAnalyticsTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
POST /api/v1/doctors/me/insights/11111111-1111-1111-1111-111111111111/resolve
Authorization: Bearer <access-token>
Idempotency-Key: 7ce6cdbd-example-operation
Content-Type: application/json

{
  "rowVersion": "AAAAAAAAB9E=",
  "reason": "Operational correction reviewed"
}
```

Example 200 response shape:

```json
{
  "insightId": "11111111-1111-1111-1111-111111111111",
  "practiceId": "11111111-1111-1111-1111-111111111111",
  "metricCode": "Example",
  "titleAr": "مراجعة تشغيلية",
  "titleEn": "Example",
  "explanationAr": "مراجعة تشغيلية",
  "explanationEn": "Example",
  "currentWindow": {
    "fromDate": "2026-10-01",
    "toDate": "2026-10-01"
  },
  "previousWindow": {
    "fromDate": "2026-10-01",
    "toDate": "2026-10-01"
  },
  "currentValue": 0,
  "previousValue": 0,
  "numerator": 0,
  "denominator": 0,
  "previousNumerator": 0,
  "previousDenominator": 0,
  "sampleCount": 0,
  "asOfUtc": "2026-10-14T09:00:00Z",
  "evidenceSummaryAr": "مراجعة تشغيلية",
  "evidenceSummaryEn": "Example",
  "dataStatus": "Complete",
  "suggestedActionAr": "مراجعة تشغيلية",
  "suggestedActionEn": "Example",
  "state": "New",
  "isPersisted": false,
  "rowVersion": "AAAAAAAAB9E=",
  "availableActions": [
    {}
  ]
}
```

## P18-053

**NEW · R.18–R.22; Doctor oversight · Doctor · DoctorHandovers.Create**

`POST /api/v1/doctors/me/practices/{practiceId}/handovers`

Permission: Handovers.Doctor.ManageOwn; Handovers.Doctor.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: Create in the DoctorHandovers journey. Refresh available actions after success; open returned detailRoute or refresh the entity, then fetch history and the affected dashboard/report.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| practiceId | path | True | string / uuid |
| Idempotency-Key | header | True | string |
| Accept-Language | header | False | string; ['ar', 'en'] |
| request | Body | False | HandoverCreateRequest; see DTO tables and domain constraints |

Response: **200** `HandoverResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Idempotent replay returns the recorded resource; no duplicate movement/history. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18ReceptionApiTests; Phase18HandoverTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
POST /api/v1/doctors/me/practices/11111111-1111-1111-1111-111111111111/handovers
Authorization: Bearer <access-token>
Idempotency-Key: 7ce6cdbd-example-operation
Content-Type: application/json

{
  "summary": "مراجعة تشغيلية",
  "reservationId": "11111111-1111-1111-1111-111111111111",
  "ticketId": "11111111-1111-1111-1111-111111111111",
  "previousHandoverId": "11111111-1111-1111-1111-111111111111"
}
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "doctorPracticeId": "11111111-1111-1111-1111-111111111111",
  "summary": "مراجعة تشغيلية",
  "reservationId": "11111111-1111-1111-1111-111111111111",
  "ticketId": "11111111-1111-1111-1111-111111111111",
  "previousHandoverId": "11111111-1111-1111-1111-111111111111",
  "status": "Open",
  "createdByApplicationUserId": "11111111-1111-1111-1111-111111111111",
  "createdOnUtc": "2026-10-14T09:00:00Z",
  "acknowledgedByApplicationUserId": "11111111-1111-1111-1111-111111111111",
  "acknowledgedOnUtc": "2026-10-14T09:00:00Z",
  "closedByApplicationUserId": "11111111-1111-1111-1111-111111111111",
  "closedOnUtc": "2026-10-14T09:00:00Z",
  "closureOutcome": "Example",
  "rowVersion": "AAAAAAAAB9E=",
  "availableActions": [
    {}
  ]
}
```

## P18-054

**NEW · R.18–R.22; Doctor oversight · Doctor · DoctorHandovers.Detail**

`GET /api/v1/doctors/me/practices/{practiceId}/handovers/{handoverId}`

Permission: Handovers.Doctor.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: Detail in the DoctorHandovers journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| practiceId | path | True | string / uuid |
| handoverId | path | True | string / uuid |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `HandoverResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18ReceptionApiTests; Phase18HandoverTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/practices/11111111-1111-1111-1111-111111111111/handovers/11111111-1111-1111-1111-111111111111
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "doctorPracticeId": "11111111-1111-1111-1111-111111111111",
  "summary": "مراجعة تشغيلية",
  "reservationId": "11111111-1111-1111-1111-111111111111",
  "ticketId": "11111111-1111-1111-1111-111111111111",
  "previousHandoverId": "11111111-1111-1111-1111-111111111111",
  "status": "Open",
  "createdByApplicationUserId": "11111111-1111-1111-1111-111111111111",
  "createdOnUtc": "2026-10-14T09:00:00Z",
  "acknowledgedByApplicationUserId": "11111111-1111-1111-1111-111111111111",
  "acknowledgedOnUtc": "2026-10-14T09:00:00Z",
  "closedByApplicationUserId": "11111111-1111-1111-1111-111111111111",
  "closedOnUtc": "2026-10-14T09:00:00Z",
  "closureOutcome": "Example",
  "rowVersion": "AAAAAAAAB9E=",
  "availableActions": [
    {}
  ]
}
```

## P18-055

**NEW · R.18–R.22; Doctor oversight · Doctor · DoctorHandovers.Acknowledge**

`POST /api/v1/doctors/me/practices/{practiceId}/handovers/{handoverId}/acknowledge`

Permission: Handovers.Doctor.ManageOwn; Handovers.Doctor.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: Acknowledge in the DoctorHandovers journey. Refresh available actions after success; open returned detailRoute or refresh the entity, then fetch history and the affected dashboard/report.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| practiceId | path | True | string / uuid |
| handoverId | path | True | string / uuid |
| Idempotency-Key | header | True | string |
| Accept-Language | header | False | string; ['ar', 'en'] |
| request | Body | False | HandoverActionRequest; see DTO tables and domain constraints |

Response: **200** `HandoverResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Idempotent replay returns the recorded resource; no duplicate movement/history. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18ReceptionApiTests; Phase18HandoverTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
POST /api/v1/doctors/me/practices/11111111-1111-1111-1111-111111111111/handovers/11111111-1111-1111-1111-111111111111/acknowledge
Authorization: Bearer <access-token>
Idempotency-Key: 7ce6cdbd-example-operation
Content-Type: application/json

{
  "rowVersion": "AAAAAAAAB9E=",
  "outcome": "Operational correction reviewed",
  "annotation": "Example"
}
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "doctorPracticeId": "11111111-1111-1111-1111-111111111111",
  "summary": "مراجعة تشغيلية",
  "reservationId": "11111111-1111-1111-1111-111111111111",
  "ticketId": "11111111-1111-1111-1111-111111111111",
  "previousHandoverId": "11111111-1111-1111-1111-111111111111",
  "status": "Open",
  "createdByApplicationUserId": "11111111-1111-1111-1111-111111111111",
  "createdOnUtc": "2026-10-14T09:00:00Z",
  "acknowledgedByApplicationUserId": "11111111-1111-1111-1111-111111111111",
  "acknowledgedOnUtc": "2026-10-14T09:00:00Z",
  "closedByApplicationUserId": "11111111-1111-1111-1111-111111111111",
  "closedOnUtc": "2026-10-14T09:00:00Z",
  "closureOutcome": "Example",
  "rowVersion": "AAAAAAAAB9E=",
  "availableActions": [
    {}
  ]
}
```

## P18-056

**NEW · R.18–R.22; Doctor oversight · Doctor · DoctorHandovers.Close**

`POST /api/v1/doctors/me/practices/{practiceId}/handovers/{handoverId}/close`

Permission: Handovers.Doctor.ManageOwn; Handovers.Doctor.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: Close in the DoctorHandovers journey. Refresh available actions after success; open returned detailRoute or refresh the entity, then fetch history and the affected dashboard/report.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| practiceId | path | True | string / uuid |
| handoverId | path | True | string / uuid |
| Idempotency-Key | header | True | string |
| Accept-Language | header | False | string; ['ar', 'en'] |
| request | Body | False | HandoverActionRequest; see DTO tables and domain constraints |

Response: **200** `HandoverResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Idempotent replay returns the recorded resource; no duplicate movement/history. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18ReceptionApiTests; Phase18HandoverTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
POST /api/v1/doctors/me/practices/11111111-1111-1111-1111-111111111111/handovers/11111111-1111-1111-1111-111111111111/close
Authorization: Bearer <access-token>
Idempotency-Key: 7ce6cdbd-example-operation
Content-Type: application/json

{
  "rowVersion": "AAAAAAAAB9E=",
  "outcome": "Operational correction reviewed",
  "annotation": "Example"
}
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "doctorPracticeId": "11111111-1111-1111-1111-111111111111",
  "summary": "مراجعة تشغيلية",
  "reservationId": "11111111-1111-1111-1111-111111111111",
  "ticketId": "11111111-1111-1111-1111-111111111111",
  "previousHandoverId": "11111111-1111-1111-1111-111111111111",
  "status": "Open",
  "createdByApplicationUserId": "11111111-1111-1111-1111-111111111111",
  "createdOnUtc": "2026-10-14T09:00:00Z",
  "acknowledgedByApplicationUserId": "11111111-1111-1111-1111-111111111111",
  "acknowledgedOnUtc": "2026-10-14T09:00:00Z",
  "closedByApplicationUserId": "11111111-1111-1111-1111-111111111111",
  "closedOnUtc": "2026-10-14T09:00:00Z",
  "closureOutcome": "Example",
  "rowVersion": "AAAAAAAAB9E=",
  "availableActions": [
    {}
  ]
}
```

## P18-057

**NEW · R.18–R.22; Doctor oversight · Doctor · DoctorHandovers.History**

`GET /api/v1/doctors/me/practices/{practiceId}/handovers/{handoverId}/history`

Permission: Handovers.Doctor.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: History in the DoctorHandovers journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| practiceId | path | True | string / uuid |
| handoverId | path | True | string / uuid |
| pageNumber | query | False | integer / int32 |
| pageSize | query | False | integer / int32 |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `HandoverHistoryResponseReportPage`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18ReceptionApiTests; Phase18HandoverTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/practices/11111111-1111-1111-1111-111111111111/handovers/11111111-1111-1111-1111-111111111111/history
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "items": [
    {}
  ],
  "totalCount": 0,
  "pageNumber": 1,
  "pageSize": 20,
  "dataStatus": "Complete",
  "warnings": [
    "Example"
  ]
}
```

## P18-058

**NEW · D.43–D.48 · Doctor · OperatingFinance.GetReceptionSalaryPlans**

`GET /api/v1/doctors/me/reception-salary-plans`

Permission: SalaryPlans.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: GetReceptionSalaryPlans in the financial management journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| FromDate | query | False | string / date |
| ToDate | query | False | string / date |
| PracticeId | query | False | string / uuid |
| ReceptionId | query | False | string / uuid |
| PlanId | query | False | string / uuid |
| PlanStatus | query | False | OperatingPlanStatus |
| ObligationStatus | query | False | OperatingObligationStatus |
| Category | query | False | ExpenseCategoryCode |
| PageNumber | query | False | integer / int32 |
| PageSize | query | False | integer / int32 |
| SortBy | query | False | string |
| SortDirection | query | False | string |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `OperatingPlanResponseReportPage`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/reception-salary-plans?fromDate=2026-10-01&toDate=2026-10-14
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "items": [
    {}
  ],
  "totalCount": 0,
  "pageNumber": 1,
  "pageSize": 20,
  "dataStatus": "Complete",
  "warnings": [
    "Example"
  ]
}
```

## P18-059

**NEW · D.43–D.48 · Doctor · OperatingFinance.PostReceptionSalaryPlans**

`POST /api/v1/doctors/me/reception-salary-plans`

Permission: SalaryPlans.ManageOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: PostReceptionSalaryPlans in the financial management journey. Refresh available actions after success; open returned detailRoute or refresh the entity, then fetch history and the affected dashboard/report.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| Idempotency-Key | header | True | string |
| Accept-Language | header | False | string; ['ar', 'en'] |
| request | Body | False | OperatingPlanRequest; see DTO tables and domain constraints |

Response: **200** `OperatingFinanceWriteResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Idempotent replay returns the recorded resource; no duplicate movement/history. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
POST /api/v1/doctors/me/reception-salary-plans
Authorization: Bearer <access-token>
Idempotency-Key: 7ce6cdbd-example-operation
Content-Type: application/json

{
  "amount": 650,
  "dueDay": 31,
  "effectiveFrom": "2026-10-01",
  "effectiveTo": "2026-10-01",
  "allocations": [
    {}
  ],
  "descriptionAr": "مراجعة تشغيلية",
  "descriptionEn": "Example",
  "receptionId": "11111111-1111-1111-1111-111111111111",
  "category": "Rent",
  "cadence": "Monthly",
  "amountPolicy": "Fixed",
  "rowVersion": "AAAAAAAAB9E=",
  "reason": "Operational correction reviewed"
}
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "resourceType": "Example",
  "detailRoute": "Example",
  "rowVersion": "AAAAAAAAB9E=",
  "obligationId": "11111111-1111-1111-1111-111111111111",
  "settlementId": "11111111-1111-1111-1111-111111111111"
}
```

## P18-060

**NEW · D.43–D.48 · Doctor · OperatingFinance.GetReceptionSalaryPlansById**

`GET /api/v1/doctors/me/reception-salary-plans/{id}`

Permission: SalaryPlans.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: GetReceptionSalaryPlansById in the financial management journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| id | path | True | string / uuid |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `OperatingPlanResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/reception-salary-plans/11111111-1111-1111-1111-111111111111
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "doctorId": "11111111-1111-1111-1111-111111111111",
  "receptionId": "11111111-1111-1111-1111-111111111111",
  "kind": "Salary",
  "status": "Active",
  "firstCoveredMonth": "2026-10-01",
  "noProration": false,
  "effectiveFrom": "2026-10-01",
  "effectiveTo": "2026-10-01",
  "revisions": [
    {}
  ],
  "rowVersion": "AAAAAAAAB9E=",
  "availableActions": [
    {}
  ]
}
```

## P18-061

**NEW · D.43–D.48 · Doctor · OperatingFinance.PutReceptionSalaryPlansById**

`PUT /api/v1/doctors/me/reception-salary-plans/{id}`

Permission: SalaryPlans.ManageOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: PutReceptionSalaryPlansById in the financial management journey. Refresh available actions after success; open returned detailRoute or refresh the entity, then fetch history and the affected dashboard/report.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| id | path | True | string / uuid |
| Idempotency-Key | header | True | string |
| Accept-Language | header | False | string; ['ar', 'en'] |
| request | Body | False | OperatingPlanRequest; see DTO tables and domain constraints |

Response: **200** `OperatingFinanceWriteResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Idempotent replay returns the recorded resource; no duplicate movement/history. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
PUT /api/v1/doctors/me/reception-salary-plans/11111111-1111-1111-1111-111111111111
Authorization: Bearer <access-token>
Idempotency-Key: 7ce6cdbd-example-operation
Content-Type: application/json

{
  "amount": 650,
  "dueDay": 31,
  "effectiveFrom": "2026-10-01",
  "effectiveTo": "2026-10-01",
  "allocations": [
    {}
  ],
  "descriptionAr": "مراجعة تشغيلية",
  "descriptionEn": "Example",
  "receptionId": "11111111-1111-1111-1111-111111111111",
  "category": "Rent",
  "cadence": "Monthly",
  "amountPolicy": "Fixed",
  "rowVersion": "AAAAAAAAB9E=",
  "reason": "Operational correction reviewed"
}
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "resourceType": "Example",
  "detailRoute": "Example",
  "rowVersion": "AAAAAAAAB9E=",
  "obligationId": "11111111-1111-1111-1111-111111111111",
  "settlementId": "11111111-1111-1111-1111-111111111111"
}
```

## P18-062

**NEW · D.43–D.48 · Doctor · OperatingFinance.PostReceptionSalaryPlansByIdEnd**

`POST /api/v1/doctors/me/reception-salary-plans/{id}/end`

Permission: SalaryPlans.ManageOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: PostReceptionSalaryPlansByIdEnd in the financial management journey. Refresh available actions after success; open returned detailRoute or refresh the entity, then fetch history and the affected dashboard/report.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| id | path | True | string / uuid |
| Idempotency-Key | header | True | string |
| Accept-Language | header | False | string; ['ar', 'en'] |
| request | Body | False | OperatingTransitionRequest; see DTO tables and domain constraints |

Response: **200** `OperatingFinanceWriteResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Idempotent replay returns the recorded resource; no duplicate movement/history. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
POST /api/v1/doctors/me/reception-salary-plans/11111111-1111-1111-1111-111111111111/end
Authorization: Bearer <access-token>
Idempotency-Key: 7ce6cdbd-example-operation
Content-Type: application/json

{
  "rowVersion": "AAAAAAAAB9E=",
  "reason": "Operational correction reviewed"
}
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "resourceType": "Example",
  "detailRoute": "Example",
  "rowVersion": "AAAAAAAAB9E=",
  "obligationId": "11111111-1111-1111-1111-111111111111",
  "settlementId": "11111111-1111-1111-1111-111111111111"
}
```

## P18-063

**NEW · D.43–D.48 · Doctor · OperatingFinance.GetReceptionSalaryPlansByIdHistory**

`GET /api/v1/doctors/me/reception-salary-plans/{id}/history`

Permission: SalaryPlans.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: GetReceptionSalaryPlansByIdHistory in the financial management journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| id | path | True | string / uuid |
| pageNumber | query | False | integer / int32 |
| pageSize | query | False | integer / int32 |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `OperatingHistoryResponseReportPage`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/reception-salary-plans/11111111-1111-1111-1111-111111111111/history
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "items": [
    {}
  ],
  "totalCount": 0,
  "pageNumber": 1,
  "pageSize": 20,
  "dataStatus": "Complete",
  "warnings": [
    "Example"
  ]
}
```

## P18-064

**NEW · D.43–D.48 · Doctor · OperatingFinance.GetRecurringExpensePlans**

`GET /api/v1/doctors/me/recurring-expense-plans`

Permission: FinancialExpenses.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: GetRecurringExpensePlans in the financial management journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| FromDate | query | False | string / date |
| ToDate | query | False | string / date |
| PracticeId | query | False | string / uuid |
| ReceptionId | query | False | string / uuid |
| PlanId | query | False | string / uuid |
| PlanStatus | query | False | OperatingPlanStatus |
| ObligationStatus | query | False | OperatingObligationStatus |
| Category | query | False | ExpenseCategoryCode |
| PageNumber | query | False | integer / int32 |
| PageSize | query | False | integer / int32 |
| SortBy | query | False | string |
| SortDirection | query | False | string |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `OperatingPlanResponseReportPage`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/recurring-expense-plans?fromDate=2026-10-01&toDate=2026-10-14
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "items": [
    {}
  ],
  "totalCount": 0,
  "pageNumber": 1,
  "pageSize": 20,
  "dataStatus": "Complete",
  "warnings": [
    "Example"
  ]
}
```

## P18-065

**NEW · D.43–D.48 · Doctor · OperatingFinance.PostRecurringExpensePlans**

`POST /api/v1/doctors/me/recurring-expense-plans`

Permission: FinancialExpenses.ManageOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: PostRecurringExpensePlans in the financial management journey. Refresh available actions after success; open returned detailRoute or refresh the entity, then fetch history and the affected dashboard/report.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| Idempotency-Key | header | True | string |
| Accept-Language | header | False | string; ['ar', 'en'] |
| request | Body | False | OperatingPlanRequest; see DTO tables and domain constraints |

Response: **200** `OperatingFinanceWriteResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Idempotent replay returns the recorded resource; no duplicate movement/history. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
POST /api/v1/doctors/me/recurring-expense-plans
Authorization: Bearer <access-token>
Idempotency-Key: 7ce6cdbd-example-operation
Content-Type: application/json

{
  "amount": 650,
  "dueDay": 31,
  "effectiveFrom": "2026-10-01",
  "effectiveTo": "2026-10-01",
  "allocations": [
    {}
  ],
  "descriptionAr": "مراجعة تشغيلية",
  "descriptionEn": "Example",
  "receptionId": "11111111-1111-1111-1111-111111111111",
  "category": "Rent",
  "cadence": "Monthly",
  "amountPolicy": "Fixed",
  "rowVersion": "AAAAAAAAB9E=",
  "reason": "Operational correction reviewed"
}
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "resourceType": "Example",
  "detailRoute": "Example",
  "rowVersion": "AAAAAAAAB9E=",
  "obligationId": "11111111-1111-1111-1111-111111111111",
  "settlementId": "11111111-1111-1111-1111-111111111111"
}
```

## P18-066

**NEW · D.43–D.48 · Doctor · OperatingFinance.GetRecurringExpensePlansById**

`GET /api/v1/doctors/me/recurring-expense-plans/{id}`

Permission: FinancialExpenses.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: GetRecurringExpensePlansById in the financial management journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| id | path | True | string / uuid |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `OperatingPlanResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/recurring-expense-plans/11111111-1111-1111-1111-111111111111
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "doctorId": "11111111-1111-1111-1111-111111111111",
  "receptionId": "11111111-1111-1111-1111-111111111111",
  "kind": "Salary",
  "status": "Active",
  "firstCoveredMonth": "2026-10-01",
  "noProration": false,
  "effectiveFrom": "2026-10-01",
  "effectiveTo": "2026-10-01",
  "revisions": [
    {}
  ],
  "rowVersion": "AAAAAAAAB9E=",
  "availableActions": [
    {}
  ]
}
```

## P18-067

**NEW · D.43–D.48 · Doctor · OperatingFinance.PutRecurringExpensePlansById**

`PUT /api/v1/doctors/me/recurring-expense-plans/{id}`

Permission: FinancialExpenses.ManageOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: PutRecurringExpensePlansById in the financial management journey. Refresh available actions after success; open returned detailRoute or refresh the entity, then fetch history and the affected dashboard/report.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| id | path | True | string / uuid |
| Idempotency-Key | header | True | string |
| Accept-Language | header | False | string; ['ar', 'en'] |
| request | Body | False | OperatingPlanRequest; see DTO tables and domain constraints |

Response: **200** `OperatingFinanceWriteResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Idempotent replay returns the recorded resource; no duplicate movement/history. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
PUT /api/v1/doctors/me/recurring-expense-plans/11111111-1111-1111-1111-111111111111
Authorization: Bearer <access-token>
Idempotency-Key: 7ce6cdbd-example-operation
Content-Type: application/json

{
  "amount": 650,
  "dueDay": 31,
  "effectiveFrom": "2026-10-01",
  "effectiveTo": "2026-10-01",
  "allocations": [
    {}
  ],
  "descriptionAr": "مراجعة تشغيلية",
  "descriptionEn": "Example",
  "receptionId": "11111111-1111-1111-1111-111111111111",
  "category": "Rent",
  "cadence": "Monthly",
  "amountPolicy": "Fixed",
  "rowVersion": "AAAAAAAAB9E=",
  "reason": "Operational correction reviewed"
}
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "resourceType": "Example",
  "detailRoute": "Example",
  "rowVersion": "AAAAAAAAB9E=",
  "obligationId": "11111111-1111-1111-1111-111111111111",
  "settlementId": "11111111-1111-1111-1111-111111111111"
}
```

## P18-068

**NEW · D.43–D.48 · Doctor · OperatingFinance.PostRecurringExpensePlansByIdEnd**

`POST /api/v1/doctors/me/recurring-expense-plans/{id}/end`

Permission: FinancialExpenses.ManageOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: PostRecurringExpensePlansByIdEnd in the financial management journey. Refresh available actions after success; open returned detailRoute or refresh the entity, then fetch history and the affected dashboard/report.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| id | path | True | string / uuid |
| Idempotency-Key | header | True | string |
| Accept-Language | header | False | string; ['ar', 'en'] |
| request | Body | False | OperatingTransitionRequest; see DTO tables and domain constraints |

Response: **200** `OperatingFinanceWriteResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Idempotent replay returns the recorded resource; no duplicate movement/history. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
POST /api/v1/doctors/me/recurring-expense-plans/11111111-1111-1111-1111-111111111111/end
Authorization: Bearer <access-token>
Idempotency-Key: 7ce6cdbd-example-operation
Content-Type: application/json

{
  "rowVersion": "AAAAAAAAB9E=",
  "reason": "Operational correction reviewed"
}
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "resourceType": "Example",
  "detailRoute": "Example",
  "rowVersion": "AAAAAAAAB9E=",
  "obligationId": "11111111-1111-1111-1111-111111111111",
  "settlementId": "11111111-1111-1111-1111-111111111111"
}
```

## P18-069

**NEW · D.43–D.48 · Doctor · OperatingFinance.GetRecurringExpensePlansByIdHistory**

`GET /api/v1/doctors/me/recurring-expense-plans/{id}/history`

Permission: FinancialExpenses.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: GetRecurringExpensePlansByIdHistory in the financial management journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| id | path | True | string / uuid |
| pageNumber | query | False | integer / int32 |
| pageSize | query | False | integer / int32 |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `OperatingHistoryResponseReportPage`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/recurring-expense-plans/11111111-1111-1111-1111-111111111111/history
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "items": [
    {}
  ],
  "totalCount": 0,
  "pageNumber": 1,
  "pageSize": 20,
  "dataStatus": "Complete",
  "warnings": [
    "Example"
  ]
}
```

## P18-070

**NEW · D.43–D.48 · Doctor · OperatingFinance.GetRecurringExpensePlansByIdObligations**

`GET /api/v1/doctors/me/recurring-expense-plans/{id}/obligations`

Permission: FinancialExpenses.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: GetRecurringExpensePlansByIdObligations in the financial management journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| id | path | True | string / uuid |
| FromDate | query | False | string / date |
| ToDate | query | False | string / date |
| PracticeId | query | False | string / uuid |
| ReceptionId | query | False | string / uuid |
| PlanId | query | False | string / uuid |
| PlanStatus | query | False | OperatingPlanStatus |
| ObligationStatus | query | False | OperatingObligationStatus |
| Category | query | False | ExpenseCategoryCode |
| PageNumber | query | False | integer / int32 |
| PageSize | query | False | integer / int32 |
| SortBy | query | False | string |
| SortDirection | query | False | string |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `OperatingObligationResponseReportPage`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/recurring-expense-plans/11111111-1111-1111-1111-111111111111/obligations?fromDate=2026-10-01&toDate=2026-10-14
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "items": [
    {}
  ],
  "totalCount": 0,
  "pageNumber": 1,
  "pageSize": 20,
  "dataStatus": "Complete",
  "warnings": [
    "Example"
  ]
}
```

## P18-071

**NEW · D.43–D.48 · Doctor · OperatingFinance.PostRecurringExpensePlansByIdPause**

`POST /api/v1/doctors/me/recurring-expense-plans/{id}/pause`

Permission: FinancialExpenses.ManageOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: PostRecurringExpensePlansByIdPause in the financial management journey. Refresh available actions after success; open returned detailRoute or refresh the entity, then fetch history and the affected dashboard/report.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| id | path | True | string / uuid |
| Idempotency-Key | header | True | string |
| Accept-Language | header | False | string; ['ar', 'en'] |
| request | Body | False | OperatingTransitionRequest; see DTO tables and domain constraints |

Response: **200** `OperatingFinanceWriteResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Idempotent replay returns the recorded resource; no duplicate movement/history. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
POST /api/v1/doctors/me/recurring-expense-plans/11111111-1111-1111-1111-111111111111/pause
Authorization: Bearer <access-token>
Idempotency-Key: 7ce6cdbd-example-operation
Content-Type: application/json

{
  "rowVersion": "AAAAAAAAB9E=",
  "reason": "Operational correction reviewed"
}
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "resourceType": "Example",
  "detailRoute": "Example",
  "rowVersion": "AAAAAAAAB9E=",
  "obligationId": "11111111-1111-1111-1111-111111111111",
  "settlementId": "11111111-1111-1111-1111-111111111111"
}
```

## P18-072

**NEW · D.43–D.48 · Doctor · OperatingFinance.PostRecurringExpensePlansByIdResume**

`POST /api/v1/doctors/me/recurring-expense-plans/{id}/resume`

Permission: FinancialExpenses.ManageOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: PostRecurringExpensePlansByIdResume in the financial management journey. Refresh available actions after success; open returned detailRoute or refresh the entity, then fetch history and the affected dashboard/report.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| id | path | True | string / uuid |
| Idempotency-Key | header | True | string |
| Accept-Language | header | False | string; ['ar', 'en'] |
| request | Body | False | OperatingTransitionRequest; see DTO tables and domain constraints |

Response: **200** `OperatingFinanceWriteResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Idempotent replay returns the recorded resource; no duplicate movement/history. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
POST /api/v1/doctors/me/recurring-expense-plans/11111111-1111-1111-1111-111111111111/resume
Authorization: Bearer <access-token>
Idempotency-Key: 7ce6cdbd-example-operation
Content-Type: application/json

{
  "rowVersion": "AAAAAAAAB9E=",
  "reason": "Operational correction reviewed"
}
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "resourceType": "Example",
  "detailRoute": "Example",
  "rowVersion": "AAAAAAAAB9E=",
  "obligationId": "11111111-1111-1111-1111-111111111111",
  "settlementId": "11111111-1111-1111-1111-111111111111"
}
```

## P18-073

**NEW · D.01–D.36; report catalog · Doctor · DoctorReporting.Report**

`GET /api/v1/doctors/me/reports/{reportCode}`

Permission: Reports.Doctor.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: Report in the DoctorReporting journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| reportCode | path | True | string |
| FromDate | query | False | string / date |
| ToDate | query | False | string / date |
| PracticeId | query | False | string / uuid |
| DoctorId | query | False | string / uuid |
| SegmentId | query | False | string / uuid |
| VisitTypeId | query | False | string / uuid |
| BookingSource | query | False | ReservationBookingSource |
| ReservationStatus | query | False | ReservationStatus |
| TicketStatus | query | False | TicketStatus |
| AsOfUtc | query | False | string / date-time |
| ReportTimeZoneId | query | False | string |
| Grouping | query | False | ReportGrouping |
| ComparePreviousPeriod | query | False | boolean |
| PageNumber | query | False | integer / int32 |
| PageSize | query | False | integer / int32 |
| SortBy | query | False | string |
| SortDirection | query | False | string |
| ExpenseCategory | query | False | ExpenseCategoryCode |
| ReceptionId | query | False | string / uuid |
| PlanId | query | False | string / uuid |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `ReportModel`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18DoctorReportingApiTests; Phase18OperationalAnalyticsTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/reports/overview?fromDate=2026-10-01&toDate=2026-10-14
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "reportCode": "Example",
  "titleAr": "مراجعة تشغيلية",
  "titleEn": "Example",
  "description": "Example",
  "appliedFilters": {
    "fromDate": null,
    "toDate": null,
    "practiceId": null,
    "doctorId": null,
    "segmentId": null,
    "visitTypeId": null,
    "bookingSource": "Patient",
    "reservationStatus": "Active",
    "ticketStatus": "Waiting",
    "asOfUtc": null,
    "reportTimeZoneId": null,
    "grouping": "Day",
    "comparePreviousPeriod": false,
    "pageNumber": 1,
    "pageSize": 20,
    "sortBy": null,
    "sortDirection": null,
    "expenseCategory": "Rent",
    "receptionId": null,
    "planId": null
  },
  "scope": {
    "actor": "Doctor",
    "actorId": "11111111-1111-1111-1111-111111111111",
    "doctorId": null,
    "patientId": null,
    "practiceIds": null,
    "permissions": null
  },
  "window": {
    "fromDate": "2026-10-01",
    "toDate": "2026-10-01",
    "asOfUtc": "2026-10-14T09:00:00Z",
    "generatedAtUtc": "2026-10-14T09:00:00Z",
    "reportTimeZoneId": null
  },
  "dataFreshnessUtc": "2026-10-14T09:00:00Z",
  "dataStatus": "Complete",
  "metrics": [
    {}
  ],
  "series": [
    {}
  ],
  "breakdowns": [
    {}
  ],
  "availableActions": [
    {}
  ],
  "warnings": [
    "Example"
  ],
  "eligibleSampleCount": 0,
  "excludedSampleCount": 0,
  "records": [
    {}
  ],
  "detailRowCount": 0,
  "previousPeriodComparisons": [
    {}
  ]
}
```

## P18-074

**NEW · D.01–D.36; report catalog · Doctor · DoctorReporting.Drilldown**

`GET /api/v1/doctors/me/reports/{reportCode}/drilldown`

Permission: Reports.Doctor.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: Drilldown in the DoctorReporting journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| reportCode | path | True | string |
| metricCode | query | False | string |
| FromDate | query | False | string / date |
| ToDate | query | False | string / date |
| PracticeId | query | False | string / uuid |
| DoctorId | query | False | string / uuid |
| SegmentId | query | False | string / uuid |
| VisitTypeId | query | False | string / uuid |
| BookingSource | query | False | ReservationBookingSource |
| ReservationStatus | query | False | ReservationStatus |
| TicketStatus | query | False | TicketStatus |
| AsOfUtc | query | False | string / date-time |
| ReportTimeZoneId | query | False | string |
| Grouping | query | False | ReportGrouping |
| ComparePreviousPeriod | query | False | boolean |
| PageNumber | query | False | integer / int32 |
| PageSize | query | False | integer / int32 |
| SortBy | query | False | string |
| SortDirection | query | False | string |
| ExpenseCategory | query | False | ExpenseCategoryCode |
| ReceptionId | query | False | string / uuid |
| PlanId | query | False | string / uuid |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `ReportRecordReportPage`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18DoctorReportingApiTests; Phase18OperationalAnalyticsTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/reports/overview/drilldown?fromDate=2026-10-01&toDate=2026-10-14&metricCode=CheckIns
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "items": [
    {}
  ],
  "totalCount": 0,
  "pageNumber": 1,
  "pageSize": 20,
  "dataStatus": "Complete",
  "warnings": [
    "Example"
  ]
}
```

## P18-075

**NEW · D.01–D.36; report catalog · Doctor · DoctorReportExports.Export**

`GET /api/v1/doctors/me/reports/{reportCode}/export`

Permission: Reports.Doctor.ExportOperational OR Reports.Doctor.ExportFinancial (by code); corresponding preview/source read permissions. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: Export in the DoctorReportExports journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| reportCode | path | True | string |
| FromDate | query | False | string / date |
| ToDate | query | False | string / date |
| PracticeId | query | False | string / uuid |
| DoctorId | query | False | string / uuid |
| SegmentId | query | False | string / uuid |
| VisitTypeId | query | False | string / uuid |
| BookingSource | query | False | ReservationBookingSource |
| ReservationStatus | query | False | ReservationStatus |
| TicketStatus | query | False | TicketStatus |
| AsOfUtc | query | False | string / date-time |
| ReportTimeZoneId | query | False | string |
| Grouping | query | False | ReportGrouping |
| ComparePreviousPeriod | query | False | boolean |
| PageNumber | query | False | integer / int32 |
| PageSize | query | False | integer / int32 |
| SortBy | query | False | string |
| SortDirection | query | False | string |
| ExpenseCategory | query | False | ExpenseCategoryCode |
| ReceptionId | query | False | string / uuid |
| PlanId | query | False | string / uuid |
| format | query | False | string |
| language | query | False | string |
| landscape | query | False | boolean |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `string / binary`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18ExportApiTests; Phase18ReportExportTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/reports/overview/export?fromDate=2026-10-01&toDate=2026-10-14&format=xlsx&language=ar
Authorization: Bearer <access-token>
```

Example 200 response shape:

```text
<binary PDF or XLSX bytes; Content-Disposition supplies the filename>
```

## P18-076

**NEW · D.01–D.36; report catalog · Doctor · DoctorReporting.Catalog**

`GET /api/v1/doctors/me/reports/catalog`

Permission: Reports.Doctor.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: Catalog in the DoctorReporting journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `ReportCatalogEntry[]`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18DoctorReportingApiTests; Phase18OperationalAnalyticsTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/reports/catalog
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
[
  {
    "reportCode": null,
    "titleAr": null,
    "titleEn": null,
    "description": null,
    "allowedFilters": null,
    "availableGroupings": null,
    "scope": null,
    "canPreview": false,
    "canDrillDown": false,
    "canExportPdf": false,
    "canExportExcel": false,
    "exportLimits": {},
    "requiredPermissions": null,
    "dataStatus": "Complete",
    "exportRequiredPermissions": null,
    "minimumCalendarDays": 0,
    "maximumCalendarDays": 0,
    "dataAvailability": null,
    "previewRoute": null,
    "drilldownRoute": null,
    "exportRoute": null
  }
]
```

## P18-077

**NEW · D.30–D.33 · Doctor · DoctorReporting.Patients**

`GET /api/v1/doctors/me/reports/patients`

Permission: Reports.Doctor.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: Patients in the DoctorReporting journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| FromDate | query | False | string / date |
| ToDate | query | False | string / date |
| PracticeId | query | False | string / uuid |
| DoctorId | query | False | string / uuid |
| SegmentId | query | False | string / uuid |
| VisitTypeId | query | False | string / uuid |
| BookingSource | query | False | ReservationBookingSource |
| ReservationStatus | query | False | ReservationStatus |
| TicketStatus | query | False | TicketStatus |
| AsOfUtc | query | False | string / date-time |
| ReportTimeZoneId | query | False | string |
| Grouping | query | False | ReportGrouping |
| ComparePreviousPeriod | query | False | boolean |
| PageNumber | query | False | integer / int32 |
| PageSize | query | False | integer / int32 |
| SortBy | query | False | string |
| SortDirection | query | False | string |
| ExpenseCategory | query | False | ExpenseCategoryCode |
| ReceptionId | query | False | string / uuid |
| PlanId | query | False | string / uuid |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `ReportModel`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18DoctorReportingApiTests; Phase18OperationalAnalyticsTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/reports/patients?fromDate=2026-10-01&toDate=2026-10-14
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "reportCode": "Example",
  "titleAr": "مراجعة تشغيلية",
  "titleEn": "Example",
  "description": "Example",
  "appliedFilters": {
    "fromDate": null,
    "toDate": null,
    "practiceId": null,
    "doctorId": null,
    "segmentId": null,
    "visitTypeId": null,
    "bookingSource": "Patient",
    "reservationStatus": "Active",
    "ticketStatus": "Waiting",
    "asOfUtc": null,
    "reportTimeZoneId": null,
    "grouping": "Day",
    "comparePreviousPeriod": false,
    "pageNumber": 1,
    "pageSize": 20,
    "sortBy": null,
    "sortDirection": null,
    "expenseCategory": "Rent",
    "receptionId": null,
    "planId": null
  },
  "scope": {
    "actor": "Doctor",
    "actorId": "11111111-1111-1111-1111-111111111111",
    "doctorId": null,
    "patientId": null,
    "practiceIds": null,
    "permissions": null
  },
  "window": {
    "fromDate": "2026-10-01",
    "toDate": "2026-10-01",
    "asOfUtc": "2026-10-14T09:00:00Z",
    "generatedAtUtc": "2026-10-14T09:00:00Z",
    "reportTimeZoneId": null
  },
  "dataFreshnessUtc": "2026-10-14T09:00:00Z",
  "dataStatus": "Complete",
  "metrics": [
    {}
  ],
  "series": [
    {}
  ],
  "breakdowns": [
    {}
  ],
  "availableActions": [
    {}
  ],
  "warnings": [
    "Example"
  ],
  "eligibleSampleCount": 0,
  "excludedSampleCount": 0,
  "records": [
    {}
  ],
  "detailRowCount": 0,
  "previousPeriodComparisons": [
    {}
  ]
}
```

## P18-078

**NEW · D.34–D.36 · Doctor · DoctorReporting.PracticeComparison**

`GET /api/v1/doctors/me/reports/practice-comparison`

Permission: Reports.Doctor.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: PracticeComparison in the DoctorReporting journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| FromDate | query | False | string / date |
| ToDate | query | False | string / date |
| PracticeId | query | False | string / uuid |
| DoctorId | query | False | string / uuid |
| SegmentId | query | False | string / uuid |
| VisitTypeId | query | False | string / uuid |
| BookingSource | query | False | ReservationBookingSource |
| ReservationStatus | query | False | ReservationStatus |
| TicketStatus | query | False | TicketStatus |
| AsOfUtc | query | False | string / date-time |
| ReportTimeZoneId | query | False | string |
| Grouping | query | False | ReportGrouping |
| ComparePreviousPeriod | query | False | boolean |
| PageNumber | query | False | integer / int32 |
| PageSize | query | False | integer / int32 |
| SortBy | query | False | string |
| SortDirection | query | False | string |
| ExpenseCategory | query | False | ExpenseCategoryCode |
| ReceptionId | query | False | string / uuid |
| PlanId | query | False | string / uuid |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `ReportModel`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18DoctorReportingApiTests; Phase18OperationalAnalyticsTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/reports/practice-comparison?fromDate=2026-10-01&toDate=2026-10-14
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "reportCode": "Example",
  "titleAr": "مراجعة تشغيلية",
  "titleEn": "Example",
  "description": "Example",
  "appliedFilters": {
    "fromDate": null,
    "toDate": null,
    "practiceId": null,
    "doctorId": null,
    "segmentId": null,
    "visitTypeId": null,
    "bookingSource": "Patient",
    "reservationStatus": "Active",
    "ticketStatus": "Waiting",
    "asOfUtc": null,
    "reportTimeZoneId": null,
    "grouping": "Day",
    "comparePreviousPeriod": false,
    "pageNumber": 1,
    "pageSize": 20,
    "sortBy": null,
    "sortDirection": null,
    "expenseCategory": "Rent",
    "receptionId": null,
    "planId": null
  },
  "scope": {
    "actor": "Doctor",
    "actorId": "11111111-1111-1111-1111-111111111111",
    "doctorId": null,
    "patientId": null,
    "practiceIds": null,
    "permissions": null
  },
  "window": {
    "fromDate": "2026-10-01",
    "toDate": "2026-10-01",
    "asOfUtc": "2026-10-14T09:00:00Z",
    "generatedAtUtc": "2026-10-14T09:00:00Z",
    "reportTimeZoneId": null
  },
  "dataFreshnessUtc": "2026-10-14T09:00:00Z",
  "dataStatus": "Complete",
  "metrics": [
    {}
  ],
  "series": [
    {}
  ],
  "breakdowns": [
    {}
  ],
  "availableActions": [
    {}
  ],
  "warnings": [
    "Example"
  ],
  "eligibleSampleCount": 0,
  "excludedSampleCount": 0,
  "records": [
    {}
  ],
  "detailRowCount": 0,
  "previousPeriodComparisons": [
    {}
  ]
}
```

## P18-079

**NEW · D.15–D.24 · Doctor · DoctorReporting.Queue**

`GET /api/v1/doctors/me/reports/queue`

Permission: Reports.Doctor.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: Queue in the DoctorReporting journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| FromDate | query | False | string / date |
| ToDate | query | False | string / date |
| PracticeId | query | False | string / uuid |
| DoctorId | query | False | string / uuid |
| SegmentId | query | False | string / uuid |
| VisitTypeId | query | False | string / uuid |
| BookingSource | query | False | ReservationBookingSource |
| ReservationStatus | query | False | ReservationStatus |
| TicketStatus | query | False | TicketStatus |
| AsOfUtc | query | False | string / date-time |
| ReportTimeZoneId | query | False | string |
| Grouping | query | False | ReportGrouping |
| ComparePreviousPeriod | query | False | boolean |
| PageNumber | query | False | integer / int32 |
| PageSize | query | False | integer / int32 |
| SortBy | query | False | string |
| SortDirection | query | False | string |
| ExpenseCategory | query | False | ExpenseCategoryCode |
| ReceptionId | query | False | string / uuid |
| PlanId | query | False | string / uuid |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `ReportModel`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18DoctorReportingApiTests; Phase18OperationalAnalyticsTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/reports/queue?fromDate=2026-10-01&toDate=2026-10-14
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "reportCode": "Example",
  "titleAr": "مراجعة تشغيلية",
  "titleEn": "Example",
  "description": "Example",
  "appliedFilters": {
    "fromDate": null,
    "toDate": null,
    "practiceId": null,
    "doctorId": null,
    "segmentId": null,
    "visitTypeId": null,
    "bookingSource": "Patient",
    "reservationStatus": "Active",
    "ticketStatus": "Waiting",
    "asOfUtc": null,
    "reportTimeZoneId": null,
    "grouping": "Day",
    "comparePreviousPeriod": false,
    "pageNumber": 1,
    "pageSize": 20,
    "sortBy": null,
    "sortDirection": null,
    "expenseCategory": "Rent",
    "receptionId": null,
    "planId": null
  },
  "scope": {
    "actor": "Doctor",
    "actorId": "11111111-1111-1111-1111-111111111111",
    "doctorId": null,
    "patientId": null,
    "practiceIds": null,
    "permissions": null
  },
  "window": {
    "fromDate": "2026-10-01",
    "toDate": "2026-10-01",
    "asOfUtc": "2026-10-14T09:00:00Z",
    "generatedAtUtc": "2026-10-14T09:00:00Z",
    "reportTimeZoneId": null
  },
  "dataFreshnessUtc": "2026-10-14T09:00:00Z",
  "dataStatus": "Complete",
  "metrics": [
    {}
  ],
  "series": [
    {}
  ],
  "breakdowns": [
    {}
  ],
  "availableActions": [
    {}
  ],
  "warnings": [
    "Example"
  ],
  "eligibleSampleCount": 0,
  "excludedSampleCount": 0,
  "records": [
    {}
  ],
  "detailRowCount": 0,
  "previousPeriodComparisons": [
    {}
  ]
}
```

## P18-080

**NEW · D.02–D.14 · Doctor · DoctorReporting.Reservations**

`GET /api/v1/doctors/me/reports/reservations`

Permission: Reports.Doctor.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: Reservations in the DoctorReporting journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| FromDate | query | False | string / date |
| ToDate | query | False | string / date |
| PracticeId | query | False | string / uuid |
| DoctorId | query | False | string / uuid |
| SegmentId | query | False | string / uuid |
| VisitTypeId | query | False | string / uuid |
| BookingSource | query | False | ReservationBookingSource |
| ReservationStatus | query | False | ReservationStatus |
| TicketStatus | query | False | TicketStatus |
| AsOfUtc | query | False | string / date-time |
| ReportTimeZoneId | query | False | string |
| Grouping | query | False | ReportGrouping |
| ComparePreviousPeriod | query | False | boolean |
| PageNumber | query | False | integer / int32 |
| PageSize | query | False | integer / int32 |
| SortBy | query | False | string |
| SortDirection | query | False | string |
| ExpenseCategory | query | False | ExpenseCategoryCode |
| ReceptionId | query | False | string / uuid |
| PlanId | query | False | string / uuid |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `ReportModel`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18DoctorReportingApiTests; Phase18OperationalAnalyticsTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/reports/reservations?fromDate=2026-10-01&toDate=2026-10-14
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "reportCode": "Example",
  "titleAr": "مراجعة تشغيلية",
  "titleEn": "Example",
  "description": "Example",
  "appliedFilters": {
    "fromDate": null,
    "toDate": null,
    "practiceId": null,
    "doctorId": null,
    "segmentId": null,
    "visitTypeId": null,
    "bookingSource": "Patient",
    "reservationStatus": "Active",
    "ticketStatus": "Waiting",
    "asOfUtc": null,
    "reportTimeZoneId": null,
    "grouping": "Day",
    "comparePreviousPeriod": false,
    "pageNumber": 1,
    "pageSize": 20,
    "sortBy": null,
    "sortDirection": null,
    "expenseCategory": "Rent",
    "receptionId": null,
    "planId": null
  },
  "scope": {
    "actor": "Doctor",
    "actorId": "11111111-1111-1111-1111-111111111111",
    "doctorId": null,
    "patientId": null,
    "practiceIds": null,
    "permissions": null
  },
  "window": {
    "fromDate": "2026-10-01",
    "toDate": "2026-10-01",
    "asOfUtc": "2026-10-14T09:00:00Z",
    "generatedAtUtc": "2026-10-14T09:00:00Z",
    "reportTimeZoneId": null
  },
  "dataFreshnessUtc": "2026-10-14T09:00:00Z",
  "dataStatus": "Complete",
  "metrics": [
    {}
  ],
  "series": [
    {}
  ],
  "breakdowns": [
    {}
  ],
  "availableActions": [
    {}
  ],
  "warnings": [
    "Example"
  ],
  "eligibleSampleCount": 0,
  "excludedSampleCount": 0,
  "records": [
    {}
  ],
  "detailRowCount": 0,
  "previousPeriodComparisons": [
    {}
  ]
}
```

## P18-081

**NEW · D.43–D.48 · Doctor · OperatingFinance.GetSalaryObligations**

`GET /api/v1/doctors/me/salary-obligations`

Permission: SalaryObligations.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: GetSalaryObligations in the financial management journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| FromDate | query | False | string / date |
| ToDate | query | False | string / date |
| PracticeId | query | False | string / uuid |
| ReceptionId | query | False | string / uuid |
| PlanId | query | False | string / uuid |
| PlanStatus | query | False | OperatingPlanStatus |
| ObligationStatus | query | False | OperatingObligationStatus |
| Category | query | False | ExpenseCategoryCode |
| PageNumber | query | False | integer / int32 |
| PageSize | query | False | integer / int32 |
| SortBy | query | False | string |
| SortDirection | query | False | string |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `OperatingObligationResponseReportPage`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/salary-obligations?fromDate=2026-10-01&toDate=2026-10-14
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "items": [
    {}
  ],
  "totalCount": 0,
  "pageNumber": 1,
  "pageSize": 20,
  "dataStatus": "Complete",
  "warnings": [
    "Example"
  ]
}
```

## P18-082

**NEW · D.43–D.48 · Doctor · OperatingFinance.GetSalaryObligationsById**

`GET /api/v1/doctors/me/salary-obligations/{id}`

Permission: SalaryObligations.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: GetSalaryObligationsById in the financial management journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| id | path | True | string / uuid |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `OperatingObligationResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/salary-obligations/11111111-1111-1111-1111-111111111111
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "doctorId": "11111111-1111-1111-1111-111111111111",
  "planId": "11111111-1111-1111-1111-111111111111",
  "receptionId": "11111111-1111-1111-1111-111111111111",
  "kind": "Salary",
  "occurrenceKey": "Example",
  "periodYear": 0,
  "periodMonth": 0,
  "businessDate": "2026-10-01",
  "dueBusinessDate": "2026-10-01",
  "plannedAmount": 650,
  "amount": 650,
  "currencyCode": "Example",
  "amountPolicy": "Fixed",
  "actualAmountConfirmed": false,
  "category": "Rent",
  "descriptionAr": "مراجعة تشغيلية",
  "descriptionEn": "Example",
  "status": "Unpaid",
  "dueState": "Example",
  "activeSettlementId": "11111111-1111-1111-1111-111111111111",
  "allocations": [
    {}
  ],
  "recordedAtUtc": "2026-10-14T09:00:00Z",
  "rowVersion": "AAAAAAAAB9E=",
  "availableActions": [
    {}
  ]
}
```

## P18-083

**NEW · D.43–D.48 · Doctor · OperatingFinance.PatchSalaryObligationsById**

`PATCH /api/v1/doctors/me/salary-obligations/{id}`

Permission: SalaryPlans.ManageOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: PatchSalaryObligationsById in the financial management journey. Refresh available actions after success; open returned detailRoute or refresh the entity, then fetch history and the affected dashboard/report.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| id | path | True | string / uuid |
| Idempotency-Key | header | True | string |
| Accept-Language | header | False | string; ['ar', 'en'] |
| request | Body | False | OperatingCorrectionRequest; see DTO tables and domain constraints |

Response: **200** `OperatingFinanceWriteResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Idempotent replay returns the recorded resource; no duplicate movement/history. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
PATCH /api/v1/doctors/me/salary-obligations/11111111-1111-1111-1111-111111111111
Authorization: Bearer <access-token>
Idempotency-Key: 7ce6cdbd-example-operation
Content-Type: application/json

{
  "amount": 650,
  "dueBusinessDate": "2026-10-01",
  "category": "Rent",
  "descriptionAr": "مراجعة تشغيلية",
  "allocations": [
    {}
  ],
  "reason": "Operational correction reviewed",
  "rowVersion": "AAAAAAAAB9E=",
  "descriptionEn": "Example"
}
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "resourceType": "Example",
  "detailRoute": "Example",
  "rowVersion": "AAAAAAAAB9E=",
  "obligationId": "11111111-1111-1111-1111-111111111111",
  "settlementId": "11111111-1111-1111-1111-111111111111"
}
```

## P18-084

**NEW · D.43–D.48 · Doctor · OperatingFinance.PostSalaryObligationsByIdCancel**

`POST /api/v1/doctors/me/salary-obligations/{id}/cancel`

Permission: SalaryPlans.ManageOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: PostSalaryObligationsByIdCancel in the financial management journey. Refresh available actions after success; open returned detailRoute or refresh the entity, then fetch history and the affected dashboard/report.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| id | path | True | string / uuid |
| Idempotency-Key | header | True | string |
| Accept-Language | header | False | string; ['ar', 'en'] |
| request | Body | False | OperatingTransitionRequest; see DTO tables and domain constraints |

Response: **200** `OperatingFinanceWriteResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Idempotent replay returns the recorded resource; no duplicate movement/history. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
POST /api/v1/doctors/me/salary-obligations/11111111-1111-1111-1111-111111111111/cancel
Authorization: Bearer <access-token>
Idempotency-Key: 7ce6cdbd-example-operation
Content-Type: application/json

{
  "rowVersion": "AAAAAAAAB9E=",
  "reason": "Operational correction reviewed"
}
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "resourceType": "Example",
  "detailRoute": "Example",
  "rowVersion": "AAAAAAAAB9E=",
  "obligationId": "11111111-1111-1111-1111-111111111111",
  "settlementId": "11111111-1111-1111-1111-111111111111"
}
```

## P18-085

**NEW · D.43–D.48 · Doctor · OperatingFinance.GetSalaryObligationsByIdHistory**

`GET /api/v1/doctors/me/salary-obligations/{id}/history`

Permission: SalaryObligations.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: GetSalaryObligationsByIdHistory in the financial management journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| id | path | True | string / uuid |
| pageNumber | query | False | integer / int32 |
| pageSize | query | False | integer / int32 |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `OperatingHistoryResponseReportPage`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/salary-obligations/11111111-1111-1111-1111-111111111111/history
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "items": [
    {}
  ],
  "totalCount": 0,
  "pageNumber": 1,
  "pageSize": 20,
  "dataStatus": "Complete",
  "warnings": [
    "Example"
  ]
}
```

## P18-086

**NEW · D.43–D.48 · Doctor · OperatingFinance.PostSalaryObligationsByIdSettle**

`POST /api/v1/doctors/me/salary-obligations/{id}/settle`

Permission: SalarySettlements.RecordOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: PostSalaryObligationsByIdSettle in the financial management journey. Refresh available actions after success; open returned detailRoute or refresh the entity, then fetch history and the affected dashboard/report.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| id | path | True | string / uuid |
| Idempotency-Key | header | True | string |
| Accept-Language | header | False | string; ['ar', 'en'] |
| request | Body | False | OperatingSettlementRequest; see DTO tables and domain constraints |

Response: **200** `OperatingFinanceWriteResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Idempotent replay returns the recorded resource; no duplicate movement/history. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
POST /api/v1/doctors/me/salary-obligations/11111111-1111-1111-1111-111111111111/settle
Authorization: Bearer <access-token>
Idempotency-Key: 7ce6cdbd-example-operation
Content-Type: application/json

{
  "amount": 650,
  "paidBusinessDate": "2026-10-01",
  "paymentMethod": "Cash",
  "rowVersion": "AAAAAAAAB9E=",
  "reference": "Example",
  "backdateReason": "Operational correction reviewed",
  "reportTimeZoneId": "Example"
}
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "resourceType": "Example",
  "detailRoute": "Example",
  "rowVersion": "AAAAAAAAB9E=",
  "obligationId": "11111111-1111-1111-1111-111111111111",
  "settlementId": "11111111-1111-1111-1111-111111111111"
}
```

## P18-087

**NEW · D.43–D.48 · Doctor · OperatingFinance.GetSalarySettlementsById**

`GET /api/v1/doctors/me/salary-settlements/{id}`

Permission: SalaryObligations.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: GetSalarySettlementsById in the financial management journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| id | path | True | string / uuid |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `OperatingSettlementResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/salary-settlements/11111111-1111-1111-1111-111111111111
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "obligationId": "11111111-1111-1111-1111-111111111111",
  "obligationRevisionId": "11111111-1111-1111-1111-111111111111",
  "receiptNumber": "Example",
  "amount": 650,
  "currencyCode": "Example",
  "paymentMethod": "Cash",
  "reference": "Example",
  "businessDate": "2026-10-01",
  "reportTimeZoneId": "Example",
  "backdateReason": "Operational correction reviewed",
  "actorApplicationUserId": "11111111-1111-1111-1111-111111111111",
  "recordedAtUtc": "2026-10-14T09:00:00Z",
  "isReversed": false,
  "reversalId": "11111111-1111-1111-1111-111111111111",
  "obligationRowVersion": "AAAAAAAAB9E=",
  "descriptionAr": "مراجعة تشغيلية",
  "descriptionEn": "Example",
  "allocations": [
    {}
  ],
  "availableActions": [
    {}
  ]
}
```

## P18-088

**NEW · D.43–D.48 · Doctor · OperatingFinance.GetSalarySettlementsByIdHistory**

`GET /api/v1/doctors/me/salary-settlements/{id}/history`

Permission: SalaryObligations.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: GetSalarySettlementsByIdHistory in the financial management journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| id | path | True | string / uuid |
| pageNumber | query | False | integer / int32 |
| pageSize | query | False | integer / int32 |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `OperatingHistoryResponseReportPage`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/salary-settlements/11111111-1111-1111-1111-111111111111/history
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "items": [
    {}
  ],
  "totalCount": 0,
  "pageNumber": 1,
  "pageSize": 20,
  "dataStatus": "Complete",
  "warnings": [
    "Example"
  ]
}
```

## P18-089

**NEW · D.43–D.48 · Doctor · OperatingFinance.GetSalarySettlementsByIdReceipt**

`GET /api/v1/doctors/me/salary-settlements/{id}/receipt`

Permission: SalaryObligations.ViewOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: GetSalarySettlementsByIdReceipt in the financial management journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| id | path | True | string / uuid |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `OperatingSettlementResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/doctors/me/salary-settlements/11111111-1111-1111-1111-111111111111/receipt
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "obligationId": "11111111-1111-1111-1111-111111111111",
  "obligationRevisionId": "11111111-1111-1111-1111-111111111111",
  "receiptNumber": "Example",
  "amount": 650,
  "currencyCode": "Example",
  "paymentMethod": "Cash",
  "reference": "Example",
  "businessDate": "2026-10-01",
  "reportTimeZoneId": "Example",
  "backdateReason": "Operational correction reviewed",
  "actorApplicationUserId": "11111111-1111-1111-1111-111111111111",
  "recordedAtUtc": "2026-10-14T09:00:00Z",
  "isReversed": false,
  "reversalId": "11111111-1111-1111-1111-111111111111",
  "obligationRowVersion": "AAAAAAAAB9E=",
  "descriptionAr": "مراجعة تشغيلية",
  "descriptionEn": "Example",
  "allocations": [
    {}
  ],
  "availableActions": [
    {}
  ]
}
```

## P18-090

**NEW · D.43–D.48 · Doctor · OperatingFinance.PostSalarySettlementsByIdReverse**

`POST /api/v1/doctors/me/salary-settlements/{id}/reverse`

Permission: SalarySettlements.ReverseOwn. Scope: Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden.

UI step: PostSalarySettlementsByIdReverse in the financial management journey. Refresh available actions after success; open returned detailRoute or refresh the entity, then fetch history and the affected dashboard/report.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| id | path | True | string / uuid |
| Idempotency-Key | header | True | string |
| Accept-Language | header | False | string; ['ar', 'en'] |
| request | Body | False | OperatingReversalRequest; see DTO tables and domain constraints |

Response: **200** `OperatingFinanceWriteResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Idempotent replay returns the recorded resource; no duplicate movement/history. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
POST /api/v1/doctors/me/salary-settlements/11111111-1111-1111-1111-111111111111/reverse
Authorization: Bearer <access-token>
Idempotency-Key: 7ce6cdbd-example-operation
Content-Type: application/json

{
  "businessDate": "2026-10-01",
  "rowVersion": "AAAAAAAAB9E=",
  "reason": "Operational correction reviewed",
  "reportTimeZoneId": "Example"
}
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "resourceType": "Example",
  "detailRoute": "Example",
  "rowVersion": "AAAAAAAAB9E=",
  "obligationId": "11111111-1111-1111-1111-111111111111",
  "settlementId": "11111111-1111-1111-1111-111111111111"
}
```

## P18-091

**NEW · P.01–P.08 · Patient · PatientDashboard.Capabilities**

`GET /api/v1/patients/me/dashboard/capabilities`

Permission: Dashboards.Patient.ViewOwn. Scope: Linked self PatientId resolved from current account; family membership never broadens this dashboard. Clinical/attachment access uses the existing policies.

UI step: Capabilities in the PatientDashboard journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `PatientDashboardCapabilities`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18PatientDashboardApiTests; existing Phase16 privacy regressions`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/patients/me/dashboard/capabilities
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "patientId": "11111111-1111-1111-1111-111111111111",
  "generatedAtUtc": "2026-10-14T09:00:00Z",
  "capabilities": [
    {}
  ],
  "crossDoctorSharingEnabled": false,
  "guardianSharingEnabled": false,
  "warnings": [
    "Example"
  ]
}
```

## P18-092

**NEW · P.01–P.08 · Patient · PatientDashboard.Overview**

`GET /api/v1/patients/me/dashboard/overview`

Permission: Dashboards.Patient.ViewOwn. Scope: Linked self PatientId resolved from current account; family membership never broadens this dashboard. Clinical/attachment access uses the existing policies.

UI step: Overview in the PatientDashboard journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `PatientDashboardOverview`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18PatientDashboardApiTests; existing Phase16 privacy regressions`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/patients/me/dashboard/overview
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "patientId": "11111111-1111-1111-1111-111111111111",
  "generatedAtUtc": "2026-10-14T09:00:00Z",
  "profile": {
    "patientId": "11111111-1111-1111-1111-111111111111",
    "nameAr": null,
    "nameEn": null,
    "dateOfBirth": "2026-10-01",
    "gender": "Male",
    "phoneNumber": null,
    "email": null,
    "hasProfileImage": false,
    "rowVersion": null
  },
  "upcomingBookings": {
    "items": null,
    "totalCount": 0,
    "pageNumber": 1,
    "pageSize": 20,
    "summary": {}
  },
  "activeTickets": [
    {}
  ],
  "medicalSummary": {
    "completedVisitCount": 0,
    "currentPrescriptionCount": 0,
    "officialLabResultCount": 0,
    "officialRadiologyResultCount": 0,
    "hiddenEncounterCount": 0
  },
  "recentPermittedTimeline": {
    "items": null,
    "nextCursor": null,
    "hasMore": false
  },
  "followUpAvailability": {
    "items": null,
    "totalCount": 0,
    "pageNumber": 1,
    "pageSize": 20
  },
  "recentFinancialTransactions": {
    "items": null,
    "totalCount": 0,
    "pageNumber": 1,
    "pageSize": 20
  },
  "family": {
    "familyId": "11111111-1111-1111-1111-111111111111",
    "status": "Active",
    "members": null
  },
  "capabilities": {
    "patientId": "11111111-1111-1111-1111-111111111111",
    "generatedAtUtc": "2026-10-14T09:00:00Z",
    "capabilities": null,
    "crossDoctorSharingEnabled": false,
    "guardianSharingEnabled": false,
    "warnings": null
  },
  "warnings": [
    "Example"
  ]
}
```

## P18-093

**NEW · R.01–R.17 · Reception · ReceptionReporting.Attention**

`GET /api/v1/reception/attention`

Permission: Dashboards.Reception.ViewAssigned. Scope: Current active Reception, approved active owner, active assignment and exact delegated permission for each Practice; underlying detail permission is checked separately.

UI step: Attention in the ReceptionReporting journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| practiceId | query | False | string / uuid |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `ReceptionAttentionResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18ReceptionApiTests; Phase18HandoverTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/reception/attention
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "asOfUtc": "2026-10-14T09:00:00Z",
  "metrics": [
    {}
  ],
  "worklist": {
    "items": null,
    "totalCount": 0,
    "pageNumber": 1,
    "pageSize": 20,
    "dataStatus": "Complete",
    "warnings": null
  },
  "warnings": [
    "Example"
  ]
}
```

## P18-094

**NEW · R.01–R.17 · Reception · ReceptionReporting.FilterOptions**

`GET /api/v1/reception/dashboard/filter-options`

Permission: Dashboards.Reception.ViewAssigned OR Reports.Reception.ViewAssigned (service guard). Scope: Current active Reception, approved active owner, active assignment and exact delegated permission for each Practice; underlying detail permission is checked separately.

UI step: FilterOptions in the ReceptionReporting journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| PracticeId | query | False | string / uuid |
| Search | query | False | string |
| PageNumber | query | False | integer / int32 |
| PageSize | query | False | integer / int32 |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `ReportFilterOptions`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18ReceptionApiTests; Phase18HandoverTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/reception/dashboard/filter-options
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "practices": [
    {}
  ],
  "segments": [
    {}
  ],
  "visitTypes": [
    {}
  ],
  "bookingSources": [
    {}
  ],
  "reservationStatuses": [
    {}
  ],
  "ticketStatuses": [
    {}
  ],
  "groupings": [
    "Day"
  ],
  "defaultReportTimeZoneId": "Example",
  "exportLimits": {
    "maximumCalendarDays": 0,
    "excelMaximumRows": 0,
    "pdfMaximumRows": 0,
    "excelMaximumBytes": 0,
    "pdfMaximumBytes": 0
  },
  "pagination": {
    "pageNumber": 1,
    "pageSize": 20,
    "practiceTotalCount": 0,
    "segmentTotalCount": 0,
    "visitTypeTotalCount": 0,
    "receptionTotalCount": 0
  }
}
```

## P18-095

**NEW · R.01–R.17 · Reception · ReceptionReporting.Overview**

`GET /api/v1/reception/dashboard/overview`

Permission: Dashboards.Reception.ViewAssigned. Scope: Current active Reception, approved active owner, active assignment and exact delegated permission for each Practice; underlying detail permission is checked separately.

UI step: Overview in the ReceptionReporting journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| FromDate | query | False | string / date |
| ToDate | query | False | string / date |
| PracticeId | query | False | string / uuid |
| DoctorId | query | False | string / uuid |
| SegmentId | query | False | string / uuid |
| VisitTypeId | query | False | string / uuid |
| BookingSource | query | False | ReservationBookingSource |
| ReservationStatus | query | False | ReservationStatus |
| TicketStatus | query | False | TicketStatus |
| AsOfUtc | query | False | string / date-time |
| ReportTimeZoneId | query | False | string |
| Grouping | query | False | ReportGrouping |
| ComparePreviousPeriod | query | False | boolean |
| PageNumber | query | False | integer / int32 |
| PageSize | query | False | integer / int32 |
| SortBy | query | False | string |
| SortDirection | query | False | string |
| ExpenseCategory | query | False | ExpenseCategoryCode |
| ReceptionId | query | False | string / uuid |
| PlanId | query | False | string / uuid |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `ReportModel`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18ReceptionApiTests; Phase18HandoverTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/reception/dashboard/overview?fromDate=2026-10-01&toDate=2026-10-14
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "reportCode": "Example",
  "titleAr": "مراجعة تشغيلية",
  "titleEn": "Example",
  "description": "Example",
  "appliedFilters": {
    "fromDate": null,
    "toDate": null,
    "practiceId": null,
    "doctorId": null,
    "segmentId": null,
    "visitTypeId": null,
    "bookingSource": "Patient",
    "reservationStatus": "Active",
    "ticketStatus": "Waiting",
    "asOfUtc": null,
    "reportTimeZoneId": null,
    "grouping": "Day",
    "comparePreviousPeriod": false,
    "pageNumber": 1,
    "pageSize": 20,
    "sortBy": null,
    "sortDirection": null,
    "expenseCategory": "Rent",
    "receptionId": null,
    "planId": null
  },
  "scope": {
    "actor": "Doctor",
    "actorId": "11111111-1111-1111-1111-111111111111",
    "doctorId": null,
    "patientId": null,
    "practiceIds": null,
    "permissions": null
  },
  "window": {
    "fromDate": "2026-10-01",
    "toDate": "2026-10-01",
    "asOfUtc": "2026-10-14T09:00:00Z",
    "generatedAtUtc": "2026-10-14T09:00:00Z",
    "reportTimeZoneId": null
  },
  "dataFreshnessUtc": "2026-10-14T09:00:00Z",
  "dataStatus": "Complete",
  "metrics": [
    {}
  ],
  "series": [
    {}
  ],
  "breakdowns": [
    {}
  ],
  "availableActions": [
    {}
  ],
  "warnings": [
    "Example"
  ],
  "eligibleSampleCount": 0,
  "excludedSampleCount": 0,
  "records": [
    {}
  ],
  "detailRowCount": 0,
  "previousPeriodComparisons": [
    {}
  ]
}
```

## P18-096

**NEW · R.18–R.22; Doctor oversight · Reception · ReceptionHandovers.List**

`GET /api/v1/reception/practices/{practiceId}/handovers`

Permission: Handovers.ViewAssigned. Scope: Current active Reception, approved active owner, active assignment and exact delegated permission for each Practice; underlying detail permission is checked separately.

UI step: List in the ReceptionHandovers journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| practiceId | path | True | string / uuid |
| PracticeId | query | False | string / uuid |
| Status | query | False | HandoverStatus |
| FromDate | query | False | string / date |
| ToDate | query | False | string / date |
| PageNumber | query | False | integer / int32 |
| PageSize | query | False | integer / int32 |
| SortDirection | query | False | string |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `HandoverResponseReportPage`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18ReceptionApiTests; Phase18HandoverTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/reception/practices/11111111-1111-1111-1111-111111111111/handovers?fromDate=2026-10-01&toDate=2026-10-14
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "items": [
    {}
  ],
  "totalCount": 0,
  "pageNumber": 1,
  "pageSize": 20,
  "dataStatus": "Complete",
  "warnings": [
    "Example"
  ]
}
```

## P18-097

**NEW · R.18–R.22; Doctor oversight · Reception · ReceptionHandovers.Create**

`POST /api/v1/reception/practices/{practiceId}/handovers`

Permission: Handovers.CreateAssigned; Handovers.ViewAssigned. Scope: Current active Reception, approved active owner, active assignment and exact delegated permission for each Practice; underlying detail permission is checked separately.

UI step: Create in the ReceptionHandovers journey. Refresh available actions after success; open returned detailRoute or refresh the entity, then fetch history and the affected dashboard/report.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| practiceId | path | True | string / uuid |
| Idempotency-Key | header | True | string |
| Accept-Language | header | False | string; ['ar', 'en'] |
| request | Body | False | HandoverCreateRequest; see DTO tables and domain constraints |

Response: **200** `HandoverResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Idempotent replay returns the recorded resource; no duplicate movement/history. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18ReceptionApiTests; Phase18HandoverTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
POST /api/v1/reception/practices/11111111-1111-1111-1111-111111111111/handovers
Authorization: Bearer <access-token>
Idempotency-Key: 7ce6cdbd-example-operation
Content-Type: application/json

{
  "summary": "مراجعة تشغيلية",
  "reservationId": "11111111-1111-1111-1111-111111111111",
  "ticketId": "11111111-1111-1111-1111-111111111111",
  "previousHandoverId": "11111111-1111-1111-1111-111111111111"
}
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "doctorPracticeId": "11111111-1111-1111-1111-111111111111",
  "summary": "مراجعة تشغيلية",
  "reservationId": "11111111-1111-1111-1111-111111111111",
  "ticketId": "11111111-1111-1111-1111-111111111111",
  "previousHandoverId": "11111111-1111-1111-1111-111111111111",
  "status": "Open",
  "createdByApplicationUserId": "11111111-1111-1111-1111-111111111111",
  "createdOnUtc": "2026-10-14T09:00:00Z",
  "acknowledgedByApplicationUserId": "11111111-1111-1111-1111-111111111111",
  "acknowledgedOnUtc": "2026-10-14T09:00:00Z",
  "closedByApplicationUserId": "11111111-1111-1111-1111-111111111111",
  "closedOnUtc": "2026-10-14T09:00:00Z",
  "closureOutcome": "Example",
  "rowVersion": "AAAAAAAAB9E=",
  "availableActions": [
    {}
  ]
}
```

## P18-098

**NEW · R.18–R.22; Doctor oversight · Reception · ReceptionHandovers.Detail**

`GET /api/v1/reception/practices/{practiceId}/handovers/{handoverId}`

Permission: Handovers.ViewAssigned. Scope: Current active Reception, approved active owner, active assignment and exact delegated permission for each Practice; underlying detail permission is checked separately.

UI step: Detail in the ReceptionHandovers journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| practiceId | path | True | string / uuid |
| handoverId | path | True | string / uuid |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `HandoverResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18ReceptionApiTests; Phase18HandoverTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/reception/practices/11111111-1111-1111-1111-111111111111/handovers/11111111-1111-1111-1111-111111111111
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "doctorPracticeId": "11111111-1111-1111-1111-111111111111",
  "summary": "مراجعة تشغيلية",
  "reservationId": "11111111-1111-1111-1111-111111111111",
  "ticketId": "11111111-1111-1111-1111-111111111111",
  "previousHandoverId": "11111111-1111-1111-1111-111111111111",
  "status": "Open",
  "createdByApplicationUserId": "11111111-1111-1111-1111-111111111111",
  "createdOnUtc": "2026-10-14T09:00:00Z",
  "acknowledgedByApplicationUserId": "11111111-1111-1111-1111-111111111111",
  "acknowledgedOnUtc": "2026-10-14T09:00:00Z",
  "closedByApplicationUserId": "11111111-1111-1111-1111-111111111111",
  "closedOnUtc": "2026-10-14T09:00:00Z",
  "closureOutcome": "Example",
  "rowVersion": "AAAAAAAAB9E=",
  "availableActions": [
    {}
  ]
}
```

## P18-099

**NEW · R.18–R.22; Doctor oversight · Reception · ReceptionHandovers.Acknowledge**

`POST /api/v1/reception/practices/{practiceId}/handovers/{handoverId}/acknowledge`

Permission: Handovers.AcknowledgeAssigned; Handovers.ViewAssigned. Scope: Current active Reception, approved active owner, active assignment and exact delegated permission for each Practice; underlying detail permission is checked separately.

UI step: Acknowledge in the ReceptionHandovers journey. Refresh available actions after success; open returned detailRoute or refresh the entity, then fetch history and the affected dashboard/report.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| practiceId | path | True | string / uuid |
| handoverId | path | True | string / uuid |
| Idempotency-Key | header | True | string |
| Accept-Language | header | False | string; ['ar', 'en'] |
| request | Body | False | HandoverActionRequest; see DTO tables and domain constraints |

Response: **200** `HandoverResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Idempotent replay returns the recorded resource; no duplicate movement/history. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18ReceptionApiTests; Phase18HandoverTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
POST /api/v1/reception/practices/11111111-1111-1111-1111-111111111111/handovers/11111111-1111-1111-1111-111111111111/acknowledge
Authorization: Bearer <access-token>
Idempotency-Key: 7ce6cdbd-example-operation
Content-Type: application/json

{
  "rowVersion": "AAAAAAAAB9E=",
  "outcome": "Operational correction reviewed",
  "annotation": "Example"
}
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "doctorPracticeId": "11111111-1111-1111-1111-111111111111",
  "summary": "مراجعة تشغيلية",
  "reservationId": "11111111-1111-1111-1111-111111111111",
  "ticketId": "11111111-1111-1111-1111-111111111111",
  "previousHandoverId": "11111111-1111-1111-1111-111111111111",
  "status": "Open",
  "createdByApplicationUserId": "11111111-1111-1111-1111-111111111111",
  "createdOnUtc": "2026-10-14T09:00:00Z",
  "acknowledgedByApplicationUserId": "11111111-1111-1111-1111-111111111111",
  "acknowledgedOnUtc": "2026-10-14T09:00:00Z",
  "closedByApplicationUserId": "11111111-1111-1111-1111-111111111111",
  "closedOnUtc": "2026-10-14T09:00:00Z",
  "closureOutcome": "Example",
  "rowVersion": "AAAAAAAAB9E=",
  "availableActions": [
    {}
  ]
}
```

## P18-100

**NEW · R.18–R.22; Doctor oversight · Reception · ReceptionHandovers.Annotate**

`POST /api/v1/reception/practices/{practiceId}/handovers/{handoverId}/annotations`

Permission: Handovers.AnnotateAssigned; Handovers.ViewAssigned. Scope: Current active Reception, approved active owner, active assignment and exact delegated permission for each Practice; underlying detail permission is checked separately.

UI step: Annotate in the ReceptionHandovers journey. Refresh available actions after success; open returned detailRoute or refresh the entity, then fetch history and the affected dashboard/report.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| practiceId | path | True | string / uuid |
| handoverId | path | True | string / uuid |
| Idempotency-Key | header | True | string |
| Accept-Language | header | False | string; ['ar', 'en'] |
| request | Body | False | HandoverActionRequest; see DTO tables and domain constraints |

Response: **200** `HandoverResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Idempotent replay returns the recorded resource; no duplicate movement/history. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18ReceptionApiTests; Phase18HandoverTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
POST /api/v1/reception/practices/11111111-1111-1111-1111-111111111111/handovers/11111111-1111-1111-1111-111111111111/annotations
Authorization: Bearer <access-token>
Idempotency-Key: 7ce6cdbd-example-operation
Content-Type: application/json

{
  "rowVersion": "AAAAAAAAB9E=",
  "outcome": "Operational correction reviewed",
  "annotation": "Example"
}
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "doctorPracticeId": "11111111-1111-1111-1111-111111111111",
  "summary": "مراجعة تشغيلية",
  "reservationId": "11111111-1111-1111-1111-111111111111",
  "ticketId": "11111111-1111-1111-1111-111111111111",
  "previousHandoverId": "11111111-1111-1111-1111-111111111111",
  "status": "Open",
  "createdByApplicationUserId": "11111111-1111-1111-1111-111111111111",
  "createdOnUtc": "2026-10-14T09:00:00Z",
  "acknowledgedByApplicationUserId": "11111111-1111-1111-1111-111111111111",
  "acknowledgedOnUtc": "2026-10-14T09:00:00Z",
  "closedByApplicationUserId": "11111111-1111-1111-1111-111111111111",
  "closedOnUtc": "2026-10-14T09:00:00Z",
  "closureOutcome": "Example",
  "rowVersion": "AAAAAAAAB9E=",
  "availableActions": [
    {}
  ]
}
```

## P18-101

**NEW · R.18–R.22; Doctor oversight · Reception · ReceptionHandovers.Close**

`POST /api/v1/reception/practices/{practiceId}/handovers/{handoverId}/close`

Permission: Handovers.CloseAssigned; Handovers.ViewAssigned. Scope: Current active Reception, approved active owner, active assignment and exact delegated permission for each Practice; underlying detail permission is checked separately.

UI step: Close in the ReceptionHandovers journey. Refresh available actions after success; open returned detailRoute or refresh the entity, then fetch history and the affected dashboard/report.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| practiceId | path | True | string / uuid |
| handoverId | path | True | string / uuid |
| Idempotency-Key | header | True | string |
| Accept-Language | header | False | string; ['ar', 'en'] |
| request | Body | False | HandoverActionRequest; see DTO tables and domain constraints |

Response: **200** `HandoverResponse`. Nullable members, enums and nested contracts are in the linked DTO tables. Idempotent replay returns the recorded resource; no duplicate movement/history. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18ReceptionApiTests; Phase18HandoverTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
POST /api/v1/reception/practices/11111111-1111-1111-1111-111111111111/handovers/11111111-1111-1111-1111-111111111111/close
Authorization: Bearer <access-token>
Idempotency-Key: 7ce6cdbd-example-operation
Content-Type: application/json

{
  "rowVersion": "AAAAAAAAB9E=",
  "outcome": "Operational correction reviewed",
  "annotation": "Example"
}
```

Example 200 response shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "doctorPracticeId": "11111111-1111-1111-1111-111111111111",
  "summary": "مراجعة تشغيلية",
  "reservationId": "11111111-1111-1111-1111-111111111111",
  "ticketId": "11111111-1111-1111-1111-111111111111",
  "previousHandoverId": "11111111-1111-1111-1111-111111111111",
  "status": "Open",
  "createdByApplicationUserId": "11111111-1111-1111-1111-111111111111",
  "createdOnUtc": "2026-10-14T09:00:00Z",
  "acknowledgedByApplicationUserId": "11111111-1111-1111-1111-111111111111",
  "acknowledgedOnUtc": "2026-10-14T09:00:00Z",
  "closedByApplicationUserId": "11111111-1111-1111-1111-111111111111",
  "closedOnUtc": "2026-10-14T09:00:00Z",
  "closureOutcome": "Example",
  "rowVersion": "AAAAAAAAB9E=",
  "availableActions": [
    {}
  ]
}
```

## P18-102

**NEW · R.18–R.22; Doctor oversight · Reception · ReceptionHandovers.History**

`GET /api/v1/reception/practices/{practiceId}/handovers/{handoverId}/history`

Permission: Handovers.ViewAssigned. Scope: Current active Reception, approved active owner, active assignment and exact delegated permission for each Practice; underlying detail permission is checked separately.

UI step: History in the ReceptionHandovers journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| practiceId | path | True | string / uuid |
| handoverId | path | True | string / uuid |
| pageNumber | query | False | integer / int32 |
| pageSize | query | False | integer / int32 |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `HandoverHistoryResponseReportPage`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18ReceptionApiTests; Phase18HandoverTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/reception/practices/11111111-1111-1111-1111-111111111111/handovers/11111111-1111-1111-1111-111111111111/history
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "items": [
    {}
  ],
  "totalCount": 0,
  "pageNumber": 1,
  "pageSize": 20,
  "dataStatus": "Complete",
  "warnings": [
    "Example"
  ]
}
```

## P18-103

**NEW · R.01–R.17 · Reception · ReceptionReporting.Worklist**

`GET /api/v1/reception/practices/{practiceId}/worklist`

Permission: Dashboards.Reception.ViewAssigned. Scope: Current active Reception, approved active owner, active assignment and exact delegated permission for each Practice; underlying detail permission is checked separately.

UI step: Worklist in the ReceptionReporting journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| practiceId | path | True | string / uuid |
| BusinessDate | query | False | string / date |
| Category | query | False | WorklistCategory |
| SegmentId | query | False | string / uuid |
| VisitTypeId | query | False | string / uuid |
| ReservationStatus | query | False | ReservationStatus |
| TicketStatus | query | False | TicketStatus |
| PageNumber | query | False | integer / int32 |
| PageSize | query | False | integer / int32 |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `WorklistItemReportPage`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18ReceptionApiTests; Phase18HandoverTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/reception/practices/11111111-1111-1111-1111-111111111111/worklist
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "items": [
    {}
  ],
  "totalCount": 0,
  "pageNumber": 1,
  "pageSize": 20,
  "dataStatus": "Complete",
  "warnings": [
    "Example"
  ]
}
```

## P18-104

**NEW · R.01–R.17 · Reception · ReceptionReporting.Drilldown**

`GET /api/v1/reception/reports/{reportCode}/drilldown`

Permission: Reports.Reception.ViewAssigned. Scope: Current active Reception, approved active owner, active assignment and exact delegated permission for each Practice; underlying detail permission is checked separately.

UI step: Drilldown in the ReceptionReporting journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| reportCode | path | True | string |
| metricCode | query | False | string |
| FromDate | query | False | string / date |
| ToDate | query | False | string / date |
| PracticeId | query | False | string / uuid |
| DoctorId | query | False | string / uuid |
| SegmentId | query | False | string / uuid |
| VisitTypeId | query | False | string / uuid |
| BookingSource | query | False | ReservationBookingSource |
| ReservationStatus | query | False | ReservationStatus |
| TicketStatus | query | False | TicketStatus |
| AsOfUtc | query | False | string / date-time |
| ReportTimeZoneId | query | False | string |
| Grouping | query | False | ReportGrouping |
| ComparePreviousPeriod | query | False | boolean |
| PageNumber | query | False | integer / int32 |
| PageSize | query | False | integer / int32 |
| SortBy | query | False | string |
| SortDirection | query | False | string |
| ExpenseCategory | query | False | ExpenseCategoryCode |
| ReceptionId | query | False | string / uuid |
| PlanId | query | False | string / uuid |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `ReportRecordReportPage`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18ReceptionApiTests; Phase18HandoverTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/reception/reports/overview/drilldown?fromDate=2026-10-01&toDate=2026-10-14&metricCode=CheckIns
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "items": [
    {}
  ],
  "totalCount": 0,
  "pageNumber": 1,
  "pageSize": 20,
  "dataStatus": "Complete",
  "warnings": [
    "Example"
  ]
}
```

## P18-105

**NEW · R.01–R.17 · Reception · ReceptionReportExports.Export**

`GET /api/v1/reception/reports/{reportCode}/export`

Permission: Reports.Reception.ExportOperational + preview/source permissions + exact assignment delegation. Scope: Current active Reception, approved active owner, active assignment and exact delegated permission for each Practice; underlying detail permission is checked separately.

UI step: Export in the ReceptionReportExports journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| reportCode | path | True | string |
| FromDate | query | False | string / date |
| ToDate | query | False | string / date |
| PracticeId | query | False | string / uuid |
| DoctorId | query | False | string / uuid |
| SegmentId | query | False | string / uuid |
| VisitTypeId | query | False | string / uuid |
| BookingSource | query | False | ReservationBookingSource |
| ReservationStatus | query | False | ReservationStatus |
| TicketStatus | query | False | TicketStatus |
| AsOfUtc | query | False | string / date-time |
| ReportTimeZoneId | query | False | string |
| Grouping | query | False | ReportGrouping |
| ComparePreviousPeriod | query | False | boolean |
| PageNumber | query | False | integer / int32 |
| PageSize | query | False | integer / int32 |
| SortBy | query | False | string |
| SortDirection | query | False | string |
| ExpenseCategory | query | False | ExpenseCategoryCode |
| ReceptionId | query | False | string / uuid |
| PlanId | query | False | string / uuid |
| format | query | False | string |
| language | query | False | string |
| landscape | query | False | boolean |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `string / binary`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18ExportApiTests; Phase18ReportExportTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/reception/reports/overview/export?fromDate=2026-10-01&toDate=2026-10-14&format=xlsx&language=ar
Authorization: Bearer <access-token>
```

Example 200 response shape:

```text
<binary PDF or XLSX bytes; Content-Disposition supplies the filename>
```

## P18-106

**NEW · R.01–R.17 · Reception · ReceptionReporting.Activity**

`GET /api/v1/reception/reports/activity`

Permission: Reports.Reception.ViewAssigned. Scope: Current active Reception, approved active owner, active assignment and exact delegated permission for each Practice; underlying detail permission is checked separately.

UI step: Activity in the ReceptionReporting journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| FromDate | query | False | string / date |
| ToDate | query | False | string / date |
| PracticeId | query | False | string / uuid |
| DoctorId | query | False | string / uuid |
| SegmentId | query | False | string / uuid |
| VisitTypeId | query | False | string / uuid |
| BookingSource | query | False | ReservationBookingSource |
| ReservationStatus | query | False | ReservationStatus |
| TicketStatus | query | False | TicketStatus |
| AsOfUtc | query | False | string / date-time |
| ReportTimeZoneId | query | False | string |
| Grouping | query | False | ReportGrouping |
| ComparePreviousPeriod | query | False | boolean |
| PageNumber | query | False | integer / int32 |
| PageSize | query | False | integer / int32 |
| SortBy | query | False | string |
| SortDirection | query | False | string |
| ExpenseCategory | query | False | ExpenseCategoryCode |
| ReceptionId | query | False | string / uuid |
| PlanId | query | False | string / uuid |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `ReportModel`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18ReceptionApiTests; Phase18HandoverTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/reception/reports/activity?fromDate=2026-10-01&toDate=2026-10-14
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "reportCode": "Example",
  "titleAr": "مراجعة تشغيلية",
  "titleEn": "Example",
  "description": "Example",
  "appliedFilters": {
    "fromDate": null,
    "toDate": null,
    "practiceId": null,
    "doctorId": null,
    "segmentId": null,
    "visitTypeId": null,
    "bookingSource": "Patient",
    "reservationStatus": "Active",
    "ticketStatus": "Waiting",
    "asOfUtc": null,
    "reportTimeZoneId": null,
    "grouping": "Day",
    "comparePreviousPeriod": false,
    "pageNumber": 1,
    "pageSize": 20,
    "sortBy": null,
    "sortDirection": null,
    "expenseCategory": "Rent",
    "receptionId": null,
    "planId": null
  },
  "scope": {
    "actor": "Doctor",
    "actorId": "11111111-1111-1111-1111-111111111111",
    "doctorId": null,
    "patientId": null,
    "practiceIds": null,
    "permissions": null
  },
  "window": {
    "fromDate": "2026-10-01",
    "toDate": "2026-10-01",
    "asOfUtc": "2026-10-14T09:00:00Z",
    "generatedAtUtc": "2026-10-14T09:00:00Z",
    "reportTimeZoneId": null
  },
  "dataFreshnessUtc": "2026-10-14T09:00:00Z",
  "dataStatus": "Complete",
  "metrics": [
    {}
  ],
  "series": [
    {}
  ],
  "breakdowns": [
    {}
  ],
  "availableActions": [
    {}
  ],
  "warnings": [
    "Example"
  ],
  "eligibleSampleCount": 0,
  "excludedSampleCount": 0,
  "records": [
    {}
  ],
  "detailRowCount": 0,
  "previousPeriodComparisons": [
    {}
  ]
}
```

## P18-107

**NEW · R.01–R.17 · Reception · ReceptionReporting.Catalog**

`GET /api/v1/reception/reports/catalog`

Permission: Reports.Reception.ViewAssigned. Scope: Current active Reception, approved active owner, active assignment and exact delegated permission for each Practice; underlying detail permission is checked separately.

UI step: Catalog in the ReceptionReporting journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `ReportCatalogEntry[]`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18ReceptionApiTests; Phase18HandoverTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/reception/reports/catalog
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
[
  {
    "reportCode": null,
    "titleAr": null,
    "titleEn": null,
    "description": null,
    "allowedFilters": null,
    "availableGroupings": null,
    "scope": null,
    "canPreview": false,
    "canDrillDown": false,
    "canExportPdf": false,
    "canExportExcel": false,
    "exportLimits": {},
    "requiredPermissions": null,
    "dataStatus": "Complete",
    "exportRequiredPermissions": null,
    "minimumCalendarDays": 0,
    "maximumCalendarDays": 0,
    "dataAvailability": null,
    "previewRoute": null,
    "drilldownRoute": null,
    "exportRoute": null
  }
]
```

## P18-108

**NEW · R.01–R.17 · Reception · ReceptionReporting.Queue**

`GET /api/v1/reception/reports/queue`

Permission: Reports.Reception.ViewAssigned. Scope: Current active Reception, approved active owner, active assignment and exact delegated permission for each Practice; underlying detail permission is checked separately.

UI step: Queue in the ReceptionReporting journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| FromDate | query | False | string / date |
| ToDate | query | False | string / date |
| PracticeId | query | False | string / uuid |
| DoctorId | query | False | string / uuid |
| SegmentId | query | False | string / uuid |
| VisitTypeId | query | False | string / uuid |
| BookingSource | query | False | ReservationBookingSource |
| ReservationStatus | query | False | ReservationStatus |
| TicketStatus | query | False | TicketStatus |
| AsOfUtc | query | False | string / date-time |
| ReportTimeZoneId | query | False | string |
| Grouping | query | False | ReportGrouping |
| ComparePreviousPeriod | query | False | boolean |
| PageNumber | query | False | integer / int32 |
| PageSize | query | False | integer / int32 |
| SortBy | query | False | string |
| SortDirection | query | False | string |
| ExpenseCategory | query | False | ExpenseCategoryCode |
| ReceptionId | query | False | string / uuid |
| PlanId | query | False | string / uuid |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `ReportModel`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18ReceptionApiTests; Phase18HandoverTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/reception/reports/queue?fromDate=2026-10-01&toDate=2026-10-14
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "reportCode": "Example",
  "titleAr": "مراجعة تشغيلية",
  "titleEn": "Example",
  "description": "Example",
  "appliedFilters": {
    "fromDate": null,
    "toDate": null,
    "practiceId": null,
    "doctorId": null,
    "segmentId": null,
    "visitTypeId": null,
    "bookingSource": "Patient",
    "reservationStatus": "Active",
    "ticketStatus": "Waiting",
    "asOfUtc": null,
    "reportTimeZoneId": null,
    "grouping": "Day",
    "comparePreviousPeriod": false,
    "pageNumber": 1,
    "pageSize": 20,
    "sortBy": null,
    "sortDirection": null,
    "expenseCategory": "Rent",
    "receptionId": null,
    "planId": null
  },
  "scope": {
    "actor": "Doctor",
    "actorId": "11111111-1111-1111-1111-111111111111",
    "doctorId": null,
    "patientId": null,
    "practiceIds": null,
    "permissions": null
  },
  "window": {
    "fromDate": "2026-10-01",
    "toDate": "2026-10-01",
    "asOfUtc": "2026-10-14T09:00:00Z",
    "generatedAtUtc": "2026-10-14T09:00:00Z",
    "reportTimeZoneId": null
  },
  "dataFreshnessUtc": "2026-10-14T09:00:00Z",
  "dataStatus": "Complete",
  "metrics": [
    {}
  ],
  "series": [
    {}
  ],
  "breakdowns": [
    {}
  ],
  "availableActions": [
    {}
  ],
  "warnings": [
    "Example"
  ],
  "eligibleSampleCount": 0,
  "excludedSampleCount": 0,
  "records": [
    {}
  ],
  "detailRowCount": 0,
  "previousPeriodComparisons": [
    {}
  ]
}
```

## P18-109

**NEW · R.01–R.17 · Reception · ReceptionReporting.Reservations**

`GET /api/v1/reception/reports/reservations`

Permission: Reports.Reception.ViewAssigned. Scope: Current active Reception, approved active owner, active assignment and exact delegated permission for each Practice; underlying detail permission is checked separately.

UI step: Reservations in the ReceptionReporting journey. Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions.

| Input | Location | Required by schema | Type / values |
|---|---|---|---|
| FromDate | query | False | string / date |
| ToDate | query | False | string / date |
| PracticeId | query | False | string / uuid |
| DoctorId | query | False | string / uuid |
| SegmentId | query | False | string / uuid |
| VisitTypeId | query | False | string / uuid |
| BookingSource | query | False | ReservationBookingSource |
| ReservationStatus | query | False | ReservationStatus |
| TicketStatus | query | False | TicketStatus |
| AsOfUtc | query | False | string / date-time |
| ReportTimeZoneId | query | False | string |
| Grouping | query | False | ReportGrouping |
| ComparePreviousPeriod | query | False | boolean |
| PageNumber | query | False | integer / int32 |
| PageSize | query | False | integer / int32 |
| SortBy | query | False | string |
| SortDirection | query | False | string |
| ExpenseCategory | query | False | ExpenseCategoryCode |
| ReceptionId | query | False | string / uuid |
| PlanId | query | False | string / uuid |
| Accept-Language | header | False | string; ['ar', 'en'] |

Response: **200** `ReportModel`. Nullable members, enums and nested contracts are in the linked DTO tables. Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.

Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `Phase18ReceptionApiTests; Phase18HandoverTests; Phase18ReconciliationTests`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.

Example request:

```http
GET /api/v1/reception/reports/reservations?fromDate=2026-10-01&toDate=2026-10-14
Authorization: Bearer <access-token>
```

Example 200 response shape:

```json
{
  "reportCode": "Example",
  "titleAr": "مراجعة تشغيلية",
  "titleEn": "Example",
  "description": "Example",
  "appliedFilters": {
    "fromDate": null,
    "toDate": null,
    "practiceId": null,
    "doctorId": null,
    "segmentId": null,
    "visitTypeId": null,
    "bookingSource": "Patient",
    "reservationStatus": "Active",
    "ticketStatus": "Waiting",
    "asOfUtc": null,
    "reportTimeZoneId": null,
    "grouping": "Day",
    "comparePreviousPeriod": false,
    "pageNumber": 1,
    "pageSize": 20,
    "sortBy": null,
    "sortDirection": null,
    "expenseCategory": "Rent",
    "receptionId": null,
    "planId": null
  },
  "scope": {
    "actor": "Doctor",
    "actorId": "11111111-1111-1111-1111-111111111111",
    "doctorId": null,
    "patientId": null,
    "practiceIds": null,
    "permissions": null
  },
  "window": {
    "fromDate": "2026-10-01",
    "toDate": "2026-10-01",
    "asOfUtc": "2026-10-14T09:00:00Z",
    "generatedAtUtc": "2026-10-14T09:00:00Z",
    "reportTimeZoneId": null
  },
  "dataFreshnessUtc": "2026-10-14T09:00:00Z",
  "dataStatus": "Complete",
  "metrics": [
    {}
  ],
  "series": [
    {}
  ],
  "breakdowns": [
    {}
  ],
  "availableActions": [
    {}
  ],
  "warnings": [
    "Example"
  ],
  "eligibleSampleCount": 0,
  "excludedSampleCount": 0,
  "records": [
    {}
  ],
  "detailRowCount": 0,
  "previousPeriodComparisons": [
    {}
  ]
}
```
