# Commercial subscriptions addendum

Version: **1.0.0**. Approved specification implemented on **2026-11-02**. Currency: EGP.

The historical `Healthcare-Platform-Master-Source-of-Truth-v1.2-Updated.txt` leaves platform monetization open (subscription, commission, or hybrid). This addendum records the newly approved subscription model. It does not replace or edit the historical source, patient payment rules, medical privacy, relationship authorization, or clinical workflows.

## Commercial rules

There are two selectable products: `FULL_PLATFORM` and `ONLINE_BOOKING_ONLY`. The latter stays disabled until approved prices and a maximum practice count are configured. `LEGACY_TRANSITION` is an internal Full Platform plan and is never publicly selectable.

| Full Platform plan | Monthly | Annual, paid upfront | Practices | Active reception identities |
|---|---:|---:|---:|---:|
| STARTER | 499 | 4,990 | 1 | 1 |
| PROFESSIONAL | 999 | 9,990 | 3 | 5 |
| PREMIUM | 1,799 | 17,990 | 5 | 10 |

Prices and capacities are configuration values, not domain or handler constants. Annual prices are explicitly configured and validated to equal ten monthly payments. Both cycles have the same feature and capacity entitlements. New selections and renewals use current configuration; accepted subscriptions and paid future periods retain their snapshots.

`STARTER_FREE_TRIAL_20D` is an offer on Full Platform Starter. It is neither a product nor a separate plan. Registration records a pending selection. Approval activates its Starter features in the same transaction, with `TrialStartedAtUtc = Doctor.ApprovedOnUtc` and `TrialEndsAtUtc = Doctor.ApprovedOnUtc.AddDays(configured duration)`. The approved default duration is 20 days. There is no start-trial endpoint, payment, card requirement, automatic charge, or automatic conversion. One trial per doctor is protected by a filtered unique index.

Trial reminders are scheduled five, two, one, and zero days before expiry. An authorized SuperAdmin may extend an active trial by positive additional days with a mandatory reason. History records both expiration dates. Pending old reminders are cancelled and new revisioned reminders are created.

Paid selections remain pending after doctor approval. SuperAdmins manually confirm the exact expected snapshot amount and an enabled payment method before activation. Cash is enabled; Card and EWallet are disabled. SaaS payments have their own entity and never use patient payments. Payment intents use durable idempotency keys and fingerprints. Monthly periods use `AddMonths(1)` and annual periods use `AddYears(1)`.

Approved doctors without effective paid or trial access retain profile, subscription, and onboarding access. The existing one-location onboarding contract may save an **inactive draft** against the pending selection's practice capacity; that draft counts toward all later practice limits. It does not activate operations or public booking. Normal practice and reception creation require effective entitlements.

## Entitlements

Features exist independently of plans. Each subscription stores accepted terms JSON and relational feature snapshots. Authorization resolves database permission mappings against those snapshots, without plan-name checks in permission handlers. Full includes ReadOnly; ReadOnly never authorizes a mutation. Implementation status and release readiness are separate catalog metadata; included features currently carry `NeedsReview`, rather than an unverified release-ready claim.

| Feature | Booking Only | Starter / Trial | Professional | Premium |
|---|---|---|---|---|
| DOCTOR_PROFILE | Full | Full | Full | Full |
| PRACTICE_MANAGEMENT | Full | Full | Full | Full |
| PRACTICE_BRANDING | Full | Full | Full | Full |
| SCHEDULE_AVAILABILITY | Full | Full | Full | Full |
| PUBLIC_DISCOVERY | Full | Full | Full | Full |
| ONLINE_BOOKING | Full | Full | Full | Full |
| RESERVATION_MANAGEMENT | ReadOnly | Full | Full | Full |
| PATIENT_MANAGEMENT | — | Full | Full | Full |
| BASIC_SEGMENTS_PRICING | — | Full | Full | Full |
| RECEPTION_MANAGEMENT | — | Full | Full | Full |
| CHECKIN_WALKIN | — | Full | Full | Full |
| TICKET_QUEUE | — | Full | Full | Full |
| PAYMENT_COLLECTION | — | Full | Full | Full |
| DOCTOR_REVENUE_DASHBOARD | — | Full | Full | Full |
| MEDICAL_ENCOUNTER | — | Full | Full | Full |
| DIAGNOSIS | — | Full | Full | Full |
| PRESCRIPTION | — | Full | Full | Full |
| LAB_RADIOLOGY | — | Full | Full | Full |
| FOLLOWUP_ELIGIBILITY | — | Full | Full | Full |
| MEDICAL_RECORD_ACCESS | — | Full | Full | Full |
| ADVANCED_SEGMENTS_PRIORITY_QUOTA | — | — | Full | Full |
| RECEPTION_HANDOVER | — | — | Full | Full |
| OPERATING_EXPENSES | — | — | Full | Full |
| SALARY_MANAGEMENT | — | — | Full | Full |
| OPERATIONAL_REPORTS | — | — | Full | Full |
| PDF_XLSX_EXPORT | — | — | Full | Full |
| CROSS_PRACTICE_COMPARISON | — | — | — | Full |
| ADVANCED_RULE_BASED_INSIGHTS | — | — | — | Full |

