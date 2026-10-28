# Doctor My Patients V1 changed-file manifest

Base: `a019a0cf9be548cb8918e5513b5b67b062e23826`; branch: `codex/doctor-my-patients-v1`.

65 files: 36 added and 29 modified. Generated console logs and ignored local QA/TRX/PDF artifacts are excluded. No pre-existing user edits were present when the branch was created.

## API

| Change | Repository-relative path | Purpose |
| --- | --- | --- |
| Added | [src/Wasla/Wasla.Api/Controllers/DoctorPatientsController.cs](../../src/Wasla/Wasla.Api/Controllers/DoctorPatientsController.cs) | Seven patient GETs plus the private issued-version PDF GET. |
| Added | [src/Wasla/Wasla.Api/OpenApi/DoctorPatientsOperationFilter.cs](../../src/Wasla/Wasla.Api/OpenApi/DoctorPatientsOperationFilter.cs) | Typed private GET contracts, required headers and binary PDF schema. |
| Modified | [src/Wasla/Wasla.Api/Program.cs](../../src/Wasla/Wasla.Api/Program.cs) | Workspace no-store/nosniff middleware and final-order OpenAPI filter registration. |

## Application

| Change | Repository-relative path | Purpose |
| --- | --- | --- |
| Modified | [src/Wasla/Wasla.Application/DependencyInjection.cs](../../src/Wasla/Wasla.Application/DependencyInjection.cs) | Approved task, rule traceability, contracts or detailed implementation/handoff documentation. |
| Modified | [src/Wasla/Wasla.Application/Features/Clinical/ClinicalAccessService.cs](../../src/Wasla/Wasla.Application/Features/Clinical/ClinicalAccessService.cs) | Separate own read policy; original active-Practice writers preserved. |
| Modified | [src/Wasla/Wasla.Application/Features/Clinical/ClinicalMutations.cs](../../src/Wasla/Wasla.Application/Features/Clinical/ClinicalMutations.cs) | Response capability projection disables inactive-Practice actions/drafts only. |
| Modified | [src/Wasla/Wasla.Application/Features/Clinical/ClinicalQueries.cs](../../src/Wasla/Wasla.Application/Features/Clinical/ClinicalQueries.cs) | Historical own Encounter/amendment reads, relation checks, rechecks and audit. |
| Modified | [src/Wasla/Wasla.Application/Features/Diagnostics/DiagnosticContracts.cs](../../src/Wasla/Wasla.Application/Features/Diagnostics/DiagnosticContracts.cs) | Own source lookup and permission-aware private media read port. |
| Modified | [src/Wasla/Wasla.Application/Features/Diagnostics/DiagnosticOrderHandler.cs](../../src/Wasla/Wasla.Application/Features/Diagnostics/DiagnosticOrderHandler.cs) | Keep active-Practice writer guard on idempotency replay. |
| Modified | [src/Wasla/Wasla.Application/Features/Diagnostics/DiagnosticQueries.cs](../../src/Wasla/Wasla.Application/Features/Diagnostics/DiagnosticQueries.cs) | Own source historical authorization, fresh grants and safe audited media disposal. |
| Modified | [src/Wasla/Wasla.Application/Features/Diagnostics/DiagnosticResultHandler.cs](../../src/Wasla/Wasla.Application/Features/Diagnostics/DiagnosticResultHandler.cs) | Keep active-Practice review/result writer guard on idempotency replay. |
| Added | [src/Wasla/Wasla.Application/Features/DoctorPatients/DoctorOwnHistoricalReadPolicy.cs](../../src/Wasla/Wasla.Application/Features/DoctorPatients/DoctorOwnHistoricalReadPolicy.cs) | Explicit own closed-Practice read-only decision; never a writer guard. |
| Added | [src/Wasla/Wasla.Application/Features/DoctorPatients/DoctorPatientContracts.cs](../../src/Wasla/Wasla.Application/Features/DoctorPatients/DoctorPatientContracts.cs) | DTOs, CQRS requests and read/export ports; no Patient ownership model. |
| Added | [src/Wasla/Wasla.Application/Features/DoctorPatients/DoctorPatientErrors.cs](../../src/Wasla/Wasla.Application/Features/DoctorPatients/DoctorPatientErrors.cs) | Controlled localized PDF-unavailable errors. |
| Added | [src/Wasla/Wasla.Application/Features/DoctorPatients/DoctorPatientHandlers.cs](../../src/Wasla/Wasla.Application/Features/DoctorPatients/DoctorPatientHandlers.cs) | Authorized reads, post-read/re-render rechecks and required audits. |
| Added | [src/Wasla/Wasla.Application/Features/DoctorPatients/DoctorPatientRelationshipPolicy.cs](../../src/Wasla/Wasla.Application/Features/DoctorPatients/DoctorPatientRelationshipPolicy.cs) | Current DB Doctor state, effective grants and trusted admission proof. |
| Added | [src/Wasla/Wasla.Application/Features/DoctorPatients/DoctorPatientValidators.cs](../../src/Wasla/Wasla.Application/Features/DoctorPatients/DoctorPatientValidators.cs) | Bounded filters, pagination, resource/status and PDF input validation. |
| Modified | [src/Wasla/Wasla.Application/Features/Medications/PrescriptionHandlers.cs](../../src/Wasla/Wasla.Application/Features/Medications/PrescriptionHandlers.cs) | Historical own issued reads/versions; original correction/void writers preserved. |
| Modified | [src/Wasla/Wasla.Application/Features/Reservations/ReservationManagement.cs](../../src/Wasla/Wasla.Application/Features/Reservations/ReservationManagement.cs) | Exact owned historical Reservation detail, false capabilities and archive rechecks. |
| Modified | [src/Wasla/Wasla.Application/Features/Tickets/GetTicketDetails/GetTicketDetails.cs](../../src/Wasla/Wasla.Application/Features/Tickets/GetTicketDetails/GetTicketDetails.cs) | Own historical Ticket read, false operations/refunds and source-specific redaction. |

