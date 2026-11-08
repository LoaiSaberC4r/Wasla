[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][string]$ContentRootPath,
    [Parameter(Mandatory)][string]$WebRootPath,
    [Parameter(Mandatory)][string]$KeyRingPath,
    [Parameter(Mandatory)][string]$PrivateMediaPath,
    [Parameter(Mandatory)][string]$ApplicationIdentity
)
$ErrorActionPreference = 'Stop'
$comparison = if ([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
foreach ($path in @($ContentRootPath, $WebRootPath, $KeyRingPath, $PrivateMediaPath)) {
    if ([string]::IsNullOrWhiteSpace($path) -or -not [IO.Path]::IsPathFullyQualified($path)) {
        throw 'Supply the actual absolute ContentRootPath, WebRootPath and private storage targets.'
    }
}
$contentRoot = [IO.Path]::GetFullPath($ContentRootPath)
$webRoot = [IO.Path]::GetFullPath($WebRootPath).TrimEnd([char[]]@('/', '\'))
$webRootPrefix = $webRoot + [IO.Path]::DirectorySeparatorChar
$privateRoot = [IO.Path]::GetFullPath((Join-Path $contentRoot 'App_Data/Wasla')).TrimEnd([char[]]@('/', '\'))
$privateRootPrefix = $privateRoot + [IO.Path]::DirectorySeparatorChar
# Validate both targets before creating directories or changing any ACL.
$resolvedTargets = foreach ($target in @($KeyRingPath, $PrivateMediaPath)) {
    $resolved = [IO.Path]::GetFullPath($target).TrimEnd([char[]]@('/', '\'))
    if (-not $resolved.StartsWith($privateRootPrefix, $comparison)) {
        throw 'Private storage targets must remain inside ContentRootPath/App_Data/Wasla.'
    }
    if ($resolved.Equals($webRoot, $comparison) -or $resolved.StartsWith($webRootPrefix, $comparison)) {
        throw 'Private storage targets cannot be equal to or inside the actual WebRootPath.'
    }
    $resolved
}
foreach ($resolved in $resolvedTargets) {
    if (-not $PSCmdlet.ShouldProcess($resolved, 'Provision private storage directory and permissions')) { continue }
    New-Item -ItemType Directory -Path $resolved -Force | Out-Null
    $acl = Get-Acl -LiteralPath $resolved
    $acl.SetAccessRuleProtection($true, $false)
    foreach ($entry in @(@('SYSTEM', 'FullControl'), @('BUILTIN\Administrators', 'FullControl'), @($ApplicationIdentity, 'Modify'))) {
        $rule = [Security.AccessControl.FileSystemAccessRule]::new($entry[0], $entry[1], 'ContainerInherit,ObjectInherit', 'None', 'Allow')
        $acl.AddAccessRule($rule)
    }
    Set-Acl -LiteralPath $resolved -AclObject $acl
}
if ($WhatIfPreference) { Write-Output 'Private storage paths validated; no directories or ACLs changed.' }
else { Write-Output 'Persistent private storage permissions prepared. Keep the relative App_Data paths in appsettings and provision the certificate separately.' }
