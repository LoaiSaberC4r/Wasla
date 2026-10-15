# Phase 18 Part Two — verification evidence

Environment: local Windows, .NET SDK 10, SQL Server Express `.\SQLEXPRESS`, synthetic disposable databases, local Google Chrome. All migrations, backups and restores were limited to disposable synthetic local databases; no production data or deployment was used. Test result files and synthetic plans remain in ignored `_local-data/phase18-part2/`.

## Baseline and failed iterations

Baseline: `84e50dba497400ca11fa0a6304482a5e48b53ba8`. The first audited restore identified transitive System.Security.Cryptography.Pkcs advisories; the central transitive pin is now 10.0.13, with no permanent audit suppression. Baseline unit 256 and architecture 21 passed. Baseline real-SQL integration: 209 passed, 1 failed, zero skipped; the Phase16 thousand-visit performance test timed out under contention.

The first whole-solution implementation regression passed unit 270 and architecture 21, with integration 210 passed / 13 failed / zero skipped. Causes: EF runtime models omit check-constraint metadata (fixed by querying IDesignTimeModel); the former NoShow status-only unique-index assertion (updated to assert the approved operational lease and its check); incomplete hand-built Phase12 DI fixtures; and two earlier migration tests attempting to reverse the non-reversible archive migration. The latter now create nonempty fresh legacy copies, migrate forward, and verify their original assertions. These failures are retained in the local TRX evidence.

Focused iterations additionally caught an incorrect assignment-response test assumption, SQL Server's OUTPUT/trigger restriction on rowversion checkpoint tables, and normal/backfill cursor rotation in the fault test. The two checkpoint writes use transaction-scoped EF command interception; the other thirteen movement points use real SQL triggers. No production fault-injection hook or trigger compatibility change was introduced. The additional SQL retry harness was corrected to observe exceptions arriving during row reads and to delegate provider-specific typed reads; failed fixture iterations remain in local TRX files. The final harness uses independent SQL transactions for a real deadlock and lock timeout, with safe numeric error evidence and deterministic lock release.

## Executable scenarios

| Test class | Evidence |
|---|---|
| Phase18PartTwoDomainTests | All six Ticket statuses; five Reservation statuses; observed/revised/closed day invariants; no fabricated historical snapshot; append-only permission list/default restrictions |
| Phase18PartTwoApiTests | Module-only selectors; exact permission GUID → assignment ID/version → grant/revoke; existing assignments never backfilled; cross-Practice concealment; Patient subject override denied; redacted platform metadata; health/version/idempotency/conflicts; restricted grant + reason; opaque stable audit keys; real SQL archive detail navigation |
| Phase18PartTwoContractTests | All 38 numbered routes, two supplemental selectors, typed schemas, required/null fields, control header, no-store/Retry-After, administrative oneOf; generates OpenAPI handoff |
| Phase18PartTwoSqlTests | Linked copy/delete/history/attempt/pointer/audit/count/sequence failure rollback (15 points); original booking/check-in/cancel replay; full refund exactly once; missing-FK/index/check readiness rejection; two workers; equal-clock pagination watermark; tamper/filter/revocation; clinical continuity and amendment; live-to-archive authorization race; closed NoShow finality and new admission; missing-payment quarantine; lost COMMIT acknowledgement |
| Phase18PartTwoSqlTests reservation-only scenario | Effective multi-period end, before-close revision, DayOff invalidation/recovery, no Ticket requirement, cancellation/expiry relocation, immutable closed evidence, inactive owned Practice read, Patient History and platform detail navigation |
| Phase18PartTwoMigrationTests | Nonempty old-schema copy; expansion; premature cutover rejected with SQL 51000; 10 bounded registry batches; repeated application; 1,000 relocations in 20 fresh-context batches of 50; original IDs/history/payment totals; indexed paging query plan; synthetic CHECKSUM backup/VERIFYONLY/restore |
| Existing Phase11–18 tests | Full regression covers admission/payment, queue, bookings, clinical/visibility/official outputs, family entitlement, operating finance, audit, reporting/exports, security and architecture |

The fixed synthetic relocation dataset compares every role-supported report definition for Doctor, Reception and platform at a common historical AsOfUtc: metrics, series, breakdowns, data status and sample/detail counts. Queue XLSX cells in ar/en are compared before/after, excluding only generated-time metadata. Real ar/en PDF exports are requested before/after when WASLA_CHROMIUM_PATH is configured; these verify generation and PDF signatures, while source-value parity is asserted through the report model/XLSX. Byte-for-byte PDF equality is not claimed. Existing HTML/XLSX/PDF renderer tests also run with real Chrome.

## Commands and final results

Final audited restore/build: **0 warnings, 0 errors**. Maintenance utility restore/build: **0 warnings, 0 errors**, and the owned local preparation database returned **0** missing identities. EF reports **no pending model changes**; the full idempotent SQL script generated successfully.

