# Phase 17 engineering report

## 25.1 Implementation summary

Phase 17 Engineering Status: **IN PROGRESS — final verification running**.

Branch: `codex/phase-17-notifications-reminders`.
Baseline: `68902c076c4b46415ed90e406212ea6e9ee7c0a6`.
Commit SHA: the final response records the commit containing this report.
Migrations: `20261025114416_Phase17NotificationsAndReminders`, `20261025120012_Phase17VerifiedNotificationContacts`.
CI: **NOT RUN**; this is a local branch, with no remote run or deployment created.

Implemented durable notification capture, revision-bound appointment reminders, guarded reuse of the existing email outbox, user-owned inbox/preferences, current recipient/source policies, generic clinical notifications, platform settings, safe delivery monitoring and audited controlled retry. Existing source lifecycle and queue rules remain authoritative. Clinical release gates remain closed by default; legacy booking emails retain their existing owner until controlled cutover.

## 25.2 Changed files

The complete file-by-file inventory, grouped by layer with purpose, is in [CHANGED-FILES.md](docs/phase17/CHANGED-FILES.md). No existing migration was rewritten and no source file was deleted. Generated migration target models and the current snapshot accompany the additive migrations.

The existing frozen permission-ID regression test was extended to retain every original assertion, explicitly preserve the ten Phase 5 additions, verify exactly the eight new Phase 17 additions, and verify deterministic IDs for the entire sequence. Its former fixed final-count assumption is no longer valid after an approved permission extension.

## 25.3 New endpoints

All eleven endpoint contracts, permissions, request/response shapes, status codes, concurrency token formats and behavior are in [API.md](docs/phase17/API.md). They cover six own-inbox/preferences operations and five settings/delivery/retry operations. No arbitrary send, template editing, clinical document retrieval or disposal endpoint was added.

Own user identity always comes from CurrentUser. Admin routes require current SuperAdmin profile and the specific database permission. Existing global ProblemDetails behavior is retained, including **422** for FluentValidation failures and **400** for malformed JSON/model binding, rather than introducing a new error convention.

## 25.4 Existing endpoints modified internally

Patient/Reception/Doctor booking mutations, normal/force check-in, operational NoShow/expiry, doctor governance and clinical publication/result mutations retain their HTTP contracts. Their actual immutable history/version changes are captured in `WaslaDbContext.SaveChangesAsync`, in the same source transaction. This central integration also covers background and system cancellation paths that do not share an HTTP handler.

Reservation and governance services only change internal email ownership selection. Existing governance/security/Reception invitation emails retain their owner. Details and backward compatibility behavior are in [API.md](docs/phase17/API.md) and [IMPLEMENTATION.md](docs/phase17/IMPLEMENTATION.md).

## 25.5 Database changes

Nine new tables: NotificationEvents, NotificationIntents, NotificationDeliveries, UserNotifications, NotificationPreferences, PlatformNotificationSettings, PlatformNotificationSettingsAudits, NotificationAudits and NotificationVerifiedContacts.

Outbox adds nullable NotificationDeliveryId, LastAttemptOnUtc and terminal Suppressed status. Active/archive Reservations add nullable AppointmentRevisionHistoryId. Untouched old rows retain null and resolve existing appointment history when a new operational update requires it; they receive no historical reminder backfill.

Event/idempotency, intent/event, logical delivery, inbox owner/delivery, preference category/channel, settings audit version, contact binding and optional outbox correlation have unique constraints. Due/retry, delivery status/date, inbox owner/date/read and source/revision indexes support bounded queries. Mutable notification entities use rowversion. Notification/event/contact/audit history is append-only at the persistence boundary, with restricted FKs and no operational-source cascading deletion.

Migrations preserve existing email/reservation data and only seed the singleton 120-minute settings row. The model snapshot, generated SQL and populated real SQL upgrade evidence are provided. Reviewable SQL is [MIGRATION.sql](docs/phase17/MIGRATION.sql). Normal startup migration policy was not changed.

## 25.6 Notification event matrix

All 25 approved matrix rows, actual source hooks, channels, exclusions, eligibility and acceptance references are in [EVENT-MATRIX.md](docs/phase17/EVENT-MATRIX.md). Deferred Ticket calls/recalls and digests have explicit capture exclusions. ConvertedToTicket/CheckedIn has one canonical attendance event. New clinical triggers exclude drafts and pending submissions.

## 25.7 Acceptance tests

