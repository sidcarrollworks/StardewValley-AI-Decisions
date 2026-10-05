# 12. Save data and persistence

**Status: partial.** Version 2 saves memory, regard and the motives runner's pacing; version-1
saves migrate. The overnight plan and recent lines are saved (`intents`, `recentLines` — step 5);
newcomer state and several small pieces are not saved. The retired ladder (D31) is no longer saved.
D19; architecture, "Persistence".

## Player-visible behavior

Quitting and reloading changes nothing an NPC knows or plans. A line planned last night is still
pending after a reload; an NPC that was ignored still remembers it; newcomer-week visits happen on
their planned days.

## Data model

One SMAPI save-data entry, key `squid.StardewNpcMod.memory`, a `Dictionary<string, string>`
(`Helper.Data.WriteSaveData`). SMAPI 4.5.2 serializes it to JSON inside the save file itself, in the
game's `CustomData` under the key `smapi/mod-data/squid.stardewnpcmod/squid.stardewnpcmod.memory`
(lower-cased), so its size adds directly to the save file. Both `ReadSaveData` and `WriteSaveData`
throw on a farmhand connected to a remote host, since the save lives on the host's computer
(verified, SMAPI 4.5.2 `DataHelper.cs:181-226`); split-screen guests may use them.

| Key | Content | Status |
|---|---|---|
| `version` | `MemoryStore.CurrentVersion` | done (2) |
| `memory` | ledger, diaries, beliefs (`MemoryStore.ToJson`) | done |
| `ladder` | per-NPC ladder state, global counters | retired (D31): no longer written; an old save's is left unread and dropped at the next save |
| `intents` | today's `PlannedLine`s, with their day and delivered flag ([intents.md](intents.md)) | done (step 5) |
| `recentLines` | per NPC, last 20 delivered lines with day and cited (kind, subject) | done (step 5; empty until step 6 delivers) |
| `newcomer` | `NewcomerPlan` ([newcomer-week.md](newcomer-week.md)) | planned |
| `talkedToday` | NPCs talked to today, for `Talked` and `PassedBy` ([diary.md](diary.md)) | planned |
| `letters` | pending letters and invitations ([invitations.md](invitations.md)) | planned |
| `trades` | today's trade offers and which were taken ([trades.md](trades.md)) | planned |
| `stats` | 28 days of daily counters for the playtest digest ([debug-tools.md](debug-tools.md)) | planned |
| `regard` | signed regard per (observer, subject), sparse, the player included: the plastic part of feelings, the grudge, and NPC-to-NPC bonds ([motives.md](motives.md), [town-life.md](town-life.md)); replaces the planned `bonds` key. `RegardBook.ToJson`: `{"observer\|subject": value}`, rounded to 4 places; a missing, damaged or malformed value loads empty | built and saved (wired, step 14 part 3) |
| `historySeeded` | `"1"` once the history at install has been seeded into `regard` (D29, [vanilla-sources.md](vanilla-sources.md)); missing means not yet | built (wired, 2026-10-02) |
| `motives` | the motives runner's pacing state: per NPC the day's attempts, frustration, open and waiting attempts, motives used up, recent vents, the last grudge question and penalty day; the town's daily and weekly counters (`MotivesRunner.ToJson`; damaged loads fresh) | built and saved (wired, step 14 part 3) |

**Additive keys need no version bump.** The loader already reads keys it finds and ignores the rest.
A new key is read with a default when missing, so a version-2 save loads fine into a build that adds
`intents`. Bump `CurrentVersion` only when an existing key's format changes, and then add a branch
in `ModEntry.LoadMemory` plus a migration test (the loader does not check the number today:
architecture, "Loading and migration").

Changes to existing values that need no bump:
- `RoutineBelief.UnlockThreshold` is still written but ignored on read ([routines.md](routines.md)).
- New diary kinds are just new `Kind` strings; `Detail` stays a string ([diary.md](diary.md)).

## Triggers and game hooks

Done: `Saving` writes (never waits on the model; the `motives` part is the worker's last finished
snapshot); `SaveLoaded` reads; `ReturnedToTitle` resets everything except the live ledger (audit
2026-10-05, below).

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

`MaxDiaryEntries` 500 (not saved). Over the cap, the entries least worth keeping go first (`DiaryKeep`, D35): plain sightings and entries about people the NPC cares little about, the oldest first. This replaces the planned `DiaryKeepDays`.

## Acceptance tests

- Round-trip each new key; a missing key loads as its default; a plan from an earlier day is dropped.
- A version-2 save written by the current build loads into the new build unchanged (golden JSON in
  tests, kept as a fixture).
- Serialization is byte-identical for identical memory (determinism).
- In-game: save, quit, reload mid-day; the pending line, ladder state and newcomer plan are all
  present (log counts at load).

## Status

Done: `ModEntry.SaveMemory`/`LoadMemory`, `MemoryStore.ToJson`/`FromJson`/`FromVersion1`. The
`regard`, `motives` and `historySeeded` values are built, tested and written (wired with the
motives runner, step 14 part 3, and D29). A diary over the cap is trimmed at its next entry, the
least worth keeping first (`DiaryKeep`, D35); a save loaded with more is kept whole until then.

Found in the audit (2026-10-05), not fixed yet (Sid's call):
- `OnSaveLoaded` sets `_planToday` back to empty right after `LoadMemory` restored it from
  `intents` (the clear predates step 5's plan persistence). After a reload the motives see no news
  from the plan, the viewer shows no planned line, and the next save writes no `intents`.
  `_intentsToday` does survive.
- `MemoryStore.FromJson` builds the store with the default chat seed, not the save's.
- `LiveLedger` (shown acts awaiting an answer) is not reset at the title screen. Not started: the
other planned keys, size logging, pruning. Visit counters and each NPC's last friendship-penalty
day are in the `motives` value; the grudge is the negative part of `regard` ([motives.md](motives.md)).

## Open questions

- How much a megabyte of mod data slows saving and loading. It lives inside the save file, so
  measure save time on Sid's save before and after the diary enrichment lands.
