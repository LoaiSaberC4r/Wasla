# Current rollout — owner-approved activation target

The 2026-11-08 owner decision supersedes the disabled-feature target below.
All in-scope Production flags are true. Approved product-policy metadata is
integrated in the repository Master Source of Truth and Production appsettings.
Startup migrations/seeding remain false. Actual host deployment is unverified.
Use [the current deployment/backup/rollback runbook](../../deploy/family-production/README.md)
and [current closure evidence](CLOSURE-REPORT-2026-11-08.md).

## Archived first-checkpoint rollout

# Family self-service rollout / release gates

Production deployment is not authorized. New operational paths default disabled.
The missing master artifact and signed medical policy have not been fabricated.

| Runtime state | Operational dependent access | Medical access / new uploads |
|---|---|---|
| DependentManagement.Enabled=false | new operational proof policy denies | existing signed clinical policy remains independently enforced |
| Operational=true, medical sharing/policy=false | exact reviewed under18 pair may use implemented operational routes | child medical sharing remains denied |
| Same Family, no exact operational proof | denied even with CanManageFamily | membership cannot substitute for clinical proof |
| Operational proof revoked or child18 | new dependent operational access denied; history retained | existing clinical rules independent |
| Scanner required, no real scanner registered | upload validation fails closed | no bytes accepted as scanned |
| Scanner enabled, daemon error/timeout/unsafe | upload validation rejects | no clean verdict inferred |
| Signed medical policy absent | no effect on a valid operational proof | child medical release remains blocked |

Safe deployment settings (standard ASP.NET Core appsettings/environment overrides):

```json
{
  "DatabaseInitialization": {
    "ApplyMigrationsOnStartup": false,
    "ApplySeedingOnStartup": false
  },
  "DependentManagement": {
    "Enabled": false,
    "OperationalTimeZoneId": "Africa/Cairo",
    "CandidateReferenceTtlMinutes": 10,
    "SearchMaxCandidates": 5,
    "MaxEvidenceFiles": 5,
    "MaxEvidenceFileBytes": 10485760
  },
  "MedicalRecord": {
    "GuardianSharingEnabled": false,
    "CrossDoctorSharingEnabled": false
  },
  "FamilyMedicalPolicy": {
    "Approved": false,
    "MasterSourceOfTruthAdopted": false
  },
  "HistoricalDocuments": {
    "Enabled": false,
    "RequireRealScanner": true,
    "MaxFiles": 5,
    "MaxFileBytes": 10485760,
    "MaxDocumentsPerSubject": 500,
    "MaxBytesPerSubject": 1073741824,
    "MaxWritesPerDay": 20
  },
  "PrivateMediaReconciliation": {
    "Enabled": false,
    "IntervalHours": 24,
    "StaleStagingHours": 24,
    "MaxFilesPerModule": 10000
  },
  "MediaStorage": { "RequireMalwareScanner": true },
  "MalwareScanner": {
    "Enabled": false,
    "Host": "localhost",
    "Port": 3310,
    "TimeoutSeconds": 30,
    "MaxStreamBytes": 10485760
  }
}
```

The adapter implements ClamD's framed INSTREAM protocol. Configure a real local
daemon with current signatures and matching StreamMaxLength. The unencrypted,
unauthenticated TCP transport is restricted to loopback; a remote scanner requires
a locally terminated authenticated tunnel. See the [official protocol](https://docs.clamav.net/manual/Usage/ClamdProtocol.html).
Protocol test servers are not malware-scanning verification. Provisioning, clean
and safe test-signature scans, outage/readiness monitoring and signature freshness
remain mandatory operational checks.

Before any pilot: review additive migration SQL and old permission-ID snapshot;
rehearse backups and restore with sanitized data; apply schema and additive
permission seeding through the controlled pipeline while disabled; verify private
media filesystem ACLs/storage and audit/outbox health; validate actual scanner;
coordinate security breaking frontend redactions. No active proof is backfilled
from historical membership by migration. Existing exact cases need explicit
reviewed verification/reconciliation.

Credentials: `credential-review.json` identifies five tracked configuration key
locations without values. Treat any active values and history as compromised.
An authorized operator must rotate/revoke DB, SMTP, JWT, password-reset HMAC and
root bootstrap credentials where active; configure secure overrides under the
same key names; then remove tracked values in a coordinated rollout. This branch
does not assert external rotation or history remediation. Do not deploy relying
on a silent secret substitution. Do not print values in tickets or evidence.

The branch now includes contact/photo writes, historical documents and Timeline, dependent proxy diagnostics and finance. Governance review, invitations and reconciliation are in a separate validation checkpoint. Read the extension/governance checkpoint and TEST-EVIDENCE.json for the exact tested revision; a running, skipped or failed check is not a release gate pass. Enable only validated features after the remaining SQL, staging and operator checks. Keep child medical flags off without signed policy. HistoricalDocuments.Enabled controls the own/dependent historical module independently; child writes additionally require the signed medical policy and their own write permission.

Rollback: disable DependentManagement, retain additive schema, evidence, requests
and histories. Do not drop history/proof tables or run a destructive Down. Do not
roll back public APIs to an older binary that authorizes dependents by membership
alone. Maintain containment until the older application has equivalent scope
checks. Proof revocation denies future use without cancelling historical bookings
or deleting Patients/clinical/financial records.

Media reconciliation is disabled by default. The count-only worker checks HistoricalDocuments, FamilyGovernance and ChildOnboarding private folders against immutable database references. It skips filesystem links, bounds work, reports an incomplete scan explicitly, and emits no file keys, paths, PatientIds or names. Stale staging age is a discrepancy-detection threshold, not an approved retention duration. It never deletes or changes files or medical metadata.

Enable the worker only after verifying the configured private storage root and database permissions in staging. Observe wasla.private_media.reconciliation.runs/failures, stale_unreferenced_candidates and missing_references on the Wasla.PrivateMedia meter. Alert on failures, missing files and incomplete scans; incomplete scans require an operator to increase the bound or investigate the storage inventory. Logs contain aggregate counts only.

For a stale candidate, an authorized operator must establish the transaction outcome and search all retained/versioned evidence references using a private maintenance session before any repair. Keep unknown outcomes quarantined privately; never delete a retained blob, a legal-hold item or an uncertain-commit file. For a missing reference, restore the exact bytes from protected backup and verify recorded size/hash where available; do not fabricate replacement document content or change ownership. Rerun inspection and record the authorized repair outside generic logs. No purge schedule or general medical access is approved here.

Invitation replays rely on ASP.NET Core Data Protection. Persist and protect a shared key ring across application nodes, restrict storage ACLs and retain keys for live invitations. Loss of the key ring fails replay closed. Private references expire after 30 minutes and may be revoked by their issuer; no invitation email is sent automatically.
