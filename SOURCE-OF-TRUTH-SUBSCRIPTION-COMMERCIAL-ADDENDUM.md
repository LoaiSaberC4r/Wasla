# Commercial subscriptions addendum v1.2

Approved 2026-11-05. Implementation evidence is maintained in docs/subscriptions.

## Supersession and revision history

- v1.2: independent contract series, practice allocations, doctor-initiated lifetime trial, per-contract reception seats, 50 EGP monthly Booking Only seats, proven original reservation rights and paid reminders.
- v1.0 (2026-11-02): preserved below as historical record; conflicting single-contract, approval-started trial, global capacity and tier-pricing rules are superseded.
- Healthcare identity, privacy, guardian/consent, practice scope and clinical workflow rules retain their original authority.

## Approved decision record (verbatim)

```text
WASLA — COMMERCIAL SUBSCRIPTIONS BUSINESS CLOSURE
Commercial Addendum v1.2 | 2026-11-05
STATUS: BUSINESS RULES APPROVED; NOT IMPLEMENTED IN REPOSITORY
SCOPE: Multiple independent subscriptions, practice allocations, Booking Only, Free Trial,
       expiry/renewal, grandfathered commitments, reception seats and migrations.

AUTHORITY / CHANGE CONTROL
- This is the consolidated approved commercial decision record from the Wasla discussion.
- It supersedes the conflicting commercial rules in repository
  SOURCE-OF-TRUTH-SUBSCRIPTION-COMMERCIAL-ADDENDUM.md (v1.0.0, 2026-11-02)
  and the earlier Wasla-Commercial-Subscriptions-Source-of-Truth-Addendum-v1.1.txt.
- Healthcare-Platform-Master-Source-of-Truth-v1.2-Updated.txt remains authoritative for
  identity, patient/family rights, consent/privacy, clinical workflows and practice scope.
- For a proposed technical shape, do not treat an engineering suggestion as a new business rule.
- No source files were modified in GitHub by creating this document. PR #36 remains baseline.

01. APPROVED COMMERCIAL CATALOG
FULL_PLATFORM plans (EGP):
  STARTER      499/month; 4,990/year upfront; 1 Practice; 1 active Reception identity.
  PROFESSIONAL 999/month; 9,990/year upfront; 3 Practices; 5 active Reception identities.
  PREMIUM    1,799/month; 17,990/year upfront; 5 Practices; 10 active Reception identities.
Existing feature matrix, read-only/full distinctions, and pricing snapshots survive unchanged.
ONLINE_BOOKING_ONLY: EGP 50 per purchased Practice seat per month; positive chosen count;
  total = seat count * 50; no yearly cycle; no fixed business cap on quantity.
  Allows discovery/online booking and narrowly allowed practice presentation settings.
  No new Reception, Ticket/Queue, Walk-In, clinical work or Doctor revenue features.
All accepted prices, quantities, plan features, dates and currency are snapshotted.
A paid period is activated only after approved manual payment confirmation by SuperAdmin;
no automatic charge, payment gateway or automatic renewal. Moving a Practice does not
cancel/refund the period already purchased. No new proration/refund policy introduced.

02. MULTIPLE SUBSCRIPTIONS / OWNERSHIP
- A Doctor may own multiple independent paid subscription contracts at the same time,
  including contracts with the same product and plan (e.g. two STARTER contracts).
- Each contract has independent paid periods, status, term, seats, history and renewal.
- A renewal creates a new paid period in the SAME contract/series, preserving immutable
  snapshots. Its start is the current period's exclusive end if paid early; if already
  expired, the new paid period starts when activated after payment.
- Do not pool the features of a Doctor's contracts across all Practices.
- Every Practice has at most one CURRENT subscription contract allocation.
- Existing historic allocations are retained after release/move/renewal.

03. PRACTICE CREATION / ALLOCATION
- On creating a new Practice after approval, the Doctor selects an eligible currently
  effective contract with at least one available Practice seat, including an active
  Starter Trial contract. Creation and allocation succeed/fail together.
- A Practice initially remains manually inactive/unpublished until existing branding,
  configuration, logo, availability, publication and other activation prerequisites pass.
- A created Practice consumes its contract's Practice seat even while manually inactive,
  incomplete or unpublished. Manually disabling a Practice does not release its seat.
- Only explicit release/transfer frees a seat. Unallocated Practices cannot receive
  public discovery, booking or general new operational work.
- Preserve the pre-approval single inactive onboarding draft exception already in source;
  it creates no active service or unlimited free Practice capacity.
- If no current seat is available, Doctor buys/activates a new contract or extra eligible
  capacity, or transfers/releases allocations; never silently overbook capacity.

04. TRANSFER AND REASSIGNMENT
- A Doctor may transfer an owned Practice immediately to any other effective contract
  with spare capacity. E.g. Booking Only -> Premium, even before Booking Only expires.
- Transfer is atomic, ends source allocation, creates target allocation and applies
  target feature rights for NEW work. Source contract's purchased period stays paid.
- A Practice's original bookings keep their ORIGINAL granted rights; transfers cannot
  grant a retroactive clinical entitlement to a Booking Only reservation.
- A Practice transferred away from an expired Premium contract must not be reclaimed
  automatically when that Premium contract is renewed.
- Transfers always enforce practice ownership, permissions, capacity and concurrency.

05. TRIAL — FINAL APPROVED RULE (SUPERSEDES AUTOMATIC-START AND REGISTRATION-OFFER RULES)
- STARTER_FREE_TRIAL_20D is an entitlement offered ONCE IN THE DOCTOR'S LIFETIME.
- An approved Doctor who has never started a Starter trial may START IT AT ANY TIME.
  Mere Doctor approval automatically gives eligibility, NOT an active trial period.
- Trial starts at the successful Doctor-initiated Start Free Trial action, not at the
  SuperAdmin's initial Doctor approval date. Duration is 20 days from actual start.
- No new SuperAdmin approval, payment or credit card is needed to start the trial.
- Trial capacity: 1 Practice and 1 active Reception identity; Starter features.
- Buying any paid contract before/during trial does not consume, cancel or curtail
  trial eligibility or active trial duration. Trial and paid contracts remain independent.
- Existing Doctors who have not used their lifetime trial remain eligible, subject to
  Doctor approved/active status and verified trial usage history. No second trial.
- Trial expiry blocks new operations on still-allocated Practices and leaves proven
  historical commitments fulfillable as defined below.
- Preserve existing reasoned SuperAdmin extension of an ACTIVE trial when separately
  authorized; that is not an approval to start a trial and not a second free trial.
- Original trial reminder schedule (5/2/1/0 days) remains in place unless separately
  changed. Trial entitlement is not automatically converted into paid billing.
- Trial activation must be idempotent and concurrency-safe: two start attempts never
  produce two trial subscriptions or reset its original start/end timestamps.

06. DOCTOR / PRACTICE EFFECTIVE ACTIVITY
Effective commercial permission for NEW work depends on all applicable gates:
  Doctor Approved + user Active + authorized user permission/scope + Practice manual
  active/ready/published as needed + current eligible allocated contract + feature
  access level + workflow state/availability conditions.
Expiry of a contract immediately removes its still-allocated Practices from public
search, booking dates/slots and NEW reservations/operations, even if a worker is late.
Practice manual deactivation and commercial expiry are SEPARATE facts.
When the same contract is renewed, its still-associated Practices recover effective
access automatically if all manual, Doctor, configuration and release conditions pass.
A manually disabled Practice never silently becomes manually active on renewal.
No Practice, clinical record, branding, schedule, reservation or history is deleted.

07. RESERVATIONS AND HISTORICAL CLINICAL COMMITMENTS
- Existing CONFIRMED valid reservations created while the Practice had appropriate
  effective feature coverage are NOT automatically cancelled if the contract expires,
  a Practice is transferred, or the subscription is renewed on a smaller plan.
- A proven Full Platform reservation may finish its original allowed lifecycle after
  expiry or transfer: check-in -> Ticket -> Queue -> MedicalEncounter -> diagnosis /
  prescription and applicable linked clinical outputs, per the original rules.
- No historical entitlement can become a blanket permission for new Walk-In, new
  unrelated reservations, arbitrary Ticket/Encounter creation, or other Practices.
- Booking Only reservations do not retroactively gain Full Platform clinical rights.
- Authorization must still validate Doctor/Reception/Patient/Guardian role, permissions,
  active assignment, resource link, clinical relationship, consent and privacy.
- Doctor suspension / user deactivation / clinical safety and original state/cutoff
  policies are not bypassed by a commercial continuation right.
- Historical reads/audits are preserved; changes do not erase medical history.

08. RECEPTION SEATS — APPROVED OPTION A
- Reception profile/ApplicationUser is one Doctor-scoped identity, serving one or more
  of that Doctor's Practices through Practice assignments.
- A distinct ACTIVE Reception identity consumes ONE reception seat in EACH current
  Full Platform contract whose Practices it is actively assigned to serve.
- Two assigned Practices in the same contract consume one seat, not two.
- A Reception serving two different STARTER contracts consumes one seat in EACH.
- No reception seat/operations exist under Booking Only.
- Existing role/permission/active account/Practice assignment checks remain necessary.
- Ending one contract suspends new Reception operations ONLY for its Practices;
  other contract assignments and the Reception account remain intact.

09. TRANSFER WITH RECEPTION CAPACITY — APPROVED OPTION B
- If a Practice transfers to a contract with fewer available Reception seats, the
  Doctor chooses which currently assigned Reception identities can continue to
  operate on the Practice under the target contract, including choosing none.
- Transfer, capacity revalidation, and the active/inactive assignment changes are
  applied together transactionally. No partial transfer or seat overcommit.
- Non-selected users cannot perform NEW operational actions on that Practice.
- Their accounts and historic assignments/operational audit are preserved, and their
  access to other legitimately assigned Practices is unaffected.
- Selecting a Reception who already serves another Practice on the same contract
  does not consume a duplicate seat; selecting one serving other contracts still
  counts separately against each affected contract.
- Target Booking Only carries zero active Reception operational assignments.
- Proven historical visit continuity uses current legitimate scoped actors, and must
  not reactivate terminated employees or bypass authorization to fulfill old visits.

10. RENEWAL, DOWNGRADE AND ALLOCATION SELECTION
- On any relevant renewal, Doctor can see current contract, term, price, seats, active
  Reception usage, current linked Practices and their statuses.
- Booking Only renewal may reduce/increase selected paid seat quantity. Its new total
  is selected positive count * 50 EGP/month; zero is not a paid plan renewal.
- A Full Platform downgrade or capacity reduction applies at the next period start.
- Doctor selects which Practices will keep coverage and which Reception identities
  will remain operational in the new plan/contract capacity.
- All current Practices still receive their old paid features UNTIL the original
  period ends. At the new period start, selected allocations continue; unselected
  excess allocations become commercially uncovered/inactive unless transferred to
  other valid contracts. Accounts, assignments, bookings and history are preserved.
- No blanket refusal because the OLD period had a higher Practice count, provided
  the exact future selections fit the target paid period's capacity.
- Keep original Full Platform immediate same-cycle upgrade policy and actual payment
  difference, and existing scheduled downgrade/cycle-change policy where compatible.
- Explicit seat/Reception reallocation is required at first post-migration renewal
  for any noncompliant grandfathered contract; no silent capacity overrun.

11. PAID REMINDERS — APPROVED
- For EACH individual paid contract period, notify Doctor at 7 days, 3 days, 1 day
  BEFORE period end, and at the exact end if it expires without an already confirmed
  paid succeeding period.
- Include product/plan, end time, linked Practice count/names, and computed eligible
  renewal price; show expiry status in Doctor Dashboard.
- Durable Email Outbox, unique idempotency/event keys, retry-safe scheduling.
- If a paid renewal for the next period is fully recorded, cancel/suppress stale
  expiry-risk reminders. Do not present an already secured paid period as uncovered.
- No automatic renewal, payment collection or unexpected subscription restart.

12. LEGACY MIGRATION — APPROVED
- Do not delete/disable currently operational Practices unexpectedly at deployment.
- Snapshot existing contract/practice/reception/booking state; backfill provenance and
  grandfathered allocations where usage exceeds the existing paid capacity.
- A Doctor with e.g. four historical Practices attached to a STARTER capacity of one
  retains all four during controlled migration grace, but cannot create a fifth using
  that exception. Same safety principle applies to existing Reception assignments.
- At the FIRST post-migration renewal, require valid explicit capacity/allocations.
  Unselected excess Practices become uncovered; do not delete data, bookings or staff.
- Do not give expired/unpaid historic Doctors an ACTIVE paid or free subscription
  automatically. However, independently verify unused lifetime Trial eligibility;
  available means not started and does not imply a running commercial period.
- For pre-existing reservations, recover original entitlement provenance where
  provable; otherwise flag exception for controlled remediation and do not invent
  extra clinical rights, cancel or erase patient records.
- Before release: backup, non-production dry run, data reconciliation, controlled
  cut-over, rollback rehearsal and tests. No irreversible silent change to records.

13. CONCURRENCY / CONSISTENCY OUTCOMES — BUSINESS INVARIANTS
- There cannot be two simultaneous current contract allocations for one Practice.
- No operation may consume more seats than an eligible current contract has, except
  the explicitly preserved preexisting grandfathered state, which cannot grow.
- Concurrent transfer/renewal/expiry/Practice creation must resolve consistently;
  recheck status, ownership, capacity and effective period at transactional write time.
- New reservation requires eligible current commercial coverage at creation, not
  merely a search result or stale cached listing.
- Effective expiry is time-based (exclusive end); worker delays cannot produce rights.
- Concurrent trial starts produce exactly one lifetime trial and one initial start time.
- Payment retries never duplicate charges/periods; historical rights cannot be
  granted retroactively or cross-Practice because of a concurrent allocation change.

14. ACTORS / SCOPE
Doctor: administers own contracts, requests/submits allocations/renewals, and manages
  own permitted Practices and Reception users; can start unused Trial when Approved.
SuperAdmin: Doctor approval/suspension, permitted manual SaaS payment/renewal approval,
  catalog/governance, existing supervised trial extension.
Reception: only active assigned Practice operations and accepted historical visit
  continuation when authorized; no Doctor subscription financial/revenue rights.
Patient/Guardian: normal own reservation and medical access/privacy rules persist;
  cannot acquire staff or commercial entitlements through this model.

15. WHAT THIS DOCUMENT DOES NOT AUTHORIZE
- No automatic payments/renewals, card or wallet gateway, new refund/proration rules,
  plan price changes, clinical privacy overrides, cross-Doctor Reception assignments,
  automatic published/manual-activity changes or uncontrolled patient record edits.
- Engineering choices (SubscriptionContract table name, API URL shape, SQL locking,
  exact query topology and migration implementation) are DESIGN PROPOSALS for Codex,
  not new commercial approvals; enforce the approved outcomes above.
- Release scheduling, ownership, rollback signoff and live configuration are DEPLOYMENT
  gates, not reasons to keep the agreed commercial Business Model open.

ACCEPTANCE SCENARIOS — MUST BE PRESERVED
S1. Doctor approved today -> Trial Available, not running -> starts after 1 year -> 20 days.
S2. Doctor with active paid Premium starts unused Trial; neither subscription changes.
S3. Two STARTER contracts cover one separate Practice each; no shared entitlement leak.
S4. Booking Only quantity 5 costs 250 EGP/month, only 3 Practices assigned, two free seats.
S5. Booking Only -> Premium move to spare seat, no refund; new work gets Premium only.
S6. Premium expires, its Practices vanish; a different Booking Only Practice stays live.
S7. Doctor renews expired Premium; remaining associated manually active Practices return;
    moved-away and manually disabled Practices do not auto-activate/rebind.
S8. Premium -> Starter downgrade selects 1 Practice + eligible Reception; excess cease
    new work only when the future period starts; old confirmed visits can continue.
S9. Reception assigned twice within one contract counts once; across two counts twice.
S10. Transfer Clinic A from Premium (Ahmed, Sara) to Starter (one Reception seat):
     select Ahmed; Sara loses new operations there but remains elsewhere; history kept.
S11. Full Platform reservation before expiry/transfer can finish clinical visit after;
     newly booked Booking Only reservation cannot borrow old Full permissions.
S12. Legacy 4 Practices on 1-seat STARTER preserved at migration, cannot create fifth;
     explicit compliant first renewal required.
S13. Parallel transfers competing for one remaining seat: only one succeeds.
S14. Same Doctor clicks Start Free Trial twice: exactly one started Trial.
S15. Paid reminders at -7/-3/-1/0 are per contract; confirmed paid successor suppresses stale notices.

BUSINESS SIGN-OFF: COMPLETE for the above subscription feature scope.
NEXT STEP: review/commit this new addendum as approved commercial Source of Truth,
then prepare the full Codex implementation task against the repository's current main.

```

