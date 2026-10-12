param(
    [ValidateSet('wording', 'reflection')][string]$Contract = 'reflection',
    [string]$Model = 'claude-haiku-5-5',
    [ValidateSet('disabled', 'adaptive')][string]$Thinking = 'disabled',
    [ValidateSet('low', 'medium', 'high', 'xhigh', 'max')][string]$Effort = 'low',
    [int]$Port = 8081,
    [double]$TimeoutSeconds = 20,
    [int]$MaxRetries = 0,
    [string]$ApiKeyFile = '',
    [switch]$Verify
)

# Starts the loopback Claude generator (sim/tools/claude_generator.py) with the existing sidecar
# Python environment. It installs nothing: the anthropic SDK must already be in that environment
# (sim/tools/claude-generator-requirements.txt). Credentials come from ANTHROPIC_API_KEY in this
# shell or from -ApiKeyFile; this launcher never prints or stores them.
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$pythonPath = Join-Path $repoRoot 'sidecar/.venv/Scripts/python.exe'
if (-not (Test-Path -LiteralPath $pythonPath)) {
    throw 'The existing sidecar Python environment is required. This launcher installs nothing.'
}
& $pythonPath -c 'import anthropic' 2>$null
if ($LASTEXITCODE -ne 0) {
    throw 'The anthropic package is missing from sidecar/.venv. Install sim/tools/claude-generator-requirements.txt explicitly.'
}
if (-not $ApiKeyFile -and -not $env:ANTHROPIC_API_KEY -and -not $env:ANTHROPIC_AUTH_TOKEN) {
    Write-Warning 'No ANTHROPIC_API_KEY or -ApiKeyFile; the SDK will try its own stored credentials and /health reports the outcome.'
}
$generatorPath = Join-Path $PSScriptRoot 'tools/claude_generator.py'
$generatorArgs = @($generatorPath, '--model', $Model, '--contract', $Contract, '--thinking', $Thinking,
    '--effort', $Effort, '--port', $Port, '--timeout-seconds', $TimeoutSeconds, '--max-retries', $MaxRetries)
if ($ApiKeyFile) { $generatorArgs += @('--api-key-file', $ApiKeyFile) }
if ($Verify) { $generatorArgs += '--verify' }
& $pythonPath @generatorArgs
exit $LASTEXITCODE
