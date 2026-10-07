# Phase 15 closure report — 2026-10-07

**Phase 15 status: DONE.** Branch: `codex/phase15-diagnostic-orders-results`. Original baseline: clean `main` at `2181b40b54d0884fe1362563423b184f6c5b174d`; the implementation was recorded in `cc99169` and has since been merged according to the follow-up task. Real-package hotfix evidence is in sections 13 and 14; the Arabic mapping hotfix continues from `10577dc` on the same branch. Subsequent manual development full Apply evidence is in section 15. No production database migration or deployment was performed during the hotfixes.

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

**Original Phase 15 delivery: 69 files, 45 added and 24 changed.** Its path inventory is at the end of this report; subsequent hotfix file lists are in sections 13 and 14.

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

Permanent unit/API/SQL tests use small deterministic synthetic packages. The initial delivery did not have the full licensed 2.83 ZIP. The subsequent hotfix used the supplied local official package successfully for both parsers and both authenticated SQL Server preview endpoints; exact counts and timings are in section 13. The package is ignored and uncommitted. At the time of that automated hotfix verification, full-release catalog Apply was not performed. Subsequent full Lab and Radiology Apply and persisted catalog verification succeeded in the local/development environment, as documented in section 15. Production Apply and production performance/load acceptance remain pending; local preview timings are not a production benchmark.

## 8. Security, concurrency and idempotency

Every operation checks actor type, current account state, first login, token/current-database permission intersection and clinical ownership. Doctor actions require approval plus an active owned Practice; Patient actions require the account-linked own profile. Other owners receive safe 404s. Root, ordinary SuperAdmin, Reception and catalog managers have no automatic clinical override. Reads are scoped/server-paged, clinical responses use no-store, and no global clinical query cache is introduced.

Root rowversions advance on child writes. Existing transaction/application-lock infrastructure coordinates draft creation, publication, coverage, result lifecycle and catalog reconciliation. SQL uniqueness and guards protect draft/current-version/source/coverage invariants and retained immutable content. Existing Phase 14 idempotency persistence is reused with separate diagnostic operation namespaces; matching retries create no duplicate work and return latest authorized clinical state, changed payloads conflict and failed transactions retain no success key.

PDF/JPG/PNG uploads reuse BuildingBlock private storage, signature/MIME checks and configured malware-scanner chain. Limits are 10 MiB per file, 20 files and 100 MiB total; Radiology requires attachment kinds. SQL stores metadata/private references only. Every download rechecks current ownership, permissions, actor state and official version validity. Original Patient uploader provenance survives Doctor acceptance. File compensation surrounds the transaction pipeline, including failures after handler success, and checks persisted references before deleting files after an uncertain commit. If the database or storage cannot complete reconciliation, metadata-only logging preserves the original failure and calls for retained-file reconciliation; there is no claim that storage and SQL form one distributed transaction.

Mutations retain immutable actor/time/reason/before/after history. Single-record reads/downloads reuse EncounterAuditEvents with metadata only, without modifying clinical rowversions. PHI redaction is extended; file bytes, report contents, patient notes, internal clinical text and secrets are not intentionally logged. **131 new diagnostic error codes** have English and Arabic text. Existing media and shared idempotency codes keep their established names.

## 9. Original delivery commands

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

## 10. Original delivery verification results

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

V1 exclusions remain as approved: structured observations/reference ranges/abnormal flags, provider/laboratory/PACS integration, DICOM viewing/storage, external result ingestion, sharing/access delegation, substitute reviewers, notifications and wider longitudinal-record work. No requested V1 lifecycle is left as a scaffold. Production deployment/seeding, production catalog Apply/upgrade and performance/load acceptance, and deployment-specific scanner configuration remain operational work. Local real-package parsing and preview are verified in sections 13 and 14; the subsequent real full development Apply and persisted catalog verification for both domains are documented in section 15.

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

## 13. Real LOINC 2.83 import hotfix — 2026-10-07

The follow-up work stays on `codex/phase15-diagnostic-orders-results`, based on `cc99169`. It changes import compatibility and bounded package handling without redesigning Phase 15. No domain lifecycle, ownership/review rule, permission, manager-governance rule, result correction/void behavior or medical attachment business limit is changed. **No frontend API contract change.** No new migration is required.

### Root cause and exact path selection

