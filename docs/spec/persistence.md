# 12. Save data and persistence

**Status: partial.** Version 2 saves memory and the ladder; version-1 saves migrate. The overnight
plan, recent lines, newcomer state and several small pieces are not saved. D19; architecture,
"Persistence".

## Player-visible behavior

Quitting and reloading changes nothing an NPC knows or plans. A line planned last night is still
pending after a reload; an NPC that was ignored still remembers it; newcomer-week visits happen on
their planned days.

## Data model

One SMAPI save-data entry, key `squid.StardewNpcMod.memory`, a `Dictionary<string, string>`
(`Helper.Data.WriteSaveData`). SMAPI 4.5.2 serializes it to JSON inside the save file itself, in the
game's `CustomData` under the key `smapi/mod-data/squid.stardewnpcmod/squid.stardewnpcmod.memory`
(lower-cased), so its size adds directly to the save file. It throws on a farmhand connected to a
remote host (the save lives on the host's computer).

| Key | Content | Status |
|---|---|---|
| `version` | `MemoryStore.CurrentVersion` | done (2) |
| `memory` | ledger, diaries, beliefs (`MemoryStore.ToJson`) | done |
| `ladder` | per-NPC ladder state, global counters | done |
| `intents` | today's `PlannedLine`s, with their day and delivered flag ([intents.md](intents.md)) | planned |
| `recentLines` | per NPC, last 20 delivered lines with day and cited (kind, subject) | planned |
| `newcomer` | `NewcomerPlan` ([newcomer-week.md](newcomer-week.md)) | planned |
| `talkedToday` | NPCs talked to today, for `Talked` and `PassedBy` ([diary.md](diary.md)) | planned |
| `letters` | pending letters and invitations ([invitations.md](invitations.md)) | planned |

**Additive keys need no version bump.** The loader already reads keys it finds and ignores the rest.
A new key is read with a default when missing, so a version-2 save loads fine into a build that adds
`intents`. Bump `CurrentVersion` only when an existing key's format changes, and then add a branch
in `ModEntry.LoadMemory` plus a migration test (the loader does not check the number today:
architecture, "Loading and migration").

Changes to existing values that need no bump:
- `RoutineBelief.UnlockThreshold` is still written but ignored on read ([routines.md](routines.md)).
- New diary kinds are just new `Kind` strings; `Detail` stays a string ([diary.md](diary.md)).

## Triggers and game hooks

Done: `Saving` writes (never waits on the model; the ladder part is its last finished snapshot);
`SaveLoaded` reads; `ReturnedToTitle` resets everything.

Planned: the new keys are written in the same `SaveMemory` call and read in `LoadMemory`. A
`PlannedLine` whose day is not today is discarded on load.

## Laya questions

None.

## Deterministic rules

- Saving is fast local serialization only; no model calls, no waiting (D14).
- Everything is serialized in name order, so the same memory gives the same bytes.
- An older build drops keys it doesn't know when it saves, so downgrading the mod loses newer data.
  That is acceptable, and worth a line in the release notes.

## Size

Estimate for a long save: ~30 diaries x 500 entries x ~80 bytes, JSON-in-JSON escaped, is about
1.3 MB; ledger and beliefs add ~200 KB. Measure it on Sid's save (log the byte count at `Saving`).
If it is over ~1 MB, reduce in this order: drop `Saw` entries about NPCs older than 28 days first
(the planner never cites anything older than yesterday anyway, and nothing else reads old `Saw`
lines), then lower `MaxDiaryEntries`.

## Tuning constants

`MaxDiaryEntries` 500 (not saved). Planned `DiaryKeepDays` 28 for NPC `Saw` entries.

## Acceptance tests

- Round-trip each new key; a missing key loads as its default; a plan from an earlier day is dropped.
- A version-2 save written by the current build loads into the new build unchanged (golden JSON in
  tests, kept as a fixture).
- Serialization is byte-identical for identical memory (determinism).
- In-game: save, quit, reload mid-day; the pending line, ladder state and newcomer plan are all
  present (log counts at load).

## Status

Done: `ModEntry.SaveMemory`/`LoadMemory`, `MemoryStore.ToJson`/`FromJson`/`FromVersion1`,
`InitiationLadder.ToJson`/`FromJson`. Not started: the five planned keys, size logging, pruning. Visit counters go inside the existing `ladder` value ([find.md](find.md)).

## Open questions

- How much a megabyte of mod data slows saving and loading. It lives inside the save file, so
  measure save time on Sid's save before and after the diary enrichment lands.
