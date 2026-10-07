# Wasla

**Wasla** is a healthcare platform designed to connect Patients, Doctors, Reception teams, and Platform Administration in one unified system.

The long-term goal is to support the complete outpatient healthcare journey:

**Doctor Discovery → Reservation → Check-In → Queue → Medical Encounter → Prescription / Lab / Radiology → Longitudinal Patient Medical Record**

---

## Project Status

**Current Stage:** Phase 15 — Diagnostic Orders & Results — DONE

Identity, permission-based authorization, authentication/password recovery,
Doctor and Patient self-registration, Doctor approval governance, Root
SuperAdmin governance, localized errors, private verification media, and the
durable email outbox are implemented.

The operational model is practice-scoped: one Doctor can own multiple
`DoctorPractice` records, each with independent location, configuration,
branding/logo, schedule and exceptions, segments, visit types, pricing, and
Reception assignments. Public discovery now exposes automatically eligible
Doctors, all active Practices, exact base consultation pricing, Doctor-owned Bio
and Qualifications, and Practice-local availability. Phase 10 adds confirmed
Reservation booking, lifecycle, history, capacity protection, idempotency, and
actor-scoped operations. Phase 11 adds paid Ticket admission and the practice
queue. Phase 12 records EGP Payments and full Refunds as separate auditable
transactions, supports financial corrections and receipt-ready data, and
exposes scoped finance views and Doctor revenue datasets. Phase 13 adds separate
clinical encounters, notes, diagnoses, immutable completed-record amendments,
patient-owned completed views, and single-use follow-up eligibility integrated
with patient/reception reservations, paid admission and Walk-In. Start and
Complete Visit commit clinical and operational state atomically.

Phase 14 adds Root-managed DrugCatalogManager accounts, a central drug catalog
with controlled offline imports and medication request review, separate
prescriptions with drafts and immutable versions, correction/discard/void,
patient-owned completed outputs, and atomic prescription finalization during
Complete Visit. Catalog search is server-side; missing medication can be
requested and added in one call without waiting for catalog review.

Phase 15 adds Root-managed MedicalCatalogManager accounts, separate LOINC Lab
and LOINC/RSNA Radiology catalogs with reviewed offline imports, missing-catalog
request review, encounter drafts and post-visit diagnostic orders, and atomic
publication during Complete Visit. Patients submit their own private medical
documents for the requesting Doctor to review; only Doctor acceptance or direct
recording creates an official result. Document results can cover multiple items
and support immutable correction/void history and scoped private downloads.

Final Phase 15 verification, including the real LOINC hotfix: **210 unit, 151
integration and 20 architecture tests passed (381 total; zero failures/skips)**,
including **119 focused Phase 15 tests**; the solution builds with **zero
warnings/errors**. SQL Server tests
verify concurrency, transaction rollback and private-file compensation.
The supplied local official LOINC 2.83 ZIP has passed both parsers and authenticated
SQL Server Preview, full development/local Apply and persisted catalog verification
for both catalogs: **47,977 Lab concepts (43,465 active from prior verification;
1,192 with official Arabic)** and **7,016 Radiology procedures (6,941 active)**. Canonical
table selection uses its exact path, the upload boundary uses a stream, and
`_local-data/` remains ignored. API contracts and clinical rules are unchanged.
[API changes](PHASE-15-API-CHANGES.md), [closure report](PHASE-15-CLOSURE-REPORT.md),
[source-of-truth addendum](SOURCE-OF-TRUTH-PHASE-15-ADDENDUM.md) and
[terminology source/license notice](LOINC-SOURCE-NOTICE.md) record the contracts,
decisions, completed development full-apply verification and remaining production
deployment/performance acceptance. Production Apply was not performed; production
performance/load acceptance remains pending and deployment-specific.

Phase 14 frontend contracts, decisions and verification (130 unit, 116
integration and 16 architecture tests passed; zero build warnings/errors):
[API changes](PHASE-14-API-CHANGES.md), [closure report](PHASE-14-CLOSURE-REPORT.md),
and [source-of-truth implementation addendum](SOURCE-OF-TRUTH-PHASE-14-ADDENDUM.md).

Phase 13 frontend contracts, verification and decision implementation status:
[API changes](PHASE-13-API-CHANGES.md), [closure report](PHASE-13-CLOSURE-REPORT.md),
and [source-of-truth implementation addendum](SOURCE-OF-TRUTH-PHASE-13-ADDENDUM.md).

The initial repository baseline is based on the reusable technical foundation from:

- `.NET 10`
- `C# 14`
- Clean Architecture
- CQRS / MediatR
- FluentValidation
- Result Pattern
- Specification Pattern
- Repository + Unit of Work
- SQL Server / EF Core
- Optimistic Concurrency
- Domain Events
- Transaction Pipeline
- Outbox Pattern
- Auditing
- Soft Delete
- ProblemDetails
- Localization
- Serilog / Correlation / Log Redaction
- Health Checks
- API Versioning
- xUnit testing

