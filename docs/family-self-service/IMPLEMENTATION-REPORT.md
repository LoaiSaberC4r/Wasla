# Implementation review checkpoint — epic incomplete

This checkpoint is a reviewable operational/discovery/intake foundation. It is
**not completion of the consolidated epic and not Production approval**. Remaining
code and external gates are distinguished below. Final test results are recorded
in TEST-EVIDENCE.json; superseded failed attempts must not be called passing runs.

## 1. Baseline, branch and slices

Baseline `feaa8c73f4f2eaed46d38e24e161893fae6909a3` matched main exactly; no later
commits existed at task start. Branch:
`codex/family-self-service-historical-documents`. PR-0 decision/inventory commit:
`a8608d1`. This batch covers PR-1 operational authority/privacy, PR-2 workspace and
selected-child review, the provisional intake/review part of PR-3, and PR-4
authorization/queue integration. It does not claim those entire slices or PR-5–8
complete. No merge or deployment was performed.

## 2. Business decisions

Follow FD-02 AND search and FD-03 opaque selection/evidence/admin review; BR-001–006
global Patient, separate accounts, nullable identity email, existing one-Family
constraints and independent exact operational authority; BR-008/011 reviewed
under18 intake and local-date operational cutoff; BR-012 no automatic transfer;
BR-017 safe profile read with identity writes withheld; BR-019 safe queue reads;
BR-020 operational notification authority independent from clinical proof;
BR-021–024 history/privacy and fail-closed release gates.

The versioned 2026-11-07 addendum references the owner task without claiming to
possess or amend the absent master. Clinical source-of-truth addenda remain in
force. Medical proof is not manufactured from new operational verification/join
types 5/6. No jurisdiction, signed policy version or legal adoption was invented.

## 3. Files

See [CHANGED-FILES.md](CHANGED-FILES.md) for the complete layer-grouped inventory.
Domain adds exact authority, opaque selections and private child intake/history.
Application adds scoped queries/commands/validation and hardens legacy handlers.
Infrastructure persists proofs/intake, performs Arabic exact matching, integrates
operational notification/archive checks and provides ClamD streaming scanning.
API adds purpose-specific routes and redacts legacy private responses. Tests cover
ID stability, age-day semantics, scanner protocol and real SQL/API workflows.
Config retains disabled release flags; docs provide contracts, SQL and gates.

## 4. Persistence and migration

Generated EF migration:
`20261107223612_FamilySelfServiceOperationalFoundation`, its Designer and updated
WaslaDbContextModelSnapshot. Six new tables:

- ChildOnboardingRequests, ChildOnboardingEvidence, ChildOnboardingHistories.
- FamilyCandidateSelections.
- ParentChildOperationalAuthorities, OperationalAuthorityHistories.

Mutable intake, candidate and authority rows use SQL native rowversion. Restrictive
FKs preserve Patient/Family/request/user references. Active pair uniqueness is
FamilyId/ParentPatientId/ChildPatientId with IsActive=1; reviewed source request is
unique; distinct parent/child and permitted role/revision checks apply. Candidate
hash is unique, as is consumed actor/idempotency key. Intake has unique actor/key
and a filtered open parent/normalized-intent index. Patient DOB/Gender index bounds
Arabic-normalized search and duplicate classification. Existing identity-email,
account-link, Family membership and Father/Mother unique indexes are unchanged.

No historical approval backfill runs. Join and operational verification reuse
FamilyRelationshipRequest/evidence/history with appended types 6 and 5 instead of
duplicating that review state machine. Intake copies only its reviewed current
evidence revision into the definitive relationship workflow. Source PatientId is
reused only after explicit reviewer selection; no clinical merge/reassignment.

Up is additive. Down deliberately throws: disabling flags and retaining histories
is the rollback strategy. MIGRATION.sql is generated idempotent upgrade SQL. Fresh
SQL Server migration success is not a populated upgrade/backup-restore rehearsal.

## 5. API contracts

