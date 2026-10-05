# Phase 13 API Changes

Branch: `codex/phase-13-clinical-encounters`. Baseline: Master Source of Truth v1.2, DEC-056–069. All paths below use API v1. JSON names are camelCase; enum values are strings. Dates are `yyyy-MM-dd`, slot times are `HH:mm`, timestamps are UTC, and rowversions are opaque Base64 strings.

## Breaking frontend change: Complete Visit

`POST /api/v1/practices/{practiceId}/tickets/{ticketId}/complete` now requires both independently current tokens:

```json
{
  "ticketRowVersion": "<latest TicketDetails.rowVersion>",
  "encounterRowVersion": "<latest EncounterDetails.rowVersion>"
}
```

Continue sending `Idempotency-Key` on existing operational writes. Completion validates required clinical notes and the current diagnosis set, then completes Ticket and Encounter and consumes a matching follow-up claim in one transaction. A failed/stale write commits none of these transitions. Refresh details after HTTP 409; never silently overwrite or retry with guessed tokens. Matching idempotency replay returns current persisted TicketDetails without another encounter or consumption.

Start Visit keeps its existing request (`rowVersion`) and response type. Successful start now creates exactly one MedicalEncounter in the same transaction. TicketDetails adds `medicalEncounterId`, `medicalEncounterRowVersion`, and `followUpEligibilityId`; no clinical notes, diagnoses, or amendment snapshots are added to Ticket responses. These fields are nullable before start or for historical tickets.

## Doctor endpoints

The prefix is `/api/v1/doctors/me/practices/{practiceId}`. Every action requires an active account, an approved owning Doctor, an active owned Practice, and its granular permission. Cross-Doctor access and implicit SuperAdmin access are denied.

| Method | Suffix | Request / result |
| --- | --- | --- |
| GET | `/encounters` | Filters `patientId`, `status`, `fromDate`, `toDate`, `search`, `pageNumber`, `pageSize`; paged summaries |
| GET | `/encounters/{encounterId}` | Current doctor EncounterDetails |
| GET | `/tickets/{ticketId}/encounter` | EncounterDetails discovered from Ticket |
| PATCH | `/encounters/{encounterId}/clinical-notes` | `{clinicalNotes,rowVersion}`; updated EncounterDetails |
| POST | `/encounters/{encounterId}/diagnoses` | `{type,displayText,notes?,encounterRowVersion}`; 201 updated EncounterDetails |
| PUT | `/encounters/{encounterId}/diagnoses/{diagnosisId}` | Same diagnosis request; updated EncounterDetails |
| DELETE | `/encounters/{encounterId}/diagnoses/{diagnosisId}` | JSON body `{rowVersion}`; updated EncounterDetails |
| POST | `/encounters/{encounterId}/amendments` | `{reason,encounterRowVersion,changes[]}`; 201 updated EncounterDetails |
| GET | `/encounters/{encounterId}/amendments` | Doctor-only immutable amendment history |
| POST | `/encounters/{encounterId}/follow-up-eligibility` | `{validUntil}`; 201 FollowUpEligibilityResponse |

EncounterDetails contains `encounterId`, `ticketId`, `doctor`, `practice`, `patient`, `status`, `startedAtUtc`, `completedAtUtc`, `clinicalNotes`, `diagnoses[]`, source `followUpEligibility`, `capabilities`, and `rowVersion`. Parties contain `id`, `nameAr`, `nameEn`. Diagnoses contain `diagnosisId`, `type`, `displayText`, `notes`.

Capabilities are server-computed: `canEditClinicalNotes`, `canManageDiagnoses`, `canComplete`, `canAmend`, `canCreateFollowUpEligibility`. They describe the current actor and encounter; authoritative mutation validation still applies. Successful note/diagnosis/amendment changes return the latest encounter token, including deletion and child-only edits.

Doctor encounter list filtering, counting and pagination run in the database. Date boundaries use the Practice timezone, include `toDate`, and handle skipped/ambiguous midnight. Search is bounded to 200 characters and searches patient names. Summaries contain encounter/ticket/patient/practice identity and names, visit-type snapshots, status, times, `hasDiagnosis`, `hasFollowUpEligibility`, and `rowVersion`. Page envelopes contain `items`, `totalCount`, `pageNumber`, `pageSize`; default size 20, maximum 100.

