# Phase 5 Final Closure Report

Status: **Phase 5 IN-PROGRESS**. Approval2026-10-18. Branch `codex/phase5-final-closure`; fresh main/origin/main baseline `17c9e0b96af87de0143ca4ac4978bc970ca6f9dd`. Product code, migration, tests and handoff are implemented; final acceptance is not asserted until every gate below is verified. This is not a production-readiness declaration.

## 1. Business implementation outcome

The task provides grouped P5 decision descriptions rather than a full individual historical decision text. The table preserves every ID and maps to those authoritative groups; it does not invent missing old wording. Original P5-09 and P5-23 nonunique-email outcomes remain SUPERSEDED by P5-D01.1 in the approved addendum.

| Decision ID | Outcome | Implemented rule / remaining gate |

|---|---|---|

| P5-01 | Implemented | Basic without User; adult scoped Full creates disabled invitation-only credentials; existing Basic uses Claim. E02/N01. |

| P5-02 | Implemented | Basic without User; adult scoped Full creates disabled invitation-only credentials; existing Basic uses Claim. E02/N01. |

| P5-03 | Implemented | Basic without User; adult scoped Full creates disabled invitation-only credentials; existing Basic uses Claim. E02/N01. |

| P5-04 | Implemented | Basic without User; adult scoped Full creates disabled invitation-only credentials; existing Basic uses Claim. E02/N01. |

| P5-05 | Implemented | Basic without User; adult scoped Full creates disabled invitation-only credentials; existing Basic uses Claim. E02/N01. |

| P5-06 | Implemented | Basic without User; adult scoped Full creates disabled invitation-only credentials; existing Basic uses Claim. E02/N01. |

| P5-07 | Implemented | Basic without User; adult scoped Full creates disabled invitation-only credentials; existing Basic uses Claim. E02/N01. |

| P5-08 | Implemented | Exact normalized email hard collision; soft matching/attested override; classification only and scoped same-Patient reuse. E03/N02/N18–N20. |

| P5-09 | Implemented | Superseded original outcome replaced by P5-D01.1 normalized unique identity email; soft multiple matches remain supported. |

| P5-10 | Implemented | Exact normalized email hard collision; soft matching/attested override; classification only and scoped same-Patient reuse. E03/N02/N18–N20. |

| P5-11 | Implemented | Exact normalized email hard collision; soft matching/attested override; classification only and scoped same-Patient reuse. E03/N02/N18–N20. |

| P5-12 | Implemented | Exact normalized email hard collision; soft matching/attested override; classification only and scoped same-Patient reuse. E03/N02/N18–N20. |

| P5-13 | Implemented | Exact normalized email hard collision; soft matching/attested override; classification only and scoped same-Patient reuse. E03/N02/N18–N20. |

| P5-14 | Implemented | Exact normalized email hard collision; soft matching/attested override; classification only and scoped same-Patient reuse. E03/N02/N18–N20. |

| P5-15 | Implemented | Exact normalized email hard collision; soft matching/attested override; classification only and scoped same-Patient reuse. E03/N02/N18–N20. |

| P5-16 | Implemented | Verified email plus independent identity evidence and granular manual SuperAdmin review; immutable revisions; original-Patient activation. N06–N17. |

| P5-17 | Implemented | Verified email plus independent identity evidence and granular manual SuperAdmin review; immutable revisions; original-Patient activation. N06–N17. |

| P5-18 | Implemented | Verified email plus independent identity evidence and granular manual SuperAdmin review; immutable revisions; original-Patient activation. N06–N17. |

| P5-19 | Implemented | Verified email plus independent identity evidence and granular manual SuperAdmin review; immutable revisions; original-Patient activation. N06–N17. |

| P5-20 | Implemented | Verified email plus independent identity evidence and granular manual SuperAdmin review; immutable revisions; original-Patient activation. N06–N17. |

| P5-21 | Implemented | Verified email plus independent identity evidence and granular manual SuperAdmin review; immutable revisions; original-Patient activation. N06–N17. |

| P5-22 | Implemented | Verified email plus independent identity evidence and granular manual SuperAdmin review; immutable revisions; original-Patient activation. N06–N17. |

| P5-23 | Implemented | Superseded original outcome replaced by P5-D01.1 normalized unique identity email; soft multiple matches remain supported. |

