# Working in this repo (read me first)

This file is for any coding agent or model working here: Claude, DeepSeek, or anything else.
Read it before you change code. It is short on purpose; the detail is in the files it points to.

## What this is

A SMAPI (C#) mod for Stardew Valley 1.6 that makes NPCs less scripted:
- they remember what they saw;
- they bring it up the next day;
- they try to get the player's attention;
- they look for the player by asking each other.

It currently runs in **shadow mode**: it records memory and writes `[shadow]` lines to the SMAPI log
saying what NPCs *would* do, and changes nothing in the game. The typed decision model is **Laya**,
run locally. A deterministic fake is the default.

## Read in this order

1. `AGENTS.md` (this file): rules and workflow.
2. `docs/architecture.md`: how the pieces work and fit together.
3. `docs/decisions.md`: why they work that way. Read the entry before you change what it covers.
4. `docs/spec/`: the full spec. Every feature, built or planned, with its data model, hooks,
   model questions, rules, tuning, tests, status and open questions. Start at
   `docs/spec/README.md`; what to build next is `docs/spec/roadmap.md`. Read the feature's file
   before you build any part of it.
5. `stardew-npc-project-brief.md`: goals, original design, step-by-step history.
6. `stardew-source-notes.md`: game internals. Read it before answering anything about Stardew's
   code.

## Rules you must not break

1. **Shadow mode.** No code path may change game state (dialogue, NPC movement, mail, items,
   friendship) unless Sid has explicitly asked for that behaviour to go live. New behaviour ships
   as a `[shadow]` log line first.
2. **No true positions in decisions.** Only `ModEntry.CollectPresences` and
   `MemoryStore.Observe` read live positions. Anything that decides reads memory only: ledger
   views, diaries, routine beliefs, `Whereabouts`.
3. **Co-located means same location and within 8 tiles** (Chebyshev), never the same region.
4. **Deterministic.** Use seeded `Random` or `Fnv1a`, never `string.GetHashCode`. Iterate NPCs
   in name order.
5. **No model calls on the game thread**, including during the save. Use `IntentPlanJob` or
   `BackgroundLadder`, always through `ResilientDecisionClient` (timeout plus fallback). Never
   touch `Game1` from a background thread.
6. **The model never writes text.** It answers typed questions (choice, score, yes/no). Lines are
   templated and must pass `LineSanitizer.Sanitize`, which strips `#`, `$`, `%`, `{` and `[`.
7. **Mark what you haven't verified.** Anything recalled rather than confirmed in source, docs or
   in-game gets a `VERIFY` comment in code or a "verify" note in docs. Don't invent APIs; if unsure,
   say so. How to settle one: `docs/spec/README.md`, "Verifying game facts".
8. **Laya facts come only from** github.com/NandhaKishorM/laya and
   huggingface.co/convaiinnovations/laya, not blog posts.

## Workflow

- **Branches.** Never commit to `main`. Branch from the latest `main` (`claude/...`,
  `deepseek/...`) and open a PR; Sid reviews and merges on GitHub.
- **Other agents.** More than one may work here. Run `git status` first and don't commit changes
  you didn't make.
- **Tests.** Every change comes with tests. Before committing:
  ```bash
  dotnet test NpcSchedules.sln
  dotnet build mod/StardewNpcMod
  ```
  All tests must pass and the mod must build (it compiles against the installed game). Warning
  CS8032 about SMAPI analyzers is expected on the installed SDK 6.0.300.
- **Docs.** When behaviour changes, update `docs/architecture.md`, `docs/decisions.md`, the test
  counts in `README.md`, the status in the brief, and the feature's **Status** section in
  `docs/spec/`, in the same PR. If you build something differently from the spec, change the spec
  too.
- **Commits.** Messages say what changed and why, like the existing history.

## Running it in the game

The build never deploys itself (`EnableModDeploy=false`, on purpose). To try a build, copy it into
the game. On Sid's PC the game is at `D:\SteamLibrary\steamapps\common\Stardew Valley`:

```bash
cp mod/StardewNpcMod/bin/Debug/net6.0/*.dll mod/StardewNpcMod/bin/Debug/net6.0/*.pdb mod/StardewNpcMod/bin/Debug/net6.0/regions.json mod/StardewNpcMod/bin/Debug/net6.0/temperament*.json mod/StardewNpcMod/manifest.json "/d/SteamLibrary/steamapps/common/Stardew Valley/Mods/StardewNpcMod/"
```

- **Timing.** Close the game first; changes load on the next launch.
- **Log.** `%APPDATA%\StardewValley\ErrorLogs\SMAPI-latest.txt`. Filter for `[shadow]`.
- **Viewer.** While the game runs, open `http://127.0.0.1:8765/` in a browser: the NPC Minds page
  shows every NPC's urge, rung, knowledge of the player, plan and diary, live, plus every model
  call. It only reads. The page is `src/NpcMinds/viewer/index.html`, embedded in `NpcMinds.dll`,
  so the usual copy deploys it.
- **Settings.** `Mods/StardewNpcMod/config.json` is created on first run. `DecisionBackend` is
  `Fake` or `Laya`; the Laya server setup is in `sidecar/README.md`.
- **Test save.** Sid tests with the save `BUNKO_450391925`.

## Things that will bite you

- **Two threads, two owners.** `MemoryStore` belongs to the game thread; the ladder belongs to its
  worker. Pass copies or immutable records between them.
- **The night order.** `DayEnding` → the 6:00 `TimeChanged` (already the new date) → `Saving` →
  `DayStarted`. Most "morning" work actually happens at that 6:00 tick, before `DayStarted`.
- **Records grow at the end.** `LedgerView`, `InitiationInput`, `InitiationEvent` and
  `Whereabouts` are positional records used across projects. Add new fields only at the end, with
  defaults.
- **Ladder steps are saved as integers.** Never insert or reorder values in `InitiationStep`.
- **Tuning is not saved.** Ledger thresholds and every `*Options` class are code defaults, so a
  change applies to old saves. `RoutineBelief.UnlockThreshold` is the exception (known issue).
- **Save format.** Changing it means bumping `MemoryStore.CurrentVersion` and adding a migration
  with a test.
- **Region names are data.** They live in `data/regions.json`, with tests that pin known entries.
  Player-facing names live in `PlaceNames` and NPC voices in `VoiceSheets`.
- **Don't trust names alone.** `SeedShop` is Pierre's store, `JoshHouse` is 1 River Road, and
  `ScienceHouse` is Robin's shop.

## Where to add things

| To add... | Start at | Remember |
|---|---|---|
| A new diary kind (e.g. a gift) | the mod's event hook, then `MemoryStore.DiaryOf(npc).Append` | Give `LineRenderer` a template, or add the kind to `IntentPlannerOptions.SkipKinds`; once motives are built, also give it a row in the stressor table (feeling and juiciness, `docs/spec/motives.md`) |
| A new model question | `IDecisionClient` callers | Call through `ResilientDecisionClient`, off the game thread; give a deterministic fallback |
| A new ladder step | the end of `InitiationStep`, `Available`, the `StepThresholds` array, the response window in `ResolveAt` | Add a cap if it's passive |
| A player-facing place name | `PlaceNames` | Add a test in `PlannedLineDateAndPlaceTests` |
| A setting | `ModConfig` | Document it in `docs/architecture.md` |
| Something to show in the viewer | `NpcMinds.Models` (a field at the end), `MindsSnapshotBuilder`, `viewer/index.html` | Build it from memory on the game thread; never call the model or change state for it |
| A temperament trait or signal | `TemperamentScorer.Recipes` / `DialogueFeatures` | Regenerate `fixtures/game/temperament/` in the same PR (`docs/spec/temperament.md`) |
