# Builds the Windows player (and records Android / iOS / PlayStation availability) into Builds\.
#   powershell -ExecutionPolicy Bypass -File Assets\Game\Tests\MultiProcess\Build-Players.ps1 [-All]
param([switch]$All)
. (Join-Path $PSScriptRoot 'Common.ps1')
$method = if ($All) { 'Adventure3D.EditorTools.BuildScripts.BuildAll' } else { 'Adventure3D.EditorTools.BuildScripts.BuildWindows' }
$r = Invoke-Unity @('-executeMethod', $method) 'unity-build.log'
Get-Content (Join-Path $A3DProject 'Reports\build-results.json') -Encoding UTF8
exit $r.ExitCode