| P5-24 | Blocked | OTP/continuation/invitation purpose, expiry, attempts, live authorization, privacy and transactional outbox implemented. Exhaustive security/fault acceptance and deployment scanner provisioning remain explicit gates. |

| P5-25 | Blocked | OTP/continuation/invitation purpose, expiry, attempts, live authorization, privacy and transactional outbox implemented. Exhaustive security/fault acceptance and deployment scanner provisioning remain explicit gates. |

| P5-26 | Blocked | OTP/continuation/invitation purpose, expiry, attempts, live authorization, privacy and transactional outbox implemented. Exhaustive security/fault acceptance and deployment scanner provisioning remain explicit gates. |

| P5-27 | Blocked | OTP/continuation/invitation purpose, expiry, attempts, live authorization, privacy and transactional outbox implemented. Exhaustive security/fault acceptance and deployment scanner provisioning remain explicit gates. |

| P5-28 | Blocked | OTP/continuation/invitation purpose, expiry, attempts, live authorization, privacy and transactional outbox implemented. Exhaustive security/fault acceptance and deployment scanner provisioning remain explicit gates. |

| P5-29 | Blocked | OTP/continuation/invitation purpose, expiry, attempts, live authorization, privacy and transactional outbox implemented. Exhaustive security/fault acceptance and deployment scanner provisioning remain explicit gates. |

| P5-30 | Blocked | Grouped final closure/retention/security/API/permission/migration/regression evidence requirements: full DoD and fresh CI verification remain open. |

| P5-31 | Blocked | Grouped final closure/retention/security/API/permission/migration/regression evidence requirements: full DoD and fresh CI verification remain open. |

| P5-32 | Blocked | Grouped final closure/retention/security/API/permission/migration/regression evidence requirements: full DoD and fresh CI verification remain open. |

| P5-33 | Blocked | Grouped final closure/retention/security/API/permission/migration/regression evidence requirements: full DoD and fresh CI verification remain open. |

| P5-34 | Blocked | Grouped final closure/retention/security/API/permission/migration/regression evidence requirements: full DoD and fresh CI verification remain open. |

| P5-D01.1 | Implemented | Optional unique Patient identity email; nonunique phone and separate nonunique ContactEmail; existing PatientId reused without clinical escalation |

| P5-D01.2 | Implemented | Additive migration; preflight fails on collisions; no historical cleanup, automatic merge or app reset |

| P5-D01 | Implemented | Actual medical-record merge remains unsupported; duplicate classification cannot move clinical rows |

| P5-D02 | Implemented | One effective UserType; separate unique account email/credentials; UserRole schema retained |

| P5-D03 | Implemented | 90d inactivity, evidence90d, audit24m, durable tombstones/retry, legal hold CLI, purge flags off; legal/privacy release gate remains |

| DEC-020 / DEC-021 | Implemented | Reception minimum contact and existing Family/guardian privacy retained |

## 2. Route-by-route diff

[API changes](PHASE-5-FINAL-API-CHANGES.md) contains every E01–E06/N01–N20 method/path, actual CQRS operation/handler, permission/proof, request/response projection, examples, pagination, rowversions, idempotency, errors and coverage. Four new controllers publish exactly20 new operations; typed OpenAPI projections and headers are supplied. Existing legacy self-registration delegates to the same verified workflow. Existing booking and Walk-In accept one optional attestation field.

Owner-approved corrections: preserve `/api/v1/practices/{practiceId}/tickets/walk-in`; no move/alias. Verified Claim with no exact email/reference receives Claim-scoped continuation; N06 evaluates eligibility and evidence. No other business rule or route divergence is approved or intentionally introduced.

## 3. Changed-file inventory

### Domain

| Change | File |

|---|---|

| Modified | [Patient.cs](src/Wasla/Wasla.Domain/Patients/Patient.cs) |

| Modified | [PatientAccountLink.cs](src/Wasla/Wasla.Domain/Patients/PatientAccountLink.cs) |

| Modified | [PatientContact.cs](src/Wasla/Wasla.Domain/Patients/PatientContact.cs) |

| Modified | [ErrorMessage.ar.resx](src/Wasla/Wasla.Domain/Resources/ErrorMessage.ar.resx) |

