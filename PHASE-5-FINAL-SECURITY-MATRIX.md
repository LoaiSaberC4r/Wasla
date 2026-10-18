# Phase 5 final security matrix

Approved 2026-10-18. Acceptance evidence and unresolved checks are in the closure report.

| Actor | Routes | Permitted identity data | Required live checks | Medical authority |
|---|---|---|---|---|
| Anonymous unverified | N03/N04; N09 invitation; neutral Full N10 | Neutral request receipt; continuation only after valid email OTP; sanitized activation IDs | IP limiter, per-email/purpose cooldown, attempts/expiry; invitation hash/replay | None |
| Verified applicant | N05/E01, N06–N08, Claim N10 | Own submitted Claim status/reason; no matched patient details | HMAC token lookup, exact normalized email, server AllowedAction, mutation consumption, 30m expiry, ownership | None until committed activation and normal Login |
| Reception | E02/E03, N01/N02, existing booking/Walk-In | Minimal names/DOB/gender/opaque PatientId; masked email/phone; presence of contact phone | Active user/Reception/Doctor/Practice, current assignment, role catalog AND delegated practice permission | No other doctor's records; reservation/search/attestation is not clinical consent |
| Active linked Patient | E04/E05/E06, EmailChange N03/N04 | Own profile/contact data only | Immutable user ID -> live active Patient snapshot -> AccountLink; own persisted permission; profile rowversion | Existing clinical policy only |
| Permissioned SuperAdmin | N11–N17 | Minimal identity candidates, applicant evidence and decision history | Active effective UserType, SuperAdmin profile/role, endpoint granular current DB permission; evidence belongs to request and remains unpurged | Identity review only, no global clinical shortcut |
| Permissioned SuperAdmin | N18–N20 | Duplicate identity summaries; safe indicators/reasons | View/Resolve permission, current rowversion, open state, mandatory reason | Classification does not merge/move medical records or alter access |
| Verified operator | Maintenance CLI hold/release | Named claim legal-hold metadata | Fresh password, active SuperAdmin profile/role, Approve + ViewEvidence permissions; transaction-owned identity lock | None |
| Retention worker | Internal bounded batches | Claim/evidence tombstones and safe metadata | Shared SQL lock, recheck legal hold, elapsed UTC retention; purge flags explicitly enabled | Cannot delete Patient, AccountLink, Family, clinical, booking, payment or queue records |

## Permissions and compatibility

Ten permissions append after all 303 earlier permission entries, including the full Phase18PartTwo tail. Existing deterministic GUIDs are frozen by `Phase5ExistingPermissionIds.txt` and tested against every name. Full/RegistrationMatch appear in the Reception role catalog and assignment delegation catalog, but new assignments do not acquire them without delegation. Eight Claim/duplicate grants are seeded for SuperAdmin; no ordinary Reception/Doctor/Patient receives review authority. Seeder conflict checks and idempotent existing mappings remain.

All new controllers use existing `[Permission]` policies and handlers recheck live persisted access. Onboarding proof is opaque, not a JWT. One effective UserType remains enforced; no role/context conversion or alternate authorization framework exists. Pending Full users cannot Login/Me or escape activation through password recovery. Claim submission and approval create no authenticated account/link. Full's early link points only to a disabled account. Normal activation emits no JWT.

## Secrets, privacy and proof

OTP is HMAC hashed using a dedicated patient-identity domain separator including challenge ID, email and purpose. Defaults: six digits, 10m, five attempts, 60s cooldown; wrong-attempt state commits even when the API returns rejection. The existing password-reset protector supplies crypto primitives only; reset tokens do not redeem onboarding. Continuation and activation tokens use different hash domains. Continuation lasts30m and one mutation; a consumed token can still read its applicant's status until expiry. Invitation lasts48h, is one-use, and resend revokes the previous row. Comparison/OTP verification uses the existing constant-time protector. Exact expiry boundaries fail closed via IDateTimeProvider.

N03 follows the same202 response shape for known/new/operational-account emails; lookup outcomes appear only after correct OTP in N04. Per-IP limiting and per-email/purpose durable challenge rate controls apply. Public status returns indistinguishable404 for wrong identity or guessed GUID. No SQL errors or private keys are returned. Verification tokens are the intentional N04 response secret; raw OTP/invitation/password never appears in API output or logs. Email content necessarily contains the delivery secret in the existing transactional outbox; protect that database and SMTP transport operationally.

All evidence uses the existing private media service, MIME/signature/size validation and configured malware-scanner abstraction. Evidence file content is SHA256 fingerprinted. API streams only matching unretired evidence with no-store and a safe name; it never returns a storage key or permanent public URL. Access uses existing ArchiveAuditEvent metadata. The current bundled scanner may be NoOp unless an actual scanner is provisioned: production scanner provisioning is an explicit release/security gate, not evidence that malware scanning occurred in local tests.

New media compensation wraps the transaction behavior and checks retained references in a fresh scope after an uncertain commit. A cleanup failure is logged without PII for reconciliation. Do not claim durable orphan-file reconciliation beyond the existing retained-reference compensation pattern; see closure blockers if exhaustive fault-injection/reconciliation is not verified.

## Minor and family boundaries

Reception Basic accepts minors with a valid primary guardian/contact phone. Direct new Self/Full is adult-only. Under16 receives no standalone account. Age16–17 must already have a Basic profile and pass independent identity/DOB evidence and explicit manual SuperAdmin review. A parent's ContactEmail can repeat across children and proves no ownership or medical entitlement. Claim never creates Family membership, treating-doctor access or guardian clinical eligibility. Existing age16 cutoffs, hidden encounters, reviewed guardian eligibility and effective fail-closed policy gates remain unchanged.

## Transactions and audit

The BuildingBlock WaslaWritePersistence transaction owns all entity, idempotency and EmailOutbox writes. No SMTP occurs before commit. A transaction-owned SQL Server application lock `Wasla:PatientIdentity` provides a conservative fixed order across identity writers and attestation consumption; identity writers never acquire booking locks. Existing booking locks are acquired first. The gate is safe but serializes unrelated identity writes; load/timeout acceptance remains necessary. Unique indexes and native rowversion arbitrate final races. Errors map to stable409. Retention tombstones commit before physical deletion and keep keys until successful retry; legal hold is rechecked under the same lock. Claim history rejects tracked modification/deletion; only the separate elapsed-retention aggregate purge may delete old history.

Append-only claim history records evidence revisions, reviewed IDs, decisions and actors. Duplicate resolution adds a safe classification audit with reason hash. Identity search and evidence reads audit actor/practice/purpose without clinical content. Metrics count onboarding operation/outcome and retention work without identity labels. Purging defaults off; production privacy/legal policy, scanner, mail transport, secure secret provisioning, monitoring and deployment rehearsal remain release gates.
