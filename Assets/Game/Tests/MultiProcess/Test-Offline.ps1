# Offline checks on the built Windows player (headless):
#  1. First run with no identity file -> guest id created; second run -> same id (persistence).
#  2. Offline session opens no network sockets.
#  3. Offline -> move/jump -> pause -> back to menu -> second offline session, no duplicates.
#  4. Same scripted input at 30 / 60 / 120 fps gives the same positions at the same ticks.
#   powershell -ExecutionPolicy Bypass -File Assets\Game\Tests\MultiProcess\Test-Offline.ps1
. (Join-Path $PSScriptRoot 'Common.ps1')
$S = 'offline'

# ---- 1. guest identity persistence (simulated first launch: identity folder removed)
$idDir = Join-Path $env:USERPROFILE 'AppData\LocalLow\WesamGames\Adventure3D\identity'
if (Test-Path $idDir) { Remove-Item $idDir -Recurse -Force }
$a = Start-A3D 'identity-run1' @('-agent', 'identity') -Headless; Wait-A3D @($a) 30
$b = Start-A3D 'identity-run2' @('-agent', 'identity') -Headless; Wait-A3D @($b) 30
$ida = @(Get-A3DEvents $a.Log 'identity')[0]
$idb = @(Get-A3DEvents $b.Log 'identity')[0]
Add-Check $S 'guest id created on first run' ($ida -and $ida.profileId.Length -eq 32 -and $ida.guest) "id=$($ida.profileId) file=$($ida.file)"
Add-Check $S 'guest id identical after restart' ($ida -and $idb -and $ida.profileId -eq $idb.profileId) "run1=$($ida.profileId) run2=$($idb.profileId)"

# ---- 2. no sockets while playing offline
$o = Start-A3D 'offline-sockets' @('-mode', 'offline', '-agent', 'idle', '-quitAfter', '8') -Headless
Start-Sleep -Seconds 5
$udp = @(Get-NetUDPEndpoint -OwningProcess $o.Process.Id -ErrorAction SilentlyContinue)
$tcp = @(Get-NetTCPConnection -OwningProcess $o.Process.Id -ErrorAction SilentlyContinue)
Wait-A3D @($o) 20
$running = @(Get-A3DEvents $o.Log 'session_status' | Where-Object { $_.status -eq 'Running' -and $_.mode -eq 'Offline' }).Count -gt 0
Add-Check $S 'offline session starts without network' $running 'session_status Running/Offline'
Add-Check $S 'offline session opens no UDP/TCP sockets' ($udp.Count -eq 0 -and $tcp.Count -eq 0) "udp=$($udp.Count) tcp=$($tcp.Count)"

# ---- 3. offline cycle
$c = Start-A3D 'offline-cycle' @('-mode', 'offline', '-agent', 'offline-cycle', '-quitAfter', '60') -Headless
Wait-A3D @($c) 70
$rounds = @(Get-A3DEvents $c.Log 'offline_round')
$menus = @(Get-A3DEvents $c.Log 'menu_after_round')
$pauses = @(Get-A3DEvents $c.Log 'offline_pause')
Add-Check $S 'two offline sessions played' ($rounds.Count -eq 2) "rounds=$($rounds.Count)"
foreach ($r in $rounds) {
    Add-Check $S "round $($r.round): one player + bots ok, one session, player+weapon cameras, one input" ($r.avatars -eq 1 -and $r.sessions -eq 1 -and $r.runners -eq 1 -and $r.cameras -eq 2 -and $r.inputs -eq 1 -and $r.appRoots -eq 1 -and $r.eventSystems -eq 1) ("avatars={0} sessions={1} runners={2} cameras={3} inputs={4} appRoots={5} eventSystems={6}" -f $r.avatars, $r.sessions, $r.runners, $r.cameras, $r.inputs, $r.appRoots, $r.eventSystems)
    Add-Check $S "round $($r.round): player moved and jumped back to ground" ($r.traveled -gt 5 -and $r.grounded) "traveled=$($r.traveled) m grounded=$($r.grounded)"
}
foreach ($m in $menus) {
    Add-Check $S "menu after round $($m.round): nothing left behind" ($m.avatars -eq 0 -and $m.sessions -eq 0 -and $m.runners -eq 0 -and $m.inputs -eq 0 -and $m.appRoots -eq 1 -and $m.eventSystems -eq 1) ("avatars={0} sessions={1} runners={2} inputs={3} appRoots={4} eventSystems={5}" -f $m.avatars, $m.sessions, $m.runners, $m.inputs, $m.appRoots, $m.eventSystems)
}
foreach ($p in $pauses) { Add-Check $S "round $($p.round): pause freezes simulation" ($p.ticksWhilePaused -eq 0) "ticks while paused=$($p.ticksWhilePaused)" }

# ---- 4. frame-rate independence in the real player
$marks = @{}
foreach ($fps in 30, 60, 120) {
    $f = Start-A3D "fps-$fps" @('-mode', 'offline', '-agent', 'fps', '-fps', "$fps", '-quitAfter', '30') -Headless
    Wait-A3D @($f) 40
    $marks[$fps] = @(Get-A3DEvents $f.Log 'fps_mark')
}
$maxDrift = 0.0
$detail = @()
for ($i = 0; $i -lt 4; $i++) {
    $ref = $marks[60][$i]
    foreach ($fps in 30, 120) {
        $o2 = $marks[$fps][$i]
        if (-not $ref -or -not $o2) { $maxDrift = 999; continue }
        $d = [Math]::Sqrt([Math]::Pow($ref.pos[0] - $o2.pos[0], 2) + [Math]::Pow($ref.pos[1] - $o2.pos[1], 2) + [Math]::Pow($ref.pos[2] - $o2.pos[2], 2))
        $maxDrift = [Math]::Max($maxDrift, $d)
    }
}
foreach ($fps in 30, 60, 120) { if ($marks[$fps].Count -gt 0) { $detail += "target $fps -> measured $([Math]::Round($marks[$fps][-1].measuredFps,1)) fps" } }
Add-Check $S 'same positions at ticks 60/120/180/240 for 30/60/120 fps' ($maxDrift -lt 0.001) ("max drift={0:N5} m; {1}" -f $maxDrift, ($detail -join '; '))

exit (Save-Checks 'offline-checks.json')
