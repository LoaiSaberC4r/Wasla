# Phase 5 Final Closure Report

Status: **Phase 5 COMPLETED**. Approval: 2026-10-18. Branch `codex/phase5-final-closure`; fresh main/origin/main baseline `17c9e0b96af87de0143ca4ac4978bc970ca6f9dd`. The Definition of Done is verified by final remote CI run 39160421337 on source commit d79658a568dbfc4273b9b9dc1bab695043d316ab: 922 passed, 0 failed, 0 skipped, plus the clean EF model check and additive migration evidence below. This is not a production-readiness declaration.

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
| P5-24 | Implemented | OTP/continuation/invitation purpose, expiry, attempts, live authorization, privacy and transactional outbox; native identity races, uncertain-commit compensation and SQL retention/legal-hold checks pass. Production scanner provisioning is a separate release gate. |
| P5-25 | Implemented | OTP/continuation/invitation purpose, expiry, attempts, live authorization, privacy and transactional outbox; native identity races, uncertain-commit compensation and SQL retention/legal-hold checks pass. Production scanner provisioning is a separate release gate. |
| P5-26 | Implemented | OTP/continuation/invitation purpose, expiry, attempts, live authorization, privacy and transactional outbox; native identity races, uncertain-commit compensation and SQL retention/legal-hold checks pass. Production scanner provisioning is a separate release gate. |
| P5-27 | Implemented | OTP/continuation/invitation purpose, expiry, attempts, live authorization, privacy and transactional outbox; native identity races, uncertain-commit compensation and SQL retention/legal-hold checks pass. Production scanner provisioning is a separate release gate. |
| P5-28 | Implemented | OTP/continuation/invitation purpose, expiry, attempts, live authorization, privacy and transactional outbox; native identity races, uncertain-commit compensation and SQL retention/legal-hold checks pass. Production scanner provisioning is a separate release gate. |
| P5-29 | Implemented | OTP/continuation/invitation purpose, expiry, attempts, live authorization, privacy and transactional outbox; native identity races, uncertain-commit compensation and SQL retention/legal-hold checks pass. Production scanner provisioning is a separate release gate. |
| P5-30 | Implemented | Final grouped closure/retention/security/API/permission/migration/regression gates verified; final CI includes all Wasla and BuildingBlock suites. |
| P5-31 | Implemented | Final grouped closure/retention/security/API/permission/migration/regression gates verified; final CI includes all Wasla and BuildingBlock suites. |
| P5-32 | Implemented | Final grouped closure/retention/security/API/permission/migration/regression gates verified; final CI includes all Wasla and BuildingBlock suites. |
| P5-33 | Implemented | Final grouped closure/retention/security/API/permission/migration/regression gates verified; final CI includes all Wasla and BuildingBlock suites. |
| P5-34 | Implemented | Final grouped closure/retention/security/API/permission/migration/regression gates verified; final CI includes all Wasla and BuildingBlock suites. |
| P5-D01.1 | Implemented | Optional unique Patient identity email; nonunique phone and separate nonunique ContactEmail; existing PatientId reused without clinical escalation |
| P5-D01.2 | Implemented | Additive migration; preflight fails on collisions; no historical cleanup, automatic merge or app reset |
| P5-D01 | Implemented | Actual medical-record merge remains unsupported; duplicate classification cannot move clinical rows |
| P5-D02 | Implemented | One effective UserType; separate unique account email/credentials; UserRole schema retained |
| P5-D03 | Implemented | 90d inactivity, evidence90d, audit24m, durable tombstones/retry, legal hold CLI, purge flags off; legal/privacy release gate remains |
| DEC-020 / DEC-021 | Implemented | Reception minimum contact and existing Family/guardian privacy retained |

## 2. Route-by-route diff

[API changes](PHASE-5-FINAL-API-CHANGES.md) contains every E01–E06/N01–N20 method/path, actual CQRS operation/handler, permission/proof, request/response projection, examples, pagination, rowversions, idempotency, errors and coverage. Four new controllers publish exactly 20 new operations; typed OpenAPI projections and headers are supplied. Existing legacy self-registration delegates to the same verified workflow. Existing booking and Walk-In accept one optional attestation field.

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
| Added | [src/BuildingBlock/BuildingBlock.Tests/API-BASELINE-PROVENANCE.md](src/BuildingBlock/BuildingBlock.Tests/API-BASELINE-PROVENANCE.md) |
| Modified | [src/BuildingBlock/BuildingBlock.Tests/GlobalQueryFilterTests.cs](src/BuildingBlock/BuildingBlock.Tests/GlobalQueryFilterTests.cs) |
| Modified | [src/BuildingBlock/BuildingBlock.Tests/Phase1FoundationTests.cs](src/BuildingBlock/BuildingBlock.Tests/Phase1FoundationTests.cs) |
| Modified | [src/BuildingBlock/BuildingBlock.Tests/Phase5PublicApiBaselineTests.cs](src/BuildingBlock/BuildingBlock.Tests/Phase5PublicApiBaselineTests.cs) |
| Added | [src/BuildingBlock/BuildingBlock.Tests/PublicApiSignatures.cs](src/BuildingBlock/BuildingBlock.Tests/PublicApiSignatures.cs) |
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
| Added | [tests/Wasla.Tests.Integration/Phase5FinalFaultTests.cs](tests/Wasla.Tests.Integration/Phase5FinalFaultTests.cs) |
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

