#requires -Version 5.1
<#
.SYNOPSIS
    CardReborn local gate check (M0-T7).

.DESCRIPTION
    Static scan that answers "does this code violate the architecture rules?"
    before Unity is even opened. Implements the automatable parts of:
      - Docs/03 section 1  (ironclad rules 2/3/7/8/10/11)
      - Docs/02 section 5  (quality gate)
      - Docs/04 section 5/6 (review checklist)

    Checks:
      R1  Kernel decoupling : 0_Core / 1_Domain / 2_Application / 2_Network
                              must not reference UnityEngine or Unity-only APIs
      R2  Editor API isolation: runtime layers must not use UnityEditor / AssetDatabase
      R3  No implicit lookup : no GameObject.Find / FindAnyObjectByType /
                              MonoSingleton / ServiceLocator
      R4  Logging standard   : no Debug.Log under Assets/_Project (use GameLog)
      R5  Size limit         : single file <= 300 lines

    NOTE: This file is intentionally ASCII-only. Windows PowerShell 5.1 reads
    BOM-less files as ANSI, which corrupts non-ASCII text and can break parsing.

    Compile and tests are performed by Unity (Test Runner); this script is
    static analysis only.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File Tools/check.ps1
#>
[CmdletBinding()]
param(
    # Report violations but always return exit code 0.
    [switch]$ReportOnly
)

$ErrorActionPreference = 'Stop'

$projectRoot   = Split-Path -Parent $PSScriptRoot
$projectPath   = Join-Path $projectRoot 'Assets/_Project'
$maxFileLines  = 300

# ---------------------------------------------------------------- scan scope --

# R1: layers that must stay free of Unity entirely (server-shareable kernel).
$pureLayers = @('0_Core', '1_Domain', '2_Application', '2_Network')

# R2/R3: everything that ships in a runtime build (editor tooling excluded).
$runtimeLayers = @(
    '0_Core', '1_Domain', '2_Application', '2_Network',
    '3_Infrastructure', '4_Presentation', '5_Bootstrap'
)

$unityPatterns = @(
    @{ Name = 'using UnityEngine';       Regex = 'using\s+UnityEngine' },
    @{ Name = 'UnityEngine.* type';      Regex = '\bUnityEngine\.' },
    @{ Name = 'Debug.Log';               Regex = '\bDebug\.(Log|LogWarning|LogError|LogException)' },
    @{ Name = 'Mathf';                   Regex = '\bMathf\.' },
    @{ Name = 'JsonUtility';             Regex = '\bJsonUtility\b' },
    @{ Name = 'ScriptableObject';        Regex = '\bScriptableObject\b' },
    @{ Name = 'UnityEngine.Random';      Regex = '\bUnityEngine\.Random\b' },
    @{ Name = 'DateTime.Now/UtcNow';     Regex = '\bDateTime\.(Now|UtcNow)\b' },
    @{ Name = 'Unity Time.*';            Regex = '\bTime\.(time|deltaTime|realtimeSinceStartup|fixedTime)\b' }
)

$editorPatterns = @(
    @{ Name = 'UnityEditor';   Regex = 'using\s+UnityEditor|\bUnityEditor\.' },
    @{ Name = 'AssetDatabase'; Regex = '\bAssetDatabase\b' }
)

$forbiddenPatterns = @(
    @{ Name = 'GameObject.Find';     Regex = '\bGameObject\.Find' },
    @{ Name = 'FindAnyObjectByType'; Regex = '\bFindAnyObjectByType\b' },
    @{ Name = 'FindObjectOfType';    Regex = '\bFindObjectOfType\b' },
    @{ Name = 'MonoSingleton';       Regex = '\bMonoSingleton\b' },
    @{ Name = 'ServiceLocator';      Regex = '\bServiceLocator\b' }
)

$logPatterns = @(
    @{ Name = 'Debug.Log'; Regex = '\bDebug\.(Log|LogWarning|LogError|LogFormat|LogException)' }
)

$ruleTitles = @{
    'R1' = 'Kernel decoupling (no Unity API in Core/Domain/Application/Network)'
    'R2' = 'Editor API isolation (no UnityEditor/AssetDatabase in runtime)'
    'R3' = 'No implicit lookup or global singleton'
    'R4' = 'Logging standard (use GameLog instead of Debug.Log)'
    'R5' = 'Size limit (single file <= 300 lines)'
}