## Domain

| Change | Repository-relative path | Purpose |
| --- | --- | --- |
| Modified | [src/Wasla/Wasla.Domain/Resources/ErrorMessage.ar.resx](../../src/Wasla/Wasla.Domain/Resources/ErrorMessage.ar.resx) | Arabic controlled PDF failure text. |
| Modified | [src/Wasla/Wasla.Domain/Resources/ErrorMessage.resx](../../src/Wasla/Wasla.Domain/Resources/ErrorMessage.resx) | English controlled PDF failure text. |
| Modified | [src/Wasla/Wasla.Domain/Security/SystemSecurity.cs](../../src/Wasla/Wasla.Domain/Security/SystemSecurity.cs) | Append-only three-permission suffix; existing deterministic IDs unchanged. |

## SQL Server infrastructure

| Change | Repository-relative path | Purpose |
| --- | --- | --- |
| Modified | [src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/DependencyInjection.cs](../../src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/DependencyInjection.cs) | Approved task, rule traceability, contracts or detailed implementation/handoff documentation. |
| Modified | [src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/AuditExplorer.cs](../../src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/AuditExplorer.cs) | New medical-read audit category uses existing sensitive-audit authorization. |
| Modified | [src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/ClinicalReadService.cs](../../src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/ClinicalReadService.cs) | Permission-aware own diagnostic summaries in historical Encounter reads. |
| Modified | [src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Configurations/ClinicalConfigurations.cs](../../src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Configurations/ClinicalConfigurations.cs) | Doctor/Patient/start lookup index for MedicalEncounters. |
| Modified | [src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Configurations/ReservationConfigurations.cs](../../src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Configurations/ReservationConfigurations.cs) | Live/archive Doctor/Patient/schedule lookup indexes. |
| Modified | [src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Configurations/TicketConfigurations.cs](../../src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Configurations/TicketConfigurations.cs) | Live/archive Doctor/Patient/check-in/ID lookup indexes. |
| Modified | [src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/DiagnosticReadService.cs](../../src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/DiagnosticReadService.cs) | Own closed-Practice requests/results/submissions/versions and false write capabilities. |
| Added | [src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/DoctorPatientReadService.cs](../../src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/DoctorPatientReadService.cs) | Live/archive SQL relationship grouping, history projections and metadata audit. |
| Added | [src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Migrations/20261028111250_DoctorMyPatientsReadIndexes.Designer.cs](../../src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Migrations/20261028111250_DoctorMyPatientsReadIndexes.Designer.cs) | EF target model metadata for the lookup-index migration. |
| Added | [src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Migrations/20261028111250_DoctorMyPatientsReadIndexes.cs](../../src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Migrations/20261028111250_DoctorMyPatientsReadIndexes.cs) | Six composite lookup indexes with reversible index-only Down. |
| Modified | [src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Migrations/WaslaDbContextModelSnapshot.cs](../../src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Migrations/WaslaDbContextModelSnapshot.cs) | Snapshot corresponding to the controlled lookup-index migration. |
| Modified | [src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/WaslaDbContext.Archives.cs](../../src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/WaslaDbContext.Archives.cs) | Doctor/Patient lookup index on permanent TicketRegistry. |
| Modified | [src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/WaslaSecuritySeeder.cs](../../src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/WaslaSecuritySeeder.cs) | Idempotent Doctor-only default grants. |
| Added | [src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Reports/PrescriptionPdfRenderer.cs](../../src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Reports/PrescriptionPdfRenderer.cs) | Offline encoded bilingual issued-version rendering with status marks and limits. |

