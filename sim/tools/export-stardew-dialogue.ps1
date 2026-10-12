<#
.SYNOPSIS
Reads installed English Stardew assets into a local, provenance-preserving review corpus.
Does not launch the game, modify assets, download tools, or add game references to Under Glass.
#>
[CmdletBinding()]
param(
    [string] $GameDirectory = 'D:/SteamLibrary/steamapps/common/Stardew Valley',
    [string] $OutputDirectory = ('out/stardew-dialogue-' + (Get-Date -Format 'yyyy-MM-dd')),
    [switch] $RawOnly
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).ProviderPath
$outputPath = [IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))
$ignoredRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'out')) + [IO.Path]::DirectorySeparatorChar
if (-not $outputPath.StartsWith($ignoredRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'OutputDirectory must be inside this checkout''s ignored out/ directory.'
}
$rawPath = Join-Path $outputPath 'raw-assets.json'
if (Test-Path -LiteralPath $rawPath) { throw "Existing export preserved: $rawPath. Choose a new output directory." }
$gamePath = (Resolve-Path -LiteralPath $GameDirectory).ProviderPath
$contentPath = Join-Path $gamePath 'Content'
$gameDll = Get-Item -LiteralPath (Join-Path $gamePath 'Stardew Valley.dll')
Add-Type -Path (Join-Path $gamePath 'MonoGame.Framework.dll')
Add-Type -Path (Join-Path $gamePath 'StardewValley.GameData.dll')
if (-not ('UnderGlassCorpusServices' -as [type])) {
    Add-Type 'public sealed class UnderGlassCorpusServices : System.IServiceProvider { public object GetService(System.Type type) { return null; } }'
}
$reader = [Microsoft.Xna.Framework.Content.ContentManager]::new([UnderGlassCorpusServices]::new(), $contentPath)
$assets = [Collections.Generic.List[object]]::new()
$unsupported = [Collections.Generic.List[object]]::new()
function Asset-Record([string] $Asset, [string] $Format, $Entries) {
    $path = Join-Path $contentPath ($Asset + '.xnb')
    return [ordered]@{ asset = $Asset; sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash;
        format = $Format; entryCount = $Entries.Count; entries = $Entries }
}
try {
    $characters = $reader.Load[System.Collections.Generic.Dictionary[string,StardewValley.GameData.Characters.CharacterData]]('Data/Characters')
    $assets.Add((Asset-Record 'Data/Characters' 'character-metadata' $characters))
    $assetNames = [Collections.Generic.List[string]]::new()
    foreach ($directory in @('Characters/Dialogue', 'Characters/schedules', 'Data/Events', 'Data/Festivals', 'Strings', 'Strings/schedules')) {
        foreach ($file in Get-ChildItem -LiteralPath (Join-Path $contentPath $directory) -Filter '*.xnb' |
            Where-Object { $_.Name -notmatch '\.[a-z]{2}(?:-[A-Z]{2})?\.xnb$' } | Sort-Object Name) {
            $assetNames.Add($directory + '/' + $file.BaseName)
        }
    }
    foreach ($asset in @('Data/NPCGiftTastes', 'Data/ExtraDialogue', 'Data/EngagementDialogue', 'Data/mail', 'Data/Quests', 'Data/AnimationDescriptions')) {
        $assetNames.Add($asset)
    }
    foreach ($asset in $assetNames | Sort-Object -Unique) {
        try {
            $entries = $reader.Load[System.Collections.Generic.Dictionary[string,string]]($asset)
            # Event conditions are case-sensitive (for example o Emily vs O Emily).
            # PowerShell's default ordered hashtable would silently merge those keys.
            $orderedEntries = [Collections.Specialized.OrderedDictionary]::new([StringComparer]::Ordinal)
            foreach ($key in $entries.Keys | Sort-Object -CaseSensitive) { $orderedEntries[$key] = $entries[$key] }
            $assets.Add((Asset-Record $asset 'string-dictionary' $orderedEntries))
        }
        catch {
            $unsupported.Add([ordered]@{ asset = $asset; reason = $_.Exception.Message;
                sha256 = (Get-FileHash -LiteralPath (Join-Path $contentPath ($asset + '.xnb')) -Algorithm SHA256).Hash })
        }
    }
    $calls = $reader.Load[System.Collections.Generic.Dictionary[string,StardewValley.GameData.IncomingPhoneCallData]]('Data/IncomingPhoneCalls')
    $assets.Add((Asset-Record 'Data/IncomingPhoneCalls' 'phone-call-metadata' $calls))
    $movies = $reader.Load[System.Collections.Generic.List[StardewValley.GameData.Movies.MovieCharacterReaction]]('Data/MoviesReactions')
    $assets.Add((Asset-Record 'Data/MoviesReactions' 'movie-reaction-metadata' $movies))
}
finally { $reader.Dispose() }
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
$manifest = [ordered]@{
    schemaVersion = 1; createdUtc = [datetime]::UtcNow.ToString('o'); language = 'en';
    gameVersion = $gameDll.VersionInfo.FileVersion; gameDirectory = $gamePath;
    gameAssemblySha256 = (Get-FileHash -LiteralPath $gameDll.FullName -Algorithm SHA256).Hash;
    exporter = 'sim/tools/export-stardew-dialogue.ps1';
    characterNames = @($characters.Keys | Sort-Object -CaseSensitive);
    assets = $assets.ToArray(); unsupported = $unsupported.ToArray();
    scope = @('Installed base English assets; no mod or save state applied.',
        'Raw dictionary entries are not a count of spoken lines.',
        'Conditions and mutually exclusive histories remain source material, not simulated memories.',
        'Maps, code-selected speaker attribution, other structured Data assets, and dynamic substitutions require further coverage.')
}
$manifest | ConvertTo-Json -Depth 80 | Set-Content -LiteralPath $rawPath -Encoding utf8
Write-Host "Exported $($assets.Count) assets; $($unsupported.Count) unsupported assets. Raw source: $rawPath"
if (-not $RawOnly) {
    $pythonPath = Join-Path $repoRoot 'sidecar/.venv/Scripts/python.exe'
    if (-not (Test-Path -LiteralPath $pythonPath)) { $pythonPath = (Get-Command python -ErrorAction Stop).Source }
    & $pythonPath (Join-Path $PSScriptRoot 'stardew_dialogue_corpus.py') --input $rawPath --out $outputPath
    if ($LASTEXITCODE -ne 0) { throw 'Corpus parsing failed; raw export was preserved.' }
}
