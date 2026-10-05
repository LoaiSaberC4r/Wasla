# Phase 14 closure report — 2026-10-05

**Status: DONE.** Branch: `codex/phase14-drug-catalog-prescriptions`. Baseline: `main` / `origin/main` at `5ec70b6`. Implementation is local on the new branch; no production database or external account was modified.

## 1. Implemented behavior

Dedicated catalog, immutable catalog history, offline import staging/reconciliation, profile-free catalog manager governance, Doctor medication requests and review, separate prescription roots/versions/items, Request + Add, implicit incomplete initial drafts, atomic visit finalization, completed-visit corrections/discard/void, immutable snapshots, scoped Doctor/Patient reads, SQL constraints/indexes/locks, localized errors, Swagger and PHI redaction. Existing Phase 13 notes, diagnoses, amendments, own-record access and follow-up transactions are preserved.

## 2. Files added/changed

55 files: 29 added and 26 changed. The complete path inventory is below. Domain logic is in `Wasla.Domain/Medications`; CQRS/contracts/access/import parsing are in `Wasla.Application/Features/Medications`; persistence/read/import/configurations are in the SQL Server infrastructure. Clinical/Ticket completion and read contracts, deterministic security, manager authentication/governance, localization, logging and DI are extended. Six Phase 14 test files and four Phase 14 documents are added; README is updated after final verification.

## 3. Migration

`20261005133417_Phase14PrescriptionsAndDrugCatalog` adds DrugCatalogs, DrugCatalogHistories, DrugCatalogImportBatches, DrugCatalogImportRecords, DrugCatalogRequests, DrugCatalogRequestHistories, Prescriptions, PrescriptionVersions, PrescriptionItems, PrescriptionAuditEvents and MedicationIdempotencyRecords, with new indexes/checks/restrict relationships. Up is additive; Down removes the new schema only. No source medicines or historical prescriptions are seeded.

`dotnet ef migrations has-pending-model-changes --project src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer --startup-project src/Wasla/Wasla.Api --no-build` reported no model changes. An idempotent SQL script from Phase 13 to Phase 14 was generated and inspected: 11 new CREATE TABLE statements, new indexes and foreign keys, no destructive Up operation. Authenticated SQL Server fixtures apply the migration to isolated disposable databases. Production migration remains a deployment operation.

## 4. Roles and permissions

New deterministic role/type: DrugCatalogManager, ID `10000000-0000-0000-0000-000000000005`, UserType 5. Twenty-five permissions are appended without renumbering existing permissions: manager ten, Doctor eight, Patient one, Root governance six. Exact names and mappings are in `SOURCE-OF-TRUTH-PHASE-14-ADDENDUM.md`. First-login password change is enforced. The manager role cannot acquire clinical permissions, even through Root role editing.

## 5. Endpoints and contracts

44 new routes: Root accounts 6, Doctor active search 1, catalog administration 7, imports 5, Doctor requests 4, manager requests 5, initial draft items 3, correction 7, void 1, Doctor reads 3, Patient reads 2. All routes, bodies, enum/token conventions and response DTOs are listed in `PHASE-14-API-CHANGES.md`.

Existing Encounter details/by-Ticket now embed prescription state, completion blockers and capabilities. Ticket details expose nullable PrescriptionId. Existing Complete Visit accepts optional PrescriptionRowVersion, required for an existing draft, and finalizes in its existing transaction. Start Visit remains unchanged. Swagger publishes response schemas, multipart upload, newMedication and completion concurrency fields. There is no catalog DELETE, separate initial-create/finalize route, arbitrary patient-ID prescription route or Reception prescription route.

## 6. Import behavior and full-source evidence

Authenticated offline JSON upload validates schema/limits, calculates file SHA-256, persists a reviewable batch and paged changes, then applies with idempotency and one transaction. It handles new/unchanged/price-only/NeedsReview/missing/possible-duplicate/exact-duplicate rows, preserves reviewed clinical data/manual prices, retains source identities separately from business IDs and never automatically merges, deletes or deactivates missing data. Runtime search is SQL-ranked/paged and has no GitHub dependency.

A supplemental one-off authenticated API smoke test used the full offline MedicianDB snapshot against an isolated SQL Server database:

| Evidence | Observed result |
| --- | --- |
| File size / rows | 8,021,909 bytes / 25,070 rows |
| SHA-256 | `351130D918CB901F38FDA88B47A41B044F16A241CC51C34D30097E672518E645` |
| Staged records | 25,070 |
| Exact duplicate rows skipped | 4 |
| Persisted catalog rows | 25,066 |
| Active / NeedsReview / Inactive | 22,409 / 2,303 / 354 |
| Preview / apply elapsed | 16.884 seconds / 48.783 seconds |
| Active `panadol` search | 13 matching results |
| Repeat apply | Success; no duplicate catalog set |

