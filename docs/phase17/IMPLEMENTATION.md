# Phase 17 implementation and operations

Baseline: `68902c076c4b46415ed90e406212ea6e9ee7c0a6`. Branch: `codex/phase-17-notifications-reminders`.

## Integration map and compatibility audit

The repository already provides a transactional SQL email outbox, command transactions, application locks, current access snapshots, verified patient account links, exact parent/child proof, clinical publication history, and archive-aware source readers. These facilities are reused. No broker, SMTP implementation, transport credentials, appointment calculation, or clinical lifecycle was added.

| Existing integration | Phase 17 implementation |
|---|---|
| `WaslaDbContext.SaveChangesAsync` | Captures new immutable source transitions and notification events/intents before the same save. Uses the caller's transaction, or owns one when necessary. This covers operational/background writers as well as HTTP commands. |
| `ReservationApplicationService.QueueNotificationAsync` | Existing confirmation/cancellation/reschedule emails remain the default owner. The explicit internal cutover switch disables this path when notification ownership is enabled. |
| `DoctorLifecycleService` | Existing governance emails remain. Reservation cancellation email ownership follows the same cutover switch. Actual status histories produce governance In-App intents. |
| `Reservation` | Records the exact Created/Rescheduled history ID in a nullable appointment revision pointer. No-op/rejected reschedules and restoration do not change it. |
| Reservation NoShow, expiry, check-in/force check-in | The actual persisted reservation history is captured, including transitions from operational workers. ConvertedToTicket maps to one `Patient.CheckedIn` event. |
| `CompleteTicket`, prescription correction, diagnostic publication/results | Existing atomic publication and official history/version transitions are captured. Drafts and patient submissions are excluded. |
| `EmailOutboxProcessor` | Keeps its claims, retry/backoff, permanent failure handling and SMTP sender. Correlated emails gain final eligibility checks and terminal suppression. Uncorrelated legacy messages retain existing behavior. |
| `WaslaSecuritySeeder` | Appends deterministic permissions and idempotent grants. Existing permission IDs and assignment-scoped permission lists remain intact. |

Notification capture is infrastructure integration around existing domain history, rather than an in-memory event handler after commit. SMTP never runs inside the source transaction or notification dispatcher.

## Data and identity

`NotificationEvents` contain routing identifiers, event/revision identity and classification, without serialized entities or clinical content. Each event has one unique intent. A reminder uses its Created/Rescheduled history identity; the intent stores committed UTC appointment, lead time, due time, 15-minute expiry and settings version snapshots.

An intent has recipient/channel deliveries. An In-App delivery creates an exclusively user-owned `UserNotification`; an email delivery correlates to the existing outbox through nullable `EmailOutboxMessages.NotificationDeliveryId`. Outbox state is authoritative for SMTP status, including monitoring. In-App `Queued` means an inbox item exists, not that email was physically sent.

Tables added:

- `NotificationEvents`, `NotificationIntents`, `NotificationDeliveries`, `UserNotifications`.
- `NotificationPreferences`, `PlatformNotificationSettings`, `PlatformNotificationSettingsAudits`, `NotificationAudits`.
- `NotificationVerifiedContacts`, containing an address fingerprint and verified identity/evidence binding, without another plaintext email address.

Outbox additions: nullable delivery correlation, last attempt UTC metadata and terminal status `Suppressed = 5`. Active and archived reservations gain nullable `AppointmentRevisionHistoryId`. Historical rows are not backfilled with notifications or invented verification evidence.

Unique keys protect event idempotency, one intent/event, recipient/channel logical delivery, user/delivery inbox ownership, user/category/channel preference, settings audit version, verified contact binding, and non-null outbox delivery correlation. Worker due/retry indexes and owner/date/read inbox indexes bound scans. Mutable notification entities use SQL rowversion. Event, contact-evidence and audit records are append-only through the persistence boundary. FKs between notification records and user/contact identities use restricted deletion; routing IDs do not cascade from operational or archived sources.

Migrations:

1. `20261025114416_Phase17NotificationsAndReminders`.
2. `20261025120012_Phase17VerifiedNotificationContacts`.

