# Phase 5 Final Closure Report

Status: **Phase 5 IN-PROGRESS**. Approval: 2026-10-18. Branch `codex/phase5-final-closure`; fresh main/origin/main baseline `17c9e0b96af87de0143ca4ac4978bc970ca6f9dd`. Product code, migration, tests and handoff are implemented; final acceptance is not asserted until every gate below is verified. This is not a production-readiness declaration.

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
| P5-24 | Blocked | OTP/continuation/invitation purpose, expiry, attempts, live authorization, privacy and transactional outbox implemented. Required cross-flow/fault acceptance has not all been demonstrated; see the explicit gates in section 9. Production scanner provisioning is a separate release gate. |
| P5-25 | Blocked | OTP/continuation/invitation purpose, expiry, attempts, live authorization, privacy and transactional outbox implemented. Required cross-flow/fault acceptance has not all been demonstrated; see the explicit gates in section 9. Production scanner provisioning is a separate release gate. |
| P5-26 | Blocked | OTP/continuation/invitation purpose, expiry, attempts, live authorization, privacy and transactional outbox implemented. Required cross-flow/fault acceptance has not all been demonstrated; see the explicit gates in section 9. Production scanner provisioning is a separate release gate. |
| P5-27 | Blocked | OTP/continuation/invitation purpose, expiry, attempts, live authorization, privacy and transactional outbox implemented. Required cross-flow/fault acceptance has not all been demonstrated; see the explicit gates in section 9. Production scanner provisioning is a separate release gate. |
| P5-28 | Blocked | OTP/continuation/invitation purpose, expiry, attempts, live authorization, privacy and transactional outbox implemented. Required cross-flow/fault acceptance has not all been demonstrated; see the explicit gates in section 9. Production scanner provisioning is a separate release gate. |
| P5-29 | Blocked | OTP/continuation/invitation purpose, expiry, attempts, live authorization, privacy and transactional outbox implemented. Required cross-flow/fault acceptance has not all been demonstrated; see the explicit gates in section 9. Production scanner provisioning is a separate release gate. |
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
| Modified | [src/Wasla/Wasla.Domain/Patients/Patient.cs](src/Wasla/Wasla.Domain/Patients/Patient.cs) |
| Modified | [src/Wasla/Wasla.Domain/Patients/PatientAccountLink.cs](src/Wasla/Wasla.Domain/Patients/PatientAccountLink.cs) |
| Added | [src/Wasla/Wasla.Domain/Patients/PatientActivationInvitation.cs](src/Wasla/Wasla.Domain/Patients/PatientActivationInvitation.cs) |
| Added | [src/Wasla/Wasla.Domain/Patients/PatientClaimEvidence.cs](src/Wasla/Wasla.Domain/Patients/PatientClaimEvidence.cs) |
| Added | [src/Wasla/Wasla.Domain/Patients/PatientClaimHistory.cs](src/Wasla/Wasla.Domain/Patients/PatientClaimHistory.cs) |
| Added | [src/Wasla/Wasla.Domain/Patients/PatientClaimRequest.cs](src/Wasla/Wasla.Domain/Patients/PatientClaimRequest.cs) |
| Modified | [src/Wasla/Wasla.Domain/Patients/PatientContact.cs](src/Wasla/Wasla.Domain/Patients/PatientContact.cs) |
| Added | [src/Wasla/Wasla.Domain/Patients/PatientDuplicateCase.cs](src/Wasla/Wasla.Domain/Patients/PatientDuplicateCase.cs) |
| Added | [src/Wasla/Wasla.Domain/Patients/PatientIdentityAttestation.cs](src/Wasla/Wasla.Domain/Patients/PatientIdentityAttestation.cs) |
| Added | [src/Wasla/Wasla.Domain/Patients/PatientIdentityErrors.cs](src/Wasla/Wasla.Domain/Patients/PatientIdentityErrors.cs) |
| Added | [src/Wasla/Wasla.Domain/Patients/PatientOnboardingChallenge.cs](src/Wasla/Wasla.Domain/Patients/PatientOnboardingChallenge.cs) |
| Modified | [src/Wasla/Wasla.Domain/Resources/ErrorMessage.ar.resx](src/Wasla/Wasla.Domain/Resources/ErrorMessage.ar.resx) |
| Modified | [src/Wasla/Wasla.Domain/Resources/ErrorMessage.cs](src/Wasla/Wasla.Domain/Resources/ErrorMessage.cs) |
| Modified | [src/Wasla/Wasla.Domain/Resources/ErrorMessage.resx](src/Wasla/Wasla.Domain/Resources/ErrorMessage.resx) |
| Modified | [src/Wasla/Wasla.Domain/Security/SystemSecurity.cs](src/Wasla/Wasla.Domain/Security/SystemSecurity.cs) |