The frozen whole-solution regression passed **270 unit + 21 architecture + 225 integration = 516**, zero failures/skips. Subsequent capability/schema refinements were covered by repeated unit/architecture checks and **31** final integration/contract checks, including **two added real SQL retry cases**. There are **518 distinct passed cases** across the union; counts from repeated runs are not added together. The earlier Phase16 thousand-visit timeout passed in this full regression.

Real retry proof uses two independent connections: a high-priority synthetic blocker owns the Payment lock, and the worker's low-priority transaction forms the Ticket/Payment lock cycle. SQL chooses the worker as victim (1205), which retries and commits once. A separate case observes two real lock-timeout errors (1222), releases the owned blocker, and commits the third attempt. Both preserve one physical location and zero false integrity issues. Tests alter only session settings and locks in disposable synthetic databases.

| Result file in `_local-data/phase18-part2/final-results` | Passed | Failed | Skipped | SHA-256 |
|---|---|---|---|---|
| `part2-regression-verified_net10.0_20261015111009.trx` | 21 | 0 | 0 | `2efa7917c37e7e7050b703aeee8d58ef36eb0aaa9a4a061c58789407a15599e9` |
| `part2-regression-verified_net10.0_20261015111024.trx` | 270 | 0 | 0 | `e4f6aba031ca3329ddb2de2f6024a6fc05bdb8e75fc7eefff1f76658347522a2` |
| `part2-regression-verified_net10.0_20261015113053.trx` | 225 | 0 | 0 | `4bd34ddc5b266b33859a2e5a5afdf68005f7d898742a4073952c2c716bfeb954` |
| `part2-final-unit.trx` | 270 | 0 | 0 | `fa8a80a2fe5c9b90ab795db6b7c268f37657b4f4ccbdb0449cda74d33b399409` |
| `part2-final-architecture.trx` | 21 | 0 | 0 | `3a949349edf968e22b044c0c2bb3628d1b55a7f3ce098799fdba6d6f9c48fe90` |
| `part2-release-acceptance.trx` | 31 | 0 | 0 | `8ea9167357be9f9121ef429b15a049c245ea62e4e2e7a94f45fca1f0663098bc` |

Run commands from repository root:

```powershell
dotnet restore Wasla.sln
dotnet build Wasla.sln --no-restore
'{"parallelizeAssembly":false,"parallelizeTestCollections":true,"maxParallelThreads":2}' | Set-Content -Encoding utf8NoBOM tests/Wasla.Tests.Integration/bin/Debug/net10.0/xunit.runner.json
$env:WASLA_SQLSERVER_CONNECTION_STRING='Server=.\SQLEXPRESS;Database=master;Trusted_Connection=True;TrustServerCertificate=True'
$env:WASLA_CHROMIUM_PATH='C:\Program Files\Google\Chrome\Application\chrome.exe'
$env:WASLA_CONTRACT_OUTPUT_DIRECTORY='E:\Wasla\_local-data\phase18-part2\contracts'
$env:WASLA_PART2_EVIDENCE_DIRECTORY='E:\Wasla\_local-data\phase18-part2\evidence'
dotnet test Wasla.sln --no-build --no-restore --logger 'trx;LogFilePrefix=part2-regression-verified' --results-directory _local-data/phase18-part2/final-results
# Final affected-contract/SQL acceptance after the small capability/schema refinements:
dotnet test tests/Wasla.Tests.Integration/Wasla.Tests.Integration.csproj --no-build --no-restore --filter 'FullyQualifiedName~Phase18PartTwo|FullyQualifiedName~OpenAPI|FullyQualifiedName~OpenApi|FullyQualifiedName~Contract' --logger 'trx;LogFileName=part2-release-acceptance.trx' --results-directory _local-data/phase18-part2/final-results
dotnet ef migrations has-pending-model-changes --project src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer --startup-project src/Wasla/Wasla.Api --no-build
dotnet build tools/Wasla.ArchiveMaintenance/Wasla.ArchiveMaintenance.csproj --no-restore
```

For this resource-constrained host, a generated, uncommitted xUnit configuration limits the integration assembly to two parallel test collections. This changes scheduling only; it excludes no tests. TRX counts are the executed authority. No SQLite result is presented as proof of SQL Server locks, foreign keys or migration behavior.

## Limits and release gates

The synthetic 1,000-record rehearsal is a measured local exercise, not an SLA or target-environment capacity approval. Fresh DbContexts and a lost-ack interceptor exercise restart/checkpoint behavior; they do not prove real network partition, SQL failover, operating-system process kill or PITR recovery. Production-sized lock duration/memory/load, SQL contention across each business race, simultaneous admission/EOD/refund/clinical amendment, and deployment-environment recovery acceptance need a separate staging exercise. The local application-lock race and existing SQL concurrency regressions are useful evidence, not substitutes for those gates.

The original Master v1.2 and separate BuildingBlock/QControl exports were absent; exact unrechecked sections/DECs are in the dependency inventory. Owner-source incorporation, target-environment validation, shared protected Data Protection keys, independent security review and clinical/finance acceptance remain release gates. Feature flags stay OFF. Records lacking trustworthy day/clinical/financial evidence are held with metadata exceptions; no automatic repair or invented history is accepted.
