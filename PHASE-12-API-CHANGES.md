# Phase 12 API Changes

All routes below are under `/api/v1`. Financial mutation requests require `Idempotency-Key`; correction requests also require the current Base64 `RowVersion`. Finance authorization is checked against actor state, permission, Practice ownership or Reception assignment, and resource scope.

## Paid admission

Existing endpoints accept `paidAmount`, `paymentMethod` (`Cash`, `Card`, or `Wallet`), optional `referenceNumber`, and optional `notes`. Force Check-In retains its required `reason`.

| Method | Route |
| --- | --- |
| POST | `/practices/{practiceId}/reservations/{reservationId}/check-in` |
| POST | `/practices/{practiceId}/reservations/{reservationId}/force-check-in` |
| POST | `/practices/{practiceId}/tickets/walk-in` |

The backend requires full payment, assigns EGP and a PAY transaction number, and stores Payment with Ticket in one transaction. Existing admission idempotency applies to the new payment fields.

## Post-Phase-12 hardening: Reception Walk-In options

`GET /api/v1/reception/practices/{practiceId}/walk-in/options`

Actor: Reception only. Permission: `PracticeTickets.CreateWalkIn`, granted both to the Reception Role and to the active Practice assignment. The existing scoped authorization service checks authentication, active Reception account, completed initial password change (`IsFirstLogin = false`), Reception profile, active Doctor account, approved Doctor, active Practice, and active assignment.

Example response:

```json
{
  "practiceId": "6c12101a-cf3b-4311-bd6c-43e3185047dd",
  "currencyCode": "EGP",
  "segments": [
    {
      "segmentId": "150eecc5-e600-4a9b-a753-201ae177fbab",
      "nameAr": "عادي",
      "nameEn": "Normal",
      "priority": 0,
      "visitTypes": [
        {
          "visitTypeId": "22876d6e-9e20-4e7d-8b17-f71b9c47b94c",
          "code": "NewConsultation",
          "nameAr": "كشف جديد",
          "nameEn": "New Consultation",
          "price": 500
        }
      ]
    }
  ]
}
```

The Practice must have configuration with `AllowWalkIn = true`. Only active Segments and active `NewConsultation` Visit Types belonging to that Practice are returned, with an existing Price for the same Practice and Segment/Visit Type pair. Inactive items, FollowUp, missing prices, and items from other Practices are excluded. Segments are ordered by descending priority, then Arabic name and ID. A valid Practice with no priced combinations returns `200` with `segments: []`. Currency comes from `FinancialPolicy.CurrencyCode` (EGP).

The read has no Reservation date, time, slot, schedule, or availability dependency. Reception Walk-In screens should replace their `/doctors/me/practices/{id}/segments`, `/visit-types`, and `/prices` calls with this endpoint. Those Doctor management routes remain forbidden to Reception.

Denied scoped authorization returns `403`. Subsequent operational validation uses existing `Ticket.PracticeUnavailable` or `Ticket.WalkInNotAllowed` errors (`409`); missing configuration is unavailable. The POST remains authoritative: it revalidates state, catalog, and current price, requires both `PracticeTickets.CreateWalkIn` and `PracticeTickets.RecordPayment`, and saves Ticket plus full Payment atomically. A successful options read does not guarantee a later POST will succeed.

The Reception Role already seeds all required Reservation, Ticket, and Phase 12 finance permissions. No seed or assignment backfill changes are needed. Existing assignments receive no automatic `PracticePayments.View`, `PracticePayments.Correct`, or `PracticePayments.Refund` grants. The owning Doctor must explicitly delegate any of these permissions needed for the Reception's duties. Walk-In operations need `PracticeTickets.CreateWalkIn` and `PracticeTickets.RecordPayment`; Queue needs `PracticeTickets.View`; Reservation filter options need `PracticeReservations.View`; financial transactions need `PracticePayments.View`. Initial password change and account/Practice/assignment state checks remain required.

Deploy the updated API and switch the Reception frontend to the new options route. No database migration is required. Existing Queue, Reservation filter options, Finance endpoints, legacy Reservation/Ticket backfills, payment rules, and refund rules remain unchanged.

## Ticket refund eligibility

`GET /practices/{practiceId}/tickets/{ticketId}` and `POST /practices/{practiceId}/tickets/{ticketId}/cancel` expose `canRefund`, `isRefunded`, `refundableAmount`, and `currencyCode`, plus related transaction IDs/numbers when available. `canRefund` reflects the current actor's refund permission and Practice scope as well as the persisted Ticket and financial state.

## Financial mutations

| Method | Route | Request data |
| --- | --- | --- |
| POST | `/practices/{practiceId}/payments/{paymentId}/refund` | `refundMethod`, `refundReasonCode`, optional `reason`, `referenceNumber`, `notes` |
| POST | `/practices/{practiceId}/payments/{paymentId}/correct` | `paymentMethod`, optional `referenceNumber`, `notes`, required `correctionReason`, `rowVersion` |
| POST | `/practices/{practiceId}/refunds/{refundId}/correct` | `refundMethod`, `refundReasonCode`, optional `reason`, `referenceNumber`, `notes`, required `correctionReason`, `rowVersion` |

Refund amount and currency are taken from the Payment. The Ticket must be Cancelled and must never have entered InProgress. A matching idempotent replay returns the same financial result; a changed payload conflicts. Corrections create immutable audit history only when metadata changes.

## Financial reads

| Audience | Method and route | Data |
| --- | --- | --- |
| Reception or owning Doctor | GET `/practices/{practiceId}/financial-transactions` | Paginated Practice transactions and filters |
| Reception or owning Doctor | GET `/practices/{practiceId}/payments/{paymentId}` | Payment, optional Refund, correction histories, Ticket context |
| Reception or owning Doctor | GET `/practices/{practiceId}/payments/{paymentId}/receipt` | Payment receipt-ready data |
| Reception or owning Doctor | GET `/practices/{practiceId}/refunds/{refundId}/receipt` | Refund receipt-ready data |
| Doctor | GET `/doctors/me/financial-transactions` | Paginated transactions across owned Practices |
| Doctor | GET `/doctors/me/revenue/dashboard` | Summary and chart-ready daily, Practice, method, Segment, Visit Type datasets |
| Patient | GET `/patients/me/financial-transactions` | Paginated own transactions with corrected public values |
| Patient | GET `/patients/me/payments/{paymentId}/receipt` | Own Payment receipt |
| Patient | GET `/patients/me/refunds/{refundId}/receipt` | Own Refund receipt |
| SuperAdmin | GET `/admin/revenue/aggregates` | Doctor/Practice grouped totals only |

Dashboard queries accept `fromDate`, `toDate`, and optional `practiceId` for Doctor; the SuperAdmin aggregate also accepts optional `doctorId`. Payment and Refund use their respective Practice-local financial BusinessDates in all revenue calculations.
