<# Run with pwsh -File sim/tools/test-observer-pack.ps1. No builds, services or model calls. #>
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).ProviderPath
$recorder = Join-Path $PSScriptRoot 'record-observer-pack.ps1'
$relativeOutput = 'out/observer-pack-smoke-' + [guid]::NewGuid().ToString('N')
$smokeOutput = [IO.Path]::GetFullPath((Join-Path $repoRoot $relativeOutput))
$probe = @{ Calls = 0 }
function dotnet { $probe.Calls++; throw 'SMOKE_BUILD_REACHED' }
function Assert-True([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw $Message }
}
function Contents([string] $Path) { return [Convert]::ToBase64String([IO.File]::ReadAllBytes($Path)) }
function Expect-Failure([scriptblock] $Action, [string] $Message) {
    try { & $Action; throw 'Expected the recorder to fail.' }
    catch { if ($_.Exception.Message -notlike "*$Message*") { throw } }
}
try {
    & $recorder -PlanOnly -OutputDirectory $relativeOutput -Backends authored
    $planPath = Join-Path $smokeOutput 'pack-plan.json'
    Assert-True (Test-Path -LiteralPath $planPath) 'A new preview must write its plan.'
    Assert-True ($probe.Calls -eq 0) 'PlanOnly reached dotnet.'
    $originalPlan = Contents $planPath
    $originalTime = (Get-Item -LiteralPath $planPath).LastWriteTimeUtc

    & $recorder -PlanOnly -OutputDirectory $relativeOutput -Days 3 -Seed 11 -Backends hybrid
    Assert-True ((Contents $planPath) -ceq $originalPlan) 'PlanOnly replaced the original plan.'
    Assert-True ((Get-Item -LiteralPath $planPath).LastWriteTimeUtc -eq $originalTime) 'PlanOnly rewrote the original plan.'
    Assert-True ($probe.Calls -eq 0) 'Existing-plan preview reached dotnet.'

    $recordingPath = Join-Path $smokeOutput 'authored-seed7.json'
    $manifestPath = Join-Path $smokeOutput 'authored-seed7.manifest.json'
    [IO.File]::WriteAllText($recordingPath, '{"original":"recording"}')
    [IO.File]::WriteAllText($manifestPath, '{"original":"manifest"}')
    $originalRecording = Contents $recordingPath
    $originalManifest = Contents $manifestPath
    Expect-Failure { & $recorder -OutputDirectory $relativeOutput -Backends authored -Days 4 } 'Existing recording preserved'
    Assert-True ($probe.Calls -eq 0) 'Rejected rerun reached dotnet.'
    Assert-True ((Contents $planPath) -ceq $originalPlan) 'Rejected rerun replaced the plan.'
    Assert-True ((Contents $recordingPath) -ceq $originalRecording) 'Rejected rerun replaced its recording.'
    Assert-True ((Contents $manifestPath) -ceq $originalManifest) 'Rejected rerun replaced its manifest.'

    # A completed backend must not prevent a different backend from resuming the pack.
    Expect-Failure { & $recorder -OutputDirectory $relativeOutput -Backends hybrid } 'SMOKE_BUILD_REACHED'
    Assert-True ($probe.Calls -eq 1) 'Partial-backend resume did not reach the stubbed build.'
    Assert-True ((Contents $planPath) -ceq $originalPlan) 'Resume replaced the original plan.'
    Assert-True ((Contents $recordingPath) -ceq $originalRecording) 'Resume replaced a completed recording.'
    Assert-True ((Contents $manifestPath) -ceq $originalManifest) 'Resume replaced a completed manifest.'

    $manifestOnly = Join-Path $smokeOutput 'laya-seed7.manifest.json'
    [IO.File]::WriteAllText($manifestOnly, '{"status":"failed","original":true}')
    $originalFailedManifest = Contents $manifestOnly
    Expect-Failure { & $recorder -OutputDirectory $relativeOutput -Backends laya } 'Existing recording preserved'
    Assert-True ((Contents $manifestOnly) -ceq $originalFailedManifest) 'A manifest-only run was overwritten.'
    Assert-True ($probe.Calls -eq 1) 'Manifest-only collision reached dotnet.'

    $noPlanOutput = Join-Path $smokeOutput 'without-plan'
    New-Item -ItemType Directory -Path $noPlanOutput | Out-Null
    [IO.File]::WriteAllText((Join-Path $noPlanOutput 'hybrid-seed7.log'), 'original partial log')
    Expect-Failure { & $recorder -OutputDirectory "$relativeOutput/without-plan" -Backends hybrid } 'Existing recording preserved'
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $noPlanOutput 'pack-plan.json'))) 'Rejected rerun created misleading provenance.'
    Assert-True ($probe.Calls -eq 1) 'Log-only collision reached dotnet.'

    $parseErrors = $null
    $tokens = $null
    $ast = [Management.Automation.Language.Parser]::ParseFile($recorder, [ref] $tokens, [ref] $parseErrors)
    Assert-True ($parseErrors.Count -eq 0) 'Recorder has a PowerShell syntax error.'
    $outputParameter = $ast.ParamBlock.Parameters | Where-Object { $_.Name.VariablePath.UserPath -eq 'OutputDirectory' }
    $defaultOutput = & ([scriptblock]::Create($outputParameter.DefaultValue.Extent.Text))
    Assert-True ($defaultOutput -eq ('out/observer-test-' + (Get-Date -Format 'yyyy-MM-dd'))) 'Default output does not use the current date.'
    Write-Host 'PASS: seven observer-pack smoke checks; no real build or model call.'
}
finally {
    $expectedParent = [IO.Path]::GetFullPath((Join-Path $repoRoot 'out')) + [IO.Path]::DirectorySeparatorChar
    if (-not $smokeOutput.StartsWith($expectedParent, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe smoke cleanup path.' }
    if (Test-Path -LiteralPath $smokeOutput) { Remove-Item -LiteralPath $smokeOutput -Recurse -Force }
}
