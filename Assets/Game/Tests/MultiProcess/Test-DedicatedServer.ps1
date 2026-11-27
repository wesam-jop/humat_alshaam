# Headless dedicated server + two client processes (localhost), then server shutdown.
#  - each client controls only its own player, both are synced on the other client
#  - graceful server close -> Arabic "session ended" message -> back to the menu
#  - hard server kill -> "connection lost" after the disconnect timeout -> back to the menu
#   powershell -ExecutionPolicy Bypass -File Assets\Game\Tests\MultiProcess\Test-DedicatedServer.ps1 [-Port 7831]
param([int]$Port = 7831)
. (Join-Path $PSScriptRoot 'Common.ps1')
. (Join-Path $PSScriptRoot 'NetChecks.ps1')
$S = 'dedicated-localhost'

# ---- graceful shutdown run
$srv = Start-A3D 'ds-server' @('-mode', 'server', '-port', "$Port", '-agent', 'idle', '-quitAfter', '20') -Headless
Start-Sleep -Seconds 3
$ca = Start-A3D 'ds-clientA' @('-mode', 'client', '-address', '127.0.0.1', '-port', "$Port", '-agent', 'walk', '-quitAfter', '45') -Headless
Start-Sleep -Milliseconds 500
$cb = Start-A3D 'ds-clientB' @('-mode', 'client', '-address', '127.0.0.1', '-port', "$Port", '-agent', 'walk', '-quitAfter', '45') -Headless
Wait-A3D @($srv, $ca, $cb) 60

$server = @(Get-A3DEvents $srv.Log 'server_state')
$started = @(Get-A3DEvents $srv.Log 'server_started')
Add-Check $S 'headless server started (no camera / UI / input)' ($started.Count -eq 1 -and (Select-String $srv.Log -Pattern 'family=Server' -Quiet)) "port=$($started[0].port)"
$noUi = -not (Select-String $srv.Log -Pattern 'ArenaHudCanvas|LocalPlayerCamera' -Quiet)
Add-Check $S 'server log has no HUD / camera objects' $noUi 'grep ArenaHudCanvas|LocalPlayerCamera'
$maxP = ($server | ForEach-Object { $_.players.Count } | Measure-Object -Maximum).Maximum
Add-Check $S 'server had two players' ($maxP -eq 2) "max players=$maxP"

$a = @(Get-A3DEvents $ca.Log 'agent_state' | Where-Object { $_.status -eq 'Running' })
$b = @(Get-A3DEvents $cb.Log 'agent_state' | Where-Object { $_.status -eq 'Running' })
$idA = ($a | Select-Object -Last 1).local; $idB = ($b | Select-Object -Last 1).local
Add-Check $S 'clients got different server-assigned ids' ($idA -gt 0 -and $idB -gt 0 -and $idA -ne $idB) "A=$idA B=$idB"
$aSeesB = @($a | Where-Object { $_.others | Where-Object { $_.id -eq $idB } }).Count
$bSeesA = @($b | Where-Object { $_.others | Where-Object { $_.id -eq $idA } }).Count
Add-Check $S 'each client sees the other' ($aSeesB -gt 20 -and $bSeesA -gt 20) "A sees B: $aSeesB samples, B sees A: $bSeesA samples"

$syncAB = Measure-RemoteSync $a $server $idB 100
$syncBA = Measure-RemoteSync $b $server $idA 100
Add-Check $S 'B position on client A matches the server' ($syncAB.Median -lt 0.5) ("median={0:N3} m max={1:N3} m n={2}" -f $syncAB.Median, $syncAB.Max, $syncAB.Count)
Add-Check $S 'A position on client B matches the server' ($syncBA.Median -lt 0.5) ("median={0:N3} m max={1:N3} m n={2}" -f $syncBA.Median, $syncBA.Max, $syncBA.Count)

# Independent input: the two agents walk the same square rotated by 90 degrees.
function First-Move($states) {
    $s = @($states | Where-Object { $_.pos })
    if ($s.Count -lt 12) { return @(0, 0) }
    return @(($s[11].pos[0] - $s[1].pos[0]), ($s[11].pos[2] - $s[1].pos[2]))
}
$ma = First-Move $a; $mb = First-Move $b
$dot = ($ma[0] * $mb[0] + $ma[1] * $mb[1]) / ([Math]::Max(1e-3, [Math]::Sqrt($ma[0]*$ma[0]+$ma[1]*$ma[1]) * [Math]::Sqrt($mb[0]*$mb[0]+$mb[1]*$mb[1])))
Add-Check $S 'inputs are independent (different directions)' ([Math]::Abs($dot) -lt 0.5) ("A first 1 s move=({0:N2},{1:N2}) B=({2:N2},{3:N2}) cos={4:N2}" -f $ma[0], $ma[1], $mb[0], $mb[1], $dot)

foreach ($c in @($ca, $cb)) {
    $seen = @(Get-A3DEvents $c.Log 'agent_disconnect_seen')
    $menu = @(Get-A3DEvents $c.Log 'agent_menu_after_disconnect')
    Add-Check $S "$($c.Tag): server close shown in Arabic" ($seen.Count -eq 1 -and $seen[0].reason -eq 'ServerClosed' -and $seen[0].dialog -and $seen[0].message -like '*أغلق المضيف*') "reason=$($seen[0].reason) message=$($seen[0].message)"
    Add-Check $S "$($c.Tag): back to the menu" ($menu.Count -eq 1 -and $menu[0].menu) "selected=$($menu[0].selected)"
}

# ---- hard kill run (server process terminated without notice)
$srv2 = Start-A3D 'ds-server-kill' @('-mode', 'server', '-port', "$($Port + 1)", '-agent', 'idle', '-quitAfter', '60') -Headless
Start-Sleep -Seconds 3
$cc = Start-A3D 'ds-clientC' @('-mode', 'client', '-address', '127.0.0.1', '-port', "$($Port + 1)", '-agent', 'walk', '-quitAfter', '40') -Headless
Start-Sleep -Seconds 6
$killedAt = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
Stop-Process -Id $srv2.Process.Id -Force
Wait-A3D @($cc) 40
$seen = @(Get-A3DEvents $cc.Log 'agent_disconnect_seen')
$menu = @(Get-A3DEvents $cc.Log 'agent_menu_after_disconnect')
$detect = if ($seen.Count -gt 0) { ($seen[0].wall - $killedAt) / 1000.0 } else { -1 }
Add-Check $S 'killed server detected as connection lost' ($seen.Count -eq 1 -and $seen[0].reason -eq 'ConnectionLost' -and $seen[0].message -like '*انقطع*') ("reason={0} after {1:N1} s message={2}" -f $seen[0].reason, $detect, $seen[0].message)
Add-Check $S 'client returned to the menu after the kill' ($menu.Count -eq 1 -and $menu[0].menu) ''

exit (Save-Checks 'dedicated-checks.json')
