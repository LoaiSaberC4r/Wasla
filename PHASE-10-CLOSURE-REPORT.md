# Phase 10 closure report

## Defects fixed

- Reservation metadata is common-authenticated while Patient operations remain Patient-only.
- The legacy reception umbrella permission is no longer assignable or accepted; existing assignments are normalized to granular grants.
- Late, filters, summaries, and capabilities use each Reservation Practice's real grace period without per-row queries.
- Patient reschedule enforces online-booking, cutoff/Late/limit, current catalog validity, actionable reservation-aware availability, and no-op rejection.
- RestoreFromNoShow capabilities now evaluate status, historical local business date, operational end, active Practice, daily capacity, and Segment capacity; confirmation revalidates under transaction.
- Upcoming/History classification uses `ScheduledStartUtc`; lifecycle operations retain historical timezone meaning until an intentional reschedule replaces the snapshot.
- Doctor operational access requires an approved Doctor, active account, and active owned Practice.
- Dependent create/view/cancel/reschedule permissions are evaluated independently.
- Schedule/deactivation conflicts have a structured ProblemDetails contract.
- Responsible-contact notification resolution is centralized and Doctor suspension recipient/practice reads are bounded rather than N+1.
- Reservation projection work is durable, after-commit, retryable, and independent from transaction success.
- Reservation references use bounded generation retry plus a transaction-owned candidate lock and uniqueness recheck.
- Missing and invalid idempotency keys have distinct stable errors.
- Historical inactive Practice Segments remain available to filter options through the Practice-scoped catalog projection.

## Endpoint contract changes

- `GET /api/v1/reservations/metadata`: any authenticated actor.
- `GET /api/v1/reservations/mine`: Upcoming/History use appointment UTC instant.
- Reservation details: capabilities reflect current online-booking/catalog/restore policy.
- Reservation-aware reschedule date/slot endpoints return only targets that pass deterministic booking constraints.
- Schedule-period, schedule-exception, and Practice-deactivation conflicts return structured affected-Reservation fields.
- Public Practice search responses expose `onlineBookingEnabled` and `bookingDisabledReason`.

## Security and persistence

- Corrective migration: `20260916153802_Phase10Hardening`.
- `ReservationHistories -> Reservations` is `RESTRICT` rather than cascade.
- New durable `ReservationProjectionInvalidations` table has a unique idempotency index and pending-work index.
- The migration backfills granular reception Reservation grants before deleting legacy assignment-level umbrella grants. The system permission itself remains for compatibility.

## Concurrency, notification, projection, and tests

- Create lock order is Patient, Practice/date, Idempotency, then Reference candidate.
- Recipient resolution deduplicates Patient, approved responsible/guardian, and applicable family-actor emails without logging addresses.
- Create, Cancel, Reschedule, RestoreNoShow, NoShow, Expire, Doctor suspension, and ConvertToTicket queue projection work atomically; the processor observes it only after commit.
- Unit, integration, architecture, migration/model, API-contract, and SQL Server application-lock contention coverage were added. GitHub Actions runs Release build and all suites with a SQL Server 2022 service.

Local closure verification on 2026-09-16:

- Release build: succeeded with 0 warnings and 0 errors.
- Unit: 68 passed, 0 failed.
- Integration excluding the SQL-specific category: 40 passed, 0 failed.
- SQL Server 2022 concurrency: 8 passed, 0 failed.
- Architecture: 6 passed, 0 failed.
- CI workflow: added; its complete command set passed locally. The remote GitHub Actions run begins when the branch is pushed/opened as a pull request.

## Remaining technical debt

No unresolved Phase 10 business defect is known. Projection processing is intentionally eventually consistent and retryable; direct public availability remains immediately correct because it reads committed occupancy.