### Foundation compatibility references

| Change | File |
|---|---|
| Added | [src/BuildingBlock/BuildingBlock.Api/PublicAPI.Shipped.txt](src/BuildingBlock/BuildingBlock.Api/PublicAPI.Shipped.txt) |
| Added | [src/BuildingBlock/BuildingBlock.Api/PublicAPI.Unshipped.txt](src/BuildingBlock/BuildingBlock.Api/PublicAPI.Unshipped.txt) |
| Added | [src/BuildingBlock/BuildingBlock.Application/PublicAPI.Shipped.txt](src/BuildingBlock/BuildingBlock.Application/PublicAPI.Shipped.txt) |
| Added | [src/BuildingBlock/BuildingBlock.Application/PublicAPI.Unshipped.txt](src/BuildingBlock/BuildingBlock.Application/PublicAPI.Unshipped.txt) |
| Added | [src/BuildingBlock/BuildingBlock.Domain/PublicAPI.Shipped.txt](src/BuildingBlock/BuildingBlock.Domain/PublicAPI.Shipped.txt) |
| Added | [src/BuildingBlock/BuildingBlock.Domain/PublicAPI.Unshipped.txt](src/BuildingBlock/BuildingBlock.Domain/PublicAPI.Unshipped.txt) |
| Added | [src/BuildingBlock/BuildingBlock.Infrastructure.EntityFrameworkCore.SqlServer/PublicAPI.Shipped.txt](src/BuildingBlock/BuildingBlock.Infrastructure.EntityFrameworkCore.SqlServer/PublicAPI.Shipped.txt) |
| Added | [src/BuildingBlock/BuildingBlock.Infrastructure.EntityFrameworkCore.SqlServer/PublicAPI.Unshipped.txt](src/BuildingBlock/BuildingBlock.Infrastructure.EntityFrameworkCore.SqlServer/PublicAPI.Unshipped.txt) |
| Added | [src/BuildingBlock/BuildingBlock.Infrastructure/PublicAPI.Shipped.txt](src/BuildingBlock/BuildingBlock.Infrastructure/PublicAPI.Shipped.txt) |
| Added | [src/BuildingBlock/BuildingBlock.Infrastructure/PublicAPI.Unshipped.txt](src/BuildingBlock/BuildingBlock.Infrastructure/PublicAPI.Unshipped.txt) |

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

Start executes the same challenge/outbox path before any account lookup for both known/new emails; identity-dependent nextAction is computed only after OTP proof. Source inspection confirms pipeline logging records request name/error code/trace rather than the request object, OTP or token. These structural controls and neutral-response API tests support the enumeration/logging assessment; no statistical load/timing benchmark is claimed.

Full registration commits Patient+disabled User+role+link+attestations+invitation+outbox atomically; Claim approval creates no User/link, and activation links original PatientId. Linked email change updates both identity/login emails with proof in one transaction; conflict leaves both old values. Evidence streams use private storage and metadata-only access audit. Matching never joins medical data. Existing clinical privacy/age/guardian rules are not relaxed; regression setup now performs required staff verification.

Retention tests prove90d boundaries, legal hold placement/release authorization, durable tombstones surviving transient storage deletion failure, retry, and24m audit retirement while live Patient/AccountLinks remain. Sensitive purging defaults off. Actual malware scanner, SMTP/secret provisioning and production privacy/legal approval are release gates; the existing scanner abstraction is reused and no actual production scanner execution is claimed. Media compensation uses the existing retained-reference pattern; persistent external deletion failure still needs operator reconciliation and operator reconciliation. A direct lost-acknowledgement simulation now proves committed private media survives compensation while an unreferenced upload is deleted; a native independent-worker test proves a committed legal hold prevents retirement/deletion across SQL contexts.

## 6. Test evidence and commands

