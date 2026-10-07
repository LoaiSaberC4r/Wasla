# Phase 15 closure report — 2026-10-07

**Status: DONE.** Branch: `codex/phase15-diagnostic-orders-results`. Baseline: clean `main` at `2181b40b54d0884fe1362563423b184f6c5b174d`. Implementation and documentation are local branch changes. No production database migration, deployment, terminology download, push or pull request was performed.

## 1. Implementation summary

MedicalCatalogManager governance, distinct Lab and Radiology reference catalogs and import/review workflows, missing-catalog requests, encounter drafts and post-visit orders, atomic Complete Visit publication, scoped cancellation, Patient submissions, requesting-Doctor review, document-first official results, one-step corrections and voids, immutable snapshots/history, private medical downloads and localized API contracts are implemented. Existing encounter, prescription, queue, financial and follow-up behavior is preserved and covered by the full regression suite.

The approved task and resolved intake decisions are retained in [the Source-of-Truth addendum](SOURCE-OF-TRUTH-PHASE-15-ADDENDUM.md). [The API contract](PHASE-15-API-CHANGES.md) contains all routes, request/response fields, pagination, permissions, capabilities, concurrency tokens, multipart formats, error codes and frontend retry rules.

## 2. Important domain decisions

- Lab and Radiology have separate catalog, request, submission, result, version, coverage, attachment, history and import entities. Neither is embedded in Ticket or financial aggregates.
- A Patient upload is PendingReview and cannot choose coverage, create an official result or complete any item. Only the same requesting, approved and active Doctor can accept or directly record a report. Suspension grants no alternate reviewer.
- A document can cover several items. Official content and coverage are immutable; correction creates a new finalized version and supersedes the previous version atomically. Void reopens uncovered items without reopening the completed encounter.
- Catalog ID XOR DoctorSubmitted catalog request is mandatory. Snapshots never change after catalog edits, merges or request review. Missing terminology can be requested and ordered in one transaction without delaying care.
- Each encounter has at most one draft per domain. Complete Visit requires each existing draft's rowversion and publishes both domains with prescription, encounter, ticket and follow-up changes in the same transaction. Post-visit orders require an internal reason and preserve the original encounter.
- Cancellation affects Requested items only and records a Patient-visible reason. Completed items remain; pending submissions become NoLongerApplicable when no Requested items remain.
- Patients see their own issued orders/current valid official results immediately, without internal reasons, reference-review IDs or audit data. They retain their original submitted documents through separate submission routes; a superseded/voided official attachment is not downloadable as a current official result.

## 3. Files and modules

**69 files: 45 added and 24 changed.** The complete path inventory is at the end of this report.

New domain code is in `src/Wasla/Wasla.Domain/Labs`, `Radiology` and `Diagnostics`. New application code is in `src/Wasla/Wasla.Application/Features/Diagnostics`, plus `Features/Governance/MedicalCatalogManagers.cs`. It includes contracts, access checks, capability policy, order/result handlers, queries, validators, offline LOINC parsing and transaction-aware private-file compensation.

API modules are `LabControllers.cs`, `RadiologyControllers.cs` and `MedicalCatalogManagersController.cs`. SQL infrastructure adds explicit entity configurations, diagnostic reads/imports, immutable-content guards and the migration. Existing authentication/governance, deterministic security/seeder, clinical read/completion contracts, Ticket completion, DI, PHI redaction and Arabic/English resources are extended. Database initialization defaults now disable migration and seeding outside Development; Development enables both explicitly.

Eight Phase 15 test files are added: three unit, four integration and one architecture file. The existing integration fixture receives an isolated temporary private-media directory; its cleanup only removes that fixture-owned directory. The existing role-count assertion is updated from five to six for the added role. Required documents, the terminology notice and README are included. The complete path inventory follows this report.

## 4. Migration and deployment

Migration: `20261007072127_Phase15DiagnosticOrdersAndResults`.

