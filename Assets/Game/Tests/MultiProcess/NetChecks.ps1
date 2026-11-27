# Helpers to compare what one process saw with what another process reported, by wall clock.
function Get-Dist($a, $b) {
    return [Math]::Sqrt([Math]::Pow($a[0] - $b[0], 2) + [Math]::Pow($a[1] - $b[1], 2) + [Math]::Pow($a[2] - $b[2], 2))
}

# Authoritative position of player $id from server_state events closest to wall time $wall.
function Get-ServerPos($serverStates, [int]$id, [double]$wall) {
    $best = $null; $bestDt = [double]::MaxValue
    foreach ($s in $serverStates) {
        $dt = [Math]::Abs($s.wall - $wall)
        if ($dt -lt $bestDt) {
            $p = $s.players | Where-Object { $_.id -eq $id } | Select-Object -First 1
            if ($p) { $best = $p.pos; $bestDt = $dt }
        }
    }
    return $best
}

# Median distance between how a client sees player $id and the server's position of $id
# (server sampled $delayMs earlier to account for interpolation delay).
function Measure-RemoteSync($clientStates, $serverStates, [int]$id, [double]$delayMs) {
    $d = @()
    foreach ($c in $clientStates) {
        $o = $c.others | Where-Object { $_.id -eq $id } | Select-Object -First 1
        if (-not $o) { continue }
        $sp = Get-ServerPos $serverStates $id ($c.wall - $delayMs)
        if ($sp) { $d += (Get-Dist $o.pos $sp) }
    }
    if ($d.Count -eq 0) { return @{ Count = 0; Median = 999; Max = 999 } }
    $sorted = $d | Sort-Object
    return @{ Count = $d.Count; Median = $sorted[[int]($sorted.Count / 2)]; Max = $sorted[-1] }
}

# Median distance between a client's predicted local position and the server's position for it.
function Measure-PredictionError($clientStates, $serverStates) {
    $d = @()
    foreach ($c in $clientStates) {
        if (-not $c.pos -or $c.local -le 0) { continue }
        $sp = Get-ServerPos $serverStates $c.local $c.wall
        if ($sp) { $d += (Get-Dist $c.pos $sp) }
    }
    if ($d.Count -eq 0) { return @{ Count = 0; Median = 999 } }
    $sorted = $d | Sort-Object
    return @{ Count = $d.Count; Median = $sorted[[int]($sorted.Count / 2)] }
}

function Get-Path-Length($states) {
    $len = 0.0
    for ($i = 1; $i -lt $states.Count; $i++) { if ($states[$i].pos -and $states[$i - 1].pos) { $len += Get-Dist $states[$i].pos $states[$i - 1].pos } }
    return $len
}
