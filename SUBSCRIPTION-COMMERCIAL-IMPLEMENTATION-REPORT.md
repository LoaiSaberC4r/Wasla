# Commercial subscription implementation report

Branch: `codex/commercial-subscriptions`. Base commit: `23ed851`. Implementation date: 2026-11-02.

PR #36 received targeted authorization hardening on 2026-11-03. Current archive policy, capability contract, permission coverage, regression evidence, reseeding requirements, and merge recommendation are documented in [PR36-PRE-MERGE-HARDENING-REPORT.md](PR36-PRE-MERGE-HARDENING-REPORT.md). The implementation and original verification record below are retained.

## Baseline inspection

The repository targets .NET 10 and uses Clean Architecture, MediatR CQRS, `Result/Error`, FluentValidation, BuildingBlock transactions, one `WaslaDbContext`, and existing email Outbox infrastructure. `BuildingBlockWithNET10` transaction behavior and the historical v1.2 Source of Truth were inspected. The source left monetization open; the approved rules are recorded in the separate commercial addendum.

Baseline evidence:

- A clean detached baseline build succeeded with zero warnings and errors. An initial build/test overlap caused output-file locks; the isolated build removed that environmental interference.
- Original unit tests: 395 passed. Original architecture tests: 26 passed.
- Clean relevant integration run: 33 passed, 19 SQL-dependent cases skipped, followed by the configured 120-second inactivity abort in `PublicDiscoveryApiTests.Search_with_one_thousand_candidates_has_constant_queries_and_page_scoped_hydration`. No failed assertion preceded that abort. A later dedicated run of that test completed successfully in 10 minutes 32 seconds; the baseline timeout was not suppressed or represented as a successful full run.
- Test logs are under `artifacts/subscription-*.log`; TRX files are under each test project's `TestResults`.

Integration points inspected and extended include registration, `DoctorLifecycleService`, doctor practices/configuration/segments, the legacy onboarding location factory, reception management/account state, permission authorization and the append-only security catalog, public discovery, reservations, ticket access/check-in/clinical workflows, diagnostic/prescription access, reporting, finance access, shared persistence, and the email Outbox.

## Implemented behavior

The domain contains independent commercial products, plans, features, plan entitlements, permission mappings, subscriptions, relational feature snapshots, dedicated SaaS payments, immutable history, change requests, reminder schedules, and a durable legacy enrollment batch.

The catalog resolver reads prices and capacities through validated `IOptionsMonitor<SubscriptionCatalogOptions>` and reads entitlements from the database. Selection persists an immutable terms JSON snapshot and relational feature snapshots. Later catalog/configuration changes do not rewrite accepted terms.

Registration, approval-started trials, exact manual payment activation, reasoned trial extension, requested/reviewed upgrades, renewal-time downgrades/cycle changes, manual renewal, cancellation at period end, and explicit legacy enrollment use the existing transactional command marker. The `Scheduled` state represents a paid future renewal period; no automatic renewal or charging is implemented.

Commercial permission checks layer onto existing permission, user activity, doctor approval, reception assignment, privacy, and relationship checks. Shared clinical/ticket/diagnostic/prescription/financial access paths enforce subscriptions even where a controller does not use a permission attribute. Historical reads retain accepted features. Continuity mutations resolve the original reservation/ticket/encounter commitment, including linked clinical and patient-payment resources, rather than granting blanket expired write access.

All persisted practices consume capacity. Reception usage counts distinct owned identities with active application users, independently of assignment activity. Reception account state changes preserve profiles and assignments and use the reception row version plus account-state revision. The existing inactive onboarding draft consumes pending selected capacity and cannot unlock operations.

Booking Only keeps the existing booking engine. Its reservation entitlement is ReadOnly; clinical, queue, reception, and financial access are denied. Practice defaults disable walk-in without check-in entitlements. A narrow price-presentation operation updates the existing default segment/new-consultation price without exposing operational segment configuration.

## Database and migration

Migration: `20261102190031_CommercialSubscriptionFoundation`.

