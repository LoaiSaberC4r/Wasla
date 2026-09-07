param(
    [string]$OutputPath = "BuildingBlock-All-Code.txt",
    [switch]$ExcludeMigrations
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$excludedSegments = @("bin", "obj", ".vs", "TestResults", "Generated")
$excludedPatterns = @("*.g.cs", "*.AssemblyInfo.cs")

$files = Get-ChildItem -Path (Join-Path $root "src/BuildingBlock") -Filter *.cs -Recurse |
    Where-Object {
        $file = $_
        $path = $file.FullName
        $hasExcludedSegment = $excludedSegments | Where-Object {
            $path -match "[\/]$([regex]::Escape($_))[\/]"
        }
        $matchesExcludedPattern = $excludedPatterns | Where-Object {
            $file.Name -like $_
        }

        -not $hasExcludedSegment -and
        -not $matchesExcludedPattern -and
        (-not $ExcludeMigrations -or $path -notmatch "[\/]Migrations[\/]")
    } |
    Sort-Object FullName

$builder = [System.Text.StringBuilder]::new()
foreach ($file in $files) {
    [void]$builder.AppendLine("// ==================== $($file.FullName) ====================")
    [void]$builder.AppendLine((Get-Content $file.FullName -Raw))
}

[System.IO.File]::WriteAllText(
    (Join-Path $root $OutputPath),
    $builder.ToString(),
    [System.Text.UTF8Encoding]::new($false))
