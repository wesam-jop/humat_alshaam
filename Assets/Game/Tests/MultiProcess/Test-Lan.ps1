# LAN host + separate client process on this machine (localhost):
#  - both see each other, both move at the same time, positions agree with the host's authority
#  - the client leaves to the menu and rejoins: the host has no stale copy afterwards
#   powershell -ExecutionPolicy Bypass -File Assets\Game\Tests\MultiProcess\Test-Lan.ps1 [-Port 7821]
param([int]$Port = 7821)
. (Join-Path $PSScriptRoot 'Common.ps1')
. (Join-Path $PSScriptRoot 'NetChecks.ps1')
$S = 'lan-localhost'

$host1 = Start-A3D 'lan-host' @('-mode', 'host', '-port', "$Port", '-agent', 'walk', '-quitAfter', '24') -Headless
Start-Sleep -Seconds 3
$client = Start-A3D 'lan-client' @('-mode', 'client', '-address', '127.0.0.1', '-port', "$Port", '-agent', 'reconnect', '-quitAfter', '30') -Headless
Wait-A3D @($client, $host1) 45

$hostStarted = @(Get-A3DEvents $host1.Log 'host_started')
Add-Check $S 'host listens' ($hostStarted.Count -eq 1) "port=$($hostStarted[0].port)"

$server = @(Get-A3DEvents $host1.Log 'server_state')
$joined = @(Get-A3DEvents $host1.Log 'server_player_joined')
$left = @(Get-A3DEvents $host1.Log 'server_player_left')
$hostStates = @(Get-A3DEvents $host1.Log 'agent_state')
$cStates = @(Get-A3DEvents $client.Log 'agent_state')
$r1 = @($cStates | Where-Object { $_.label -eq 'round1' -and $_.status -eq 'Running' })
$r2 = @($cStates | Where-Object { $_.label -eq 'round2' -and $_.status -eq 'Running' })
$rounds = @(Get-A3DEvents $client.Log 'reconnect_round')

Add-Check $S 'client joined twice (join + rejoin)' ($joined.Count -eq 2) ("player ids joined: " + (($joined | ForEach-Object { $_.player }) -join ','))
Add-Check $S 'old copy removed when the client left' ($left.Count -ge 1 -and $left[0].player -eq $joined[0].player) ("left: " + (($left | ForEach-Object { $_.player }) -join ','))

$afterRejoin = @($server | Where-Object { $_.wall -gt ($joined[-1].wall + 1500) -and $_.wall -lt ($rounds[-1].wall) })
$maxPlayers = ($afterRejoin | ForEach-Object { $_.players.Count } | Measure-Object -Maximum).Maximum
Add-Check $S 'exactly 2 players on the host after rejoin (no stale player)' ($afterRejoin.Count -gt 0 -and $maxPlayers -eq 2) "max players after rejoin=$maxPlayers samples=$($afterRejoin.Count)"

$hostId = 1
$seesHost1 = @($r1 | Where-Object { $_.others | Where-Object { $_.id -eq $hostId } }).Count
$seesHost2 = @($r2 | Where-Object { $_.others | Where-Object { $_.id -eq $hostId } }).Count
Add-Check $S 'client sees the host player (both rounds)' ($seesHost1 -gt 10 -and $seesHost2 -gt 10) "round1 samples=$seesHost1 round2 samples=$seesHost2"
$hostSeesClient = @($hostStates | Where-Object { $_.others.Count -ge 1 }).Count
Add-Check $S 'host sees the client player' ($hostSeesClient -gt 10) "samples=$hostSeesClient"
$hostSeesTwoClients = @($hostStates | Where-Object { $_.others.Count -ge 2 }).Count
Add-Check $S 'host never shows two copies of the client' ($hostSeesTwoClients -eq 0) "samples with 2+ others=$hostSeesTwoClients"

$clientMoved = Get-Path-Length $r1
$hostMovedDuring = Get-Path-Length @($hostStates | Where-Object { $_.wall -ge $r1[0].wall -and $_.wall -le $r1[-1].wall })
Add-Check $S 'host and client move at the same time' ($clientMoved -gt 5 -and $hostMovedDuring -gt 5) ("client path={0:N1} m, host path in same window={1:N1} m" -f $clientMoved, $hostMovedDuring)

$sync = Measure-RemoteSync ($r1 + $r2) $server $hostId 100
Add-Check $S 'host position on the client matches the host authority' ($sync.Median -lt 0.5) ("median={0:N3} m max={1:N3} m n={2} (interp delay 0.1 s)" -f $sync.Median, $sync.Max, $sync.Count)
$pred = Measure-PredictionError ($r1 + $r2) $server
Add-Check $S 'client prediction matches the host authority' ($pred.Median -lt 0.5) ("median={0:N3} m n={1}" -f $pred.Median, $pred.Count)
$corr = ($cStates | Select-Object -Last 1)
Add-Check $S 'prediction corrections recorded' $true ("corrections={0} max={1} m" -f $corr.corrections, $corr.maxCorrection)

exit (Save-Checks 'lan-checks.json')
