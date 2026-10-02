# Stardew NPC Mod

SMAPI (C#) mod for Stardew Valley 1.6 that makes NPCs feel less scripted. See `stardew-npc-project-brief.md` for the design and `stardew-source-notes.md` for the verified game internals. Progress so far: extractor (step 1), NPC memory layer (step 2), shadow-mode harness (step 3), live SMAPI scaffold with persistence + decision client (step 4), overnight intents in shadow mode (step 5), audit fixes (step 6), a real Laya client (step 7), the initiation ladder in shadow mode (step 8), and finding the player (step 9). On the roadmap (`docs/spec/roadmap.md`), the motives engine of step 14 is built as a library and waits to be wired into the mod.

**Working on the code?** Start with [`AGENTS.md`](AGENTS.md), then [`docs/architecture.md`](docs/architecture.md) (how it works) and [`docs/decisions.md`](docs/decisions.md) (why). The full spec and roadmap are in [`docs/spec/`](docs/spec/README.md).

## Layout

| Path | What |
|---|---|
| `src/NpcSchedules/` | Game-independent library (net6.0, same runtime as Stardew 1.6). The mod will reference it. |
| `src/NpcMemory/` | NPC memory layer: `Diary` (event log), `Ledger` (last-seen + same-day decay, gone at the next 6:00, two-hop gossip that never overwrites fresher knowledge), `RoutineBelief` (co-presence routine learning, best guess per hour), `Proximity` (tile-radius co-location), `MemoryStore` (per-tick observation from the NPC side, asking around and looking for someone, save format and migration), `GameClock` (year-aware ticks). References NpcSchedules. |
| `src/NpcDecision/` | Typed decision client: `IDecisionClient` (choice / score / yes-no), a deterministic fake, a timeout-and-budget fallback wrapper, and `LayaDecisionClient` for a local `laya-serve`. |
| `src/NpcIntents/` | Overnight-intent layer: `IntentPlanner` (who speaks + about what via the decision client), `IntentPlanJob` (runs planning off the game thread with a budget), `Newsworthiness` (news scoring), `LineRenderer` (templated first-person lines), `PlaceNames`, `LineSanitizer`, `VoiceSheets`. References NpcMemory + NpcDecision. |
| `src/NpcInitiation/` | Initiation ladder (shadow mode): per-NPC urge, mildest fitting step, caps, ignored attempts, going to look for the player; `BackgroundLadder` runs it off the game thread; `PlayerSearch` decides who asks around. |
| `src/NpcMotives/` | Motives (step 14, shadow): a character acts only with a motive and enough boldness for the act. `MotivesEngine` (motives, netting, the act rule, close calls), `Stresses` and `StressorTable` (the fading part of feelings), `RegardBook` and `RegardKeeper` (the lasting part, saved per pair), `MoodRoll`, and `MotivesRunner`/`BackgroundMotives` (pacing, the model's questions and the grudge, on a worker). Built and tested; not wired into the mod yet. |
| `src/NpcLive/` | The first live acts (D30): emotes and speech bubbles from the motives runner, each behind its own switch (off by default). `LivePlanner` picks the emote or the templated line, `LiveGate` holds an act back at a bad moment, `LiveBreaker` turns a switch off after an error, `LiveLedger` writes an ignored live act to the diary. Built and tested; not wired into the mod yet. |
| `src/NpcBoard/` | The notice-board experiment (roadmap step 21): `NoteReactions` asks how each villager would react to a note in the player's words (a typed choice over six reactions; the note is quoted data, cleaned and capped at about two sentences) and maps the reaction to a confirmed emote and a templated line. `sidecar/eval/run_notes.py` runs the same question against a local Laya. Not in the mod yet. |
| `src/NpcMinds/` | The NPC Minds viewer (read-only): `MindsSnapshotBuilder` (what each NPC knows and wants, from memory and the ladder's last state), `RecordingDecisionClient` (copies every model call to a log, answers unchanged), `MindsServer` (a loopback page and `state.json` at `http://127.0.0.1:8765/`). The page is `viewer/index.html`, embedded in the DLL. Each card also shows the NPC's seed temperament (`temperament.json`, shipped in the mod folder) and, once the motives runner is wired, its motives and the act rule's parts. `Playtest/` is the playtest log (one JSON-lines file per save and day; `tools/playtest_summary.py` reads it). |
| `src/NpcDiaryEvents/` | Pure diary producers: `GiftNotes`, `SawGiftNotes`, `QuestNotes`, `FestivalNotes` turn plain event records into `DiaryEntry` values (no game types). The mod's read-only Harmony postfixes in `mod/StardewNpcMod/Patches/` capture the events. |
| `docs/` | `architecture.md` (how it works), `decisions.md` (why) and `spec/` (every feature, built or planned, plus the roadmap). `AGENTS.md` at the root is the entry point for coding agents. |
| `sidecar/` | How to run Laya locally (`laya-serve`), run scripts, and a smoke test. `sidecar/eval/` holds the golden eval set + runner + results (typed-decisions 5/6 vs english 3/6). |
| `src/NpcShadow/` | Shadow-mode harness: simulates days from schedules, drives the memory layer, logs what the mod would do (changes nothing). |
| `mod/StardewNpcMod/` | The SMAPI mod (compile-verified against the real game + SMAPI). Hooks SaveLoaded/DayStarted/TimeChanged/DayEnding/Saving/ReturnedToTitle; persists memory per save; each tick every NPC records the player and other NPCs in the same location within 8 tiles; shadow-logs overnight intents and ladder attempts. `config.json`: `DecisionBackend` = `Fake` or `Laya`. While the game runs, the NPC Minds viewer is at `http://127.0.0.1:8765/` (`MindsViewer`, `MindsViewerPort`). |
| `tools/ScheduleExtractor/` | Command-line wrapper: schedule JSON files in, counts out. |
| `src/NpcTemperament/` | Seed temperaments: splits dialogue into pages, counts mood and word signals, and scores six behaviour traits and six Ekman emotion biases per character with the game's `Data/Characters` traits (`docs/spec/temperament.md`). The mod loads the table for the viewer; the motives engine reads it. |
| `tools/TemperamentExtractor/` | Command-line wrapper: unpacked dialogue + game traits in, `temperament.json` / `temperament.md` out; `character_traits.py` decodes `Data/Characters.xnb`. |
| `tests/NpcSchedules.Tests/` | xUnit tests (71). |
| `tests/NpcMemory.Tests/` | xUnit tests (170). |
| `tests/NpcShadow.Tests/` | xUnit tests (31). |
| `tests/NpcDecision.Tests/` | xUnit tests (125). |
| `tests/NpcDiaryEvents.Tests/` | xUnit tests (65). |
| `tests/NpcIntents.Tests/` | xUnit tests (184). |
| `tests/NpcInitiation.Tests/` | xUnit tests (71). |
| `tests/NpcMinds.Tests/` | xUnit tests (71). |
| `tests/NpcMotives.Tests/` | xUnit tests (64). |
| `tests/NpcBoard.Tests/` | xUnit tests (7). |
| `tests/NpcLive.Tests/` | xUnit tests (7). |
| `tests/NpcTemperament.Tests/` | xUnit tests (23). |
| `data/regions.json` | Location-to-region map, block size, rain weights, home overrides. Editable without rebuilding. |
| `fixtures/game/*.json` | **Real 1.6 schedule data**, unpacked from this machine's copy of the game with xnbcli (see notes). 32 NPCs. |
| `fixtures/game/temperament/` | Game traits per villager (`characters.json`, decoded from `Data/Characters`), the draft seed table (`temperament.json`, plus `temperament.md` for review) and hand overrides. Dialogue itself is not committed. |
| `fixtures/wiki/Abigail.json` | Abigail's schedule as quoted on the wiki's Modding:Schedule data page (1.5.1-era data, kept for comparison). |
| `fixtures/synthetic/Testy.json` | An invented NPC that exercises every rule (GOTO, NOT friendship, MAIL, `a` times, omitted location, `bed`, time 0, bad data). Not game data. |

## What the extractor does

For each NPC it simulates a full year (4 seasons x 28 days), picks the schedule key the game would pick on each day (sunny/rainy variants by the season's rain weight), follows `GOTO` / `NOT friendship` / `MAIL` chains, and fills the day's 120 ten-minute ticks (6:00 to 2:00) with where the script puts the NPC. It then sums ticks by **region x 2-hour block**.

Outputs, per NPC: ticks and shares by region and block, the NPC's inferred home, how many days each key chain was used, unmapped locations, and warnings.

`RoutinePrior.Build(routine, observer, saveSeed, options)` turns that into the rough belief another NPC starts with: each block is smeared into its neighbours by a random amount, each region gets log-normal noise, and the result is scaled to a pseudo-count strength (family high, friends low). Same inputs and seed always give the same prior. Seeds use FNV-1a, not `string.GetHashCode` (randomized per process).

### The game rules it mirrors (verified against decompiled 1.6)

- **Key order** is the exact `NPC.TryLoadSchedule` unmarried sequence, including the `rain2` coin flip, Pam's `bus` key (needs `ccVault` mail), and the game's `tryHearts--` double-decrement quirk in the `<season>_<dow>_<hearts>` and `<dow>_<hearts>` loops. Day 1 of every season is a Monday.
- **Command semantics** mirror `NPC.parseMasterScheduleImpl`: initial `GOTO` (with `season`), `NOT friendship` (any pair met -> spring), `MAIL` (not received -> next command, received -> the one after), `GOTO NO_SCHEDULE`, cycle detection, and the game's error behaviour (a parse failure or duplicate time gives the NPC an empty schedule, not a fallback).
- **Tick math** mirrors the clock: 120 ticks per day, minute field only ever 0-50, `timeOfDay` capped at 2600.

Not modelled (documented in code): marriage keys, passive festivals, the year-1 `GreenRain` key, the island resort, locked-location replacement schedules, and walking time (`a` arrive-by times are kept as written, minus the walk adjustment).

### Defaults

- **Blocks:** 2 hours, so 10 blocks from 0600 to 2600. Change `blockMinutes` in `regions.json`.
- **Regions:** Farm, Town, Mountain, Forest, Beach, Desert, Island, plus `Other` for anything unmapped. Town includes BusStop and every town building; Backwoods counts as Farm.
- **Rain weights:** spring 18.3%, summer 12%, fall 18.3%, winter 0 — the wiki's documented vanilla values; confirm against `Data/Locations` if it matters.
- **Player state:** 0 hearts, no mail received (a newcomer). `--hearts`, `--friend`, `--mail` change it.

## Getting the real schedule files

The game ships them as `Content/Characters/schedules/*.xnb`. Two ways:

1. **Unpack them** (how `fixtures/game/` was made): [xnbcli](https://github.com/LeonBlade/xnbcli) reads these dictionary XNBs directly — `node xnbcli.js unpack "<game>/Content/Characters/schedules" out`. Each output JSON has `header`/`readers`/`content`; the tool wants the flat `content` object. On machines without a C++ toolchain, stub the `lz4` require (`const LZ4 = null;` — the schedule files are LZX, so it is never hit).
2. **In the mod**, skip files entirely: load each NPC's raw schedule with `helper.GameContent.Load<Dictionary<string, string>>("Characters/schedules/<Name>")` or `npc.getMasterScheduleRawData()` (both verify) and call `RoutineExtractor.Extract` directly. This also picks up other mods' schedule edits.

## Running

Needs the .NET 6 SDK or newer (the library targets net6.0).

```
dotnet test
dotnet run --project tools/ScheduleExtractor -- --schedules fixtures/game --out out
dotnet run --project tools/ScheduleExtractor -- --schedules fixtures/synthetic --out out --seed 42 --observer Pierre --strength friend --friend Player:5
```

Writes `out/routines.json`, `out/routines.csv` (`npc,region,block,ticks,share`), and with `--seed` a `prior-<observer>-<seed>.json`. It prints unmapped locations and warnings so the region map can be fixed.

## Remaining recalls to verify

- The rain weights above (against `Data/Locations`).
- SMAPI method names for the mod step (`getMasterScheduleRawData`, the `GameContent.Load` asset path).
- No-schedule days count at the inferred home; the game puts the NPC at its Data/Characters default map — pass the real home in the mod (`regions.json` `homes` supports overrides today).
