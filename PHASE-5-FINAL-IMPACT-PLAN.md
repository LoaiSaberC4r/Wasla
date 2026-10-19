# Phase 5 final closure impact and execution order

Base: `main` / `origin/main` **17c9e0b96af87de0143ca4ac4978bc970ca6f9dd**.
Branch: `codex/phase5-final-closure`. Approved task received 2026-10-18.

1. Approved addendum and inventory (this document).
2. Domain/schema: normalized email, optional ContactEmail, immutable link source,
   challenge, claim/evidence/history, invitation, duplicate case and attestation.
3. Persistence: additive migration, computed-email uniqueness, restrictive FKs,
   rowversion, bounded indexed reads and consistent transaction-owned locks.
4. Application: matching, Basic/Full, verified self registration, Claim review,
   activation, email change, reservation/Walk-In reuse.
5. Auth/scoped permissions: preserve existing IDs; append ten grants after all
   Phase 18 entries; disable pending login and old password paths as applicable.
6. API: E01–E06, N01–N20 only; preserve legacy booking contracts additively.
7. Media/outbox/retention: existing services and transaction pipeline, compensation,
   private access audit, durable tombstones and privileged legal-hold operations.
8. Unit, hosted API, native SQL, architecture and full regression verification.
9. API/security/database/frontend documentation and evidence-based closure report.

Inventory inspected: existing Patient/PatientContact/PatientAccountLink,
ApplicationUser and UserRole; PatientManagement, Registration, Authentication,
PasswordRecovery; IWaslaDataStore/WaslaDataStore/ProfileConfigurations/DbContext;
SystemSecurity and seeder; Auth/Patients/Families/Reservations/Tickets controllers;
BuildingBlock transaction behavior and EF transaction manager; email outbox and
existing retained-reference media compensation pattern; all existing tests and CI.
Additional usages are checked before changing their respective integration paths.

Risk areas: identity email notification recipients, disabled Full password reset,
existing API registration test setup, SQL Server filtered computed-column indexes,
SQLite rowversion/computed-column adaptation, request idempotency, failure-result
rollback of OTP attempt counters, file storage after uncertain SQL commit,
cross-practice reuse without inventing Patient ownership, and Phase 16/18 baseline
failures. No medical/Family state machine, historical PatientId or existing
migration is to be rewritten. No database reset is authorized by this task run.

Baseline command: `dotnet test Wasla.sln --configuration Release --no-restore
--logger trx --results-directory TestResults/phase5-final-closure-baseline -v minimal`.
Native SQL baseline will use local SQLEXPRESS with integrated security and only
the existing isolated fixture databases; no development database is reset.