See [API-HANDOFF.md](API-HANDOFF.md) for methods, routes, actors, scopes, requests,
responses and security breaking changes. Own profile, own tickets, own finance,
own encounters and existing diagnostic ownership routes were not broadened.
New queue DTOs exclude clinical/payment/staff controls. Before approval, selected
target identity and other-family roster are never returned to the applicant.
Private reviewer text is kept in admin views rather than applicant history/email.

## 6. Permissions, seeding and commercial classification

All 337 pre-task name/index/GUID pairs are frozen in baseline-permissions.json.
The 13 new entries in exact append order and GUIDs are frozen in
[new-permissions.json](new-permissions.json): Workspace; Search; Submit; Profile;
Tickets; RevokeAuthority; Onboard; OnboardingOwn; OnboardingResubmit;
OnboardingReview; OnboardingInbox; OnboardingDetails; OnboardingEvidence.
Their full names are authoritative in that JSON, not these abbreviated labels.

Eight are UniversalPatient and five SuperAdminGovernance. Role seeding is additive
and idempotent; no Doctor/Reception commercial feature is granted. The explicit
CommercialPermissionCatalog and permission-coverage.csv classify every new name.
Prior suffix/count tests were extended for the approved append, preserving their
old sequence/GUID assertions; the new snapshot also freezes every pre-task ID.

## 7. Authorization map

| Operation | Required scope |
|---|---|
| Search/intake/workspace | active non-first-login linked adult Patient; JWT AND DB operation permission; operational flag |
| Dependent create/view/cancel/reschedule | existing granular Reservations.*Dependents permission; live exact operational proof, active Family/member pair, child under18 in configured operational timezone |
| Follow-up booking/read | existing follow-up permission plus hardened bookable-patient scope; no clinical sharing prerequisite |
| Queue/profile read | independent Tickets/Profile permission, same exact current operational scope; exact ticket subject; post-fetch recheck |
| Operational notification | independently verified/eligible recipient and preferences; current exact operational proof; clinical notifications remain self-only |
| Existing dependent clinical read | original signed policy + sharing flag + exact clinical proof/restrictions + token/DB medical permission; under16 in signed policy timezone |
| New historical/diagnostic medical write | NOT IMPLEMENTED; no permission/clinical authority inferred from operational proof |
| New dependent finance | NOT IMPLEMENTED; own financial routes remain own-only |

Operational timezone default is Africa/Cairo, independent of medical policy.
Family role/contact/shared phone never substitutes for exact proof. New proof
source type/revision/reviewer/member IDs and approval time are checked live.
Revocation retains requests, Patients, bookings and history. Read rights end when
applicable proof, membership, Family, permission/account or age predicates fail.

## 8. Executed evidence and limits

Commands use .NET 10.0.401 and explicit Platform="Any CPU" because inherited MCD
failed the first restore. Baseline restore/build passed (0 warnings/errors);
baseline unit 432 and architecture 26 passed. Broad baseline integration was
stopped after prolonged lack of output/resource consumption and is INCOMPLETE.
No full integration pass is claimed. Windows binary locks required isolated
`--artifacts-path artifacts/family-validation` outputs for the changed build.

Focused initial unit run: 9 passed. Initial API run: 2 passed, 1 fixture-account
failure, 1 SQL skip; corrected root account, not production authentication.
Subsequent API run: 7 passed, 0 failed/skipped with explicit disposable LocalDB
SQL tests. It covers concurrent approval (one winner), durable replay, native
rowversion/migrations, no preapproval Patient, no child account, policy-off
operations, unproved sibling denial, live revocation, opaque actor-bound search,
Arabic folding and confidential review redaction. Architecture: 26 passed.

The first full changed unit run found three catalog/suffix expectations; explicit
classification/inventory and approved append expectations were corrected. See
TEST-EVIDENCE.json for rerun results: **441 unit, 26 architecture and 36 focused
integration tests passed, 0 failed/skipped**. The focused integration set includes
the seven new API cases and existing family/notification/permission regressions
with an explicit disposable LocalDB connection. Its first run found three real
proof-supersession concurrency failures: applying the client's RowVersion after a
nested SaveChanges restored a stale version. Moving it before the first save
resolved them without weakening assertions. The final build has 0 warnings/errors.
EF reports no pending model changes.
Remaining mandatory coverage includes populated SQL upgrade/restore, broader
reservation mutation/age/revocation races, intake storage/uncertain-commit fault
injection, per-actor quota/load tests and the complete regression suite.