### Application

| Change | File |
|---|---|
| Modified | [src/Wasla/Wasla.Application/DependencyInjection.cs](src/Wasla/Wasla.Application/DependencyInjection.cs) |
| Modified | [src/Wasla/Wasla.Application/Features/Auth/Authentication.cs](src/Wasla/Wasla.Application/Features/Auth/Authentication.cs) |
| Modified | [src/Wasla/Wasla.Application/Features/Auth/PasswordRecovery.cs](src/Wasla/Wasla.Application/Features/Auth/PasswordRecovery.cs) |
| Added | [src/Wasla/Wasla.Application/Features/PatientClaims/PatientIdentityLegalHold.cs](src/Wasla/Wasla.Application/Features/PatientClaims/PatientIdentityLegalHold.cs) |
| Added | [src/Wasla/Wasla.Application/Features/PatientIdentityMatching/PatientIdentityReuseGuard.cs](src/Wasla/Wasla.Application/Features/PatientIdentityMatching/PatientIdentityReuseGuard.cs) |
| Added | [src/Wasla/Wasla.Application/Features/PatientOnboarding/PatientClaimWorkflow.cs](src/Wasla/Wasla.Application/Features/PatientOnboarding/PatientClaimWorkflow.cs) |
| Added | [src/Wasla/Wasla.Application/Features/PatientOnboarding/PatientIdentityContracts.cs](src/Wasla/Wasla.Application/Features/PatientOnboarding/PatientIdentityContracts.cs) |
| Added | [src/Wasla/Wasla.Application/Features/PatientOnboarding/PatientIdentityMediaBehavior.cs](src/Wasla/Wasla.Application/Features/PatientOnboarding/PatientIdentityMediaBehavior.cs) |
| Added | [src/Wasla/Wasla.Application/Features/PatientOnboarding/PatientIdentityOptions.cs](src/Wasla/Wasla.Application/Features/PatientOnboarding/PatientIdentityOptions.cs) |
| Added | [src/Wasla/Wasla.Application/Features/PatientOnboarding/PatientIdentityReads.cs](src/Wasla/Wasla.Application/Features/PatientOnboarding/PatientIdentityReads.cs) |
| Added | [src/Wasla/Wasla.Application/Features/PatientOnboarding/PatientIdentityUploadFingerprint.cs](src/Wasla/Wasla.Application/Features/PatientOnboarding/PatientIdentityUploadFingerprint.cs) |
| Added | [src/Wasla/Wasla.Application/Features/PatientOnboarding/PatientIdentityWorkflow.cs](src/Wasla/Wasla.Application/Features/PatientOnboarding/PatientIdentityWorkflow.cs) |
| Modified | [src/Wasla/Wasla.Application/Features/Patients/PatientManagement.cs](src/Wasla/Wasla.Application/Features/Patients/PatientManagement.cs) |
| Modified | [src/Wasla/Wasla.Application/Features/Registration/Registration.cs](src/Wasla/Wasla.Application/Features/Registration/Registration.cs) |
| Modified | [src/Wasla/Wasla.Application/Features/Reservations/ReservationManagement.cs](src/Wasla/Wasla.Application/Features/Reservations/ReservationManagement.cs) |
| Modified | [src/Wasla/Wasla.Application/Features/Tickets/CreateWalkIn/CreateWalkIn.cs](src/Wasla/Wasla.Application/Features/Tickets/CreateWalkIn/CreateWalkIn.cs) |
| Added | [src/Wasla/Wasla.Application/Persistence/IPatientIdentityStore.cs](src/Wasla/Wasla.Application/Persistence/IPatientIdentityStore.cs) |

### Infrastructure

