# PR #38 implementation closure — 2026-11-08

## 1. Branch verification

Repository LoaiSaberC4r/Wasla. Existing branch
`codex/family-self-service-historical-documents`; starting SHA
`d05338369ed6c0b80691459f4086470005f8ad67`.
Existing PR: https://github.com/LoaiSaberC4r/Wasla/pull/38.
No replacement branch, replacement PR, reset, force push or merge is used.
The final commit is recorded by the PR head and the CI test-results/provenance.json
artifact; that artifact records the actual tested checkout without a self-referential
commit hash inside a committed report.

## 2. R-01 through R-13

| Item | Classification | Result |
|---|---|---|
| R-01 Permissions | COMPLETED | Generated inventory covers all 371 names; all prior IDs are frozen in a new pre-closure snapshot. Three existing governance permissions are now in JSON/CSV. Existing Patient/SuperAdmin mappings and subscription classification remain intact. |
| R-02 Discovery | COMPLETED | AND identity search, supplemental phone ranking, bounded strictly masked multiple candidates, explicit manual-review continuation, reviewer duplicate pagination, live identity revalidation, requester/purpose/expiry binding and transactional one-use replay. Durable actor and HTTP rate limits remain. No search grants membership. |
| R-03 Cross-family onboarding | COMPLETED | Preserved intake enters appended ConflictReview state5 with a private governance case, copied current evidence and immutable history. Explicit existing Patient reuse, no contact mutation or transfer in conflict, transactional persistence and durable review replay. |
| R-04 Conditional contact | COMPLETED | Initial and revised intake share the domain contact invariant: child phone or usable named guardian contact; no unconditional guardian-phone requirement when child phone exists. All four phone combinations have SQL API coverage. |
| R-05 Parent collision | COMPLETED | Vacant parent role checked at join submission and rechecked by serialized approval/domain constraints. Two concurrent reviewed claims yield one parent; an occupied role is rejected. Confidential review does not require managing the applicant's current Family. |
| R-06 Linked ticket | COMPLETED | Parent detail uses exact current dependent-ticket authority, ticket subject/existence and archive state/permission; rechecks after loading. Unauthorized linkedTicketId is null and canViewLinkedTicket=false. No queue operation grant. |
| R-07 Parent reservations | COMPLETED | Native SQL journeys exercise bookable child/create/list/details/reschedule/cancel/ticket and live revocation; existing follow-up regression fixture now establishes real reviewed operational authority. Existing16/18 birthday and archive tests remain. |
| R-08 Workspace | COMPLETED | Batched exact operational proofs, medical statuses/operation capabilities and account-link loading replace per-child policy queries. Separate booking/cancel/reschedule/ticket, historical, lab/radiology, profile and finance capabilities/reasons; own pending intake/conflict/identity/invitation states. Legacy GET mine404 preserved. |
| R-09 Profile | COMPLETED | New scoped phone endpoint, primary-contact fallback invariant, immutable before/after audit, rowversion, linked-child-account write denial, private image validation and safe photo replacement/replay. Failed DB commit retains old image; stale new/old bytes are reported for investigation and never automatically deleted. |
| R-10 Finance | COMPLETED | Independent transaction/receipt permissions and live under18 operational pair; forced child SQL filtering, minimal payment/refund receipt projection, no staff free text or mutation rights. Positive and cross-child/permission regressions added. |
| R-11 Historical lifecycle | COMPLETED | PatientProvided/Unverified categories, private immutable versions, logical withdrawal, quotas/signatures/MIME, sanitized filename and reauthorized hash-checked download retained. Added durable rejected/quarantined validation receipts without publishing unsafe bytes; failed replacement preserves last clean version. Real ClamAV clean/EICAR plus deterministic ClamD verdict, timeout/unavailability and existing DB/lost-ack failure tests. |
| R-12 Private media | COMPLETED | Six actual module/path/reference resolvers, durable SQL cursors and serialized batches, stable continuation, private per-module investigation reports and metrics. No automatic orphan deletion. Fresh-context continuation and photo reconciliation tested. |
| R-13 Database | COMPLETED | Two additive forward-only migrations, unchanged historical migrations, consistent snapshot; complete idempotent script through current model. Clean/populated/restored SQL Server rehearsals execute the actual script twice where applicable. |

Additional baseline performance fix: parameterized bounded inserts replace change
tracking of disposable availability-slot rows during full public-discovery refreshes.
Per-batch transactions keep deletion, replacement and rank refresh atomic. The unmodified full-suite 1000-doctor
fixture retained hundreds of thousands of slot entities through every clinical/audit
SaveChanges guard. Persisted projection values and all aggregate guards remain unchanged.

The existing 2001-patient/20002-ticket directory regression also exposed repeated
change detection while registering archive identities and capturing reporting
policies, plus a native SQL correlated sort over complete attendance history.
Those operations now use one local identity snapshot, one policy change-detection
pass and a bounded latest-timestamp join. The unchanged regression passed with
five reader queries and a 3817.6 ms first page. All 12 directory tests and all five
public discovery tests passed. Existing finance-constructor and real-browser
fixtures were corrected without reducing assertions or substituting renderers.

