# Wasla

**Wasla** is a healthcare platform designed to connect Patients, Doctors, Reception teams, and Platform Administration in one unified system.

The long-term goal is to support the complete outpatient healthcare journey:

**Doctor Discovery → Reservation → Check-In → Queue → Medical Encounter → Prescription / Lab / Radiology → Longitudinal Patient Medical Record**

---

## Project Status

**Current Stage:** Technical Bootstrap / Pre-Implementation

The product implementation has not started yet.

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

Owns the operational doctor scope and manages profile, configuration, schedule, reception users, reservations, queue operations, clinical encounters, and permitted financial visibility.

### Reception

Operates within an assigned Doctor scope and handles patient registration/search, reservations, check-in, tickets, queue operations, and payment recording.

### Patient

A platform-global patient profile that is not owned by one Doctor and can accumulate a longitudinal medical record across permitted healthcare encounters.

---

## Core Domain Principles

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
