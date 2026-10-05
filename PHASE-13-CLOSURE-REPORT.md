# Phase 13 Closure Report — 2026-10-05

Branch: `codex/phase-13-clinical-encounters`, created from `main`. Approved baseline: Master Source of Truth v1.2, DEC-056–069.

## Result

Clinical Encounter, Diagnosis and Follow-Up Eligibility are implemented. Final evidence below closes the task's Definition of Done. Production deployment is a separate operation; this branch has not been pushed or merged.

## Implementation and boundaries

MedicalEncounter is a separate aggregate from Ticket. Its Diagnosis, EncounterAmendment, EncounterAmendmentChange and EncounterAuditEvent children implement draft editing, current corrected completed values, immutable amendment snapshots/sequence and metadata read/mutation audit. FollowUpEligibility is a separate aggregate with append-only FollowUpEligibilityHistory.

Start Visit creates one encounter atomically with Ticket InProgress and existing idempotency/history/no-show processing. Complete Visit requires independent Ticket and Encounter rowversions, clinical notes and valid diagnoses; Ticket, Encounter and optional follow-up consumption commit together. Completed clinical changes use amendment only. Draft diagnosis deletion is physical; completed deletion is rejected both by domain/API and the persistence guard; amendment removal is logical voiding.

Zero diagnoses is allowed. A nonempty active set has exactly one Primary and case-insensitively unique trimmed text. Notes/display limits are enforced. Multi-change amendments stage their final state before application and preserve server-generated before/after values.

Follow-up integrates with patient and assigned Reception reservations, existing schedule/exception/occupancy/conflict/quota/price rules, paid Check-In/Force Check-In, and paid Walk-In. Explicit completed-source creation permits at most one eligibility per encounter. Claims remain Reserved until successful completion. Cancellation, NoShow and expiration release matching claims in the same SaveChanges transaction; restore reacquires and reschedule/Check-In revalidate current business dates and claims. A historical cancelled workflow cannot release a new claimant.

Doctor lists/detail/by-Ticket discovery, own Completed patient lists/details, own/bookable-dependent eligibility lists/details, follow-up date/slot/options APIs and minimal delegated Reception lookup support discovery without database intervention. Latest rowversions, linkage and actor/status capabilities are returned. See `PHASE-13-API-CHANGES.md` for every route, request and response change and the appended permission grants.

Reception receives no notes, diagnoses or amendment history. Patients receive current own Completed output only; family booking access remains separate from clinical access. Doctor access requires current active/approved owner scope and granular permissions. Administrator role gives no implicit clinical access.

The SQL Server tests exposed an existing queue ordering bug: EF child collection order is not guaranteed. Current call attempt now uses the current cycle and largest attempt number, with a regression test that reverses materialized child order. This is required for reliable follow-up NoShow/restore behavior.

## Concurrency, constraints and migration

Migration: `20261004152344_Phase13ClinicalEncountersFollowUp` (including Designer and model snapshot). Reviewed generated upgrade SQL is additive: new clinical tables and nullable Reservation/Ticket linkage, with no existing-table/column drop and no invented clinical backfill. All foreign keys use SQL Server NO ACTION. Draft diagnosis client-side orphan deletion does not create database cascade deletion.

Important constraints:

- Unique encounter per Ticket and eligibility per source encounter.
- Unique amendment sequence per encounter.
- Filtered unique active Primary and active normalized diagnosis text.
- Filtered unique claimed Reservation, claimed Ticket and consumed Encounter.
- Encounter status/completed timestamp/nonblank completed notes checks.
- Eligibility status/claim consistency check: exactly one current claim while Reserved, neither while Available/Expired/Consumed, and consumed encounter only when Consumed.
- Indexed Doctor/Practice/status/date and patient/status/date discovery.
- Native SQL Server rowversions for Encounter and Eligibility.

Existing transaction-owned queue/reservation/idempotency locks remain in place. The shared eligibility rowversion arbitrates workflows with different lock keys (including Reservation versus Walk-In), so a losing claimant cannot commit operational rows or payment/history/idempotency changes. Encounter Revision guarantees token advancement for child-only edits even at an unchanged clock. Expected concurrency/unique-index failures map to stable localized errors; failure is not suppressed. SQLite has explicit test rowversion fallback, while production concurrency tests use native SQL Server tokens.

Following DEC-063, Check-In transfers a mutually exclusive Reservation claim to a Ticket claim atomically. The illustrative task example allowing both current references does not override the approved master decision. The preceding Reserved event retains the reservation reference and the transfer event retains the ticket reference.

## Final flow trace

