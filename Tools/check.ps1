#requires -Version 5.1
<#
.SYNOPSIS
    CardReborn local gate check (M0-T7, hardened in M1-T9).

.DESCRIPTION
    Static scan that answers "does this code violate the architecture rules?"
    before Unity is opened. Implements the automatable parts of:
      - Docs/03 section 1    (ironclad rules 2/3/7/8/10/11)
      - Docs/03 section 5.9  (kernel decoupling: no Unity types, serialization, log, time)
      - Docs/03 section 2.3  (asmdef engine references and dependency direction)
      - Docs/02 section 5    (quality gate)
      - Docs/04 section 5/6  (review checklist)

    Checks:
      R1  Kernel decoupling   : 0_Core / 1_Domain / 2_Application / 2_Network must not use
                                UnityEngine types, Unity serialization, Unity logging,
                                UnityEngine.Random, system time or Unity Time
      R2  Editor API isolation: runtime assemblies must not use UnityEditor / AssetDatabase
      R3  No implicit lookup  : no GameObject.Find / FindAnyObjectByType / MonoSingleton /
                                ServiceLocator / mutable static Instance
      R4  Logging standard    : no Debug.Log under Assets/_Project (use GameLog)
      R5  Size limit          : single file <= 300 lines
      R6  asmdef guard        : kernel asmdefs must set noEngineReferences and must not
                                reference Unity assemblies; no upward layer references

    Comments and string literals are stripped before pattern matching, so prose that
    merely *mentions* a forbidden API is not reported (M1-R3), and word boundaries keep
    names such as System.Runtime.CompilerServices from matching "Time.".

    Compile and tests are performed by Unity (Test Runner); this script is static
    analysis only.

.NOTES
    This file is intentionally ASCII-only. Windows PowerShell 5.1 reads BOM-less files
    as ANSI, which corrupts non-ASCII text and can break parsing.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File Tools/check.ps1
    powershell -ExecutionPolicy Bypass -File Tools/check.ps1 -SelfTest
    powershell -ExecutionPolicy Bypass -File Tools/check.ps1 -ReportOnly