# ------------------------------------------------------------- helper funcs --

$script:violations = New-Object System.Collections.Generic.List[object]

function Get-CsFiles {
    param([string[]]$LayerNames)

    $files = @()
    foreach ($layer in $LayerNames) {
        $dir = if ($layer -eq '.') { $projectPath } else { Join-Path $projectPath $layer }
        if (Test-Path $dir) {
            $files += Get-ChildItem -Path $dir -Recurse -File -Filter '*.cs' -ErrorAction SilentlyContinue
        }
    }
    return $files
}

function Test-Patterns {
    param(
        [System.IO.FileInfo[]]$Files,
        [array]$Patterns,
        [string]$RuleId
    )

    foreach ($file in $Files) {
        $lines = Get-Content -LiteralPath $file.FullName
        for ($i = 0; $i -lt $lines.Count; $i++) {
            foreach ($p in $Patterns) {
                if ($lines[$i] -match $p.Regex) {
                    $script:violations.Add([pscustomobject]@{
                        Rule   = $RuleId
                        Title  = $ruleTitles[$RuleId]
                        Detail = $p.Name
                        File   = Resolve-Path -Relative $file.FullName
                        Line   = ($i + 1)
                        Text   = $lines[$i].Trim()
                    })
                }
            }
        }
    }
}

# -------------------------------------------------------------------- run ----

Write-Host ''
Write-Host 'CardReborn - local gate check' -ForegroundColor Cyan
Write-Host "Project root: $projectRoot"
Write-Host ''

if (-not (Test-Path $projectPath)) {
    Write-Host "Assets/_Project not found: $projectPath" -ForegroundColor Red
    exit 2
}

$pureFiles      = Get-CsFiles $pureLayers
$runtimeFiles   = Get-CsFiles $runtimeLayers
$allFiles       = Get-CsFiles @('.')

Write-Host ("Scanned: {0} kernel file(s), {1} runtime file(s), {2} total" -f `
    $pureFiles.Count, $runtimeFiles.Count, $allFiles.Count)
Write-Host ''

Test-Patterns -Files $pureFiles    -Patterns $unityPatterns     -RuleId 'R1'
Test-Patterns -Files $runtimeFiles -Patterns $editorPatterns    -RuleId 'R2'
Test-Patterns -Files $runtimeFiles -Patterns $forbiddenPatterns -RuleId 'R3'
Test-Patterns -Files $allFiles     -Patterns $logPatterns       -RuleId 'R4'

foreach ($file in $allFiles) {
    $count = (Get-Content -LiteralPath $file.FullName).Count
    if ($count -gt $maxFileLines) {
        $script:violations.Add([pscustomobject]@{
            Rule   = 'R5'
            Title  = $ruleTitles['R5']
            Detail = "file has $count lines (limit $maxFileLines)"
            File   = Resolve-Path -Relative $file.FullName
            Line   = 1
            Text   = ''
        })
    }
}

# ----------------------------------------------------------------- result ----

$ordered = $script:violations | Sort-Object Rule, File, Line

if ($ordered.Count -eq 0) {
    Write-Host 'PASS - no violations found.' -ForegroundColor Green
    Write-Host ''
    Write-Host 'Compile and tests run inside Unity: Window > General > Test Runner.' -ForegroundColor DarkGray
    exit 0
}

Write-Host ("FAIL - {0} violation(s):" -f $ordered.Count) -ForegroundColor Red
Write-Host ''
$ordered | Format-Table -AutoSize -Property `
    @{ n = 'Rule';   e = { $_.Rule } },
    @{ n = 'Issue';  e = { $_.Detail } },
    @{ n = 'File';   e = { $_.File } },
    @{ n = 'Line';   e = { $_.Line } },
    @{ n = 'Source'; e = { $_.Text } }

Write-Host ''
Write-Host 'Summary by rule:' -ForegroundColor Yellow
$ordered | Group-Object Rule | Sort-Object Name | ForEach-Object {
    Write-Host ("  {0}  {1}  -- {2} hit(s)" -f $_.Name, $ruleTitles[$_.Name], $_.Count)
}
Write-Host ''
Write-Host 'See Docs/03 section 1 (rules) and section 13 (forbidden list).' -ForegroundColor DarkGray

if ($ReportOnly) { exit 0 }
exit 1
