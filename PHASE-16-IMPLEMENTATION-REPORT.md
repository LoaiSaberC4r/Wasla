# Phase 16 implementation and verification report

Status: **backend implemented for the 23 non-family routes; sharing release gated;
6 family routes inactive/policy blocked. Phase 16 is NOT marked DONE.**

Date: 2026-10-12 (Africa/Cairo). Branch: `codex/phase16-medical-record`.
Initial clean HEAD exactly matched the supplied reviewed baseline
`563e2111182f584986f225a3711a459a00e3b913`. Current HEAD contains the local ordered
changes below. No GitHub push, PR, deployment, production migration/seed, Jira
change or external message was performed. No formal privacy/legal approval is
claimed. The authoritative full v1.2 master is not present in this checkout.

## Route delivery and frontend review

[PHASE-16-API-CHANGES.md](PHASE-16-API-CHANGES.md) enumerates every P16-01–29 and
EXT-01–03 individually with HTTP route, required permission, response template,
status, enum/cursor/idempotency/error behavior and all seven frontend journeys.

- P16-01–06 and P16-22–23: IMPLEMENTED (patient-safe own record and owner visibility).
- P16-07–21: GATED, with working handlers (live treating gateway, scoped requests,
  global owner inbox/decisions/history). CrossDoctorSharingEnabled defaults false.
- P16-24–29: GATED / BLOCKED, with safe inactive routes. No active dependent
  enumeration/guardian clinical policy is shipped, even if the config flag is true.
- EXT-01–03: IMPLEMENTED, additive completion/owner detail/list contract.

Patient-safe and DoctorShared response types are distinct from private owner
DTOs. Hidden is a separate union with exact date-only safe fields, not a large
DTO with nulled clinical properties. Current Rx/diagnostic outputs resolve through
the new gateway, not foreign owner-only APIs. The item safeContent variants and
hidden/clinical timeline union are described in OpenAPI.

A–F frontend journeys have backend integration coverage: self record, both live
ticket origins, hidden request/approval/rejection/revocation/expiry, global owner
inbox including two owned practices, atomic chosen visibility, typed item/media
navigation and current capabilities. G is deliberately disabled; no age/guardian
policy approval or active child-record workflow is claimed.

## Ordered local packages

| Package | Commit | Review intent |
|---|---|---|
| P16.0 | `3d7a43e` | Proposed governance addendum and baseline impact inventory |
| A | `2283668` | Visibility, conservative legacy migration, atomic completion and owner contracts |
| B | `d7b63e4` | Request lifecycle, scope/permissions/token foundations, audit schema and repeatable request tests |
| C | `849db2f` | Safe unified patient/current-output projections, cursor paging, typed contracts, media and patient API |
| D | `183becc` | Gated doctor gateway and authorization/lifecycle/privacy tests |
| E | `97568b3` | Inactive family contract with explicit policy gate |
| F | final local commit | Hardening, SQL races/plans/migration rehearsal, complete docs and recorded verification |

Package B was additionally exported from its own Git commit and built independently:
its API build passed with zero warnings/errors. EF Designer/snapshot changes are
scaffolded metadata; the meaningful code changes remain separated by package.

## Code impact / exact file locations

Paths below are repository-relative under `E:\Wasla`; the final changed-file
inventory is in `_local-data/phase16-validation/changed-files.txt`.

Domain:
- `src/Wasla/Wasla.Domain/Clinical/MedicalEncounter.cs`
- `src/Wasla/Wasla.Domain/Clinical/MedicalRecordEntities.cs`
- `src/Wasla/Wasla.Domain/Clinical/HiddenEncounterAccessRequest.cs`
- `src/Wasla/Wasla.Domain/Security/SystemSecurity.cs`
- `src/Wasla/Wasla.Domain/Resources/ErrorMessage.resx` and `ErrorMessage.ar.resx`

Application:
- `src/Wasla/Wasla.Application/Features/Clinical/{EncounterVisibility,HiddenAccessContracts,HiddenAccessHandlers,MedicalRecordAccessPolicy,MedicalRecordContracts,MedicalRecordHandlers,MedicalRecordTelemetry}.cs`
- Existing `ClinicalAccessService.cs`, `ClinicalContracts.cs`, `ClinicalMutations.cs`
- `Features/Tickets/CompleteTicket/CompleteTicket.cs` and `CancelTicket/CancelTicket.cs`
- Existing `DependencyInjection.cs`

