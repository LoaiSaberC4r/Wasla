# Wasla Source-of-Truth Decision Addendum — Phase 11 — 2026-09-17

This repository-tracked addendum records approved Ticket, queue-runtime, check-in, and minimal paid-admission decisions. It supplements Phase 10. DEC-036 remains true: payment does not gate Reservation booking; full payment gates Ticket creation and queue admission.

## DEC-038 — Ticket identity, source, and lifecycle

Ticket is a dedicated operational aggregate, distinct from Reservation and MedicalEncounter. Every Ticket belongs to one approved Doctor, active DoctorPractice, and existing Patient. Its source is Reservation or WalkIn and its initial status is Waiting. The only statuses are Waiting, Called, InProgress, NoShow, Completed, and Cancelled. The mandatory consultation path is Waiting → Called → InProgress → Completed. Completed and Cancelled are terminal. Tickets, call attempts, payments, and history are never soft- or hard-deleted.

## DEC-039 — Reception-only admission and paid gate

Only an active, assigned, globally and assignment-authorized Reception user can create a Ticket. Reservation check-in and WalkIn atomically record a Paid payment whose amount equals the Ticket PriceSnapshot. Unpaid and partial admission are forbidden and have no override. Reservation pricing remains locked; WalkIn snapshots the current active NewConsultation price. Cancellation and operational-day closure do not refund or mutate the Paid record.

## DEC-040 — Reservation check-in and force check-in

Normal check-in requires an Active same-Practice Reservation on the current Practice-local BusinessDate, no prior Ticket for that Reservation, no open Patient/Practice Ticket, full payment, and entry into the window beginning ScheduledStart minus CheckInOpenBeforeMinutes, default 30. Late does not block check-in. Force Check-In is a separate reason-required use case that bypasses only the early-window limit. It does not bypass payment, date, state, ownership, authorization, or duplicate protection. Reservation NoShow must first use the existing same-day restore flow. Reservation conversion, Ticket, Payment, both histories, idempotency, and projection invalidation commit or roll back together.

## DEC-041 — WalkIn admission

WalkIn uses an existing Patient and requires AllowWalkIn, active Practice and approved Doctor, active in-Practice Segment, active in-Practice NewConsultation Visit Type, a matching positive price, full payment, and no open Patient/Practice Ticket. A same-day Reservation NoShow cannot be bypassed with WalkIn. WalkIn does not consume or obey Reservation capacity. FollowUp WalkIn is not supported in Phase 11.

## DEC-042 — Ticket snapshots, numbering, and persistence invariants

Ticket snapshots Doctor, Practice, Patient, optional Reservation, BusinessDate, daily number, source, Segment identity/names/priority, Visit Type identity/code/names, Price, Practice timezone, check-in time, and queue-order time. These commercial and ownership snapshots are immutable. Human TicketNumber is allocated atomically per DoctorPractice and BusinessDate, begins at one each day, and is never reused. Database invariants enforce one Ticket per Reservation forever, one open Ticket per Patient/Practice, unique Practice/date/number, and at most one Called-or-InProgress Ticket per Practice across operational days.

## DEC-043 — Queue order and selection

Call Next selects server-side from Waiting Tickets: restored FastTrack first; then Segment priority descending; QueueOrderTime ascending; Patient Arabic name ascending; TicketNumber ascending. Multiple FastTrack Tickets use oldest grant/restore then TicketNumber. New Tickets set QueueOrderTime to check-in time. Normal NoShow restore resets it to restore time. FastTrack is explicit runtime state, never a Segment-priority mutation. Manual Call requires a reason and audit history and does not reorder remaining Tickets. Practice/day transaction locks protect selection and Called/InProgress invariants.

## DEC-044 — Calls, NoShow, restore, and FastTrack

Call creates attempt one. Recall creates the next attempt in the same call cycle only after explicit No Response confirmation. A Ticket remains Called while retries remain; the final configured confirmed No Response automatically transitions it to NoShow. Starting a visit records response on the pending attempt. Same-day Doctor or authorized Reception restore reuses the same Ticket, number, snapshots, and payment and begins a new call cycle. If the number of same-Practice/day Tickets actually entering InProgress after NoShow is at or below TicketNoShowReturnFastTrackLimit, default three, one-time FastTrack is granted and consumed when Called; otherwise restore returns to normal Waiting order.

## DEC-045 — Start, completion, cancellation, and operational-day closure

Only the owning Doctor can transition Called → InProgress and InProgress → Completed. Completion never calls the next Patient automatically and does not create MedicalEncounter. The owning Doctor or authorized assigned Reception may cancel Waiting or Called with a reason; InProgress cannot be cancelled. After effective Practice operational-day end, targeted idempotent system processing cancels Waiting and Called with OperationalDayEnded history while allowing InProgress to complete normally.

## DEC-046 — Passed-patient Reservation NoShow

Each actual Ticket transition into InProgress evaluates Late Active Reservations for the same Practice and BusinessDate. A Reservation becomes NoShow only when the configured number of subsequent actual InProgress transitions has been reached. Waiting, Called, elapsed time alone, and cancelled Tickets do not qualify. Evaluation is transactionally coordinated, idempotent, appends immutable Reservation history, and durably queues existing projection invalidation.

## DEC-047 — Actors, permissions, privacy, and idempotency

Ticket permissions are granular. Reception requires both a global permission and an active assignment-delegated permission for view, check-in, force check-in, WalkIn, payment recording, call/recall/no-response, manual call, restore, and cancel. Owning Doctor permissions cover view, call, no-response, manual call, restore, cancel, start, and complete. Patient may view only their own Ticket. Patient queue responses expose Ticket number, status, Practice/Doctor basics, live PatientsAheadNow, and own timestamps but no other Patient identity. Mutations use RowVersion and Actor + Operation + Idempotency-Key semantics; same key and payload returns the same logical result and a changed payload conflicts.

## DEC-048 — Phase 11 architecture boundary

Phase 11 Ticket application code uses BuildingBlock repositories, IUnitOfWork, Specifications, and ITransactionalCommand&lt;WaslaWritePersistence&gt;. IWaslaDataStore is frozen and receives no Ticket, queue, or payment methods. Focused persistence ports exist only for atomic daily numbering, transaction-owned queue locking, efficient queue projections/PatientsAheadNow, and Reservation NoShow runtime counting. No broad Ticket data store, God interface, generic workflow framework, full Payments module, or clinical module is introduced.
