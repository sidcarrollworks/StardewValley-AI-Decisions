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

Write-Host "Starting Laya on http://$($env:LAYA_HOST):$($env:LAYA_PORT) (models: $($env:LAYA_MODELS)). First run downloads weights; this can take a while."
& $exe
exit $LASTEXITCODE
