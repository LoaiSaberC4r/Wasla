# Wasla

**Wasla** is a healthcare platform designed to connect Patients, Doctors, Reception teams, and Platform Administration in one unified system.

The long-term goal is to support the complete outpatient healthcare journey:

**Doctor Discovery → Reservation → Check-In → Queue → Medical Encounter → Prescription / Lab / Radiology → Longitudinal Patient Medical Record**

---

## Project Status

**Current Stage:** Doctor Practices Operational Foundation

Identity, permission-based authorization, authentication/password recovery,
Doctor and Patient self-registration, Doctor approval governance, Root
SuperAdmin governance, localized errors, private verification media, and the
durable email outbox are implemented.

The operational model is practice-scoped: one Doctor can own multiple
`DoctorPractice` records, each with independent location, configuration,
branding/logo, schedule and exceptions, segments, visit types, pricing, and
Reception assignments.

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
    + Reception role/relevant permission
    + active ReceptionPracticeAssignment
    + assignment-specific permission
```

Selecting a current practice in a client never grants access by itself.

`Normal`/`VIP` are Segments (and own queue priority); `NewConsultation`/`FollowUp`
are Visit Types (and own visit duration). Prices are defined by the complete
Practice + Segment + Visit Type tuple. Future Reservations must snapshot these
names, priority, and price so later catalog changes do not rewrite history.

Follow-up eligibility remains intentionally deferred until Medical Encounters
exist. The confirmed rule is: only a completed encounter can let the Doctor open
a time-limited, single-use eligibility, progressing through Available → Reserved
→ Completed → Consumed. Prior attendance alone never grants unlimited follow-up
access.

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

1. `Healthcare-Platform-Master-Source-of-Truth-v1.0`
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
