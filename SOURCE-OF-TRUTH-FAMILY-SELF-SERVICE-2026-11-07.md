> Historical decision v1. The Production disablement and configuration-provider instructions are superseded by [owner decision v2](docs/family-self-service/DECISION-2026-11-08-v2.md), integrated in [the repository master](MASTER-SOURCE-OF-TRUTH.md). Exact authority, privacy and immutable-history rules remain effective.

# Family self-service implementation decision addendum v1

Authority: the owner-approved 2026-11-07 implementation epic, preserved in
`docs/family-self-service/OWNER-EPIC-2026-11-07.txt`. Baseline:
`feaa8c73f4f2eaed46d38e24e161893fae6909a3`; no intervening commits.
The master v1.2 artifact is absent. This addendum does not amend an unseen master
or assert legal, clinical, staging or production approval.

BR-001–024 and FD-02/FD-03 govern this implementation. Operational authority is
an independently reviewed exact adult/child pair, never inferred from membership,
contact details, clinical proof or a shared phone. Under-18 operational age uses
Africa/Cairo, configured independently from signed medical policy. Clinical
reads/writes retain the existing signed-policy under-16 cutoff and restrictions.
Request enum values and permission IDs remain append-only. Existing own routes,
global Patient identity, unique active Family constraints and clinical histories
retain their scopes. No historical proof is automatically elevated by migration.

Candidate references use 256-bit random secrets, SHA-256 storage, actor/patient
and purpose binding, ten-minute expiration and transactional one-use redemption.
Search requires normalized Arabic name AND date of birth AND gender. Phone is
supplemental only. Preapproval family identity disclosure is a security breaking
change, including the legacy request routes.

Production gates D01–D10 remain mandatory. Missing signed policy, scanner,
external credential rotation or deployment approval is not implementation
authorization to bypass a gate. Clinical flags remain off. Cross-Family transfer,
clinical retention periods and Guardian medical write remain outside approval.
See the implementation report for executed evidence and feature status; this
decision record alone makes no completion claim.
