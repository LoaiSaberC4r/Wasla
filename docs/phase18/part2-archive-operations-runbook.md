# Archive operations runbook

Base configuration enables physical archival (`Archive:Enabled=true`, `Archive:ReadModelsVerified=true`, batch size 50, interval 300 seconds) after isolated local SQL verification. `appsettings.Production.json` explicitly disables both flags until that environment's separate migration, backup/recovery, security and operator checks are approved. This change does not deploy or enable Production. There is no manual archive/start/repair/unarchive/purge API or disposal policy.

## Local activation evidence — 2026-10-15

- Used a newly allocated synthetic database on local SQL Server Express; no existing database was downgraded or reset.
- Applied expansion `20261014170532` over a nonempty baseline. The existing maintenance executable's `populate-registries` command registered four identities, then reported zero; SQL independently confirmed zero missing identities. Applied and confirmed cutover `20261014171141`, audit keys `20261014173651` and progress `20261014191455`, with no pending migrations.
- Health API returned `Ready=true` before and after two instances of the existing background worker ran with batch size 50 and interval 300. Exactly three Tickets (two Completed, one Cancelled) and their linked converted Reservation were archived once. Source/archive scalar snapshots, history, call attempts, original IDs, reservation reference and grouped movement audit/checkpoint linkage matched. Three Payments, one Refund and two Medical Encounters remained unchanged; existing archive/history and encounter APIs remained readable, with zero integrity exceptions.
- Focused checks passed: 13 Archive integration cases, 13 eligibility/day-closure domain cases, the separate maintenance/background-worker verification, and three Development/Staging/Production configuration checks. The migration rehearsal additionally moved 1,000 synthetic Tickets in 20 restarted batches, preserved history IDs and financial totals, confirmed the archive paging index in the SQL plan, and passed local checksum backup/verification/restore.
- Local TRX evidence is under `TestResults/ArchiveActivationEvidence/` (Git-ignored). This verifies local activation only; Production remains disabled pending its own approvals.

## Controlled preparation

1. Explicitly override `Archive:Enabled=false` and `Archive:ReadModelsVerified=false` in an unverified target environment. Set `DatabaseInitialization:ApplyMigrationsOnStartup=false` and `ApplySeedingOnStartup=false` for controlled staging preparation; the existing Production guard already refuses startup migrations. Development's existing convenience default is not a controlled cutover. Inventory the actual staging FK graph, source integrity/volume and backups. Compare the dated Part Two change-control addendum with the authoritative owner sources. Stop movement if original scopes, financial/clinical facts, schema targets or historical day evidence cannot be proven.
2. Quiesce operational writers for the controlled schema preparation window. Apply expansion migration `20261014170532` only. Existing older executables can still read the old operational tables; the final executable requires all four migrations and must not be used as an expansion-only application server.
3. Build/run `tools/Wasla.ArchiveMaintenance` with a separately configured `WASLA_ARCHIVE_MAINTENANCE_CONNECTION` targeting the verified staging database. `populate-registries` performs at most four batches of 250 per invocation, Reservation identities before Ticket identities, under transaction-owned population application lock. Rerun until zero and verify complete coverage. This utility does not migrate, move/delete rows, run hosted services or enable archival.
4. Apply guarded FK cutover `20261014171141`, persistent audit key `20261014173651`, and progress/liveness expansion `20261014191455`. The cutover fails before replacing constraints if any source identity is missing. Every new persistent target uses enforced Restrict/NO ACTION. Review generated SQL; do not use a destructive Down as rollback.
5. Deploy the compatible read version with the feature OFF. Run SQL schema/location reconciliation, scope/privacy tests, admission/queue/reservation/clinical/finance/history/report/export parity and migration recovery rehearsals. Verify all required FKs, uniqueness and archive indexes are present and trusted. Persist/share Data Protection keys across instances; protect the database audit-key secret as database security material.
6. Only after the operator has accepted staging evidence and its own environment/release gates, configure ReadModelsVerified and Enabled. This implementation does not authorize production activation. The automatic scheduler handles normal closure and bounded historical backfill; default batch size 50 (validated 1–250), interval 300 seconds (validated 30–3600). These are tunable technical limits, not retention periods or an SLA.

## Startup error: Archive.RegistryPopulationRequired

SQL error 51000 with `Archive.RegistryPopulationRequired` means the expansion is present but existing Ticket/Reservation identities have not all been registered. A successful solution build does not perform this data preparation. The guarded FK migration stops before replacing constraints, and restarting the API repeats the same error.