The supplied official ZIP contains both `LoincTable/Loinc.csv` and `AccessoryFiles/PanelsAndForms/Loinc.csv`. The original basename search rejected this valid package as UnsafeArchive. Canonical selection now compares the entire normalized path against `LoincTable/Loinc.csv`, case-insensitively, and requires exactly one match. It never takes the first arbitrary basename match or accepts a prefixed suffix match. Global traversal, absolute-path, symlink, duplicate-normalized-path, expansion, ratio, row and encoding checks remain intact.

The real 2.83 accessory paths were inspected before finalizing the resolver:

```text
AccessoryFiles/LoincUniversalLabOrdersValueSet/LoincUniversalLabOrdersValueSet.csv
AccessoryFiles/LinguisticVariants/arJO32LinguisticVariant.csv
AccessoryFiles/LoincRsnaRadiologyPlaybook/LoincRsnaRadiologyPlaybook.csv
```

Those paths are recognized explicitly. Each accessory basename is unique in this package; safe unique-basename fallback preserves earlier fixture/layout compatibility. Multiple matching accessory basenames, including optional Arabic, fail rather than being chosen arbitrarily. Supported versions still come from `DiagnosticCatalogImport.SupportedVersions`.

### Local official package and real verification

Local verification input: **`_local-data/Loinc_2.83.zip`**. Size: **92,815,327 bytes**, **95 ZIP entries**, **1,063,582,650 declared expanded bytes**. SHA-256:

```text
077A0718E87D8309FFE3A673F75B836A8E783DC36A646413EF97C71C12EAB27E
```

The ZIP and any local extracted files were **not committed**. `_local-data/` is ignored, including the local-only verification harness, reports and its build/test output. Normal CI does not require this directory or source package. Existing source/license notices are preserved. The official package was supplied locally; no terminology was downloaded by this hotfix.

| Real-package verification | Lab | Radiology |
| --- | ---: | ---: |
| Parser | PASS | PASS |
| Exact parsed concepts / grouped procedures | 47,977 | 7,016 |
| ACTIVE / Doctor selectable on initial import | 43,465 | 6,941 |
| Official Arabic names (corrected in section 14) | 1,192 | 0 |
| IsCommonOrder rows | 1,517 | 0 |
| TRIAL | 2,568 | 2 |
| DISCOURAGED | 1,182 | 73 |
| DEPRECATED | 762 | 0 |
| Authenticated preview endpoint | HTTP 200 / PASS | HTTP 200 / PASS |
| Persisted Staged records in disposable SQL Server DB | 47,977 | 7,016 |
| Local preview elapsed seconds | 34.222 | 15.035 |

Counts are measured output, not hard-coded expectations. The previously reported Arabic count of 883 was invalid: it counted English values from the wrong variant column. Section 14 verifies the corrected count and null behavior against the localized source field. Every parsed Lab row satisfies CLASSTYPE 1 and ORDER_OBS Order/Both. Non-active rows remain retained and non-selectable. The common value set supplies flags/ranking only; it does not replace the complete catalog. Each real output row's code, name and status was checked against the canonical source fields. The authenticated previews used the unchanged multipart endpoints, committed reviewable batches and verified package SHA-256 plus paged change counts. Those earlier automated previews did not apply the full catalog or modify a production database. A subsequent manual development full Apply is documented in section 15.

Radiology produces one procedure per LoincNumber, retains **49,621 original Part rows** and preserves their PartNumber/PartSequenceOrder plus original names. The 18 actual PartTypeName values are:

```text
Rad.Anatomic Location.Imaging Focus
Rad.Anatomic Location.Laterality
Rad.Anatomic Location.Laterality.Presence
Rad.Anatomic Location.Region Imaged
Rad.Guidance for.Action
Rad.Guidance for.Approach
Rad.Guidance for.Object
Rad.Guidance for.Presence
Rad.Maneuver.Maneuver Type
Rad.Modality.Modality Subtype
Rad.Modality.Modality Type
Rad.Pharmaceutical.Route
Rad.Pharmaceutical.Substance Given
Rad.Reason for Exam
Rad.Subject
Rad.Timing
Rad.View.Aggregation
Rad.View.View Type
```

The local check validates every original Part row against the grouped attributes without rewriting source spelling/case. The source here uses `Rad.Modality.Modality Type`; existing case-insensitive snapshot extraction also accepts the lower-case `type` variant covered by synthetic tests.

### Memory and release-scale persistence hardening

