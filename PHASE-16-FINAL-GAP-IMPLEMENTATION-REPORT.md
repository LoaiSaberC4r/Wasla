# Phase 16 final gap closure — implementation and compliance

Date: 2026-10-24 (Africa/Cairo). Branch: `codex/phase16-final-gap-closure`.
Baseline main: `898526bd9de4bf28bea9fa47d26b9ff7374d49e0`, fetched and matched.
Business scope: the owner's two supplied final Phase 16 closure briefs.

**BUSINESS APPROVED. ENGINEERING PASS. Legal/clinical sign-off is PENDING.
Production release is BLOCKED.** Final runtime/test source:
`aa5a8e0dbdedac4a992496003ce57de9d9e56023`.
Read the [machine-readable evidence](PHASE-16-FINAL-GAP-TEST-EVIDENCE.json)
alongside the exact results below. Documentation-only follow-ups retain this
tested runtime source identity.

## Implementation

The previous policy required all four adult roles to have medical eligibility and
treated separately verified membership as relationship proof. The new policy
requires exact approved adult-child provenance. Father/Mother use active persisted
parent verification, independent of historical medical eligibility. Guardian and
LegalGuardian use current Approved eligibility bound to one exact authority
request, current member IDs and signed policy/basis. Existing effective
restrictions remain a veto for all adults; Pending cases do not suspend access.

The existing Family, clinical source models, central read service, hidden grants,
eligibility, restriction/restoration, documents and histories are reused. New
request types use existing self/assisted/review routes. Verification validates
existing same-family members and exact stored roles without inserting membership.
Ordinary initial-family/member approvals record parent proof only for an explicit
adult-child source with current permitted real evidence. Adult-adult approval
never grants proof to children. Approval/proof/history/outbox writes remain inside
the existing transactional command boundary; pair locking and rowversions protect
concurrent approval. Administrative relationship review now explicitly checks
current active SuperAdmin scope and both JWT/database permission.

New Guardian eligibility candidates exclude parents. Historical parent reviews
and their detail/history remain; the reviews queue focuses on Guardians. Explicit
reapproval through the existing endpoint supports current proof/policy renewal and
records another immutable decision. New restriction approvals require an accepted
configured ground and actual current private evidence; configured grounds default
empty. Existing approved restrictions are not reclassified or deleted.

Each new eligibility decision preserves its policy/basis and exact authority
reference in immutable administrative history metadata, so re-verification cannot
erase the provenance of an earlier approval. Existing history rows are untouched.
API tests also exposed an EF tracking issue in the existing member-addition path:
the domain assigns GUIDs before tracking, so the workflow now explicitly registers
the new member as an insert instead of allowing EF to infer an existing row.
Native SQL and SQLite parent creation/addition tests verify that correction.
The full regression run also exposed a pre-existing repeated-shutdown race in the
public discovery worker. StopAsync now atomically takes ownership of its token
source before cancellation/disposal; a unit test covers concurrent/repeated stop.
The worker's refresh behavior and clinical authorization are unchanged.
The older public-discovery and onboarding API fixtures also now follow the
existing Phase13 fixture by removing hosted workers: those fixtures share one
SQLite connection and drive transitions/projection refreshes explicitly. This
prevents unrelated workers from opening competing transactions during setup.
The Phase5 native identity regression now checks that its required migration is
present in the applied chain, rather than assuming no later phase can add a
migration. Its native identity constraints and concurrency assertions remain.

The centralized policy serves P16-24–29 and family status/roster. It batches
members, parent proofs, source requests/current documents, eligibility and
restrictions. No per-child authorization query is added. Access versions include
exact proof and current request/document/member/policy/date state. Parent versions
do not use historical eligibility state. Existing record and attachment readers
recheck current scope before disclosure and after audit; no clinical payload logging
or public attachment path is introduced. Existing identifier-free medical telemetry
is retained.

## Additive persistence and compatibility

Migration `20261024174818_Phase16ExactFamilyRelationshipProof` adds:

- VerifiedParentChildRelationships with exact patient/member IDs, parent role,
  approved request/revision, original reviewer/time, status/revocation, policy
  version, SQL rowversion, seven restrictive FKs, role/status/distinct-patient
  checks, unique active exact pair and lookup indexes.
- Nullable indexed AuthorityRelationshipRequestId with restrictive request FK
  on FamilyMedicalEligibilityReviews. Legacy rows stay intact and nullable.