| Flow | Public API trace and verification |
| --- | --- |
| A | Patient NewConsultation Reservation → paid Reception Check-In → Ticket → queue call/Start → Encounter → primary Diagnosis + Notes → dual-token Complete. Authenticated API tests run on SQLite and SQL Server. |
| B | Completed source → Doctor eligibility → patient list/detail → bounded dates/slots/backend-priced options → FollowUp Reservation/Reserved → paid Check-In/transfer → Start/Encounter → Complete/Consumed. Both providers verify no consumption at booking or Check-In. |
| C | FollowUp Reservation → patient Cancel → Available → backend options → Reception Walk-In/rebooking → Reserved → completion. Both providers verify release and next claim. |
| D | FollowUp Reservation → clock passes grace period → three other visits started through public APIs → runtime NoShow → Available → Reception restore → Reserved. Both providers verify this flow; no direct database status manipulation. |
| E | Reception eligibility lookup → FollowUp Walk-In options → paid Walk-In → Ticket → Start/Encounter → Complete/Consumed. Both providers verify. |
| F | Doctor Completed encounter list → open returned encounter ID → amendment → corrected own patient output → doctor immutable original snapshots. Both providers verify; SQL Server additionally tests Primary replacement and completed diagnosis preservation. |

Test fixture setup creates identities, catalogs, schedules and verified family test data. Lifecycle transitions for A–F use authenticated public API routes and IDs returned by those routes. No production business flow requires hidden endpoints, manual state changes, frontend-generated prices or duplicated frontend eligibility rules.

## Security, audit, performance and PHI review

Security tests verify cross-Doctor/practice denial, Reception denial for reads/writes/amendments, administrator denial, unassigned/missing delegated Reception permission denial, other-patient and draft hiding, corrected patient output without notes/reasons/snapshots, and family clinical denial while dependent booking succeeds.

Mutation/read audit events contain action, target, actor and UTC time. Amendments and eligibility history retain append-only reconstruction data. Generic logging behavior logs request name, duration, error code/type and trace IDs, without request bodies. EF sensitive-data logging is not enabled. Product redaction adds ClinicalNotes, DisplayText, Notes, amendment snapshots/reasons/changes and Search; tests verify the configured policy and English/Arabic expiry errors.

Lists execute database filtering/count/pagination. Diagnosis/eligibility summary booleans use EXISTS. Detail queries use bounded independent queries; lists avoid collection Includes and per-row calls. Eligibility expiration filtering occurs before pagination using each Practice timezone. Follow-up availability reuses the existing deterministic schedule calculator and batches occupancy, conflicts and catalogs over the existing booking horizon.

## Verification

| Verification | Result |
| --- | --- |
| `dotnet build Wasla.sln --no-restore -v minimal` | Passed; 0 warnings, 0 errors |
| All unit tests | 102 passed, 0 skipped; includes 17 Phase 13 domain/queue regression cases |
| All architecture tests | 12 passed, 0 skipped; includes 3 Phase 13 architecture checks |
| All integration tests with isolated SQL Server connection | 95 passed, 0 skipped; includes 20 Phase 13 cases |
| Final Phase 13 integration rerun after completed-deletion guard | 20 passed, 0 skipped (12 SQL Server / 8 SQLite cases) |
| `dotnet ef migrations has-pending-model-changes ... --no-build` | No pending model changes |
| Phase 12 → Phase 13 SQL migration script | Generated and reviewed; migration applied in isolated SQL Server API-test databases |
| `git diff --check` | Passed |

The broad run initially found a setup mistake in the new queue regression test (it began with an already-started ticket). The fixture was corrected to start Waiting; the entire 102-test unit suite then passed. Existing integration (95) and architecture (12) suites passed. A later focused run validates the completed-diagnosis persistence deletion guard. TRX evidence is saved under `%TEMP%/WaslaPhase13Verification`; this directory is outside the repository.

SQL Server coverage includes competing start, competing note edits, concurrent eligibility creation, competing Reservations, Reservation versus Walk-In, optional stale eligibility tokens, stale Encounter completion, Start/Complete failure injection, consumption failure injection, matching idempotency replay, native rowversions, Primary replacement and live migration application. Test databases are isolated names owned by the fixture; production databases were not changed.

## Definition of Done

Phase 13 satisfies the Definition of Done. All task checklist items are implemented and verified: clinical aggregates/atomic start/unique Ticket linkage; notes and diagnosis invariants; atomic dual-token completion; amendments and completed-record protection; the full eligibility lifecycle across patient/reception reservations and Walk-In; date/restore/claim/consumption safety; frontend discovery/detail/linkage/capability/token contracts; privacy and appended permission seeding; migration review and live test application; domain, API, security, native concurrency and architecture tests; database paging and PHI review; API/closure/status documentation and Master Source of Truth implementation status update.

