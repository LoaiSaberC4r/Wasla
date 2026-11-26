# Migration and delivery report

The migration replaces the former bilingual factory, default interface HTML, inline Patient Identity and Reservation messages, governance cancellation HTML, and the guarded notification email renderer. Authentication, patient identity and claims, doctor governance and specialization, family requests, reception, both reservation ownership paths, guarded clinical notification, and commercial subscriptions all render through the unified factory.

The guarded notification mapper keeps the event template code and version and passes `NotificationDeliveryId` through the existing queue contract. In-app rendering retains its existing title, message, language fallback, and version check. The Outbox schema, persistence, retries, idempotency formats, SMTP delivery, and send-time guards retain their existing behavior. Historical messages are transported from their persisted content. Queued notification intents can use the new design when they produce a new Outbox message.

A future paid renewal queues its existing projection at scheduling time. When the worker starts that period, it queues EMAIL-25 and the original `period-started` projection in the existing transaction using separate paths. The email retains the original `subscription-activated:{SubscriptionPeriodId}` key and the Outbox's deduplication. A scheduled period that expires before activation follows the existing expiration path.

## Final corrections for PR #42

| Finding | Resolution |
|---|---|
| EMAIL-FIX-01 | Queue the paid activation email when the scheduled period becomes active |
| EMAIL-FIX-02 | Publish 31 licensed outline PNGs and refine the shared feature rows and registration stepper |
| EMAIL-FIX-03 | Set the single action position per template in the JSON catalog |
| EMAIL-FIX-04 | Localize customer-facing dates and times while preserving appointment-local time and exact subscription boundaries |
| EMAIL-FIX-05 | Render the activation link or token once, before features and the footer, with matching instructions and expiration |
| EMAIL-FIX-06 | Resolve matching commercial plan names and use the saved code when a name is unavailable |

The correction changes subscription processing, notification orchestration, plan lookup, shared email models and rendering, configuration, the preview tool, focused assertions, and this documentation. It adds `EmailDateFormatter.cs` and the licensed icons under `wwwroot/email-assets/icons/`.

| Area | Correction files |
|---|---|
| Application | `SubscriptionProcessing.cs`, `SubscriptionLifecycle.cs`, `CommercialCatalog.cs`, `ISubscriptionDataStore.cs`, `EmailTemplateModels.cs`, `EmailNotificationFactory.cs` |
| Infrastructure | `SubscriptionDataStore.cs`, `EmailComponents.cs`, `EmailDateFormatter.cs`, `EmailTemplateRegistry.cs`, `EmailTemplates.v1.json`, `WaslaEmailTemplateRenderer.cs`, `EmailBrandingOptions.cs`, `EmailUrlValidator.cs` |
| API | `appsettings.json`, `appsettings.Production.json`, and `wwwroot/email-assets/icons/` |
| Previews and tests | `tools/Wasla.EmailPreview/Program.cs`, `SampleEmails.cs`, `Wasla.EmailPreview.csproj`, `UnifiedEmailRenderingTests.cs`, `SubscriptionApiTests.cs`, `Phase5FinalRegistrationApiTests.cs` |
| Documentation | The five documents under `docs/email-system/` |

### Correction verification

- `dotnet build Wasla.sln` passed with zero warnings and zero errors.
- All 32 focused rendering cases passed, including the 27-template catalog in three languages.
- The scheduled renewal, immediate paid activation, and patient one-time activation cases passed on SQLite. The three SQL Server variants skipped because `WASLA_SQLSERVER_CONNECTION_STRING` was unavailable.
- The API publish output contained 31 PNGs and the Lucide license. The preview generator produced 81 catalog HTML and 81 text files, plus three HTML and three text fallback previews and the index.
- The six requested templates and token fallback were reviewed locally in Chromium at desktop and 390 px widths. The checks used local fixtures and browser rendering without live SMTP or native email clients.

## File inventory

| Change | Files |
|---|---|
| Added Application contracts and mapping | `EmailNotificationFactory.cs`, `IEmailNotificationFactory.cs`, `EmailTemplateKeys.cs`, `EmailTemplateModels.cs` |
| Added Infrastructure rendering | `EmailBrandingOptions.cs`, `EmailComponents.cs`, `EmailRenderingServiceCollectionExtensions.cs`, `EmailTemplateRegistry.cs`, `EmailTemplates.v1.json`, `EmailUrlValidator.cs`, `WaslaEmailTemplateRenderer.cs` |
| Added preview and focused verification | `tools/Wasla.EmailPreview/Wasla.EmailPreview.csproj`, `Program.cs`, `SampleEmails.cs`, `PreviewIndex.html`, `UnifiedEmailRenderingTests.cs` |
| Added documentation | The five Markdown documents in `docs/email-system` |
| Updated workflow files | `PasswordRecovery.cs`, `DoctorGovernance.cs`, `FamilyRelationshipWorkflow.cs`, `NotificationTemplates.cs`, `DoctorSpecializationWorkflow.cs`, `PatientClaimWorkflow.cs`, `PatientIdentityWorkflow.cs`, `ReceptionManagement.cs`, `Registration.cs`, `ReservationManagement.cs`, `SubscriptionLifecycle.cs`, `NotificationDispatchProcessor.cs` |
| Updated supporting files | Application and Infrastructure `DependencyInjection.cs`, `EmailBrandingProvider.cs`, `Wasla.Infrastructure.csproj`, API `appsettings.json`, `.gitignore`, `MASTER-SOURCE-OF-TRUTH.md`, `docs/phase17/EVENT-MATRIX.md` |
| Updated focused test files | `TrustAccessApiTests.cs`, `SubscriptionApiTests.cs`, `Phase17NotificationTests.cs` (unit), `TrustAccessDomainTests.cs`, `Wasla.Tests.Unit.csproj` |
| Removed legacy renderer and contracts | `BilingualEmailNotificationFactory.cs`, `EmailNotificationContracts.cs` |

## Initial migration verification

- `dotnet build Wasla.sln` passed with zero warnings and zero errors.
- 32 focused unit cases passed. The template theory rendered all 27 templates in all three language modes and checked both representations.
- 18 focused integration cases passed across Outbox persistence and retry, governance and clinical notification delivery, password recovery, patient activation, and subscription renewal. Two SQL Server variants skipped because `WASLA_SQLSERVER_CONNECTION_STRING` was unavailable. The corresponding SQLite variants passed.
- The preview CLI generated 81 HTML and 81 text files plus the index under `email-previews/`. The index and representative OTP, doctor approval, reservation confirmation, medical update, and subscription reminder layouts were reviewed in Chromium at desktop and 390 px widths.
- Integration checks used local fixtures and synthetic email transport. Live SMTP and native email clients were outside this verification.

## Deferred findings

- Existing Outbox content can contain OTPs or activation tokens. Sensitive payload storage and send-time expiry policy are separate work.
- Outbox processing leases and attempt recovery remain separate reliability work.
- Existing subscription expiry guard coverage and stale trial reminders remain separate lifecycle work.
- Potentially exposed configuration credentials require independent assessment and rotation through the deployment process. This change introduces no credentials.
- The repository's Master Source of Truth is an index and does not contain the original DEC-006 or Section 57 text. The migration retains the transactional behavior in the repository and the supplied brief.
