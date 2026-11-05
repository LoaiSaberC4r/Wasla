param([string] $SqlServer = '(localdb)\MSSQLLocalDB')
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$rehearsalDb = 'WaslaCommercialV12Script_' + [Guid]::NewGuid().ToString('N')
if ($rehearsalDb -notmatch '^WaslaCommercialV12Script_[0-9a-f]{32}$') { throw 'Unexpected rehearsal database name.' }
$report = Join-Path $repoRoot 'artifacts/commercial-v1.2-script-rehearsal-verified.log'
New-Item -ItemType Directory -Path (Split-Path $report) -Force | Out-Null
Push-Location $repoRoot
try {
    # This helper creates and deletes only its own GUID-named disposable database.
    # Build Release first. Existing databases are never migration targets.
    $connection = "Server=$SqlServer;Database=$rehearsalDb;Integrated Security=true;TrustServerCertificate=true"
    dotnet ef database update 20261102190031_CommercialSubscriptionFoundation --connection $connection --no-build --configuration Release --project src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer --startup-project src/Wasla/Wasla.Api *> $report
    if ($LASTEXITCODE -ne 0) { throw 'Could not establish the foundation schema; inspect the rehearsal log.' }
    sqlcmd -S $SqlServer -E -I -b -d $rehearsalDb -i docs/subscriptions/sql/V1.2-RECONCILIATION.sql >> $report
    if ($LASTEXITCODE -ne 0) { throw 'Before-cutover reconciliation failed.' }
    sqlcmd -S $SqlServer -E -I -b -d $rehearsalDb -i docs/subscriptions/sql/V1.2-MIGRATION.sql >> $report
    if ($LASTEXITCODE -ne 0) { throw 'First commercial script execution failed.' }
    sqlcmd -S $SqlServer -E -I -b -d $rehearsalDb -i docs/subscriptions/sql/V1.2-MIGRATION.sql >> $report
    if ($LASTEXITCODE -ne 0) { throw 'Idempotent replay failed.' }
    sqlcmd -S $SqlServer -E -I -b -d $rehearsalDb -i docs/subscriptions/sql/V1.2-RECONCILIATION.sql >> $report
    if ($LASTEXITCODE -ne 0) { throw 'After-cutover reconciliation failed.' }
    sqlcmd -S $SqlServer -E -I -b -d $rehearsalDb -Q "IF NOT EXISTS(SELECT 1 FROM sys.triggers WHERE name=N'TR_DoctorSubscriptions_NoPeriodOverlap') THROW 51064,'Overlap trigger missing.',1; IF (SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId=N'20261105111042_CommercialContractsAndPracticeAllocations')<>1 THROW 51065,'Unexpected migration count.',1; SELECT N'Script applied twice; one migration and overlap trigger verified.' AS RehearsalResult;" >> $report
    if ($LASTEXITCODE -ne 0) { throw 'Final schema verification failed.' }
    Get-Content $report -Tail 8
} finally {
    sqlcmd -S $SqlServer -E -I -b -d master -Q "IF DB_ID(N'$rehearsalDb') IS NOT NULL BEGIN ALTER DATABASE [$rehearsalDb] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$rehearsalDb]; END;" >> $report
    Pop-Location
}
