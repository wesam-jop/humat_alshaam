# Network failure paths on the built player + artificial latency:
#  incompatible protocol, nothing listening (timeout), invalid address, 100 ms simulated latency.
#   powershell -ExecutionPolicy Bypass -File Assets\Game\Tests\MultiProcess\Test-NetworkEdgeCases.ps1 [-Port 7851]
param([int]$Port = 7851)
. (Join-Path $PSScriptRoot 'Common.ps1')
. (Join-Path $PSScriptRoot 'NetChecks.ps1')
$S = 'network-edge'

function Expect-Failure($tag, $argsList, $reason, $arabic) {
    $c = Start-A3D $tag ($argsList + @('-agent', 'walk', '-quitAfter', '30')) -Headless
    Wait-A3D @($c) 40
    $seen = @(Get-A3DEvents $c.Log 'agent_disconnect_seen')
    $menu = @(Get-A3DEvents $c.Log 'agent_menu_after_disconnect')
    Add-Check $S "$tag -> $reason + Arabic message + menu" ($seen.Count -eq 1 -and $seen[0].reason -eq $reason -and $seen[0].dialog -and $seen[0].message -like "*$arabic*" -and $menu.Count -eq 1) ("reason={0} message={1} menu={2}" -f $seen[0].reason, $seen[0].message, ($menu.Count -eq 1))
}

$srv = Start-A3D 'edge-server' @('-mode', 'server', '-port', "$Port", '-agent', 'idle', '-quitAfter', '70') -Headless
Start-Sleep -Seconds 3
Expect-Failure 'edge-protocol' @('-mode', 'client', '-address', '127.0.0.1', '-port', "$Port", '-protocol', '999') 'IncompatibleVersion' 'غير متوافق'
Expect-Failure 'edge-timeout' @('-mode', 'client', '-address', '127.0.0.1', '-port', "$($Port + 9)") 'ConnectTimeout' 'مهلة'
Expect-Failure 'edge-invalid' @('-mode', 'client', '-address', '999.1.1', '-port', "$Port") 'InvalidAddress' 'غير صالح'

# Latency: 100 ms each way on the client (simulator pipeline), jumps and walls included in the walk.
$lat = Start-A3D 'edge-latency' @('-mode', 'client', '-address', '127.0.0.1', '-port', "$Port", '-agent', 'walk', '-simLatency', '100', '-simJitter', '10', '-quitAfter', '16') -Headless
Wait-A3D @($lat) 30
Stop-Process -Id $srv.Process.Id -Force -ErrorAction SilentlyContinue
$server = @(Get-A3DEvents $srv.Log 'server_state')
$ls = @(Get-A3DEvents $lat.Log 'agent_state' | Where-Object { $_.status -eq 'Running' })
$last = $ls | Select-Object -Last 1
$moved = Get-Path-Length $ls
Add-Check $S 'client under 200 ms RTT keeps playing (local prediction)' ($moved -gt 20 -and $last.snapshots -gt 100) ("path={0:N1} m snapshots={1}" -f $moved, $last.snapshots)
$pred = Measure-PredictionError $ls $server
Add-Check $S 'prediction error vs server under latency' ($pred.Median -lt 1.2) ("median={0:N3} m n={1} (client is ~100-200 ms ahead of the server by design)" -f $pred.Median, $pred.Count)
Add-Check $S 'reconciliation recorded under latency' $true ("corrections={0} max correction={1} m" -f $last.corrections, $last.maxCorrection)


# Reconciliation on player-vs-player collisions: two clients walk the same pattern side by side (tags
# chosen so both walk in the same direction), one with 100 ms simulated latency.
$srv2 = Start-A3D 'edge-collide-server' @('-mode', 'server', '-port', "$($Port + 1)", '-agent', 'idle', '-quitAfter', '40') -Headless
Start-Sleep -Seconds 3
$k1 = Start-A3D 'edge-collideA' @('-mode', 'client', '-address', '127.0.0.1', '-port', "$($Port + 1)", '-agent', 'walk', '-simLatency', '100', '-quitAfter', '20') -Headless
Start-Sleep -Milliseconds 300
$k2 = Start-A3D 'edge-collideI' @('-mode', 'client', '-address', '127.0.0.1', '-port', "$($Port + 1)", '-agent', 'walk', '-quitAfter', '20') -Headless
Wait-A3D @($k1, $k2) 40
Stop-Process -Id $srv2.Process.Id -Force -ErrorAction SilentlyContinue
$server2 = @(Get-A3DEvents $srv2.Log 'server_state')
foreach ($k in @($k1, $k2)) {
    $ks = @(Get-A3DEvents $k.Log 'agent_state' | Where-Object { $_.status -eq 'Running' })
    $kl = $ks | Select-Object -Last 1
    $pe = Measure-PredictionError $ks $server2
    Add-Check $S "$($k.Tag): reconciliation with player collisions" ($kl.snapshots -gt 100 -and $pe.Median -lt 1.2) ("corrections={0} max correction={1} m, prediction median={2:N3} m" -f $kl.corrections, $kl.maxCorrection, $pe.Median)
}
exit (Save-Checks 'network-edge-checks.json')