Booking Only uses the existing booking engine and practices. A narrow booking-presentation price endpoint writes the existing default segment's new-consultation price without exposing advanced segment controls. New practices without check-in entitlements have walk-in disabled. Operational configuration changes, reservation mutations, reception, tickets, clinical work, and finance remain unavailable to Booking Only.

Patient global identity, the patient's own medical history, privacy, consent, guardianship, family authority, audit, and SuperAdmin governance remain governed by their existing rules. Subscription checks add a commercial requirement; they never grant permission, practice scope, relationship access, or consent.

## Capacity and concurrency

Every persisted doctor practice counts, including inactive, incomplete, deactivated, and legacy onboarding records. Deactivation does not release practice capacity. Reception usage counts distinct owned reception identities joined to active application users; assignments do not consume extra seats or release them when disabled. Account deactivation releases a seat; reactivation checks capacity. Reception's row version and an account-state revision protect that operation without adding a parallel Identity lifecycle.

Capacity checks and writes use the existing transaction behavior and the shared scoped `WaslaDbContext`. SQL Server transaction-owned `sp_getapplock` serializes each doctor's practice creation, reception creation/reactivation, subscription changes, approval, and lifecycle processing. Authorization's unchanged tracked doctor/subscription records are refreshed after lock acquisition, so a pre-transaction authorization read cannot supply stale limits to a write.

## Changes, renewal, expiry, and legacy doctors

Immediate upgrades require a higher Full Platform plan within the same billing cycle, or a higher Booking Only practice count. The server snapshots target terms and computes the full cycle-price difference without proration. The current period end stays unchanged. A same-tier Booking Only increase may have a zero difference, still explicitly confirmed by SuperAdmin.

Downgrades and cycle changes are scheduled for renewal. Actual practice and active reception usage must fit the target, and usage is checked again when renewal payment is recorded. Excess usage returns current counts and target capacities. No resources are deleted or automatically deactivated.

Manual renewal before expiry creates a separate, already-paid **Scheduled** next period beginning at the existing exclusive end. Renewal after expiry begins at activation time. Scheduled access becomes effective at its start timestamp even if the background status update is delayed. This additional state preserves one active subscription row and does not implement automatic renewal or payment. Cancellation records intent at period end and preserves paid access.

Expiry records history and queues an idempotent notification. It blocks new reservations, practices, reception accounts, walk-ins, and other new commercial work. Historical reads retain accepted entitlements. Continuity mutations must resolve an actual owned reservation, ticket, encounter, prescription, or diagnostic resource and verify its original visit commitment predates expiry. Patient access and existing reservation rights remain intact.

Legacy enrollment is explicit, SuperAdmin-only, disabled by default, and uses one durable launch batch. Eligibility is bounded by the first commercial product seed timestamp, so newly registered doctors awaiting paid activation do not accidentally receive transition access. Eligible pre-commercial doctors may already have a pending paid selection. Each enrolled doctor receives the batch's same 30-day start/end, full features, and actual usage capacities with configured minimums of one practice and one reception. Replays never reset dates. Paid activation supersedes the transition atomically. An internal transition does not itself constitute the doctor's first selectable commercial product choice.

## Implementation references

- Domain: `src/Wasla/Wasla.Domain/Subscriptions/`.
- Catalog, selection, management, entitlement, continuity, and lifecycle commands: `src/Wasla/Wasla.Application/Features/Subscriptions/`.
- Focused persistence contract: `src/Wasla/Wasla.Application/Persistence/ISubscriptionDataStore.cs`.
- EF configurations, store, deterministic catalog seeder, and migration: `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/`.
- API and Angular contract: [docs/subscriptions/API-CONTRACT.md](docs/subscriptions/API-CONTRACT.md).
- Deployment and actual verification: [SUBSCRIPTION-COMMERCIAL-IMPLEMENTATION-REPORT.md](SUBSCRIPTION-COMMERCIAL-IMPLEMENTATION-REPORT.md).

Gateways, Fawry, card/wallet processing, automatic charges/renewal, fees, commission, taxes, invoices, refunds, add-ons, promotions, product switching between selected commercial products, and Angular implementation remain out of scope.
