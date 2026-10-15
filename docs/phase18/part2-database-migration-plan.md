# Controlled migration plan

1. S01 expansion creates permanent registries, immutable archive copies, observed operational days, worker control/run/integrity/audit/idempotency tables. No dbo operational records are deleted. Default Archive:Enabled=false; no auto-activation from migrations.
2. S02 creates registries for every new Ticket/Reservation in its creation transaction. Bounded population inserts missing old identities under SQL application lock, respecting Reservation-before-Ticket order. Persisted identities themselves are restart checkpoints; missing identities are selected on each cycle. The cutover migration refuses incomplete coverage; run population with the expansion version first on nonempty databases.
3. S02 FK cutover replaces every persistent operational reference with registry FKs in one controlled migration transaction, WITH CHECK. Composite handover scope and all unique indexes remain enforced. Active/archive parents also reference their registry identity. Down refuses automatic destructive reversal.
4. Deploy unified readers and historical union; run schema/readiness/reconciliation and API/privacy/finance/report parity tests while feature remains OFF.
5. Only an explicit validated nonproduction/deployment configuration enables automatic worker. Readiness rechecks each cycle and resume; failure keeps source rows untouched. Pause prevents new acquisitions. No public start/backfill/archive/repair/purge route.
6. Rehearse realistic volume, SQL query plans/lock duration, transient errors, parallel instances, process interruption and backup/restore before production enablement. Record actual evidence separately; unrun gates remain blockers.

Same-database per-group relocation copies exact scalar columns, original history and attempt IDs, verifies counts, deletes source children/parents only after copy validation, updates both registry pointers, and commits safe audit together. Any failure rolls back. Payments/Refunds/medical outputs remain in dbo with enforced registry references. Source rowversion bytes are immutable archival evidence; archive does not generate a replacement rowversion. Business status remains unchanged.

Rollback means disable movement and retain compatible reads. After moves, a previous executable version reading only dbo is incompatible. Never drop registries/archive or claim a destructive Down is safe. Recovery/PITR requires a rehearsed approved operator procedure; this implementation performs no production backups, restores or migrations.

## Exact migration sequence

| Migration | Responsibility |
|---|---|
| `20261014170532_Phase18PartTwoArchiveExpansion` | Registries, complete constrained archive copies, operational evidence, control/run/exception/audit/request tables |
| `20261014171141_Phase18PartTwoRegistryForeignKeys` | Missing-coverage guard, enforced permanent FK cutover, active root identity FKs and composite handover targets |
| `20261014173651_Phase18PartTwoAuditKeys` | Persistent random authenticated event-key secret; no record movement |
| `20261014191455_Phase18PartTwoArchiveProgress` | Durable cursors/committed sequence, archive watermark, NoShow operational lease/backfill, immutable day evidence, control replay snapshot, safe correlation/failure fields, archive terminal constraints/indexes and singleton initialization |

Use a quiesced maintenance window: expansion only → bounded maintenance utility population until zero → guarded cutover and remaining expansions → final executable with both feature flags OFF → verification. The final EF model must not serve an expansion-only database. The standalone utility intentionally selects only baseline columns and inserts expansion-era registry columns, so it can run between migrations 1 and 2. This is a concrete controlled cutover, not an assertion of rolling compatibility with old writers after final schema adoption.

Readiness verifies expected FK targets/ordered columns, trust/NO ACTION and absence of surviving non-operational source references; expected registry/queue/archive unique and paging index keys; required trusted checks including physical archive terminal constraints; coverage, positive committed sequences, exactly-one physical location and linked registry co-location. Eligibility/financial/clinical/history checks run again inside each movement transaction. An ambiguous COMMIT acknowledgement resolves durable registry/location before retry/failure accounting.

The synthetic nonempty baseline rehearsal exercises a deliberately rejected premature cutover, repeated bounded population, all migrations twice, 1,000 historical relocations in 20 fresh-context batches, original history IDs/payment totals, indexed paging plan, COPY_ONLY/CHECKSUM backup, VERIFYONLY and restore to a separate newly named local database. These results are environment-specific evidence, not production activation approval. See the evidence report for exact runs, remaining coverage and target-environment release gates.
