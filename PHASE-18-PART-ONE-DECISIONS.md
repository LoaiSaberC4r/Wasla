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

Baseline build: `dotnet build Wasla.sln --no-restore`, succeeded with 0 warnings and 0 errors. Test evidence is captured under ignored `_local-data/phase18/baseline`; final outcomes will be recorded in the implementation report.
