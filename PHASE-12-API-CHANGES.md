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
