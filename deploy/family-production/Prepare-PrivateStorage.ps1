param(
    [Parameter(Mandatory)][string]$KeyRingPath,
    [Parameter(Mandatory)][string]$PrivateMediaPath,
    [Parameter(Mandatory)][string]$ApplicationIdentity
)
$ErrorActionPreference = 'Stop'
foreach ($target in @($KeyRingPath, $PrivateMediaPath)) {
    if (-not [IO.Path]::IsPathFullyQualified($target)) { throw 'Use the confirmed absolute persistent host paths.' }
    $resolved = [IO.Path]::GetFullPath($target)
    if ($resolved -match '[\\/]wwwroot([\\/]|$)') { throw 'Private files cannot be placed under wwwroot.' }
    New-Item -ItemType Directory -Path $resolved -Force | Out-Null
    $acl = Get-Acl -LiteralPath $resolved
    $acl.SetAccessRuleProtection($true, $false)
    foreach ($entry in @(@('SYSTEM', 'FullControl'), @('BUILTIN\Administrators', 'FullControl'), @($ApplicationIdentity, 'Modify'))) {
        $rule = [Security.AccessControl.FileSystemAccessRule]::new($entry[0], $entry[1], 'ContainerInherit,ObjectInherit', 'None', 'Allow')
        $acl.AddAccessRule($rule)
    }
    Set-Acl -LiteralPath $resolved -AclObject $acl
}
Write-Output 'Persistent private storage permissions prepared. Configure the actual paths and certificate in appsettings.Production.json.'
