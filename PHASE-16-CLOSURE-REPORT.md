# Wasla — Phase 16 final closure report

**2026-10-24 update:** the owner approved final parent/Guardian business rules.
The old all-adult eligibility interpretation in the historical report below is
superseded. Current implementation and verification are recorded in the
[final gap closure report](PHASE-16-FINAL-GAP-IMPLEMENTATION-REPORT.md),
[API diff](PHASE-16-FINAL-GAP-API-DIFF.md) and
[business incorporation addendum](SOURCE-OF-TRUTH-PHASE-16-FINAL-BUSINESS-ADDENDUM.md).
Business approval does not grant legal/clinical or Production authorization.

---

Historical 2026-10-15 engineering report follows; its test counts describe that
earlier source revision, not the 2026-10-24 change.

Date: 2026-10-15 (Africa/Cairo). Branch: `codex/phase16-final-closure`.
Reviewed baseline: `564a1e2ed54cdd4a8826a1150c438d9f782ccdf3`.

## Closure decision

**Engineering verification PASS; base Guardian flag activated. Formal Phase 16
closure and Production release remain BLOCKED.** This is a gap-closure change over the existing implementation.
It does not replace the approved Doctor-to-Doctor or Family workflows.

The full authoritative `Healthcare-Platform-Master-Source-of-Truth-v1.2-Updated`
is referenced by README but absent from the checkout, including the local
Phase 16 artifact search. A connected Atlassian search for its exact title was
denied (403; app not installed on that instance). Its location was requested.
This report cannot certify consistency with unseen Master decisions or adopt
either proposed addendum. The user's task supplies the product rules verified
below; it does not supply jurisdiction-specific policy or signed approvals.

The current governing implementation inventory is the Phase 16 API contract,
Family/medical-record policies and workflows, clinical read projections, their
tests, base/environment configuration, rollout documentation and the two proposed
Phase 16 addenda. Earlier implementation/activation reports remain historical
evidence; this report and the updated rollout describe current status.

## Changes completed

1. Enabled `MedicalRecord:GuardianSharingEnabled` in base appsettings after the
   existing Phase 16 integration suite passed. Cross-doctor enablement is retained.
   No approval/adoption switch, legal reference, proof taxonomy, eligibility,
   permission or patient link was fabricated to make the flag usable.
2. Closed the legacy patient diagnosis-note exposure. The existing
   `/api/v1/encounters/mine/{encounterId}` response keeps its
   `currentActiveDiagnoses` contract but redacts nullable `notes` values in the
   database projection. Doctor details still return private diagnosis notes.
   New Phase 16 patient/dependent/shared DTOs already exclude those fields.
3. Added configuration-bound activation tests for Father, Mother, Guardian and
   LegalGuardian, including a real SQL Server case, plus denial matrices for
   current permission/account/link/relationship/eligibility/policy/age changes,
   proof review, and restoration after eligibility revocation. Added exact safe
   patient access-log shape and cross-patient isolation assertions.
4. Updated the route status, Family migration inventory, rollout, README and
   historical-report pointers. The deployment template explicitly keeps both
   Family policy approval/adoption switches false.

Production code changes are limited to one sharing flag and the legacy patient
read projection. No domain, workflow, endpoint, permission, migration, package,
booking, claim/merge or emergency-access implementation was redesigned.

## Effective configuration

| Configuration | Cross-doctor flag | Guardian flag | Family medical policy |
|---|---|---|---|
| Base appsettings | true | true | Unapproved, unadopted; required signed-policy fields unset |
| Development | inherits true | inherits true | Requires explicit approved policy configuration |
| Production | false | false | Requires separate signed deployment configuration |
| Staging | false | false | Requires separate signed deployment configuration |
| `.env.example` | false | false | Approved=false; MasterSourceOfTruthAdopted=false |

**Flag enablement alone does not make dependent records accessible.** Under the
shipped base policy all six dependent routes fail closed. A configured test-only
policy proves the real API can serve authorized records after individual
eligibility review. These fixture values are not supplied in base configuration
or presented as legal approval. Any custom deployment environment must explicitly
disable both flags until its own release gates are satisfied.

## Guardian end-to-end verification

The existing `FamilyMedicalAccessPolicy` is the single clinical authorization
policy for all six routes. Each direct read/download obtains current scope,
and record/media readers recheck it before releasing the response or stream.
The dependent listing loads and reconfirms current eligibility.

