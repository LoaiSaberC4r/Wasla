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

Enable only implemented operational features after E2E and SQL checks. Keep
medical flags off. Contact/photo writes, identity correction review, historical
documents/timeline, dependent proxy diagnostics and independent finance require
their remaining slices; no setting in this document implements them.

Rollback: disable DependentManagement, retain additive schema, evidence, requests
and histories. Do not drop history/proof tables or run a destructive Down. Do not
roll back public APIs to an older binary that authorizes dependents by membership
alone. Maintain containment until the older application has equivalent scope
checks. Proof revocation denies future use without cancelling historical bookings
or deleting Patients/clinical/financial records.

Media reconciliation: uncertain transaction outcomes retain staged files. An
operator must first establish transaction outcome and verify all onboarding and
family evidence references before treating a blob as orphaned. Never delete a
retained evidence blob on an ambiguous SQL commit. Automated reconciliation and
operational orphan metrics are not represented as complete by this slice.