## 9. Production and rollback

See [ROLLOUT.md](ROLLOUT.md) and MIGRATION.sql. Operational defaults are disabled;
Production medical sharing remains false. No startup migration/seeding was
enabled. MediaStorage requires a real scanner in Production; the ClamD adapter
rejects missing/error/unknown/unsafe verdicts and restricts plain TCP to loopback.
Protocol mocks are not actual scanner provisioning or malware verification.

Five credential-bearing key locations are identified without values in
credential-review.json. External rotation/history remediation and coordinated
removal/secure overrides are still P0; no external rotation is claimed. Private
storage, reconciliation/metrics, persistent protected key ring, outbox monitoring,
sanitized staging rehearsal and deployment authorization remain release gates.
No production/staging DB was queried or mutated.

## 10. Frontend handoff

Open workspace directly, including no-Family accounts; never require GET mine to
succeed first. Search requires all three identity fields. Store opaque selection
only; no manual PatientId/FamilyId entry. Admin-only duplicate candidates support
explicit existing identity selection. Pending intake creates no Patient.
Update null/redacted legacy target/history contracts before release. Unfinished
contact/photo/identity-correction/medical upload/finance actions are unavailable.
No new historical Timeline discriminator exists in this batch; official counters
and existing clinical polymorphism remain unchanged. New error/feedback resources
are English/Arabic. Evidence resubmission currently revises files, not intake
identity/contact metadata; metadata correction remains an implementation gap.

## 11. Remaining code and genuinely external blocks

Remaining approved implementation: dependent contact/photo writes and image reads;
identity-correction review; invitation/conflict case UI/workflow completeness;
historical own documents/version/withdraw/private download/timeline; separately
authorized child historical writes and diagnostic proxy; independent dependent
finance; full capability/reason/dashboard completeness; automated media orphan
reconciliation/observability; remaining security, SQL and release verification.

External/governance constraints, not excuses to label missing code implemented:

1. Production clinical release requires signed jurisdiction/privacy/minor policy
   and formal adoption; current flags remain off. This does not block valid
   operational booking in the implemented enabled pilot paths.
2. Active credential rotation/revocation and secure environment overrides require
   authorized operators; repository/history exposure is not remediated by a plan.
3. Real scanner/private storage/key-ring/backups/staging and deployment approvals
   require operational provisioning and evidence.
4. Cross-Family transfer and Guardian historical medical write are not approved;
   preserve constraints and confidential review rather than inventing a grant.

## 12. Sub-feature status at this checkpoint

| Sub-feature | Status |
|---|---|
| Decision addendum/baseline inventories | IMPLEMENTED/TESTED (integration baseline incomplete) |
| Exact operational authority, privacy, discovery/workspace | IMPLEMENTED_NOT_RELEASED; focused API/SQL evidence, broader gates remain |
| Reviewed provisional child intake/atomic create or explicit reuse | IMPLEMENTED_NOT_RELEASED; metadata revisions/fault coverage incomplete |
| Scoped reservations/follow-up/notifications, profile and safe queue read | IMPLEMENTED_NOT_RELEASED; broader regression/concurrency coverage pending |
| ClamD scanner adapter | IMPLEMENTED_NOT_RELEASED; protocol tested, daemon/provisioning unverified |
| Contact/photo writes, image read, identity correction | NOT_IMPLEMENTED |
| Own historical source/lifecycle/timeline | NOT_IMPLEMENTED |
| Child historical write and diagnostic proxy | NOT_IMPLEMENTED; eventual medical release also BLOCKED_POLICY |
| Dependent finance/receipt reads | NOT_IMPLEMENTED |
| Full dashboard, rollout/observability/production readiness | NOT_IMPLEMENTED / external gates open |

The complete epic is NOT DONE. This checkpoint must not be presented as full
clinical sharing, complete testing, secret remediation or Production readiness.