The complete 85-ID mapping is [ACCEPTANCE.md](docs/phase17/ACCEPTANCE.md). [TEST-EVIDENCE.json](docs/phase17/TEST-EVIDENCE.json) contains actual executable data-row names, outcomes, durations and originating TRX files. Missing or skipped evidence is reported as NOT RUN. `scripts/phase17/generate-evidence.ps1` regenerates this evidence without inferring PASS from test existence.

Evidence is still being finalized. The complete native SQL regression suite passed 54/54, final unit passed 320/320, final architecture passed 24/24, BuildingBlock passed 323/323, and the final focused Phase 17 integration run passed 55/55. All have zero failures/skips. The full application integration run must finish before this report can declare engineering completion.

## 25.8 Security review

| Boundary | Implementation/evidence |
|---|---|
| Cross-user isolation | CurrentUser ownership predicates, cross-user 404, scoped bulk read/count and pagination tests. |
| Cross-Practice isolation | Current owner/practice checks for doctor operational delivery, current Reception assignment and unchanged source access policies. |
| Family authority | Independent exact current proof, booking-age policy and revocation checks; historical actor/membership alone is insufficient. |
| Clinical patient-self | Current patient link, account type, source permission and patient-safe completed source; no family/Reception/admin clinical bypass. |
| Clinical email opt-in | Explicit category/channel opt-in plus independent false-by-default release gate. |
| Hidden encounter safety | Generic preview and MedicalRecord navigation without provider/encounter/source/media keys; current visibility rechecked on inbox reads. |
| Outbox final guard | Source/revision/appointment/contact/recipient/preference/privacy/grace revalidation for correlated emails, with session-lock SMTP admission. Uncorrelated legacy emails retain existing behavior. |
| Admin monitoring | Metadata-only DTOs, with no recipient address, body, private source identifiers or clinical provenance. |
| Retry | Dedicated permission, required reason/version, original destination only, supported failed state only, source revalidation, audit and concurrent retry protection. |
| Logging/metrics | Code-controlled low-cardinality operation/event/channel/status/reason dimensions; no clinical payload or recipient address labels. |

The native SMTP-boundary test verifies that no SQL transaction spans network delivery while source mutations serialize with admission. The fault-provider test explicitly demonstrates possible physical duplicate acceptance after an unobserved acknowledgment, while logical delivery identity remains unique. Exactly-once physical SMTP delivery is not claimed.

## 25.9 Commands and CI

Release solution build:

```powershell
dotnet build Wasla.sln --configuration Release -p:Platform='Any CPU' --no-restore -m:1 -v quiet
```

Wasla test projects use project platform `AnyCPU` (solution `Any CPU` maps to that project value):

```powershell
dotnet test tests/Wasla.Tests.Unit/Wasla.Tests.Unit.csproj --configuration Release -p:Platform=AnyCPU --no-restore -m:1 --logger 'trx;LogFileName=unit.trx' --results-directory TestResults/phase17/regression -v quiet -- xUnit.MaxParallelThreads=2
dotnet test tests/Wasla.Tests.Architecture/Wasla.Tests.Architecture.csproj --configuration Release -p:Platform=AnyCPU --no-build --no-restore --logger 'trx;LogFileName=architecture.trx' --results-directory TestResults/phase17/regression -v quiet -- xUnit.MaxParallelThreads=2
dotnet test tests/Wasla.Tests.Integration/Wasla.Tests.Integration.csproj --configuration Release -p:Platform=AnyCPU --no-build --no-restore --filter 'Category!=SQLServerConcurrency' --logger 'trx;LogFileName=integration.trx' --results-directory TestResults/phase17/regression -v quiet -- xUnit.MaxParallelThreads=2
dotnet test tests/Wasla.Tests.Integration/Wasla.Tests.Integration.csproj --configuration Release -p:Platform=AnyCPU --no-build --no-restore --filter 'Category=SQLServerConcurrency' --logger 'trx;LogFileName=sql.trx' --results-directory TestResults/phase17/regression -v quiet -- xUnit.MaxParallelThreads=2
```

Native Wasla tests use the existing WASLA_SQLSERVER_CONNECTION_STRING contract, set locally to Windows integrated authentication against `SQLEXPRESS` with isolated databases. No production database was migrated. The BuildingBlock suite uses its separate BUILDINGBLOCK_SQLSERVER_CONNECTION_STRING and BUILDINGBLOCK_SQLSERVER_ALLOW_DATABASE_CREATE=true settings; its isolated SQL database path was used after the initial unconfigured fixture tried unavailable Docker.

