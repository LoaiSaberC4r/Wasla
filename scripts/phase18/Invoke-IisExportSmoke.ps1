param(
    [Parameter(Mandatory)][uri]$BaseUri,
    [Parameter(Mandatory)][guid]$PrescriptionId,
    [int]$Version = 1,
    [Parameter(Mandatory)][guid]$PracticeId,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [ValidateSet('ar','en')][string]$Language = 'ar',
    [switch]$Landscape
)

$ErrorActionPreference = 'Stop'
if ($BaseUri.Scheme -ne 'https') { throw 'Use the existing HTTPS IIS endpoint.' }
$token = [Environment]::GetEnvironmentVariable('WASLA_SMOKE_DOCTOR_TOKEN', 'Process')
if ([string]::IsNullOrWhiteSpace($token)) { throw 'Supply a short-lived permitted Doctor token through WASLA_SMOKE_DOCTOR_TOKEN (process environment).' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$headers = @{ Authorization = "Bearer $token" }
$prefix = $BaseUri.AbsoluteUri.TrimEnd('/')
$wide = $Landscape.IsPresent.ToString().ToLowerInvariant()
$exports = @(
    @{ Name='prescription.pdf'; Mime='application/pdf'; Route="/api/v1/doctors/me/prescriptions/$PrescriptionId/versions/$Version/pdf?disposition=attachment" },
    @{ Name='report.pdf'; Mime='application/pdf'; Route="/api/v1/doctors/me/reports/queue/export?format=pdf&language=$Language&practiceId=$PracticeId&landscape=$wide" },
    @{ Name='report.xlsx'; Mime='application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'; Route="/api/v1/doctors/me/reports/queue/export?format=xlsx&language=$Language&practiceId=$PracticeId" }
)
foreach ($export in $exports) {
    $destination = Join-Path $OutputDirectory $export.Name
    $response = Invoke-WebRequest -Uri ($prefix + $export.Route) -Headers $headers -OutFile $destination -PassThru -UseBasicParsing
    if ($response.StatusCode -ne 200 -or $response.Headers['Content-Type'] -notlike "$($export.Mime)*") { throw "Unexpected status/content type for $($export.Name)." }
    if ($response.Headers['Cache-Control'] -notmatch 'no-store') { throw "Missing no-store for $($export.Name)." }
    $extension = [IO.Path]::GetExtension($export.Name)
    if ($response.Headers['Content-Disposition'] -notmatch ([regex]::Escape($extension))) { throw "Missing expected download filename for $($export.Name)." }
    $bytes = [IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $destination).Path)
    if ($extension -eq '.pdf') {
        if ($bytes.Length -lt 5 -or [Text.Encoding]::ASCII.GetString($bytes,0,4) -ne '%PDF') { throw "Invalid PDF: $($export.Name)." }
    }
    else {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $destination).Path)
        try {
            $entry = $archive.GetEntry('xl/workbook.xml')
            if ($null -eq $entry) { throw 'XLSX workbook.xml is missing.' }
            $reader = [IO.StreamReader]::new($entry.Open())
            try { [xml]$workbook = $reader.ReadToEnd() } finally { $reader.Dispose() }
            $names = @($workbook.workbook.sheets.sheet | ForEach-Object { $_.name })
            $expected = @('01 Summary','02 Filters and Quality','03 Trends','04 Breakdowns','05 Details','06 Charts','07 Comparisons')
            if (@(Compare-Object $expected $names).Count -ne 0) { throw 'Unexpected XLSX worksheets.' }
        }
        finally { $archive.Dispose() }
    }
    $hash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
    [pscustomobject]@{ File=$export.Name; Bytes=$bytes.Length; SHA256=$hash; Status='Verified download structure; open in PDF/Excel reader for content review' }
}