| Route | Verified behavior |
|---|---|
| P16-24 `GET /families/mine/medical-record-dependents` | Only currently eligible children; denied children omitted; policy/actor failures return safe 404 |
| P16-25 `GET .../medical-record/overview` | Current patient-safe completed outputs and capabilities |
| P16-26 `GET .../medical-record/timeline` | Safe current output timeline and protected cursor |
| P16-27 `GET .../medical-record/encounters/{encounterId}` | Authorized child's completed encounter; private notes excluded |
| P16-28 `GET .../medical-record/items/{resourceType}/{sourceId}` | Current typed output; source/patient/version checks retained |
| P16-29 `GET .../attachments/{attachmentId}/content` | Current private attachment; no-store, nosniff, fresh authorization |

All routes use `/api/v1` and the dependent prefix documented in
[the exact route inventory](PHASE-16-API-CHANGES.md).

Required predicates are cumulative: active non-first-login Patient account,
current JWT and database permission, verified PatientAccountLink, verified
Father/Mother/Guardian/LegalGuardian and Child memberships in the same active
Family, age below 16 in the configured policy timezone, Approved eligibility
bound to the current family-member IDs and current permitted policy/basis, and
no active restriction. Relationship proof must be present, current and of an
allowed type before eligibility approval. Pending/rejected/revoked eligibility
and membership alone confer no medical authorization.

Tests deny subsequent JSON reads and attachment downloads after policy approval
or adoption removal, changed version/basis, invalid timezone, eligibility or
permission revocation, account deactivation/unlinking, inactive family/member,
unverified relationship or changed adult role. The birthday check allows access
one tick before local midnight and denies all five direct routes at age 16;
the child also disappears from the eligible listing. Restriction approval blocks
new reads immediately. Restoration lifts only that restriction; revoked
eligibility still denies every read. Family membership and under-18 booking
remain independent, with no automatic patient merge or privilege escalation.
The legacy domain `PatientAccessPolicy.CanAccessDependentMedicalData` predicate
is not called by a clinical route; none of these six endpoints substitutes it
for `IFamilyMedicalAccessPolicy` or uses booking authority as a medical grant.

## Approved Doctor-to-Doctor behavior preserved

The owning Doctor retains their own encounter access, including Hidden visits.
A different treating Doctor requires the exact active Ticket and Encounter;
Visible completed history is permitted, and Hidden content requires a separate
request approved by the source Doctor. The source Doctor rejects or revokes;
the Patient has no approval/rejection step. Completion ends access and expires
the grant; a new Ticket requires its own request. Visibility revision changes
invalidate old approvals. Reception and SuperAdmin have no clinical bypass.
Existing SQL lifecycle, decision/completion races, private media, role/permission,
opaque token and owner-visibility tests remain in the verification scope.

## Privacy and governance review

| Decision area | Engineering disposition | Outstanding authority / release gate |
|---|---|---|
| Cross-doctor consent and Hidden requests | Approved task flow retained; no Patient decision stage or group/admin bypass added | Master consistency/adoption and privacy sign-off cannot be certified without the authoritative source |
| Guardian eligibility and proof | Same active verified Family, under 16, current Approved entitlement, current proof at approval, restrictions and per-read checks enforced; admin review is metadata/proof only | Approved jurisdiction, effective timezone, legal basis, document taxonomy, consent/minor confidential categories, age-transition rights and appeals |
| Critical information in Hidden records | No critical-alert exception or leakage introduced; Hidden content remains behind the source-Doctor grant | Clinical authority must approve any future alert mechanism and confidentiality boundary; clinical safety sign-off remains open |
| Emergency access | Unavailable: no break-glass endpoint, permission or policy bypass implemented | Separate explicit approval and scoped design required before introduction |
| Patient access-log disclosure | Own Phase 16 metadata only: entryId, actorCategory, action, occurredAtUtc; no reasons, actor identity, target IDs, correlation IDs or clinical payload; cross-patient access isolated | Approval of scope/retention/disclosure and any historical-source aggregation; the current endpoint is not a complete historical source-module access export |
| Audit retention and sensitive data | Immutable application histories, metadata-only clinical read audit, fail-closed clinical audit writes, private attachment access and log redaction retained; no purge duration invented | Signed audit/evidence retention, legal hold/deletion, storage encryption, key protection/recovery and operational access controls |
| Patient claim/deduplication | Current verified links rechecked; no fuzzy matching, automatic merge or Family escalation | Phase 5 claim/proof/revocation/matching risk acceptance and formal closure evidence |
| Existing patient clinical DTOs / private Notes | Legacy diagnosis leak fixed; Doctor-private values preserved; patient prescriptions use dedicated safe DTOs; Lab/Radiology patient mapping removes internal post-visit/correction/void reasons | No remaining private Notes exposure found in the inspected Phase 16-dependent projections; broader minor-data classifications still require signed policy |