Up adds **34 tables** (17 per domain), foreign keys, rowversions, filtered unique indexes, scope/state indexes and source/status/post-visit checks. It does not alter or delete existing clinical data and contains no terminology dataset. Down removes the new schema only. An idempotent script from Phase 14 to Phase 15 was generated and reviewed: **34 CREATE TABLE statements; zero DROP TABLE, DROP COLUMN, DELETE, TRUNCATE or ALTER COLUMN operations in Up**. EF reports no pending model changes. Disposable SQL Server fixtures apply the actual migration.

Production migration remains a controlled deployment operation. Apply the reviewed migration through the existing deployment process, then run the idempotent security seeder in a controlled configuration to add the deterministic role/permissions and default mappings. Migration SQL alone does not seed roles. Supply bootstrap credentials through the existing secure configuration mechanism, and restore production startup initialization flags to false after controlled initialization. No production initialization was run here.

## 5. Endpoints

**118 new routes:** Root manager accounts 6; Lab 56; Radiology 56. Each diagnostic domain has 18 manager reference/import/review routes, 28 Doctor catalog/order/result/submission routes and 10 own-Patient routes. The exact permission, response and idempotency requirement for every route is in the API document.

Existing Doctor encounter details/by-Ticket embed domain drafts, order summaries and capabilities. Existing Complete Visit accepts Lab/Radiology rowversions and includes them in its idempotency fingerprint. Existing Patient encounter DTO shape, Start Visit and prescription correction behavior are preserved. There is no Reception clinical route, catalog DELETE or Patient coverage-selection contract.

## 6. Permissions

MedicalCatalogManager is UserType 6, role ID `10000000-0000-0000-0000-000000000006`. Root alone governs its accounts; first-login password change is enforced. **62 distinct permissions** are appended without renumbering existing deterministic permission IDs: manager defaults 20, Doctor 28, Patient 10 and Root governance 6, with two ViewOwn submission permissions shared between Doctor and Patient. The manager has reference authority only, including no medication-manager authority or clinical override. Exact names are listed in the API contract and security registry.

## 7. LOINC import

Import is authenticated and offline, initially accepting release **2.83**. Header-based strict UTF-8 RFC 4180 parsing reads canonical LoincTable/Loinc.csv, common Lab orders, optional partial Arabic and grouped LOINC/RSNA Playbook rows. Lab includes CLASSTYPE 1 and ORDER_OBS Order/Both. Non-active external rows are retained but unselectable. Radiology uses canonical LoincNumber and actual PartTypeName values; original PartNumber, sequence and all supplied Part fields are retained. Attribute snapshot extraction accepts the official `Rad.Modality.Modality type` casing.

Official source fields, copyright columns and source version remain distinct from Wasla display names, aliases and notes. Missing official Arabic stays null. Common Lab orders rank first in SQL-paged search. Merged names/codes resolve to active canonical rows without changing historical clinical snapshots.

ZIP checks reject traversal, duplicate/ambiguous paths, symlinks, unsupported schemas/encoding and duplicate source identities. Default limits: 200 MiB compressed, 2 GiB declared total expansion, 1 GiB per entry, 4,096 entries, 500,000 rows per CSV and a 250 compression ratio ceiling for large entries. Actual entry reads are bounded. Packages are never extracted to Patient media.

Preview persists an immutable reviewable batch/rows with SHA-256 and comparison dispositions. Apply revalidates its staged comparison under catalog locks, commits atomically with rowversion/idempotency and preserves local presentation/lifecycle decisions. It never automatically merges, deletes or deactivates codes absent from a later release. PossibleConflict blocks apply unless explicitly skipped; retained conflict rows remain reviewable. Source/license documentation is in [LOINC-SOURCE-NOTICE.md](LOINC-SOURCE-NOTICE.md).

Permanent unit/API/SQL tests use small deterministic synthetic packages. An authenticated import of the entire licensed 2.83 release and release-scale performance measurement were **not run**, because no full source ZIP was supplied or committed. No full-release benchmark is claimed.

## 8. Security, concurrency and idempotency

