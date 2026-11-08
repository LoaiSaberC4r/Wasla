$ErrorActionPreference = 'Stop'
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw 'Run these path-validation scenarios on Windows.'
}
$prepareScript = Join-Path (Split-Path $PSScriptRoot -Parent) 'deploy/family-production/Prepare-PrivateStorage.ps1'
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('WaslaStoragePaths_' + [Guid]::NewGuid().ToString('N'))
$contentRoot = Join-Path $testRoot 'wwwroot/application'
$privateRoot = Join-Path $contentRoot 'App_Data/Wasla'
$media = Join-Path $privateRoot 'Media'
$keyRing = Join-Path $privateRoot 'DataProtection/KeyRing'
$webRoot = Join-Path $contentRoot 'public'

function Assert-PathValidation {
    param([string]$Name, [string]$MediaPath, [string]$KeysPath, [string]$ActualWebRoot, [bool]$Accept)
    $accepted = $false
    try {
        & $prepareScript -ContentRootPath $contentRoot -WebRootPath $ActualWebRoot `
            -PrivateMediaPath $MediaPath -KeyRingPath $KeysPath `
            -ApplicationIdentity 'unused-validation-only' -WhatIf | Out-Null
        $accepted = $true
    }
    catch { if ($Accept) { throw } }
    if ($accepted -ne $Accept) { throw "Path-validation scenario failed: $Name" }
    Write-Output "PASS: $Name"
}

# WhatIf exercises the real validation without provisioning directories or touching ACLs.
Assert-PathValidation 'wwwroot ancestor outside actual WebRoot' $media $keyRing $webRoot $true
Assert-PathValidation 'actual WebRoot descendant' (Join-Path $privateRoot 'public/Media') $keyRing (Join-Path $privateRoot 'public') $false
Assert-PathValidation 'target equals actual WebRoot' $media $keyRing $media $false
Assert-PathValidation 'target outside approved root' (Join-Path $contentRoot 'outside/Media') $keyRing $webRoot $false
Assert-PathValidation 'parent traversal escape' (Join-Path $privateRoot 'Media/../../../outside') $keyRing $webRoot $false
Assert-PathValidation 'similar WaslaBackup directory' (Join-Path $contentRoot 'App_Data/WaslaBackup/Media') $keyRing $webRoot $false
Assert-PathValidation 'invalid KeyRing target' $media (Join-Path $contentRoot 'outside/KeyRing') $webRoot $false
Assert-PathValidation 'Windows case-insensitive containment' $media.ToUpperInvariant() $keyRing.ToUpperInvariant() $webRoot $true
Assert-PathValidation 'WebRoot prefix is not a containing directory' $media $keyRing (Join-Path $privateRoot 'Med') $true
if (Test-Path -LiteralPath $testRoot) { throw 'Validation-only scenarios unexpectedly created storage directories.' }
