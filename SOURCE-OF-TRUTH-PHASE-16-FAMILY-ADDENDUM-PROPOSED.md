# Proposed Phase 16 family addendum — formal adoption pending

Historical proposal. The all-adult medical-eligibility interpretation below is
superseded by the owner's [2026-10-24 approved business decisions](SOURCE-OF-TRUTH-PHASE-16-FINAL-BUSINESS-ADDENDUM.md).
Full Master incorporation and independent legal/clinical/release approval remain pending.

The [final closure report](PHASE-16-CLOSURE-REPORT.md) records verification and
base Guardian flag enablement. Production/Staging remain disabled and base
policy remains unapproved; the final sentence below describes original defaults.

Product decisions 01–28 in the supplied Family Medical Access task v1.0 are
approved by the user. This document records implementation intent, not legal
consent, Master adoption or production authorization.

Family membership and medical eligibility are independent. Verified Father,
Mother, Guardian and LegalGuardian may be evaluated for a verified Child under
16 in the same active Family. Guardian and LegalGuardian remain distinct.
Clinical authorization additionally requires a reviewed adult-child eligibility,
current approved policy version, current permission/account/link and no active
restriction. Booking retains its separate existing under-18 predicate.

Relatives submit restriction cases against another adult and restoration cases
for themselves. Pending cases do not change access. Permissioned SuperAdmin
review is metadata-only: Pending → Approved/Rejected/ModificationRequested;
ModificationRequested → Pending on resubmission. Applicants may withdraw either
open state. Evidence revisions and histories remain immutable. One open case per
pair/type and one active restriction per pair are enforced in SQL. Restoration
only lifts the exact restriction and never grants an entitlement.

Existing verified pairs receive Pending eligibility candidates, never automatic
Approved grants. Clinical projection is the existing patient-safe record; private
notes, amendments, internal diagnosis notes and storage keys remain excluded.
Every new read/download checks current membership, age, grant and restriction.

OPEN for formal signoff: jurisdiction and effective timezone, acceptable proof
and legal basis references, consent/minor confidentiality categories, age
transition rights, appeals/urgent exceptions, evidence retention/encryption,
audit retention/disclosure, Phase 5 identity risk acceptance, Master SoT adoption
and release authorization. Configuration must explicitly represent signed
policy and adoption; a role label or admin case approval cannot substitute.
GuardianSharingEnabled and CrossDoctorSharingEnabled default false independently.