---

## Solution Structure

```text
Wasla
│
├── src
│   ├── BuildingBlock
│   │   ├── BuildingBlock.Domain
│   │   ├── BuildingBlock.Application
│   │   ├── BuildingBlock.Infrastructure
│   │   ├── BuildingBlock.Infrastructure.EntityFrameworkCore.SqlServer
│   │   └── BuildingBlock.Api
│   │
│   └── Wasla
│       ├── Wasla.Domain
│       ├── Wasla.Application
│       ├── Wasla.Infrastructure
│       ├── Wasla.Infrastructure.EntityFrameworkCore.SqlServer
│       └── Wasla.Api
│
├── tests
│   ├── Wasla.Tests.Unit
│   ├── Wasla.Tests.Integration
│   └── Wasla.Tests.Architecture
│
├── Wasla.sln
└── BuildingBlock.sln
```

---

## Core Product Actors

### SuperAdmin

Platform-level authority responsible for governance, doctor approval, security administration, permissions, and reference data.

### Doctor

Owns one or more operational practices and manages their configuration, schedule, reception users, reservations, queue operations, clinical encounters, and permitted financial visibility.

### Reception

Operates only within explicitly assigned Doctor Practices and handles the capabilities granted independently on each assignment.

### Patient

A platform-global patient profile that is not owned by one Doctor and can accumulate a longitudinal medical record across permitted healthcare encounters.

### DrugCatalogManager

A Root-managed platform account that maintains medication reference data,
reviews offline imports and Doctor medication requests, and has no access to
patient clinical records or prescriptions.

### MedicalCatalogManager

A Root-managed platform account that maintains Lab and Radiology reference
catalogs, reviews offline LOINC/RSNA imports and Doctor catalog requests, and has
no authority over patient diagnostic orders/results or medication reference data.

---

## Core Domain Principles

### Doctor Owns Many Practices

`Doctor 1 ── * DoctorPractice` is the source-of-truth ownership model. Operational
settings must not be attached directly to `Doctor` because each physical practice
can have a different location, theme, schedule, availability, segment catalog,
pricing, and Reception team.

Practices are created inactive. Activation requires complete practice/location
details, configuration, branding, and a logo; a schedule is deliberately not an
activation prerequisite. Therefore `Active` is not synonymous with `Bookable`:
bookability also requires online booking to be enabled, an effective schedule,
and available capacity.

Reception authorization is always server-side and practice-scoped:

```text
Authenticated user
    + active Reception identity
    + Approved Doctor with active account
    + active Practice owned by that Doctor
    + active ReceptionPracticeAssignment
    + global role permission
    + assignment-specific permission
```

Selecting a current practice in a client never grants access by itself.

`Normal`/`VIP` are Segments (and own queue priority); `NewConsultation`/`FollowUp`
are Visit Types. Appointment duration comes only from the effective schedule
period's `SlotDurationMinutes`; a Visit Type has no duration. Prices are defined
by the complete Practice + Segment + Visit Type tuple. Future Reservations must
snapshot these names, priority, and price so later catalog changes do not rewrite
history.

Follow-up eligibility remains intentionally deferred until Medical Encounters
exist. The confirmed rule is: only a completed encounter can let the Doctor open
a time-limited, single-use eligibility, progressing through Available → Reserved
→ Completed → Consumed. Prior attendance alone never grants unlimited follow-up
access.

### Public Discovery

Anonymous Phase 9 reads are available at:

- `GET /api/v1/public/specializations`
- `GET /api/v1/public/doctors`
- `GET /api/v1/public/doctors/{doctorId}`
- `GET /api/v1/public/doctors/{doctorId}/profile-image`
- `GET /api/v1/public/practices/{practiceId}/logo`
- `GET /api/v1/public/practices/{practiceId}/available-dates`
- `GET /api/v1/public/practices/{practiceId}/available-slots?date=...`
- `GET /api/v1/public/practices/{practiceId}/booking-options?date=...&time=...`

`PublicSearchPrice` always means the active Normal Segment plus active
NewConsultation Visit Type price. Availability includes only currently available
slots across a 30-day horizon (Practice-local today plus 29 days). Returned date,
time and `IsToday` values use the Practice's configured timezone. FollowUp is
withheld from anonymous booking options until real clinical eligibility exists.

### Patient is Platform-Global

A Patient does not belong to one Doctor.

Doctor/patient relationships are established through business interactions such as Reservations, Tickets, and Medical Encounters.

### Reservation and Ticket are Different Concepts