## Tests

| Change | Repository-relative path | Purpose |
| --- | --- | --- |
| Added | [tests/Wasla.Tests.Architecture/DoctorPatientsArchitectureTests.cs](../../tests/Wasla.Tests.Architecture/DoctorPatientsArchitectureTests.cs) | Feature acceptance/regression coverage. |
| Added | [tests/Wasla.Tests.Integration/DoctorPatientDiagnosticHistoryTests.cs](../../tests/Wasla.Tests.Integration/DoctorPatientDiagnosticHistoryTests.cs) | Feature acceptance/regression coverage. |
| Added | [tests/Wasla.Tests.Integration/DoctorPatientExportSecurityTests.cs](../../tests/Wasla.Tests.Integration/DoctorPatientExportSecurityTests.cs) | Feature acceptance/regression coverage. |
| Added | [tests/Wasla.Tests.Integration/DoctorPatientMembershipTests.cs](../../tests/Wasla.Tests.Integration/DoctorPatientMembershipTests.cs) | Feature acceptance/regression coverage. |
| Added | [tests/Wasla.Tests.Integration/DoctorPatientPrescriptionTests.cs](../../tests/Wasla.Tests.Integration/DoctorPatientPrescriptionTests.cs) | Feature acceptance/regression coverage. |
| Added | [tests/Wasla.Tests.Integration/DoctorPatientsApiTests.cs](../../tests/Wasla.Tests.Integration/DoctorPatientsApiTests.cs) | Feature acceptance/regression coverage. |
| Added | [tests/Wasla.Tests.Integration/DoctorPatientsContractTests.cs](../../tests/Wasla.Tests.Integration/DoctorPatientsContractTests.cs) | Feature acceptance/regression coverage. |
| Added | [tests/Wasla.Tests.Integration/DoctorPatientsHistoricalTests.cs](../../tests/Wasla.Tests.Integration/DoctorPatientsHistoricalTests.cs) | Feature acceptance/regression coverage. |
| Added | [tests/Wasla.Tests.Integration/DoctorPatientsSqlTests.cs](../../tests/Wasla.Tests.Integration/DoctorPatientsSqlTests.cs) | Feature acceptance/regression coverage. |
| Modified | [tests/Wasla.Tests.Integration/Phase12ArchiveTestServices.cs](../../tests/Wasla.Tests.Integration/Phase12ArchiveTestServices.cs) | Complete minimal component fixture registration with the actual read service. |
| Modified | [tests/Wasla.Tests.Integration/Phase12TicketRefundProjectionTests.cs](../../tests/Wasla.Tests.Integration/Phase12TicketRefundProjectionTests.cs) | Seed current Doctor role/DB/source-finance rights required by its existing assertions. |
| Added | [tests/Wasla.Tests.Unit/DoctorPatientPdfTests.cs](../../tests/Wasla.Tests.Unit/DoctorPatientPdfTests.cs) | Feature acceptance/regression coverage. |
| Added | [tests/Wasla.Tests.Unit/DoctorPatientPolicyTests.cs](../../tests/Wasla.Tests.Unit/DoctorPatientPolicyTests.cs) | Feature acceptance/regression coverage. |
| Modified | [tests/Wasla.Tests.Unit/Phase5FinalIdentityDomainTests.cs](../../tests/Wasla.Tests.Unit/Phase5FinalIdentityDomainTests.cs) | Preserve old permission IDs and assert the new append-only suffix. |