| Change | File |
|---|---|
| Modified | [src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/DependencyInjection.cs](src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/DependencyInjection.cs) |
| Added | [src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Configurations/PatientIdentityConfigurations.cs](src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Configurations/PatientIdentityConfigurations.cs) |
| Modified | [src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Configurations/ProfileConfigurations.cs](src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Configurations/ProfileConfigurations.cs) |
| Added | [src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Migrations/20261018140120_Phase5FinalPatientIdentityClosure.Designer.cs](src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Migrations/20261018140120_Phase5FinalPatientIdentityClosure.Designer.cs) |
| Added | [src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Migrations/20261018140120_Phase5FinalPatientIdentityClosure.cs](src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Migrations/20261018140120_Phase5FinalPatientIdentityClosure.cs) |
| Modified | [src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Migrations/WaslaDbContextModelSnapshot.cs](src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Migrations/WaslaDbContextModelSnapshot.cs) |
| Added | [src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/PatientIdentityMediaCompensation.cs](src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/PatientIdentityMediaCompensation.cs) |
| Added | [src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/PatientIdentityPersistenceErrorMapper.cs](src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/PatientIdentityPersistenceErrorMapper.cs) |
| Added | [src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/PatientIdentityRetention.cs](src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/PatientIdentityRetention.cs) |
| Added | [src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/PatientIdentityStore.cs](src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/PatientIdentityStore.cs) |
| Modified | [src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/WaslaDbContext.cs](src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/WaslaDbContext.cs) |
| Modified | [src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/WaslaSecuritySeeder.cs](src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/WaslaSecuritySeeder.cs) |

### API

| Change | File |
|---|---|
| Added | [src/Wasla/Wasla.Api/Configuration/PatientIdentityProblemDetailsFilter.cs](src/Wasla/Wasla.Api/Configuration/PatientIdentityProblemDetailsFilter.cs) |
| Modified | [src/Wasla/Wasla.Api/Controllers/AuthController.cs](src/Wasla/Wasla.Api/Controllers/AuthController.cs) |
| Added | [src/Wasla/Wasla.Api/Controllers/PatientClaimsAdminController.cs](src/Wasla/Wasla.Api/Controllers/PatientClaimsAdminController.cs) |
| Added | [src/Wasla/Wasla.Api/Controllers/PatientDuplicatesAdminController.cs](src/Wasla/Wasla.Api/Controllers/PatientDuplicatesAdminController.cs) |
| Added | [src/Wasla/Wasla.Api/Controllers/PatientOnboardingController.cs](src/Wasla/Wasla.Api/Controllers/PatientOnboardingController.cs) |
| Modified | [src/Wasla/Wasla.Api/Controllers/PatientsController.cs](src/Wasla/Wasla.Api/Controllers/PatientsController.cs) |
| Added | [src/Wasla/Wasla.Api/Controllers/PatientsRegistrationController.cs](src/Wasla/Wasla.Api/Controllers/PatientsRegistrationController.cs) |
| Modified | [src/Wasla/Wasla.Api/Controllers/ReservationsController.cs](src/Wasla/Wasla.Api/Controllers/ReservationsController.cs) |
| Modified | [src/Wasla/Wasla.Api/Controllers/TicketsController.cs](src/Wasla/Wasla.Api/Controllers/TicketsController.cs) |
| Added | [src/Wasla/Wasla.Api/OpenApi/PatientIdentityContracts.cs](src/Wasla/Wasla.Api/OpenApi/PatientIdentityContracts.cs) |
| Added | [src/Wasla/Wasla.Api/OpenApi/PatientIdentityOperationFilter.cs](src/Wasla/Wasla.Api/OpenApi/PatientIdentityOperationFilter.cs) |
| Modified | [src/Wasla/Wasla.Api/Program.cs](src/Wasla/Wasla.Api/Program.cs) |
| Modified | [src/Wasla/Wasla.Api/appsettings.json](src/Wasla/Wasla.Api/appsettings.json) |

### Tests

