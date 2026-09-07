param(
    [switch]$IncludeSqlServerIntegration
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    dotnet --info
    dotnet tool restore
    dotnet restore Wasla.sln
    dotnet build Wasla.sln --configuration Release --no-restore

    if ($IncludeSqlServerIntegration) {
        dotnet test Wasla.sln --configuration Release --no-build
    }
    else {
        dotnet test Wasla.sln `
            --configuration Release `
            --no-build `
            --filter "Category!=SqlServerIntegration"
    }

    dotnet tool run dotnet-ef migrations list `
        --project src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer `
        --startup-project src/Wasla/Wasla.Api `
        --no-connect
}
finally {
    Pop-Location
}
