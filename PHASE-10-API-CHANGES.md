# Phase 10 frontend/API change summary

## Patient and Guardian

- `GET /api/v1/reservations/metadata` is a common authenticated endpoint for Patient, Reception, Doctor, and SuperAdmin clients. The remaining `/reservations` controller stays Patient-only.
- `GET /api/v1/reservations/bookable-patients`
- `POST /api/v1/reservations`
- `GET /api/v1/reservations/mine`
- `GET /api/v1/reservations/mine/{reservationId}`
- `POST /api/v1/reservations/mine/{reservationId}/cancel`
- Reservation-aware reschedule date, slot, and confirmation endpoints under `/mine/{reservationId}/reschedule`.

## Reception

The existing `GET /api/v1/reception/practices` remains the Practice-context source.

- `GET /api/v1/doctors/me/receptions/assignable-permissions`
- Booking dates, slots, and options under `/api/v1/reception/practices/{practiceId}/booking`
- Create, list, filter-options, details, cancel, provider-reschedule, and restore-no-show under the Practice reservations route.

## Doctor

Under `/api/v1/doctors/me/practices/{practiceId}/reservations`: list, filter-options, details, cancel, and consent-backed reschedule availability/confirmation. There is no Doctor create endpoint.

## SuperAdmin

- `GET /api/v1/admin/reservations`
- `GET /api/v1/admin/reservations/{reservationId}`
- `GET /api/v1/admin/doctors/{doctorId}/suspension-impact`
- Existing Doctor suspension now returns `CancelledReservationCount` and atomically cancels future Active Reservations.

All create/cancel/reschedule/restore mutations require `Idempotency-Key`. A missing key returns `Reservation.IdempotencyKeyRequired`; malformed, non-ASCII, or over-200-character keys return `Reservation.IdempotencyKeyInvalid`; reuse with a different payload remains a 409 `Reservation.IdempotencyKeyReused`. Existing-Reservation mutations also require the current Base64 `RowVersion`.

Detail responses provide server-derived capabilities and a localized timeline. Patient reschedule capabilities and commands both enforce `AllowOnlineBooking`, cutoff, Late, the two-reschedule limit, and current Segment/Visit Type/pricing validity. Reception restore capability now applies the same business-date, operational-day, Practice, daily-capacity, and Segment-capacity rules as confirmation. Administrative details omit BookingNote.

`GET /api/v1/reservations/mine?view=Upcoming|History` classifies against `ScheduledStartUtc`; explicit `fromDate`/`toDate` parameters remain Practice `BusinessDate` filters.

Schedule-period update/delete, schedule-exception create/update/delete, and Practice deactivation return a structured 409 for `DoctorPractice.FutureReservationsExist`, with `affectedReservationsCount` and an `affectedReservations` array. Clients no longer parse JSON embedded in an error-details string.