Every operation checks actor type, current account state, first login, token/current-database permission intersection and clinical ownership. Doctor actions require approval plus an active owned Practice; Patient actions require the account-linked own profile. Other owners receive safe 404s. Root, ordinary SuperAdmin, Reception and catalog managers have no automatic clinical override. Reads are scoped/server-paged, clinical responses use no-store, and no global clinical query cache is introduced.

Root rowversions advance on child writes. Existing transaction/application-lock infrastructure coordinates draft creation, publication, coverage, result lifecycle and catalog reconciliation. SQL uniqueness and guards protect draft/current-version/source/coverage invariants and retained immutable content. Existing Phase 14 idempotency persistence is reused with separate diagnostic operation namespaces; matching retries create no duplicate work and return latest authorized clinical state, changed payloads conflict and failed transactions retain no success key.

PDF/JPG/PNG uploads reuse BuildingBlock private storage, signature/MIME checks and configured malware-scanner chain. Limits are 10 MiB per file, 20 files and 100 MiB total; Radiology requires attachment kinds. SQL stores metadata/private references only. Every download rechecks current ownership, permissions, actor state and official version validity. Original Patient uploader provenance survives Doctor acceptance. File compensation surrounds the transaction pipeline, including failures after handler success, and checks persisted references before deleting files after an uncertain commit. If the database or storage cannot complete reconciliation, metadata-only logging preserves the original failure and calls for retained-file reconciliation; there is no claim that storage and SQL form one distributed transaction.

Mutations retain immutable actor/time/reason/before/after history. Single-record reads/downloads reuse EncounterAuditEvents with metadata only, without modifying clinical rowversions. PHI redaction is extended; file bytes, report contents, patient notes, internal clinical text and secrets are not intentionally logged. **131 new diagnostic error codes** have English and Arabic text. Existing media and shared idempotency codes keep their established names.

## 9. Commands actually run

Final build:

```powershell
dotnet build Wasla.sln --no-restore --verbosity minimal
```

Focused verification (the SQL connection targets local SQL Express; fixtures create/dispose isolated databases):

```powershell
$env:WASLA_SQLSERVER_CONNECTION_STRING = 'Server=.\SQLEXPRESS;Database=master;Integrated Security=true;TrustServerCertificate=true'
dotnet test Wasla.sln --no-build --no-restore --filter 'FullyQualifiedName~Phase15' --logger 'trx;LogFilePrefix=phase15-verified' --verbosity quiet
```

Full solution regression:

```powershell
dotnet test Wasla.sln --no-restore --logger 'trx;LogFilePrefix=phase15-complete' --verbosity quiet
```

Model drift check and reviewed script generation:

```powershell
dotnet ef migrations has-pending-model-changes --project src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer --startup-project src/Wasla/Wasla.Api --no-build
dotnet ef migrations script 20261005133417_Phase14PrescriptionsAndDrugCatalog 20261007072127_Phase15DiagnosticOrdersAndResults --idempotent --project src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer --startup-project src/Wasla/Wasla.Api --no-build --output .phase15-migration-review.sql
git diff --check
```

The temporary review script and implementation-generation helpers are removed from the deliverable. `--verbosity quiet` reduces runner noise; failures remain visible and no failing tests were suppressed.

## 10. Exact verification results

| Final verification | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Phase 15 unit | 44 | 0 | 0 |
| Phase 15 integration | 31 | 0 | 0 |
| Phase 15 architecture | 4 | 0 | 0 |
| **Phase 15 total** | **79** | **0** | **0** |
| Full solution unit | 174 | 0 | 0 |
| Full solution integration | 147 | 0 | 0 |
| Full solution architecture | 20 | 0 | 0 |
| **Full solution total** | **341** | **0** | **0** |

Build: **0 warnings, 0 errors**. EF: **no model changes since the last migration**. The full regression covers authentication, governance, permissions, encounters, Complete Visit, prescription finalization/corrections, Patient clinical access, media and idempotency. SQL-specific cases prove concurrent single winners, rollback of both diagnostics/prescription/ticket/encounter/follow-up, failed acceptance/correction/void rollback, and file cleanup when idempotency persistence fails after a successful upload handler. The same failed command/key can then succeed on retry. Catalog SQL tests cover staged apply, preserved local fields and full 1,000-character names. API tests cover scoping, suspension/revocation, patient projection, all main submission terminal states, no coverage input, private download validity, spoofed documents, merge conflicts, review resubmission and Swagger contracts.

