#requires -Version 5.1
<#
.SYNOPSIS
  Soak test resource sampler (M14-T7). Samples process memory/handle counters
  to a CSV file so leak trends can be judged after long idle sessions.

.DESCRIPTION
  Samples the given processes every -Interval seconds for -Duration seconds
  (0 = run until Ctrl+C). Appends one CSV row per process per sample:
    timestamp,pid,name,working_set_mb,private_mb,handles,threads

  Usage (dual-launch soak):
    powershell -ExecutionPolicy Bypass -File Tools\soak-watch.ps1 -Name CardReborn -Out soak.csv
    powershell -ExecutionPolicy Bypass -File Tools\soak-watch.ps1 -ProcIds 1234,5678 -Interval 10 -Duration 1800 -Out soak.csv

  Leak judgement guide: in a healthy session, working_set_mb and handles
  fluctuate but do NOT trend upward across matches. After 30 matches plus
  30 minutes idle, compare first/last 10 rows average per pid.

.NOTES
  Comments are ASCII-only on purpose: Windows PowerShell 5.1 reads .ps1 as
  ANSI, non-ASCII comments break parsing (project convention, see M14 notes).
#>

param(
    # Process name to auto-discover (without .exe), e.g. CardReborn
    [string]$Name = "",
    # Explicit process ids (comma separated). Overrides -Name when given.
    [string]$ProcIds = "",
    # Sampling interval in seconds
    [int]$Interval = 5,
    # Total duration in seconds; 0 means run until Ctrl+C
    [int]$Duration = 0,
    # Output CSV path
    [string]$Out = "soak-watch.csv"
)

$ErrorActionPreference = "Stop"

if ($Interval -le 0) { throw "Interval must be positive." }
if ([string]::IsNullOrWhiteSpace($Name) -and [string]::IsNullOrWhiteSpace($ProcIds)) {
    throw "Provide -Name or -ProcIds."
}

function Get-TargetProcesses {
    if (-not [string]::IsNullOrWhiteSpace($ProcIds)) {
        $ids = $ProcIds.Split(",") | ForEach-Object { [int]$_.Trim() }
        return Get-Process -Id $ids -ErrorAction SilentlyContinue
    }
    return Get-Process -Name $Name -ErrorAction SilentlyContinue
}

# Write CSV header (overwrite) then append rows.
$header = "timestamp,pid,name,working_set_mb,private_mb,handles,threads"
Set-Content -Path $Out -Value $header -Encoding ASCII

Write-Host ("Sampling every {0}s -> {1} (Ctrl+C to stop)" -f $Interval, $Out)

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
while ($true) {
    $procs = Get-TargetProcesses
    if (-not $procs) {
        Write-Host ("[{0}] no matching process found; retrying..." -f (Get-Date -Format "HH:mm:ss"))
    }
    else {
        foreach ($p in $procs) {
            $row = "{0},{1},{2},{3:F1},{4:F1},{5},{6}" -f (
                (Get-Date -Format "yyyy-MM-dd HH:mm:ss"),
                $p.Id,
                $p.ProcessName,
                ($p.WorkingSet64 / 1MB),
                ($p.PrivateMemorySize64 / 1MB),
                $p.HandleCount,
                $p.Threads.Count)
            Add-Content -Path $Out -Value $row -Encoding ASCII
            Write-Host $row
        }
    }

    if ($Duration -gt 0 -and $stopwatch.Elapsed.TotalSeconds -ge $Duration) {
        Write-Host ("Duration {0}s reached; done." -f $Duration)
        break
    }
    Start-Sleep -Seconds $Interval
}
