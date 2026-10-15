# Phase 18 Part Two dependency inventory

Reviewed 2026-10-14 on `codex/phase18-part-two`, baseline `84e50dba497400ca11fa0a6304482a5e48b53ba8` (exact task baseline), clean working tree. All projects inherit net10.0 from Directory.Build.props; SDK 10.0.401. No AGENTS.md was found. The entire owner attachment, including Annexes A, B and C, was read.

## Sources and change control

Phase10 DEC-028–037, Phase11 DEC-038–048, Phase12 DEC-049–055, Phase13 DEC-056–069, Phase14, Phase15 and Part One repository addenda were examined. Phase16 files explicitly marked PROPOSED remain proposals; current medical/family implementations and their tests are retained. The original master v1.2 text, BuildingBlock-All-Code.txt and Qcontrol-Without-BuildingBlock.txt were not found in the repository or attachment directory. Consequently original §§35/73/88/91 and DEC-005/006/007/008/009/011/012 cannot be independently rechecked; Annex A supplies the owner-approved Part Two rules. BuildingBlock source is available in src/BuildingBlock.

DEC-038 prohibits hard/soft deletion of Tickets, payments, histories and attempts. Annex A approves only atomic relocation of complete operational records, preserving logical identity and every history/attempt ID. The dated Part Two addendum records this exception without modifying old decisions. Purge remains prohibited. DEC-051 allows cancellation of unserved NoShow; Part Two narrows this after verified day closure, so closed-day NoShow cannot be cancelled to obtain refunds. Open-day rules remain intact.

## FK graph and cutover

All entries below use Restrict/NO ACTION; no cascade is introduced. Existing child-column indexes are retained. Configurations are under src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Configurations.

| Child / columns | Existing target | Intended target / phase |
|---|---|---|
| Tickets.ReservationId | Reservations.Id | ReservationRegistry.Id, S02 cutover |
| TicketIdempotencyRecords.TicketId | Tickets.Id | TicketRegistry.Id, S02 |
| ReservationIdempotencyRecords.ReservationId | Reservations.Id | ReservationRegistry.Id, S02 |
| MedicalEncounters.TicketId | Tickets.Id | TicketRegistry.Id, S02; clinical rows stay in dbo |
| Payments.TicketId, ReservationId | Tickets.Id, Reservations.Id | Both registries, S02; financial identity retained |
| Refunds.TicketId | Tickets.Id | TicketRegistry.Id, S02; PaymentId FK retained |
| FollowUpEligibilities.ReservedTicketId, ReservedReservationId | Tickets.Id, Reservations.Id | Both registries, S02; exclusive current claim unchanged |
| HiddenEncounterAccessRequests.RequestingTicketId | Tickets.Id | TicketRegistry.Id, S02; encounter FKs unchanged |
| ReceptionHandovers.(DoctorPracticeId,TicketId), (DoctorPracticeId,ReservationId) | Practice-scoped alternate keys of Tickets/Reservations | Identical composite registry keys, S02 |
| TicketHistories.TicketId | Tickets.Id | Keep active parent; move whole child set into archive with matching restrictive archive parent FK |
| TicketCallAttempts.TicketId | Tickets.Id | Keep active parent; move complete attempts into archive |
| ReservationHistories.ReservationId | Reservations.Id | Keep active parent; atomic archive movement |
| Tickets/Reservations.DoctorId, DoctorPracticeId, PatientId, SegmentId, VisitTypeId, actor IDs, FollowUpEligibilityId | Respective independent entities | Keep and duplicate enforced references in archive |
| TicketDailyCounters, FinancialDailyCounters | DoctorPractice | Keep forever, never reset/delete |
| financial corrections, clinical diagnoses/amendments/outputs, visibility, access audits, handover histories | Their financial/clinical/governance roots | Keep; these do not directly reference movable physical rows |

TicketRegistry preserves Id, Doctor/Practice/Patient, BusinessDate, TicketNumber, ReservationId, location and archival instant. Lifetime unique Practice/date/number and nullable unique ReservationId replace reliance on dbo-only indexes. ReservationRegistry preserves identity and globally unique reference. Composite Practice/Id keys preserve handover enforcement. Mutable reservation appointment dates are not registry identity.

