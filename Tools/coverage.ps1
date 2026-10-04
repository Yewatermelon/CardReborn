#requires -Version 5.1
<#
.SYNOPSIS
    Runs the off-Unity kernel tests with coverage and checks the M1 gate (M1-R2).

.DESCRIPTION
    Uses Tools/Coverage/CardReborn.Coverage.csproj to compile Assets/_Project/0_Core
    together with the EditMode tests, runs them with dotnet test + coverlet, then reports
    line coverage for 0_Core and fails when it is below the threshold (default 90%).

    This runs without Unity, which also proves the kernel is Unity-free (FR-13.2/13.3).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File Tools/coverage.ps1
    powershell -ExecutionPolicy Bypass -File Tools/coverage.ps1 -MinimumLineRate 0.95
#>
[CmdletBinding()]
param(
    [double]$MinimumLineRate = 0.90,
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot 'Coverage/CardReborn.Kernel.Tests/CardReborn.Kernel.Tests.csproj'
$results = Join-Path $PSScriptRoot 'Coverage/results'

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

if (Test-Path $results) {
    Remove-Item -LiteralPath $results -Recurse -Force
}

New-Item -ItemType Directory -Path $results -Force | Out-Null

Write-Host ''
Write-Host 'CardReborn - off-Unity kernel tests + coverage' -ForegroundColor Cyan
Write-Host ("Project: {0}" -f $project)
Write-Host ''

$testArguments = @('test', $project, '--collect:XPlat Code Coverage', '--results-directory', $results, '--nologo', '-v', 'minimal')
if ($NoRestore) {
    $testArguments += '--no-restore'
}

& dotnet $testArguments
$testExitCode = $LASTEXITCODE

$cobertura = Get-ChildItem -Path $results -Recurse -Filter 'coverage.cobertura.xml' -ErrorAction SilentlyContinue |
    Select-Object -First 1

if (-not $cobertura) {
    Write-Host 'FAIL - no coverage report was produced.' -ForegroundColor Red
    exit 2
}

[xml]$report = Get-Content -LiteralPath $cobertura.FullName -Raw

# coverlet writes per-file names relative to <sources>; the kernel library links 0_Core.
$sourceRoots = @($report.coverage.sources.source) -join ';'
$coversKernel = $sourceRoots -match '0_Core'

if (-not $coversKernel) {
    Write-Host ("WARN - coverage sources do not point at 0_Core: {0}" -f $sourceRoots) -ForegroundColor Yellow
}

$totalLines = 0
$coveredLines = 0
$perFile = @()

foreach ($class in $report.coverage.packages.package.classes.class) {
    if (-not $coversKernel) { continue }

    $fileName = [string]$class.filename

    $fileTotal = 0
    $fileCovered = 0
    foreach ($line in $class.lines.line) {
        $fileTotal++
        if ([int]$line.hits -gt 0) { $fileCovered++ }
    }

    $totalLines += $fileTotal
    $coveredLines += $fileCovered
    $rate = if ($fileTotal -gt 0) { $fileCovered / $fileTotal } else { 1.0 }
    $perFile += [pscustomobject]@{
        File    = Split-Path $fileName -Leaf
        Covered = $fileCovered
        Lines   = $fileTotal
        Rate    = ('{0:P0}' -f $rate)
    }
}

Write-Host ''
Write-Host 'Card.Core line coverage (per file):' -ForegroundColor Cyan
$perFile | Sort-Object File | Format-Table -AutoSize | Out-Host

if ($totalLines -eq 0) {
    Write-Host 'FAIL - coverage report contains no 0_Core lines.' -ForegroundColor Red
    exit 2
}

$overall = $coveredLines / $totalLines
$summary = 'Card.Core line coverage: {0}/{1} = {2:P2} (threshold {3:P0})' -f $coveredLines, $totalLines, $overall, $MinimumLineRate

Write-Host ''
if ($overall -ge $MinimumLineRate -and $testExitCode -eq 0) {
    Write-Host $summary -ForegroundColor Green
    Write-Host 'PASS - kernel tests green and coverage gate satisfied.' -ForegroundColor Green
    exit 0
}

Write-Host $summary -ForegroundColor Red
if ($testExitCode -ne 0) {
    Write-Host ("FAIL - dotnet test exit code {0}." -f $testExitCode) -ForegroundColor Red
}
else {
    Write-Host 'FAIL - coverage is below the threshold.' -ForegroundColor Red
}

exit 1
