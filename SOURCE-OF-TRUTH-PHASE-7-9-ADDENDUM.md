# Wasla Source-of-Truth Decision Addendum — 2026-09-15

This repository-tracked addendum records the governance delta applied to the authoritative `Healthcare-Platform-Master-Source-of-Truth-v1.1-Updated.txt`. Historical decisions remain intact; DEC-022 through DEC-027 are the current rules where older text conflicts.

## DEC-022 — Schedule period is the single V1 appointment-duration source

- Previous: DEC-017/DEC-019 allowed VisitType duration to differ from schedule granularity.
- New: `DoctorPracticeSchedulePeriod.SlotDurationMinutes` is the appointment duration. `DefaultSlotDurationMinutes` is setup assistance only; VisitType has no duration.
- Impact: remove VisitType duration from domain/API/EF/database while preserving schedule and Practice/location data.

## DEC-023 — Reception access is revalidated per Practice operation

- Previous: Practice-scoped delegation existed without an explicit closure rule for fresh state validation.
- New: server-side authorization requires active Reception user, Approved Doctor with active user, active owned Practice, active assignment, global role permission and delegated assignment permission; resource state is checked by the operation.
- Impact: stale token claims or a frontend-supplied Practice ID cannot preserve revoked access. No schema change; Phase 8 is closure-verified.

## DEC-024 — Public publication is automatic and profile scope is explicit

- Previous: a separate manual publication state and Languages were proposed.
- New: Approved Doctor + active Doctor user + at least one active Practice is automatically discoverable. V1 adds a plain-text Bio (maximum 2000 characters) and ordered bilingual Qualifications; Languages and manual publication are deferred.
- Impact: add Bio, normalized names and `DoctorQualification`; public projections remain allowlisted and Doctor-owned mutations use permissions/concurrency.

## DEC-025 — Search returns one Doctor with all active Practices

- Previous: name normalization, location ordering and multi-Practice shape were unspecified.
- New: filter by normalized Doctor name, active specialization and Governorate/City/Area. Normalize Arabic diacritics/tatweel/Alef variants/`ى→ي`, collapse spaces, and never map `ة→ه`. Return each Doctor once, all active Practices, location matches first. Rank by 90-day successful-Reservation popularity, earliest actual next slot, then stable Doctor ID.
- Impact: persisted indexed normalized names and a replaceable popularity reader; Phase 10 will supply real counts.

## DEC-026 — Public price is Normal + NewConsultation

- Previous: the pricing matrix had no canonical search-card price.
- New: `PublicSearchPrice` is exactly active default Normal Segment + active NewConsultation VisitType, never matrix minimum. Missing base price makes a Practice non-bookable but does not hide it. FollowUp remains modeled/priced but is excluded from anonymous options until clinical eligibility exists.
- Impact: server-priced public search/details/options; no fake FollowUp eligibility.

## DEC-027 — Availability is Practice-local with a 30-day horizon

- Previous: horizon, timezone, lead-time and pre-Reservation occupancy rules were incomplete.
- New: use Practice `TimeZoneId`, today plus 29 days, and no extra lead time beyond slot start being after local now. Apply recurring periods, DayOff/Vacation, CustomWorkingHours, BlockedTimeRange and maximum daily capacity. Return only available slots and structured `NextAvailableSlotDate`, `NextAvailableSlotTime`, `IsToday`.
- Impact: occupancy and popularity are bulk read abstractions returning empty/zero until Phase 10. Phase 9 adds no Reservation endpoint, aggregate, lifecycle or persistence.

## Phase closure state

- Phase 7: corrected and closed under DEC-022.
- Phase 8: closure/security invariant verified and hardened under DEC-023.
- Phase 9: public discovery/profile/availability implemented under DEC-024..DEC-027.
- Phase 10: not implemented.