**Final remote CI: [run 39160421337](https://github.com/LoaiSaberC4r/Wasla/actions/runs/39160421337), source d79658a568dbfc4273b9b9dc1bab695043d316ab, PASS.** Final build reports 0 warnings / 0 errors. Test totals: **922 passed, 0 failed, 0 skipped**. Raw TRX files are uploaded as the run's test-results artifact. Machine-readable counts, prior failures and local TRX paths are in [test evidence](PHASE-5-FINAL-TEST-EVIDENCE.json).

| Final CI category | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Wasla unit | 290 | 0 | 0 |
| Wasla integration/regression | 245 | 0 | 0 |
| SQL Server concurrency | 41 | 0 | 0 |
| Wasla architecture | 23 | 0 | 0 |
| BuildingBlock foundation/compatibility | 323 | 0 | 0 |

Local environment: Windows/PowerShell, SDK10.0.401, EF CLI10.0.10, SQL Express using integrated authentication. Remote: Ubuntu, SDK10.0.402/runtime10.0.13, SQL Server2022 service container. Only disposable GUID fixture databases and synthetic .test addresses/documents were used. No development/non-disposable database was reset. Two concurrent xUnit fixture threads bound SQL load; race tests still use genuinely concurrent requests.

Additional final local evidence: unit290passed, Phase5 hosted API/native SQL26passed, BuildingBlock323passed; EF reports no pending model changes. These runs cover all 20 new routes and six affected contracts, ordinary SuperAdmin review and live evidence-permission revocation, wrong applicant denial, activation expiry during password hashing, original-Patient linking, normalization of valid historical email variants, role-specific409, 401/403 email-change authorization, original submission-based90d expiry, SQL identity/approval/activation/duplicate races, private media rollback/uncertain commit, legal-hold contention and durable purge retry. The final CI reruns the complete solution and foundation suites against final source.

Commands executed; process SQL connection configuration is intentionally omitted from published examples:

```powershell
dotnet --info
dotnet restore Wasla.sln
dotnet build Wasla.sln --configuration Release --no-restore -warnaserror
dotnet test tests/Wasla.Tests.Unit --configuration Release --no-build
dotnet test tests/Wasla.Tests.Integration --configuration Release --no-build
dotnet test tests/Wasla.Tests.Architecture --configuration Release --no-build
dotnet test Wasla.sln --configuration Release --no-build --logger trx --results-directory TestResults/phase5-final-solution -v quiet -- xUnit.MaxParallelThreads=2 xUnit.LongRunningTestSeconds=90 xUnit.DiagnosticMessages=true
dotnet test tests/Wasla.Tests.Integration --configuration Release --no-build --filter FullyQualifiedName~Phase5Final --logger trx --results-directory TestResults/phase5-final-normalization -v quiet -- xUnit.MaxParallelThreads=2
dotnet test src/BuildingBlock/BuildingBlock.Tests --configuration Release --logger trx --results-directory TestResults/phase5-final-foundation -v quiet
dotnet build tools/Wasla.PatientIdentityMaintenance --configuration Release -warnaserror
dotnet ef migrations has-pending-model-changes --project src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer --startup-project src/Wasla/Wasla.Api --context WaslaDbContext --configuration Release --no-build
```

CI executes unit, architecture and both complementary integration filters Category!=SQLServerConcurrency / Category=SQLServerConcurrency with TRX output and xUnit.MaxParallelThreads=2, then runs the BuildingBlock project. The two filters cover the full integration assembly. Earlier isolated artifacts builds used --artifacts-path TestResults/phase5-closure-final-artifacts; all exact local runs remain in the evidence manifest.

Baseline attribution and resolution:

- Fresh main:270unit/21architecture pass; native integration258pass/2fail of260. The two Phase18 dashboard failures asserted an outdated false base flag. Tests now compare actual configuration while retaining effective fail-closed family policy and denied dependent reads.
- Appendix A's Phase16 sharing failure did not reproduce in the completed local baseline but did reproduce in [initial branch CI39139538569](https://github.com/LoaiSaberC4r/Wasla/actions/runs/39139538569). Investigation found a configured requesting host and an unconfigured approving host; local developer secrets masked the latter. Both doctors now use the explicitly keyed host, with a new assertion that the unconfigured host still denies approval. No clinical privacy flag or rule was weakened. Final CI passes this SQL scenario.
- Clean archived main BuildingBlock:316pass/7fail of323. Wrong source-layout assumptions, an outdated SDK policy assertion and a source-boundary scan were corrected. An ignored source copy caused an extra checkout-only duplicate-project failure; restricting project inventory to actual src/tools trees resolved it without deleting user files.
- Five API-reference pairs were missing from both source trees/history. They were independently captured from the clean pre-change main assemblies built during initial baseline execution, with source revision/assembly SHA256 recorded. The compatibility test now compares all 744 frozen public type/member signatures, retaining every former assertion. [Provenance](src/BuildingBlock/BuildingBlock.Tests/API-BASELINE-PROVENANCE.md) records the independent capture and its limits; these are not claimed to be recovered historical files or snapshots of the code under test. All323 foundation tests now pass locally and in CI.
- SSH.NET2025.1.0 arrived transitively through Testcontainers. Central transitive pin2026.0.0 resolves the reported advisories; no NU1903 remains in final runs. Maintainer references: [remote-path handling](https://github.com/advisories/GHSA-mggc-4xg6-vcxf), [recursive filename handling](https://github.com/advisories/GHSA-q939-rpr3-3284).
- Intermediate setup/compile/assertion failures were corrected, not hidden: legacy fixtures now obtain OTP/attestation or legitimate new-registration provenance; a new missing-evidence test now supplies the required evidenceType so it tests422 rather than malformed400. Earlier overlapping Windows hosts caused assembly-copy locks/SQL contention; interrupted runs are not final acceptance evidence. No test was disabled, removed or skipped.

Final contract review additionally corrected the separate-UserType approval conflict code, modification-request inactivity timestamps, expiry during asynchronous hashing, preservation of the existing Patient.ConcurrencyConflict code, conditional EmailChange401/403 and normalized comparison of valid historical identity emails. Regression assertions cover each correction. No migration cleanup or business-rule substitution was used.

## 7. Change control

[Binding approved addendum](SOURCE-OF-TRUTH-PHASE-5-FINAL-ADDENDUM.md) preserves superseded text and both owner execution clarifications. Master v1.2 was absent; no claim is made to editing it. Suggested Master amendment: adopt this addendum and record Phase5 as COMPLETED with the final CI/source evidence in this report; production release gates remain separate. Existing Phase16/18 approved source addenda are not overwritten. No Jira updates, multi-UserType login, account conversion, medical merge or extra public API were introduced.

## 8. Frontend handoff

[Frontend handoff](PHASE-5-FINAL-FRONTEND-HANDOFF.md) covers Basic/Full, global search/Use Existing Patient, OTP nextAction, independent Claim proof/review/modification/rejection, secure activation/resend/Login, email change, localization, age guidance and retry conflicts. [API reference](PHASE-5-FINAL-API-CHANGES.md) provides all payloads and compatibility impacts. Legacy E01 requires proof/header; E03 redacts raw phone; E05 requires verified changed email; existing operational routes remain.

## 9. Closure determination

**Phase5 COMPLETED.** Final source and CI evidence above verify the Definition of Done. No unresolved implementation blocker or known failing regression remains.

| Definition of Done | Evidence |
|---|---|
| New scoped adult Full with disabled invitation-only credentials | Registration API / expiry / live delegation tests |
| Basic minimum contact preserved, no ApplicationUser | Existing Phase5 unit / Basic / duplicate API tests |
| Optional normalized unique identity email, shared phones/contact emails | Native SQL migration/constraints and normalization tests |
| Same global Patient reused in existing reservation/Walk-In without clinical escalation | Cross-doctor reservation and foreign-patient Walk-In tests |
| Existing Basic email routes verified self-registration to Claim | Registration / Claim tests, including legacy alias |
| Claim review/modification/resubmission/rejection/approval/link/expiry/history | Claim / transition / retention tests |
| Original Patient linked once after activation, relationships preserved | Native approval/activation race tests and unchanged PatientId/FK model |
| One operational UserType and unique account email | Account-conflict / authentication regression tests |
| Six affected and exactly 20 new routes | Swagger contract / architecture tests, API handoff |
| Ten appended permissions, prior 303 IDs unchanged, live authorization | Frozen permission IDs / delegation / evidence revocation tests |
| OTP10m/5/60s, continuation30m, one-use48h invitation and outbox | Domain / API boundary / resend / rollback tests |
| Private evidence, safe matching, no enumeration, localization and audit | Security / evidence / architecture / existing localization tests |
| Retention90d/24m, durable tombstones/retry, legal hold | Retention / native worker contention / real CLI tests |
| Additive migration, clean model, SQL native uniqueness/rowversion/races | Migration/SQL artifact and final EF/native tests |
| Complete cross-phase/foundation regression evidence and handoff | Final CI922 passed / 0 failed / 0 skipped; all listed deliverables |

Production release remains gated by jurisdiction-specific legal/privacy retention approval, actual malware scanner and secure SMTP/secrets provisioning, controlled deployment/backup/recovery and throughput/monitoring acceptance. Sensitive purging stays false by default. This report does not declare production ready. Final documentation-only changes do not modify the runtime/test/CI source verified at the commit above.