| Modified | [ErrorMessage.cs](src/Wasla/Wasla.Domain/Resources/ErrorMessage.cs) |

| Modified | [ErrorMessage.resx](src/Wasla/Wasla.Domain/Resources/ErrorMessage.resx) |

| Modified | [SystemSecurity.cs](src/Wasla/Wasla.Domain/Security/SystemSecurity.cs) |

| Added | [PatientActivationInvitation.cs](src/Wasla/Wasla.Domain/Patients/PatientActivationInvitation.cs) |

| Added | [PatientClaimEvidence.cs](src/Wasla/Wasla.Domain/Patients/PatientClaimEvidence.cs) |

| Added | [PatientClaimHistory.cs](src/Wasla/Wasla.Domain/Patients/PatientClaimHistory.cs) |

| Added | [PatientClaimRequest.cs](src/Wasla/Wasla.Domain/Patients/PatientClaimRequest.cs) |

| Added | [PatientDuplicateCase.cs](src/Wasla/Wasla.Domain/Patients/PatientDuplicateCase.cs) |

| Added | [PatientIdentityAttestation.cs](src/Wasla/Wasla.Domain/Patients/PatientIdentityAttestation.cs) |

| Added | [PatientIdentityErrors.cs](src/Wasla/Wasla.Domain/Patients/PatientIdentityErrors.cs) |

| Added | [PatientOnboardingChallenge.cs](src/Wasla/Wasla.Domain/Patients/PatientOnboardingChallenge.cs) |

### Application

| Change | File |

|---|---|

| Modified | [DependencyInjection.cs](src/Wasla/Wasla.Application/DependencyInjection.cs) |

| Modified | [Authentication.cs](src/Wasla/Wasla.Application/Features/Auth/Authentication.cs) |

| Modified | [PasswordRecovery.cs](src/Wasla/Wasla.Application/Features/Auth/PasswordRecovery.cs) |

| Modified | [PatientManagement.cs](src/Wasla/Wasla.Application/Features/Patients/PatientManagement.cs) |

| Modified | [Registration.cs](src/Wasla/Wasla.Application/Features/Registration/Registration.cs) |

| Modified | [ReservationManagement.cs](src/Wasla/Wasla.Application/Features/Reservations/ReservationManagement.cs) |

| Modified | [CreateWalkIn.cs](src/Wasla/Wasla.Application/Features/Tickets/CreateWalkIn/CreateWalkIn.cs) |

| Added | [PatientIdentityLegalHold.cs](src/Wasla/Wasla.Application/Features/PatientClaims/PatientIdentityLegalHold.cs) |

| Added | [PatientIdentityReuseGuard.cs](src/Wasla/Wasla.Application/Features/PatientIdentityMatching/PatientIdentityReuseGuard.cs) |

| Added | [PatientClaimWorkflow.cs](src/Wasla/Wasla.Application/Features/PatientOnboarding/PatientClaimWorkflow.cs) |

| Added | [PatientIdentityContracts.cs](src/Wasla/Wasla.Application/Features/PatientOnboarding/PatientIdentityContracts.cs) |

| Added | [PatientIdentityMediaBehavior.cs](src/Wasla/Wasla.Application/Features/PatientOnboarding/PatientIdentityMediaBehavior.cs) |

| Added | [PatientIdentityOptions.cs](src/Wasla/Wasla.Application/Features/PatientOnboarding/PatientIdentityOptions.cs) |

| Added | [PatientIdentityReads.cs](src/Wasla/Wasla.Application/Features/PatientOnboarding/PatientIdentityReads.cs) |

| Added | [PatientIdentityUploadFingerprint.cs](src/Wasla/Wasla.Application/Features/PatientOnboarding/PatientIdentityUploadFingerprint.cs) |

| Added | [PatientIdentityWorkflow.cs](src/Wasla/Wasla.Application/Features/PatientOnboarding/PatientIdentityWorkflow.cs) |

| Added | [IPatientIdentityStore.cs](src/Wasla/Wasla.Application/Persistence/IPatientIdentityStore.cs) |

### Infrastructure

| Change | File |

|---|---|

| Modified | [DependencyInjection.cs](src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/DependencyInjection.cs) |

