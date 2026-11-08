# Changed files by layer

## Domain

- `src/Wasla/Wasla.Domain/Families/ChildOnboardingRequest.cs`
- `src/Wasla/Wasla.Domain/Families/FamilyCandidateSelection.cs`
- `src/Wasla/Wasla.Domain/Families/FamilyRelationshipRequest.cs`
- `src/Wasla/Wasla.Domain/Families/ParentChildOperationalAuthority.cs`
- `src/Wasla/Wasla.Domain/Resources/ErrorMessage.ar.resx`
- `src/Wasla/Wasla.Domain/Resources/ErrorMessage.resx`
- `src/Wasla/Wasla.Domain/Resources/FamilySelfServiceText.cs`
- `src/Wasla/Wasla.Domain/Security/FamilySelfServicePermissionNames.cs`
- `src/Wasla/Wasla.Domain/Security/SystemSecurity.cs`

## Application

- `src/Wasla/Wasla.Application/Features/Families/ChildOnboarding.cs`
- `src/Wasla/Wasla.Application/Features/Families/ChildOnboardingValidation.cs`
- `src/Wasla/Wasla.Application/Features/Families/DependentAccess.cs`
- `src/Wasla/Wasla.Application/Features/Families/DependentOperationalReads.cs`
- `src/Wasla/Wasla.Application/Features/Families/FamilyDiscovery.cs`
- `src/Wasla/Wasla.Application/Features/Families/FamilyHandlerAccess.cs`
- `src/Wasla/Wasla.Application/Features/Families/FamilyRelationshipWorkflow.cs`
- `src/Wasla/Wasla.Application/Features/Patients/PatientManagement.cs`
- `src/Wasla/Wasla.Application/Features/Reservations/ReservationManagement.cs`
- `src/Wasla/Wasla.Application/Features/Subscriptions/CommercialPermissionCatalog.cs`

## Infrastructure

- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/DependencyInjection.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Media/ClamAvMalwareScanner.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Notifications/NotificationEligibility.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/ArchiveQueries.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/ChildOnboardingWorkflow.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Configurations/ChildOnboardingConfigurations.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Configurations/FamilyCandidateConfiguration.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Configurations/OperationalAuthorityConfigurations.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Configurations/ProfileConfigurations.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/DependentAccessPolicy.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/FamilyCandidateStore.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/FamilyIdentitySearch.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/FamilyRelationshipMedicalVerification.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Migrations/20261107223612_FamilySelfServiceOperationalFoundation.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Migrations/20261107223612_FamilySelfServiceOperationalFoundation.Designer.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Migrations/WaslaDbContextModelSnapshot.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/WaslaDbContext.FamilyMedical.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/WaslaSecuritySeeder.cs`

## API

- `src/Wasla/Wasla.Api/Controllers/ChildOnboardingController.cs`
- `src/Wasla/Wasla.Api/Controllers/DependentOperationsController.cs`
- `src/Wasla/Wasla.Api/Controllers/FamiliesController.cs`
- `src/Wasla/Wasla.Api/Program.cs`

## Tests

- `tests/Wasla.Tests.Integration/FamilySelfServiceApiTests.cs`
- `tests/Wasla.Tests.Integration/Phase16FamilyFixture.cs`
- `tests/Wasla.Tests.Integration/Phase16FinalGapTests.cs`
- `tests/Wasla.Tests.Integration/Phase17RecipientNotificationTests.cs`
- `tests/Wasla.Tests.Unit/ClamAvMalwareScannerTests.cs`
- `tests/Wasla.Tests.Unit/DoctorPatientPolicyTests.cs`
- `tests/Wasla.Tests.Unit/FamilySelfServiceFoundationTests.cs`
- `tests/Wasla.Tests.Unit/Phase5FinalIdentityDomainTests.cs`

## Docs

- `docs/family-self-service/API-HANDOFF.md`
- `docs/family-self-service/baseline-api-inventory.json`
- `docs/family-self-service/baseline-permissions.json`
- `docs/family-self-service/BASELINE.md`
- `docs/family-self-service/CHANGED-FILES.md`
- `docs/family-self-service/credential-review.json`
- `docs/family-self-service/IMPLEMENTATION-REPORT.md`
- `docs/family-self-service/MIGRATION.sql`
- `docs/family-self-service/new-permissions.json`
- `docs/family-self-service/OWNER-EPIC-2026-11-07.txt`
- `docs/family-self-service/ROLLOUT.md`
- `docs/family-self-service/TEST-EVIDENCE.json`
- `docs/subscriptions/permission-coverage.csv`
- `SOURCE-OF-TRUTH-FAMILY-SELF-SERVICE-2026-11-07.md`

## Config

- `.gitignore`
- `src/Wasla/Wasla.Api/appsettings.json`
- `src/Wasla/Wasla.Api/appsettings.Production.json`