<details><summary>Historical v1.0 — superseded commercial rules</summary>

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

## Authorization clarification — PR #36 hardening, 2026-11-03

The approved plan matrix, prices, capacities, trial duration, and immutable accepted terms above are unchanged. The existing Doctor/Reception Ticket archive permissions require `TICKET_QUEUE` ReadOnly; the corresponding Reservation archive permissions require `RESERVATION_MANAGEMENT` ReadOnly. Unknown permissions and missing expected mappings cannot bypass commercial authorization. Explicit account/onboarding, patient, privacy/relationship, platform-governance, and system permissions are reviewed separately; a subscription never grants those rights.

Current entitled reads remain subject to existing ownership and relationship rules. An exception based on past accepted archive entitlements is tied to the original owned resource or a filtered owned collection. Paid/trial coverage uses inclusive start and exclusive end. Legacy transition coverage can include older owned records and ends at expiry or paid supersession. Filters precede pagination and cursor construction. Past Full Platform coverage does not give an active Booking Only account general Ticket/Queue access or new operational work.

Reservation capability responses must reflect actual current authorization. ReadOnly Reservation Management does not advertise cancellation, rescheduling, restoration, or unrelated Ticket navigation. Existing lifecycle/cutoff rules and narrowly proven operational continuity remain authoritative. Patient/Guardian rights and SuperAdmin governance remain outside Doctor subscription prerequisites. Prescription PDF export and Reception account-state operations use their existing export and Reception Management features respectively.

</details>