| Modified | [ProfileConfigurations.cs](src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Configurations/ProfileConfigurations.cs) |

| Modified | [WaslaDbContextModelSnapshot.cs](src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Migrations/WaslaDbContextModelSnapshot.cs) |

| Modified | [WaslaDbContext.cs](src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/WaslaDbContext.cs) |

| Modified | [WaslaSecuritySeeder.cs](src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/WaslaSecuritySeeder.cs) |

| Added | [PatientIdentityConfigurations.cs](src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Configurations/PatientIdentityConfigurations.cs) |

| Added | [20261018140120_Phase5FinalPatientIdentityClosure.Designer.cs](src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Migrations/20261018140120_Phase5FinalPatientIdentityClosure.Designer.cs) |

| Added | [20261018140120_Phase5FinalPatientIdentityClosure.cs](src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Migrations/20261018140120_Phase5FinalPatientIdentityClosure.cs) |

| Added | [PatientIdentityMediaCompensation.cs](src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/PatientIdentityMediaCompensation.cs) |

| Added | [PatientIdentityPersistenceErrorMapper.cs](src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/PatientIdentityPersistenceErrorMapper.cs) |

| Added | [PatientIdentityRetention.cs](src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/PatientIdentityRetention.cs) |

| Added | [PatientIdentityStore.cs](src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/PatientIdentityStore.cs) |

### API

| Change | File |

|---|---|

| Modified | [AuthController.cs](src/Wasla/Wasla.Api/Controllers/AuthController.cs) |

| Modified | [PatientsController.cs](src/Wasla/Wasla.Api/Controllers/PatientsController.cs) |

| Modified | [ReservationsController.cs](src/Wasla/Wasla.Api/Controllers/ReservationsController.cs) |

| Modified | [TicketsController.cs](src/Wasla/Wasla.Api/Controllers/TicketsController.cs) |

| Modified | [Program.cs](src/Wasla/Wasla.Api/Program.cs) |

| Modified | [appsettings.json](src/Wasla/Wasla.Api/appsettings.json) |

| Added | [PatientIdentityProblemDetailsFilter.cs](src/Wasla/Wasla.Api/Configuration/PatientIdentityProblemDetailsFilter.cs) |

| Added | [PatientClaimsAdminController.cs](src/Wasla/Wasla.Api/Controllers/PatientClaimsAdminController.cs) |

| Added | [PatientDuplicatesAdminController.cs](src/Wasla/Wasla.Api/Controllers/PatientDuplicatesAdminController.cs) |

| Added | [PatientOnboardingController.cs](src/Wasla/Wasla.Api/Controllers/PatientOnboardingController.cs) |

| Added | [PatientsRegistrationController.cs](src/Wasla/Wasla.Api/Controllers/PatientsRegistrationController.cs) |

| Added | [PatientIdentityContracts.cs](src/Wasla/Wasla.Api/OpenApi/PatientIdentityContracts.cs) |

| Added | [PatientIdentityOperationFilter.cs](src/Wasla/Wasla.Api/OpenApi/PatientIdentityOperationFilter.cs) |

### Tests

| Change | File |

|---|---|

| Modified | [DoctorOnboardingApiTests.cs](tests/Wasla.Tests.Integration/DoctorOnboardingApiTests.cs) |

| Modified | [Phase12ArchiveTestServices.cs](tests/Wasla.Tests.Integration/Phase12ArchiveTestServices.cs) |

| Modified | [Phase12WalkInPaymentTests.cs](tests/Wasla.Tests.Integration/Phase12WalkInPaymentTests.cs) |

| Modified | [Phase13ApiFixture.cs](tests/Wasla.Tests.Integration/Phase13ApiFixture.cs) |

| Modified | [Phase18PatientDashboardApiTests.cs](tests/Wasla.Tests.Integration/Phase18PatientDashboardApiTests.cs) |

| Modified | [ReceptionWalkInOptionsTests.cs](tests/Wasla.Tests.Integration/ReceptionWalkInOptionsTests.cs) |

| Modified | [TrustAccessApiTests.cs](tests/Wasla.Tests.Integration/TrustAccessApiTests.cs) |

| Modified | [Phase18PartTwoDomainTests.cs](tests/Wasla.Tests.Unit/Phase18PartTwoDomainTests.cs) |

