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
    [double]$MinimumRuleLayerRate = 0.80,
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot 'Coverage/CardReborn.Kernel.Tests/CardReborn.Kernel.Tests.csproj'
$results = Join-Path $PSScriptRoot 'Coverage/results'
$repoRoot = Split-Path $PSScriptRoot -Parent

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

# coverlet writes per-file names relative to <sources>; map them back onto the layer folders.
$layerMap = @{}
foreach ($layer in @('0_Core', '1_Domain', '2_Application')) {
    $dir = Join-Path $repoRoot ("Assets/_Project/{0}" -f $layer)
    if (-not (Test-Path $dir)) { continue }
    foreach ($file in Get-ChildItem -Path $dir -Recurse -File -Filter '*.cs') {
        $layerMap[$file.Name] = $layer
    }
}

function Get-LayerOfFile {
    param([string]$FileName)

    foreach ($layer in @('0_Core', '1_Domain', '2_Application')) {
        if ($FileName -match $layer) { return $layer }
    }

    $leaf = Split-Path $FileName -Leaf
    if ($layerMap.ContainsKey($leaf)) { return $layerMap[$leaf] }
    return ''
}

$layerTotal = @{ '0_Core' = 0; '1_Domain' = 0; '2_Application' = 0 }
$layerCovered = @{ '0_Core' = 0; '1_Domain' = 0; '2_Application' = 0 }
$perFile = @()

foreach ($class in $report.coverage.packages.package.classes.class) {
    $fileName = [string]$class.filename
    $layer = Get-LayerOfFile -FileName $fileName
    if ($layer -eq '') { continue }

    $fileTotal = 0
    $fileCovered = 0
    foreach ($line in $class.lines.line) {
        $fileTotal++
        if ([int]$line.hits -gt 0) { $fileCovered++ }
    }

    $layerTotal[$layer] += $fileTotal
    $layerCovered[$layer] += $fileCovered
    $rate = if ($fileTotal -gt 0) { $fileCovered / $fileTotal } else { 1.0 }
    $perFile += [pscustomobject]@{
        Layer   = $layer
        File    = Split-Path $fileName -Leaf
        Covered = $fileCovered
        Lines   = $fileTotal
        Rate    = ('{0:P0}' -f $rate)
    }
}

Write-Host ''
Write-Host 'Kernel line coverage (per file):' -ForegroundColor Cyan
$perFile | Sort-Object Layer, File | Format-Table -AutoSize | Out-Host

$coreTotal = $layerTotal['0_Core']
$ruleTotal = $layerTotal['1_Domain'] + $layerTotal['2_Application']

if ($coreTotal -eq 0) {
    Write-Host 'FAIL - coverage report contains no 0_Core lines.' -ForegroundColor Red
    exit 2
}

$coreRate = $layerCovered['0_Core'] / $coreTotal
$ruleCovered = $layerCovered['1_Domain'] + $layerCovered['2_Application']
$ruleRate = if ($ruleTotal -gt 0) { $ruleCovered / $ruleTotal } else { 1.0 }

Write-Host ''
Write-Host ('0_Core          : {0}/{1} = {2:P2} (gate {3:P0})' -f $layerCovered['0_Core'], $coreTotal, $coreRate, $MinimumLineRate)
Write-Host ('Domain + App    : {0}/{1} = {2:P2} (gate {3:P0}, NFR-4)' -f $ruleCovered, $ruleTotal, $ruleRate, $MinimumRuleLayerRate)

$gateOk = ($coreRate -ge $MinimumLineRate) -and ($ruleRate -ge $MinimumRuleLayerRate) -and ($testExitCode -eq 0)

Write-Host ''
if ($gateOk) {
    Write-Host 'PASS - kernel tests green and coverage gates satisfied.' -ForegroundColor Green
    exit 0
}

if ($testExitCode -ne 0) {
    Write-Host ("FAIL - dotnet test exit code {0}." -f $testExitCode) -ForegroundColor Red
}
if ($coreRate -lt $MinimumLineRate) {
    Write-Host 'FAIL - 0_Core coverage is below its threshold.' -ForegroundColor Red
}
if ($ruleRate -lt $MinimumRuleLayerRate) {
    Write-Host 'FAIL - Domain + Application coverage is below its threshold (NFR-4).' -ForegroundColor Red
}

exit 1