| Table | Purpose / important constraints |
|---|---|
| CommercialProducts | Unique product code, catalog audit, row version |
| CommercialPlans | Product FK; unique product/code; public/internal visibility; row version |
| CommercialFeatures | Unique stable code; independent implementation/readiness metadata; row version |
| PlanFeatureEntitlements | Unique plan/feature; access level |
| FeaturePermissionMappings | Unique existing permission ID; required access level |
| DoctorSubscriptions | Doctor/product/plan FKs; immutable accepted terms; lookup indexes; filtered uniqueness for pending, active, scheduled, and one trial per doctor; row version |
| SubscriptionFeatureSnapshots | Unique subscription/feature; accepted feature code/access |
| SubscriptionPayments | Dedicated SaaS money records, `decimal(18,2)`; unique idempotency key; period/payment lookup |
| SubscriptionHistories | Append-only events, previous/new terms, metadata, actor, unique event key |
| SubscriptionChangeRequests | Accepted target snapshot, difference, review/effective timestamps, row version; one Pending request per subscription |
| SubscriptionReminderSchedules | Unique subscription/reminder/revision; due-work index; row version |
| LegacyTransitionEnrollmentBatches | Stable launch batch identity, shared period, completion state, row version |

The existing `Receptions` table gains `AccountStateRevision`; its existing row version protects activity transitions. Commercial FKs use Restrict/NO ACTION deletion. UTC subscription timestamps use `datetime2(7)`, preserving the precision of the doctor's approval timestamp. JSON remains NVARCHAR(MAX) under SQL Server. The existing SQLite concurrency fallback remains usable. No patient-payment table or revenue-calculation schema is repurposed.

The idempotent up script is [docs/subscriptions/CommercialSubscriptionFoundation.sql](docs/subscriptions/CommercialSubscriptionFoundation.sql). It adds commercial structures and migration history only; it does not delete patient, clinical, reservation, practice, or reception records. No production migration was executed.

## Seed and configuration

`SubscriptionCatalogSeeder` runs after the existing security seeder, inside the existing controlled seeding flag. Stable GUIDs derive from semantic catalog keys. It seeds two products, five plans including the hidden transition plan, 28 existing business features, the approved plan matrix, and mappings to existing permissions. It stores no commercial prices or plan capacities and never overwrites subscription snapshots. Existing records and reviewed readiness metadata are preserved on rerun. The feature-to-plan matrix is in the [commercial addendum](SOURCE-OF-TRUTH-SUBSCRIPTION-COMMERCIAL-ADDENDUM.md).

Validated configuration groups are `CatalogVersion`, `Currency`, `FullPlatform.Trial`, `FullPlatform.Plans`, `BookingOnly`, `LegacyTransition`, `PaymentMethods`, `Policies`, and `BackgroundProcessing`, all under `SubscriptionCatalog`. Validation covers required plans/prices, SQL money precision/range, explicitly configured annual pricing, positive capacities, trial rules/reminders, contiguous/non-overlapping booking tiers, maximum count, legacy minima/duration, and supported policy flags. Configuration uses environment-specific settings and standard environment variable overrides.

Default Booking Only tiers remain empty, with maximum zero and Enabled false; no unapproved prices/count were invented. Legacy enrollment is also disabled by default. Configured transition minimum capacities are explicit (`MinimumPracticeLimit`, `MinimumReceptionLimit`) rather than C# plan-limit constants.

All keys below are relative to `SubscriptionCatalog:`; array indices use standard .NET configuration binding.