| Modified | [Phase18ReportingFoundationTests.cs](tests/Wasla.Tests.Unit/Phase18ReportingFoundationTests.cs) |

| Modified | [Phase5PatientFamilyTests.cs](tests/Wasla.Tests.Unit/Phase5PatientFamilyTests.cs) |

| Added | [Phase5FinalIdentityArchitectureTests.cs](tests/Wasla.Tests.Architecture/Phase5FinalIdentityArchitectureTests.cs) |

| Added | [Phase5FinalClaimsApiTests.cs](tests/Wasla.Tests.Integration/Phase5FinalClaimsApiTests.cs) |

| Added | [Phase5FinalContractTests.cs](tests/Wasla.Tests.Integration/Phase5FinalContractTests.cs) |

| Added | [Phase5FinalDuplicateApiTests.cs](tests/Wasla.Tests.Integration/Phase5FinalDuplicateApiTests.cs) |

| Added | [Phase5FinalIdentitySecurityTests.cs](tests/Wasla.Tests.Integration/Phase5FinalIdentitySecurityTests.cs) |

| Added | [Phase5FinalIdentitySqlServerTests.cs](tests/Wasla.Tests.Integration/Phase5FinalIdentitySqlServerTests.cs) |

| Added | [Phase5FinalIdentityTransitionTests.cs](tests/Wasla.Tests.Integration/Phase5FinalIdentityTransitionTests.cs) |

| Added | [Phase5FinalRegistrationApiTests.cs](tests/Wasla.Tests.Integration/Phase5FinalRegistrationApiTests.cs) |

| Added | [Phase5FinalRetentionTests.cs](tests/Wasla.Tests.Integration/Phase5FinalRetentionTests.cs) |

| Added | [Phase5FinalTransactionTests.cs](tests/Wasla.Tests.Integration/Phase5FinalTransactionTests.cs) |

| Added | [Phase5ExistingPermissionIds.txt](tests/Wasla.Tests.Unit/Phase5ExistingPermissionIds.txt) |

| Added | [Phase5FinalIdentityApplicationTests.cs](tests/Wasla.Tests.Unit/Phase5FinalIdentityApplicationTests.cs) |

| Added | [Phase5FinalIdentityDomainTests.cs](tests/Wasla.Tests.Unit/Phase5FinalIdentityDomainTests.cs) |

### Operator tooling

| Change | File |

|---|---|

| Added | [Program.cs](tools/Wasla.PatientIdentityMaintenance/Program.cs) |

| Added | [Wasla.PatientIdentityMaintenance.csproj](tools/Wasla.PatientIdentityMaintenance/Wasla.PatientIdentityMaintenance.csproj) |

### Documentation

| Change | File |

|---|---|

| Modified | [README.md](README.md) |

| Added | [PHASE-5-FINAL-API-CHANGES.md](PHASE-5-FINAL-API-CHANGES.md) |

| Added | [PHASE-5-FINAL-DATABASE-MIGRATION-PLAN.md](PHASE-5-FINAL-DATABASE-MIGRATION-PLAN.md) |

| Added | [PHASE-5-FINAL-FRONTEND-HANDOFF.md](PHASE-5-FINAL-FRONTEND-HANDOFF.md) |

| Added | [PHASE-5-FINAL-IMPACT-PLAN.md](PHASE-5-FINAL-IMPACT-PLAN.md) |

| Added | [PHASE-5-FINAL-MIGRATION.sql](PHASE-5-FINAL-MIGRATION.sql) |

| Added | [PHASE-5-FINAL-SECURITY-MATRIX.md](PHASE-5-FINAL-SECURITY-MATRIX.md) |

| Added | [PHASE-5-FINAL-TEST-EVIDENCE.json](PHASE-5-FINAL-TEST-EVIDENCE.json) |

| Added | [SOURCE-OF-TRUTH-PHASE-5-FINAL-ADDENDUM.md](SOURCE-OF-TRUTH-PHASE-5-FINAL-ADDENDUM.md) |

## 4. Migration and seeding evidence

