> Historical implementation checkpoint. Superseded by [CLOSURE-REPORT-2026-11-08.md](CLOSURE-REPORT-2026-11-08.md) and [owner decision v2](DECISION-2026-11-08-v2.md). Earlier disabled-feature and missing-policy statements describe that checkpoint, not the current Production target.

# Family governance implementation checkpoint

Work in progress. These additions are being compiled and tested separately from the already running extension regression suite. They are not deployed or released.

Identity correction preserves the global PatientId, linked records, contacts and immutable old/proposed identity snapshots. A currently entitled Father or Mother submits evidence. An authorized SuperAdmin reviews with rowversion and an idempotency key. Duplicate identities, active claims or legal holds, linked accounts, identity email, an adult proposed identity, or a change across the medical age boundary are referred to PendingGovernanceDecision instead of changing Patient. No general transfer or merge action exists. A requested revision retains prior evidence and snapshots.

Different active families now produce a private membership-conflict case after opaque candidate validation. The applicant receives only a case reference and safe next action. Existing memberships and authorities remain intact. A reviewer can reject or refer the case to governance; approval cannot transfer a person.

A current family manager with an exact operational pair can issue a bounded one-use invitation for the missing Father/Mother role. The recipient must be a linked adult Patient and supply independent evidence. The 256-bit reference is stored only as a hash plus a Data Protection encrypted replay value. Keys must be shared and protected across nodes. Invitations expire after 30 minutes, are actor-bound on redemption, are limited per issuer, and are checked again against issuer authority and current membership. Redemption opens a reviewed request or private conflict case; it never grants access.

New routes:

- POST /api/v1/families/mine/dependents/{childPatientId}/identity-corrections (multipart proposed identity, reason, evidence/documentTypes, Idempotency-Key; optional caseId + rowVersion for requested revision).
- GET /api/v1/families/mine/governance-cases and /{caseId} (applicant-only minimal status; no snapshots, reviewer notes or third-party roster).
- GET /api/v1/admin/family-governance-cases and /{caseId}; evidence/{evidenceId}/content; POST approve, reject, request-modification, refer-governance.
- POST /api/v1/families/mine/join-invitations (childPatientId, intendedParentRole, Idempotency-Key).
- POST /api/v1/families/mine/join-requests (multipart invitationReference, intendedParentRole, evidence/documentTypes, Idempotency-Key).

The candidate submission response is now minimal: requestId, status, conflictCaseId, nextAction. Exactly one resource reference is present. Existing normal relationship request read endpoints retain their safe contract. The new candidate endpoint is introduced by this branch and has no previously released client contract.

Three append-only Patient permissions follow the previous 368 names: DependentIdentityCorrection.RequestOwn, FamilyJoinInvitations.CreateOwn, FamilyJoinInvitations.RedeemOwn. Existing SuperAdmin family review permissions authorize governance review; they convey no medical document access.

Generated migration: 20261108022505_FamilyGovernanceAndJoinInvitations (six new tables and the private candidate-case reference, restrictive foreign keys, forward-only history retention). Build passed with zero warnings/errors. SQLite preflight passed eight cases with three SQL variants explicitly skipped. SQL execution, populated upgrade/restore, exact birthday/archive tests and final integrated verification remain pending.

Invitation revocation: POST /families/mine/join-invitations/revoke with invitationReference and Idempotency-Key. Only the issuing adult may revoke; a consumed invitation cannot be reopened. Conflict cases support POST /families/mine/governance-cases/{caseId}/resubmit-conflict with fresh evidence and rowVersion after requested modification.

The workspace includes independent capabilityReasons keyed by each can* field. Medical unavailability never removes a valid operational grant. An exact current proof is checked before advertising verification/correction actions.