TRX results are in each test project's ignored `TestResults` directory with prefixes `phase15-complete` and `phase15-verified`. Counts above are actual runner/TRX results; focused tests are included in the full total, not added to it.

## 11. Intentionally deferred and verification limits

V1 exclusions remain as approved: structured observations/reference ranges/abnormal flags, provider/laboratory/PACS integration, DICOM viewing/storage, external result ingestion, sharing/access delegation, substitute reviewers, notifications and wider longitudinal-record work. No requested V1 lifecycle is left as a scaffold. Production deployment/seeding, full licensed-release import/performance acceptance and deployment-specific scanner configuration remain operational work; they were not represented as verified in this branch.

## 12. Technical adaptations

- Explicit Lab/Radiology domain entities use shared technical enums, DTOs and CQRS dispatch to avoid duplicating authorization/media plumbing. They remain separate persisted clinical domains, as required.
- Existing Wasla snapshot/API naming (`NameEnSnapshot`, domain-specific Lab/Radiology request tokens) is used. Full official columns and original Radiology Part rows are preserved in source JSON plus indexed canonical identity/status fields rather than one SQL column per source header.
- Existing MedicationIdempotencyRecords and its shared stable key-error codes are reused with diagnostic operation names; metadata-only read audit uses EncounterAuditEvents. These preserve existing Wasla persistence/contracts while providing the approved behavior.
- Empty unissued roots are removed when their last item is removed. Their immutable history has no FK requiring the deleted root, so the removal evidence remains. No issued clinical content is deleted.
- Explicitly skipped import conflicts keep their original immutable PossibleConflict disposition for later review instead of rewriting the preview row to Skipped. The apply response/batch status records completion, and the catalog row remains unchanged.
- Unbounded-width normalized catalog-name indexes were omitted because a 1,000-character Unicode name exceeds SQL Server's nonclustered key limit. Canonical identity, status/source/common and clinical scope/state indexes remain; normalized filtering and bounded server paging are implemented. A live SQL test verifies the full allowed length.
- Production-default startup migration/seeding flags were corrected to false; Development opts in explicitly. This implements the task's controlled-migration rule and matches the pre-existing README promise.
- Patient source-submission files remain accessible in their own submission scope after acceptance/correction/void, while invalid official attachment routes return 404. This keeps the original Patient-owned upload distinguishable from the current official clinical document.

These adaptations preserve approved business behavior; no broader clinical authority or additional product scope was introduced. README advances to Phase 15 DONE only after this implementation, documentation and verification.

## Complete file inventory