`20261018140120_Phase5FinalPatientIdentityClosure` adds seven identity tables plus ContactEmail, persisted normalized identity/search columns, registration audit provenance, restrictive FKs, indexes and concurrency tokens. Existing migrations are unchanged. [Idempotent Up SQL](PHASE-5-FINAL-MIGRATION.sql) contains additive DDL and an incompatibility THROW preflight, with no data cleanup or destructive old-table operation. Standard Down is destructive to new identity history and is not an authorized rollback on used data.

SQL Server forbids a computed column in a filtered-index predicate: UX_Patients_NormalizedEmail filters `[Email] IS NOT NULL`, equivalent under the computed expression and blank-email constraint. Patient phone remains nonunique; ContactEmail is nonunique. All existing account/link uniqueness remains. Open claim targets/unresolved emails and single live invitation slots are SQL-enforced. Native rowversions are8bytes. A conservative transaction-owned global identity app lock fixes ordering; it serializes unrelated identity operations, so production throughput/timeout acceptance remains necessary.

Native fixtures migrate an isolated disposable GUID catalog from full baseline lineage to HEAD; tests exercise raw case/space normalized rejection, multiple null/shared phones/contacts, duplicate links/live invitations/open claims, concurrent Basic registration, explicit unresolved-target approval races, activation races, duplicate-resolution races and cross-doctor reuse in existing booking. EF model/snapshot check reports no pending changes. No development or non-disposable database was reset. [Migration plan](PHASE-5-FINAL-DATABASE-MIGRATION-PLAN.md) records operator preflight, controlled rollout and legal-hold invocation.

The ten new permissions append after303 earlier entries, with every earlier GUID frozen/tested. Two Reception catalog permissions require explicit active-practice delegation; eight admin grants are seeded for SuperAdmin. Existing seeder conflict protections remain. Repeated fixture seeding and original mapping tests pass in focused checks; no unrelated grants are overwritten.

## 5. Security proof and operational limits

[Security matrix](PHASE-5-FINAL-SECURITY-MATRIX.md) maps actors to routes/data/current checks. OTP uses the existing protector with onboarding-specific HMAC domain/account/email/purpose binding; continuation/invitation domains differ from password reset. Tests verify exact expiry, mutation/read distinction, wrong-attempt persistence, inactive Full denial, one-use activation, resend, private evidence, own-status404, age gates and scoped booking.

Full registration commits Patient+disabled User+role+link+attestations+invitation+outbox atomically; Claim approval creates no User/link, and activation links original PatientId. Linked email change updates both identity/login emails with proof in one transaction; conflict leaves both old values. Evidence streams use private storage and metadata-only access audit. Matching never joins medical data. Existing clinical privacy/age/guardian rules are not relaxed; regression setup now performs required staff verification.

Retention tests prove90d boundaries, legal hold placement/release authorization, durable tombstones surviving transient storage deletion failure, retry, and24m audit retirement while live Patient/AccountLinks remain. Sensitive purging defaults off. Actual malware scanner, SMTP/secret provisioning and production privacy/legal approval are release gates; the existing scanner abstraction is reused and no actual production scanner execution is claimed. Media compensation uses the existing retained-reference pattern; persistent external deletion failure still needs operator reconciliation and exhaustive uncertain-commit/multi-node fault verification.

## 6. Test evidence and commands

Environment: Windows, PowerShell, .NET SDK10.0.401, EF CLI10.0.10, local `.\SQLEXPRESS` with integrated authentication; no real patient data or real contact addresses. Per-process `WASLA_SQLSERVER_CONNECTION_STRING` targets master for GUID fixture creation. Exact counts/failure names are in [machine-readable evidence](PHASE-5-FINAL-TEST-EVIDENCE.json); TRX/log artifacts are under ignored TestResults.

```powershell
dotnet --info
dotnet restore Wasla.sln
dotnet build Wasla.sln --configuration Release --no-restore -warnaserror
dotnet test tests/Wasla.Tests.Unit --configuration Release --no-build
dotnet test tests/Wasla.Tests.Integration --configuration Release --no-build
dotnet test tests/Wasla.Tests.Architecture --configuration Release --no-build
dotnet test src/BuildingBlock/BuildingBlock.Tests --configuration Release --logger trx
dotnet build tools/Wasla.PatientIdentityMaintenance --configuration Release -warnaserror
dotnet ef migrations has-pending-model-changes --project src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer --startup-project src/Wasla/Wasla.Api --context WaslaDbContext --configuration Release --no-build
```