The singleton settings seed enables scheduling at a 120-minute lead with version 1. It creates no event, intent, delivery or email. Both migrations are additive. The normal controlled database migration policy remains in effect.

Untouched pre-migration reservations keep a null revision pointer. New operational updates resolve their existing immutable appointment history for revalidation; restoration does not fabricate a new revision or backfill a reminder.

## Recipients and privacy

New channels require current account/identity/relationship checks. Email syntax and a historical booking actor do not confer eligibility. Current patient account linkage and active, operable accounts are checked again during dispatch and email admission. Doctor operational recipients require current practice ownership, approval and relevant source permission. Reception is not subscribed to patient booking messages.

Verified registration response identity, bound email-change verification and consumed account activation evidence prove email ownership. New verification/activation commits also preserve a fingerprint-only contact binding, so ordinary OTP cleanup does not erase that evidence. Existing historical email evidence is reused only when it binds the exact current user/patient/address. Missing or ambiguous proof suppresses the affected new email channel.

A global patient without an account can qualify through a still-current, independently reviewed approved claim that binds the same verified email to that exact patient. The address must match the current patient contact and not belong to another account. This creates no user or account link, and no inbox. Unsupported consent/opt-out administration for such contacts remains a release concern.

Family recipients require current under-18 booking authority, exact reviewed relationship evidence and current membership. Parent proof revocation, applicable restrictions and revoked legal-guardian authority are checked. Medical sharing's under-16 cutoff is not used as the booking-age calculation. Different logical recipients retain separate inboxes; an email string never establishes audience equivalence or authority.

Clinical recipients are patient-self only. Published source, current finalized/voided version as appropriate, completed encounter/ticket consistency, current linkage and source-read permissions are checked. A notification cannot give Reception, a relative or SuperAdmin clinical access. Clinical navigation is the generic `MedicalRecord` target with no encounter, provider, attachment or medical source key. Inbox reads and unread counts re-evaluate sensitive visibility; inaccessible entries are excluded. Destination APIs retain their independent authorization.

Both clinical release flags default false. Clinical email additionally requires explicit current email opt-in. Preferences do not activate medical sharing or legal release. No existing medical-sharing/archive flags were enabled.

## Configuration and scheduling

```json
{
  "Notifications": {
    "Enabled": true,
    "DispatchEnabled": true,
    "OwnReservationEmails": false,
    "PollingIntervalSeconds": 15,
    "BatchSize": 50,
    "ClaimLeaseSeconds": 120,
    "MaxAttempts": 5
  },
  "NotificationPrivacy": {
    "ClinicalEmailReleaseApproved": false,
    "ClinicalInAppReleaseApproved": false
  }
}
```

These are startup options; deployment changes require restart. Enabled controls new source capture; DispatchEnabled controls notification processing. The email guard also refuses correlated sends while processing is disabled. No source mutation, history deletion or historical scheduling is performed by these switches.

Reminder policy is the separately persisted platform singleton. Lead time input is 1–43,200 minutes: a conservative technical bound matching the existing 30-day maximum booking horizon, not a new appointment business rule. Worker limits are independently validated: batch 1–100, polling 1–3,600 seconds, claim lease 120–3,600 seconds, attempts 1–20.

Only booking strictly before due schedules a reminder. Dispatch is allowed from due through due+15 minutes inclusive and strictly before appointment start, for the current active appointment revision. Committed UTC snapshots are used without timezone recomputation or quiet-hours shifting. Cancellation, expiry, reschedule and check-in invalidate unsent old reminders. NoShow remains ineligible; an unchanged future unsent intent can survive restoration, while restoration at/after due cannot produce catch-up delivery. A dispatched reminder is never recreated on restoration.

Changing lead time affects subsequent bookings/actual reschedules only. Existing intent snapshots are immutable. A closed operational day, unavailable practice/doctor, changed appointment snapshot, stale revision or terminal reservation cannot admit a reminder.

## Worker and SMTP concurrency boundary

