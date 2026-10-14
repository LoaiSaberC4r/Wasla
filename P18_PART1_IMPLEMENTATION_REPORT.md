# Phase 18 Part One implementation report

Branch: `codex/phase18-part-one`. Reviewed baseline: `f91348fc64441523f5910b70260e26523d63a101`. The existing intermediate user commit `b0c3d7e` is preserved. This report covers the approved backend PartOne delivery and supporting integration documentation; production release remains separately blocked.

## Implementation and traceability

The [business rule matrix](docs/phase18/part1-business-rule-traceability.md) explicitly maps all113 D.01–D.48, R.01–R.22, S.01–S.35 and P.01–P.08 IDs to IMPLEMENTED, REUSED, EXTENDED or DEFERRED with code/test evidence. S.30 is unassigned in the approved task and deliberately has no invented feature. Missing historical sources and disabled privacy gates are explicit unavailable states, not fabricated implementations. [Business scope](PHASE-18-PART-ONE-BUSINESS-SCOPE.md) and [the proposed master addendum](SOURCE-OF-TRUTH-PHASE-18-PART-ONE-ADDENDUM.md) distinguish existing rules, approved additions and excluded work.

The typed API surface includes 109 NEW endpoints and 108 REUSED existing endpoint contracts. There are 0 EXTENDED existing wire contracts; supporting services and capability/delegation behavior are extended as identified in the rule matrix. The delivery adds 39 permissions, 15 allow-listed report definitions and 96 KPI definitions. Catalog entries publish concrete preview/drilldown/export routes and independent preview/export capability. The [API reference](PHASE-18-PART-ONE-API-CHANGES.md), [UI acceptance matrix](PHASE-18-PART-ONE-UI-API-MATRIX.md), generated [OpenAPI](docs/phase18/part1-openapi.json), DTO tables and [reused API inventory](docs/phase18/part1-reused-api-contracts.md) describe required inputs, nullability, enum codes, version/idempotency behavior, errors, samples and frontend states. Stable P18 endpoint IDs are preserved by the generator.

| Area | Concrete implementation |
|---|---|
| Reporting foundations | Application ReportingContracts/TimeScope/KpiDefinitionRegistry/OperationalKpiCalculator; persistence current-DB ReportingAccessGuard and ReportingQueryService partials |
| Outgoing finance | OperatingFinance domain roots/revisions/allocations/history, transactional service, SQL constraints/triggers and restart-safe generation; separate from Phase12 incoming ledger |
| Doctor | Creation/schedule/event cohorts, queue intervals/capacity, patient cohorts/comparisons, historical finance and persisted insight follow-up/evidence |
| Reception | Scoped worklist/attention, existing operation routes, operational activity/reports/export and versioned same-Practice handovers |
| Platform | Actual governance/acquisition/activity/cohort/geography/security metadata, conservative suppression, measured quality/job/runtime health and explicit investigation state machine |
| Patient | Application self composer reusing approved handlers/access policies, small recent lists and explicit safe capabilities |
| Exports | Shared model → embedded trusted Razor/installed sandboxed Chromium PDF or typed ClosedXML workbook; row/date/byte/runtime bounds, release-time authorization and immutable scope audit |
| Persistence | Eight additive migrations, normalized restrictive ownership links, optimistic versions, append-only evidence, monetary/occurrence/settlement/reversal invariants and current-policy reconciliation |

No original appsettings key/value was changed. No production database, deployment, push/merge, credential rotation, notification expansion, queue rule, clinical-sharing gate or PartTwo retention/archive behavior was changed. Logs additionally redact financial/operational note and snapshot payload names.

## Validation evidence

Previously executed regression runs pass. The final narrow guard run also completed successfully. These are saved results from runs started before the owner's latest instruction to test only remaining work. Closure after that instruction changed documentation only: no further test suite was launched and no completed feature was reimplemented. Production release remains subject to the stated external blockers.