Final comprehensive runs may add `--artifacts-path TestResults/phase5-final-artifacts` to build/test: this isolates outputs from an earlier Windows test host that locks loaded assemblies. Temporary MSB3026/3027 copy failures during overlapping intermediate builds were environmental execution errors and are not mislabeled source compilation failures. The stale intermediate run was stopped after reproducing its setup regressions; it is not acceptance evidence. A final isolated run is recorded separately. No test was removed, disabled or weakened to obtain green.

Baseline fresh main:270unit and21architecture pass; native integration258pass/2fail/0skip of260. Both failures were Phase18PatientDashboard parameterizations expecting a stale false configuration flag; the test now compares actual config while retaining effective fail-closed assertions and adding a denied dependent-read check. Appendix's Phase16 SQL sharing failure did not reproduce on fresh baseline; its existing privacy assertions remain.

Broader BuildingBlock baseline reproduced7failures/316pass/0skip of323 in a clean archive of fresh main. Five shipped-API baseline tests look for project folders at repository root instead of src/BuildingBlock; global.json test expects latestPatch while actual repository uses latestFeature; unrestricted-query-filter source scan includes product code beyond the BuildingBlock boundary. The local checkout run has an eighth failure caused by an existing ignored `_local-data/phase18/baseline-check` source copy being recursively treated as a duplicate project. No such ignored data was changed/deleted. SSH.NET2025.1.0 restore reports two NU1903 high-severity advisories, also reproduced on fresh main. These are explicit unresolved foundation/dependency gates, not Phase5-specific regressions.

Intermediate branch regression failures came from intentionally changed contracts: unverified legacy registration, unbounded search fixtures, unknown-patient Walk-In setup, and missing identity-store wiring in a hand-assembled payment fixture. The tests now obtain verification/attestation or seed legitimate newly-registered provenance. Guardian clinical authorization and payment/queue state assertions remain. Focused guardian/Family/Walk-In run53pass with one Swagger projection-location assertion failure subsequently corrected; later targeted run26pass with one payment fixture DI failure subsequently verified passing.

## 7. Change control

[Binding approved addendum](SOURCE-OF-TRUTH-PHASE-5-FINAL-ADDENDUM.md) preserves superseded text and both owner execution clarifications. Master v1.2 was absent; no claim is made to editing it. Suggested Master amendment: adopt this addendum and record Phase5 as IN-PROGRESS until all gates below pass. Existing Phase16/18 approved source addenda are not overwritten. No Jira updates, multi-UserType login, account conversion, medical merge or extra public API were introduced.

## 8. Frontend handoff

[Frontend handoff](PHASE-5-FINAL-FRONTEND-HANDOFF.md) covers Basic/Full, global search/Use Existing Patient, OTP nextAction, independent Claim proof/review/modification/rejection, secure activation/resend/Login, email change, localization, age guidance and retry conflicts. [API reference](PHASE-5-FINAL-API-CHANGES.md) provides all payloads and compatibility impacts. Legacy E01 requires proof/header; E03 redacts raw phone; E05 requires verified changed email; existing operational routes remain.

## 9. Closure determination — explicit remaining gates

**Phase5 IN-PROGRESS.** Completion is blocked until all of the following are closed with evidence:

- Final full integration/regression and SQL concurrency run results are verified against final source, with no concealed/skipped failures. Fresh remote CI has not been run or declared green.
- The seven clean-baseline BuildingBlock failures, checkout-only duplicate-project scan and known dependency vulnerability advisories need resolution/owner disposition consistent with full-green acceptance. They are not silently suppressed.
- Complete the remaining exhaustive matrix/fault cases: uniform public timing/log inspection, all cross-flow identity races, media uncertain-commit and persistent compensation failure reconciliation, multi-instance retention/legal-hold contention and deployment scanner/email operational validation. Existing focused evidence is listed without substituting it for unexecuted cases.

Release gates remain separate: production legal/privacy retention approval, actual malware scanner and secure transport/secrets provisioning, controlled SQL rehearsal/backup/recovery and load/observability acceptance. Purge flags remain false. Do not label production ready.