| Change | File |
|---|---|
| Modified | [src/BuildingBlock/BuildingBlock.Tests/GlobalQueryFilterTests.cs](src/BuildingBlock/BuildingBlock.Tests/GlobalQueryFilterTests.cs) |
| Modified | [src/BuildingBlock/BuildingBlock.Tests/Phase1FoundationTests.cs](src/BuildingBlock/BuildingBlock.Tests/Phase1FoundationTests.cs) |
| Modified | [src/BuildingBlock/BuildingBlock.Tests/Phase5PublicApiBaselineTests.cs](src/BuildingBlock/BuildingBlock.Tests/Phase5PublicApiBaselineTests.cs) |
| Added | [tests/Wasla.Tests.Architecture/Phase5FinalIdentityArchitectureTests.cs](tests/Wasla.Tests.Architecture/Phase5FinalIdentityArchitectureTests.cs) |
| Modified | [tests/Wasla.Tests.Integration/DoctorOnboardingApiTests.cs](tests/Wasla.Tests.Integration/DoctorOnboardingApiTests.cs) |
| Modified | [tests/Wasla.Tests.Integration/Phase12ArchiveTestServices.cs](tests/Wasla.Tests.Integration/Phase12ArchiveTestServices.cs) |
| Modified | [tests/Wasla.Tests.Integration/Phase12WalkInPaymentTests.cs](tests/Wasla.Tests.Integration/Phase12WalkInPaymentTests.cs) |
| Modified | [tests/Wasla.Tests.Integration/Phase13ApiFixture.cs](tests/Wasla.Tests.Integration/Phase13ApiFixture.cs) |
| Modified | [tests/Wasla.Tests.Integration/Phase16SharingActivationTests.cs](tests/Wasla.Tests.Integration/Phase16SharingActivationTests.cs) |
| Modified | [tests/Wasla.Tests.Integration/Phase18PatientDashboardApiTests.cs](tests/Wasla.Tests.Integration/Phase18PatientDashboardApiTests.cs) |
| Added | [tests/Wasla.Tests.Integration/Phase5FinalBoundaryTests.cs](tests/Wasla.Tests.Integration/Phase5FinalBoundaryTests.cs) |
| Added | [tests/Wasla.Tests.Integration/Phase5FinalClaimsApiTests.cs](tests/Wasla.Tests.Integration/Phase5FinalClaimsApiTests.cs) |
| Added | [tests/Wasla.Tests.Integration/Phase5FinalContractTests.cs](tests/Wasla.Tests.Integration/Phase5FinalContractTests.cs) |
| Added | [tests/Wasla.Tests.Integration/Phase5FinalDuplicateApiTests.cs](tests/Wasla.Tests.Integration/Phase5FinalDuplicateApiTests.cs) |
| Added | [tests/Wasla.Tests.Integration/Phase5FinalIdentitySecurityTests.cs](tests/Wasla.Tests.Integration/Phase5FinalIdentitySecurityTests.cs) |
| Added | [tests/Wasla.Tests.Integration/Phase5FinalIdentitySqlServerTests.cs](tests/Wasla.Tests.Integration/Phase5FinalIdentitySqlServerTests.cs) |
| Added | [tests/Wasla.Tests.Integration/Phase5FinalIdentityTransitionTests.cs](tests/Wasla.Tests.Integration/Phase5FinalIdentityTransitionTests.cs) |
| Added | [tests/Wasla.Tests.Integration/Phase5FinalMaintenanceTests.cs](tests/Wasla.Tests.Integration/Phase5FinalMaintenanceTests.cs) |
| Added | [tests/Wasla.Tests.Integration/Phase5FinalRegistrationApiTests.cs](tests/Wasla.Tests.Integration/Phase5FinalRegistrationApiTests.cs) |
| Added | [tests/Wasla.Tests.Integration/Phase5FinalRetentionTests.cs](tests/Wasla.Tests.Integration/Phase5FinalRetentionTests.cs) |
| Added | [tests/Wasla.Tests.Integration/Phase5FinalTransactionTests.cs](tests/Wasla.Tests.Integration/Phase5FinalTransactionTests.cs) |
| Modified | [tests/Wasla.Tests.Integration/ReceptionWalkInOptionsTests.cs](tests/Wasla.Tests.Integration/ReceptionWalkInOptionsTests.cs) |
| Modified | [tests/Wasla.Tests.Integration/TrustAccessApiTests.cs](tests/Wasla.Tests.Integration/TrustAccessApiTests.cs) |
| Modified | [tests/Wasla.Tests.Unit/Phase18PartTwoDomainTests.cs](tests/Wasla.Tests.Unit/Phase18PartTwoDomainTests.cs) |
| Modified | [tests/Wasla.Tests.Unit/Phase18ReportingFoundationTests.cs](tests/Wasla.Tests.Unit/Phase18ReportingFoundationTests.cs) |
| Added | [tests/Wasla.Tests.Unit/Phase5ExistingPermissionIds.txt](tests/Wasla.Tests.Unit/Phase5ExistingPermissionIds.txt) |
| Added | [tests/Wasla.Tests.Unit/Phase5FinalIdentityApplicationTests.cs](tests/Wasla.Tests.Unit/Phase5FinalIdentityApplicationTests.cs) |
| Added | [tests/Wasla.Tests.Unit/Phase5FinalIdentityDomainTests.cs](tests/Wasla.Tests.Unit/Phase5FinalIdentityDomainTests.cs) |
| Modified | [tests/Wasla.Tests.Unit/Phase5PatientFamilyTests.cs](tests/Wasla.Tests.Unit/Phase5PatientFamilyTests.cs) |