The approved revision binds parent authorization to the reviewed evidence state;
the recorded policy version is provenance metadata. A new configured global
policy version does not create a separate parental medical-eligibility approval:
current permitted evidence, proof state and all common gates are re-evaluated.
Guardian eligibility still requires its own current medical policy version/basis.
Provenance cannot be edited/deleted through the
DbContext; revoke and review a new proof. Down deliberately throws to preserve
medical proof. Rollback disables sharing and retains additive schema/history.
No historical migration, existing permission order, package version, clinical
source, account-link/claim workflow or booking-age rule is changed.

The authenticated maintenance CLI supports dry-run `report` and explicit `apply`.
Candidates are approved requests naming an exact parent and child; there is no
same-family cross-product grant. It verifies active scope, matching roles, actual
current private evidence and signed configuration. Ambiguous, conflicting or
missing proof stays unavailable. Existing recorded proof, including revoked
history, is counted as already recorded and never resurrected. Reconciliation
reports total candidates, safe/already recorded/ambiguous/missing/conflicting/
failed counts and whether apply was requested. The original source reviewer and
time are retained; reconciliation does not fabricate a fresh administrative
approval. A failed transaction aborts rather than committing partial verification;
reruns are safe. No startup worker or HTTP maintenance route exists.

Legacy Guardian approvals without an explicit authority source remain unavailable
until an authorized reviewer supplies the exact existing proof through reapproval.
No duplicate relationship case is needed when the original exact proof is valid.
This conservative re-verification avoids guessing sources or rewriting history.

## Decision compliance matrix

| Rule | Implementation status | Code/behavior | Verification | Remaining gate |
|---|---|---|---|---|
| BR-01 | Preserved | Global Patient and existing source/read models retained | Cross-phase/architecture regression | Existing identity/clinical governance |
| BR-02 | Preserved | Own patient-safe record and Hidden outputs retained | Phase16PatientApiTests, Phase16PolicyApiTests | Minor/privacy sign-off |
| BR-03 | Preserved | MedicalRecordAccessPolicy exact live treatment remains | Phase16SharingActivationTests, MedicalRecordApiTests, HardeningTests | Treatment-sharing legal/clinical approval |
| BR-04 | Preserved | Hidden source-owner/revision grants and placeholders retained | Phase16HiddenAccessApiTests, ContractTests | Clinical privacy release |
| BR-05 | Preserved/fixed | Family aggregate and active membership constraints retained; explicit member insert | Family domain, native SQL/SQLite API tests | None newly introduced |
| BR-06 | Implemented | FamilyMedicalAccessPolicy parent branch bypasses medical eligibility only after exact proof | FinalGapTests parent role/legacy-state matrix | Real signed policy and verified data |
| BR-07 | Implemented | VerifiedParentChildRelationship, workflow request types 3/4, exact evidence, no adult-adult inference | FinalGapTests verification/assisted/negative/concurrency/reconciliation | Resolve unresolved legacy proof before use |
| BR-08 | Implemented | Guardian authority FK, exact proof/media validation, current eligibility, immutable provenance and explicit re-review | FinalGapTests wrong-child/re-verification; GuardianActivationTests | Approved legal authority/evidence taxonomy |
| BR-09 | Preserved | Existing patient-safe projections reused, including Hidden | Parent all-route Hidden test, Guardian activation, privacy regressions | Legally confidential minor categories |
| BR-10 | Preserved/hardened | Policy timezone/local-midnight age logic; invalid timezone fails configuration | Guardian activation age gate, common dependent policy | Approved policy timezone |
| BR-11 | Adapted | Existing cases/restrictions/history; new ground/evidence guard; restoration removes only restriction | FamilyMedicalApiTests, FamilyHardeningTests, FinalGapTests dispute/media-race denial | Real restriction grounds/mandates |
| BR-12 | Preserved | Existing four-field basic access log retained | Phase16PatientApiTests exact shape/isolation | Audit retention/disclosure governance |
| BR-13 | Preserved | User/Patient separation, no medical deletion or fuzzy merge | Phase5 regression and unchanged source models | Independent identity release risk |
| BR-14 | Documented/deferred | No clinical purge/disposal/hold API implemented; existing claim retention/archive retained | Cross-phase regression; source diff | Separate Phase 18 clinical retention workstream |
| BR-15 | Preserved/hardened | JWT/DB permissions, no staff bypass, private file rechecks, fail-closed audit, disabled release flags retained | Activation, Hardening, Contract, architecture tests | Master/legal/security/operations/deployment sign-off |

