#requires -Version 5.1
<#
.SYNOPSIS
    Creates a milestone checkpoint: annotated tag + pushed tag + verified offline bundle.

.DESCRIPTION
    Steps (in order):
      1. report working-tree / origin sync state (warn only, never blocks)
      2. create the annotated tag (default name: checkpoint/<date>-<shortsha>)
      3. push the tag to origin
      4. try to complete the local Git LFS cache, then write a git bundle of --all refs
      5. verify the bundle and print its SHA256

    It never deletes anything, never force-pushes and never moves an existing tag
    (re-running with an existing tag name fails loudly, which is the point).

    NOTE: a bundle carries history and tags but NOT Git LFS objects. For a fully self-contained
    cold backup, also copy the whole project folder including .git (see Docs/HANDOFF.md section 10).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File Tools/checkpoint.ps1 -Name checkpoint/m3-complete
    powershell -ExecutionPolicy Bypass -File Tools/checkpoint.ps1 -DryRun
#>
[CmdletBinding()]
param(
    # Tag name; defaults to checkpoint/<yyyy-MM-dd>-<shortsha>.
    [string]$Name = '',

    # Where the bundle is written; defaults to <parent of repo>\CardReborn-backups.
    [string]$BackupDirectory = '',

    # Tag message body (the "what was verified" note).
    [string]$Note = '',

    # Print the plan without touching tags, the remote or the disk.
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$repoName = Split-Path -Leaf $repoRoot

Push-Location $repoRoot
try {
    $sha = (git rev-parse HEAD).Trim()
    $shortSha = (git rev-parse --short HEAD).Trim()
    $branch = (git rev-parse --abbrev-ref HEAD).Trim()
    $date = Get-Date -Format 'yyyy-MM-dd'

    if ([string]::IsNullOrWhiteSpace($Name)) {
        $Name = "checkpoint/$date-$shortSha"
    }

    if ([string]::IsNullOrWhiteSpace($BackupDirectory)) {
        $BackupDirectory = Join-Path (Split-Path -Parent $repoRoot) "$repoName-backups"
    }

    $safeName = $Name.Replace('/', '-').Replace('\', '-')
    $bundlePath = Join-Path $BackupDirectory "$repoName-$safeName.bundle"

    if ([string]::IsNullOrWhiteSpace($Note)) {
        $Note = "Checkpoint $Name at $shortSha (see Docs/HANDOFF.md section 3 for the verification snapshot)"
    }

    Write-Host ''
    Write-Host 'CardReborn - milestone checkpoint' -ForegroundColor Cyan
    Write-Host ("Repo      : {0}" -f $repoRoot)
    Write-Host ("Branch    : {0} @ {1}" -f $branch, $shortSha)
    Write-Host ("Tag       : {0}" -f $Name)
    Write-Host ("Bundle    : {0}" -f $bundlePath)
    if ($DryRun) {
        Write-Host 'DRY RUN - nothing will be created, pushed or written.' -ForegroundColor Yellow
    }

    Write-Host ''
    Write-Host '1) state check'
    $dirty = @(git status --porcelain)
    if ($dirty.Count -gt 0) {
        Write-Host ("   WARN: {0} uncommitted change(s); commit them before tagging." -f $dirty.Count) -ForegroundColor Yellow
    }
    else {
        Write-Host '   working tree clean'
    }

    $originSha = ''
    try {
        $originSha = (git rev-parse "origin/$branch" 2>$null).Trim()
    }
    catch {
        $originSha = ''
    }

    if ($originSha -ne $sha) {
        Write-Host ("   WARN: HEAD != origin/{0}; push first so the tag is fetchable." -f $branch) -ForegroundColor Yellow
    }
    else {
        Write-Host ("   in sync with origin/{0}" -f $branch)
    }

    $existing = @(git tag -l $Name)
    if ($existing.Count -gt 0) {
        Write-Host ''
        Write-Host ("FAIL - tag already exists: {0}. Use a new name; checkpoints are meant to be immutable." -f $Name) -ForegroundColor Red
        exit 1
    }

    if ($DryRun) {
        Write-Host ''
        Write-Host 'DRY RUN complete - plan looks valid.' -ForegroundColor Green
        exit 0
    }

    Write-Host ''
    Write-Host '2) tag'
    git tag -a $Name -m $Note
    Write-Host ("   created {0}" -f $Name)

    Write-Host '3) push tag'
    git push origin $Name

    Write-Host '4) Git LFS cache (best effort) + bundle'
    try {
        git lfs fetch --all | Out-Null
        Write-Host '   LFS cache complete'
    }
    catch {
        Write-Host '   WARN: could not complete the LFS cache (offline?); the bundle stays pointer-only.' -ForegroundColor Yellow
    }

    if (-not (Test-Path $BackupDirectory)) {
        New-Item -ItemType Directory -Path $BackupDirectory -Force | Out-Null
    }

    git bundle create $bundlePath --all | Out-Null
    Write-Host ("   wrote {0}" -f $bundlePath)

    Write-Host '5) verify'
    git bundle verify $bundlePath | Select-Object -Last 2 | ForEach-Object { Write-Host ("   " + $_) }
    $hash = (Get-FileHash $bundlePath -Algorithm SHA256).Hash
    $sizeMb = [math]::Round((Get-Item $bundlePath).Length / 1MB, 2)
    Write-Host ("   size = {0} MB" -f $sizeMb)
    Write-Host ("   SHA256 = {0}" -f $hash)

    Write-Host ''
    Write-Host 'Checkpoint ready.' -ForegroundColor Green
    Write-Host 'NEXT: copy the whole project folder (including .git) AND this bundle to another disk or cloud drive.' -ForegroundColor Yellow
    Write-Host '      A bundle alone is not a cold backup: it carries no Git LFS objects.' -ForegroundColor DarkGray
}
finally {
    Pop-Location
}