### Operations and build

| Change | File |
|---|---|
| Modified | [.github/workflows/ci.yml](.github/workflows/ci.yml) |
| Modified | [Directory.Packages.props](Directory.Packages.props) |
| Modified | [Wasla.sln](Wasla.sln) |
| Added | [tools/Wasla.PatientIdentityMaintenance/MaintenanceExecutionContext.cs](tools/Wasla.PatientIdentityMaintenance/MaintenanceExecutionContext.cs) |
| Added | [tools/Wasla.PatientIdentityMaintenance/Program.cs](tools/Wasla.PatientIdentityMaintenance/Program.cs) |
| Added | [tools/Wasla.PatientIdentityMaintenance/Wasla.PatientIdentityMaintenance.csproj](tools/Wasla.PatientIdentityMaintenance/Wasla.PatientIdentityMaintenance.csproj) |

### Documentation

| Change | File |
|---|---|
| Added | [PHASE-5-FINAL-API-CHANGES.md](PHASE-5-FINAL-API-CHANGES.md) |
| Added | [PHASE-5-FINAL-CLOSURE-REPORT.md](PHASE-5-FINAL-CLOSURE-REPORT.md) |
| Added | [PHASE-5-FINAL-DATABASE-MIGRATION-PLAN.md](PHASE-5-FINAL-DATABASE-MIGRATION-PLAN.md) |
| Added | [PHASE-5-FINAL-FRONTEND-HANDOFF.md](PHASE-5-FINAL-FRONTEND-HANDOFF.md) |
| Added | [PHASE-5-FINAL-IMPACT-PLAN.md](PHASE-5-FINAL-IMPACT-PLAN.md) |
| Added | [PHASE-5-FINAL-MIGRATION.sql](PHASE-5-FINAL-MIGRATION.sql) |
| Added | [PHASE-5-FINAL-SECURITY-MATRIX.md](PHASE-5-FINAL-SECURITY-MATRIX.md) |
| Added | [PHASE-5-FINAL-TEST-EVIDENCE.json](PHASE-5-FINAL-TEST-EVIDENCE.json) |
| Modified | [README.md](README.md) |
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

Environment: Windows, PowerShell, .NET SDK 10.0.401, EF CLI 10.0.10, local `.\SQLEXPRESS` using integrated authentication. Fixture catalogs have random GUID names and synthetic data; no development/non-disposable database was reset. Native tests receive `WASLA_SQLSERVER_CONNECTION_STRING` only in their process. [Machine-readable results](PHASE-5-FINAL-TEST-EVIDENCE.json) records TRX counts and exact failed test names; raw logs/TRX remain in ignored TestResults and CI uploads its TRX artifacts.

| Run | Passed | Failed | Skipped | Evidence |
|---|---:|---:|---:|---|
| Fresh main Wasla unit | 270 | 0 | 0 | phase5-final-closure-baseline |
| Fresh main Wasla architecture | 21 | 0 | 0 | phase5-final-closure-baseline |
| Fresh main native integration | 258 | 2 | 0 | phase5-final-closure-baseline-sql |
| Full Wasla solution: unit | 290 | 0 | 0 | phase5-final-solution |
| Full Wasla solution: native integration/regression | 281 | 0 | 0 | phase5-final-solution; 21m46s |
| Full Wasla solution: architecture | 23 | 0 | 0 | phase5-final-solution |
| Latest-source unit | 290 | 0 | 0 | phase5-closure-final-unit |
| Latest-source architecture | 23 | 0 | 0 | phase5-closure-final-architecture |
| Latest-source Phase5 integration, including native SQL races | 21 | 0 | 0 | phase5-closure-final-focused; 1m39s |
| Final Phase5 acceptance (including new native identity race, OTP boundaries, minors, attestation and live evidence denial) | 24 | 0 | 0 | phase5-final-acceptance; 1m54s |
| Corrected Phase16 sharing configuration tests | 7 | 0 | 0 | phase5-ci-sharing-fix; 49s |
| Clean archived main BuildingBlock | 316 | 7 | 0 | phase5-buildingblock-baseline |
| Corrected BuildingBlock run | 318 | 5 | 0 | phase5-closure-foundation; 35s |

