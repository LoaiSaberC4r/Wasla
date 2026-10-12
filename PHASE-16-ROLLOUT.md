# Phase 16 rollout and release gates

Production release is **not approved**. The proposed addendum does not amend the
master. Cross-doctor consent, clinical safety alerts, patient identity claim and
deduplication closure, audit retention/disclosure, and guardian privacy require
formal adoption and sign-off. The full authoritative v1.2 master is absent from
this checkout. There is no automatic patient matching or merge.

## Configuration

`MedicalRecord:CrossDoctorSharingEnabled` and
`MedicalRecord:GuardianSharingEnabled` default **false** in appsettings.json.
Patient-self and owning-doctor visibility features are independent of sharing.
Disabled sharing returns the same `404 MedicalRecord.Unavailable` before any
resource lookup. Reception, administrators and catalog managers receive no new
clinical permission by default and have no role bypass.

Cross-doctor development enablement additionally requires a cryptographically
strong, stable secret of at least 32 characters in
`MedicalRecord:OpaqueEntryKey`. Supply it through a secret store or environment
configuration; do not commit it. It protects stable, non-enumerable hidden entry
identifiers. Placeholder tokens and cursors use authenticated ASP.NET Core Data
Protection under the stable application name `Wasla.Phase16`, with separate
purposes. Placeholder tokens last 15 minutes and are bound to user, doctor,
ticket, source encounter and visibility revision. They never replace DB policy.

Multi-instance deployments must share a persistent, access-controlled and
appropriately encrypted Data Protection key ring. An optional
`MedicalRecord:DataProtectionKeyRingPath` selects a managed shared directory.
Review at-rest key protection and recovery before activation. Key loss prevents
old tokens/cursors from being read. Opaque-entry secret rotation requires the UI
to reload timelines. No production secret is included in this branch.

Guardian routes remain **inactive even if their flag is set true**. The exact
legal/relationship/minor-data policy is not approved. Their handlers fail closed;
they do not interpret booking eligibility as medical access. Implement that
approved policy and under-16 per-read checks in a separate release package.
Existing under-18 booking behavior remains in the original family policy.

## Migrations and mixed deployment

Apply in order through a reviewed migration artifact:

1. `20261012101718_Phase16EncounterVisibility`: conservative SQL default Hidden,
   revision zero, allowed-value check, immutable visibility history. Existing
   encounters receive `LegacyMigration` baseline history with no actor. The
   constant default avoids a clinical UPDATE backfill. New code explicitly sends
   Visible; old application inserts conservatively remain Hidden.
2. `20261012102626_Phase16HiddenEncounterAccess`: request aggregate, SQL rowversion,
   restrictive FKs, all-status unique target/ticket index and immutable history.
3. `20261012103620_Phase16MedicalRecordAudits`: metadata-only read audit table and
   patient/time and actor/time indexes.

Back up first; rehearse against representative data; keep sharing disabled;
deploy schema before new application instances; do not enable sharing while
old instances lack the Phase 16 lock protocol; run the existing repeatable
security seed through controlled initialization; verify flags, key ring and audit
availability; require clients to refresh concurrency versions after deployment.
No production migrations or seeds were executed by this task. Startup migration
settings were not enabled. Existing migration/seed controls remain in force.

For rollback, disable sharing and revert application code while retaining the
additive schema. Down migrations discard visibility choices, requests and read
audits; they are destructive and are only used in disposable migration tests.
They preserve the preexisting clinical tables but are not a production recovery
procedure. Preserve the audit/history backup before any schema removal.

## Security and consistency

Creation/decision/revocation use the same SQL transaction applock namespace as
ticket completion/cancellation: treatment lock before source lock. Decisions
re-read current scope after locking. Visibility updates serialize on the source
lock. Rowversions protect owner mutations. Completion materializes Pending and
Approved requests as Expired inside its transaction. Every record/media read
checks current actor permissions, live matching ticket/encounter and current
source visibility/revision; effective expiry does not depend on cleanup.

Timeline SQL projects only safe fields from authorized completed encounters.
Its hidden branch selects business date and request metadata without joining
clinical output tables. Clinical events sort by UTC timestamp descending, type
ordinal, then lowercase GUID text; hidden events sort at business-date midnight
with opaque public entry identifiers. Page size is 1–100. Protected cursors bind
scope, filters, a one-hour upper time bound and a current-state watermark.
Clinical corrections, visibility/grant changes or source mutations invalidate
cursors; clients restart at page one. There is no stale clinical snapshot cache.

Successful overview, timeline, encounter, item, media, request-list/detail and
access-log disclosure writes metadata audit before returning data. Failed audit
persistence fails closed. Request and visibility mutations retain their own
immutable histories. Inbox badge counts contain no PHI. Existing source-module
audits remain in those modules; the new patient access-log currently displays
Phase 16 read audits, not a historical export of every pre-Phase 16 audit table.

Private downloads re-resolve exact current result/version/attachment ownership,
recheck policy, restrict size to 25 MiB, sanitize filenames, verify stored size,
close streams on audit failure, and return no-store/nosniff attachment responses.
They provide no public URLs or storage keys. Filesystem failures disclose no path.
Already authorized transfers may finish; new transfers after committed revocation
or completion are denied. Clients close old pages on completion and refetch after
mutations; previously downloaded bytes cannot be remotely recalled.

Rate limits are per authenticated actor (IP fallback): general reads 1,200,
downloads 120, request creation 60 per 15 minutes. No queue is configured; excess
requests return 429. Generic request logging redacts safe clinical content,
reasons, patient headers, tokens, cursors and opaque-key configuration.

## Observability

The Wasla.MedicalRecord meter exposes denied/read counts, read latency, page-size
distribution, audit-write failures and request operation/status counters. Labels
contain only mode/operation/status values. These operation counters may include
retries/no-op commands and are emitted before outer commit; immutable history is
the canonical source for committed approval/rejection/revocation/expiry totals.
Existing sanitized Result logging records denied attempts by code/trace without
request payload; rate limits bound repeated probes.

## Operational boundaries

In-app inbox and modest polling are authoritative. No new email notification or
SignalR dependency is introduced. No emergency override, medical-center group
sharing, patient deduplication/merge, frontend implementation or production
deployment is included. The synthetic 1,000-visit SQL check and its query plan
are local evidence, not a production capacity guarantee. Production-scale load,
retention and operational key/audit recovery require deployment review.