Notes may be blank while InProgress but must be nonblank on completion; maximum 8,000 characters. Diagnosis display text is required (maximum 500); notes maximum 2,000. Zero diagnoses is allowed. Any nonempty active set must have exactly one Primary, and trimmed text must be unique ignoring case. InProgress diagnosis deletion removes the draft row. Completed notes/diagnoses reject ordinary CRUD.

## Amendment contract

Corrections are applied as one staged, validated transaction. A reason is mandatory (maximum 1,000 characters), changes contain 1–100 items, and an actual change is required. ClinicalNotes accepts Updated. Diagnosis accepts Added, Updated, Removed. Added generates its own diagnosis ID; Updated/Removed require an active diagnosis ID from current details.

```json
{
  "reason": "Reviewed the completed documentation",
  "encounterRowVersion": "<latest token>",
  "changes": [
    {"target":"ClinicalNotes","changeType":"Updated","clinicalNotes":"Corrected notes"},
    {"target":"Diagnosis","changeType":"Updated","diagnosisId":"<current diagnosis ID>",
     "type":"Primary","displayText":"Corrected diagnosis","notes":null}
  ]
}
```

Removing a completed diagnosis voids it. Current patient/doctor views show active corrected values. History preserves amendment sequence, reason, author Doctor/user, UTC time, target, change type, and server-generated before/after snapshots. Client-supplied snapshots are never accepted. A replacement Primary can remove the old Primary and add its replacement in the same amendment. History cannot be modified/deleted through the persistence boundary.

## Patient clinical and booking views

| Method | Path | Result |
| --- | --- | --- |
| GET | `/api/v1/encounters/mine` | Own Completed encounter summaries; paginated |
| GET | `/api/v1/encounters/mine/{encounterId}` | Own Completed current diagnoses and eligibility |
| GET | `/api/v1/follow-up-eligibilities/mine` | Paged eligibility; optional `patientId`, `status`, `pageNumber`, `pageSize` |
| GET | `/api/v1/follow-up-eligibilities/mine/{eligibilityId}` | Own or currently bookable dependent eligibility |
| GET | `/api/v1/reservations/follow-up-eligibilities/{eligibilityId}/available-dates` | Existing booking date response, clamped by eligibility and advance horizon |
| GET | `/api/v1/reservations/follow-up-eligibilities/{eligibilityId}/available-slots?date=...` | Existing available-slot response |
| GET | `/api/v1/reservations/follow-up-eligibilities/{eligibilityId}/booking-options?date=...&time=...` | Existing segment/visit-type/price response, FollowUp only |

Patient clinical detail contains `encounterId`, `ticketId`, `doctor`, `practice`, `startedAtUtc`, `completedAtUtc`, `currentActiveDiagnoses[]`, `followUpEligibility`. It excludes ClinicalNotes, amendment reasons/snapshots/internal history and drafts. Family booking access does not grant family clinical reads.

Eligibility responses contain `eligibilityId`, `sourceEncounterId`, `patient`, `doctor`, `practice`, `status`, `validUntil`, `reservedReservationId`, `reservedTicketId`, `consumedEncounterId`, `canBook`, `rowVersion`. Available records past the Practice-local deadline are presented as Expired and cannot be booked even if no scheduler ran. `canBook` indicates available, unexpired eligibility at an operational Doctor/Practice; use availability/options to obtain actual selectable dates, segments and backend prices.

## Reception operational discovery

`GET /api/v1/reception/practices/{practiceId}/patients/{patientId}/follow-up-eligibilities` requires active assignment plus global and assignment-delegated `FollowUpEligibility.ViewBookingEligibility`. It returns available eligibility only: `eligibilityId`, `patientId`, `doctorId`, `practiceId`, `validUntil`, `status`, `canBook`, `rowVersion`. It exposes no source encounter ID or clinical content. The bounded lookup returns at most 100 available items.

Existing Reception routes accept `patientId` and `followUpEligibilityId` query parameters when requesting FollowUp:

- `/api/v1/reception/practices/{practiceId}/booking/available-dates`
- `/api/v1/reception/practices/{practiceId}/booking/available-slots?date=...`
- `/api/v1/reception/practices/{practiceId}/booking/options?date=...&time=...`
- `/api/v1/reception/practices/{practiceId}/walk-in/options`