```text
LOINC-SOURCE-NOTICE.md
PHASE-15-API-CHANGES.md
PHASE-15-CLOSURE-REPORT.md
README.md
SOURCE-OF-TRUTH-PHASE-15-ADDENDUM.md
src/Wasla/Wasla.Api/appsettings.Development.json
src/Wasla/Wasla.Api/appsettings.json
src/Wasla/Wasla.Api/Controllers/LabControllers.cs
src/Wasla/Wasla.Api/Controllers/MedicalCatalogManagersController.cs
src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs
src/Wasla/Wasla.Api/Controllers/TicketsController.cs
src/Wasla/Wasla.Api/Program.cs
src/Wasla/Wasla.Application/DependencyInjection.cs
src/Wasla/Wasla.Application/Features/Auth/Authentication.cs
src/Wasla/Wasla.Application/Features/Clinical/ClinicalContracts.cs
src/Wasla/Wasla.Application/Features/Clinical/ClinicalMutations.cs
src/Wasla/Wasla.Application/Features/Diagnostics/DiagnosticAccess.cs
src/Wasla/Wasla.Application/Features/Diagnostics/DiagnosticCapabilities.cs
src/Wasla/Wasla.Application/Features/Diagnostics/DiagnosticContracts.cs
src/Wasla/Wasla.Application/Features/Diagnostics/DiagnosticMediaCompensation.cs
src/Wasla/Wasla.Application/Features/Diagnostics/DiagnosticOrderHandler.cs
src/Wasla/Wasla.Application/Features/Diagnostics/DiagnosticQueries.cs
src/Wasla/Wasla.Application/Features/Diagnostics/DiagnosticResultHandler.cs
src/Wasla/Wasla.Application/Features/Diagnostics/DiagnosticValidators.cs
src/Wasla/Wasla.Application/Features/Diagnostics/LoincPackageParser.cs
src/Wasla/Wasla.Application/Features/Diagnostics/MedicalCatalogHandlers.cs
src/Wasla/Wasla.Application/Features/Governance/Governance.cs
src/Wasla/Wasla.Application/Features/Governance/MedicalCatalogManagers.cs
src/Wasla/Wasla.Application/Features/Tickets/CompleteTicket/CompleteTicket.cs
src/Wasla/Wasla.Domain/Clinical/MedicalEncounter.cs
src/Wasla/Wasla.Domain/Common/Enums.cs
src/Wasla/Wasla.Domain/Diagnostics/DiagnosticTypes.cs
src/Wasla/Wasla.Domain/Labs/LabCatalogImport.cs
src/Wasla/Wasla.Domain/Labs/LabCatalogRequest.cs
src/Wasla/Wasla.Domain/Labs/LabRequest.cs
src/Wasla/Wasla.Domain/Labs/LabResult.cs
src/Wasla/Wasla.Domain/Labs/LabTestCatalog.cs
src/Wasla/Wasla.Domain/Labs/PatientLabResultSubmission.cs
src/Wasla/Wasla.Domain/Radiology/PatientRadiologyResultSubmission.cs
src/Wasla/Wasla.Domain/Radiology/RadiologyCatalogImport.cs
src/Wasla/Wasla.Domain/Radiology/RadiologyCatalogRequest.cs
src/Wasla/Wasla.Domain/Radiology/RadiologyProcedureCatalog.cs
src/Wasla/Wasla.Domain/Radiology/RadiologyRequest.cs
src/Wasla/Wasla.Domain/Radiology/RadiologyResult.cs
src/Wasla/Wasla.Domain/Resources/ErrorMessage.ar.resx
src/Wasla/Wasla.Domain/Resources/ErrorMessage.resx
src/Wasla/Wasla.Domain/Security/SystemSecurity.cs
src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/DependencyInjection.cs
src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/ClinicalPersistenceErrorMapper.cs
src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/ClinicalReadService.cs
src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Configurations/DiagnosticConfigurations.cs
src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/DiagnosticImportService.cs
src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/DiagnosticReadService.cs
src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Migrations/20261007072127_Phase15DiagnosticOrdersAndResults.cs
src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Migrations/20261007072127_Phase15DiagnosticOrdersAndResults.Designer.cs
src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Migrations/WaslaDbContextModelSnapshot.cs
src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/WaslaDbContext.cs
src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/WaslaDbContext.Diagnostics.cs
src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/WaslaSecuritySeeder.cs
tests/Wasla.Tests.Architecture/Phase15DiagnosticArchitectureTests.cs
tests/Wasla.Tests.Integration/Phase13ApiFixture.cs
tests/Wasla.Tests.Integration/Phase15CatalogApiTests.cs
tests/Wasla.Tests.Integration/Phase15DiagnosticApiTests.cs
tests/Wasla.Tests.Integration/Phase15LifecycleContractTests.cs
tests/Wasla.Tests.Integration/Phase15SqlServerTests.cs
tests/Wasla.Tests.Integration/TrustAccessApiTests.cs
tests/Wasla.Tests.Unit/Phase15LabDomainTests.cs
tests/Wasla.Tests.Unit/Phase15LoincImportTests.cs
tests/Wasla.Tests.Unit/Phase15RadiologyDomainTests.cs
```