The 2,303 NeedsReview rows are also included in NewRecords, so those counters are not additive categories. These timings are local SQL Express smoke evidence, not a production benchmark. The one-off test passed once and its temporary harness was removed; neither the dataset nor an external-fixture dependency is committed to the repository. Permanent import tests use deterministic offline fixtures.

## 7. Prescription lifecycle

First add creates Draft; incomplete input is saved and blockers are returned. Catalog snapshots and DoctorSubmitted requests are supported in one endpoint. Removing the final initial item removes the empty root/draft; a visit can finish without a prescription. Required strength/form/route/dose, frequency/custom text, fixed/ongoing duration, PRN reason/minimum interval, optional quantity/unit and instructions are enforced at the appropriate draft/finalize boundaries.

Complete Visit validates all prescription and Phase 13 blockers before mutating, finalizes the initial version and commits Ticket/Encounter/follow-up state together. Correction clones the current version into a fresh draft, repeated start resumes it, the patient keeps seeing the prior current output, finalize supersedes atomically, discard preserves the current version, and void preserves a clearly Voided current record. Empty correction drafts need explicit discard. Finalized/Superseded/Voided content is immutable; later catalog/request changes do not rewrite snapshots.

## 8. Security/scoping

Root-only manager account governance, manager medication-only access, active/not-first-login accounts, approved owning Doctor and active Practice, own requests and account-linked own Patient prescriptions are enforced. Claimed and current database permissions are both required. Permission revocation changes capability flags and rejects subsequent edits even with an older valid token. Another Doctor/Patient cannot inspect the resource. Reception, ordinary SuperAdmin and catalog managers cannot access clinical prescriptions. Patient outputs exclude drafts, superseded versions, managerial/correction reasons, request/source IDs, audit and prices. Existing family and follow-up booking rules grant no wider clinical visibility.

## 9. Concurrency/idempotency

Root rowversions advance on child changes. SQL application locks coordinate initial creation, completion, catalog selection/merge, correction and import. Unique/filter/check constraints guard one root, draft/current uniqueness, linear versions, exclusive item source, quantity and duration. Idempotency is scoped to actor + operation + key; payload reuse conflicts, matching retry creates no duplicate work, prescription retries return the latest authorized state/token, and failed transactions retain no success key. Complete Visit fingerprints include the prescription token.

Live SQL tests prove concurrent initial adds have one winner, correction start resumes one draft, concurrent finalize has one winner, stale completion returns 409, repeated import creates one set, and forced completion/correction failures roll back lifecycle, audit and idempotency. The correction rollback test specifically fails after the prior current row is superseded and verifies the original Finalized plus Draft state survives for retry.

## 10. Tests and exact counts

Final command (SQL Server connection provided through the fixture environment variable):

```powershell
$env:WASLA_SQLSERVER_CONNECTION_STRING = 'Server=.\SQLEXPRESS;Database=master;Integrated Security=true;TrustServerCertificate=true'
dotnet test Wasla.sln --no-restore --verbosity quiet --logger 'trx;LogFileName=phase14-final.trx'
```

| Suite | Existing cases | Added Phase 14 cases | Final passed | Failed | Skipped |
| --- | ---: | ---: | ---: | ---: | ---: |
| Unit | 102 | 28 | 130 | 0 | 0 |
| Integration/API | 95 | 21 | 116 | 0 | 0 |
| Architecture | 12 | 4 | 16 | 0 | 0 |
| **Total** | **209** | **53** | **262** | **0** | **0** |

The integration additions comprise 14 authenticated workflow cases across SQLite/SQL Server, five dedicated SQL Server constraint/concurrency/rollback tests and two OpenAPI/localization tests. The full suite includes all Phase 13 regressions. The supplemental full-source smoke above is one additional successful one-off test, excluded from the permanent suite totals. TRX files are in the ignored per-project TestResults directories.

## 11. Build

`dotnet build Wasla.sln --no-restore --verbosity quiet`: succeeded, **0 warnings, 0 errors**. `git diff --check` passed. The final full regression run includes the current capability-revocation and latest-state retry checks.

## 12. Unresolved blockers

None for the approved backend Phase 14 scope. Production deployment, manager provisioning and reviewing/applying the real catalog remain operational steps. The full dataset was imported only into a disposable test database. No frontend application exists in this repository; frontend lifecycle expectations are exercised through authenticated API call sequences and documented contracts.

## 13. Adaptations/deviations

No approved business rule was changed. Technical adaptations are explicit: immutable audit root/version IDs are metadata without foreign keys so approved transient draft deletion retains audit; item checks permit incomplete draft fields while finalization applies the stricter rules; an additional idempotency table implements durable retries; DELETE tokens use query parameters; PUT wraps catalog/request data plus token and returns the existing full-state convention. These preserve the requested lifecycle and architecture.

