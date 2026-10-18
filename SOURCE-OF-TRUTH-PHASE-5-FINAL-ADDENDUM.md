# Phase 5 final approved business addendum

Approval date: **2026-10-18**. Authority: the owner's complete Phase 5 Final
Closure implementation task supplied in this chat. This addendum is binding for
the business changes below. Implementation and acceptance status are tracked
separately in `PHASE-5-FINAL-CLOSURE-REPORT.md`.

The master v1.2 document is not present in the checkout. This file does not claim
to edit it. Suggested master amendment: replace Phase 5's status only after the
closure report's entire Definition of Done passes; adopt the following changes.

| Decisions | Earlier rule → approved rule | Rationale / impact |
|---|---|---|
| P5-01–07 | Basic only → Basic without login plus verified adult Reception Full invitation | Preserve global Patient identity; staff never handles passwords |
| P5-08–15 | Heuristic matching → exact normalized email collision plus soft phone/demographic signals | No automatic binding or medical merge; attested distinct-person review |
| P5-09 original | **SUPERSEDED:** nonunique Patient.Email → P5-D01.1 nullable normalized unique identity email | One exact email identifies at most one Patient |
| P5-16–23 | Direct account creation for existing Basic → verified email, independent evidence, granular SuperAdmin review, approved invitation, atomic activation | Email control alone does not prove patient identity |
| P5-23 original | **SUPERSEDED:** multiple Basic Patients with same exact identity email → P5-D01.1 unique identity email | Multiple soft matches via shared phone/name remain possible |
| P5-24–29 | Unverified onboarding → 10m OTP, 5 attempts, 60s cooldown, 30m purpose-bound continuation, 48h one-use invitation | Transactional outbox, replay protection, private evidence, neutral public start |
| P5-30–34 | Incomplete closure → scoped reuse, classification, auditable retention, exact contracts and regression evidence | These IDs adopt the task's grouped closure requirements; no missing original decision wording is invented |
| P5-D01.1 | Nonunique identity email → optional globally unique normalized Patient.Email; nonunique Patient.PhoneNumber and separate nonunique PatientContact.ContactEmail | Preserve shared family contact information without account authority |
| P5-D01.2 | Historical cleanup assumption → disposable preproduction, additive migration, fail on incompatible rows | No implicit reset, deletion, merge or historical ID rewriting |
| P5-D01 | Duplicate detection → classification only, medical merge outside V1 | No combined clinical access or moved records |
| P5-D02 | Mixed account contexts → one operational UserType per ApplicationUser; separate unique emails/credentials | Preserve relational UserRole model and existing permissions |
| P5-D03 | Unspecified retention → 90d inactivity, evidence 90d after closure, audit 24 months after closure, durable legal hold | Sensitive purge defaults off; production legal/privacy approval remains a release gate |

DEC-020's Reception name/DOB/gender plus patient-or-primary-contact phone minimum
and DEC-021's real Family aggregate remain unchanged. Contact/Claim never grants
guardian medical access. No standalone login under 16; 16–17 requires an existing
Basic Patient and manual Claim review. Direct Full/self new-account creation is
restricted to adults. Existing Phase 16/18 source addenda remain authoritative
within their clinical/reporting boundaries.

Implementation traceability: Patient/Contact/AccountLink and identity aggregates;
registration, matching, onboarding, claim, duplicate, reservation and Walk-In
features; E01–E06 and exactly N01–N20 in the API handoff. Acceptance tests must
cover email constraints, permission ID stability, state transitions, atomic
activation, private evidence, scoped reuse, retention and cross-phase regression.

## Owner execution clarifications (2026-10-18)

1. Preserve the existing Walk-In path `/api/v1/practices/{practiceId}/tickets/walk-in`; correct the task's mistaken `/reception/...` reference. No route move or alias.
2. N04 issues Claim-scoped proof when a verified Claim email has no exact Patient match/reference. N06 determines intake eligibility from demographics and independent evidence. This grants no Patient identity or access.

Decision traceability and validation are in the E/N route inventory, security matrix, migration plan and closure report. Existing phases' source-of-truth addenda are not overwritten.

## Verified implementation closure

Phase 5 COMPLETED on 2026-10-18, verified source d79658a568dbfc4273b9b9dc1bab695043d316ab and [CI 39160421337](https://github.com/LoaiSaberC4r/Wasla/actions/runs/39160421337),922 passed / 0 failed / 0 skipped. Integrate this exact status amendment into the unavailable Master v1.2; this task did not edit that unseen file. Production legal/privacy and deployment gates remain separate. See the final closure report for the full evidence and prior-baseline resolutions.
