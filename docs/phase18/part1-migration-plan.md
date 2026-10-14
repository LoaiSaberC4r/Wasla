# Phase 18 migration plan

Target SQL Server/.NET10/EFCore10. Baseline migrations remain in order. No approved Part Two archive/purge table or existing clinical sharing gate is introduced. No appsettings is changed. Apply only with normal release/change control, verified backups and exposed-credential remediation.

| Migration | Purpose |
|---|---|
| 20261013113259_Phase18OperatingFinance | Plans/revisions/allocations, obligations/revisions, settlements/reversals/history/runs; ownership keys, constraints, unique occurrence/effective payment, immutable monetary triggers |
| 20261013133419_Phase18ReportsAndInsights | Insight follow-up/history, actual policy snapshots, captured grace-period nullable metadata, generated-system evidence and history revision links |
| 20261013135730_Phase18ReceptionHandovers | Handover roots/history, normalized same-Practice reservation/ticket links and restrictive keys |
| 20261013140941_Phase18AdministrativeInvestigations | Administrative case/history, normalized typed metadata links, status and link checks |
| 20261014084528_Phase18ExportAudit | Immutable export metadata outcome records |
| 20261014085648_Phase18ReportingReconciliation | Persisted policy refresh run health/progress |
| 20261014085853_Phase18DatabaseInvariants | Additional immutable policy/insight history, root no-delete and salary overlap/immutable ownership triggers |
| 20261014095416_Phase18ExportScopeSnapshot | Exact export authorization scope/window snapshot JSON; legacy rows have empty snapshot, never fabricated scope |

New relationships use Restrict deletion. Composite keys normalize Doctor/Practice/Reception and same-Practice linked resources. SQL trigger-backed tables disable unsafe SQL OUTPUT use in EF. Existing row versions and SQLite test row versions remain supported. Decimal/money/count/currency and positive amount checks, unique reversal/occurrence/current-settlement constraints supplement domain checks.

The Up path creates/adds schema; it does not purge or rewrite old financial/clinical facts. New nullable grace metadata and newly observed policy snapshots do not backfill historical truth. Permission seeding appends names without changing existing IDs. No existing Reception delegation is automatically backfilled. Review the generated idempotent SQL in `part1-migration.sql` with the target DBA, including restrictive keys and trigger permissions.

Rehearsal commands (connection comes from an approved environment/secret source):

```powershell
dotnet build Wasla.sln --no-restore
dotnet ef migrations has-pending-model-changes --project src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer --startup-project src/Wasla/Wasla.Api --no-build
dotnet ef migrations script 20261012142810_Phase16FamilyMedicalAccess --idempotent --project src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer --startup-project src/Wasla/Wasla.Api --no-build --output docs/phase18/part1-migration.sql
```

The script's baseline is the verified immediately preceding migration `20261012142810_Phase16FamilyMedicalAccess`; use the actual deployed `__EFMigrationsHistory` when preparing a production-specific script. The ReportsAndInsights Down path rejects system-authored financial history rather than inventing a zero-Guid actor or bypassing immutable-history protection.

Local integration tests create isolated `WaslaPhase13Tests_<guid>` databases under SQL Express, apply migrations and seed approved reference/permission data, exercise endpoints/concurrency/raw constraints, then remove those test databases. No deployed Wasla database is modified. The model drift check must be clean before rollout. See implementation report for executed test counts and migration rehearsal evidence.

Rollback: disable new workers/traffic, preserve all generated facts and export audit, restore application compatibility or restore an approved database backup as the release plan dictates. Down migrations drop new tables/columns and therefore destroy phase data; they are not a routine production rollback once records exist. No automatic down/purge is part of rollout.