Both preview controllers now pass `IFormFile.OpenReadStream()` through the internal command/service instead of copying the full package into MemoryStream and another byte array. The controller owns and disposes the stream; ZipArchive leaves it open. ASP.NET's bounded multipart buffering/spooling and request cleanup remain responsible for the upload backing store; no permanent custom temporary files or absolute local paths are added to production code. SHA-256 uses an 80 KiB buffer and checks actual byte totals. Parsing checks seekability and package length, preserves bounded entry reads, and propagates cancellation through hashing, ZIP/CSV processing, output construction and staging. Non-seekable parser input is rejected instead of implicitly buffering an unbounded archive. The byte-array overload remains for small existing synthetic fixtures.

A full preview also revealed repeated EF change-tracker enumeration for every added import row. That run was interrupted, and its verified disposable test database was cleaned up. The guard now captures new Lab/Radiology batch IDs once and uses set membership for each imported record, preserving the rule that persisted previews cannot be extended. Permanent tests explicitly attempt that extension and prove it is rejected for both domains. The successful full previews were rerun after this change.

Remaining memory work is explicit: source dictionaries, parsed rows, staged source JSON and the full EF batch remain materialized. The local verification process reached approximately 1 GiB working set; this is an observation of the test process, not an isolated production request benchmark. End-to-end streaming/chunked staging and concurrent-import capacity testing are deferred to a separate performance change. Production deployment should validate memory, temporary-disk capacity, request/proxy timeouts and full apply/upgrade behavior under its own load. Medical attachments remain 10 MiB each, 20 files and 100 MiB aggregate.

### Permanent regression coverage and verification

The parser suite now has **33 passing cases**, including both domains with the unrelated Loinc.csv before/after the canonical entry, normalized slash/case matching, duplicate canonical paths, exact-path requirement, ambiguous accessories, missing required files, optional missing Arabic, Lab/common/Arabic rules, Radiology grouping, invalid headers/archive/version, package/entry/expansion/row/ratio limits, traversal, stream ownership, non-seekable input and cancellation. API fixtures now use the real official accessory structure and an unrelated panels-table basename. New API tests verify the unchanged preview contract/hash and immutable persisted batches. In total the hotfix adds **22 unit and 4 integration cases**, all synthetic and independent of licensed data.

Final build: **0 warnings, 0 errors**. Final focused Phase 15 verification: **66 unit + 35 integration + 4 architecture = 105 passed; 0 failed, 0 skipped**. All import tests are included (33 parser and 12 catalog/import/governance API cases). Local-only real-package verification: **2 parser + 2 authenticated SQL Server preview checks passed; 0 failed, 0 skipped**. These four local checks are separate from the normal solution totals.

Final full solution regression: **196 unit + 151 integration + 20 architecture = 367 passed; 0 failed, 0 skipped**. Focused cases are included in that total, not added to it. The full suite covers the existing Phase 15 lifecycle, ownership/review, permissions, correction/void, medical media, Complete Visit and prescription/follow-up transaction regressions. The final SQL fixture cleanup left no WaslaPhase13Tests databases behind.

Commands actually used:

```powershell
dotnet build Wasla.sln --verbosity minimal
dotnet test tests/Wasla.Tests.Unit/Wasla.Tests.Unit.csproj --no-restore --filter 'FullyQualifiedName~Phase15LoincImportTests' --logger 'trx;LogFilePrefix=phase15-hotfix-parser' --verbosity quiet
$env:WASLA_SQLSERVER_CONNECTION_STRING = 'Server=.\SQLEXPRESS;Database=master;Integrated Security=true;TrustServerCertificate=true'
dotnet test Wasla.sln --no-restore --filter 'FullyQualifiedName~Phase15' --logger 'trx;LogFilePrefix=phase15-hotfix-final-focused' --verbosity quiet
dotnet test Wasla.sln --no-build --no-restore --logger 'trx;LogFilePrefix=phase15-hotfix-regression' --verbosity quiet
$env:WASLA_LOINC_PACKAGE = (Resolve-Path -LiteralPath '_local-data/Loinc_2.83.zip').Path
dotnet test _local-data/phase15-hotfix-verification/Phase15.RealPackageVerification.csproj --filter 'FullyQualifiedName~Official_package_parses' --logger 'trx;LogFilePrefix=phase15-real-parser' --verbosity quiet
dotnet test _local-data/phase15-hotfix-verification/Phase15.RealPackageVerification.csproj --no-restore --filter 'FullyQualifiedName~Official_package_previews' --logger 'trx;LogFilePrefix=phase15-real-preview-final' --verbosity quiet
git diff --check
git status --short
git check-ignore _local-data/Loinc_2.83.zip
git ls-files _local-data
git log --all --format= --name-only -- _local-data
```

