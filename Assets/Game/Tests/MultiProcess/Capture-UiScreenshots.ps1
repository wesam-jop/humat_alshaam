# Runs the windowed player and captures the Arabic UI for visual review:
# menu, join form (LTR address/port), Arabic error dialog, host HUD (listen address), pause menu.
#   powershell -ExecutionPolicy Bypass -File Assets\Game\Tests\MultiProcess\Capture-UiScreenshots.ps1
param([int]$Port = 7790, [switch]$Touch)
. (Join-Path $PSScriptRoot 'Common.ps1')
$S = 'ui-screenshots'
$dir = Join-Path $A3DResults 'screenshots'
if ($Touch) { $dir = Join-Path $A3DResults 'screenshots-touch' }
New-Item -ItemType Directory -Force $dir | Out-Null
Get-ChildItem $dir -Filter *.png -ErrorAction SilentlyContinue | Remove-Item
$extra = @('-agent', 'screenshots', '-shots', "`"$dir`"", '-port', "$Port", '-quitAfter', '40')
if ($Touch) { $extra += '-touchui' }
$p = Start-A3D ("ui" + $(if ($Touch) { '-touch' } else { '' })) $extra
Wait-A3D @($p) 60

$shots = @(Get-ChildItem $dir -Filter *.png)
Add-Check $S 'screenshots captured' ($shots.Count -eq 5) (($shots | ForEach-Object { $_.Name }) -join ', ')
$dlg = @(Get-A3DEvents $p.Log 'invalid_address_dialog')[0]
Add-Check $S 'invalid address shows Arabic error and focuses OK' ($dlg.open -and $dlg.message -like '*غير صالح*' -and $dlg.selected -eq 'Button') "message=$($dlg.message) selected=$($dlg.selected)"
$focus = @(Get-A3DEvents $p.Log 'focus_after_dialog')[0]
Add-Check $S 'focus returns to Connect after closing the dialog' ($focus.isConnect) "selected=$($focus.selected)"
$listen = @(Get-A3DEvents $p.Log 'host_listen_text')[0]
Add-Check $S 'host HUD shows listen address:port (LTR)' ($listen.text -match "^\d+\.\d+\.\d+\.\d+:$Port") "text=$($listen.text -replace "`n", ' | ')"
exit (Save-Checks ("ui-checks" + $(if ($Touch) { '-touch' } else { '' }) + ".json"))
