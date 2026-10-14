# Phase 18 Part One business scope

Approved implementation authority: the owner's Phase18 Part One task dated2026-10-13, read with the supplied Master Source of Truth v1.2 file. Baseline `f91348fc64441523f5910b70260e26523d63a101`; work branch `codex/phase18-part-one`. The master file's historical header saysv1.1; its actual supplied filename and SHA256 are recorded in the decisions document. This delivery adds backend contracts and frontend integration instructions; it creates no Angular application.

| Actor | Delivered business capability | Existing behavior reused |
|---|---|---|
| Doctor | Owned-Practice operational reports, patient cohorts, comparisons, rule-based insight follow-up, salaries, expenses, recurring bills, full settlements/reversals, handover oversight, PDF/XLSX | Phase12 collections/revenue, booking/queue/clinical/family authorization and existing receipts |
| Reception | Explicitly delegated operational dashboards/reports/exports, daily worklist, attention and handover workflow | Reservations, patient search, check-in/walk-in, queue actions, patient payment collection and receipt commands |
| SuperAdmin | Lifecycle/adoption/geography/operations/security metadata, measured job/data quality, pending attention, bounded administrative investigations and exports | Existing Doctor/specialization/user/role governance; founder protection |
| Patient | Small self-only dashboard and capability navigation | Existing self reservations/tickets/medical/financial/family policies and attachment reauthorization |

The complete UI progression is overview → options → filtered list/report → authorized details/drilldown → permitted action → refreshed detail/history → refreshed report → export. The [UI matrix](PHASE-18-PART-ONE-UI-API-MATRIX.md) and [API changes](PHASE-18-PART-ONE-API-CHANGES.md) identify the contracts and mandatory UI states. Reports carry actual scope, applied filters, business date basis, AsOfUtc, quality, samples and warnings. Empty complete cohorts may count zero; missing history/instrumentation is explicitly unavailable.

Operating finance records the Doctor's own outgoing liabilities and cash records, separately from patient collections. EGP amounts use exact piastres, percentages use at most four decimal places, allocations reconcile without double counting, settlement is full only, reversals compensate rather than overwrite, and generation never transfers money. Salary does not establish employment/payroll entitlements.

Historical reporting uses immutable source events, allocation/term revisions and policy snapshots. It cannot recreate schedule/quota/account history that was never recorded. New policy capture begins with actual observed policies; no migration backdates today's settings. Current public eligibility, observed availability and historic approval are distinct facts. No missing login/page-view/search telemetry is fabricated.

Privacy remains the existing platform policy: same-Doctor ownership, exact Reception delegation, self-only Patient identity, clinical permissions separately checked and patient/clinical platform groups below five suppressed with complementary suppression. Family membership is not a clinical entitlement. No existing disabled cross-Doctor or guardian gate is enabled by this phase.

Excluded from this delivery: Part Two archives/retention/purge/Audit Explorer, subscription/commission, notification expansion, payroll taxes/advances/deductions/proration/partial payments, bank integrations, shifts or invented SLAs, clinician scoring, AI clinical recommendations and rejected-search demand. Legal/privacy approvals and exposed-credential remediation remain [external release blockers](P18_PART1_EXTERNAL_RELEASE_BLOCKERS.md).