Stop application writers and use the controlled preparation sequence above. For a verified local development database, run these commands from the repository root:

```powershell
$env:ConnectionStrings__Database = '<verified local development connection string>'
$env:WASLA_ARCHIVE_MAINTENANCE_CONNECTION = $env:ConnectionStrings__Database

# Only if expansion is still pending; never target an earlier migration to downgrade.
dotnet ef database update 20261014170532_Phase18PartTwoArchiveExpansion --project src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer --startup-project src/Wasla/Wasla.Api

# Repeat this bounded command until a successful invocation reports zero.
dotnet run --project tools/Wasla.ArchiveMaintenance/Wasla.ArchiveMaintenance.csproj -- populate-registries

# After zero is reported, apply the guarded cutover and remaining migrations.
dotnet ef database update --project src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer --startup-project src/Wasla/Wasla.Api
```

If expansion is already applied, begin with population. Keep writers stopped until the final update succeeds, then restart the API with archival disabled. Population is additive, transactional and restartable; it does not change business status, move operational records, or enable archival. Preserve the coverage guard and migration history. Production preparation remains the separately authorized operator procedure described above.

## Health and control

P18.2-16 returns enablement/readiness, pause state, last attempts/success, current mode, exception count and control rowVersion. Run IDs are omitted unless the actor can read runs. P18.2-17→18 and 19→20 are the run/exception producer→detail flows. P18.2-35 supplies exact bilingual modes/outcomes/failure codes.

Pause/Resume require current ViewHealth plus their distinct action grant. Obtain rowVersion from Health; send a nonempty reason and unique Idempotency-Key. Stale versions return 409: refresh Health. Matching Actor+Operation+key/payload returns the saved result/version; changed payload conflicts. Resume additionally requires feature enablement and readiness. Pause waits for an in-flight transaction's shared control lock, then stops new acquisitions. Do not kill an active SQL transaction as a control mechanism.

The worker holds a same-database session-owned exclusive `Wasla:ArchiveWorker` lease; each movement uses a serializable transaction, existing Ticket patient→Practice/day locks, existing Reservation patient→Practice/day resources where applicable, and a shared transaction-owned control guard. A lost worker session lease stops further work. State/cursor/sequence/moved counts and safe audit commit with the group. SQL deadlock/timeout/lock-timeout gets at most three bounded attempts. Failed transactions roll back source/children/pointers/counts. A later cycle marks interrupted runs Failed and resumes durable checkpoints; eligibility is revalidated rather than replaying a mutation.

## Integrity holds and recovery

`MissingPayment`, `PaymentMismatch`, `EncounterMismatch`, `HistoryMismatch`, `ReservationMismatch`, `IdentityMismatch`, `UnverifiedDayClosure` and `DatabaseFailure` are safe actionable classifications. Exception details contain entity/Practice/run IDs and failure code/time, not a patient body or SQL text. Deduplication uses entity/type/code/source-state fingerprint. A held record stays in its authoritative source; automatic later cycles re-evaluate it without a public repair/retry override.

Use the existing approved correction flows for correctable business data, then let automatic processing revalidate. Missing or contradictory historical evidence is an operator investigation, not permission to synthesize a Payment, encounter, schedule, status or history. Invalidated schedule evidence holds closure/archive; an unproven NoShow day also blocks restore/cancellation that could create a refund. No fabricated historical operational snapshot is stored. Legacy calendar closure is accepted only under the original valid timezone and a strictly past original business date, and records use actual processing timestamps for new events.

On readiness failure, disable movement, investigate schema/coverage/location and keep the compatible readers. Never drop registries, financial/clinical FKs or archive tables. After any moves, the old dbo-only executable is incompatible. Recovery requires the separately rehearsed backup/PITR procedure, preserving all clinical/finance/audit changes; there is no automatic reverse migration or unarchive operation.

The `Wasla.Archive` meter emits scanned/moved/excluded/integrity-exception counters and batch duration with safe mode/entity/failure labels. The background activity source supplies trace context; durable run/event correlation IDs connect metadata. Health remains the authoritative persisted readiness/last-cycle view. Configure collectors/alerts for failed or stalled cycles and growing integrity holds without logging record bodies.

Local synthetic SQL backup/restore evidence does not replace a deployment-environment backup, recovery time/point objective, load/lock assessment, privacy review or formal owner-source incorporation. Those release gates must be accepted in the target environment before production activation.
