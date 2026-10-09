<# A local smoke test: starts a preview on a spare port, reuses it, stops it, and checks
   an unrelated listener is left alone. Only this test's server and socket are cleaned up. #>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$previewLauncher = Join-Path $PSScriptRoot 'start-preview.ps1'
$previewTestRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$parseTokens = $null
$parseErrors = $null
$null = [System.Management.Automation.Language.Parser]::ParseFile($previewLauncher, [ref] $parseTokens, [ref] $parseErrors)
if ($parseErrors.Count) { throw ($parseErrors | Out-String) }

function Get-SparePreviewPort {
    $reservation = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    try { $reservation.Start(); return $reservation.LocalEndpoint.Port }
    finally { $reservation.Stop() }
}

$testPort = Get-SparePreviewPort
$receiptPath = Join-Path $previewTestRoot "out/under-glass-preview/server-$testPort.json"
$testStarted = $false
try {
    & $previewLauncher -Port $testPort
    $testStarted = $true
    $morningPage = Invoke-WebRequest -Uri "http://127.0.0.1:$testPort/sim/viewer/morning.html" -UseBasicParsing -TimeoutSec 3
    foreach ($expected in @('id="under-glass-morning"', 'href="/reflection-demo.html"', 'href="/reflection-laya-demo.html"', 'href="/reflection-hybrid-demo.html"', 'Dream on waking')) {
        if (-not $morningPage.Content.Contains($expected)) { throw "Morning page is missing $expected" }
    }
    $firstReceipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
    & $previewLauncher -Port $testPort
    $secondReceipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
    if ($firstReceipt.processId -ne $secondReceipt.processId) { throw 'Reuse started a second process.' }
    & $previewLauncher -Port $testPort -Stop
    if (Test-Path -LiteralPath $receiptPath) { throw 'Stop left its PID receipt behind.' }
    if (Get-Process -Id $firstReceipt.processId -ErrorAction SilentlyContinue) { throw 'Stop left its registered process running.' }
    $testStarted = $false
    & $previewLauncher -Port $testPort -Stop
}
finally { if ($testStarted) { & $previewLauncher -Port $testPort -Stop } }

# A socket owned by this test stands in for another app. It deliberately serves no Under Glass
# page. No other process needs to be started or killed to check the ownership boundary.
$foreignListener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
try {
    $foreignListener.Start()
    $foreignPort = $foreignListener.LocalEndpoint.Port
    $refused = $false
    try { & $previewLauncher -Port $foreignPort }
    catch {
        if ($_.Exception.Message -notlike '*already serving something else*') { throw }
        $refused = $true
    }
    if (-not $refused) { throw 'The launcher adopted an unrelated listener.' }
    & $previewLauncher -Port $foreignPort -Stop
    if ($foreignListener.LocalEndpoint.Port -ne $foreignPort) { throw 'The unrelated listener was changed.' }
}
finally { $foreignListener.Stop() }

Write-Host 'PASS: parse, start, morning page and recording links, reuse, owned stop, repeated stop, and unrelated listener preservation.'