#>
[CmdletBinding()]
param(
    # Report violations but always return exit code 0.
    [switch]$ReportOnly,

    # Run the probe self-test instead of scanning the project (M1-T9).
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------- constants --

$MaxFileLines = 300

$KernelFolders = @('0_Core', '1_Domain', '2_Application', '2_Network')

$RuntimeFolders = @(
    '0_Core', '1_Domain', '2_Application', '2_Network',
    '3_Infrastructure', '4_Presentation', '5_Bootstrap'
)

# Lower value = lower layer; an assembly may only reference layers with value <= its own.
$LayerOrder = @{
    'Card.Core'           = 0
    'Card.Domain'         = 1
    'Card.Application'    = 2
    'Card.Network'        = 2
    'Card.Infrastructure' = 3
    'Card.Presentation'   = 4
    'Card.Bootstrap'      = 5
}

$Rules = @{
    'R1' = 'Kernel decoupling (no Unity API / serialization / log / time in Core, Domain, Application, Network)'
    'R2' = 'Editor API isolation (no UnityEditor/AssetDatabase in runtime assemblies)'
    'R3' = 'No implicit lookup or global singleton'
    'R4' = 'Logging standard (use GameLog instead of Debug.Log)'
    'R5' = 'Size limit (single file <= 300 lines)'
    'R6' = 'asmdef guard (kernel engine references disabled, no upward dependencies)'
}

$KernelPatterns = @(
    @{ Name = 'using UnityEngine';         Regex = 'using\s+UnityEngine\b' },
    @{ Name = 'UnityEngine.* type';        Regex = '\bUnityEngine\.' },
    @{ Name = 'Unity struct type';         Regex = '\b(Vector2|Vector3|Vector4|Quaternion|Color32|Color|Rect)\b' },
    @{ Name = 'Debug.Log';                 Regex = '\bDebug\.(Log|LogWarning|LogError|LogFormat|LogException)\b' },
    @{ Name = 'Mathf';                     Regex = '\bMathf\.' },
    @{ Name = 'JsonUtility';               Regex = '\bJsonUtility\b' },
    @{ Name = 'ScriptableObject';          Regex = '\bScriptableObject\b' },
    @{ Name = 'SerializeField';            Regex = '\[\s*SerializeField\s*\]' },
    @{ Name = 'UnityEngine.Random';        Regex = '\bUnityEngine\.Random\b' },
    @{ Name = 'DateTime.Now/UtcNow/Today'; Regex = '\bDateTime\.(Now|UtcNow|Today)\b' },
    @{ Name = 'Unity Time.*';              Regex = '\bTime\.(time|deltaTime|unscaledTime|realtimeSinceStartup|fixedTime|frameCount)\b' }
)

$EditorPatterns = @(
    @{ Name = 'UnityEditor';   Regex = 'using\s+UnityEditor\b|\bUnityEditor\.' },
    @{ Name = 'AssetDatabase'; Regex = '\bAssetDatabase\b' }
)

$LookupPatterns = @(
    @{ Name = 'GameObject.Find';         Regex = '\bGameObject\.Find' },
    @{ Name = 'FindAnyObjectByType';     Regex = '\bFindAnyObjectByType\b' },
    @{ Name = 'FindObjectOfType';        Regex = '\bFindObjectOfType\b' },
    @{ Name = 'MonoSingleton';           Regex = '\bMonoSingleton\b' },
    @{ Name = 'ServiceLocator';          Regex = '\bServiceLocator\b' },
    @{ Name = 'mutable static Instance'; Regex = '\bstatic\s+(?!readonly\b)[A-Za-z_][A-Za-z0-9_<>,\.\?\[\]]*\s+Instance\b' }
)

$LogPatterns = @(
    @{ Name = 'Debug.Log'; Regex = '\bDebug\.(Log|LogWarning|LogError|LogFormat|LogException)\b' }
)

# ------------------------------------------------------------- helper funcs --

function Get-RelativePath {
    param([string]$FullPath, [string]$RootPath)

    if ($FullPath.StartsWith($RootPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $FullPath.Substring($RootPath.Length).TrimStart('\', '/')
    }

    return $FullPath
}

function Get-TextLines {
    param([string]$Path)

    $text = [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
    $text = $text.Replace("`r`n", "`n").Replace("`r", "`n")
    return ,@($text -split "`n")
}

function Get-StrippedLines {
    param([string]$Path)

    $lines = Get-TextLines -Path $Path
    $stripped = [System.Text.StringBuilder]::new()
    $state = 'code'

    foreach ($line in $lines) {
        $i = 0
        while ($i -lt $line.Length) {
            $c = $line[$i]
            $n = if (($i + 1) -lt $line.Length) { $line[$i + 1] } else { [char]0 }

            if ($state -eq 'code') {
                if ($c -eq '/' -and $n -eq '/') { $state = 'line'; $i += 2; continue }
                if ($c -eq '/' -and $n -eq '*') { $state = 'block'; $i += 2; continue }
                if ($c -eq '@' -and $n -eq '"') { $state = 'verbatim'; $i += 2; continue }
                if ($c -eq '"') { $state = 'string'; $i++; continue }
                if ($c -eq "'") { $state = 'char'; $i++; continue }
                [void]$stripped.Append($c)
                $i++
                continue
            }

            if ($state -eq 'line') { break }

            if ($state -eq 'block') {
                if ($c -eq '*' -and $n -eq '/') { $state = 'code'; $i += 2; continue }
                $i++
                continue
            }

            if ($state -eq 'string') {
                if ($c -eq '\') { $i += 2; continue }
                if ($c -eq '"') { $state = 'code'; $i++; continue }
                $i++
                continue
            }

            if ($state -eq 'verbatim') {
                if ($c -eq '"' -and $n -eq '"') { $i += 2; continue }
                if ($c -eq '"') { $state = 'code'; $i++; continue }
                $i++
                continue
            }

            if ($c -eq '\') { $i += 2; continue }
            if ($c -eq "'") { $state = 'code'; $i++; continue }
            $i++
        }

        if ($state -eq 'line') { $state = 'code' }
        [void]$stripped.Append("`n")
    }

    $combined = $stripped.ToString().Replace("`r`n", "`n")
    return ,@($combined -split "`n")
}

function Add-Violation {
    param(
        $List,
        [string]$Rule,
        [string]$Detail,
        [string]$File,
        [int]$Line,
        [string]$Text
    )

    $List.Add([pscustomobject]@{
        Rule   = $Rule
        Title  = $Rules[$Rule]
        Detail = $Detail
        File   = $File
        Line   = $Line
        Text   = $Text
    })
}

function Get-FilesInFolders {
    param([string]$ProjectPath, [string[]]$Folders)

    $files = @()
    foreach ($folder in $Folders) {
        $dir = Join-Path $ProjectPath $folder
        if (Test-Path $dir) {
            $files += @(Get-ChildItem -Path $dir -Recurse -File -Filter '*.cs' -ErrorAction SilentlyContinue)
        }
    }

    return ,@($files)
}

function Test-Patterns {
    param(
        $List,
        [array]$Files,
        [array]$Patterns,
        [string]$RuleId,
        [string]$ProjectPath
    )

    foreach ($file in $Files) {
        $relative = Get-RelativePath -FullPath $file.FullName -RootPath $ProjectPath
        $lines = Get-StrippedLines -Path $file.FullName

        for ($i = 0; $i -lt $lines.Count; $i++) {
            foreach ($pattern in $Patterns) {
                if ($lines[$i] -match $pattern.Regex) {
                    Add-Violation -List $List -Rule $RuleId -Detail $pattern.Name `
                        -File $relative -Line ($i + 1) -Text $lines[$i].Trim()
                }
            }
        }
    }
}

function Test-Asmdefs {
    param($List, [string]$ProjectPath)

    foreach ($folder in $RuntimeFolders) {
        $dir = Join-Path $ProjectPath $folder
        if (-not (Test-Path $dir)) { continue }

        $asmdefs = @(Get-ChildItem -Path $dir -Recurse -File -Filter '*.asmdef' -ErrorAction SilentlyContinue)
        foreach ($asmdef in $asmdefs) {
            $relative = Get-RelativePath -FullPath $asmdef.FullName -RootPath $ProjectPath
            $json = Get-Content -LiteralPath $asmdef.FullName -Raw | ConvertFrom-Json
            $name = $json.name
            $isKernel = $KernelFolders -contains $folder

            if ($isKernel -and ($json.noEngineReferences -ne $true)) {
                Add-Violation -List $List -Rule 'R6' -Detail 'kernel assembly must set noEngineReferences: true' `
                    -File $relative -Line 1 -Text $name
            }

            foreach ($reference in @($json.references)) {
                if ([string]::IsNullOrEmpty($reference)) { continue }

                if ($isKernel -and ($reference -like 'Unity*')) {
                    Add-Violation -List $List -Rule 'R6' -Detail "kernel assembly must not reference '$reference'" `
                        -File $relative -Line 1 -Text $name
                }

                if (($reference -like 'Card.*') -and $LayerOrder.ContainsKey($reference) -and $LayerOrder.ContainsKey($name)) {
                    if ($LayerOrder[$reference] -gt $LayerOrder[$name]) {
                        Add-Violation -List $List -Rule 'R6' -Detail "upward dependency: $name -> $reference" `
                            -File $relative -Line 1 -Text $name
                    }
                }
            }
        }
    }
}

function Get-GateViolations {
    param([string]$ProjectPath, [string]$ProjectRoot)

    $violations = New-Object System.Collections.Generic.List[object]

    $kernelFiles = Get-FilesInFolders -ProjectPath $ProjectPath -Folders $KernelFolders
    $runtimeFiles = Get-FilesInFolders -ProjectPath $ProjectPath -Folders $RuntimeFolders
    $allFiles = @(Get-ChildItem -Path $ProjectPath -Recurse -File -Filter '*.cs' -ErrorAction SilentlyContinue)

    Test-Patterns -List $violations -Files $kernelFiles -Patterns $KernelPatterns -RuleId 'R1' -ProjectPath $ProjectPath
    Test-Patterns -List $violations -Files $runtimeFiles -Patterns $EditorPatterns -RuleId 'R2' -ProjectPath $ProjectPath
    Test-Patterns -List $violations -Files $runtimeFiles -Patterns $LookupPatterns -RuleId 'R3' -ProjectPath $ProjectPath
    Test-Patterns -List $violations -Files $allFiles -Patterns $LogPatterns -RuleId 'R4' -ProjectPath $ProjectPath

    foreach ($file in $allFiles) {
        $count = (Get-TextLines -Path $file.FullName).Count
        if ($count -gt $MaxFileLines) {
            Add-Violation -List $violations -Rule 'R5' `
                -Detail ("file has {0} lines (limit {1})" -f $count, $MaxFileLines) `
                -File (Get-RelativePath -FullPath $file.FullName -RootPath $ProjectPath) -Line 1 -Text ''
        }
    }

    Test-Asmdefs -List $violations -ProjectPath $ProjectPath

    return ,@($violations | Sort-Object Rule, File, Line)
}

function Write-GateReport {
    param($Violations, [string]$Header)

    Write-Host ''
    Write-Host $Header -ForegroundColor Cyan
    Write-Host ''

    if ($Violations.Count -eq 0) {
        Write-Host 'PASS - no violations found.' -ForegroundColor Green
        Write-Host ''
        Write-Host 'Compile and tests run inside Unity: Window > General > Test Runner.' -ForegroundColor DarkGray
        return 0
    }

    Write-Host ("FAIL - {0} violation(s):" -f $Violations.Count) -ForegroundColor Red
    Write-Host ''
    # Out-Host keeps the table out of the function's return value (the caller needs a scalar exit code).
    $Violations | Format-Table -AutoSize -Property `
        @{ n = 'Rule';   e = { $_.Rule } },
        @{ n = 'Issue';  e = { $_.Detail } },
        @{ n = 'File';   e = { $_.File } },
        @{ n = 'Line';   e = { $_.Line } },
        @{ n = 'Source'; e = { $_.Text } } | Out-Host

    Write-Host ''
    Write-Host 'Summary by rule:' -ForegroundColor Yellow
    $Violations | Group-Object Rule | Sort-Object Name | ForEach-Object {
        Write-Host ("  {0}  {1}  -- {2} hit(s)" -f $_.Name, $Rules[$_.Name], $_.Count)
    }
    Write-Host ''
    Write-Host 'See Docs/03 section 1 (rules), section 5.9 (kernel) and section 13 (forbidden list).' -ForegroundColor DarkGray
    return 1
}

# --------------------------------------------------------------- self test --

function Invoke-SelfTest {
    $root = Join-Path ([System.IO.Path]::GetTempPath()) ('CardRebornGateSelfTest_' + [Guid]::NewGuid().ToString('N'))
    $projectPath = Join-Path $root 'Assets/_Project'
    $coreDir = Join-Path $projectPath '0_Core'
    $domainDir = Join-Path $projectPath '1_Domain'
    $applicationDir = Join-Path $projectPath '2_Application'
    $infrastructureDir = Join-Path $projectPath '3_Infrastructure'
    $presentationDir = Join-Path $projectPath '4_Presentation'
    $bootstrapDir = Join-Path $projectPath '5_Bootstrap'

    foreach ($dir in @($coreDir, $domainDir, $applicationDir, $infrastructureDir, $presentationDir, $bootstrapDir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }

    Write-Host ''
    Write-Host 'CardReborn - gate self test (throwaway probe tree in temp)' -ForegroundColor Cyan

    Set-Content -LiteralPath (Join-Path $coreDir 'Card.Core.asmdef') -Encoding ASCII -Value @'
{
    "name": "Card.Core",
    "references": [],
    "noEngineReferences": true
}
'@

    # Prose that merely mentions forbidden APIs must not be reported (M1-R3 regression).
    Set-Content -LiteralPath (Join-Path $coreDir 'CleanProbe.cs') -Encoding ASCII -Value @'
namespace Card.Core
{
    /// <summary>Mentions UnityEngine.Random, Debug.Log and DateTime.Now in prose only.</summary>
    public static class CleanProbe
    {
        public const string Note = "UnityEngine.Vector3 and Time.time inside a string must not be flagged";
    }
}
'@

    Set-Content -LiteralPath (Join-Path $domainDir 'Card.Domain.asmdef') -Encoding ASCII -Value @'
{
    "name": "Card.Domain",
    "references": ["Card.Core", "UnityEngine.UI"],
    "noEngineReferences": false
}
'@

    Set-Content -LiteralPath (Join-Path $domainDir 'ProbeKernelUnity.cs') -Encoding ASCII -Value @'
using System;
using UnityEngine;

namespace Card.Domain
{
    public sealed class ProbeKernelUnity
    {
        public float Damage()
        {
            Debug.Log("probe");
            float roll = Mathf.Abs(Time.time);
            return roll + DateTime.Now.Second;
        }

        public Vector3 Position()
        {
            return new Vector3(1f, 2f, 3f);
        }
    }
}
'@

    Set-Content -LiteralPath (Join-Path $domainDir 'ProbeSerialization.cs') -Encoding ASCII -Value @'
namespace Card.Domain
{
    public sealed class ProbeSerialization
    {
        [SerializeField]
        private int _value;

        public string Dump(object target)
        {
            return JsonUtility.ToJson(target);
        }
    }
}
'@

    Set-Content -LiteralPath (Join-Path $applicationDir 'Card.Application.asmdef') -Encoding ASCII -Value @'
{
    "name": "Card.Application",
    "references": ["Card.Core", "Card.Presentation"],
    "noEngineReferences": true
}
'@

    Set-Content -LiteralPath (Join-Path $infrastructureDir 'ProbeEditorApi.cs') -Encoding ASCII -Value @'
using UnityEditor;

namespace Card.Infrastructure
{
    public static class ProbeEditorApi
    {
        public static void Refresh()
        {
            AssetDatabase.Refresh();
        }
    }
}
'@

    Set-Content -LiteralPath (Join-Path $presentationDir 'ProbeLookup.cs') -Encoding ASCII -Value @'
namespace Card.Presentation
{
    public sealed class ProbeLookup
    {
        private static ProbeLookup Instance;

        public void Attach()
        {
            UnityEngine.GameObject.Find("DeckManager");
        }
    }
}
'@

    Set-Content -LiteralPath (Join-Path $bootstrapDir 'ProbeDebugLog.cs') -Encoding ASCII -Value @'
using UnityEngine;

namespace Card.Bootstrap
{
    public static class ProbeDebugLog
    {
        public static void Say()
        {
            Debug.Log("probe");
        }
    }
}
'@

    $longLines = @('namespace Card.Core', '{')
    for ($i = 0; $i -lt 305; $i++) { $longLines += '    // filler line to exceed the size limit' }
    $longLines += '}'
    Set-Content -LiteralPath (Join-Path $coreDir 'ProbeTooLong.cs') -Value $longLines -Encoding ASCII

    $violations = Get-GateViolations -ProjectPath $projectPath -ProjectRoot $root

    $expectations = @(
        @{ Rule = 'R1'; Min = 2; Why = 'kernel Unity / serialization probes' },
        @{ Rule = 'R2'; Min = 1; Why = 'UnityEditor probe in Infrastructure' },
        @{ Rule = 'R3'; Min = 2; Why = 'GameObject.Find + mutable static Instance probe' },
        @{ Rule = 'R4'; Min = 1; Why = 'Debug.Log probe in Bootstrap' },
        @{ Rule = 'R5'; Min = 1; Why = 'oversized file probe' },
        @{ Rule = 'R6'; Min = 3; Why = 'asmdef probes (engine references + upward dependency)' }
    )

    $failures = New-Object System.Collections.Generic.List[string]
    foreach ($expectation in $expectations) {
        $count = @($violations | Where-Object { $_.Rule -eq $expectation.Rule }).Count
        $status = if ($count -ge $expectation.Min) { 'ok' } else { 'MISSING' }
        Write-Host ("  {0}  expected >= {1,2}  found {2,2}  [{3}]  {4}" -f `
            $expectation.Rule, $expectation.Min, $count, $status, $expectation.Why)

        if ($count -lt $expectation.Min) {
            $failures.Add(("{0}: expected at least {1} hit(s), found {2}" -f $expectation.Rule, $expectation.Min, $count))
        }
    }

    $falsePositives = @($violations | Where-Object { $_.File -like '*CleanProbe.cs' })
    Write-Host ("  clean probe false positives: {0}" -f $falsePositives.Count)
    if ($falsePositives.Count -gt 0) {
        $failures.Add(("CleanProbe.cs produced {0} false positive(s)" -f $falsePositives.Count))
    }

    Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue

    Write-Host ''
    if ($failures.Count -eq 0) {
        Write-Host 'PASS - every rule fired on its probe and clean code stayed clean.' -ForegroundColor Green
        return 0
    }

    Write-Host ("FAIL - {0} self test assertion(s) failed:" -f $failures.Count) -ForegroundColor Red
    foreach ($failure in $failures) {
        Write-Host ("  " + $failure)
    }

    return 1
}

# -------------------------------------------------------------------- run ---

$projectRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $projectRoot 'Assets/_Project'

if ($SelfTest) {
    exit (Invoke-SelfTest)
}

if (-not (Test-Path $projectPath)) {
    Write-Host "Assets/_Project not found: $projectPath" -ForegroundColor Red
    exit 2
}

$violations = Get-GateViolations -ProjectPath $projectPath -ProjectRoot $projectRoot
$scannedCount = @(Get-ChildItem -Path $projectPath -Recurse -File -Filter '*.cs' -ErrorAction SilentlyContinue).Count

Write-Host ''
Write-Host 'CardReborn - local gate check' -ForegroundColor Cyan
Write-Host ("Project root: {0}" -f $projectRoot)
Write-Host ("Scanned: {0} C# file(s) in kernel + runtime layers, {1} asmdef(s) checked" -f `
    $scannedCount, @(Get-ChildItem -Path $projectPath -Recurse -File -Filter '*.asmdef' -ErrorAction SilentlyContinue).Count)

$exitCode = Write-GateReport -Violations $violations -Header 'Result'

if ($ReportOnly) { exit 0 }
exit $exitCode
