# Wasla Phase 18 Part Two — implementation report

Branch: `codex/phase18-part-two`. Reviewed baseline: `84e50dba497400ca11fa0a6304482a5e48b53ba8`. Scope: the full owner master task, Annexes A/B/C, stages S00–S09. No Angular implementation, production deployment or production data operations.

The implementation provides permanent identity registries, constrained same-database archive stores, observed operational days and automatic closure, bounded transactional normal/backfill movement, unified historical reads, the 38 numbered API contracts, two necessary supplemental selectors, granular authorization, audit federation and frontend handoff. Physical movement remains disabled by default. This report is implementation/test evidence; it is not production activation approval.

## Stage outcomes

| Stage | Delivered |
|---|---|
| S00 | Exact baseline, source and FK/query inventory, DEC-038 narrow relocation exception, business traceability. Initial documentation commit `c837380`. Missing independent sources explicitly recorded. |
| S01 | Immutable registry/archive/domain models, constrained copies, four controlled migrations and final EF snapshot. |
| S02 | Bounded standalone registry preparation, atomic creation adoption, permanent identity/uniqueness and idempotent result lookup. |
| S03 | Effective schedule/timezone evidence, audited revisions/invalidation, immutable closed day, reservation-only closure, NoShow operational lease, late InProgress completion. |
| S04 | Feature/readiness gates, durable worker/progress/control/issues, SQL session lease and transaction locks, atomic linked movement, bounded retries, rollback and ambiguous-COMMIT recovery, telemetry. |
| S05 | Storage-aware 01–06; search/metadata/selectors 07–15 and 31–34; safe typed history/capabilities/links; scoped Patient Reservation History. |
| S06 | Health/runs/exceptions/control 16–22 and 35, version producer, current action grants, full actor/key/payload replay, readiness-checked resume. |
| S07 | Federated immutable audit, opaque authenticated event keys/cursors, scoped options 23–30/36–38, required staff-read/restricted/denied-request evidence. |
| S08 | Persistent clinical/finance/follow-up/handover/idempotency registry FKs; historical source unions and report/export/detail compatibility; current operations remain active-only. |
| S09 | SQL/domain/API/contract/security/concurrency/regression tests, nonempty migration + backup/restore + query-plan rehearsal, generated OpenAPI and operations/FE documentation. Final counts below. |

## Migrations and rollout

1. `20261014170532_Phase18PartTwoArchiveExpansion`: expansion only; no operational row movement/deletion.
2. `20261014171141_Phase18PartTwoRegistryForeignKeys`: coverage guard, trusted permanent FK cutover and active identity references.
3. `20261014173651_Phase18PartTwoAuditKeys`: random persistent event-key secret.
4. `20261014191455_Phase18PartTwoArchiveProgress`: durable cursors/watermark, NoShow operational liveness, day-evidence/replay/correlation metadata, required terminal checks/indexes, singleton control.

Use the [migration plan](part2-database-migration-plan.md) and [runbook](part2-archive-operations-runbook.md): quiesce writers → expansion only → bounded maintenance population until zero → guarded cutover/remaining migrations → compatible final executable with both flags OFF → environment verification → separately authorized activation. The final executable requires the final schema. Down intentionally refuses automatic reversal; after relocation the old dbo-only executable is incompatible. No disposal/unarchive/repair API exists.

Payments, Refunds, MedicalEncounters, diagnoses/prescriptions/diagnostics, Patient identity and their histories stay in dbo. Archive copies preserve original GUIDs, rowversion bytes, statuses and operational histories/attempts. Registries preserve lifetime Practice/day/number and one-Ticket-per-Reservation uniqueness. The NoShow status-only filtered index was adapted to an explicit operational lease so closed NoShow remains status NoShow, cannot become refundable Cancelled, and does not block legitimate later admission.

The narrow BuildingBlock addition exposes the existing ProblemDetails errors to result filters before response writing. Denied-request audit uses a fresh scope/DbContext so it cannot save tracked business mutations from a rolled-back command. No duplicate Error-to-status mapping or business transaction bypass is introduced.

## Contracts and frontend readiness

