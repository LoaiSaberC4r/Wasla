# Phase 17 changed files

Paths are repository-relative. **A** = added; **M** = modified. There are no deleted files. This inventory includes generated migration models and evidence, but excludes ignored local test binaries, logs and TRX files.

## API

Prefix: `src/Wasla/Wasla.Api/`.

| Change | File | Purpose |
|---|---|---|
| A | Controllers/NotificationsController.cs | Six authenticated own-inbox/preferences routes with no-store responses. |
| A | Controllers/AdminNotificationsController.cs | Five permission-protected settings, metadata monitoring and controlled retry routes. |
| M | Security/PermissionAuthorization.cs | Persist authenticated retry permission denials and validate the current admin profile. |
| M | appsettings.json | Worker/cutover defaults and independently closed clinical release flags. |

## Application

Prefix: `src/Wasla/Wasla.Application/`.

| Change | File | Purpose |
|---|---|---|
| M | DependencyInjection.cs | Register validated notification and privacy options. |
| M | Email/QueueEmailMessage.cs | Optional notification delivery correlation on the existing email contract. |
| M | Features/Doctors/DoctorGovernance.cs | Preserve single booking-email ownership during suspension cancellation cutover. |
| M | Features/Reservations/ReservationManagement.cs | Select the existing or new single booking-email owner. |
| A | Features/Notifications/AppointmentRevisionIdentity.cs | Resolve explicit or existing legacy appointment history without fabricated revisions/backfill. |
| A | Features/Notifications/NotificationContracts.cs | Eleven CQRS contracts/handlers, validators, safe DTOs and service boundary. |
| A | Features/Notifications/NotificationOptions.cs | Bounded worker options, feature switches and independent privacy gates. |
| A | Features/Notifications/NotificationTemplates.cs | Versioned Arabic/English code-controlled safe templates. |

## Domain

Prefix: `src/Wasla/Wasla.Domain/`.

| Change | File | Purpose |
|---|---|---|
| A | Notifications/NotificationModels.cs | Policies, durable events/intents/deliveries/inbox/preferences/settings/audits and state guards. |
| A | Notifications/NotificationVerifiedContact.cs | Fingerprint-only durable verified identity/address evidence. |
| A | Notifications/NotificationErrors.cs | Stable safe notification business error codes. |
| M | Reservations/Reservation.cs | Exact nullable Created/Rescheduled history revision pointer. |
| M | Resources/ErrorMessage.ar.resx | Arabic notification error messages. |
| M | Resources/ErrorMessage.resx | Default notification error messages. |
| M | Security/SystemSecurity.cs | Append eight permissions while preserving all prior deterministic IDs. |

## SQL infrastructure

Prefix: `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/`.

| Change | File | Purpose |
|---|---|---|
| M | DependencyInjection.cs | Register policies, processor, service, final email guard and hosted dispatcher. |
| M | Email/EmailOutbox.cs | Persist optional delivery correlation through the existing queue. |
| M | Email/EmailOutboxMessage.cs | Correlation and last-attempt metadata. |
| M | Email/EmailOutboxProcessor.cs | Final guarded admission, claim refresh, terminal suppression and safe retry completion without a network-spanning transaction. |
| M | Email/EmailOutboxStatus.cs | Add terminal Suppressed without renumbering prior states. |
| A | Notifications/NotificationAuthorizationAudit.cs | Durable retry authorization-denial audit. |
| A | Notifications/NotificationDispatchBackgroundService.cs | Cancellation-safe bounded periodic dispatch. |
| A | Notifications/NotificationDispatchProcessor.cs | Atomic SQL claims/leases and transactional inbox/outbox handoff. |
| A | Notifications/NotificationEligibility.cs | Current source, account, contact, family, preference and clinical eligibility. |
| A | Notifications/NotificationEmailGuard.cs | Final source/channel revalidation and safe email outcome persistence. |
| A | Notifications/NotificationPersistenceErrorMapper.cs | Map notification uniqueness/concurrency failures to existing safe errors. |
| A | Notifications/NotificationService.cs | User-scoped SQL inbox, preferences, settings, metadata monitoring and audited retry. |
| A | Notifications/NotificationSourceLock.cs | Shared SQL source serialization for saves, dispatch and SMTP admission. |
| A | Notifications/NotificationTelemetry.cs | Low-cardinality lifecycle metrics and safe structured logs. |
| M | Persistence/Configurations/ReservationConfigurations.cs | Revision-pointer mapping/index. |
| A | Persistence/NotificationCapture.cs | Capture actual committed source history/version changes and invalidate stale intents. |
| A | Persistence/NotificationContactCapture.cs | Capture exact bound verified-contact evidence atomically with identity updates. |
| A | Persistence/WaslaDbContext.Notifications.cs | Nine-table EF mapping, keys, restrictions, rowversions, indexes and singleton seed. |
| M | Persistence/WaslaDbContext.cs | Transactional capture, source locks, notification mapping and UTC conversion. |
| M | Persistence/WaslaSecuritySeeder.cs | Idempotent own-role/admin grants without expanding Reception scoped capabilities. |
| A | Persistence/Migrations/20261025114416_Phase17NotificationsAndReminders.cs | Add core tables, nullable active/archive revision and outbox metadata. |
| A | Persistence/Migrations/20261025114416_Phase17NotificationsAndReminders.Designer.cs | Generated first-migration target model. |
| A | Persistence/Migrations/20261025120012_Phase17VerifiedNotificationContacts.cs | Add fingerprint-only verified contact evidence. |
| A | Persistence/Migrations/20261025120012_Phase17VerifiedNotificationContacts.Designer.cs | Generated second-migration target model. |
| M | Persistence/Migrations/WaslaDbContextModelSnapshot.cs | Current EF model matching both additive migrations. |