SQL Server infrastructure:
- `Persistence/{MedicalRecordReadService,MedicalRecordReadService.Diagnostics,MedicalRecordAuditWriter,HiddenAccessReader,EncounterVisibilityReader,WaslaDbContext.MedicalRecords}.cs`
- Existing `ClinicalReadService.cs`, `ClinicalPersistenceErrorMapper.cs`, `WaslaDbContext.cs`, `WaslaSecuritySeeder.cs`
- `Persistence/Configurations/{ClinicalConfigurations,MedicalRecordConfigurations}.cs`
- Existing infrastructure `DependencyInjection.cs`
- Three migration files + Designers + `WaslaDbContextModelSnapshot.cs`

API:
- `Controllers/{PatientMedicalRecordController,TreatingMedicalRecordController,DependentMedicalRecordController,MedicalRecordDependentsController,HiddenAccessControllers,EncounterVisibilityController}.cs`
- Existing `ClinicalControllers.cs`, `TicketsController.cs`, `Program.cs`, `appsettings.json`
- `OpenApi/MedicalRecordSchemaFilter.cs`

Tests:
- Unit: `Phase16VisibilityTests.cs`, `Phase16HiddenAccessTests.cs`
- Integration: `Phase16{VisibilityApiTests,HiddenAccessApiTests,Fixture,PatientApiTests,MedicalRecordApiTests,PolicyApiTests,HardeningTests,ContractTests,PerformanceTests}.cs`
- Architecture: `Phase16MedicalRecordArchitectureTests.cs`

No second clinical-record aggregate/table, duplicated mutable Rx/diagnostic
business logic, public storage URL, automatic identity merge or medical-center
privilege was introduced. Own doctor workflows remain in original source modules.
The owning clinical access helper now explicitly verifies Doctor UserType, matching
existing medication/diagnostic actor conventions. Source records also verify their
original completed ticket/patient/doctor/practice relationship before disclosure.

## Persistence, permissions and configuration

Migrations, baseline/backfill, mixed-version strategy, restrictive FKs, cleanup,
rollback/recovery and configuration prerequisites are documented in
[PHASE-16-ROLLOUT.md](PHASE-16-ROLLOUT.md).

- `20261012101718_Phase16EncounterVisibility`: old rows Hidden, baseline history,
  explicit new Visible inserts, rowversion/revision and visibility constraints.
- `20261012102626_Phase16HiddenEncounterAccess`: all-status unique source/ticket,
  restrictive relationship FKs, SQL rowversion, immutable transition history.
- `20261012103620_Phase16MedicalRecordAudits`: metadata-only read audit + indexes.

New permission strings (append-only identities):
MedicalRecords.ViewOwn, MedicalRecords.ViewTreatingPatient,
MedicalRecords.ViewDependent, MedicalRecords.ViewOwnAccessLog,
MedicalEncounterVisibility.UpdateOwn, MedicalEncounterVisibility.ViewHistoryOwn,
HiddenEncounterAccessRequests.Create, HiddenEncounterAccessRequests.ViewSent,
HiddenEncounterAccessRequests.ViewIncoming, HiddenEncounterAccessRequests.DecideOwn,
HiddenEncounterAccessRequests.RevokeOwn, HiddenEncounterAccessRequests.ViewHistoryOwn.
Doctor receives treating/visibility/request permissions; Patient receives own,
access-log and dependent (inactive) permissions; no default clinical grants are
added to Reception/SuperAdmin/catalog roles. Seeding remains repeatable and is
explicitly tested without duplicate permissions/mappings.

Both production sharing flags default false. Stable shared Data Protection keys
and a managed OpaqueEntryKey are additional operational prerequisites. Source
visibility updates and decisions coordinate through transaction locks; completion
and cancellation acquire the treatment lock before loading mutable ticket state.
Completion expires requests in the same transaction. Every new read evaluates
current permissions and effective scope independently of persisted cleanup.
Default-visible completion keeps the pre-Phase 16 idempotency fingerprint format;
a seeded legacy key replay is tested, including changed Hidden-payload conflict.

## Actual verification

Latest results are recorded after the final code/test changes; no mock result is
substituted for SQL Server. Test connection is integrated-security LocalDB, with
isolated disposable fixture databases. It is not a production connection.

| Command | Actual result |
|---|---|
| `dotnet restore Wasla.sln` | SUCCESS; all projects up to date |
| `dotnet build Wasla.sln --configuration Release --no-restore -v quiet` | SUCCESS; 0 warnings, 0 errors |
| `dotnet test Wasla.sln --configuration Release --no-build -v quiet --logger trx --results-directory _local-data/phase16-validation/verified-results` | SUCCESS; **413 passed, 0 failed, 0 skipped**: 215 unit + 177 integration + 21 architecture; integration duration 4m36s |
| SQL environment | `WASLA_SQLSERVER_CONNECTION_STRING=Server=(localdb)\MSSQLLocalDB;Database=master;Integrated Security=true;TrustServerCertificate=true;Connect Timeout=60`; real SQL migrations, two-host races, private media, audit failure, legacy rehearsal and plan/volume test ran |
| Standalone package B API build from Git archive | SUCCESS; 0 warnings, 0 errors |
| `dotnet restore BuildingBlock.sln` | SUCCESS with existing NU1903 advisory warnings |
| `dotnet test BuildingBlock.sln --no-restore -v quiet --logger trx` | **316 passed, 7 failed, 0 skipped**; baseline failures explained below |
| Pristine reviewed-main archive, the seven failing BuildingBlock cases | Same **7 failures** reproduced; source/config unchanged by Phase 16 |
| `git diff --check` | SUCCESS; no whitespace errors |