| Group | Keys |
|---|---|
| Root | `CatalogVersion`, `Currency` |
| `FullPlatform:Trial` | `Enabled`, `OfferCode`, `DurationDays`, `StartsOnDoctorApproval`, `MaximumTrialsPerDoctor`, `PracticeLimit`, `ReceptionLimit`, `ReminderDaysBeforeEnd`, `ManualExtensionEnabled` |
| `FullPlatform:Plans[]` | `Code`, `MonthlyPrice`, `AnnualPrice`, `PracticeLimit`, `ReceptionLimit` |
| `BookingOnly` | `Enabled`, `PlanCode`, `MaximumSelectablePractices`, `PricingTiers[]:MinPracticeCount`, `PricingTiers[]:MaxPracticeCount`, `PricingTiers[]:MonthlyPrice` |
| `LegacyTransition` | `Enabled`, `DurationDays`, `MinimumPracticeLimit`, `MinimumReceptionLimit` |
| `PaymentMethods` | `Cash`, `Card`, `EWallet` |
| `Policies` | `AutomaticRenewalEnabled`, `ProrationEnabled`, `SetupFeeEnabled`, `PlatformCommissionEnabled`, `TaxCalculationEnabled`, `ManualRenewalEnabled`, `CancellationAtPeriodEnd` |
| `BackgroundProcessing` | `Enabled`, `PollingIntervalSeconds`, `BatchSize` |

## API and permissions

The [Angular API contract](docs/subscriptions/API-CONTRACT.md) contains request/response fields, enum names, row-version handling, payment retry semantics, endpoint permissions, and machine error codes. New routes are:

- `GET /api/v1/public/commercial/catalog`.
- `GET /api/v1/doctors/me/subscription`; `POST` its `/selection`, `/change-requests`, and `/cancel-at-period-end` subroutes.
- `GET /api/v1/admin/subscriptions` and `GET /{subscriptionId}`; `POST /{subscriptionId}/activate`, `/trial-extend`, `/renew`, `/change-requests/{requestId}/approve`, and `/change-requests/{requestId}/reject`.
- `POST /api/v1/admin/subscriptions/legacy-transition/enroll`.
- `PUT /api/v1/doctors/me/practices/{practiceId}/booking-presentation/price`.
- `PUT /api/v1/doctors/me/receptions/{receptionId}/account-state`.

The existing multipart doctor-registration route receives the selection fields documented in the contract. Approved flat change-request names (`UpgradePlan`, `IncreaseBookingPracticeLimit`, `DowngradePlan`, `DecreaseBookingPracticeLimit`, `ChangeBillingCycle`) are accepted. Change requests may omit the subscription row version; reviews and other existing-subscription mutations require their documented row versions.

Appended permissions are `Subscriptions.ViewOwn`, `Subscriptions.RequestChangeOwn`, `Subscriptions.CancelAtPeriodEndOwn`, `Subscriptions.ViewAll`, `Subscriptions.ViewDetails`, `Subscriptions.Activate`, `Subscriptions.ExtendTrial`, `Subscriptions.ReviewChangeRequests`, `Subscriptions.Renew`, `Subscriptions.EnrollLegacyTransition`, and `ReceptionUsers.ManageAccountStateOwn`.

## Authorization, transactions, and concurrency

The original 324 permission identities remain unchanged. Eleven entries are appended: the three own-subscription permissions, seven SuperAdmin subscription permissions, and `ReceptionUsers.ManageAccountStateOwn`. Reception never receives subscription financial permissions. The API contract lists every route and permission.

`ISubscriptionDataStore` and `IWaslaDataStore` resolve the same scoped context. Registration and approval changes commit with subscription rows, snapshots, history, reminders, and Outbox messages. Payment recording and activation/change/renewal share one transaction. Replacement transitions are saved as Superseded before inserting the new Active state, within that same transaction, to respect the filtered unique index.

SQL Server admission uses `Wasla:Subscription:Doctor:{doctorId:N}`, Exclusive mode, Transaction ownership, and a bounded lock timeout. State and capacity reads occur after admission; unchanged entities tracked by pre-command authorization are refreshed. Mutable subscription, request, reminder, batch, doctor, and reception row versions are respected. Safe Result/Error mapping handles concurrency and unique constraints without exposing SQL internals.

Payment retry history fingerprints the complete commercial intent and actor. Reusing a key for a changed intent conflicts. Periods are separate subscription records, so early manual renewal can preserve the current paid period and hold one paid future period without duplicate Active rows.

The lifecycle worker scans bounded due batches and uses a new scope/transaction for each item. Failure tracking from one rollback cannot leak into another doctor's commit. Reminders and expiry notifications use durable event keys. SMTP remains the responsibility of the existing email Outbox worker; no business transaction sends email.