[API contracts](part2-api-contracts.md), [38-ID manifest](part2-endpoint-manifest.json), [generated OpenAPI](part2-openapi.json), [permission matrix](part2-permissions-matrix.md), and [screen wiring matrix](part2-ui-api-matrix.md) describe exact routes, fields, required/null shape, selectors, opaque cursors/event keys, errors and headers. Numbering remains 01–38: six existing extensions plus 32 new routes. Supplemental dependent-patient and Reception historical-Practice options remove unrelated permission prerequisites.

Verified flows include assignable permission GUID → assignment ID/version → explicit delegation/revocation; Practice selector → archive row → same-ID detail; Reservation → linked Ticket → independent Refund; completed Ticket → independent clinical read/amendment; monitoring IDs → details; health version → pause/replay/conflict/resume; audit keys → scoped detail; report/Patient History navigation. Operational read-only capabilities do not infer finance/clinical access. Admin 06 preserves its gate and active DTO; archived metadata is documented via oneOf, with dedicated 31 for archive-only administration. Frontend integration can use these contracts; no browser/Angular end-to-end application is claimed.

## Verification results

- Audited restore and solution build: **0 warnings/errors**. Maintenance utility restore/build: **0 warnings/errors**; actual bounded preparation run returned **0** missing identities on its owned local database.
- Whole-solution regression: **270 unit, 21 architecture, 225 integration passed; 0 failed, 0 skipped**. SQL integration duration: **20m46s**. The earlier Phase16 thousand-visit timeout passed.
- After final capability/nullability refinements: **270 unit + 21 architecture** passed again; **31** affected integration/OpenAPI/contract checks passed, including the **two additional real SQL deadlock/lock-timeout cases**. **518 distinct cases** passed across all final runs; repeated cases are not double-counted. This is not a claim of a single 518-case invocation.
- Final model: no pending changes; idempotent SQL script generation succeeded. The nonempty expand/populate/cutover/restart/CHECKSUM backup/VERIFYONLY/restore rehearsal passed.
- Last recorded synthetic backfill evidence: `Records=1000; batchSize=50; restartedCycles=20; durationSeconds=43.37; no SLA asserted`. Original history IDs and payment total **300,000 EGP** were preserved; the Practice/date/ID paging index appears in the saved SQL plan.
- Generated OpenAPI: **40 exact routes** (38 numbered + 2 supplemental), **45 reachable schemas**, Bearer security definition, required/null fields and response headers. Nullable references include an explicit null-only alternative; nested capabilities/timelines are described accurately.
- Both ar/en XLSX before/after cell parity and real ar/en PDF exports passed, together with all role-supported fixed-AsOf report metric/series/breakdown/sample-count comparisons. PDF byte equality is not claimed.


Detailed executed commands, earlier failed iterations, scenario coverage and unexercised operational gates are in [tests and evidence](part2-tests-and-evidence.md). The reviewed executable/contract snapshot is generated by tests, not manually inferred. Real PDF generation is environment-dependent and was configured explicitly with local Chrome for verification.

## Unresolved blockers and release gates

- Original Master v1.2, independent BuildingBlock-All-Code and QControl text exports were absent. Original Master §§35/73/88/91 and DEC-005/006/007/008/009/011/012 could not be independently rechecked. The owner-provided complete Part Two task and repository source/addenda guided implementation; formal owner-source incorporation remains required.
- Unverifiable/invalid operational-day evidence and inconsistent legacy payment/clinical/history/linkage remain untouched metadata integrity holds. Pre-clinical completed legacy Tickets without provable encounter applicability are not assigned fabricated encounters. Legal retention/disposal remains deferred.
- Target-environment production-volume load/locks/memory, forced network/failover/process-kill and every concurrent business race, deployment backup/PITR acceptance and independent security/clinical/finance review remain unwaived staging/release gates. Local synthetic tests do not prove them.
- Feature flags default OFF (`Archive:Enabled=false`, `Archive:ReadModelsVerified=false`); no production activation has occurred. Data Protection keys must be securely persisted/shared across instances. Scope/current grants are rechecked server-side, including revocation and the live-to-archive read race.

## Local commits

- `c837380`: initial S00 dependency inventory and DEC-038 change control.
- `7436ab77607797c76bc6739158df97cf19bf8cbf`: runtime, four migrations, maintenance utility, 38-ID manifest and verification tests for S01–S09.
- The following documentation commit contains this report, final OpenAPI, matrices/runbook, exact test counts/hashes and formal addendum clarification. No remote push, pull request or deployment was requested/performed.