No legal duration, jurisdiction, proof acceptance rule or clinical safety approval
is inferred from passing tests. Evidence files remain private and revisioned;
request histories are immutable. Retention and encryption requirements need
deployment evidence, not assertions in this report.

## Verification evidence

Final results after all code/test changes:

| Verification | Actual result |
|---|---|
| `dotnet build Wasla.sln --configuration Release --no-restore -v quiet` | PASS; 0 warnings, 0 errors |
| Integration filter `FullyQualifiedName~Phase16\|FullyQualifiedName~Phase13ClinicalApiTests` | **75 passed, 0 failed, 0 skipped**, 3m47s; 65 Phase 16 + 10 legacy clinical cases, including **21 real SQL Server cases** |
| Unit filter `FullyQualifiedName~Phase16` | **13 passed**, 0 failed/skipped |
| Existing Family unit filter `FullyQualifiedName~Phase5FamilyDomainTests` | **8 passed**, 0 failed/skipped |
| Architecture filter `FullyQualifiedName~Phase16` | **1 passed**, 0 failed/skipped |
| `git diff --check` | PASS |

Final scope: **97 distinct cases**, all passed; earlier repeated runs are not
added to that count. This was the relevant Phase 16/Family/clinical regression
scope, not the full Wasla or standalone BuildingBlock test suite. No remote CI
run or Production capacity certification is claimed.

Before base activation, all **39 existing Phase 16 integration cases passed**,
including the existing Family routes and SQL scenarios. An intermediate run
failed one newly added assertion because it used `diagnoses` instead of the
legacy `currentActiveDiagnoses` field. The test was corrected, SQL coverage added,
and the final 75-case integration run above passed. No security assertion was
relaxed and no authorization requirement removed.

Runtime: .NET SDK 10.0.401. SQL fixtures used real SQL Server LocalDB with Windows
integrated authentication and isolated, migrated disposable test databases.
`WASLA_SQLSERVER_CONNECTION_STRING` was set only for the test process. No app or
Production database was migrated or seeded. The synthetic SQL scenario verified
1,000 visits, 10 stable pages of 100 and 500 strictly redacted placeholders;
first page was **812.7 ms** in this run, with the SQL plan retained. That is local
synthetic evidence, not a production load guarantee.

TRX files are local ignored artifacts under `TestResults/phase16-closure`:
`phase16-final.trx`, `phase16-unit.trx`, `family-domain.trx`, and
`phase16-architecture.trx`. Historical baseline/intermediate results are kept
separately. SQL plan and performance evidence are under
`_local-data/phase16-validation/timeline-plan.xml` and `performance.txt`.

Reproduce after setting the disposable SQL Server test connection:

```powershell
dotnet build Wasla.sln --configuration Release --no-restore -v quiet
dotnet test tests/Wasla.Tests.Integration/Wasla.Tests.Integration.csproj --configuration Release --no-build --filter 'FullyQualifiedName~Phase16|FullyQualifiedName~Phase13ClinicalApiTests' --logger 'trx;LogFileName=phase16-final.trx' --results-directory TestResults/phase16-closure -v quiet
dotnet test tests/Wasla.Tests.Unit/Wasla.Tests.Unit.csproj --configuration Release --no-build --filter 'FullyQualifiedName~Phase16|FullyQualifiedName~Phase5FamilyDomainTests' -v quiet
dotnet test tests/Wasla.Tests.Architecture/Wasla.Tests.Architecture.csproj --configuration Release --no-build --filter 'FullyQualifiedName~Phase16' -v quiet
```

## Remaining closure gates

1. Provide and review the authoritative Master v1.2 and approved Phase 16 decision
   log; resolve any conflict and formally adopt the applicable addenda.
2. Record signed privacy/clinical decisions for minor confidentiality and proof,
   Hidden critical information, patient access-log scope and audit/evidence
   retention, plus Phase 5 identity claim/deduplication closure or risk acceptance.
3. For a separately approved deployment: provision real policy references and
   individually reviewed eligibility; stable secret/key ring with protection
   and recovery; reviewed backups/migrations/security seed; compatible instances;
   audit availability, representative capacity and recovery verification.
4. Verify effective deployment overrides and obtain the deployment owner's
   release approval before enabling either Production/Staging flag.

No push, PR, deployment, production migration/seed, real eligibility approval or
external message was performed. Full formal Phase 16 DONE cannot be claimed
until the listed authority and deployment gates are satisfied.