## Verification

Full SQL Server verification uses disposable LocalDB databases and the dedicated test harness, never the configured production database. Machine-readable outcomes and coverage are recorded in [test-evidence.json](docs/subscriptions/test-evidence.json).

| Check | Actual result |
|---|---|
| Full solution build | Zero warnings, zero errors |
| Unit suite | 418 passed, zero failures/skips |
| Architecture suite | 26 passed, zero failures/skips |
| Integration coverage across the recorded runs | All 426 discovered cases accounted for: 425 passed, one existing opt-in browser smoke test skipped; zero unresolved failures |
| Focused subscription and modified-flow regression run | 63 passed, zero failures/skips; includes six real SQL concurrency races |
| Final rebuilt subscription/diagnostic regression run | 45 passed, zero failures/skips; all 33 subscription-specific cases passed |
| Remaining integration batches | 29, 28, 22, and 15 passed respectively, zero failures/skips |
| Migration/backfill/backup/restore SQL rehearsal | Passed after LocalDB path fallback (1 minute 48 seconds) |
| Large patient-directory SQL test | Passed separately (7 minutes 22 seconds) |
| 1,000-candidate public discovery performance test | Passed separately (10 minutes 32 seconds) |

The monolithic integration run passed 293 cases and skipped the existing opt-in browser smoke test, then aborted on the three-minute inactivity limit while two old large tests were executing. It is not reported as a successful full run. Remaining cases were executed in isolated batches with the same SQL connection and separate shared test log files. One early isolated invocation used an incorrect Serilog configuration index and failed startup; the invocation was corrected without relaxing application configuration or assertions. The browser test was not enabled (`WASLA_RUN_BROWSER_TESTS=1`); no new skip was added.

The migration/backfill rehearsal initially exposed an existing LocalDB test-harness assumption: `InstanceDefaultBackupPath` may be NULL. Its backup/restore helper now falls back to directories read from the synthetic database's server-side file metadata. It still performs real COPY_ONLY/CHECKSUM backup, VERIFYONLY, a separate-database restore, and restored row/payment assertions.

Commands used (PowerShell; set `WASLA_SQLSERVER_CONNECTION_STRING` to a dedicated test SQL Server connection before SQL-dependent runs):

```powershell
dotnet build Wasla.sln --no-restore
dotnet test tests/Wasla.Tests.Unit/Wasla.Tests.Unit.csproj --no-build --no-restore --logger "trx;LogFileName=commercial-final-unit.trx"
dotnet test tests/Wasla.Tests.Architecture/Wasla.Tests.Architecture.csproj --no-build --no-restore --logger "trx;LogFileName=commercial-final-architecture.trx"
dotnet test tests/Wasla.Tests.Integration/Wasla.Tests.Integration.csproj --no-build --no-restore --filter "FullyQualifiedName!~Search_with_one_thousand_candidates" --blame-hang-timeout 3m --logger "trx;LogFileName=commercial-subscriptions.trx"
dotnet test tests/Wasla.Tests.Integration/Wasla.Tests.Integration.csproj --no-build --no-restore --filter "FullyQualifiedName~Subscription" --logger "trx;LogFileName=commercial-subscription-focused.trx"
dotnet test tests/Wasla.Tests.Integration/Wasla.Tests.Integration.csproj --no-build --no-restore --filter "FullyQualifiedName~Phase15DiagnosticApiTests|FullyQualifiedName~DoctorPatientDiagnosticHistoryTests|FullyQualifiedName~Subscription" --logger "trx;LogFileName=commercial-final-diagnostics.trx"
dotnet test tests/Wasla.Tests.Integration/Wasla.Tests.Integration.csproj --no-build --no-restore --filter "FullyQualifiedName~DoctorPatientsSqlTests.Archive_move_preserves_membership_and_large_directory_uses_bounded_sql_pages"
dotnet test tests/Wasla.Tests.Integration/Wasla.Tests.Integration.csproj --no-build --no-restore --filter "FullyQualifiedName~Phase18PartTwoMigrationTests.Nonempty_baseline_expand_population_cutover_restart_backfill_and_query_plan_rehearsal"
dotnet test tests/Wasla.Tests.Integration/Wasla.Tests.Integration.csproj --no-build --no-restore --filter "FullyQualifiedName~PublicDiscoveryApiTests.Search_with_one_thousand_candidates_has_constant_queries_and_page_scoped_hydration"
git diff --check
```