The full 281 integration run precedes the final self-registration soft-duplicate audit addition; all 21 Phase5 integration tests were then rerun against that final source. The 7-test Phase16 rerun validates the subsequent test-host correction. Three further boundary tests pass together with all existing Phase5 tests in the final24-test run; this also validates denial of another verified applicant and live removal of evidence permission from an ordinary SuperAdmin. One initial new test omitted its required evidenceType field and correctly received400; the corrected well-formed-but-evidence-free request receives422. Both attempt and final results are retained in machine evidence. Fresh remote CI is recorded separately below. No local run is presented as remote CI evidence.

Commands executed (Release configuration):

```powershell
dotnet --info
dotnet restore Wasla.sln
dotnet build Wasla.sln --configuration Release --no-restore -warnaserror
dotnet test tests/Wasla.Tests.Unit --configuration Release --no-build
dotnet test tests/Wasla.Tests.Integration --configuration Release --no-build
dotnet test tests/Wasla.Tests.Architecture --configuration Release --no-build
dotnet test Wasla.sln --configuration Release --no-build --logger trx --results-directory TestResults/phase5-final-solution -v quiet -- xUnit.MaxParallelThreads=2 xUnit.LongRunningTestSeconds=90 xUnit.DiagnosticMessages=true
dotnet build Wasla.sln --configuration Release --artifacts-path TestResults/phase5-closure-final-artifacts -warnaserror -v quiet
dotnet test tests/Wasla.Tests.Unit --configuration Release --artifacts-path TestResults/phase5-closure-final-artifacts --no-build --logger trx --results-directory TestResults/phase5-closure-final-unit -v quiet
dotnet test tests/Wasla.Tests.Architecture --configuration Release --artifacts-path TestResults/phase5-closure-final-artifacts --no-build --logger trx --results-directory TestResults/phase5-closure-final-architecture -v quiet
dotnet test tests/Wasla.Tests.Integration --configuration Release --artifacts-path TestResults/phase5-closure-final-artifacts --no-build --filter FullyQualifiedName~Phase5Final --logger trx --results-directory TestResults/phase5-closure-final-focused -v quiet -- xUnit.MaxParallelThreads=2
dotnet test tests/Wasla.Tests.Integration --configuration Release --no-build --filter FullyQualifiedName~Phase16SharingActivationTests --logger trx --results-directory TestResults/phase5-ci-sharing-fix -v quiet -- xUnit.MaxParallelThreads=2
dotnet test tests/Wasla.Tests.Integration --configuration Release --no-build --filter FullyQualifiedName~Phase5Final --logger trx --results-directory TestResults/phase5-final-acceptance -v quiet -- xUnit.MaxParallelThreads=2
dotnet test src/BuildingBlock/BuildingBlock.Tests --configuration Release --logger trx --results-directory TestResults/phase5-closure-foundation -v quiet
dotnet build tools/Wasla.PatientIdentityMaintenance --configuration Release -warnaserror
dotnet ef migrations has-pending-model-changes --project src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer --startup-project src/Wasla/Wasla.Api --context WaslaDbContext --configuration Release --no-build
```

Two concurrent xUnit fixture threads bound local SQL Express load; the race tests still issue concurrent requests with Task.WhenAll. No tests were disabled/skipped. Current build logs phase5-closure-final-build and phase5-ci-fix-build both report zero warnings/errors. The final model check reports no changes since the new migration. Earlier overlapping Windows test hosts caused assembly-copy locks and SQL fixture contention; those interrupted intermediate runs are not acceptance evidence.

Baseline attribution and fixes:

