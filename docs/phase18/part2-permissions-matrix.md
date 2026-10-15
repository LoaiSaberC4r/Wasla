# Permissions and privacy

Every protected read requires current database permission, eligible account/role/profile and the exact resource scope. Cached claims alone do not prove authorization. First-login/inactive/suspended accounts are denied under existing guards. Doctor archive/audit scopes include owned historically inactive Practices. Reception requires the current active assignment and exact delegated permission; revocation applies on the next request. Patient Ticket archive remains self-only; dependent Reservations require the existing specific Parent/minor-child or Legal Guardian policy and dependent-view grant in addition to archive-own.

| Actor | Additive defaults | Boundary |
|---|---|---|
| Patient | Archives.Tickets.ViewOwn; Archives.Reservations.ViewOwn | Linked self Patient identity; no dependent Ticket grant |
| Doctor | Archives.Tickets.ViewPractice; Archives.Reservations.ViewPractice; Audit.Events.ViewPractice; Audit.SensitiveAccess.ViewPracticeMetadata | Current approved owner; clinical/finance rights independent |
| Reception | Archives.Tickets.ViewAssigned; Archives.Reservations.ViewAssigned; Audit.Events.ViewOwnActivity | Global **and** current exact assignment delegation; audit own activity only |
| SuperAdmin | Archives.Tickets.ViewPlatformMetadata; Archives.Reservations.ViewPlatformMetadata; Archives.Operations.ViewHealth/ViewFailures/ViewRuns/Pause/Resume; Audit.Events.ViewPlatform | Safe operational metadata, no patient identities/clinical/transaction private data |
| Explicit restricted admin | Audit.SensitiveAccess.ViewRestrictedMetadata | Not seeded to any role; no Root bypass; reason and immutable access evidence required |
| Catalog managers | None | Static neutral vocabulary only |

The 18 codes append after the original PermissionNames.All prefix, preserving position-based permission GUIDs. Only the three Reception codes enter the assignment allowlist. Existing assignments receive none automatically. Existing Founder protection and Patient MedicalRecords.ViewOwnAccessLog remain unchanged.

ViewHealth is the documented prerequisite for Pause/Resume so the caller can obtain rowVersion. Idempotency replay still checks current action permission. An archive read does not grant Refund, payment correction or clinical access. Dedicated platform archive projections omit PatientId/name/phone, BookingNote, reason text, price/transaction/clinical data. Safe audit masks Patient actor identity.
