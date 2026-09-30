# Wasla Source-of-Truth Decision Addendum — Phase 12 — 2026-09-30

This addendum extends the Phase 11 paid-admission decisions. It governs Phase 12 Payments, Refunds, financial visibility, and Doctor revenue.

## DEC-049 — Collected payment

Reception records one full Payment while creating a Ticket through Reservation Check-In, Force Check-In, or Walk-In. Payment and Ticket commit atomically. Amount must equal the Ticket price snapshot and be positive. New payments select Cash, Card, or Wallet, with optional reference and notes. Currency is backend-owned EGP. No prepayment, partial payment, gateway, or insurance flow exists in V1. A legacy method marker preserves Phase 11 rows whose collection method was not recorded and is never accepted for new admission.

## DEC-050 — Financial identity and business date

Payment and Refund are separate, immutable financial transactions. Each stores an immutable human-readable PAY or REF number, per-Practice/day/type sequence, owning Doctor/Practice, Patient, Ticket, amount, currency, actor, UTC occurrence time, and Practice-local financial BusinessDate. A Refund is reported on its own BusinessDate. Number allocation is transaction-safe and database uniqueness is authoritative. Payment remains Paid after Refund; a Refund record represents the negative amount in financial reporting.

## DEC-051 — Explicit full refund

Refund is a separate action after Ticket cancellation. It is never automatic. The Ticket must be Cancelled and must never have entered InProgress, established from persisted Ticket state/history. NoShow may be cancelled and then refunded when unserved. Refund amount and currency come from the original Payment; the caller cannot choose them. Each Payment has at most one full Refund. Refund method is independently chosen from Cash, Card, or Wallet. Reason code is one of PatientRequestedCancellation, DoctorUnavailable, DuplicatePayment, WrongPaymentMethod, OperationalError, or Other; Other requires text. Refund cannot be deleted, cancelled, or reversed. A refunded NoShow Ticket cannot return to the queue.

## DEC-052 — Corrections and audit

Payment correction may change only method, reference, and notes; it is forbidden after Refund. Refund correction may change only method, reason code/text, reference, and notes. Both require a correction reason, an actual change, current RowVersion, and immutable old/new history with actor and UTC time. Amount, currency, identity, transaction number, original occurrence time, and original actor never change. Financial mutations require an Idempotency-Key; matching replay returns the previous result and changed payload conflicts.

## DEC-053 — Actor scope and privacy

Reception requires active account, active Practice assignment, global permission, and assignment-delegated permission for each financial operation; only assigned Practice data is visible. Owning Doctor may view and correct Practice financial details, refund, and view the Doctor revenue dashboard. Patient may view only their own current corrected transactions and receipt data; staff identities, notes, internal audit, and other patients are excluded. SuperAdmin receives grouped aggregates only, with no patient or transaction drill-down. New permission names are appended to PermissionNames.All to preserve existing position-derived IDs.

## DEC-054 — Revenue source and chart datasets

Gross Revenue is the sum of collected Payments; Refunds is the sum of recorded Refunds; Net Revenue is Gross minus Refunds. Ticket cancellation alone does not reduce revenue. Each transaction uses its own financial BusinessDate. The Doctor dashboard uses one date and optional owned-Practice filter across summary, daily trend, Practice, payment method, refund method, Segment snapshot, and Visit Type snapshot datasets. SuperAdmin totals can be grouped by Doctor and Practice. Authoritative aggregation is performed in database queries, not by summing Ticket prices in application memory.

## DEC-055 — Receipt-ready data and scope boundary

Backend receipt data includes transaction numbers, Patient/Doctor/Practice names, Ticket number, amount, EGP, method, reference when present, UTC occurrence time, and BusinessDate. Refund receipts include original Payment number and reason. Server-generated PDFs and external notifications are not required. Expenses, salaries, accounting ledger, platform commission, subscriptions, multiple currencies, and refund reversal remain outside Phase 12.
