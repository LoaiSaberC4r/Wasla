> Historical implementation checkpoint. Superseded by [CLOSURE-REPORT-2026-11-08.md](CLOSURE-REPORT-2026-11-08.md) and [owner decision v2](DECISION-2026-11-08-v2.md). Earlier disabled-feature and missing-policy statements describe that checkpoint, not the current Production target.

# Extension implementation checkpoint — epic still in progress

Branch: `codex/family-self-service-historical-documents`.
Draft review: https://github.com/LoaiSaberC4r/Wasla/pull/38.
The first foundation is committed as `07d3cbf`; these extensions are the next
reviewable slice. See API-HANDOFF-EXTENSIONS.md for complete added contracts.

## Added implementation

- Exact-authority dependent contacts/photo and private image read. Patient
  rowVersion, durable actor/operation/key replay, immutable change journal,
  last-primary-contact guard, no arbitrary linked-contact pointers. A linked child
  controls their own operational profile; proxy writes are withheld.
- Own and separately authorized dependent historical documents, immutable scanned
  versions/attachments, logical withdrawal, retained source history, bounded
  subject/actor quotas, private integrity-checked downloads and audited reads.
  Source remains PatientProvided/Unverified. No clinician or general admin access.
- HistoricalPatientDocument timeline subtype without EncounterId. Additive filter,
  SQL keyset paging, scope/version invalidation and unchanged official counters.
  Unknown dates retain original upload ordering. Doctor timelines exclude this source.
- Independent signed medical operation policy for Father/Mother, current exact
  clinical proof, restrictions and under16 policy-local date. Read permission and
  operational authority never become medical write grants.
- Child doctor-ordered Lab/Radiology submission and pending-only author withdrawal
  reuse existing request/submission/Doctor review. Minimal mutation receipts preserve
  actor UserId separately from child PatientId. Existing own endpoints remain own-only.
- Independently permissioned under18 dependent transaction/payment/refund reads,
  forced subject filtering, repeated scope checks and safe patient projections.
- Versioned intake metadata correction after requested modification. Old intake and
  evidence remain retained; duplicate intent checks repeat; approval uses only the
  current reviewed revision. Earlier revision-key replay retains its response.
- Independently computed workspace contacts/photo/receipt/upload/diagnostic flags.
- Batched permission/grant seeding preserving deterministic IDs and custom grants.

## Persistence and permissions

The original337 permission IDs and first13 family additions retain their positions.
The next18 append after them: contacts/photo/finance4, medical documents8, diagnostics6.
All31 new IDs are frozen in new-permissions.json; total368. Patient defaults and
explicit UniversalPatient classification are additive; no new Doctor/Reception or
SuperAdmin medical grant is introduced.

Generated migrations `20261108003144_FamilyDocumentsAndDependentProfile` and
`20261108004756_HistoricalTimelineAndIntakeRevisions` add six tables plus the intake
revision table and a derived timeline timestamp. All foreign keys are restrictive.
Historical versions/attachments/history/operation receipts and intake snapshots are
immutable. Down throws: flags can be disabled while retaining records. SQL timestamp
backfill derives from existing document date/original upload, never fabricates a
clinical event. MIGRATION.sql is the generated idempotent upgrade from the actual
pre-epic `20261105111042_CommercialContractsAndPracticeAllocations` migration.

## Validation evidence at this checkpoint

- Solution build passed with zero warnings/errors before the final paging adjustment.
- 451 unit tests and26 architecture tests passed, no failures/skips.
- 57 selected integration tests passed, no failures/skips, with native LocalDB enabled.
  Includes family/revocation/notification/subscription and existing diagnostic/contract
  regressions, plus new historical/diagnostic/profile/finance/intake journeys.
- Subsequent20-case API run passed18, including both custom security-seed replay
  tests; two version-page cases exposed a stale compilation snapshot. Source fix
  was verified against PDB checksums; a forced rebuild and rerun are pending here.
- EF reports no pending model changes. SQL generation uses the correct baseline ID.
  Superseded empty/stale migration attempts and incorrect script baseline ID are
  not claimed as successful validation.

Update TEST-EVIDENCE.json and this section with final rerun evidence before treating
this extension checkpoint as verified. The complete regression suite, populated
family-specific upgrade/restore rehearsal and storage fault injection remain open.

## Release boundaries and remaining work

HistoricalDocuments.Enabled and operational Production flags stay false. Production
cannot disable real scanning. Passive PDF policy rejects actions/forms/attachments/
encrypted/object-stream documents and escaped dangerous names; image dimensions
are bounded. This is a conservative input policy followed by signature validation
and scanning, not a general PDF sanitizer or medical verification.

Provision/verify scanner signatures, time/decompression limits and clean/unsafe/error
behavior. In particular, configure limit exceedance to produce unsafe verdicts;
ClamAV's [official configuration](https://github.com/Cisco-Talos/clamav/blob/main/etc/clamd.conf.sample)
documents AlertExceedsMax. Mock protocol/storage tests do not prove deployed health.

Remaining CODE: identity-correction review workflow; invitation/conflict-case
completeness; full capability/reason dashboard; dependent archived ticket read;
automated orphan reconciliation/metrics; remaining mutation/age/revocation and
storage/uncertain-commit fault coverage. Staged files are deliberately retained on
uncertain outcomes until references can be proven safely; no cleanup claim is made.

External release gates: signed/adopted medical policy and deployment approval;
active credential rotation/revocation and coordinated configuration remediation;
private storage/key ring/backup/restore/scanner provisioning and sanitized staging
rehearsal. No production/staging DB was queried or mutated. Secret values are omitted.
The complete epic is not done and no Production readiness is claimed.
