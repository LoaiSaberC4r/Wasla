# Business traceability

Authority: owner attachment WAS-P18.2-MASTER including Annex A, superseding Annex B v1.1 and Annex C; repository Phase10–15 and Part One addenda. Original master v1.2 text was not found, including in ignored workspace inputs and attachment storage. Original master §§35/73/88/91 and DEC-005/006/007/008/009/011/012 could not be independently rechecked. BuildingBlock source was reviewed directly; standalone BuildingBlock/QControl exports were unavailable. Phase16 PROPOSED files are not treated as independently approved policy; existing tested clinical/family policies remain authoritative.

| Problem / actor / outcome | Approved rule | Implementation / test boundary |
|---|---|---|
| Operational growth / System / smaller live stores | Annex A §§3–6; DEC-038 relocation exception | Permanent registries, constrained archive schema, per-group transactions, readiness gate and bounded durable progress |
| Identity continuity / all actors | DEC-035/042/047; Annex A §3 | Stable GUID/reference/Practice-day-number, lifetime registry uniqueness and idempotency FKs |
| Daily closure / System / preserve open visit | DEC-033/045; Annex A §4 | Observed effective schedule/timezone evidence; audited revisions before close; immutable closed snapshots; InProgress remains active |
| Returning Patient / Reception / new legitimate admission | Annex A §§5/7 | Closed NoShow retains status but releases operational queue lease; original status-only unique index needed technical adaptation |
| Historical retrieval / Doctor, Reception, Patient | Annex A §§7/10 | Storage-aware detail permissions, current ownership/delegation/self/family checks; archive-only grant does not authorize active detail |
| Refund continuity / financial actors | DEC-049–055; Annex A §8 | Original Paid ledger remains; one full Refund for Cancelled never-started Ticket, independent finance rights |
| Clinical continuity / Doctor, eligible Patient | DEC-056–068; Phase14–16 policies | Encounter/outputs stay separate; completed source linkage uses historical Ticket facts; current-treatment queries remain active-only; no sharing gate change |
| Historical truth / reporting actors | Part One §§1/3/8/9; Annex A §9 | Read-once active/archive unions preserve facts, business dates, source histories and AsOfUtc; protected export model is reused |
| Audit evidence / permitted staff/admin | Annex A §§10–11 | Immutable source federation and new archive/control/closure/read metadata; encrypted source keys; no PHI/free-text/financial amounts in general audit |
| Metadata administration / SuperAdmin | DEC-053; Annex A §§8/10 | Dedicated redacted archive DTOs; restricted sensitive metadata is explicit, reason-gated and not a Root/default bypass |

The 38 numbered route/DTO contracts are **implementation contracts**, not an owner-approved fixed DDL. Registry direction, indexes, liveness flag, cursor watermark, event-key cryptography, batch limits and work checkpoints are technical choices documented and verified against the code. Reception default grants are global only; the three new assignment permissions require explicit Doctor delegation and are never backfilled into old assignments.

DEFERRED: record disposal/purge, numeric retention, anonymization, unarchive, manual archive/start/backfill/repair API, subscription/commission, new notifications, cross-midnight schedules and cross-Doctor clinical sharing. No production activation or legal approval is claimed.

OPEN / held safely: an observed open day whose edited schedule has zero effective periods or an invalid DST end cannot acquire an invented new end. Its evidence is invalidated with metadata audit; automatic closure/movement is held until defensible evidence exists. Closed snapshots remain unchanged. Pre-clinical completed legacy Tickets without verifiable encounter applicability remain integrity exceptions rather than receiving fabricated encounters.
