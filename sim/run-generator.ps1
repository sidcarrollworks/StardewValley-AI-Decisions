param(
    [ValidateSet('cpu', 'cuda')][string]$Device = 'cuda',
    [int]$Port = 8080,
    [double]$TimeoutSeconds = 4,
    [switch]$Warmup,
    [switch]$DownloadOnly
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$pythonPath = Join-Path $repoRoot 'sidecar/.venv/Scripts/python.exe'
if (-not (Test-Path -LiteralPath $pythonPath)) {
    throw 'The existing sidecar Python environment is required. This launcher installs nothing.'
}
$generatorPath = Join-Path $PSScriptRoot 'tools/reflection_generator.py'
$generatorArgs = @($generatorPath, '--model', 'Qwen/Qwen3-0.6B',
    '--revision', 'c1899de289a04d12100db370d81485cdf75e47ca',
    '--device', $Device, '--port', $Port, '--timeout-seconds', $TimeoutSeconds)
if ($Warmup) { $generatorArgs += '--warmup' }
if ($DownloadOnly) { $generatorArgs += '--download-only' }
& $pythonPath @generatorArgs
exit $LASTEXITCODE