No unresolved Phase 13 implementation blocker remains. Deployment must finish existing legacy InProgress Tickets before switching the completion contract, because the migration deliberately does not fabricate historical clinical notes/encounters. Prescription, DrugCatalog, medicine imports, pharmacy, family clinical sharing, cross-Doctor sharing and other deferred decisions remain outside this task.

The external Master Source of Truth v1.2 roadmap now records Phase 13 as [DONE], with an implementation verification appendix and a pre-update backup. The repository addendum records the same status.

## Files changed

- `PHASE-13-API-CHANGES.md`
- `PHASE-13-CLOSURE-REPORT.md`
- `README.md`
- `SOURCE-OF-TRUTH-PHASE-13-ADDENDUM.md`
- `src/Wasla/Wasla.Api/Controllers/ClinicalControllers.cs`
- `src/Wasla/Wasla.Api/Controllers/ReservationsController.cs`
- `src/Wasla/Wasla.Api/Controllers/TicketsController.cs`
- `src/Wasla/Wasla.Api/Program.cs`
- `src/Wasla/Wasla.Application/DependencyInjection.cs`
- `src/Wasla/Wasla.Application/Features/Clinical/ClinicalAccessService.cs`
- `src/Wasla/Wasla.Application/Features/Clinical/ClinicalContracts.cs`
- `src/Wasla/Wasla.Application/Features/Clinical/ClinicalMutations.cs`
- `src/Wasla/Wasla.Application/Features/Clinical/ClinicalQueries.cs`
- `src/Wasla/Wasla.Application/Features/Clinical/FollowUpWorkflow.cs`
- `src/Wasla/Wasla.Application/Features/Practices/ReceptionManagement.cs`
- `src/Wasla/Wasla.Application/Features/Reservations/FollowUpAvailability.cs`
- `src/Wasla/Wasla.Application/Features/Reservations/ReservationManagement.cs`
- `src/Wasla/Wasla.Application/Features/Tickets/CompleteTicket/CompleteTicket.cs`
- `src/Wasla/Wasla.Application/Features/Tickets/CreateWalkIn/CreateWalkIn.cs`
- `src/Wasla/Wasla.Application/Features/Tickets/GetWalkInOptions/GetWalkInOptions.cs`
- `src/Wasla/Wasla.Application/Features/Tickets/RestoreNoShow/RestoreNoShow.cs`
- `src/Wasla/Wasla.Application/Features/Tickets/Shared/ReservationCheckInWorkflow.cs`
- `src/Wasla/Wasla.Application/Features/Tickets/Shared/TicketContracts.cs`
- `src/Wasla/Wasla.Application/Features/Tickets/StartVisit/StartVisit.cs`
- `src/Wasla/Wasla.Domain/Clinical/ClinicalErrors.cs`
- `src/Wasla/Wasla.Domain/Clinical/FollowUpEligibility.cs`
- `src/Wasla/Wasla.Domain/Clinical/MedicalEncounter.cs`
- `src/Wasla/Wasla.Domain/Reservations/Reservation.cs`
- `src/Wasla/Wasla.Domain/Resources/ErrorMessage.ar.resx`
- `src/Wasla/Wasla.Domain/Resources/ErrorMessage.resx`
- `src/Wasla/Wasla.Domain/Security/SystemSecurity.cs`
- `src/Wasla/Wasla.Domain/Tickets/Ticket.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/DependencyInjection.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/ClinicalPersistenceErrorMapper.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/ClinicalReadService.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Configurations/ClinicalConfigurations.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Configurations/ReservationConfigurations.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Configurations/TicketConfigurations.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Migrations/20261004152344_Phase13ClinicalEncountersFollowUp.Designer.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Migrations/20261004152344_Phase13ClinicalEncountersFollowUp.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Migrations/WaslaDbContextModelSnapshot.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/TicketQueueReader.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/WalkInOptionsReader.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/WaslaDbContext.cs`
- `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/WaslaSecuritySeeder.cs`
- `tests/Wasla.Tests.Architecture/Phase13ClinicalArchitectureTests.cs`
- `tests/Wasla.Tests.Integration/Phase13ApiFixture.cs`
- `tests/Wasla.Tests.Integration/Phase13ClinicalApiTests.cs`
- `tests/Wasla.Tests.Integration/Phase13LifecycleSecurityTests.cs`
- `tests/Wasla.Tests.Integration/Phase13SqlServerTests.cs`
- `tests/Wasla.Tests.Unit/Phase13ClinicalDomainTests.cs`