The local harness and metadata-only JSON/TRX evidence remain ignored. Git tracked-file/history checks for `_local-data` return empty; the directory does not appear in staged or ordinary untracked changes. No licensed ZIP or extracted terminology was copied into tests, src, fixtures, wwwroot or migrations. No hotfix commit/push/deployment was performed by this run.

Hotfix changes are confined to these 13 tracked files:

```text
.gitignore
PHASE-15-API-CHANGES.md
PHASE-15-CLOSURE-REPORT.md
README.md
src/Wasla/Wasla.Api/Controllers/LabControllers.cs
src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs
src/Wasla/Wasla.Application/Features/Diagnostics/DiagnosticContracts.cs
src/Wasla/Wasla.Application/Features/Diagnostics/LoincPackageParser.cs
src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/DiagnosticImportService.cs
src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/WaslaDbContext.Diagnostics.cs
tests/Wasla.Tests.Integration/Phase15CatalogApiTests.cs
tests/Wasla.Tests.Unit/Phase15LoincImportTests.cs
tests/Wasla.Tests.Unit/Phase15RadiologyDomainTests.cs
```

## 14. Arabic linguistic variant mapping hotfix — 2026-10-07

### Root cause and mapping

The extracted `AccessoryFiles/LinguisticVariants/arJO32LinguisticVariant.csv` and the same entry in the local official 2.83 ZIP were inspected before changing the parser. Their actual header is:

```text
LOINC_NUM,COMPONENT,PROPERTY,TIME_ASPCT,SYSTEM,SCALE_TYP,METHOD_TYP,CLASS,SHORTNAME,LONG_COMMON_NAME,RELATEDNAMES2,LinguisticVariantDisplayName,ConsumerName
```

The old parser preferred `LONG_COMMON_NAME`, which is English even in this Arabic variant, and overlooked `LinguisticVariantDisplayName`. It could therefore put English into `OfficialNameAr`. The corrected parser selects the first valid localized value from **`LinguisticVariantDisplayName` → `LONG_COMMON_NAME_AR` → `DisplayName`**, with case-insensitive headers. `LONG_COMMON_NAME` is never an Arabic fallback; the variant must expose a supported localized column. Empty or obviously nonlocalized values are skipped, and no valid candidate produces null. A candidate must contain an Arabic letter, but may also contain Latin gene names, abbreviations, numbers, units and symbols. Source text and formatting are preserved without translation or generation.

### Real package and authenticated preview

The corrected production parser was run against the existing ignored **`_local-data/Loinc_2.83.zip`**, with the same SHA-256 recorded in section 13. Lab parsing succeeds with **47,977 records**, source version **2.83**, and exactly **1,192 non-null official Arabic names**. Every Lab row's `OfficialNameAr` was compared with its official `LinguisticVariantDisplayName`, including nulls for absent/blank source values. All non-null values differ from the canonical English name. Code **100026-4** matches its official localized display value and retains the canonical `LoincTable/Loinc.csv` English name; code **62369-4** has a blank localized value and remains null. The variant's English base name and canonical English name differ for 100026-4; neither is used as Arabic. During that hotfix, real terminology values were retained only in ignored local evidence. Radiology was also rechecked: **7,016 procedures**, **0 official Arabic names**, with original Part metadata preserved.

Authenticated **`POST /api/v1/admin/lab-catalog/imports/preview`** used that same ZIP with `sourceVersion=2.83` on a disposable SQL Server database. Result: **HTTP 200, Staged, 47,977 records**. The staged change for 100026-4 was queried through the existing changes endpoint; its `SourceDataJson.OfficialNameAr` equals the official localized field and differs from `NameEn`. The missing value for 62369-4 also remains null in persisted source JSON. Verification batch **`409c32f7-ad0e-4484-8ddc-0da8a84896d7`** was explicitly **discarded**, and the catalog still contained **0 rows**. **At the time of this automated hotfix verification, no real-package Apply was executed.** A subsequent manual development full Apply is documented in section 15. The fixture disposed its isolated database; no production database or earlier discarded batch was changed.

### Permanent tests and verification

Fourteen synthetic regression cases cover the official localized field with an English base name, preferred-field/alias priority, case-insensitive aliases, empty/whitespace/missing values, English-only values, Arabic digits/punctuation without letters, and mixed Arabic/Latin gene names, units and symbols. All fourteen failed against the old parser before the fix, then passed. Existing unit and API fixtures now put English in `LONG_COMMON_NAME` and synthetic Arabic in `LinguisticVariantDisplayName`; normal CI remains independent of the licensed ZIP.