```powershell
dotnet build src/BuildingBlock/BuildingBlock.Tests/BuildingBlock.Tests.csproj --configuration Release -p:Platform='Any CPU' --no-restore -m:1 -v quiet
dotnet test src/BuildingBlock/BuildingBlock.Tests/BuildingBlock.Tests.csproj --configuration Release -p:Platform='Any CPU' --no-build --no-restore --logger 'trx;LogFileName=buildingblock.trx' --results-directory TestResults/phase17/regression -v quiet -- xUnit.MaxParallelThreads=2
```

Focused Phase 17 verification uses a separate output folder, so it can complete without replacing assemblies used by a long-running regression host:

```powershell
dotnet test tests/Wasla.Tests.Integration/Wasla.Tests.Integration.csproj --configuration Release -p:Platform=AnyCPU --no-restore -m:1 --output artifacts/phase17-supplement --filter 'FullyQualifiedName~Phase17' --logger 'trx;LogFileName=phase17-supplement.trx' --results-directory TestResults/phase17/regression -v quiet -- xUnit.MaxParallelThreads=2
$phase17Cs = @(git diff --name-only -- '*.cs'; git ls-files --others --exclude-standard -- '*.cs')
dotnet format whitespace --folder . --include $phase17Cs --verbosity quiet
dotnet format whitespace --folder . --include $phase17Cs --verify-no-changes --verbosity quiet
git diff --check
scripts/phase17/generate-evidence.ps1
```

The final Release solution build completed with **zero warnings and zero errors** after compatibility hardening. It used `-p:OutDir=<absolute workspace>/artifacts/phase17-final-checks/solution/` in addition to the build command above to avoid replacing assemblies in the active regression host. Final unit and architecture builds/tests likewise used separate `--output artifacts/phase17-final-checks/unit` and `--output artifacts/phase17-final-checks/architecture` folders. Whitespace verification also passed with `--verify-no-changes`, and `git diff --check` passed.

Exact final build and unit/architecture rerun commands:

```powershell
$phase17BuildOutput = Join-Path (Get-Location) 'artifacts/phase17-final-checks/solution/'
dotnet build Wasla.sln --configuration Release -p:Platform='Any CPU' "-p:OutDir=$phase17BuildOutput" --no-restore -m:1 -v quiet
dotnet test tests/Wasla.Tests.Unit/Wasla.Tests.Unit.csproj --configuration Release -p:Platform=AnyCPU --no-restore -m:1 --output artifacts/phase17-final-checks/unit --logger 'trx;LogFileName=unit.trx' --results-directory TestResults/phase17/regression -v quiet -- xUnit.MaxParallelThreads=2
dotnet test tests/Wasla.Tests.Architecture/Wasla.Tests.Architecture.csproj --configuration Release -p:Platform=AnyCPU --no-restore -m:1 --output artifacts/phase17-final-checks/architecture --logger 'trx;LogFileName=architecture.trx' --results-directory TestResults/phase17/regression -v quiet -- xUnit.MaxParallelThreads=2
```

Initial failures and their resolutions were the old permission final-count assertion, unconfigured BuildingBlock Docker fallback, an In-App skip rollback for accountless recipients, source request rowversion in test setup, and an EF metadata projection translation. Actual rerun outcomes are authoritative. No previous test was removed or weakened.

CI URL: unavailable; no remote CI run was started. Existing CI automatically discovers the new tests and SQL category.

## 25.10 Remaining independent gates

| Gate | Status |
|---|---|
| Engineering | Final integration/acceptance evidence pending; do not treat this draft as completion. |
| Clinical/legal approval | Clinical email and In-App release flags remain false. No legal/clinical release is asserted. |
| Production configuration | Controlled recipient-compatibility review and email ownership cutover, worker tuning, permissions reconciliation and SMTP configuration required before deployment. |
| Notification retention/disposal | Independent policy required; no automatic notification/contact-evidence purge was added. |
| Security credential remediation | Verify/rotate production credentials through the existing secret-management process; no production secret was introduced. |
| Staging verification | Verify real provider behavior, localized messages, source API reauthorization and feature disable/restart/forward rollback. |
| Backup/recovery | Rehearse backup, recovery and lease/outbox recovery before release; preserve audit/outbox/source history. |
| Communication compliance | Review approved communication basis and consent/opt-out administration for eligible contacts without accounts; do not invent public consent tokens. |
| Formal deployment approval | Not requested or performed. Engineering completion is independent from Production Approved. |

Known limits: old verification evidence already purged before this feature is not fabricated or automatically backfilled; affected new emails fail closed. Shared address syntax never grants authority, and existing unique account-email rules are preserved. Legacy uncorrelated sends retain original semantics pending cutover. Clinical activation, retention and deployment remain separate decisions.