The pre-existing ReceptionWalkInOptions fixture failed during baseline teardown because its hosted service was stopped twice. It now removes hosted services like the existing Phase 13 fixture, isolating its API tests without changing production hosting. TrustAccess expected deterministic role count changes from four to five. No unrelated production refactor, pharmacy/dispensing, finance change, online sync, notification or sharing policy was added.

## 14. Definition of Done

Phase 14 can honestly be marked **DONE**: all requested catalog/manager/import/request/prescription/versioning/security/concurrency constraints and contracts are implemented; localization and Swagger are verified; 262 permanent tests and the full-source smoke pass; build has zero warnings/errors; the additive migration has no pending model changes; the source-of-truth addendum/API/impact/closure documents are present. README is updated only after this verification.

## Complete file inventory

- `PHASE-14-API-CHANGES.md`
- `PHASE-14-CLOSURE-REPORT.md`
- `PHASE-14-IMPACT-PLAN.md`
- `README.md`
- `SOURCE-OF-TRUTH-PHASE-14-ADDENDUM.md`
- `src/Wasla/Wasla.Api/Controllers/ClinicalControllers.cs`
- `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs`
- `src/Wasla/Wasla.Api/Controllers/TicketsController.cs`
- `src/Wasla/Wasla.Api/Program.cs`
- `src/Wasla/Wasla.Application/DependencyInjection.cs`
- `src/Wasla/Wasla.Application/Features/Auth/Authentication.cs`
- `src/Wasla/Wasla.Application/Features/Clinical/ClinicalContracts.cs`
- `src/Wasla/Wasla.Application/Features/Clinical/ClinicalMutations.cs`
- `src/Wasla/Wasla.Application/Features/Clinical/ClinicalQueries.cs`
- `src/Wasla/Wasla.Application/Features/Governance/DrugCatalogManagers.cs`
- `src/Wasla/Wasla.Application/Features/Governance/Governance.cs`
- `src/Wasla/Wasla.Application/Features/Medications/DrugCatalogHandlers.cs`
- `src/Wasla/Wasla.Application/Features/Medications/DrugImportHandlers.cs`
- `src/Wasla/Wasla.Application/Features/Medications/DrugImportParser.cs`
- `src/Wasla/Wasla.Application/Features/Medications/MedicationAccess.cs`
- `src/Wasla/Wasla.Application/Features/Medications/MedicationContracts.cs`
- `src/Wasla/Wasla.Application/Features/Medications/PrescriptionHandlers.cs`
- `src/Wasla/Wasla.Application/Features/Tickets/CompleteTicket/CompleteTicket.cs`
- `src/Wasla/Wasla.Application/Features/Tickets/Shared/TicketContracts.cs`
- `src/Wasla/Wasla.Domain/Clinical/MedicalEncounter.cs`
- `src/Wasla/Wasla.Domain/Common/Enums.cs`
- `src/Wasla/Wasla.Domain/Medications/DrugCatalog.cs`
- `src/Wasla/Wasla.Domain/Medications/DrugCatalogImport.cs`
- `src/Wasla/Wasla.Domain/Medications/DrugCatalogRequest.cs`
- `src/Wasla/Wasla.Domain/Medications/MedicationErrors.cs`
- `src/Wasla/Wasla.Domain/Medications/MedicationIdempotencyRecord.cs`
- `src/Wasla/Wasla.Domain/Medications/Prescription.cs`
- `src/Wasla/Wasla.Domain/Resources/ErrorMessage.ar.resx`
- `src/Wasla/Wasla.Domain/Resources/ErrorMessage.resx`
- `src/Wasla/Wasla.Domain/Security/SystemSecurity.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/DependencyInjection.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/ClinicalPersistenceErrorMapper.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/ClinicalReadService.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Configurations/MedicationConfigurations.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/DrugCatalogImportService.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/MedicationReadService.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Migrations/20261005133417_Phase14PrescriptionsAndDrugCatalog.Designer.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Migrations/20261005133417_Phase14PrescriptionsAndDrugCatalog.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Migrations/WaslaDbContextModelSnapshot.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/TicketQueueReader.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/WaslaDbContext.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/WaslaSecuritySeeder.cs`
- `tests/Wasla.Tests.Architecture/Phase14MedicationArchitectureTests.cs`
- `tests/Wasla.Tests.Integration/Phase14ContractTests.cs`
- `tests/Wasla.Tests.Integration/Phase14LifecycleApiTests.cs`
- `tests/Wasla.Tests.Integration/Phase14MedicationApiTests.cs`
- `tests/Wasla.Tests.Integration/Phase14SqlServerTests.cs`
- `tests/Wasla.Tests.Integration/ReceptionWalkInOptionsTests.cs`
- `tests/Wasla.Tests.Integration/TrustAccessApiTests.cs`
- `tests/Wasla.Tests.Unit/Phase14MedicationDomainTests.cs`