For concurrent test processes, the test-only file sink overrides are `Serilog__WriteTo__1__Args__configure__0__Args__path` (a distinct path per process) and `Serilog__WriteTo__1__Args__configure__0__Args__shared=true`. Production logging is unchanged. Exact batch results are retained in the evidence file and local TRX/log files.

## Deployment

1. Back up the database.
2. Deploy the application code.
3. Apply the generated migration through the approved migration process, after its predecessor migrations.
4. Run controlled seeding using the existing initialization/seeding mechanism after migration; do not enable production startup migrations.
5. Confirm the configured Full Platform prices, annual prices, and capacities.
6. Configure approved Booking Only tier prices and maximum before enabling that product.
7. Verify Cash enabled and Card/EWallet disabled.
8. After launch approval, enable and invoke legacy enrollment; repeat while the durable batch is Running. All chunks retain the same dates. Its cohort cutoff is the first product seed timestamp, excluding new commercial registrations.
9. Verify the public catalog and feature-readiness presentation.
10. Verify approval-started trial dates and the original approval email.
11. Verify exact manual Cash activation and a replay with the same key.
12. Verify practice/reception capacity and account-state enforcement.
13. Verify Outbox delivery using deployment email configuration.
14. Verify reminders, expiry, paid future-period start, and safe completion of existing healthcare commitments.

External release work remains approved Booking Only pricing/count, launch authorization for legacy enrollment, feature readiness review, and deployment-specific SMTP/UAT verification. Automatic charging, renewal, gateways, taxes, invoices, add-ons, and Angular implementation were not added.

## Changed files

