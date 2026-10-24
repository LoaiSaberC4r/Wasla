# Phase 16 proposed addendum — pending formal adoption

Historical proposal. The owner's [2026-10-24 final business decisions](SOURCE-OF-TRUTH-PHASE-16-FINAL-BUSINESS-ADDENDUM.md)
supersede earlier unresolved business interpretations, including the uniform
parent/Guardian eligibility rule. Full Master incorporation and independent
legal/clinical/deployment approval remain pending.

Current implementation and base sharing flags are recorded in
[final closure](PHASE-16-CLOSURE-REPORT.md). Default-off and inactive-family
statements below describe the original proposal. Formal Master adoption remains
pending; this task does not supply missing privacy/legal approval.

Status: **PROPOSED, UNAPPROVED**. Prepared 2026-10-12 against main
`563e2111182f584986f225a3711a459a00e3b913`. The complete authoritative v1.2
master is referenced by README but is not present in this checkout. The task
states that its consent model remains OPEN; this file does not amend that master.

Proposed product policy: the encounter owner chooses Visible/Hidden for completed
visits. A different approved doctor may read safe current outputs only while
their exact matching Ticket AND Encounter are InProgress. Hidden visits expose
date-only placeholders; an owner-approved request grants access to one source
encounter for that treating ticket and visibility revision. Completion,
revocation, or a visibility revision invalidates further access. One request
forever per source encounter/ticket, even after rejection or revocation.

New encounters explicitly start Visible. Legacy records migrate Hidden. Sharing
never exposes internal notes, amendment reasons, drafts, superseded versions,
unreviewed submissions, storage keys, or raw audit. Verified patients see their
own safe completed outputs independent of sharing visibility.

Production release blockers: jurisdiction-specific privacy/consent and patient
rights review; formal master adoption; Phase 5 dedup/claim/linking closure;
clinical safety policy for hidden critical alerts; audit retention/disclosure
policy; secure persistent multi-instance Data Protection keys. Cross-doctor and
guardian flags default false. No legal approval is inferred from implementation.

Family scope has a separate legal/privacy gate. Membership alone does not grant
medical access; under-16 medical eligibility differs from under-18 booking. No
automatic patient merge, emergency override, or medical-center group privilege.

Phase 5 gap: PatientAccountLink records verified identities, but this checkout
does not establish formal governance closure of deduplication, claim proof,
revocation and identity matching. No fuzzy matching or automatic merge is added.