## Consumer classification

| Actual consumer | Classification / risk |
|---|---|
| TicketQueueReader queue/order/open predicates, TicketRuntimePersistence, booking capacity/conflicts, admission/state commands | Active only; do not union into queue/mutations. Check duplicate Reservation against lifetime registry. |
| TicketQueueReader.GetDetailsAsync; GetTicketDetails/GetMyTicketDetails | Unified read; location-dependent permission, current owner/delegation/self; timeline and financial/clinical actions independently checked |
| WaslaDataStore.GetReservationViewAsync / ReservationApplicationService details | Unified read; history from original location; dependent entitlement rechecked |
| WaslaDataStore.ListReservationViewsAsync | Historical union when requested history; upcoming/live retain active semantics |
| FinanceReadService and Revenue, RefundPayment handler | Historical Ticket snapshots/history, independent finance privileges; no tracking/writing archived Ticket |
| ClinicalReadService, MedicalRecordReadService | Historical operational linkage, independent clinical privacy; current treatment remains active only |
| ReportingQueryService.Operations/Cohorts/CapacityDrilldowns/PatientDrilldowns/Platform/PlatformCohorts | Full historical union and immutable event histories; AsOfUtc and event knowledge unchanged; export shares queries |
| HandoverService links | Registry identities and same-Practice enforcement; historical links stay navigable |
| ReservationPublicReaders | Capacity active only; popularity historical converted facts |
| PlatformOperationsService.DataQuality / Jobs | Existing current operational missing-history checks and existing obligation/outbox/reporting job health retain their meaning; archive location/schema/integrity health is separately authorized through Part Two operations. No private archive controls leak through existing JobsView. |
| ReceptionOperationsService worklist / HiddenAccessReader live treatment | Active operational admission/treatment predicates stay active-only; closed requesting Tickets cannot sustain current cross-Doctor access. Historical request/target readers resolve preserved registry/unified links. |
| auth/me, DoctorPracticesController, DoctorReceptionsController, patient/family selectors | Reuse; archive/audit selectors cannot require unrelated report/practice-management grant |

## Transaction and rollout invariants

Expand first with feature disabled. Populate registries in bounded repeatable transactions; new creation registers within SaveChanges' transaction. Cutover migration refuses missing registry identities and replaces constraints transactionally. No movement until schema, registry coverage, FK targets and read cutover pass. Exactly one physical row and truthful location must reconcile before and after every move. Copy full original columns including stored rowversion bytes, history/attempt IDs, then delete children/parents, update pointers and append safe audit in one transaction. Financial/clinical rows are never moved.

Lock order: existing Ticket patient then Practice/day, existing Reservation patient then Practice/day for linked/standalone Reservations, followed by registry/control guards. Movement locks are transaction-owned; the exclusive worker lease is session-owned and checked inside each movement transaction. No detached in-memory multi-instance lock is used. Pause blocks new movement and permits an in-flight transaction to finish. Required sensitive-read audit failure blocks response. Diagnostic logs contain safe exception type/code only. Closed snapshots are immutable; today's schedule cannot prove historical closure. Legacy data require past business date under original timezone plus coherent terminal/history/financial/clinical facts; ambiguous cases become deduplicated metadata exceptions.

Migration reversal after relocation is unsafe: disable worker immediately, keep compatible reads, restore/PITR only through a separately rehearsed approved recovery. No destructive Down or production operations. Backup/restore and realistic volume/lock/query-plan acceptance remain deployment gates.

Rejected alternatives: dbo soft-delete (fails physical archiving), a FK to either active/archive (SQL cannot enforce), dropping persistent FKs (breaks clinical/financial integrity), copying PHI into registry/audit (privacy), midnight or today's schedule for old records (false history), whole-day transaction (InProgress blocks siblings), purge/30-day retention (unapproved).

OPEN DECISION: legal retention durations and disposal policy; excluded. Cross-midnight support and cross-Doctor sharing remain excluded. Production readiness requires independently validated backup/security/legal gates.