- `README.md`
- `src/Wasla/Wasla.Api/Controllers/AuthController.cs`
- `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs`
- `src/Wasla/Wasla.Api/Controllers/DoctorReceptionsController.cs`
- `src/Wasla/Wasla.Api/Program.cs`
- `src/Wasla/Wasla.Api/Security/PermissionAuthorization.cs`
- `src/Wasla/Wasla.Api/appsettings.json`
- `src/Wasla/Wasla.Application/DependencyInjection.cs`
- `src/Wasla/Wasla.Application/Email/EmailNotificationContracts.cs`
- `src/Wasla/Wasla.Application/Features/Auth/Authentication.cs`
- `src/Wasla/Wasla.Application/Features/Clinical/ClinicalAccessService.cs`
- `src/Wasla/Wasla.Application/Features/Clinical/ClinicalMutations.cs`
- `src/Wasla/Wasla.Application/Features/Clinical/MedicalRecordAccessPolicy.cs`
- `src/Wasla/Wasla.Application/Features/Diagnostics/DiagnosticAccess.cs`
- `src/Wasla/Wasla.Application/Features/Diagnostics/DiagnosticOrderHandler.cs`
- `src/Wasla/Wasla.Application/Features/Diagnostics/DiagnosticResultHandler.cs`
- `src/Wasla/Wasla.Application/Features/DoctorPatients/DoctorPatientRelationshipPolicy.cs`
- `src/Wasla/Wasla.Application/Features/Doctors/DoctorGovernance.cs`
- `src/Wasla/Wasla.Application/Features/Finance/FinancialMutations.cs`
- `src/Wasla/Wasla.Application/Features/Medications/MedicationAccess.cs`
- `src/Wasla/Wasla.Application/Features/Medications/PrescriptionHandlers.cs`
- `src/Wasla/Wasla.Application/Features/Onboarding/PracticeLocation.cs`
- `src/Wasla/Wasla.Application/Features/Practices/DoctorPracticeCatalog.cs`
- `src/Wasla/Wasla.Application/Features/Practices/DoctorPractices.cs`
- `src/Wasla/Wasla.Application/Features/Practices/ReceptionManagement.cs`
- `src/Wasla/Wasla.Application/Features/Registration/Registration.cs`
- `src/Wasla/Wasla.Application/Features/Reservations/ReservationManagement.cs`
- `src/Wasla/Wasla.Application/Features/Tickets/CallNext/CallNext.cs`
- `src/Wasla/Wasla.Application/Features/Tickets/CancelTicket/CancelTicket.cs`
- `src/Wasla/Wasla.Application/Features/Tickets/CompleteTicket/CompleteTicket.cs`
- `src/Wasla/Wasla.Application/Features/Tickets/ConfirmNoResponse/ConfirmNoResponse.cs`
- `src/Wasla/Wasla.Application/Features/Tickets/GetTicketDetails/GetTicketDetails.cs`
- `src/Wasla/Wasla.Application/Features/Tickets/ManualCall/ManualCall.cs`
- `src/Wasla/Wasla.Application/Features/Tickets/Recall/Recall.cs`
- `src/Wasla/Wasla.Application/Features/Tickets/RestoreNoShow/RestoreNoShow.cs`
- `src/Wasla/Wasla.Application/Features/Tickets/Shared/ReservationCheckInWorkflow.cs`
- `src/Wasla/Wasla.Application/Features/Tickets/Shared/TicketContracts.cs`
- `src/Wasla/Wasla.Application/Features/Tickets/StartVisit/StartVisit.cs`
- `src/Wasla/Wasla.Domain/Practices/DoctorPracticeConfiguration.cs`
- `src/Wasla/Wasla.Domain/Practices/ReceptionPracticeAccess.cs`
- `src/Wasla/Wasla.Domain/Security/SystemSecurity.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/DependencyInjection.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/DatabaseInitializationHostedService.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/FinanceReadService.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Migrations/WaslaDbContextModelSnapshot.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/PublicDiscoveryService.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/ReportingAccessGuard.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/ReportingQueryService.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/WaslaDbContext.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/WaslaSecuritySeeder.cs`
- `tests/Wasla.Tests.Integration/DoctorOnboardingApiTests.cs`
- `tests/Wasla.Tests.Integration/Phase12FinanceReadTests.cs`
- `tests/Wasla.Tests.Integration/Phase12TicketRefundProjectionTests.cs`
- `tests/Wasla.Tests.Integration/Phase12WalkInPaymentTests.cs`
- `tests/Wasla.Tests.Integration/Phase13ApiFixture.cs`
- `tests/Wasla.Tests.Integration/Phase18DoctorReportingApiTests.cs`
- `tests/Wasla.Tests.Integration/Phase18PartTwoMigrationTests.cs`
- `tests/Wasla.Tests.Integration/PublicDiscoveryApiTests.cs`
- `tests/Wasla.Tests.Integration/ReceptionWalkInOptionsTests.cs`
- `tests/Wasla.Tests.Integration/TrustAccessApiTests.cs`
- `tests/Wasla.Tests.Unit/DoctorPatientPolicyTests.cs`
- `tests/Wasla.Tests.Unit/Phase18ReportingFoundationTests.cs`
- `tests/Wasla.Tests.Unit/Phase5FinalIdentityDomainTests.cs`

## Added files

