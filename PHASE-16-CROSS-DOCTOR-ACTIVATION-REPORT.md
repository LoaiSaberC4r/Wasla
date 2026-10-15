# Phase 16 Cross-Doctor Sharing activation

Date: 2026-10-15 (Africa/Cairo). Branch:
`codex/phase16-cross-doctor-sharing-activation`.

## Result

Final activation verification: **PASS**. Production release: **FAIL / BLOCKED**
until its separate release gates are approved. No deployment was performed.

Only configuration, focused activation tests and rollout documentation changed.
Existing Doctor-to-Doctor policies, endpoints, consent and business flows are
unchanged. Family Medical Sharing was not activated.

| Configuration | Cross-Doctor | Guardian |
|---|---|---|
| Base appsettings.json | true | false |
| Development (inherits base) | true | false |
| Production override | false | false |
| Staging override | false | false |
| .env.example | false | false |

The tracked deployment configuration inventory contains Production and a folder
publish profile; Staging is explicitly protected as well. Custom deployment
environments must supply `MedicalRecord__CrossDoctorSharingEnabled=false` until
their own gates are approved. Environment variables override JSON, so deployed
values must also be reviewed before release.

## Secret configuration

The previously absent development `MedicalRecord:OpaqueEntryKey` was provisioned
as 32 cryptographically random bytes encoded as 64 hexadecimal characters in the
existing `Wasla.Api` .NET user-secrets store, outside the repository. Its value
was not printed or added to tracked files. No Production secret was provisioned.

The API's existing `IOptions<MedicalRecordOptions>` registration binds the
`MedicalRecord` section. Activation tests supply a randomly generated test key
through the environment configuration provider and exercise the real API without
mutating the options object. Missing and short keys deny a live treating Doctor.
Deployments must inject `MedicalRecord__OpaqueEntryKey` from their own approved
environment/secret store; `.env.example` contains no key value.

## Verification

- Before activation: **25 existing focused integration cases PASS**, including
  **9 real SQL Server cases**, zero failed/skipped.
- Focused existing unit tests: **5 PASS**, zero failed/skipped.
- Existing Phase 16 architecture check: **1 PASS**, zero failed/skipped.
- Final integration run after activation: **32 PASS**, including **10 real SQL
  Server cases**, zero failed/skipped. This includes the 25 existing cases and
  7 new configuration/activation cases.
- Final verification covers **38 distinct cases** (32 integration + 5 unit +
  1 architecture); repeated baseline runs are not added to that total.

Coverage includes Visible/Hidden access, request/approve/reject/revoke,
idempotency, actor/ticket/patient/visibility scope, token expiry, permission
revocation, completion expiry, concurrent approval/completion, private media
revocation and audit failure. The added SQL scenario verifies configuration-bound
activation, visible access, hidden denial, approved access, atomic Ticket
completion, persisted Expired status and subsequent 404 responses for overview,
timeline and encounter reads.

SQL Server 2022 (16.0.1000.6), using Windows integrated authentication, ran the
existing fixtures' isolated, migrated disposable databases. No application or
Production database migration/seeding was performed. Family and thousand-visit
performance suites were excluded from this focused activation run.

Reproduce with `WASLA_SQLSERVER_CONNECTION_STRING` supplied securely:

```powershell
dotnet test tests/Wasla.Tests.Unit/Wasla.Tests.Unit.csproj --filter 'FullyQualifiedName~Phase16VisibilityTests|FullyQualifiedName~Phase16HiddenAccessTests'
dotnet test tests/Wasla.Tests.Architecture/Wasla.Tests.Architecture.csproj --filter 'FullyQualifiedName~Phase16MedicalRecordArchitectureTests'
dotnet test tests/Wasla.Tests.Integration/Wasla.Tests.Integration.csproj --filter 'FullyQualifiedName~Phase16&FullyQualifiedName!~Family&FullyQualifiedName!~Performance'
```

Local TRX evidence is under ignored `TestResults/phase16-activation/`.

## Remaining Production release blockers

The existing [rollout gates](PHASE-16-ROLLOUT.md) remain in force:

1. Formal adoption/sign-off for cross-doctor consent, clinical safety alerts,
   patient identity/deduplication and audit retention/disclosure; the full
   authoritative v1.2 master remains absent from this checkout.
2. Production-specific opaque-key provisioning and rotation/recovery controls,
   plus a shared, persistent, access-controlled and appropriately encrypted Data
   Protection key ring for all instances.
3. Approved migration/backup rehearsal, repeatable security seed, compatible
   application instances using the existing lock protocol, audit availability
   and recovery, and representative production load/retention verification.
4. Deployment owner approval and verification of the effective environment
   overrides before separately enabling sharing. Guardian sharing stays false.