Final parser verification: **47 passed, 0 failed, 0 skipped**. Final focused Phase 15 verification: **80 unit + 35 integration + 4 architecture = 119 passed, 0 failed, 0 skipped**. Local-only verification: **2 real-package parser checks + 1 authenticated Lab preview/discard check passed, 0 failed, 0 skipped**; these are separate from solution totals.

Final full solution regression: **210 unit + 151 integration + 20 architecture = 381 passed, 0 failed, 0 skipped**. Focused cases are included in that total. Final SQL fixture cleanup left **0 WaslaPhase13Tests databases**. Runner/TRX evidence uses the `phase15-arabic-parser`, `phase15-arabic-focused`, `phase15-arabic-regression`, `phase15-arabic-real-parser` and `phase15-arabic-real-preview` prefixes; expected pre-fix failures are retained separately as `phase15-arabic-red`.

The default-output solution build initially failed because the running Wasla API/Visual Studio session locked its DLLs/PDBs. The same solution built successfully with output redirected to ignored `_local-data/phase15-arabic-build`: **0 warnings, 0 errors**. Verification commands use that output directory to preserve the running development session:

```powershell
dotnet build Wasla.sln --artifacts-path _local-data/phase15-arabic-build --verbosity minimal
dotnet test tests/Wasla.Tests.Unit/Wasla.Tests.Unit.csproj --no-restore --filter FullyQualifiedName~Phase15LoincImportTests --logger 'trx;LogFilePrefix=phase15-arabic-parser' --verbosity quiet
$env:WASLA_SQLSERVER_CONNECTION_STRING = 'Server=.\SQLEXPRESS;Database=master;Integrated Security=true;TrustServerCertificate=true'
dotnet test Wasla.sln --artifacts-path _local-data/phase15-arabic-build --no-build --no-restore --filter FullyQualifiedName~Phase15 --logger 'trx;LogFilePrefix=phase15-arabic-focused' --verbosity quiet
dotnet test Wasla.sln --artifacts-path _local-data/phase15-arabic-build --no-build --no-restore --logger 'trx;LogFilePrefix=phase15-arabic-regression' --verbosity quiet
$env:WASLA_LOINC_PACKAGE = (Resolve-Path -LiteralPath '_local-data/Loinc_2.83.zip').Path
dotnet test _local-data/phase15-hotfix-verification/Phase15.RealPackageVerification.csproj --artifacts-path _local-data/phase15-arabic-build --filter FullyQualifiedName~Official_package_parses --logger 'trx;LogFilePrefix=phase15-arabic-real-parser' --verbosity quiet
dotnet test _local-data/phase15-hotfix-verification/Phase15.RealPackageVerification.csproj --artifacts-path _local-data/phase15-arabic-build --no-build --no-restore --filter FullyQualifiedName~Official_arabic_lab_preview --logger 'trx;LogFilePrefix=phase15-arabic-real-preview' --verbosity quiet
```

Only five tracked files change for this hotfix: `LoincPackageParser.cs`, `Phase15LoincImportTests.cs`, `Phase15CatalogApiTests.cs`, this report and README's incorrect Arabic count/current verification summary. `_local-data/` remains ignored and untracked, with empty tracked-file/history checks. The official package was not copied, moved, committed or redistributed. No API/business contract, permission, lifecycle, apply/merge behavior, source-version rule, Lab filtering, selectability, active/inactive handling, clinical snapshot, attachment rule or migration changed. No commit, push or deployment was performed.

## 15. Real LOINC 2.83 full development Apply — 2026-10-07

### Scope

The official ignored **`_local-data/Loinc_2.83.zip`** was manually exercised in the **local/development environment**, using `SourceVersion=2.83` and the same known SHA-256 **`077A0718E87D8309FFE3A673F75B836A8E783DC36A646413EF97C71C12EAB27E`**. Both catalogs used the MedicalCatalogManager-authenticated API flow with user **`82cc0ddd-bdb7-4b65-b2ea-948eb38297d5`**. This was manual development verification, not a Production deployment or Production Apply.

### Lab Preview, review and Apply

The corrected Lab Preview created batch **`b375911c-db5e-499b-a5e1-902caa044a0a`** with `Status=Staged`, `TotalRecords=47,977`, `Counts.New=47,977`, `CanApply=true` and `CanDiscard=true`.