The earlier full Debug Wasla run passed all 409 then-present tests. An intermediate
Release run failed one unsupported shared-connection SQLite overlap and four
LocalDB login/availability cases under overlapping test processes. SQLite media
checks now run sequentially while the real SQL Server cases retain their races;
the final isolated Release run above used a 60-second LocalDB connection timeout
and passed every scenario. No assertion was relaxed to accept a clinical 500 or
unauthorized disclosure. This report uses the final run, not the failed attempt.


Local evidence files (ignored development artifacts):
- `_local-data/phase16-validation/verified-results/*.trx`
- `_local-data/phase16-validation/buildingblock-results/*.trx`
- `_local-data/phase16-validation/timeline-plan.xml`
- `_local-data/phase16-validation/performance.txt`
- `_local-data/phase16-validation/validation-summary.txt`

Coverage includes domain transitions/terminal states, current DB permission and
account status, patient/ticket/encounter identity mismatch, both ticket origins,
foreign InProgress source denial, exact hidden JSON, actor/ticket/purpose/time/
revision-bound tokens, request uniqueness/replay, stale rowversions, visibility
revocation, official late results/media, original owner-route IDOR denial,
current Rx/diagnoses safe projections, cursor invalidation, metadata audit failure,
repeatable permissions, disabled guardian route denial, endpoint/OpenAPI inventory,
legacy migration rehearsal and architecture isolation.

SQL race verification uses two separate app hosts/DB scopes for duplicate create
and approval versus completion, with shared Data Protection identity. Official
media/revocation races execute on real SQL Server. SQLite covers sequential
functional lifecycle only because its fixture shares one in-memory connection.
There is no security dependency on a background expiration task or read cache.

The synthetic SQL check uses 1,000 completed encounters, 500 foreign Hidden
placeholders, ten pages of 100 without duplicate/missing entries, a bounded
translated UNION projection and a saved SQL query plan. It checks that no internal
notes are projected and records actual first-page latency in performance.txt.
This is a local synthetic history check, not production capacity certification.

## Existing baseline issues and release blockers

Standalone BuildingBlock tests: 316 passed, 7 failed, 0 skipped. All seven were
reproduced against a pristine archive of reviewed main `563e211...`:
- Five public-API baseline tests assume projects at repository root instead of
  `src/BuildingBlock`, so PublicAPI.Shipped.txt lookup fails.
- SDK policy test expects latestPatch while unchanged global.json uses latestFeature.
- Query-filter test scans the whole monorepo and reports existing Wasla usages.
No BuildingBlock source, central package file or global.json was changed to mask
these baseline failures. They are separate engineering debt; the suite is not
reported green.

BuildingBlock restore emitted NU1903 for existing SSH.NET 2025.1.0. The primary
[SCP path handling advisory](https://github.com/advisories/GHSA-mggc-4xg6-vcxf) and
[recursive SCP download advisory](https://github.com/advisories/GHSA-q939-rpr3-3284)
list it as affected. No dependency upgrade/suppression is included in this task.

Formal master adoption, privacy/legal/clinical consent sign-off, Phase 5 identity
claim/dedup closure, hidden critical-alert safety policy and audit retention/
patient disclosure policy: **UNAPPROVED / RELEASE BLOCKED**. Guardian legal policy:
**UNAPPROVED / implementation inactive**. Remote GitHub CI: **NOT RUN** (no push).
Production migrations/seeds/deployment/flag activation: **NOT PERFORMED**.

The new access-log currently displays Phase 16 read audits; it is not a historical
export of every preexisting source-module audit. Older patient diagnosis DTOs
still expose their preexisting Notes contract; new patient/shared projections
exclude it. Changing/classifying that established contract requires compatibility
and privacy review. No older endpoint was silently reclassified as a shared API.

Production-scale load and recovery/retention verification, formal governance,
active guardian policy wiring/age-boundary scenarios and baseline BuildingBlock
harness/dependency repairs remain outstanding. No Phase 16 DONE/closure claim is
made, and no production release approval is inferred from passing local tests.