Ordinary requests keep their existing NewConsultation behavior. Follow-up options reuse schedules, exceptions, occupancy, quotas, conflict checks and pricing; selection IDs and prices come from those responses.

## Reservation and Walk-In writes

The existing create Reservation endpoints (`POST /api/v1/reservations` and `POST /api/v1/reception/practices/{practiceId}/reservations`) and paid Walk-In (`POST /api/v1/practices/{practiceId}/tickets/walk-in`) add optional `followUpEligibilityId` and `followUpEligibilityRowVersion` fields. Supply the ID for a FollowUp visit type; omit it for NewConsultation. An optional supplied eligibility token must match. Doctor, patient, practice, available status and deadline are validated server-side, and writes still require their existing operational permissions/idempotency key.

ReservationDetails adds `followUpEligibilityId`, `followUpEligibilityStatus`, `followUpValidUntil`. Existing reschedule availability and capability responses respect the eligibility deadline. Restore retains the existing capacity, operational-day and conflict rules and reacquires a free claim. Payment amount remains the current backend price; follow-up does not bypass paid admission.

## Follow-up lifecycle

- Doctor explicitly creates Available eligibility after a Completed source encounter; one source can create at most one eligibility.
- Reservation creation reserves a reservation claim; Walk-In reserves a ticket claim. Neither consumes it.
- Check-In atomically creates Ticket and Payment and transfers the reservation claim to the ticket. **ReservedReservationId and ReservedTicketId are mutually exclusive**, following authoritative DEC-063.
- Cancel, NoShow and operational expiration release only the matching claim in the same persistence transaction, to Available or Expired using the Practice business date. Historical cancelled/no-show operations cannot release another booking's claim.
- Restore must reacquire Available, unexpired eligibility. Reschedule and Check-In revalidate deadlines/current claims.
- Successful follow-up completion atomically consumes eligibility and records the completed encounter. Consumption is single-use.

SQL Server rowversions arbitrate writers on the shared eligibility row across Reservation and Ticket workflows, including workflows with different application-lock keys. A losing write rolls back its Reservation/Ticket/Payment/history/idempotency changes.

## Permission additions

PermissionNames.All appends these names, preserving prior position-derived IDs:

| Grant | Names |
| --- | --- |
| Doctor | `MedicalEncounters.ViewOwn`, `MedicalEncounters.StartOwn`, `MedicalEncounters.UpdateOwn`, `MedicalEncounters.CompleteOwn`, `MedicalEncounters.AmendOwn`, `Diagnoses.ViewOwn`, `Diagnoses.ManageOwn`, `FollowUpEligibility.ViewOwn`, `FollowUpEligibility.CreateOwn` |
| Reception (delegated per Practice) | `FollowUpEligibility.ViewBookingEligibility` |
| Patient | `MedicalEncounters.ViewOwnCompleted`, `Diagnoses.ViewOwnCompleted`, `FollowUpEligibility.ViewOwn` |
| SuperAdmin | No new clinical grants |

Existing assignment creation/update must explicitly delegate the new operational permission where needed; global grants alone do not authorize Reception.

## Errors, logging and migration

HTTP 400 covers malformed/invalid input, 403 denied clinical or assignment access, 404 inaccessible/missing patient-owned records, 409 invalid lifecycle/deadline/claim/concurrency, and 201 successful creates. Expected failures use stable localized English/Arabic ProblemDetails codes, including `MedicalEncounter.ConcurrencyConflict`, `Diagnosis.Duplicate`, and `FollowUpEligibility.ConcurrencyConflict`, `.Expired`, `.AlreadyReserved`, `.AlreadyConsumed`.

Generic request logs remain metadata-only. Clinical notes, diagnosis text/notes, amendment snapshots/reason/changes and search values are added to the product redaction policy. Explicit audit tables preserve clinical history separately.

Migration: `20261004152344_Phase13ClinicalEncountersFollowUp`. It adds clinical tables, nullable Ticket/Reservation links, rowversions, restrictive foreign keys, indexed discovery and unique/filtered claim/diagnosis constraints. It adds no fabricated clinical backfill for historical Tickets. Deployment should complete legacy InProgress visits before enabling the new completion contract; historical completed Tickets remain operational records without invented encounters. Migration application to production is a separate deployment operation.