`NotificationDispatchBackgroundService` is registered after initialization with the existing hosted services. It uses bounded conditional SQL updates, a claim token, lease recovery and bounded retries. A claim is committed before acquiring the source lock; ownership is checked again after lock admission. Deliveries, inbox/outbox handoff and intent completion commit together. Unique constraints protect logical identity across workers/restarts.

Reservation saves and dispatch use the same `Wasla:Notification:Reservation:<id>` SQL application-lock resource. Source saves/dispatch handoff hold transaction-owned locks. SMTP admission holds a session-owned lock, refreshes source/revision/recipient/trust/preference/privacy/deadline eligibility and verifies outbox claim ownership. The session lock spans SMTP; no SQL transaction spans network latency. Result persistence uses a short transaction after SMTP. The source lock timeout is 15 seconds; conflicting writes may need the normal concurrency retry while a handoff is active.

Cancellation/reschedule committed before admission prevents a stale queue/send handoff. A provider acceptance already admitted before cancellation cannot be recalled. A crash after provider acceptance and before success persistence can produce a physical duplicate on retry. This is durable logical idempotency with external SMTP uncertainty, not exactly-once physical delivery.

Notifications do not call Reservation NoShow/expiry methods or change queue algorithms. SMTP failures cannot roll back a committed reservation or clinical mutation. Retry and failure codes are safe codes; provider exception text, credentials and medical payloads are not logged.

## Email ownership and deployment

Deploy both additive migrations, then run the existing authorized security-seeding process to reconcile permissions. Do not enable startup migration/seeding against production merely for this feature. Upgrade all source writers before enabling source capture/cutover; mixed old writers cannot maintain the new appointment revision pointer. Keep the existing archive and clinical flags unchanged.

`OwnReservationEmails = false` preserves existing booking email behavior and idempotency. Captured events record legacy ownership durably, so dispatch adds In-App without a second email. Setting ownership true makes the new module the single owner for subsequent events, while historical ownership remains unchanged. New booking emails retain the reservation/event/address idempotency identity, including suspension cancellation keys. Existing pending uncorrelated outbox rows continue and are not reconstructed. Governance, Reception invitation and security/account emails retain their existing owners.

Perform recipient compatibility review and staging verification before cutover. Historical contacts whose verification evidence was already purged fail closed for new channels; do not fabricate verification. Independently approved clinical email and In-App release gates remain closed until explicitly released.

Rollback is operational feature disable and forward repair. Do not apply a destructive Down migration after notifications/audits exist. Preserve committed source data, notification/outbox history, settings audits, contact evidence and pending delivery evidence. No automatic notification purge or retention job was added.

## Templates and observability

Every approved event uses its event code plus explicit template version `1`. Rendering is code-controlled, Arabic by default with English preference. Booking render input is limited to a reference and committed local date/time; variables are encoded, control characters removed, and email subjects are code-controlled. Clinical bodies/previews are generic. Existing legacy email bodies are preserved. Queued email stores its rendered body/version, and unsupported inbox template versions are excluded safely. Future releases must retain supported historical template rendering.

Structured lifecycle logs/counters use operation, event type, channel, status and safe reason only. `Wasla.Notifications.Lifecycle` exposes `notification_events_total` and `notification_dispatch_lag_seconds`; `Wasla.Notifications` exposes `notification_dispatch_total`. Capture logs use status `Staged` because an enclosing command may still roll back; durable tables are authoritative. No patient ID, source ID or email is a metric dimension. Admin retry decisions/settings/preferences also have persisted audit evidence.

Monitoring exposes delivery/event/category/channel/status/times/attempt count/safe code/version only. It omits source/patient/recipient identifiers, addresses, subjects, bodies, access tokens and clinical provenance. RetryEligibility is an indication of a supported failed state; actual retry always performs current source/contact/channel revalidation. A retry cannot substitute a destination, body or event.

## Independent release gates

Engineering evidence is recorded separately in the Phase 17 report. Passing tests is not production approval. Remaining independent gates include clinical/legal communication release, contact communication compliance, production SMTP credential/configuration verification, notification/contact-evidence retention and disposal policy, staging verification, backup/recovery rehearsal and formal deployment approval. No production migration, cutover, clinical sharing activation, purge or deployment is performed by this branch.