## Documentation and evidence

| Change | Repository-relative path | Purpose |
| --- | --- | --- |
| Added | [DOCTOR-MY-PATIENTS-V1-API-CHANGES.md](../../DOCTOR-MY-PATIENTS-V1-API-CHANGES.md) | Approved task, rule traceability, contracts or detailed implementation/handoff documentation. |
| Added | [DOCTOR-MY-PATIENTS-V1-FRONTEND-HANDOFF.md](../../DOCTOR-MY-PATIENTS-V1-FRONTEND-HANDOFF.md) | Approved task, rule traceability, contracts or detailed implementation/handoff documentation. |
| Added | [DOCTOR-MY-PATIENTS-V1-IMPLEMENTATION-REPORT.md](../../DOCTOR-MY-PATIENTS-V1-IMPLEMENTATION-REPORT.md) | Approved task, rule traceability, contracts or detailed implementation/handoff documentation. |
| Modified | [README.md](../../README.md) | Link the current implementation, API, handoff and exact verification artifacts. |
| Added | [SOURCE-OF-TRUTH-DOCTOR-MY-PATIENTS-V1-ADDENDUM.md](../../SOURCE-OF-TRUTH-DOCTOR-MY-PATIENTS-V1-ADDENDUM.md) | Approved task, rule traceability, contracts or detailed implementation/handoff documentation. |
| Added | [WASLA-CODEX-DOCTOR-MY-PATIENTS-V1.md](../../WASLA-CODEX-DOCTOR-MY-PATIENTS-V1.md) | Approved task, rule traceability, contracts or detailed implementation/handoff documentation. |
| Added | [docs/doctor-patients/changed-files.md](../../docs/doctor-patients/changed-files.md) | Complete review manifest generated from the working tree. |
| Added | [docs/doctor-patients/directory-query.sql](../../docs/doctor-patients/directory-query.sql) | Parameterized SQL captured from the synthetic scale acceptance test. |
| Added | [docs/doctor-patients/migration.sql](../../docs/doctor-patients/migration.sql) | Controlled idempotent forward migration SQL. |
| Added | [docs/doctor-patients/openapi.json](../../docs/doctor-patients/openapi.json) | Actual generated eight-route contract subset and transitive DTO schemas. |
| Added | [docs/doctor-patients/response-examples.json](../../docs/doctor-patients/response-examples.json) | Synthetic non-empty response examples checked against schema property shapes. |
| Added | [docs/doctor-patients/rollback.sql](../../docs/doctor-patients/rollback.sql) | Controlled reverse index migration SQL. |
| Added | [docs/doctor-patients/verification.json](../../docs/doctor-patients/verification.json) | Observed run counters, commands, artifacts/hashes and acceptance case outcomes. |
| Added | [docs/doctor-patients/verify.ps1](../../docs/doctor-patients/verify.ps1) | Syntax-checked convenience script for sequential reproducible local build/test/model checks. |
