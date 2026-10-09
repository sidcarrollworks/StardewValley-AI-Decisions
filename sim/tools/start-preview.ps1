<#
.SYNOPSIS
Serves the prepared Under Glass pages on loopback; no simulation or model is started.
.EXAMPLE
pwsh -File sim/tools/start-preview.ps1
# Then visit http://127.0.0.1:8766/sim/viewer/morning.html
.EXAMPLE
pwsh -File sim/tools/start-preview.ps1 -Open
.EXAMPLE
pwsh -File sim/tools/start-preview.ps1 -Stop
# Stops only the process registered by this launcher for this checkout and port.
#>
[CmdletBinding()]
param(
    [ValidateRange(1024, 65535)] [int] $Port = 8766,
    [switch] $Stop,
    [switch] $Open
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($Stop -and $Open) { throw 'Choose -Stop or -Open, not both.' }

$previewRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$previewRoot = (Resolve-Path -LiteralPath $previewRoot).ProviderPath.TrimEnd('\', '/')
if (-not (Test-Path -LiteralPath (Join-Path $previewRoot 'sim/UnderGlass.sln') -PathType Leaf)) {
    throw "Cannot find the Under Glass checkout above $PSScriptRoot."
}

function Get-PreviewPath([string] $RelativePath) {
    $candidatePath = [System.IO.Path]::GetFullPath((Join-Path $previewRoot $RelativePath))
    if (-not $candidatePath.StartsWith($previewRoot + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Preview path escapes this checkout: $candidatePath"
    }
    return $candidatePath
}

$previewDirectory = Get-PreviewPath 'out/under-glass-preview'
$previewReceipt = Get-PreviewPath "out/under-glass-preview/server-$Port.json"
$previewOutput = Get-PreviewPath "out/under-glass-preview/server-$Port.stdout.log"
$previewErrors = Get-PreviewPath "out/under-glass-preview/server-$Port.stderr.log"
$previewViewer = Get-PreviewPath 'sim/viewer/index.html'
$previewBaseUrl = "http://127.0.0.1:$Port"
$previewPageUrl = "$previewBaseUrl/sim/viewer/morning.html"

function Get-RegisteredPreview {
    if (-not (Test-Path -LiteralPath $previewReceipt -PathType Leaf)) { return $null }
    $receipt = Get-Content -LiteralPath $previewReceipt -Raw | ConvertFrom-Json
    if ($receipt.root -ne $previewRoot -or $receipt.port -ne $Port) {
        throw "The preview receipt does not belong to this checkout and port: $previewReceipt"
    }
    $registeredProcess = Get-Process -Id $receipt.processId -ErrorAction SilentlyContinue
    if ($null -eq $registeredProcess) { return $null }
    # A recycled PID is not ours. Never stop it or overwrite its receipt as a live server.
    if ($registeredProcess.StartTime.ToUniversalTime().Ticks -ne ([datetime] $receipt.startedAtUtc).ToUniversalTime().Ticks) {
        return $null
    }
    $processInfo = Get-CimInstance Win32_Process -Filter "ProcessId = $($registeredProcess.Id)"
    $commandLine = $processInfo.CommandLine
    if ($registeredProcess.Path -ne $receipt.executable -or
        $commandLine -notmatch ('-m\s+http\.server\s+' + $Port + '\s') -or
        $commandLine -notmatch '--bind\s+127\.0\.0\.1\s' -or
        -not $commandLine.Contains('--directory "' + $previewRoot + '"')) {
        throw "The registered PID no longer matches this preview. No process was stopped. Receipt: $previewReceipt"
    }
    return $registeredProcess
}

function Test-PreviewPort {
    $socket = New-Object System.Net.Sockets.TcpClient
    try {
        $attempt = $socket.ConnectAsync('127.0.0.1', $Port)
        if (-not $attempt.Wait(750)) { return $false }
        return $socket.Connected
    }
    catch { return $false }
    finally { $socket.Dispose() }
}

function Test-ExpectedPreview {
    try {
        $response = Invoke-WebRequest -Uri "$previewBaseUrl/sim/viewer/index.html" -UseBasicParsing -TimeoutSec 3
        $expected = [System.IO.File]::ReadAllText($previewViewer)
        return $response.StatusCode -eq 200 -and $response.Content.Contains('Under Glass') -and
            [string]::Equals($response.Content, $expected, [StringComparison]::Ordinal)
    }
    catch { return $false }
}

function Write-PreviewReady([string] $Status) {
    Write-Host $Status
    Write-Host "Morning page: $previewPageUrl"
    Write-Host "Viewer:       $previewBaseUrl/sim/viewer/index.html"
    if (-not (Test-Path -LiteralPath (Get-PreviewPath 'sim/viewer/morning.html') -PathType Leaf)) {
        Write-Host 'The morning page has not been prepared yet. The viewer can open an existing run JSON.'
    }
    Write-Host 'This server only serves files; it does not run the simulation or start a model.'
    if ($Open) { Start-Process -FilePath $previewPageUrl | Out-Null }
}

$registered = Get-RegisteredPreview
if ($Stop) {
    if ($null -ne $registered) {
        Stop-Process -Id $registered.Id
        if (-not $registered.WaitForExit(5000)) { throw 'The preview has not exited; its receipt was kept for another stop attempt.' }
        Write-Host "Stopped this checkout's preview on port $Port."
    }
    else { Write-Host "No running preview registered by this launcher on port $Port." }
    if (Test-Path -LiteralPath $previewReceipt -PathType Leaf) { Remove-Item -LiteralPath $previewReceipt }
    return
}

if (Test-PreviewPort) {
    if (-not (Test-ExpectedPreview)) {
        throw "Port $Port is already serving something else. It was left running. Choose another -Port."
    }
    Write-PreviewReady 'Reusing the existing preview of this checkout.'
    return
}
if ($null -ne $registered) {
    throw "The registered preview is running but is not responding on port $Port. Use -Stop, then start again."
}

$previewPython = Get-PreviewPath 'sidecar/.venv/Scripts/python.exe'
if (-not (Test-Path -LiteralPath $previewPython -PathType Leaf)) {
    $pythonCommand = Get-Command python.exe, python3.exe -CommandType Application -ErrorAction SilentlyContinue |
        Where-Object { $_.Source -notlike '*\WindowsApps\*' } | Select-Object -First 1
    if ($null -eq $pythonCommand) { throw 'Python was not found. Install Python or create sidecar/.venv, then try again.' }
    $previewPython = $pythonCommand.Source
}

New-Item -ItemType Directory -Path $previewDirectory -Force | Out-Null
# Start-Process joins arguments into a Windows command line; quote the validated absolute root.
$previewArguments = '-u -m http.server ' + $Port + ' --bind 127.0.0.1 --directory "' + $previewRoot + '"'
$previewProcess = Start-Process -FilePath $previewPython -ArgumentList $previewArguments -WorkingDirectory $previewRoot `
    -WindowStyle Hidden -RedirectStandardOutput $previewOutput -RedirectStandardError $previewErrors -PassThru
try {
    [ordered]@{
        root = $previewRoot
        port = $Port
        processId = $previewProcess.Id
        startedAtUtc = $previewProcess.StartTime.ToUniversalTime().ToString('o')
        executable = $previewPython
    } | ConvertTo-Json | Set-Content -LiteralPath $previewReceipt -Encoding UTF8
    $ready = $false
    for ($probe = 0; $probe -lt 20; $probe++) {
        if ($previewProcess.HasExited) { break }
        if (Test-ExpectedPreview) { $ready = $true; break }
        Start-Sleep -Milliseconds 200
    }
    if (-not $ready) { throw "Preview did not become ready. See $previewErrors" }
    Write-PreviewReady 'Started a loopback preview of this checkout.'
    Write-Host "Stop it: pwsh -File sim/tools/start-preview.ps1 -Port $Port -Stop"
}
catch {
    # The process object came from this launch, so cleanup cannot select an unrelated port owner.
    if (-not $previewProcess.HasExited) { $previewProcess.Kill(); $previewProcess.WaitForExit(5000) | Out-Null }
    if (Test-Path -LiteralPath $previewReceipt -PathType Leaf) { Remove-Item -LiteralPath $previewReceipt }
    throw
}