## Tests

Prefix: `tests/`.

| Change | File | Purpose |
|---|---|---|
| M | Wasla.Tests.Unit/Phase5FinalIdentityDomainTests.cs | Preserve original permission order/IDs and explicitly verify the eight approved additions. |
| A | Wasla.Tests.Unit/Phase17NotificationTests.cs | Timing boundaries, policies, templates, logical identity and terminal state guards. |
| A | Wasla.Tests.Architecture/Phase17NotificationArchitectureTests.cs | No arbitrary sends/sensitive public payloads/purge routes; release flags remain closed. |
| A | Wasla.Tests.Integration/Phase17NotificationFixture.cs | Isolated source/account/contact setup, guarded dispatch and fake SMTP helpers. |
| A | Wasla.Tests.Integration/Phase17NotificationTests.cs | Capture, settings/preferences, inbox isolation, API permissions and stale-send behavior. |
| A | Wasla.Tests.Integration/Phase17LifecycleNotificationTests.cs | Check-in, NoShow/expiry/restoration, reschedule, settings/UTC snapshots, closed days, cutover and feature disable. |
| A | Wasla.Tests.Integration/Phase17ClinicalNotificationTests.cs | Publication/finalization/results, hidden-source safety, patient-self/opt-in/release policy and dependent exclusion. |
| A | Wasla.Tests.Integration/Phase17GovernanceNotificationTests.cs | Existing governance email compatibility and suspension cancellation single ownership. |
| A | Wasla.Tests.Integration/Phase17RecipientNotificationTests.cs | Trusted accountless contact, OTP cleanup, family age/proof/revocation and current Reception eligibility. |
| A | Wasla.Tests.Integration/Phase17NotificationMigrationTests.cs | Snapshot consistency, no backfill and populated native SQL upgrade preservation. |
| A | Wasla.Tests.Integration/Phase17NotificationSqlServerTests.cs | Native SQL worker races, leases, source changes, rowversion conflicts and retry concurrency. |
| A | Wasla.Tests.Integration/Phase17SmtpBoundaryTests.cs | Source/session lock serialization with no SQL transaction across paused SMTP. |
| A | Wasla.Tests.Integration/Phase17ReliabilityNotificationTests.cs | Provider acknowledgment loss, bounded SQL pagination, safe permanent failures and stale retry denial. |

## Documentation and evidence

| Change | File | Purpose |
|---|---|---|
| M | .gitignore | Exclude local Phase 17 binaries and raw console logs. |
| A | PHASE-17-IMPLEMENTATION-REPORT.md | Required 25.1–25.10 engineering handoff and independent release gates. |
| A | docs/phase17/CHANGED-FILES.md | This complete file/purpose inventory. |
| A | docs/phase17/API.md | Routes, permissions, request/response/status contracts and existing source integrations. |
| A | docs/phase17/IMPLEMENTATION.md | Architecture, data, workers, policies, configuration, cutover and rollback. |
| A | docs/phase17/EVENT-MATRIX.md | All 25 approved event rows and channel/source/evidence decisions. |
| A | docs/phase17/MIGRATION.sql | Reviewable idempotent additive upgrade SQL from the baseline migration. |
| A | docs/phase17/ACCEPTANCE.md | All 85 acceptance IDs mapped to executed evidence. |
| A | docs/phase17/TEST-EVIDENCE.json | Actual TRX run counters and per-case executable results. |
| A | scripts/phase17/generate-evidence.ps1 | Reproducible evidence generation; missing/skipped tests cannot become PASS. |
