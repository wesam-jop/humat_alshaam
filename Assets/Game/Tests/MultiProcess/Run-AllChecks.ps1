# Full verification: Unity tests, Windows build, then every multi-process scenario.
#   powershell -ExecutionPolicy Bypass -File Assets\Game\Tests\MultiProcess\Run-AllChecks.ps1
. (Join-Path $PSScriptRoot 'Common.ps1')
$steps = @(
    @{ Name = 'Unity Edit/Play Mode tests'; Script = 'Run-UnityTests.ps1'; Args = @('-Setup') },
    @{ Name = 'Build players'; Script = 'Build-Players.ps1'; Args = @('-All') },
    @{ Name = 'Offline'; Script = 'Test-Offline.ps1'; Args = @() },
    @{ Name = 'LAN host + client'; Script = 'Test-Lan.ps1'; Args = @() },
    @{ Name = 'Dedicated server + 2 clients'; Script = 'Test-DedicatedServer.ps1'; Args = @() },
    @{ Name = 'Network edge cases'; Script = 'Test-NetworkEdgeCases.ps1'; Args = @() },
    @{ Name = 'UI screenshots'; Script = 'Capture-UiScreenshots.ps1'; Args = @() },
    @{ Name = 'UI screenshots (touch layout)'; Script = 'Capture-UiScreenshots.ps1'; Args = @('-Touch', '-Port', '7791') }
)
$summary = @()
foreach ($s in $steps) {
    Write-Host "==== $($s.Name)" -ForegroundColor Cyan
    & powershell -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot $s.Script) @($s.Args)
    $summary += [pscustomobject]@{ step = $s.Name; exitCode = $LASTEXITCODE }
}
$summary | Format-Table -AutoSize