Before Apply, staged LOINC **100026-4** was manually reviewed:

- `NameEn`: MET gene mutations found [Identifier] in Blood or Tissue by Targeted gene mutation analysis Nominal
- `OfficialNameAr`: الكشف عن الطفرات الجينية في جين MET في الدم أو الأنسجة ( بتقنية علم الوراثة الجزيئية )

The official localized value confirmed that the `LinguisticVariantDisplayName` mapping hotfix was working in the real staged data. The full Lab Apply then succeeded:

| Applied result | Value |
| --- | --- |
| BatchId | `b375911c-db5e-499b-a5e1-902caa044a0a` |
| Status | `Applied` |
| TotalRecords | 47,977 |
| Counts.New | 47,977 |
| AppliedByUserId | `82cc0ddd-bdb7-4b65-b2ea-948eb38297d5` |
| AppliedAtUtc | `2026-10-07T10:41:17.0764964` |
| CanApply / CanDiscard | `false` / `false` |

Final **`GET /api/v1/admin/lab-catalog?pageNumber=1&pageSize=1`** returned **`totalCount=47,977`**. The persisted catalog was searched for **100026-4** and verified to have `Source=Loinc`, `Status=Active` and `SourceVersion=2.83`; `OfficialNameEn` retained the canonical English LOINC name and `OfficialNameAr` contained the Arabic localized value shown above. The final catalog record was persisted successfully.

The existing **43,465 Active Lab** count comes from the earlier real-package parser/verification evidence in section 13, not a new Active-Lab measurement from this manual session. Section 14's **1,192 official Arabic names** remains the corrected parser verification count.

### Radiology Preview, review and Apply

The same package produced Radiology batch **`9022bb63-6924-4f6c-9666-ac54f68fa701`** with `Status=Staged`, `TotalRecords=7,016`, `Counts.New=7,016`, `CanApply=true` and `CanDiscard=true`.

Staged samples, including **100349-0**, **100760-8**, **101301-0** and **103230-9**, were manually reviewed before Apply. They preserved LOINC identity, canonical English name, external status, `Attributes`, original `RadiologyParts`, `PartNumber`, `PartTypeName`, `PartName`, `PartSequenceOrder` and RID / PreferredName where supplied. Review also confirmed grouped multi-value attributes, Laterality, Modality, Region Imaged, Imaging Focus, View information and pharmaceutical metadata where applicable.

The full Radiology Apply then succeeded:

| Applied result | Value |
| --- | --- |
| BatchId | `9022bb63-6924-4f6c-9666-ac54f68fa701` |
| Status | `Applied` |
| TotalRecords | 7,016 |
| Counts.New | 7,016 |
| AppliedByUserId | `82cc0ddd-bdb7-4b65-b2ea-948eb38297d5` |
| AppliedAtUtc | `2026-10-07T10:54:53.7971049` |
| CanApply / CanDiscard | `false` / `false` |

Final **`GET /api/v1/admin/radiology-catalog?pageNumber=1&pageSize=1`** returned **`totalCount=7,016`**. Persisted LOINC **99633-0**, **Cone beam CT Teeth**, was inspected and contained these `Attributes`:

| Attribute | Persisted value |
| --- | --- |
| `Rad.Anatomic Location.Imaging Focus` | Teeth |
| `Rad.Anatomic Location.Region Imaged` | Head |
| `Rad.Modality.Modality Subtype` | Cone beam |
| `Rad.Modality.Modality Type` | CT |

Its persisted `SourceDataJson` also contained the original `RadiologyParts`. The explicit active-catalog query **`GET /api/v1/admin/radiology-catalog?status=Active&pageNumber=1&pageSize=1`** returned **`totalCount=6,941`**, matching the previously measured expected Active Radiology count.

### Import lifecycle and terminal state

The real **Upload → Preview → Review → Apply → Persisted Catalog Verification** flow was successfully exercised end-to-end for **both Lab and Radiology**. Successful Apply returned `CanApply=false` and `CanDiscard=false` for both batches. This records the observed terminal state; no repeated-Apply verification is claimed.

### Environment boundary and verification limits

This closes the previous **full-release catalog Apply not performed** verification gap for the **local/development environment**. Production Apply was not performed. Production deployment remains a separate controlled operation, and production performance/load acceptance remains pending and deployment-specific. Production migration, seeding and deployment remain outside this manual verification. The manual evidence does not change the existing automated test counts; no new tests or build were run for this documentation update.

## Original delivery file inventory

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

