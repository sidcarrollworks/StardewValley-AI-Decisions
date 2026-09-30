# 13. Config

**Status: partial.** Seven settings exist (`mod/StardewNpcMod/ModConfig.cs`). The spec adds the live
switches, day length, a sidecar launch option and a few knobs. Architecture, "config.json".

## Player-visible behavior

`Mods/StardewNpcMod/config.json`, created by SMAPI on first run and read once at `Entry` (restart the
game after editing). Whether SMAPI adds new keys to an existing file is unverified; if not, defaults
apply until the key is added by hand (architecture, "A new config option"). A Generic Mod Config Menu
page is possible later (see [multiplayer-compat.md](multiplayer-compat.md)).

## Settings

| Key | Default | Meaning | Status |
|---|---|---|---|
| `DecisionBackend` | `"Fake"` | `Fake`, `Varied` (seeded fake, [laya.md](laya.md)) or `Laya` | done (`Varied` planned) |
| `LayaUrl` | `"http://127.0.0.1:8000"` | Laya base URL, loopback | done |
| `LayaModel` | `"typed-decisions"` | checkpoint | done |
| `LayaApiKey` | null | bearer key if the server needs one | done |
| `DecisionTimeoutMs` | 1500 | per-call timeout | done |
| `PlanningBudgetMs` | 20000 | overnight model budget | done |
| `LadderMaxBacklog` | 6 | ladder queue before ticks drop | done |
| `LaunchSidecar` | false | start `sidecar/run-laya.ps1` with the game (roadmap decision 6) | planned |
| `Live` | all false | per-behavior live switches ([rollout.md](rollout.md)) | planned |
| `DayLengthMinutes` | 0 (vanilla) | real minutes per game day ([day-length.md](day-length.md)) | planned |
| `LogDiary` | false | log every new diary line at Trace | planned |

Not in config, on purpose: every tuning class (`InitiationOptions`, `IntentPlannerOptions`, ledger
thresholds, `NewsworthinessOptions`, ...). They are code defaults so tests pin them. Promote one to
config only when Sid wants to tune it in-game.

## Rules

- Every new setting has a default that keeps today's behavior.
- Document it here, in `docs/architecture.md`'s config table, and (for Laya settings) in
  `sidecar/README.md`.
- Validate on read: out-of-range numbers are clamped and logged once.

## Acceptance tests

- Defaults match this table (a test constructs `ModConfig` and compares).
- Clamping for `DayLengthMinutes`, timeouts and budgets.

## Status

Done: seven keys. Not started: the rest.

## Open questions

- In-game settings menu (Generic Mod Config Menu integration) or file only? Recommendation: file only
  until something goes live.