- The two fresh-main Phase18 dashboard failures expected a stale false configuration flag. Assertions now follow actual configuration while retaining the effective fail-closed family policy and a denied dependent read.
- Appendix A's Phase16 SQL approval failure did not reproduce in the completed local baseline, but did reproduce in [branch CI run 39139538569](https://github.com/LoaiSaberC4r/Wasla/actions/runs/39139538569): build passed, 290 unit passed, integration 241 passed/1 failed of 242; later SQL/architecture steps were not executed. Investigation found that the requesting doctor used the explicitly keyed host, but the approving doctor used the unconfigured base host. Local developer secrets masked this in Windows; CI correctly denied approval. Both actors now use the configured host, and the test explicitly verifies the unconfigured host still returns404. No clinical privacy gate changed. The 7-test native rerun passes.
- BuildingBlock's clean archived-main baseline had seven failures. The SDK policy assertion now checks the repository's adopted latestFeature policy; project inventory is limited to actual src/tools trees instead of ignored copies; the query-filter test scans its named BuildingBlock boundary; the public API test uses the actual src/BuildingBlock layout. Five failures remain: all five libraries lack their approved PublicAPI.Shipped.txt/Unshipped.txt baseline pairs. They are absent from both available source trees and Wasla Git history. Generating references from the code under test would not prove compatibility, so this remains a concrete blocked verification item. No baseline assertion is skipped or relaxed.
- The prior checkout-only eighth BuildingBlock failure came from an ignored _local-data source copy; the bounded src/tools inventory resolves it without deleting user data. Intermediate registration/search/Walk-In fixture errors were corrected by obtaining required OTP/attestation or legitimate registration provenance, preserving operational/privacy assertions.
- Testcontainers transitively pulled SSH.NET2025.1.0. A central transitive pin to2026.0.0 resolves the two reported advisories without changing product behavior; the corrected restore/build reports no NU1903. Maintainer remediation references: [remote-path handling](https://github.com/advisories/GHSA-mggc-4xg6-vcxf), [recursive filename handling](https://github.com/advisories/GHSA-q939-rpr3-3284).

Fresh [CI run 39154768257](https://github.com/LoaiSaberC4r/Wasla/actions/runs/39154768257) is associated with correction commit bea8b0aa62012578d7020dc447d9935899efbfd5. Its final status is recorded below; until then it is not declared green. The workflow keeps its existing test categories and now uploads TRX evidence on success/failure.

## 7. Change control

[Binding approved addendum](SOURCE-OF-TRUTH-PHASE-5-FINAL-ADDENDUM.md) preserves superseded text and both owner execution clarifications. Master v1.2 was absent; no claim is made to editing it. Suggested Master amendment: adopt this addendum and record Phase5 as IN-PROGRESS until all gates below pass. Existing Phase16/18 approved source addenda are not overwritten. No Jira updates, multi-UserType login, account conversion, medical merge or extra public API were introduced.

## 8. Frontend handoff

[Frontend handoff](PHASE-5-FINAL-FRONTEND-HANDOFF.md) covers Basic/Full, global search/Use Existing Patient, OTP nextAction, independent Claim proof/review/modification/rejection, secure activation/resend/Login, email change, localization, age guidance and retry conflicts. [API reference](PHASE-5-FINAL-API-CHANGES.md) provides all payloads and compatibility impacts. Legacy E01 requires proof/header; E03 redacts raw phone; E05 requires verified changed email; existing operational routes remain.

## 9. Closure determination — explicit remaining gates

**Phase5 IN-PROGRESS.** Completion is blocked until all of the following are closed with evidence:

- Fresh remote CI must finish successfully against final source; the completed local full and focused results are recorded above.
- Restore the approved public API baseline pairs for the five BuildingBlock libraries and rerun their compatibility suite. Current outcome is318passed/5failed/0skipped. Both local foundation trees and Wasla history lack those originals; the operator was asked for their location. Other baseline test-layout/policy failures and the SSH.NET advisories were resolved with evidence above.
- Remaining security/fault evidence is not claimed: measured public timing/log-capture inspection, uncertain-commit/persistent compensation-failure simulation and multi-instance retention/legal-hold contention. The implementation uses the existing compensation pattern and transaction-owned SQL locking; the current tests do not substitute for those unexecuted fault scenarios. Actual scanner/mail provisioning remains a deployment release gate, not a fabricated local success.

Release gates remain separate: production legal/privacy retention approval, actual malware scanner and secure transport/secrets provisioning, controlled SQL rehearsal/backup/recovery and load/observability acceptance. Purge flags remain false. Do not label production ready.
