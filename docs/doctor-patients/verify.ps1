param(
    [string]$TestSqlConnectionString = 'Server=.\SQLEXPRESS;Integrated Security=True;TrustServerCertificate=True',
    [string]$ChromiumExecutablePath = 'C:\Program Files\Google\Chrome\Application\chrome.exe'
)

# The SQL fixtures create and remove isolated disposable databases. Use a test server.
# This script builds/tests/checks the model; it never applies a deployment migration.
$ErrorActionPreference = 'Stop'
$repoPath = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Set-Location -LiteralPath $repoPath
if (-not (Test-Path -LiteralPath $ChromiumExecutablePath -PathType Leaf)) {
    throw 'An installed Chromium executable is required to verify actual PDF rendering.'
}
$resultsPath = Join-Path $repoPath '_local-data/doctor-patients-validation/reproduced-results'
New-Item -ItemType Directory -Path $resultsPath -Force | Out-Null
$env:WASLA_SQLSERVER_CONNECTION_STRING = $TestSqlConnectionString
$env:BUILDINGBLOCK_SQLSERVER_CONNECTION_STRING = $TestSqlConnectionString
$env:BUILDINGBLOCK_SQLSERVER_ALLOW_DATABASE_CREATE = 'true'
$env:WASLA_CHROMIUM_PATH = $ChromiumExecutablePath
$env:WASLA_EXPORT_QA_DIRECTORY = Split-Path $resultsPath -Parent
$env:WASLA_CONTRACT_OUTPUT_DIRECTORY = $env:WASLA_EXPORT_QA_DIRECTORY
# Process-local test logging only; avoids contention across WebApplicationFactories.
$env:Serilog__WriteTo__1__Args__configure__0__Args__shared = 'true'

dotnet build Wasla.sln -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Solution build failed.' }
$projects = [ordered]@{
    unit = 'tests/Wasla.Tests.Unit/Wasla.Tests.Unit.csproj'
    architecture = 'tests/Wasla.Tests.Architecture/Wasla.Tests.Architecture.csproj'
    buildingblock = 'src/BuildingBlock/BuildingBlock.Tests/BuildingBlock.Tests.csproj'
    integration = 'tests/Wasla.Tests.Integration/Wasla.Tests.Integration.csproj'
}
foreach ($entry in $projects.GetEnumerator()) {
    $testArgs = @('test', $entry.Value, '--no-build', '--no-restore', '--logger',
        "trx;LogFileName=$($entry.Key).trx", '--results-directory', $resultsPath, '-v', 'normal')
    & dotnet @testArgs
    if ($LASTEXITCODE -ne 0) { throw "$($entry.Key) tests failed; inspect the TRX before retrying." }
}
dotnet ef migrations has-pending-model-changes --no-build `
    --project src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer `
    --startup-project src/Wasla/Wasla.Api --context WaslaDbContext
if ($LASTEXITCODE -ne 0) { throw 'EF model consistency check failed.' }
git diff --check
if ($LASTEXITCODE -ne 0) { throw 'Git whitespace check failed.' }
