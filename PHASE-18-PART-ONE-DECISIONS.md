# Phase 18 Part One decisions

Implementation started 2026-10-13 on `codex/phase18-part-one` from `f91348fc64441523f5910b70260e26523d63a101`.

The owner-approved task is the Phase 18 Part One addendum. The locally available `Healthcare-Platform-Master-Source-of-Truth-v1.2-Updated.txt` in the owner's Downloads directory supplies the existing domain/architecture authority (its historical header still says v1.1). The embedded blueprint is available. Existing Phase 10–16 closure reports and source remain the compatibility baseline. No material conflict identified in the reviewed ownership, queue, finance, privacy, and reporting rules. The older combined Phase 18 archive/report scope is split by this approved task; archive and retention remain Part Two.

## Implementation decisions

- Keep patient collections and outgoing operating finance in separate aggregates and contracts.
- Use authoritative reads and immutable business histories rather than invent historical projections. Return explicit data quality for absent capacity, quota, or usage instrumentation.
- Preserve existing permission IDs by appending new permissions. Reception reporting requires explicit delegation; no automatic assignment backfill.
- Use integer EGP piastres and deterministic largest-remainder allocations, with GUID ordering for ties.
- Financial plan revisions apply prospectively; obligations and financial movements keep their original snapshots.
- Full settlements and compensating reversals are atomic, permissioned, versioned, and idempotent. No banking side effects.
- Generation creates an eligible current calendar occurrence at the start of its eligible cycle, with its configured due date retained. This supports Upcoming/Due/Overdue and projected liabilities. Future calendar cycles are not generated. System generation is identified separately from the Doctor who authorized the plan; no salary is automatically paid.
- Reuse existing booking, queue, collections, clinical, family, and governance commands.
- One report calculation model supplies preview, PDF, and XLSX. Adapt the CustomerSurvey RazorLight/PuppeteerSharp/ClosedXML pattern while preserving browser sandboxing and blocking remote resources.
- No configuration values, production databases, deployments, Jira records, or earlier privacy gates are changed.

## Package sequence

0. Baseline, source review, existing API inventory, integration mapping.
1. Reporting contracts, scope authorization, KPI definitions, time and quality.
2. Outgoing finance, immutable allocation/history, scheduled generation, concurrency.
3. Doctor reports and insight tracking.
4. Reception operations, worklist, handovers.
5. Platform reporting, investigations, job/data quality.
6. Patient self composition and capabilities.
7. Shared PDF/XLSX export pipeline and runtime checks.
8. SQL Server migration rehearsal, regression, security, documentation and closure.

Baseline build: `dotnet build Wasla.sln --no-restore`, succeeded with 0 warnings and 0 errors. Test evidence is captured under ignored `_local-data/phase18/baseline`; final outcomes are recorded in [the implementation report](P18_PART1_IMPLEMENTATION_REPORT.md).

## Source and reference evidence

Master source: `C:\Users\p\Downloads\Healthcare-Platform-Master-Source-of-Truth-v1.2-Updated.txt`, SHA256 `C7DDC4C68130EA8056F81A172E570AE4F1364C3154D28F2E4DC6263411CFF553`. The source's embedded blueprint and the repository's approved Phase10–16 reports were reviewed. No source was silently replaced. The baseline was also restored from the exact commit in an ignored isolated directory and its SQL Server regression suite was executed against temporary test databases.

CustomerSurvey reporting reference reviewed locally in `E:\Customer Servey` and from `NileTronixRepositories/CustomerSurvey`: report controllers/contracts, data assembly, Razor views, PDF service, Excel service, charts, dependency configuration and relevant tests/styles. Adapted the trusted embedded RazorLight template → installed Chromium/PuppeteerSharp and ClosedXML workbook pattern. Rejected reference browser flags that disable the sandbox/web security, first-request browser downloads, remote resource loading and unsafe unbounded exports. Wasla's authorization, source model, immutable financial data and bilingual presentation remain Wasla-specific.

Packages pinned centrally: ClosedXML0.105.1, PuppeteerSharp25.10.0, RazorLight2.3.1. Chromium is provisioned separately and its absolute executable path is an environment/secret-provider override. No appsettings value was edited. Razor compile references are preserved; templates are embedded resources.

## Additional implementation closure

- All report definitions are allow-listed and use one typed ReportModel for preview/export. Exact current schemas and generated endpoint IDs are checked in; report groupings and filters are catalog capabilities, not arbitrary SQL expressions.
- Date windows are inclusive labeled dates, maximum366 days. Operational events use source Practice-zone snapshots; financial movements use stored effective business dates and recorded UTC knowledge. Historical AsOfUtc does not retroactively include later-entered backdated movements.
- Capacity minutes use effective time unions. MaximumDailyPatients is a separate headcount constraint and metric; it is never converted into invented schedule minutes. Intraday quota/limit uncertainty is NotMeasured. Missing historical policy makes capacity unavailable rather than copying today's schedule.
- Booked/unused capacity and actual service utilization use union intersections. Current/future policy capacity is labeled a forecast. Clinical completion remains separate from operational Ticket.Completed.
- Insight comparison uses two complete seven-day Practice-local windows, minimum20 valid records in both and ≥25% deterioration; calculated identity is deterministic and only follow-up creates a persisted business record.
- Initial handover acknowledgement requires a different actor from the creator. Doctor direct close needs an operational outcome. Closed handovers are terminal; a follow-up is a new linked handover.
- Investigations may be withdrawn directly from Open with reason. Reopening a withdrawn unassigned case can provide an active SuperAdmin assignee and requires Assign plus Reopen permission. Reopening never erases previous resolution/history.
- All new mutations use the existing transactional command pipeline and scoped idempotency locks; finance additionally serializes per Doctor. SQL restricts immutable histories and roots, salary overlap, ownership, allocations, full settlement and reversals. Unsupported direct SQL must not bypass these rules.
- PDF and Excel are synchronous bounded downloads; heavy work has an export row/date/byte budget. PDF has a timeout and concurrency limit. Browser failure is controlled503 and reported by actual runtime health. No new outbox message is emitted because no approved external notification is part of these workflows.
- Reporting policy reconciliation runs after database initialization, scans in batches, records actual run progress/failure, captures only observed current policy and retries on the next cycle. No dashboard GET writes a business task or schedule policy.
- Export audit contains actor, role, exact scope/permissions/window, filters, format/language, outcome and byte count; no rendered content, clinical text, browser path or secrets. Permission/delegation is rechecked before releasing bytes.
- The supplied task defines S.01–S.29 and S.31–S.35; S.30 is unassigned and is not a silently invented feature. Numbered traceability within task ranges is documented as an implementation mapping.