See [the API diff](PHASE-16-FINAL-GAP-API-DIFF.md) for all affected existing
surfaces and frontend compatibility. See [the dated incorporation addendum](SOURCE-OF-TRUTH-PHASE-16-FINAL-BUSINESS-ADDENDUM.md)
for approved rules, supersession and the complete deferred release register.

## Verification evidence

**Final CI PASS: [run 40129895771](https://github.com/LoaiSaberC4r/Wasla/actions/runs/40129895771),
source `aa5a8e0dbdedac4a992496003ce57de9d9e56023`.** The complete solution Release
build succeeded with analyzers and warnings treated as errors. Raw TRX evidence
was downloaded from the run's `test-results` artifact (ID `12574344590`), verified
against SHA-256 `6c1a30e16af919a3d45a43dfe9b5332a3e93baa149a0d0012456d17d51cf1df1`
and parsed by individual result outcome, including skipped cases.

| Final CI group | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Unit | 291 | 0 | 0 |
| Integration (`Category!=SQLServerConcurrency`) | 261 | 0 | 0 |
| Native SQL concurrency (`Category=SQLServerConcurrency`) | 44 | 0 | 0 |
| Architecture | 23 | 0 | 0 |
| BuildingBlock/frozen main API compatibility | 323 | 0 | 0 |
| **Total distinct final cases** | **942** | **0** | **0** |

The two complementary integration filters cover the entire integration assembly,
including real SQL Server cases, Phase5 identity/claim, Phase18 archive, all 29 P16
routes, the 21 Family medical/status routes, affected request/response schemas,
patient-safe privacy, IDOR, exact proof, current permission, cursor and concurrent
attachment/approval boundaries. No assertion or performance threshold was relaxed.

Additional local evidence: the isolated native 1,000-visit benchmark passed with
**1,496.5 ms** first-page latency, 10 stable pages of 100 and 500 strictly redacted
Hidden placeholders. Its SQL plan and timing are under `_local-data/phase16-validation`.
EF reports **no pending model changes** on the final source. The additive forward
script is [PHASE-16-FINAL-GAP-MIGRATION.sql](PHASE-16-FINAL-GAP-MIGRATION.sql);
native migration rehearsals and database constraints passed. `git diff --check`
is clean. Repeated local cases are not added to the distinct CI total.

Initial baseline: unit **290 passed**, architecture **23 passed**. The broad
integration baseline was intentionally interrupted after about 20 minutes; its
TRX contains **178 passed / 104 skipped / 0 reported failed**, plus an aborted
test process. It is **not** a complete baseline pass. SQL cases were skipped
because that baseline invocation did not configure a SQL test connection.

Pre-final full local results were **257 passed / 3 failed / 0 skipped** in the main
integration group and **42 passed / 1 failed / 0 skipped** in native concurrency.
The failures were a loaded-host timeline timeout, repeated worker shutdown, a
shared-SQLite nested transaction and the obsolete Phase5-last-migration assertion.
The worker/test/migration assertions were corrected as described above; the SQL
benchmark passed in isolation, and all affected cases passed in final CI. Earlier
focused iterations caught missing fixture transactions and the member-insert
tracking issue. Their exact counts and paths remain in the evidence manifest.

The host inherited Platform=MCD; local commands explicitly select AnyCPU/Any CPU.
An early simultaneous test/rebuild attempt encountered Windows assembly locks;
later compilation and active test output directories were kept separate. Early
compile issues were corrected before final CI. No partial, aborted or failed
iteration is counted as a final engineering pass. Local raw results live under
ignored `TestResults/phase16`, including the verified CI artifact extraction.

[Draft PR #30](https://github.com/LoaiSaberC4r/Wasla/pull/30) contains the change.
Production and Staging remain disabled. No real credentials, jurisdiction, accepted
proof taxonomy or policy approval/adoption values were supplied. The full Master
remains absent. Previously exposed credentials require authorized external
rotation/revocation and history review; no secret is reproduced here. No Production
migration, seed, flag activation, deployment or application email/SMS dispatch was performed.