## 3. Changed files

[CHANGED-FILES-CLOSURE.md](CHANGED-FILES-CLOSURE.md) lists each changed path and
its role. Application services/handlers retain CQRS/MediatR and existing Result
patterns; controllers bind DTOs only. No duplicate authorization service or
alternate patient/family identity system was introduced.

## 4. Database and deployment artifact

New migrations:

- 20261108111202_FamilyProductionClosure: intake governance FK/state/index,
  profile audit snapshots, reconciliation checkpoint and immutable run tables.
- 20261108111713_HistoricalValidationReceipts: immutable operation verdict,
  failure code and attempted metadata fields, existing rows default to clean.

Foreign keys restrict deletion. Existing active Father/Mother, exact-pair,
rowversion, Patient and Family constraints remain. No Patient IDs, Family data,
permission IDs, clinical results or historic authorities are backfilled/reassigned.
Down operations refuse destruction of retained audit/evidence. The generated
script contains all previous migrations plus the four pre-existing feature
migrations and these two additions.

Regenerate with `python tools/generate-family-migration.py`. It preserves EF
history guards and transactions while deferring compilation of each guarded
operation with EXEC. Raw EF idempotent output otherwise fails SQL Server binding
against columns added/removed by older migrations, even inside skipped IF blocks.
The delivered artifact has 1193 guarded operations and is rehearsed directly,
including replay after the schema has advanced. Historical migrations were not
rewritten to conceal this script defect.

SQL Server Express 2022 local disposable databases passed clean initialization,
representative populated upgrade, CHECKSUM backup/VERIFYONLY/restore, retained-ID
assertions and idempotent replay. None of these commands targets Production
db73076. See [the deployment runbook](../../deploy/family-production/README.md)
for backup, restore, controlled security seeding and rollback.

## 5. Permissions

No new permission name or ID was needed in this closure. Existing code already
appended DependentIdentityCorrection.RequestOwn (369),
FamilyJoinInvitations.CreateOwn (370) and RedeemOwn (371); the missing artifacts
caused CI's372-versus-369 line-count failure. The code-derived generator synchronizes
CSV/JSON, and the frozen371-name snapshot verifies every earlier index/GUID.
The 34 post-epic names are documented by new-permissions.json; all 371 commercial
classifications are covered. Patient requests remain UniversalPatient; existing
SuperAdmin governance review confers no medical browsing entitlement.

## 6. Production configuration and dependencies

All in-scope effective Production flags are true: cross-doctor and guardian
Medical Record sharing, dependent management, historical documents with real
scanner requirement, reconciliation, malware scanner, clinical email/in-app
privacy release settings, notification dispatch and Email Outbox. Family,
onboarding, profile, queue, diagnostics and finance use those existing gates and
their independent live permissions; no invented duplicate feature flags were added.
Startup migration/seeding remain false.

The exact owner-approved policy, bases, evidence types, five restriction grounds
and mandatory restriction/restoration evidence are integrated in
[the repository master](../../MASTER-SOURCE-OF-TRUTH.md) and Production appsettings.
Policy.IsConfigured is unchanged. ApprovalReference records Product Owner product
policy approval and is never described as external legal certification. No policy
approval input remains missing.

Existing appsettings credentials are preserved. A strong stable opaque entry key
is configured without reproducing its value. No secrets moved to another provider.
Original references named in the task but absent from this checkout were not
reconstructed; the available approved final addenda and the owner's explicit
2026-11-08 successor decision govern this change.

| Capability / dependency | Classification | Verified state |
|---|---|---|
| Family/medical/profile/diagnostic/finance implementation | COMPLETED | Code and local authorization regressions; enabled Production target |
| FamilyMedicalPolicy owner inputs | COMPLETED | Exact supplied values and repository adoption; no fabricated certification |
| ClamAV integration/local provisioning | COMPLETED | Pinned 1.5.4 container on 127.0.0.1:3310, healthy; real clean/EICAR, unavailable/timeout fail closed |
| Protected persistent Data Protection implementation | COMPLETED | Application name Wasla.Phase16, certificate encryption, fresh-provider restart and shared-instance tests |
| Actual Production storage/key/certificate provisioning | PARTIALLY IMPLEMENTED | Scripts/configuration/validation ready; confirmed host paths and topology not supplied |
| Email Outbox and notification implementation | COMPLETED | Worker/retry/idempotency/recipient enforcement tests; Production settings enabled |
| Actual SMTP delivery | PARTIALLY IMPLEMENTED | Connectivity and STARTTLS succeeded; existing credentials rejected authentication; zero test messages sent |
| Actual API Production deployment | PARTIALLY IMPLEMENTED | Runbook and fail-fast checks prepared; API host/plan/publish profile and runtime support unconfirmed |