| Check | Executed outcome |
|---|---|
| Reviewed baseline build | 0 warnings / 0 errors |
| Reviewed baseline unit / architecture | 223 / 21 passed |
| Exact baseline SQL integration rehearsal | 183 passed,0 skipped,4m37s; isolated test databases |
| Current Phase18 unit/export suite | 33 passed,0 skipped; includes real Chromium Arabic/English PDFs and25000-row/empty XLSX |
| Final current full unit | 256 passed,0 skipped,21 seconds; real Chromium configured |
| Final architecture | 21 passed,0 skipped,3 seconds |
| Full integration with SQL Server enabled | 210 passed,0 skipped,13m49s; includes existing Phase10–16 regression and27 Phase18 cases |
| Migration regression corrections | Down path preserves immutable history and dependent FK/index; legacy visibility rehearsal selects the last pre-Phase16 migration even when later phases exist; both roundtrips pass |
| Final narrow review guards | 10 passed, 0 skipped; saved scope/forecast/worklist/contract results in `phase18-final-guards.trx` |
| Model drift | No pending model changes at the last executed check; subsequent refinements changed queries/action hints, with no EF model changes |
| Configuration preservation | Both original JSON files: identical keys/values and normalized source bytes; current hashes saved without revealing contents |

Synthetic artifact QA:600 detail rows,20 A4 pages per Arabic/English PDF, repeated table headers and page numbering, readable first/middle/last pages, encoded script-like reference, selected local PNG percentage chart and prior-period comparison. XLSX reopened with ClosedXML and independently inspected using openpyxl:7 worksheets,600 rows, correct dark heading fill/white font, Arabic RTL, typed date/currency, literal leading-zero/formula-like values, chart picture and typed50% relative comparison. Actual PDF sizes approximately1.15MiB/1.13MiB. QA artifacts live under ignored `_local-data/phase18/export-qa`, contain only synthetic data and are not production records.

Phase18ContractTests reads actual ApiExplorer/Swagger routes, typed response metadata, required Idempotency-Key headers and PDF binary schema. Contract/doc generation reads metadata only. `scripts/phase18/generate-contract-docs.py` and `generate-rule-matrix.py` reproduce documentation from test output. Behavioral coverage is journey/invariant coverage, not a false claim that every read alias has an independent end-to-end case.

The saved final TRX files are under ignored `_local-data/phase18/validation/final`. Equivalent commands to reproduce those completed runs are recorded below; this is not an instruction to rerun all suites for a documentation change. SQL integration requires the test-only `WASLA_SQLSERVER_CONNECTION_STRING` supplied through the environment. PDF unit coverage used the installed Chrome executable via `WASLA_CHROMIUM_PATH`. Contract output used `WASLA_CONTRACT_OUTPUT_DIRECTORY`.

```powershell
dotnet test tests/Wasla.Tests.Unit --no-restore --logger 'trx;LogFileName=phase18-unit-final.trx' --results-directory _local-data/phase18/validation/final
dotnet test tests/Wasla.Tests.Architecture --no-restore --logger 'trx;LogFileName=phase18-architecture-final.trx' --results-directory _local-data/phase18/validation/final
dotnet test tests/Wasla.Tests.Integration --no-restore --logger 'trx;LogFileName=phase18-integration-final.trx' --results-directory _local-data/phase18/validation/final
dotnet test tests/Wasla.Tests.Integration --no-restore --filter 'FullyQualifiedName~Phase18Reconciliation|FullyQualifiedName~Phase18Reception|FullyQualifiedName~Phase18Contract' --logger 'trx;LogFileName=phase18-final-guards.trx' --results-directory _local-data/phase18/validation/final
```

Documentation closure regenerates the captured contract and rule inventories, checks stable unique endpoint IDs and local document links, and verifies Git whitespace/configuration preservation. It does not query a database, launch the application, or execute tests.

## Practical limits and release status

Historical capacity/quota/current-governance state cannot be recreated where old sources were not captured. Login/page-view/search security/adoption telemetry remains NotInstrumented. Current public availability is observed projection data and is labeled Partial; no clinician performance score or exact patient wait prediction is offered. Operational-note validation is preventive and cannot replace operator policy. Source queries use no tracking, split/aggregated reads, a bounded date window, scope predicates and policy anchors; large production cardinalities still require environment-specific load rehearsal.

Production release remains **BLOCKED** by the P0 exposed-configuration-credential remediation and the independent legal/privacy/runtime approvals documented in [external release blockers](P18_PART1_EXTERNAL_RELEASE_BLOCKERS.md). The [rollout guide](P18_PART1_ROLLOUT.md) specifies migration/worker/runtime verification and preservation-first rollback. Implementation completion does not waive these release conditions.
