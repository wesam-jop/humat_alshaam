# Shared helpers for the Adventure3D test / build scripts (Windows PowerShell 5.1+).
$ErrorActionPreference = 'Stop'

$Global:A3DProject = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$Global:A3DResults = Join-Path $A3DProject 'TestResults'
$Global:A3DExe = Join-Path $A3DProject 'Builds\Windows\Adventure3D.exe'

function Get-UnityEditor {
    $version = (Get-Content (Join-Path $A3DProject 'ProjectSettings\ProjectVersion.txt') | Where-Object { $_ -like 'm_EditorVersion:*' }) -replace 'm_EditorVersion:\s*', ''
    $candidates = @(
        "C:\Program Files\Unity $version\Editor\Unity.exe",
        "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
    )
    $hubJson = Join-Path $env:APPDATA 'UnityHub\editors-v2.json'
    if (Test-Path $hubJson) {
        $j = Get-Content $hubJson -Raw | ConvertFrom-Json
        foreach ($e in $j.data) { if ($e.version -eq $version) { $candidates += $e.location } }
    }
    foreach ($c in $candidates) { if ($c -and (Test-Path $c)) { return $c } }
    throw "Unity $version not found. Install it with Unity Hub."
}

function Invoke-Unity([string[]]$Arguments, [string]$LogName) {
    New-Item -ItemType Directory -Force $A3DResults | Out-Null
    $log = Join-Path $A3DResults $LogName
    $argList = @('-batchmode', '-projectPath', "`"$A3DProject`"", '-logFile', "`"$log`"") + $Arguments
    $p = Start-Process -FilePath (Get-UnityEditor) -ArgumentList $argList -PassThru -Wait
    return @{ ExitCode = $p.ExitCode; Log = $log }
}

function Start-A3D([string]$Tag, [string[]]$Arguments, [switch]$Headless) {
    New-Item -ItemType Directory -Force $A3DResults | Out-Null
    $log = Join-Path $A3DResults "$Tag.log"
    if (Test-Path $log) { Remove-Item $log -Force }
    $base = @()
    if ($Headless) { $base += @('-batchmode', '-nographics') } else { $base += @('-screen-fullscreen', '0', '-screen-width', '960', '-screen-height', '540') }
    $all = $base + @('-tag', $Tag, '-logFile', "`"$log`"") + $Arguments
    $p = Start-Process -FilePath $A3DExe -ArgumentList $all -PassThru
    return [pscustomobject]@{ Tag = $Tag; Process = $p; Log = $log }
}

function Get-A3DEvents([string]$LogPath, [string]$Name = $null) {
    if (-not (Test-Path $LogPath)) { return @() }
    $events = @()
    foreach ($line in Get-Content $LogPath -Encoding UTF8) {
        $i = $line.IndexOf('[A3D:EVT] ')
        if ($i -lt 0) { continue }
        try { $e = $line.Substring($i + 10) | ConvertFrom-Json } catch { continue }
        if (-not $Name -or $e.evt -eq $Name) { $events += $e }
    }
    return $events
}

function Wait-A3D($Instances, [int]$TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    foreach ($i in $Instances) {
        $left = [int][Math]::Max(1, ($deadline - (Get-Date)).TotalSeconds)
        if (-not $i.Process.WaitForExit($left * 1000)) { Stop-Process -Id $i.Process.Id -Force -ErrorAction SilentlyContinue }
    }
}

$Global:A3DChecks = New-Object System.Collections.ArrayList
function Add-Check([string]$Scenario, [string]$Name, [bool]$Ok, [string]$Detail) {
    $status = if ($Ok) { 'نجح' } else { 'فشل' }
    [void]$A3DChecks.Add([pscustomobject]@{ scenario = $Scenario; check = $Name; status = $status; detail = $Detail })
    $color = if ($Ok) { 'Green' } else { 'Red' }
    Write-Host ("[{0}] {1} :: {2} ({3})" -f $status, $Scenario, $Name, $Detail) -ForegroundColor $color
}

function Save-Checks([string]$FileName) {
    $path = Join-Path $A3DResults $FileName
    $A3DChecks | ConvertTo-Json -Depth 4 | Out-File -Encoding utf8 $path
    Write-Host "Results: $path"
    $failed = @($A3DChecks | Where-Object { $_.status -ne 'نجح' }).Count
    return $failed
}
