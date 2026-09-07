$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

dotnet new uninstall $root 2>$null | Out-Null
dotnet new install $root

dotnet new list buildingblock-sln
