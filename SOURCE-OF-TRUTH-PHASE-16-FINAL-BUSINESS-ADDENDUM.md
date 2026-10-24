# Phase 16 final business decisions — incorporation addendum

Business approval date: **2026-10-24 (Africa/Cairo)**. Authority: the owner's
“Phase 16 Final Gap Closure” implementation brief and final decision register.
Business approval is distinct from engineering verification, legal/clinical
sign-off, formal Master adoption and Production authorization.

The full `Healthcare-Platform-Master-Source-of-Truth-v1.2-Updated.txt` is absent
from the repository and the supplied attachments. This is a complete proposed
incorporation addendum recording approved business decisions; it does not claim
to edit unseen Master text or set `MasterSourceOfTruthAdopted=true`.

## Dated decisions and supersession

1. **BR-01 Global Patient.** A Patient is a platform-global person with one
   longitudinal history. Doctors do not own Patients. Encounter, diagnosis,
   prescription, laboratory and radiology source models remain independent;
   the central record is a secured read model, not a new mutable aggregate.
2. **BR-02 Patient reads.** An active verified linked Patient with current
   permission sees their completed patient-safe outputs, including Hidden
   encounters. V1 has no patient sharing toggle, doctor-access approval or
   patient control of doctor-owned visibility.
3. **BR-03 Treatment.** Doctor account approval/activity, current DB permission,
   Doctor/Practice scope, exact Ticket and matching Encounter both InProgress,
   identical Patient and enabled sharing are all required. Reservation,
   Waiting/Called Tickets and earlier treatment do not grant continuing access.
   Walk-In uses the same rules. Completion ends cross-doctor entitlement.
4. **BR-04 Hidden.** Completed Visible outputs may be shared. Hidden entries
   expose a date-only placeholder with no diagnosis, notes, prescription,
   clinician/practice identity, attachment, source Encounter ID or internal
   reason. Existing source-Doctor approval binds source Encounter, requester,
   treating Ticket and visibility revision. Completion, revocation and visibility
   changes invalidate grants. V1 has no critical-alert exception or Break-Glass.
5. **BR-05 Family.** Preserve the real Family aggregate and one active Family per
   Patient. Father, Mother, Child, Guardian and LegalGuardian remain distinct.
   Membership alone does not authorize clinical reads.
6. **BR-06 Verified parents.** Father/Mother receive automatic dependent access
   after exact parent-child verification, without separate medical eligibility.
   Require authenticated active non-first-login Patient user, valid current
   PatientAccountLink, current JWT/DB ViewDependent permission, active Family
   and exact adult/Child members, age below 16 in approved policy timezone,
   enabled/configured signed policy, no effective restriction and successful
   audit. Pending/Rejected/Revoked historical parent eligibility is not a veto.
7. **BR-07 Exact proof.** A member's role cannot prove their relationship to every
   child. An adult-adult membership request cannot prove an adult-child pair.
   Use approved exact-pair requests, current revisioned private evidence and
   persisted provenance. Unknown, ambiguous, mismatched or revoked proof denies.
8. **BR-08 Guardians.** Guardian/LegalGuardian require exact reviewed authority
   proof plus current Approved child-specific medical eligibility, matching
   member IDs, current policy version and accepted basis, account/permission,
   age and restriction predicates. Guardian proof concerns care/delegation;
   LegalGuardian proof concerns official authority under the signed policy.
   No jurisdiction or legally acceptable document taxonomy is invented.
   SuperAdmin reviews restricted evidence/metadata without clinical access.
9. **BR-09 Projection.** Dependents use the existing patient-safe projection,
   including completed Hidden outputs. Exclude doctor-private clinical notes,
   diagnosis notes, drafts, amendment reasons, raw audit, storage keys and
   confidential operational metadata. Minor-confidential categories remain a
   separate legal/clinical release gate.
10. **BR-10 Age.** Stop all new dependent reads/listings/downloads at local
    midnight beginning the 16th birthday. Do not remove Family membership,
    change under-18 booking authority or change global PatientId/history.
