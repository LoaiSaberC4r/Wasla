# PR #36 pre-merge hardening report

Date: 2026-11-03, Africa/Cairo. Branch: `codex/commercial-subscriptions`. Existing PR: [#36](https://github.com/LoaiSaberC4r/Wasla/pull/36), targeting `main`.

Baseline head: `a448a3daca2b64ab8277842d9b0adf177a1bf875`. The requested fetch, checkout, fast-forward pull and clean-status checks were performed before editing. The PR head matched the local branch. Baseline GitHub Actions [CI run 71](https://github.com/LoaiSaberC4r/Wasla/actions/runs/41525488523) passed. No branch/PR was created, rebased, force-pushed, merged or deployed.

## A. Root causes

1. The original prefix classifier returned no feature for `Archives.*`. The permission guard interpreted an unclassified name as noncommercial. Archive controllers use role attributes and the shared reporting scope guard, whose commercial check covered only a selected feature subset. Consequently a valid archive role/assignment permission could return an owned practice scope without authorizing the requested commercial operation.
2. Reservation capabilities treated any Doctor/Reception as a mutable provider. They did not intersect cancellation/reschedule/restore grants with accepted feature access. A ReadOnly Reservation Management subscription could receive true action flags even when the mutation endpoint refused the action. Linked Ticket navigation used the same incomplete reporting guard.
3. The audit found three related weaknesses: future unknown permissions inherited the fail-open classification; reception account-state permission was exempted with subscription account permissions; prescription PDF export inherited Prescription instead of the approved export feature. Prefix seeding also unnecessarily classified account, platform catalog and patient-only rights as commercial metadata.

## B. Implemented behavior

- Four exact existing archive permission constants map to `TICKET_QUEUE` or `RESERVATION_MANAGEMENT`, all ReadOnly. Permission identities/order are unchanged.
- The shared reporting guard validates account activity, existing requested permission, actor/governance and owned/delegated practice scope, then authorizes the requested commercial permission. It calculates effective permissions from the same batch result; a filtered array alone is never admission.
- The subscription permission guard checks the current stored actor grant, a reviewed exact classification, matching database mapping and accepted snapshot. Unknown, absent or inconsistent metadata denies Doctor/Reception access. Account/onboarding/privacy exceptions are explicit. Patient, Guardian, catalog-manager and SuperAdmin policies remain separate.
- Permission evaluation batches mapping and snapshot reads per operation and groups equivalent feature checks. It introduces no global cache of patient or medical data.
- Historical archive access uses accepted temporal coverage. Paid/trial resource creation must be within inclusive start/exclusive end. Legacy coverage can include older owned resources and is truncated at paid supersession. Actual-resource reads prove ownership and creation time; existing relationship/closed-practice rules still apply.
- Archive filtering happens in SQL before ordering, pagination and cursor construction. Current Full Platform read access retains its owned history. An active Booking Only account can use historical Ticket coverage only for proven old resources/filtered collections. It receives no general Ticket/Queue access or new mutation right. Scope bindings include coverage intervals; linked Ticket IDs are filtered independently.
- The same resource proof reaches archived detail fallbacks, closed-practice reads and linked navigation. Audit collections and details filter operational sources/identifiers through their accepted read scopes before fetching protected records.
- Reservation capability responses batch current commercial checks and retain existing lifecycle, cutoff, restore and archive rules. Reception assignment grants are resolved together and cannot be enlarged by a plan. Patient capabilities use Patient/Guardian grants and policies. Replays calculate current flags. Archived records always advertise false mutations; valid ReadOnly reservations use `Subscription.AccessLevelInsufficient` for blocked cancellation/rescheduling.
- Existing operational continuity remains resource-specific. The queue checks its selected Ticket under the original lock; past access never permits a selectable post-expiry commitment. Existing pre-expiry reservation/clinical commitments remain safely completable when their existing policies permit it.

The [authorization entry-point audit](docs/subscriptions/authorization-coverage.md) records every reviewed feature area and its actual shared/direct guard. The [API contract](docs/subscriptions/API-CONTRACT.md) documents capability flags, precedence and stable blocked reasons.

## C. Permission coverage

| Classification | Permissions |
|---|---:|
| Commercial Feature | 137 |
| Universal Patient | 24 |
| SuperAdmin / platform catalog governance | 114 |
| Authentication / onboarding / account | 19 |
| Privacy / consent / relationship | 39 |
| Internal / system audit | 2 |
| Total inspected | 335 |
| Unresolved classifications | 0 |

The complete [inventory](docs/subscriptions/permission-coverage.csv) includes exact names, all 335 stable GUIDs, access level, actor types, enforcement locations/source references, reasons and original PR #36 mappings. Shared Patient submission/booking-eligibility permissions retain actor-specific universal Patient behavior.

Automated coverage enumerates `PermissionNames.All`, requires exact inventory coverage, validates feature/access metadata, freezes every existing GUID, verifies database mappings/duplicates and tests unknown names within existing and future prefixes. Future permissions require deliberate review. Cross-practice comparison and advanced priority/quota still have their separate operation-specific checks; a general Reports/Segments permission is insufficient.

## D. Verification

The machine-readable [current evidence](docs/subscriptions/pr36-test-evidence.json) records every local run and reconciles discovered integration cases against their latest result. No uncompleted monolithic run is reported as full-suite success.

| Check | Result |
|---|---|
| Restore | Passed |
| Release build | Zero warnings and errors |
| Unit tests | 428 passed |
| Architecture tests | 26 passed |
| BuildingBlock foundation and frozen main API compatibility | 323 passed |
| Existing focused subscription/archive/historical regression run | 52 passed |
| Complete integration coverage | All 453 discovered cases accounted for: 452 passed across exhaustive batches/isolated runs, one existing opt-in browser smoke test skipped; zero uncovered or unresolved failures |
| All subscription integration cases | 60 passed, including 27 new cases |
| Final rebuilt mapping/archive run | 9 passed; includes accepted feature access after catalog metadata retirement |
| Changed-grant idempotent replay checks | 2 passed |
| Real SQL concurrency, capacity and change races | Complete 60-case SQL concurrency suite passed with the repository CI setting `xUnit.MaxParallelThreads=2` |
| Separate large discovery test | Passed, 11 minutes 39 seconds |
| Separate large patient-directory test | Passed, 5 minutes 52 seconds |
| Separate migration/backfill/backup/restore test | Passed, 1 minute 48 seconds |
| GitHub Actions on the hardening commit | [CI run 72 passed](https://github.com/LoaiSaberC4r/Wasla/actions/runs/41621780963) on `e9370390bbc62b2f60de46f12861e3b277cae105`: 428 unit, 392 integration, 60 SQL concurrency, 26 architecture and 323 BuildingBlock tests passed; one existing opt-in browser smoke skipped; zero warnings/errors. Latest branch checks: [PR #36](https://github.com/LoaiSaberC4r/Wasla/pull/36/checks). |

Focused new scenarios cover A01–A10 and B01–B11 through `SubscriptionArchiveAuthorizationTests`, `SubscriptionReservationCapabilityTests`, `SubscriptionPermissionMappingTests` and the existing archive/continuity suites. They exercise Booking Only denials and accurate flags, owned Reservation reads, expired Full Platform history, active Booking Only with proven old Full Platform coverage, legacy supersession, cross-Doctor/assignment concealment, account governance, Patient/Guardian rights, SuperAdmin metadata, current-grant revocation and replay behavior. Cross-feature HTTP tests cover Starter expenses/reports, Professional comparison/insights and Premium access.

SQL collection tests assert two authorized records across pages despite earlier/later unauthorized records, no unauthorized counts/linked IDs, covered/uncovered detail behavior, and bounded reads: at most 35 commands and no per-row growth when page size changes. Existing archive tests retain watermark/cursor and permission-revocation checks. Both SQLite and SQL Server test catalog creation, reconstruction of all 184 original PR #36 mappings, repair to 137 reviewed mappings, repeat seeding, stable IDs and unchanged accepted terms.

Test-development notes are retained in the evidence. Initial new assertions were corrected to preserve the existing ProblemDetails `instance` echo of a caller-supplied URI while excluding protected DTO data from the remaining payload. The legacy fixture runs the existing operational-day closer before archiving, and queue denial uses an actual selectable post-expiry Ticket. One existing reconciliation case hit LocalDB `Persistence.Timeout` during login under parallel load; its isolated two-case rerun passed without product changes or weakened assertions. An initial default-parallel SQL concurrency run passed 54 and hit six timeout failures; the full 60-case suite passed with the existing CI concurrency limit of two. An overlapping rebuild hit a Windows apphost lock; the sequential Release build subsequently passed with zero warnings/errors. No failed initial run is presented as successful.

Commands (PowerShell; SQL runs set `WASLA_SQLSERVER_CONNECTION_STRING` to the dedicated disposable-test server):

```powershell
dotnet restore Wasla.sln
dotnet build Wasla.sln --configuration Release --no-restore
dotnet test tests/Wasla.Tests.Unit/Wasla.Tests.Unit.csproj --configuration Release --no-restore
dotnet test tests/Wasla.Tests.Architecture/Wasla.Tests.Architecture.csproj --configuration Release --no-restore
dotnet test tests/Wasla.Tests.Integration/Wasla.Tests.Integration.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Subscription"
dotnet test tests/Wasla.Tests.Integration/Wasla.Tests.Integration.csproj --configuration Release --no-build --no-restore --filter "Category=SQLServerConcurrency" -- xUnit.MaxParallelThreads=2
# The remaining integration methods run in exhaustive batches; the three large tests run individually.
git diff --check
git status
```

Local TRX files use `pr36-*.trx`. Per-process logs use `artifacts/pr36-*.log`; independent shared test sinks avoid cross-process file retention interference. Large tests can use an isolated copy of the Release test output to avoid rebuild/file-lock interference. No assertion or existing test was disabled.

## E. Database and deployment impact

There is no schema change, new migration or configuration change. No product, plan, plan-feature entitlement, price, limit, trial duration, payment gateway, accepted terms snapshot or subscription feature snapshot was changed.

Existing databases must rerun `SubscriptionCatalogSeeder` through the existing controlled seeding mechanism. It inserts four missing archive mappings, repairs prescription export, and removes 51 obsolete prefix-derived mappings for explicitly noncommercial permissions. Remaining mapping IDs are stable; all existing permission and role-permission identities remain intact. Seed replay is verified from empty catalogs and reconstructed PR #36 catalogs. Production startup migrations remain disabled. No production migration or deployment was performed.

## F. Merge recommendation

`READY_FOR_MERGE`. No unresolved P0/P1 findings remain in the reviewed scope. Local implementation, security review, build and exhaustive integration reconciliation are complete. [CI run 72](https://github.com/LoaiSaberC4r/Wasla/actions/runs/41621780963) passed on the functional hardening commit `e9370390bbc62b2f60de46f12861e3b277cae105`, with all 1,229 executed tests passing and one existing opt-in browser smoke skipped. This evidence update changes documentation only. Require the [latest PR #36 checks](https://github.com/LoaiSaberC4r/Wasla/pull/36/checks) to remain green before merging. Leave the PR unmerged for final review; do not deploy automatically.

## Changed-file review

| File | Change / reason | Related business rule |
|---|---|---|
| `PR36-PRE-MERGE-HARDENING-REPORT.md` | Record causes, changes, verification, reseeding and final recommendation. | Reviewed merge gate; no automatic merge/deployment. |
| `SOURCE-OF-TRUTH-SUBSCRIPTION-COMMERCIAL-ADDENDUM.md` | Append authorization clarifications without changing the approved matrix or values. | Preserve approved business rules. |
| `SUBSCRIPTION-COMMERCIAL-IMPLEMENTATION-REPORT.md` | Link this targeted hardening record while retaining original implementation evidence. | Accurate implementation/deployment history. |
| `docs/subscriptions/API-CONTRACT.md` | Document archive coverage, effective flags, blocked reasons and linked-ID rules. | Capabilities must match actual authorization; Patient/Guardian rights remain separate. |
| `docs/subscriptions/authorization-coverage.md` | Record the reviewed guard for every feature/entry-point area. | Commercial operations require backend enforcement. |
| `docs/subscriptions/permission-coverage.csv` | Inventory all 335 GUIDs, classifications, original/current mappings and source references. | Explicit deliberate mapping; no identity changes. |
| `docs/subscriptions/pr36-test-evidence.json` | Reconcile every discovered case and preserve run/CI outcomes. | No unrun or failed check represented as success. |
| `src/Wasla/Wasla.Application/Features/DoctorPatients/DoctorOwnHistoricalReadPolicy.cs` | Accept optional actual-resource context and forward it to the existing relationship policy. | Narrowly extend the existing historical policy; no parallel authorization system. |
| `src/Wasla/Wasla.Application/Features/DoctorPatients/DoctorPatientRelationshipPolicy.cs` | Forward optional actual-resource context to the shared subscription guard. | Existing role, activity, approval and relationship requirements remain mandatory. |
| `src/Wasla/Wasla.Application/Features/Reporting/ReportingContracts.cs` | Carry internal JSON-ignored read scopes and optional original-resource context. | Existing wire contract/privacy; shared authorization proof. |
| `src/Wasla/Wasla.Application/Features/Reservations/ReservationCapabilities.cs` | Resolve action grants together, intersect Reception delegation and shared commercial decisions; keep Patient policy separate. | Accurate capabilities without plan-name checks or repeated per-field entitlement loads. |
| `src/Wasla/Wasla.Application/Features/Reservations/ReservationManagement.cs` | Pass original resources on reads/mutations and apply authorization-aware flags/linked visibility on shared response paths. | ReadOnly means view only; lifecycle/cutoff/archive rules and continuity remain authoritative. |
| `src/Wasla/Wasla.Application/Features/Subscriptions/CommercialFeatures.cs` | Replace prefix guessing with exact reviewed metadata and exact archive constants. | Four existing archive mappings; unknown commercial names fail closed. |
| `src/Wasla/Wasla.Application/Features/Subscriptions/CommercialPermissionCatalog.cs` | Define all reviewed commercial/noncommercial classifications explicitly. | Future permissions require intentional review; universal rights remain separate. |
| `src/Wasla/Wasla.Application/Features/Subscriptions/SubscriptionEntitlements.cs` | Batch current grants, mappings and snapshots; return effective decisions and resource-bounded historical scopes. | Full versus ReadOnly, fail closed, governance and continuity. |
| `src/Wasla/Wasla.Application/Features/Subscriptions/SubscriptionReadScope.cs` | Define temporal proof and provider-translatable predicates. | Filter owned historical collections before counts/pagination; no N+1. |
| `src/Wasla/Wasla.Application/Features/Tickets/GetTicketDetails/GetTicketDetails.cs` | Carry the actual Ticket through historical admission and final rechecks. | Owned temporal proof plus existing relationship/privacy checks. |
| `src/Wasla/Wasla.Application/Features/Tickets/Shared/TicketContracts.cs` | Pass original resources into archive authorization; retain the selected-Ticket deferred queue check and existing live scope proof. | No blanket historical queue access; current stored grants and actual commitments. |
| `src/Wasla/Wasla.Application/Persistence/ISubscriptionDataStore.cs` | Add focused batch rules, history-window and original-read-resource projections. | Reuse existing scoped persistence; no new database context. |
| `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/ArchiveQueries.cs` | Apply SQL historical predicates before paging; bind cursor coverage and independently filter linked Ticket IDs. | No unauthorized records/counts/cursor identifiers; preserve watermarks. |
| `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/AuditExplorer.cs` | Filter operational audit sources/identifiers through accepted feature read scopes. | Audit metadata cannot bypass operational entitlements; existing audit rights still required. |
| `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/ReportingAccessGuard.cs` | Authorize the requested permission after actor/scope validation and calculate consistent effective permissions. | No return of a commercial scope on filtered-permissions metadata alone. |
| `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/Seeding/SubscriptionCatalogSeeder.cs` | Insert missing mappings, repair export mapping, remove obsolete noncommercial mappings, retain stable IDs. | Idempotent empty/upgrade seeding; accepted terms and entitlements unchanged. |
| `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/SubscriptionDataStore.cs` | Implement batch SQL projections and temporal windows, including legacy supersession and archive unions. | Accepted snapshot history and actual resource proof; no schema change. |
| `tests/Wasla.Tests.Integration/Phase12ArchiveTestServices.cs` | Adapt the existing component scope guard to optional resource context. | Retain isolated component tests and existing assertions. |
| `tests/Wasla.Tests.Integration/Phase13ApiFixture.cs` | Allow an optional command interceptor for request query-count assertions. | Verify bounded SQL without introducing separate test infrastructure. |
| `tests/Wasla.Tests.Integration/Phase18DoctorReportingApiTests.cs` | Adapt the existing fixed reporting guard to optional resource context. | Retain bounded reporting and scope regression assertions. |
| `tests/Wasla.Tests.Integration/SubscriptionArchiveAuthorizationTests.cs` | Exercise real SQL archives, temporal paging, legacy supersession, scope/privacy, governance, Guardian/Patient rights and bounded reads. | A01-A09; no data/identifier leakage or new work after expiry. |
| `tests/Wasla.Tests.Integration/SubscriptionEntitlementApiTests.cs` | Add Starter/Professional/Premium cross-feature HTTP regressions. | Reports do not imply comparison/insights; approved feature tiers unchanged. |
| `tests/Wasla.Tests.Integration/SubscriptionPermissionMappingTests.cs` | Reconstruct all original mappings, replay seeding on both providers, check missing metadata and accepted snapshot stability. | A10; empty/upgrade idempotency, stable IDs and immutable accepted entitlements. |
| `tests/Wasla.Tests.Integration/SubscriptionReservationCapabilityTests.cs` | Exercise Booking Only flags/denials, all Full Platform tiers, linked visibility, delegation, expired continuity and changed-grant replays. | B01-B11; Patient self-service remains independent. |
| `tests/Wasla.Tests.Integration/SubscriptionTestData.cs` | Adapt existing component guard protocol and add explicit commercial test arrangements. | Reuse existing test infrastructure; real API tests use production authorization. |
| `tests/Wasla.Tests.Unit/CommercialFeaturePermissionCoverageTests.cs` | Audit all classifications/GUIDs, exact mappings, unknown prefixes and temporal boundaries. | Future unmapped permissions must fail tests and authorization. |