Production startup fails clearly for missing persistent paths, invalid policy or
disabled in-scope flags, unreachable scanner and failed authenticated STARTTLS.
`/health` checks private dependencies without exposing values. The current unset
paths deliberately require real operator inputs rather than fabricated locations.
This is an activation target, not a claim that Production is deployed or ready.

## 7. Executed verification

SDK 10.0.401/runtime 10.0.12 on Windows; native SQL Server Express 2022 and Docker
ClamAV 1.5.4. Final build command:

`dotnet build Wasla.sln --artifacts-path artifacts/family-complete -m:2 -p:Platform="Any CPU"`

Unit: 463 passed, 0 failed/skipped. Architecture: 26 passed, 0 failed/skipped.
BuildingBlock: 323 passed, 0 failed/skipped. SQL deployment/worker/reservation
focused rerun: 4 passed, 0 failed/skipped. Supplemental closure/dependency/native
SQL run: 22 passed; additional medical/finance: 6 passed; final finance/paging:
3 passed. Both real-browser prescription cases passed after explicitly awaiting
Chromium provisioning before their HTTP assertions. These overlapping focused
runs are not added together as unique suite coverage.

The 548-case consolidated local integration run was still executing when this
snapshot was written. Its two initial failures were cold Chromium downloads
exceeding request timeouts; the corrected real-browser rerun passed both cases.
No incomplete run is counted as successful. Final-head GitHub CI is the
authoritative complete-suite result, with exact counters and SHA in its
test-results artifact. TEST-EVIDENCE.json records the local snapshot and is
enriched by CI with that checkout's actual results.

Baseline restore/build passed; baseline unit 450 passed/1 failed and architecture 26
passed. Original CI failed CommercialFeaturePermissionCoverageTests on missing
CSV rows. Full baseline attempts were incomplete/aborted: one ended after 78
passes; the second recorded 158 passes and the two fixture failures (duplicate
seeded permissions and unreviewed family booking) before the heavy discovery
projection fixture was diagnosed. Those are not claimed as full-suite passes.
Initial new-test failures and their corrected setup/protocol/status expectations
are retained in local TRX evidence; no failing test was deleted or skipped.

Clinical notifications remain self-only; no new proxy clinical disclosure rule
was added. Real SMTP preflight accepted connectivity and STARTTLS but returned
AuthenticationException. The approved recipient was never sent a message because
the prerequisite authentication failed. Server acceptance and inbox delivery are
unverified. The once-only probe retains a pre-send marker before any real send and
does not retry uncertain acknowledgements.

Final GitHub CI is evaluated on the pushed PR head; the uploaded provenance JSON
contains its exact SHA and all TRX counters. The final chat/PR records the observed
CI result rather than predicting it in this committed report.

## 8. API and frontend impact

[API-HANDOFF-CLOSURE.md](API-HANDOFF-CLOSURE.md) documents the changed methods,
routes, permissions, actor/resource scope, DTOs, status/validation errors,
rowversion/idempotency and capability/reason fields, plus the full 17-part journey.
Changes are additive: candidate nextAction, intake conflict reference/state,
reviewer match pagination, Workspace capabilities/pending workflows, scoped phone
mutation, linked-ticket redaction, and historical validationState/failureCode.
Historical currentVersion can be null for a never-published rejected/quarantined
source; frontend must render its safe status without a download/clinical result.

## 9. Exact remaining deployment inputs

1. API Production hostname, hosting plan and publish/deployment profile.
2. Confirmed persistent absolute MediaStorage:RootPath,
   MedicalRecord:DataProtectionKeyRingPath and
   MedicalRecord:DataProtectionCertificatePath; provide the corresponding PFX
   password in appsettings if encrypted. Confirm service identity/permissions,
   backups and whether nodes share those paths/certificate/opaque key.
3. Host support for continuous outbox/notification/reconciliation workers and a
   loopback scanner or approved local authenticated tunnel. MonsterASP SQL hosting
   alone establishes none of these. If its website plan cannot run the tunnel,
   the API/scanner need a compatible VM/container host; the SQL database may remain.
4. Working authentication for the existing configured SMTP account (credential
   validity/account policy/provider access). Existing SMTP settings were not
   modified. Do not send another message until authentication is corrected; the
   single authorized synthetic-send condition has not yet been satisfied.

No product approval clarification is required. No feature was disabled to avoid
these deployment inputs. No live Production migration, seeding or deployment was
performed. No production database/provider account was inspected or changed.

## 10. Readiness distinction

Implementation and approved policy: completed, subject to the recorded final test
results. Local scanner, protected key persistence and outbox worker: verified.
Production feature/policy target: configured. Production filesystem/certificate,
scanner/worker deployment and SMTP delivery: unverified or blocked as specified.
Actual Production deployment verification: not performed. **Production Ready is
not claimed.**