11. **BR-11 Restrictions.** Preserve requests, restrictions, documents,
    eligibility, histories and endpoints. Pending cases never suspend reads.
    Only an effective approved Active Restriction denies the affected pair.
    Separation, divorce, a LegalGuardian appointment or ordinary disagreement
    do not automatically remove parental rights. New approvals require an
    explicitly configured policy ground and reviewed evidence. Existing active
    restrictions remain effective. Restoration only lifts the restriction; it
    cannot manufacture relationship proof or Guardian eligibility.
12. **BR-12 Access log.** Preserve the existing Phase 16 scope and exactly four
    patient-facing fields: entryId, actorCategory, action, occurredAtUtc. Do not
    expose actor identity, target IDs, reasons, clinical data, correlation IDs
    or raw audit. This is not a complete cross-module historical export.
13. **BR-13 Closure.** ApplicationUser and Patient are separate. Account closure
    does not delete medical sources, attachments or histories. Relinking uses
    Phase 5 verified identity controls; no fuzzy merge or patient reassignment.
14. **BR-14 Retention.** Approve category-specific versioned retention for
    encounters, prescriptions, results, media, access audit and family evidence;
    controlled review and no automatic clinical purge. Legal Hold defaults to
    individual records; patient-wide Hold requires a mandate. Owning Doctors
    may request; authorized SuperAdmin reviews or registers a direct mandate.
    Hold grants no clinical read permission. Release requires evidence-based
    authorized review; irreversible disposal requires independent review after
    release. Jurisdiction-specific durations are not approved. Full clinical
    retention/Legal Hold APIs are deferred to a separate Phase 18 workstream;
    Phase 5 identity retention and operational archive do not implement them.
15. **BR-15 Security/release.** Fail closed; no arbitrary PatientId lookup,
    Reception/SuperAdmin clinical bypass, role-only grant, emergency override or
    generic medical payload logging. Keep attachments private and reauthorize
    before response bytes. Business approval is not deployment authorization.

These decisions supersede the prior interpretation requiring medical eligibility
for Father/Mother and the inference that independently approved memberships prove
every pair. Prior proposed Phase 16 addenda and old engineering reports are
historical; their contrary business interpretations are superseded here. Preserve
their historical text. The previously OPEN consent/family business design is
resolved as described above, while independent privacy/legal/clinical gates stay
open. There is no claim of legal approval or formal deployment adoption.

## Engineering incorporation

Reuse all 29 P16 routes and all Family relationship, restriction/restoration and
eligibility routes. Append request operations VerifyParentChild=3 and
VerifyGuardianChild=4 without changing CreateFamily=1/AddFamilyMember=2.
Extend the existing eligibility decision with authorityRelationshipRequestId.
Add exact parent proof and nullable Guardian authority persistence through an
additive migration. No new HTTP route, role or permission; no identifier changes.
Historical evidence, cases and histories remain. Reconciliation is an explicit
authenticated operator action, never a migration-wide inferred grant.

See the final gap API inventory, implementation report and test evidence for
actual engineering status. Production and Staging sharing remain disabled.

## Independent release/deferred register

| Gate/workstream | Required authority/evidence | Current status |
|---|---|---|
| Full Master incorporation | Owner-supplied authoritative Master and controlled adoption | Missing artifact; adoption not asserted |
| Treatment/minor privacy | Legal/clinical approval, Hidden/minor-confidential categories and age transition | Pending |
| Signed Family policy | Approved jurisdiction, timezone, evidence taxonomy, grounds, basis/version/reference | No real values fabricated; defaults fail closed |
| Clinical retention/hold | Per-category periods, authority, mandate/release/disposal workflows and independent review | Deferred Phase 18; NOT IMPLEMENTED here |
| Identity retention | Existing Phase 5 controls and independent production risk approval | Existing implementation preserved; no clinical reuse of durations |
| Operational archive | Existing Phase 18 archive, recovery and consistency controls | Preserved; not clinical disposal |
| Credentials | Authorized rotation/revocation of previously exposed credentials and repository-history review | External P0; no credentials reproduced in reports |
| Operations | Secret store/key ring, backups/recovery, scanning, audit availability and representative Staging verification | Separate operational sign-off required |
| Deployment | Explicit environment approval and controlled migration/flag activation | Production release BLOCKED; no deployment performed |