```text
Reservation = Planned appointment
Ticket      = Actual attendance / queue participation
```

### Ticket and Medical Encounter are Different Concepts

```text
Ticket             = Operational queue lifecycle
Medical Encounter  = Clinical visit lifecycle
```

Clinical data must not be modeled as queue data.

### Permission-Based Authorization

Authorization is based on more than roles:

```text
Authentication
    +
Permission
    +
Actor Status
    +
Resource Scope
    +
Business Relationship / Policy
    +
Resource State
```

### Outbox Pattern

External notifications related to committed business state should use a durable Outbox mechanism.

```text
Business Change
      +
Outbox Message
      ↓
Atomic Commit
      ↓
Background Delivery / Retry
```

---

## Initial Roles

The initial system roles are:

- `SuperAdmin`
- `Doctor`
- `Reception`
- `Patient`
- `DrugCatalogManager`
- `MedicalCatalogManager`

Roles are permission bundles. Business authorization should prefer granular permissions and explicit scope validation over role-only checks.

---

## Implementation Order

Development follows an explicit dependency order:

1. Technical Bootstrap
2. Identity / Users / Roles / Permissions / Seeding
3. Authentication & Password Lifecycle
4. SuperAdmin & Core Reference Data
5. Doctor Registration / Verification / Approval
6. Patient Registration & Profile
7. Doctor Configuration / Branding
8. Doctor Schedule / Availability
9. Doctor Segments / Priority / Quota / Pricing
10. Reception Management
11. Doctor Discovery
12. Reservation Lifecycle
13. Ticket / Queue / History / Walk-In
14. Payments / Doctor Revenue
15. Medical Encounter
16. Diagnosis / Prescription
17. Lab & Radiology
18. Central Patient Medical Record
19. Notifications / Outbox Expansion
20. Archiving / Reports / Dashboards / Improvements

Business and domain decisions must be resolved before implementation proceeds into dependent phases.

---

## Engineering Principles

- Business rules come before API design.
- Domain logic should not depend on Infrastructure.
- Expected business failures should use the Result pattern where appropriate.
- Reusable query logic should use Specifications.
- Transactions should be explicit where consistency matters.
- Sensitive mutations must be auditable.
- Concurrent state changes should use optimistic concurrency where required.
- Medical data access must be scoped and auditable.
- External side effects should not compromise committed business transactions.
- Database migrations in Production should be controlled deployment operations.

---

## Source of Truth

The project follows an explicit source-priority model:

1. `Healthcare-Platform-Master-Source-of-Truth-v1.2-Updated` (including DEC-056–069)
2. Approved decision/change log
3. `Healthcare-Platform-Business-Blueprint`
4. `BuildingBlockWithNET10`
5. QControl as an implementation/business-pattern reference
6. Business-thinking references and validated external research

If sources conflict, the latest approved rule in the Master Source of Truth wins.

---

## Technology

- **Runtime:** .NET 10
- **Language:** C# 14
- **API:** ASP.NET Core Web API
- **Persistence:** Entity Framework Core + SQL Server
- **Architecture:** Clean Architecture
- **Application Pattern:** CQRS + MediatR
- **Validation:** FluentValidation
- **Testing:** xUnit
- **Logging:** Serilog
- **API Errors:** ProblemDetails
- **Localization:** Arabic / English baseline

---

## Repository Bootstrap

This repository is initialized from the reusable `BuildingBlockWithNET10` technical foundation.

The reusable `BuildingBlock.*` projects remain product-agnostic.

The original sample product namespace and projects are renamed to:

```text
Wasla.*
```

without changing the underlying technical behavior during the bootstrap step.

## Required Local Secrets

The API intentionally fails startup when JWT and password-recovery secrets are
missing. Configure them with user secrets, environment variables, or a local
`.env` file (never commit real values):

```text
Jwt__SigningKey=<at-least-32-characters>
PasswordReset__HmacSecret=<at-least-32-characters>
```

Root SuperAdmin credentials are required only when
`DatabaseInitialization__ApplySeedingOnStartup=true`; the required keys are
listed in `.env.example`. Production defaults keep both migrations and seeding
disabled. The configured `EmailBranding__FooterImageUrl` must be an absolute
HTTP(S) URL. Place the approved brand image at
`src/Wasla/Wasla.Api/wwwroot/email-assets/wasla-email-footer.png` before
deployment.

---

## Development Rule

Do not implement a technical feature only because it existed in a previous system.

Every major capability should answer:

```text
What problem does it solve?
Who benefits?
What outcome does it create?
What business rules govern it?
What needs validation?
```

Then:

```text
Business Capability
    ↓
Domain Model
    ↓
Architecture
    ↓
API
    ↓
Implementation
```

---

## License

License and distribution terms are to be defined before public release.
