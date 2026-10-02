# Launch a local Laya server (laya-serve) for the Stardew NPC decision client.
# Uses sidecar\.venv if present, otherwise whatever laya-serve is on PATH.
# Environment variables already set take precedence over these defaults.
$ErrorActionPreference = 'Stop'

function Set-Default([string]$Name, [string]$Value) {
    if (-not [Environment]::GetEnvironmentVariable($Name)) {
        [Environment]::SetEnvironmentVariable($Name, $Value, 'Process')
    }
}

Set-Default 'LAYA_HOST' '127.0.0.1'   # loopback only; Laya's own default 0.0.0.0 exposes it on the LAN
Set-Default 'LAYA_PORT' '8000'
Set-Default 'LAYA_MODELS' 'typed-decisions'
Set-Default 'LAYA_PRELOAD' '1'

$venvExe = Join-Path $PSScriptRoot '.venv\Scripts\laya-serve.exe'
if (Test-Path $venvExe) {
    $exe = $venvExe
} elseif (Get-Command 'laya-serve' -ErrorAction SilentlyContinue) {
    $exe = 'laya-serve'
} else {
    Write-Error 'laya-serve not found. Create sidecar\.venv and run: python -m pip install "laya[serve]" (see sidecar\README.md).'
}

# Everything the server prints also goes to a log, so the reason for a failed question survives
# the window closing. Appended, with a start line per run. Set LAYA_LOG to another path to move
# it, or to "off" to turn it off.
$log = if ($env:LAYA_LOG) { $env:LAYA_LOG } else { Join-Path $PSScriptRoot 'laya.log' }

Write-Host "Starting Laya on http://$($env:LAYA_HOST):$($env:LAYA_PORT) (models: $($env:LAYA_MODELS)). First run downloads weights; this can take a while."
if ($log -eq 'off') {
    & $exe
    exit $LASTEXITCODE
}
Write-Host "Logging to $log"
Add-Content -Path $log -Value "==== $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') laya-serve on $($env:LAYA_HOST):$($env:LAYA_PORT), models $($env:LAYA_MODELS) ====" -Encoding UTF8
$env:PYTHONUNBUFFERED = '1'   # print lines as they happen, not when a buffer fills
# Python writes its log to stderr; with Stop, PowerShell would treat the first stderr line as an
# error, so relax it for the server's run. VERIFY on Sid's PC: lines appear in the window and the log.
$ErrorActionPreference = 'Continue'
# Each line goes to the window and the log; Add-Content rather than Tee-Object, which writes
# UTF-16 in Windows PowerShell 5.1.
& $exe 2>&1 | ForEach-Object {
    $line = "$_"
    Write-Host $line
    Add-Content -Path $log -Value $line -Encoding UTF8
}
exit $LASTEXITCODE