## Changed files relative to reviewed baseline

117 files, including required generated migration designers/model snapshot and the intentional OpenAPI handoff artifact. Local TRX, synthetic plans/backups, exports and temporary generation scripts are not committed.

- `Directory.Packages.props`
- `SOURCE-OF-TRUTH-PHASE-18-PART-TWO-ADDENDUM.md`
- `docs/phase18/part2-api-contracts.md`
- `docs/phase18/part2-archive-operations-runbook.md`
- `docs/phase18/part2-audit-source-catalog.md`
- `docs/phase18/part2-business-rule-traceability.md`
- `docs/phase18/part2-codex-implementation-report.md`
- `docs/phase18/part2-database-migration-plan.md`
- `docs/phase18/part2-dependency-inventory.md`
- `docs/phase18/part2-domain-state-machines.md`
- `docs/phase18/part2-endpoint-manifest.json`
- `docs/phase18/part2-openapi.json`
- `docs/phase18/part2-permissions-matrix.md`
- `docs/phase18/part2-tests-and-evidence.md`
- `docs/phase18/part2-ui-api-matrix.md`
- `src/BuildingBlock/BuildingBlock.Api/ProblemDetails/BuildingBlockProblemActionResult.cs`
- `src/Wasla/Wasla.Api/Controllers/Phase18PartTwoController.cs`
- `src/Wasla/Wasla.Api/Controllers/ReservationsController.cs`
- `src/Wasla/Wasla.Api/Controllers/TicketsController.cs`
- `src/Wasla/Wasla.Api/OpenApi/ArchiveContractFilters.cs`
- `src/Wasla/Wasla.Api/Program.cs`
- `src/Wasla/Wasla.Api/Security/ArchiveDeniedRequestAttribute.cs`
- `src/Wasla/Wasla.Application/DependencyInjection.cs`
- `src/Wasla/Wasla.Application/Features/Archives/ArchiveContracts.cs`
- `src/Wasla/Wasla.Application/Features/Archives/ArchiveLabels.cs`
- `src/Wasla/Wasla.Application/Features/Archives/ArchiveLocation.cs`
- `src/Wasla/Wasla.Application/Features/Archives/ArchiveTicketReplay.cs`
- `src/Wasla/Wasla.Application/Features/Finance/FinancialMutations.cs`
- `src/Wasla/Wasla.Application/Features/Practices/ReceptionManagement.cs`
- `src/Wasla/Wasla.Application/Features/Reporting/ReportingContracts.cs`
- `src/Wasla/Wasla.Application/Features/Reservations/ReservationArchiveReplay.cs`
- `src/Wasla/Wasla.Application/Features/Reservations/ReservationManagement.cs`
- `src/Wasla/Wasla.Application/Features/Tickets/CallNext/CallNext.cs`
- `src/Wasla/Wasla.Application/Features/Tickets/CancelTicket/CancelTicket.cs`
- `src/Wasla/Wasla.Application/Features/Tickets/CompleteTicket/CompleteTicket.cs`
- `src/Wasla/Wasla.Application/Features/Tickets/ConfirmNoResponse/ConfirmNoResponse.cs`
- `src/Wasla/Wasla.Application/Features/Tickets/CreateWalkIn/CreateWalkIn.cs`
- `src/Wasla/Wasla.Application/Features/Tickets/GetMyTicketDetails/GetMyTicketDetails.cs`
- `src/Wasla/Wasla.Application/Features/Tickets/GetTicketDetails/GetTicketDetails.cs`
- `src/Wasla/Wasla.Application/Features/Tickets/ManualCall/ManualCall.cs`
- `src/Wasla/Wasla.Application/Features/Tickets/Recall/Recall.cs`
- `src/Wasla/Wasla.Application/Features/Tickets/RestoreNoShow/RestoreNoShow.cs`
- `src/Wasla/Wasla.Application/Features/Tickets/Shared/ReservationCheckInWorkflow.cs`
- `src/Wasla/Wasla.Application/Features/Tickets/Shared/TicketContracts.cs`
- `src/Wasla/Wasla.Application/Features/Tickets/StartVisit/StartVisit.cs`
- `src/Wasla/Wasla.Application/Persistence/IWaslaDataStore.cs`
- `src/Wasla/Wasla.Domain/Archives/ArchiveIdentity.cs`
- `src/Wasla/Wasla.Domain/Archives/ArchiveOperations.cs`
- `src/Wasla/Wasla.Domain/Archives/ArchivedOperationalRecords.cs`
- `src/Wasla/Wasla.Domain/Security/Phase18PartTwoPermissionNames.cs`
- `src/Wasla/Wasla.Domain/Security/SystemSecurity.cs`
- `src/Wasla/Wasla.Domain/Tickets/Ticket.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/DependencyInjection.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Options/ArchiveOptions.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/ArchiveBackgroundService.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/ArchiveEngine.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/ArchiveIdentityService.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/ArchiveOperationalDays.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/ArchiveOperationsService.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/ArchiveQueries.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/ArchiveRequestAudit.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/ArchiveTelemetry.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/ArchiveTokens.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/AuditExplorer.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/ClinicalReadService.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Configurations/ClinicalConfigurations.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Configurations/FinancialConfigurations.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Configurations/HandoverConfigurations.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Configurations/MedicalRecordConfigurations.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Configurations/ReservationConfigurations.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Configurations/TicketConfigurations.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/FinanceReadService.Revenue.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/FinanceReadService.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/HandoverService.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/HiddenAccessReader.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/MedicalRecordReadService.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Migrations/20261014170532_Phase18PartTwoArchiveExpansion.Designer.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Migrations/20261014170532_Phase18PartTwoArchiveExpansion.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Migrations/20261014171141_Phase18PartTwoRegistryForeignKeys.Designer.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Migrations/20261014171141_Phase18PartTwoRegistryForeignKeys.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Migrations/20261014173651_Phase18PartTwoAuditKeys.Designer.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Migrations/20261014173651_Phase18PartTwoAuditKeys.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Migrations/20261014191455_Phase18PartTwoArchiveProgress.Designer.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Migrations/20261014191455_Phase18PartTwoArchiveProgress.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Migrations/WaslaDbContextModelSnapshot.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/ReportingQueryService.Archives.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/ReportingQueryService.CapacityDrilldowns.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/ReportingQueryService.Cohorts.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/ReportingQueryService.Operations.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/ReportingQueryService.PatientDrilldowns.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/ReportingQueryService.Platform.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/ReportingQueryService.PlatformCohorts.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/ReportingQueryService.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/ReservationExpirationBackgroundService.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/ReservationPublicReaders.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/TicketOperationalDayBackgroundService.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/TicketQueueReader.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/WaslaDataStore.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/WaslaDbContext.ArchiveGovernance.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/WaslaDbContext.Archives.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/WaslaDbContext.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/WaslaSecuritySeeder.cs`
- `tests/Wasla.Tests.Integration/Phase11TicketPersistenceTests.cs`
- `tests/Wasla.Tests.Integration/Phase12ArchiveTestServices.cs`
- `tests/Wasla.Tests.Integration/Phase12TicketRefundProjectionTests.cs`
- `tests/Wasla.Tests.Integration/Phase12WalkInPaymentTests.cs`
- `tests/Wasla.Tests.Integration/Phase16FamilyHardeningTests.cs`
- `tests/Wasla.Tests.Integration/Phase16HardeningTests.cs`
- `tests/Wasla.Tests.Integration/Phase18PartTwoApiTests.cs`
- `tests/Wasla.Tests.Integration/Phase18PartTwoContractTests.cs`
- `tests/Wasla.Tests.Integration/Phase18PartTwoMigrationTests.cs`
- `tests/Wasla.Tests.Integration/Phase18PartTwoSqlTests.cs`
- `tests/Wasla.Tests.Integration/SqlLegacyMigrationRehearsal.cs`
- `tests/Wasla.Tests.Unit/Phase18PartTwoDomainTests.cs`
- `tests/Wasla.Tests.Unit/Phase18ReportingFoundationTests.cs`
- `tools/Wasla.ArchiveMaintenance/Program.cs`
- `tools/Wasla.ArchiveMaintenance/Wasla.ArchiveMaintenance.csproj`
