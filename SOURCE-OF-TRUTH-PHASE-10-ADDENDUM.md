# Wasla Source-of-Truth Decision Addendum — Phase 10 — 2026-09-16

This repository-tracked addendum records approved Reservation decisions that supersede older open or historical wording. It does not delete prior decisions. DEC-022 remains in force: `DoctorPracticeSchedulePeriod.SlotDurationMinutes` is the sole V1 appointment-duration source; Visit Type has no duration.

## DEC-028 — Reservation actors and immediate confirmation

Patient self-service, an authorized Parent/Legal Guardian acting for a dependent, and assigned Reception may create a Reservation. Family membership alone grants no authority. Parent-to-minor-child and Legal-Guardian-to-dependent authority is revalidated for later view/cancel/reschedule actions. Doctor has no direct-create flow. A valid booking becomes `Active` immediately.

## DEC-029 — Reservation lifecycle and immutable history

The final statuses are `Active`, `Cancelled`, `NoShow`, `Expired`, and `ConvertedToTicket`. Late is derived from Active plus the configured grace time. Aggregate methods own all transitions and append immutable history. Reservations and history have no delete capability. Reservation, Ticket, and MedicalEncounter remain distinct.

## DEC-030 — Capacity, overlap, and booking horizon

Active and ConvertedToTicket consume slot, Practice-day, and Segment capacity. Cancelled, NoShow, and Expired release it. No intentional overbooking or override is allowed. Segment quota is protected minimum capacity until its release cutoff, after which unused protection becomes general capacity.

Booking is same-day capable when the exact generated slot is in the future in the Practice timezone. The platform horizon is local today plus 29 days. A Patient may have only one Active Reservation per Practice/day, no overlapping Active interval platform-wide, and at most one future Active NewConsultation with the same Doctor. A same-day NoShow must be restored rather than bypassed with a new booking.

## DEC-031 — Commercial and appointment snapshots

Reservation snapshots lock Segment names/priority, Visit Type code/names, price, Practice timezone, local/UTC appointment meaning, and slot duration. Catalog changes do not rewrite history. Reschedule keeps identity, Patient, Doctor, Practice, Segment, Visit Type, reference, and price; it changes only the appointment and target schedule-derived duration snapshot.

## DEC-032 — Cancellation and reschedule

Patient/Guardian cancellation and reschedule use the Practice cutoff (baseline 120 minutes). Structured reasons are required. Patient-initiated reschedule is limited to two and is prohibited once Late. Provider reschedule requires explicit confirmed Patient consent and a reason, and does not consume the Patient allowance. Target claim is atomic; failure leaves the original appointment unchanged.

## DEC-033 — NoShow, restore, and expiration

NoShow is not timer-only. After a Reservation is Late, it requires the configured number of subsequent same-Practice/day Tickets actually entering `InProgress`; the default is three. Runtime triggering belongs to Phase 11. Phase 10 exposes no public mark-no-show endpoint. Same-day Reception restore must atomically reclaim capacity. Active Reservations expire idempotently at the effective operational-day end in the Practice timezone.

## DEC-034 — Doctor and Practice lifecycle

Doctor voluntary Practice deactivation and schedule/exception changes are rejected when they would invalidate future Active Reservations, with affected Reservation details. SuperAdmin Doctor suspension takes effect immediately and system-cancels future Active Reservations with reason `DoctorSuspended`, releasing capacity, appending history, and queuing durable notifications. Reactivation does not restore cancellations.

## DEC-035 — Reference, idempotency, and concurrency

Reservation uses a platform-unique, stable, non-sequential human reference such as `WSL-R-X7K92M`. Mutations require a narrow Actor + Operation + Idempotency-Key record and RowVersion for existing state. Cross-row booking invariants use deterministic Patient then Practice/date SQL application locks plus filtered unique indexes.

## DEC-036 — Phase 9 and Phase 11 boundaries

Phase 9 availability counts Active and ConvertedToTicket as occupied and releases Cancelled/NoShow/Expired. Popularity is the 90-day count of ConvertedToTicket transitions. FollowUp remains non-bookable until encounter-backed eligibility exists. Payments do not gate Reservation. BookingNote is operational Patient-supplied text, is never generic-log or administrative-default output, and is not clinical record truth. Ticket, Check-In, Walk-In, queue runtime, and passed-patient NoShow triggering remain Phase 11.

## Phase closure state

- Phase 8 assignable Reception permission discovery is closed through one authoritative delegatable allowlist and legacy umbrella backfill.
- Phase 10 implements the Reservation aggregate, persistence, lifecycle APIs, concurrency/idempotency controls, occupancy/popularity integration, expiration, and Doctor/Practice lifecycle guards.
