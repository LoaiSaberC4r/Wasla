# Reporting reconciliation and source quality

Reports are authoritative reads, not a second mutable financial ledger. SQL multi-query assembly uses a serializable transaction and explicit AsOfUtc. Operational source adapters reconstruct event state/scheduled-time changes from immutable histories; unavailable old state is excluded/labeled. Reports do not infer missing historical events from current flags. Financial roots, latest-known revisions, allocation snapshots and immutable movements are read at the same recorded-knowledge cutoff.

## Required reconciliation properties

1. Sum all authorized Practice allocation shares to the whole-Doctor expense once; do not duplicate a shared cost. Practice-filtered movement lists and signed financial drilldown contributions match report totals to the piastre.
2. AllOutstandingAsOf includes liabilities outside the selected due window; OutstandingDueInPeriod does not. A backdated payment recorded later is absent from an earlier AsOf report. Cash movement and due-date trends answer distinct questions.
3. BookingsCreated uses creation date, AppointmentsScheduled uses latest effective scheduled date at AsOf. CancellationsByEvent and reschedule events count actual events; rescheduled-reservation counts are distinct IDs. Walk-ins never inflate reservation conversion.
4. Ratio/average drilldowns expose actual numerator/denominator contributions and source IDs/exclusion reasons. Totals recompute true denominators rather than average Practice percentages. Metric action URLs retain filters and AsOfUtc.
5. Capacity uses union of actual available intervals after exceptions and intersections with booked/service intervals. Policy snapshot contribution IDs are returned for audit. Minutes, quota and configured maximum headcount have separate units and completeness.
6. Patient cohorts are distinct global PatientIds in same-Doctor scope. New/returning is based on earliest known same-Doctor check-in; retention uses the preceding90-day cohort and selected Practices. No cross-Doctor clinical lookup occurs.
7. Platform patient/clinical cohorts below five suppress value, numerator, denominator, samples and complementary series/breakdowns. No platform patient identity drilldown exists. Missing login/page-view/search instrumentation is NotInstrumented.

## Policy capture and reconciliation job

`WaslaDbContext.ReportingSnapshots` appends an immutable policy when actual Practice/configuration/schedule/exception/segment/visit-type changes commit. SQL per-Practice locks sequence revisions. The policy stores zone, active state, working intervals, exceptions, maximum daily patients and segment/visit-type status/quota. It does not claim a date earlier than actual observation.

`ReportingPolicyReconciler` starts after database initialization. Default interval300 seconds; validated30–86400. A clean unit of work processes batches≤100 Practice IDs; unchanged JSON produces no new snapshot. ReportingRefreshRun persists start/finish/status/error code, progress and snapshot counts. Process interruption leaves honest Running evidence; later runs reconcile idempotently and job health becomes Stale when overdue. Failure retries at the next pass; no fake successful state is returned. Dashboard GETs do not create snapshots.

Past days without an anchor snapshot are NotMeasured. Intraday quota/limit changes without a consumption snapshot stay NotMeasured. Current/future policy capacity is labeled a forecast at the latest AsOf policy. The job cannot reconstruct legacy policy history or silently resolve a discrepancy by rewriting business facts.

## Operator investigation

Compare report window/zone/AsOfUtc first, then eligible/excluded counts, source events, immutable finance revisions/allocations and generation/refresh run metadata. Query current source details only through normal scoped routes. Correct an unpaid obligation via the explicit versioned command; reverse a paid movement before re-settlement. Never update report history or old receipts with raw SQL. Generation uniqueness, settlement uniqueness and database triggers are the final invariants.

Validation: `Phase18ReconciliationTests` executes same-total allocation/drilldown checks, equal-window comparison, current underlying permission revocation, platform suppression and raw SQL immutability/overlap constraints. `Phase18DoctorReportingApiTests` verifies old monetary revisions at historical AsOf and subsequent settlement effects. Unit analytics tests verify event/cohort separation, true denominators and invalid timestamp exclusion. Full regression evidence is in the implementation report.

Representative compile-only SQL Server plan inspection on a freshly migrated disposable schema used Index Seek for each of: Doctor/status/due-date obligations, Practice/status handovers, and status/priority investigations. The inspected indexes were `IX_OperatingObligations_DoctorId_Status_DueBusinessDate`, `IX_ReceptionHandovers_DoctorPracticeId_Status_CreatedOnUtc`, and `IX_AdministrativeInvestigations_Status_Priority_CreatedOnUtc`. Three plans, three seeks and no Table Scan were observed. The empty-schema estimates do not establish production latency/load capacity. Plan XML and summary are in ignored `_local-data/phase18/query-plans`; the temporary database was removed after inspection.