- `SOURCE-OF-TRUTH-SUBSCRIPTION-COMMERCIAL-ADDENDUM.md`
- `SUBSCRIPTION-COMMERCIAL-IMPLEMENTATION-REPORT.md`
- `docs/subscriptions/API-CONTRACT.md`
- `docs/subscriptions/CommercialSubscriptionFoundation.sql`
- `docs/subscriptions/test-evidence.json`
- `src/Wasla/Wasla.Api/Controllers/AdminSubscriptionsController.cs`
- `src/Wasla/Wasla.Api/Controllers/CommercialCatalogController.cs`
- `src/Wasla/Wasla.Api/Controllers/DoctorSubscriptionsController.cs`
- `src/Wasla/Wasla.Application/Features/Practices/BookingPresentation.cs`
- `src/Wasla/Wasla.Application/Features/Practices/ReceptionAccountState.cs`
- `src/Wasla/Wasla.Application/Features/Subscriptions/CommercialCatalog.cs`
- `src/Wasla/Wasla.Application/Features/Subscriptions/CommercialFeatures.cs`
- `src/Wasla/Wasla.Application/Features/Subscriptions/LegacyTransition.cs`
- `src/Wasla/Wasla.Application/Features/Subscriptions/SubscriptionCatalogOptions.cs`
- `src/Wasla/Wasla.Application/Features/Subscriptions/SubscriptionChangeTypeInputConverter.cs`
- `src/Wasla/Wasla.Application/Features/Subscriptions/SubscriptionContinuity.cs`
- `src/Wasla/Wasla.Application/Features/Subscriptions/SubscriptionEntitlements.cs`
- `src/Wasla/Wasla.Application/Features/Subscriptions/SubscriptionLifecycle.cs`
- `src/Wasla/Wasla.Application/Features/Subscriptions/SubscriptionManagement.cs`
- `src/Wasla/Wasla.Application/Features/Subscriptions/SubscriptionProcessing.cs`
- `src/Wasla/Wasla.Application/Persistence/ISubscriptionDataStore.cs`
- `src/Wasla/Wasla.Domain/Subscriptions/CommercialFeature.cs`
- `src/Wasla/Wasla.Domain/Subscriptions/CommercialPlan.cs`
- `src/Wasla/Wasla.Domain/Subscriptions/CommercialProduct.cs`
- `src/Wasla/Wasla.Domain/Subscriptions/DoctorSubscription.cs`
- `src/Wasla/Wasla.Domain/Subscriptions/FeaturePermissionMapping.cs`
- `src/Wasla/Wasla.Domain/Subscriptions/LegacyTransitionEnrollmentBatch.cs`
- `src/Wasla/Wasla.Domain/Subscriptions/PlanFeatureEntitlement.cs`
- `src/Wasla/Wasla.Domain/Subscriptions/SubscriptionChangeRequest.cs`
- `src/Wasla/Wasla.Domain/Subscriptions/SubscriptionEnums.cs`
- `src/Wasla/Wasla.Domain/Subscriptions/SubscriptionErrors.cs`
- `src/Wasla/Wasla.Domain/Subscriptions/SubscriptionFeatureSnapshot.cs`
- `src/Wasla/Wasla.Domain/Subscriptions/SubscriptionHistory.cs`
- `src/Wasla/Wasla.Domain/Subscriptions/SubscriptionPayment.cs`
- `src/Wasla/Wasla.Domain/Subscriptions/SubscriptionPermissionNames.cs`
- `src/Wasla/Wasla.Domain/Subscriptions/SubscriptionReminderSchedule.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Configurations/SubscriptionConfigurations.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Migrations/20261102190031_CommercialSubscriptionFoundation.Designer.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Migrations/20261102190031_CommercialSubscriptionFoundation.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Seeding/SubscriptionCatalogSeeder.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/SubscriptionDataStore.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/SubscriptionPersistenceErrorMapper.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/WaslaDbContext.Subscriptions.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Subscriptions/SubscriptionLifecycleBackgroundService.cs`
- `tests/Wasla.Tests.Integration/SubscriptionApiTests.cs`
- `tests/Wasla.Tests.Integration/SubscriptionCapacityConcurrencyTests.cs`
- `tests/Wasla.Tests.Integration/SubscriptionEntitlementApiTests.cs`
- `tests/Wasla.Tests.Integration/SubscriptionLegacyAndRenewalTests.cs`
- `tests/Wasla.Tests.Integration/SubscriptionMigrationTests.cs`
- `tests/Wasla.Tests.Integration/SubscriptionRegistrationApiTests.cs`
- `tests/Wasla.Tests.Integration/SubscriptionTestData.cs`
- `tests/Wasla.Tests.Unit/SubscriptionCatalogOptionsTests.cs`
