# Phase 5 final database migration plan

Migration: `20261018140120_Phase5FinalPatientIdentityClosure`. Previous HEAD migration: `20261014191455_Phase18PartTwoArchiveProgress`. Generated from WaslaDbContext using EF Core10 SQL Server; prior committed migrations are untouched. SQL artifact: [PHASE-5-FINAL-MIGRATION.sql](PHASE-5-FINAL-MIGRATION.sql).

## Schema and indexes

| Object | Additions / constraints |
|---|---|
| Patients | Optional audit provenance RegistrationDoctorPracticeId/RegistrationReceptionApplicationUserId/ReceptionRegisteredOnUtc for the newly-created same-workflow exemption; no ownership relation. Persisted NormalizedEmail=LOWER(LTRIM(RTRIM(Email))); unique UX_Patients_NormalizedEmail. Blank non-null email check. Persisted normalized search names and DOB/gender search indexes. Existing PatientId, media, rowversion and nonunique phone index retained. |
| PatientContacts | Optional normalized ContactEmail<=200; no unique index, identity link or login authority |
| PatientOnboardingChallenges | Purpose/email/account binding, OTP hash/attempts/UTC expiry, continuation hash/action/expiry/consumption, rowversion, mutation replay response/fingerprint; unique nonnull continuation hash, email+purpose+created descending, expiry index |
| PatientClaimRequests | Identity/evidence revision, target/reference/approved target, source challenge, all lifecycle timestamps, reviewer, closed state, idempotency key+fingerprint, legal-hold setter/reason/time, rowversion; unique open confirmed target; unique open unresolved email; email+idempotency uniqueness; status+submitted/status+activity/legal-hold+closed indexes |
| PatientClaimEvidences | Claim+revision, types, private key/MIME/size/SHA256, uploader, sealed attestation reference, uploaded/retired/purged timestamps; FK/revision and tombstone indexes |
| PatientClaimHistories | Immutable revision/state/action/actor/time/reason/safe metadata; claim+occurred+ID index; append-only tracked writes |
| PatientActivationInvitations | Purpose/account-or-claim/email/hash/created/expiry/consumed/revoked/sendcount; unique token hash and resend key; one unconsumed/unrevoked invitation per bound account or claim; email+created index |
| PatientDuplicateCases | Distinct stored canonical GUID pair, safe indicator/status/actor/reason/time/rowversion; unique open pair; non-self check; status index |
| PatientIdentityAttestations | Bound Patient/practice/Reception/basis/purpose/email/UTC expiry/optional reservation or ticket and consumption time; rowversion and practice+Patient+staff+expiry index |

All identity FKs use Restrict/NoAction; no cascade into clinical/Family data. Existing account-email, username and both AccountLink unique indexes remain. Claim approval stamps the selected target into TargetPatientId AND ApprovedTargetPatientId so the open-target index applies even to previously unresolved intake. Open unresolved requests are limited by normalized applicant email. SQL transaction locking adds the cross-index application-level checks. Invitations remain in the live slot after timeout until controlled resend revokes them; no nondeterministic GETUTCDATE filter is used.

SQL Server does not allow a computed column in a filtered-index predicate. Therefore the unique index filters **[Email] IS NOT NULL**, semantically equivalent to NormalizedEmail IS NOT NULL under the persisted expression and blank-email constraint. Both application and SQL normalize case/outer spaces. Multiple null emails, repeated patient phones and repeated ContactEmails remain valid. Canonical duplicate pairs use ordinal GUID hex ordering stored explicitly because SQL uniqueidentifier sorting differs from .NET.

## Safe preflight and rollout

The migration first THROWs51050 if non-null blank or duplicate normalized identity emails exist. It performs no automatic cleanup, UPDATE, DELETE, merge or ID rewriting. Up adds seven tables, nullable/additive fields, computed columns, checks, FKs and indexes only. The generated Up SQL has no DROP TABLE/COLUMN, TRUNCATE, DELETE or destructive UPDATE of old tables. A failing preflight leaves the incompatible database for operator review. Do not use Down to erase used identity history; prefer a controlled roll-forward repair. Standard generated Down removes the new schema and must never be run against used data without a separate approved recovery plan.

```powershell
# Inspect and apply through the approved operator connection/secret provider.
dotnet ef migrations has-pending-model-changes --project src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer --startup-project src/Wasla/Wasla.Api --context WaslaDbContext --configuration Release --no-build
dotnet ef database update --project src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer --startup-project src/Wasla/Wasla.Api --context WaslaDbContext --configuration Release --no-build
```

Review migration SQL, back up any non-disposable database, rehearse on an isolated SQL Server and verify indexes/rowversions before controlled application. Run security seeding using existing startup/operator conventions after schema deployment. Test data uses disposable GUID databases created by existing fixtures and integrated local SQL Express authentication. No real contacts, credentials or medical documents are used. No development database was reset and no reset code was added.

If synthetic legacy collisions exist, **only the operator may explicitly designate an exact Development/Test database disposable and recreate it**. Safer default: choose a new empty disposable DB/catalog in the operator connection and apply the full migration lineage. Keep the previous catalog until the operator independently authorizes retirement. Do not paste a generic destructive drop command or point a fixture at an existing catalog. Test fixtures independently check their generated database prefixes before deleting only their own databases. The API never implicitly EnsureDeleted/clears data.

## Retention and operator legal hold

PatientIdentity defaults: OTP10m/5attempts/60s, continuation30m, invitation48h, inactivity90d, evidence90d post-close, audit24months, reuse15m, expired-challenge cleanup24h; bounded worker50 items every300s. EvidencePurgingEnabled=false and AuditPurgingEnabled=false in source defaults/settings. Expiry checks apply even when cleanup is disabled. Reviewed deployments may increase retention; production legal/privacy approval must precede enabling sensitive purge flags.

Worker transactions use the shared app lock on every node, indexed batch paging, durable evidence-retirement tombstones, then a second legal-hold check before private storage deletion. Failure retains the key and unpurged marker for retry. Audit aggregate retirement requires elapsed24months, no hold and all evidence purged; current AccountLink and Patient/medical records remain. Metrics and safe failure logs support monitoring.

Legal holds have no HTTP route. Provision maintenance connection and a fresh active SuperAdmin identifier/password through the approved process environment/secret provider (`WASLA_IDENTITY_MAINTENANCE_CONNECTION`, `WASLA_IDENTITY_OPERATOR_IDENTIFIER`, `WASLA_IDENTITY_OPERATOR_PASSWORD`). The operator needs current PatientClaims.Approve AND PatientClaims.ViewEvidence. Invoke:

```powershell
dotnet run --project tools/Wasla.PatientIdentityMaintenance -- hold <claim-guid> "Documented lawful hold reason"
dotnet run --project tools/Wasla.PatientIdentityMaintenance -- release <claim-guid> "Documented authorized release reason"
```

Do not put passwords in command arguments/history or source. Existing secure HMAC/media/runtime options must also be supplied through the operator environment. Invalid credentials/permissions deny; successful changes append LegalHoldPlaced/Released history. Production policy authorization, lawful obligations, monitoring, backlogs, recovery and deployment approval remain separate from product retention defaults.

Native test coverage and exact run counts are in the closure report, including baseline-to-HEAD migration, normalized-index rejection, rowversion, shared null/contact/phone behavior, registration/approval/activation/duplicate races and invitation/link constraints. `has-pending-model-changes` currently reports clean; repeat after any schema edit.
