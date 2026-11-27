# Runs project setup (optional), Edit Mode and Play Mode tests in batch mode.
#   powershell -ExecutionPolicy Bypass -File Assets\Game\Tests\MultiProcess\Run-UnityTests.ps1 [-Setup]
param([switch]$Setup)
. (Join-Path $PSScriptRoot 'Common.ps1')

if ($Setup) {
    $r = Invoke-Unity @('-executeMethod', 'Adventure3D.EditorTools.ProjectSetup.RunBatch') 'unity-setup.log'
    Write-Host "Setup exit code: $($r.ExitCode)"
    if ($r.ExitCode -ne 0) { exit 1 }
}

$total = 0
foreach ($platform in 'EditMode', 'PlayMode') {
    $xml = Join-Path $A3DResults "$($platform.ToLower())-results.xml"
    if (Test-Path $xml) { Remove-Item $xml }
    $r = Invoke-Unity @('-runTests', '-testPlatform', $platform, '-testResults', "`"$xml`"") "$($platform.ToLower()).log"
    if (-not (Test-Path $xml)) { Write-Host "$platform : no results (compile error?) see $($r.Log)" -ForegroundColor Red; $total++; continue }
    [xml]$x = Get-Content $xml -Encoding UTF8
    $run = $x.'test-run'
    Write-Host ("{0}: total={1} passed={2} failed={3} skipped={4}" -f $platform, $run.total, $run.passed, $run.failed, $run.skipped)
    foreach ($f in $x.SelectNodes("//test-case[@result='Failed']")) {
        Write-Host ("  FAIL {0}: {1}" -f $f.fullname, $f.failure.message.InnerText) -ForegroundColor Red
    }
    $total += [int]$run.failed
}
exit $total
