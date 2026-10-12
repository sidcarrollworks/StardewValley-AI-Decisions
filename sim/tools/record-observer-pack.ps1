<#
.SYNOPSIS
Records a bounded observer comparison pack using already-running local services.
.EXAMPLE
pwsh -File sim/tools/record-observer-pack.ps1 -PlanOnly
.EXAMPLE
pwsh -File sim/tools/record-observer-pack.ps1 -Backends authored
.EXAMPLE
pwsh -File sim/tools/record-observer-pack.ps1 -Backends laya,hybrid
# No services or models are installed or started. Live output may contain recorded fallback.
#>
[CmdletBinding()]
param(
    [ValidateRange(1, 56)] [int] $Days = 28,
    [long] $Seed = 7,
    [long] $ControlSeed = 29,
    [string] $Backends = 'authored,laya,hybrid',
    [string] $OutputDirectory = ('out/observer-test-' + (Get-Date -Format 'yyyy-MM-dd')),
    [string] $LayaUrl = 'http://127.0.0.1:8000',
    [string] $LayaModel = 'typed-decisions',
    [string] $GeneratorUrl = 'http://127.0.0.1:8080',
    [string] $GeneratorModel = 'Qwen/Qwen3-0.6B',
    [switch] $PlanOnly
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$packRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).ProviderPath
$packOut = [System.IO.Path]::GetFullPath((Join-Path $packRoot $OutputDirectory))
if (-not $packOut.StartsWith($packRoot + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'OutputDirectory must be inside this checkout.'
}
$selected = @($Backends.Split(',') | ForEach-Object { $_.Trim().ToLowerInvariant() } | Select-Object -Unique)
if (@($selected | Where-Object { $_ -notin @('authored','laya','hybrid') }).Count) { throw 'Backends must be authored,laya,hybrid or a comma-separated subset.' }
if ($Seed -eq $ControlSeed) { throw 'The authored control must use a different seed.' }
$project = Join-Path $packRoot 'sim/UnderGlass.Replay'
$catalog = 'returns,company,welcome,repair,sides,late'
$shared = @('--days', "$Days", '--catalog', $catalog, '--feel', 'on', '--desire', 'on', '--reflection-chance', '0.35', '--dream-chance', '0.15')
function Quote-Argument([string] $Value) { return "'" + $Value.Replace("'", "''") + "'" }
function Replay-Command([object[]] $Arguments) {
    return 'dotnet run -c Release --project sim/UnderGlass.Replay -- ' + (($Arguments | ForEach-Object { Quote-Argument "$_" }) -join ' ')
}
function Save-Json($Value, [string] $Path) { $Value | ConvertTo-Json -Depth 60 | Set-Content -LiteralPath $Path -Encoding utf8 }

$revision = (& git -C $packRoot rev-parse HEAD).Trim()
$dirty = @(& git -C $packRoot status --porcelain)
$sourceHashes = [ordered]@{}
foreach ($source in @(& git -C $packRoot ls-files --cached --others --exclude-standard -- sim)) {
    if (Test-Path -LiteralPath (Join-Path $packRoot $source) -PathType Leaf) {
        $sourceHashes[$source] = (Get-FileHash -LiteralPath (Join-Path $packRoot $source) -Algorithm SHA256).Hash
    }
}
$sourceIdentity = [ordered]@{ commit = $revision; dirty = $dirty.Count -gt 0; status = $dirty; sha256 = $sourceHashes }
$plans = @()
foreach ($backend in @('authored','laya','hybrid')) {
    $seeds = if ($backend -eq 'authored') { @($Seed,$ControlSeed) } else { @($Seed) }
    foreach ($runSeed in $seeds) {
        $name = "$backend-seed$runSeed"
        $jsonPath = Join-Path $packOut "$name.json"
        $htmlPath = Join-Path $packOut "$name.html"
        $arguments = @('--seed', "$runSeed") + $shared + @('--reflection', $backend, '--evaluation', 'balanced')
        if ($backend -ne 'authored') { $arguments += @('--laya-url', $LayaUrl, '--laya-model', $LayaModel) }
        if ($backend -eq 'hybrid') { $arguments += @('--llm-url', $GeneratorUrl, '--llm-model', $GeneratorModel) }
        $arguments += @('--out', $jsonPath, '--html', $htmlPath)
        $tape = @('--seed', "$runSeed") + $shared + @('--reflection', 'tape', '--reflection-tape', $jsonPath,
            '--out', (Join-Path $packOut "$name-tape.json"), '--html', (Join-Path $packOut "$name-tape.html"))
        $plans += [ordered]@{ name = $name; backendRequested = $backend; seed = $runSeed; days = $Days;
            selected = $backend -in $selected; arguments = $arguments; command = Replay-Command $arguments;
            tapeArguments = $tape; tapeCommand = Replay-Command $tape; json = $jsonPath; html = $htmlPath }
    }
}
if (-not $PlanOnly) {
    foreach ($plan in $plans | Where-Object { $_.selected }) {
        $existingPaths = @($plan.json, $plan.html,
            (Join-Path $packOut "$($plan.name).manifest.json"), (Join-Path $packOut "$($plan.name).log"))
        if (@($existingPaths | Where-Object { Test-Path -LiteralPath $_ }).Count) {
            throw "Existing recording preserved: $($plan.name). Select unfinished -Backends or another -OutputDirectory."
        }
    }
}
$planPath = Join-Path $packOut 'pack-plan.json'
$planExisted = Test-Path -LiteralPath $planPath
if (-not $planExisted) {
    New-Item -ItemType Directory -Path $packOut -Force | Out-Null
    Save-Json ([ordered]@{ createdUtc = [datetime]::UtcNow.ToString('o'); source = $sourceIdentity;
    settings = [ordered]@{ catalog = $catalog; reflectionChance = 0.35; dreamChance = 0.15; evaluation = 'balanced';
        layaUrl = $LayaUrl; layaModel = $LayaModel; generatorUrl = $GeneratorUrl; generatorModel = $GeneratorModel;
        town = 'shipped default'; injectedEvent = $false; extra0d6Switches = 'none'; traitOverrides = 'none' };
    boundaries = @('Plan is not proof a run completed.', 'Live model reruns may differ; tape uses exact recorded answers.',
        'Backend/fallback counts are measured per completed recording.', 'Bookmarks reveal outcomes; open after observation.');
    runs = $plans }) $planPath
}
else { Write-Host 'Original pack-plan.json preserved. Commands below describe this invocation; each new run records its own provenance.' }
foreach ($plan in $plans | Where-Object { $_.selected }) { Write-Host $plan.command }
if ($PlanOnly) { Write-Host 'Plan only. No build, simulation, service probe or model call was made.'; return }
& dotnet build $project -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Replay build failed.' }

foreach ($plan in $plans | Where-Object { $_.selected }) {
    $manifestPath = Join-Path $packOut "$($plan.name).manifest.json"
    $logPath = Join-Path $packOut "$($plan.name).log"
    $manifest = [ordered]@{ status = 'running'; startedUtc = [datetime]::UtcNow.ToString('o'); source = $sourceIdentity; plan = $plan }
    Save-Json $manifest $manifestPath
    try {
        & dotnet run --no-build -c Release --project $project -- @($plan.arguments) 2>&1 | Tee-Object -FilePath $logPath
        if ($LASTEXITCODE -ne 0) { throw "Replay exited $LASTEXITCODE. See $logPath" }
        $run = Get-Content -LiteralPath $plan.json -Raw | ConvertFrom-Json -Depth 100
        $thoughts = @($run.reflections)
        $backendCounts = [ordered]@{}
        foreach ($group in $thoughts | Group-Object { $_.answer.backend }) { $backendCounts[$group.Name] = $group.Count }
        $focus = @('Thanked','Apologised','Comforted','StoodUpFor','Mocked')
        $actions = @()
        foreach ($kind in $focus) {
            $acts = @($run.acts | Where-Object { $run.kinds[$_[3]].name -eq $kind })
            $offered = @($thoughts | Where-Object { @($_.request.choices | Where-Object kind -eq $kind).Count -gt 0 }).Count
            $chosen = @($thoughts | Where-Object { $pick = $_.choice; @($_.request.choices | Where-Object { $_.id -eq $pick -and $_.kind -eq $kind }).Count -gt 0 }).Count
            $reflectionExecuted = @($thoughts | Where-Object {
                $pick = $_.choice
                @($_.request.choices | Where-Object { $_.id -eq $pick -and $_.kind -eq $kind }).Count -gt 0 -and
                @($_.events | Where-Object status -eq 'acted').Count -gt 0
            }).Count
            $actions += [ordered]@{ kind = $kind; thoughtsOffering = $offered; thoughtsChoosing = $chosen;
                reflectionExecutions = $reflectionExecuted; allTownActs = $acts.Count;
                bookmarks = @($acts | Select-Object -First 3 | ForEach-Object { [ordered]@{
                    actId = $_[0]; tick = $_[1]; actor = $run.names[$_[2]]; target = if ($_[7] -ge 0) { $run.names[$_[7]] } else { $null } } }) }
        }
        $apologyOutcomes = [ordered]@{}
        foreach ($entry in $run.life) {
            if ($run.roles[$entry[5]] -ne 'Did' -or $entry[4] -ne 'Apologised') { continue }
            $outcome = $run.outcomes[$entry[9]]
            if (-not $apologyOutcomes.Contains($outcome)) { $apologyOutcomes[$outcome] = 0 }
            $apologyOutcomes[$outcome]++
        }
        $manifest.status = 'completed'
        $manifest['completedUtc'] = [datetime]::UtcNow.ToString('o')
        $manifest['actualBackendCounts'] = $backendCounts
        $manifest['reflectionCount'] = $thoughts.Count
        $manifest['authoredFallbackCount'] = @($thoughts | Where-Object { $_.answer.backend -eq 'authored-fallback' }).Count
        $manifest['recordedSettings'] = $run.settings
        $manifest['actionSummary'] = $actions
        $manifest['apologyOutcomesByDoer'] = $apologyOutcomes
        $manifest['recordingSha256'] = (Get-FileHash -LiteralPath $plan.json -Algorithm SHA256).Hash
        Save-Json $manifest $manifestPath
        Write-Host "Completed $($plan.name). Actual backends: $($backendCounts | ConvertTo-Json -Compress). Manifest: $manifestPath"
    }
    catch { $manifest.status = 'failed'; $manifest['error'] = $_.Exception.Message; Save-Json $manifest $manifestPath; throw }
}
