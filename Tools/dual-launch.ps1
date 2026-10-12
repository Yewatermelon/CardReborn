#requires -Version 5.1
<#
.SYNOPSIS
    Launch two game instances for local PVP verification (M14-T4).

.DESCRIPTION
    Starts a host instance (-host <port>) and a joiner instance
    (-join=<host>:<port>) of the Windows build. Both skip the main menu and
    the lobby "choose role" step automatically (NetLaunch, Docs/05 15.3).

    Prerequisite: a Windows build exists. Build it from the editor menu
    "Card/Build/Windows x64", or headless:
      & "<Unity.exe>" -batchmode -quit -projectPath "<repo>" `
        -executeMethod Card.Editor.BuildMenu.BuildWindowsForAutomation `
        -logFile "<repo>/build.log"

    Both instances write their own log file (BootLogging):
      %USERPROFILE%/AppData/LocalLow/<company>/<product>/Logs/log-<time>-pid<pid>.log

.PARAMETER Exe
    Path to the game executable (used for the host). Default: Builds/Windows/CardReborn.exe

.PARAMETER Exe2
    Path for the joiner. Default: same file as -Exe.

.PARAMETER Port
    Host listen port. Default: 7777.

.PARAMETER HostAddress
    Address the joiner connects to. Default: 127.0.0.1.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File Tools/dual-launch.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File Tools/dual-launch.ps1 -Port 9090
#>
[CmdletBinding()]
param(
    [string]$Exe = '',
    [string]$Exe2 = '',
    [int]$Port = 7777,
    [string]$HostAddress = '127.0.0.1'
)

$ErrorActionPreference = 'Stop'

if ($Exe -eq '') {
    $Exe = Join-Path $PSScriptRoot '..\Builds\Windows\CardReborn.exe'
}

if (-not (Test-Path -LiteralPath $Exe)) {
    Write-Host ("ERROR: game executable not found: {0}" -f $Exe) -ForegroundColor Red
    Write-Host 'Build it first: editor menu "Card/Build/Windows x64", or the headless'
    Write-Host 'command shown in Tools/dual-launch.ps1 header (BuildMenu.BuildWindowsForAutomation).'
    exit 1
}

$joinerExe = if ($Exe2 -ne '') { $Exe2 } else { $Exe }
$hostArgs = "-host {0}" -f $Port
$joinArgs = "-join={0}:{1}" -f $HostAddress, $Port

Write-Host ("[1/2] Host  : {0} {1}" -f $Exe, $hostArgs)
Start-Process -FilePath $Exe -ArgumentList $hostArgs
Start-Sleep -Seconds 2

Write-Host ("[2/2] Joiner: {0} {1}" -f $joinerExe, $joinArgs)
Start-Process -FilePath $joinerExe -ArgumentList $joinArgs

Write-Host ''
Write-Host 'Both instances launched. The host window shows the listen address;'
Write-Host 'the joiner connects automatically, then click "ready" on both sides.'
Write-Host 'Logs (one file per instance, check pid to tell them apart):'
Write-Host '  %USERPROFILE%/AppData/LocalLow/<company>/<product>/Logs/'
