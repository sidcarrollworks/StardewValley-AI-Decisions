# How it works

A map of this repo for whoever maintains it next, human or model. It was checked against the code
at commit `c97a829` (step 9, Find included) on branch `claude/audit-fixes-initiation`. When the code
and this file disagree, the code wins: fix this file in the same PR. `git log c97a829..` shows what
changed since.

- Paths are relative to the repo root. **verify** marks anything not confirmed in code or in-game.
- Rules and workflow: `AGENTS.md`. Why things are this way: `docs/decisions.md`. Game internals:
  `stardew-source-notes.md`. Goals and status: `stardew-npc-project-brief.md`. Laya: `sidecar/README.md`.

## What the mod does today

`mod/StardewNpcMod` is a SMAPI mod for Stardew Valley 1.6 (the owner runs SMAPI 4.5.2 on game
1.6.15). It runs entirely in **shadow mode**: it records memory and writes `[shadow]` lines to the
SMAPI log saying what NPCs would do, and it changes no game state. No dialogue is added, no NPC
moves, no letter is sent. Every ten-minute tick, each villager NPC records the player and the other
NPCs it can "see" (same location, within 8 tiles) in its own memory: a last-seen ledger, a diary and
routine counts. At day end the mod plans on a background task which NPCs would say something
tomorrow, and logs those lines. Through the day the initiation ladder decides whether each NPC would
try to get the player's attention (emote, bubble, approach, queued line, letter, forced dialogue) and
logs the attempts and how they ended. NPCs that miss the player also ask the NPCs around them and may
go looking (see Finding the player). Decisions are typed numbers from a local Laya server or a
deterministic fake; the model never writes text. Memory and ladder state are saved per save file.
The motives engine that will replace the ladder's urge (roadmap step 14, D24) is built and tested
in `src/NpcMotives` but not yet wired into the mod (see Motives).

## Projects and dependencies

All projects target `net6.0` (the game's runtime) and are in `NpcSchedules.sln`. Everything under
`src/` is game-independent (no SMAPI or game references), so it is unit-tested; only
`mod/StardewNpcMod` touches the game.

| Project | Owns | References | In the mod |
|---|---|---|---|
| `src/NpcSchedules` | `TimeUtils` (tick math), `RegionMap` (reads `data/regions.json`), `Fnv1a` (seeds); `ScriptParser`, `ScheduleSimulator`, `RoutineExtractor`, `RoutinePrior` (schedules to routines) | nothing | yes; live code uses only `TimeUtils`, `RegionMap`, `Fnv1a` |
| `src/NpcMemory` | `GameClock`/`GameTime`, `Diary`, `Ledger`, `RoutineBelief`, `Proximity`, `MemoryStore` + `Presence`; `Whereabouts` (Find) | NpcSchedules | yes |
| `src/NpcDecision` | `IDecisionClient`, `FakeDecisionClient`, `ResilientDecisionClient`, `LayaDecisionClient` + `LayaOptions` | nothing | yes |
| `src/NpcIntents` | `IntentPlanner`, `IntentPlanJob`, `LineRenderer`, `PlaceNames`, `LineSanitizer`, `VoiceSheets` | NpcMemory, NpcDecision | yes |
| `src/NpcInitiation` | `InitiationLadder`, `BackgroundLadder`, `InitiationOptions`; `PlayerSearch` (Find) | NpcMemory, NpcDecision (NpcSchedules via NpcMemory) | yes |
| `src/NpcMotives` | the motives engine (step 14): `MotivesEngine`, `Stresses`, `StressorTable`, `RegardBook`, `RegardKeeper`, `RegardHistory`, `MoodRoll`, `MotivesRunner`, `BackgroundMotives`, `MotiveInputBuilder`, `MotiveText` | NpcMemory, NpcDecision, NpcTemperament | built, through NpcMinds; not run yet |
| `src/NpcMinds` | the NPC Minds viewer: `MindsSnapshotBuilder`, `RecordingDecisionClient`, `RingLog`, `MindsServer`, the embedded `viewer/index.html`; the playtest log (`Playtest/`) | NpcMemory, NpcDecision, NpcIntents, NpcInitiation, NpcTemperament, NpcMotives | yes (read-only) |
| `src/NpcLive` | the first live acts (D30): `LiveSwitches`, `LiveOptions`, `LivePlanner` (a runner `Act` event to an emote id or a templated bubble line), `LiveGate` (the last "not now" check), `LiveBreaker` (circuit breaker per switch), `LiveLedger` (shown acts, so an ignored one is written as `IgnoredBy`) | NpcMotives, NpcIntents | not wired yet |
| `src/NpcShadow` | `DayPlanner`, `ShadowSimulator`, `ShadowLog` | NpcSchedules, NpcMemory | no (tests only) |
| `tools/ScheduleExtractor` | command line: schedule JSON in, region x block counts out | NpcSchedules | no |
| `src/NpcTemperament` | `DialogueText`, `DialogueFeatures`, `TemperamentScorer`, `Temperament`, `TemperamentTable` (seed personality values) | nothing | yes: the viewer's temperament line; the motives engine reads it |
| `tools/TemperamentExtractor` | command line: unpacked dialogue + game traits in, seed table out | NpcTemperament | no |
| `mod/StardewNpcMod` | `ModEntry` (every game hook), `ModConfig`, `manifest.json` | the five "yes" projects; game + SMAPI via `Pathoschild.Stardew.ModBuildConfig` 4.3.1 | - |
| `tests/<Name>.Tests` | xUnit tests for `src/<Name>` | that project only | no |

```
mod/StardewNpcMod --+--> NpcIntents -----+--> NpcMemory --> NpcSchedules
                    +--> NpcInitiation --+
                    +--> NpcMinds -------+--> NpcDecision
                    |                    +--> NpcMotives --> NpcMemory, NpcDecision, NpcTemperament
                    +--> NpcMemory, NpcDecision, NpcSchedules, NpcTemperament (also directly)

src/NpcShadow ----------> NpcMemory, NpcSchedules   (tests only)
tools/ScheduleExtractor -> NpcSchedules             (command line)
tools/TemperamentExtractor -> NpcTemperament        (command line)
```

## The time model

The game clock runs from 6:00 to 2:00. `timeOfDay` rises by 10 per tick, minutes wrap at 60, and
2600 is the cap, never a live tick. So a day is exactly **120 ten-minute ticks** (verified against
decompiled 1.6; `stardew-source-notes.md`, "Clock").

| Term | Meaning | Code |
|---|---|---|
| tick of day | 0..119. Tick `k` is time `600 + (k/6)*100 + (k%6)*10`: 0 = 6:00, 6 = 7:00, 119 = 1:50 AM (2550) | `TimeUtils.TickIndex` (-1 outside the live day or off the 10-minute grid), `TimeUtils.TimeOfDay` |
| absolute tick | counts from spring 1, year 1, 6:00 (= 0): 120 a day, 3,360 a season, 13,440 a year | `GameClock.AbsoluteTick`, `GameClock.FromAbsoluteTick` |
| `GameTime` | `(SeasonIndex 0..3, DayOfMonth 1..28, Tick 0..119, Year = 1)`; a year below 1 counts as 1 | `src/NpcMemory/GameClock.cs` |
| DayIndex | `absoluteTick / 120`: 0 = spring 1 of year 1, 112 days a year | `GameClock.DayIndex`, `DayStartTick`, `DaysBetween` |
| block | 2-hour bucket (`blockMinutes` 120 in `data/regions.json`): 10 a day, labelled "0600".."2400" | `TimeUtils.BlockIndex`, `BlockLabel` |
| week | `DayIndex / 7`, Monday to Sunday (day 1 of every season is a Monday) | the ladder's forced-dialogue cap |

The mod converts the clock with `ModEntry.Now(tick)`, which reads `Game1.currentSeason`,
`Game1.dayOfMonth` and `Game1.year`. Because the year is included, a year-1 memory never looks fresh
in year 2.

**Why a day starts at 6:00.** The night has no ticks: 1:50 AM and the next 6:00 are adjacent absolute
ticks. A calendar day here is the game day, 6:00 to 2:00, and everything before the next 6:00 belongs
to it (a 1:30 AM sighting is still "today"; at 6:00 it is Gone). Anything that must not cross the
night compares `DayIndex`, never tick distance: ledger decay, diary spans, "yesterday" in overnight
lines, the ladder's daily caps and response windows.

## The per-tick flow in the mod

**Single-player guard** (`HostOnly`, docs/spec/multiplayer-compat.md): only the host's main
screen runs the mod. A farmhand on a remote host logs one line at load and stays off, before
`LoadMemory`, since it can't read or write save data. Every handler and both Harmony postfixes
return early when `Context.IsMainPlayer` is false. A host in multiplayer gets one warning that
other farmers are ignored.

Every game hook is in `mod/StardewNpcMod/ModEntry.cs`. Each handler catches and logs its own
exceptions. In `OnTimeChanged`, steps 1-3 and steps 4-5 are separate try blocks, so a memory failure
does not stop the ladder.

```
TimeChanged(e.NewTime)                                     game thread
 |  tick = TimeUtils.TickIndex(e.NewTime); stop if -1;  now = Now(tick)
 |
 |- 1. NoteSpecialOrders(now)      diff team.completedSpecialOrders -> QuestHelped (Special)
 |- 2. _events.Drain(...)          queued GiftReceived / SawGift / QuestHelped into memory
 |                                 (SawGift witnesses: the span tracker's last Observe)
 |- 3. CollectPresences()          the only live-position read: every villager in
 |                                 Game1.locations, plus the player's own location
 |- 4. _memory.Observe(now, ...)   Ledger.Record, RoutineBelief.Observe, diary "Saw"
 |- 5. AskAround(now)              Find: NPCs that miss the player ask around
 |- 6. RunLadder(now)
 |      inputs, one per NPC that has a diary, in name order:
 |        (npc, Ledger.View(npc, "Player", now), npc in _intentsToday, hearts, Find lead)
 |      _ladder.EnqueueTick(now, inputs) -------> worker: InitiationLadder.Tick
 |                                                (YesNo via ResilientDecisionClient)
 |      _ladder.Drain() <------------------------ finished results; never blocks
 |        append TriedToReach / IgnoredBy lines to the diaries (IgnoredBy only once the rung is
|        live — `RecordIgnoredBy`); log [shadow] events
 |- 7. CollectPlan(morning: false) if the overnight plan is ready: log its lines,
 |                                 fill _intentsToday
 |- 8. RunMotives(now)
 |      inputs from RunLadder's _lastLadderInputs (the same views, leads and hearts),
 |      the plan's best news score per NPC, and the NPC card with a "today" line
 |      _motives.EnqueueTick(now, inputs) ------> worker: MotivesRunner.Tick
 |                                                 (YesNo via ResilientDecisionClient)
 |      _motives.Drain() <--------------------- finished decisions; never blocks
 |        log [shadow] motives lines (Act/Grudge/Responded at Info, the rest at Trace);
 |        Grudge relief -> RegardKeeper.Relieve; decision/regard playtest records
 |- 9. PublishMinds()              the viewer's snapshot (read-only; see NPC Minds viewer)
```

Festival capture is NOT in the tick: the clock is stopped for the whole festival
(`Game1.shouldTimePass` is false while `isFestival()`) and the one `TimeChanged` after it (the
22:00 jump) already sees `isFestival()` false, so `CaptureFestival()` runs on
`OneSecondUpdateTicked` instead.

`CollectPresences` builds a `Presence(name, location, tileX, tileY, isPlayer)` for every NPC with
`IsVillager` and `CanSocialize` (`Presences.Tracks`; the casino's Bouncer, Mister Qi, Gunther, Marlon
and the like stand in their maps from day one and are not watched), from `Game1.locations` plus `Game1.player.currentLocation` (building interiors are
not in `Game1.locations` — verified; the player's own location is appended so their occupants are
seen too). Each name appears once, the first seen winning (`Presences.OnePerName`): the game has two
characters named "Mister Qi". Hearts come from `Game1.player.getFriendshipHeartLevelForNPC` (verified, Farmer.cs:2785).
The mod assumes `TimeChanged` fires once per clock change; it is SMAPI's watcher on
`Game1.timeOfDay` — one event per value change, however big the jump, not fired on save-load or
while saving (verified).

| Other hook | What the mod does |
|---|---|
| `Entry` | reads `config.json`; loads `regions.json` from the mod folder (a missing or invalid file throws before any event is hooked, so the mod does nothing); loads `temperament.json` plus `temperament-overrides.json` (missing or invalid: a warning, and only the viewer loses temperaments); builds the decision backend; `ApplyPatches()` (the read-only Harmony postfixes, one list); logs `Shadow mode ready: ...` |
| `SaveLoaded` | `LoadMemory()`: fresh memory and ladder, then the save's data (see Persistence); seeds the special-order diff set |
| `MenuChanged` | response detection for the ladder (see the ladder section); first daily conversation -> `Talked`; conversations during a festival feed the Festival `with` key |
| `OneSecondUpdateTicked` | `CaptureFestival()` while `Game1.isFestival()` (attended + actor names); gated on `Context.IsWorldReady` |
| `ReturnedToTitle` | disposes the plan job; fresh memory, ladder, `PlayerSearch`, `_intentsToday`; clears the event queue, festival capture and special-order set |

### The night, in the order it really happens

Seen in the SMAPI log (`stardew-source-notes.md`, "Tools"): `DayEnding` -> the game's "NewDay" task ->
`TimeChanged` with 600, where the date is **already the new day** -> `Saving` -> `DayStarted`.

| # | Event | What the mod does |
|---|---|---|
| 1 | `DayEnding` | `NoteDayEnd()` writes the day's `Talked`/`PassedBy`/`BirthdayForgotten` notes and the `Festival`/`MissedFestival` notes into the diaries (Trace `[shadow] diary ...`), then `StartPlanning()`: disposes any old plan job, clears `_intentsToday`, snapshots every non-empty diary with a per-NPC `NewsContext` (homes, snapshot beliefs, hearts), starts an `IntentPlanJob` (seed and `sourceDay` = DayIndex of the day just ended) |
| 2 | "NewDay" task | nothing |
| 3 | `TimeChanged` 600 | an ordinary tick 0 of the new day: the ladder's first tick settles attempts left open overnight on their own day, then halves urge and resets rungs; `CollectPlan` usually collects the plan here |
| 4 | `Saving` | `SaveMemory()`: serialization only, never waits on the model |
| 5 | `DayStarted` | `CollectPlan(morning: true)`: if the plan is still running, logs that its lines will come later |

Two consequences. The ladder runs before `CollectPlan` in the 6:00 handler, so the intent boost
reaches the ladder from 6:10. And `_intentsToday` is cleared when planning starts, not at
`DayStarted`, or the 6:00 collection would be wiped (commit `694fa38`).

## Memory (`src/NpcMemory`)

One `MemoryStore` per save holds everything NPCs know. Names compare case-insensitively everywhere.
The player is recorded as `MemoryStore.PlayerName` = `"Player"`.

### Ledger (`Ledger.cs`)

One entry per (observer, subject): where the subject was last seen, when, and how the observer knows.
`Record(observer, subject, location, region, tick, spot)` stores a first-hand sighting (hop 0) and
always replaces the pair's previous entry. `View(observer, subject, now)` returns a `LedgerView`
(`Detail`, `Place`, `AgeTicks`, `HopCount`, `AbsoluteTick`, `Spot`, `ToldBy`), or null if no entry.

| Detail | When (age in ticks, same calendar day) | `Place` | `Spot` |
|---|---|---|---|
| `NamedSpot` | age < `SpotTtl` (12 = 2 h) and a spot was recorded | location | tile `"x,y"` |
| `Location` | age < `LocationTtl` (48 = 8 h); also any age < 12 without a spot | location | null |
| `Region` | age < `RegionTtl` (96 = 16 h) | region | null |
| `EarlierToday` | older, but still the same calendar day | null | null |
| `Gone` | the sighting was on an earlier calendar day (from the next 6:00) | null | null |

Decay is a pure function of the sighting tick and "now"; nothing is rewritten as time passes. The
thresholds are public properties and are **not** saved, so retuning them applies to old saves.

`Gossip(speaker, listener, subject, now)` passes knowledge on, or returns false and stores nothing:

- refused when speaker = listener, when the speaker has no entry for the subject, or its view is `Gone`;
- the listener's hop count is the speaker's + 1; more than 2 hops is refused;
- never overwrites fresher knowledge: a later sighting wins, and for the same sighting fewer hops win;
- the listener's entry is capped (`DetailCap`) at the speaker's detail at the time, so it can never
  be recalled more finely; the spot passes only at `NamedSpot`; age still counts from the original
  sighting; `ToldBy` records the speaker.

Position gossip happens when an NPC asks around (`MemoryStore.AskAround`; Find) and in ambient
chats (below), where each side passes its view of the player.

**Ambient chats and juiciness** (`MemoryStore.Chat`/`ChatHeard`, `Gossip`; D25, D33). Each tick,
right after `Observe`, NPC pairs co-located for `ChatMinTicks` (3) may chat once per span (an FNV-1a
draw under `ChatChance`, 0.3). In a chat each side may volunteer one story to the other as a `Heard`
entry. The mod passes `StressorTable.JuicinessOf` (base juiciness per diary kind) and `KnowsPerson`
(regard or a ledger entry):
- a story is any diary entry with a base juiciness: the teller's own, or a `Heard` they can retell;
- its current juiciness fades by whole days since the teller got it: `FadePerDay` (0.5), or
  `ScandalFadePerDay` (0.8) from `ScandalBase` (4); a plain `Saw` has none and is never volunteered;
- it is told when that, plus `KnowsSomeoneBonus` (0.5) if the listener knows someone in the story
  other than the teller, reaches `VolunteerLevel` (2); the juiciest such story wins;
- never to someone in the story (its subject, the person it started with, a giver), never twice to
  the same listener by any route (`Gossip.EventKey`), and to at most `RetellsPerDay` (3) listeners a
  day per teller and story (counted from the diaries, so a reload keeps it);
- the listener gets it at `RetellFactor` (0.7) of the teller's juiciness. The `Heard` detail adds
  `of` (whose diary it started in), `b` (base), `j` (juiciness when told), `at` (tick told) and
  `hops` to the original's keys; its tick stays the event's. A `Heard` without them (before D33)
  reads as one hop from its teller.
- A story never touches the position ledger. Hearsay is confirmed (lasting regard) when its
  teller is the person it started with (`RegardKeeper.FromSource`), or when the listener sees the
  same act by the same person within `ConfirmWindowDays` (7) of hearing it
  (`RegardKeeper.ConfirmedBy`, once per story); otherwise it stays elastic.
- How hard it lands (`Stresses.Hearsay`): the original's magnitude x `HearsayFactor` (0.5),
  decayed from when it was heard; x `MaxRelevance` (2) for a listener at `DrawnHearts` (8) or more
  with the player when the story is about the player, and a pleasing gift the player gave someone
  else turns into `Jealous`.
- What it can make the listener do (D34): a story about what the player did to someone else is
  `Stress.MoodOnly` (it moves the mood, not the `Grateful` or `Hurt` motive), unless it is a
  scandal: bad, and at `HearsayActsFromJuiciness` (4) or more, like rummaging in the trash.
  Jealousy still acts.
Without a juiciness function, `Chat` keeps the old rule (today's `GiftReceived`, `SawGift`,
`QuestHelped`, `Festival`, teller's own entries, one hop), which only tests use.
`SubjectsOf(observer)` lists an observer's subjects; `RemapTicks` exists for save migration. The JSON
is an `entries` array of objects with observer, subject, location, region, spot, absoluteTick,
hopCount, toldBy and detailCap, sorted by observer then subject.

### Diary (`Diary.cs`)

A per-NPC list of `DiaryEntry(AbsoluteTick, Subject, Kind, Detail)` in insertion order (not sorted by
tick). Queries: `Since(tick)` (inclusive), `Recent(n)` (newest first), `About(subject)`. `TrimTo(max)`
drops the oldest. JSON is a flat array of entries. `Kind` is a free-form string.

| Kind | Written by | Subject | Detail |
|---|---|---|---|
| `Saw` | `MemoryStore.Observe`, at the start of each co-located span | `"Player"` or an NPC | internal location name, e.g. `SeedShop` |
| `TriedToReach` | the ladder, when it makes an attempt | `"Player"` | step name, e.g. `Emote` |
| `IgnoredBy` | the ladder, when an attempt goes unanswered (once the rung is live; not while shadow) | `"Player"` | step name |

A **span** is a run of ticks in which the same observer and subject stay co-located. It continues
only from the immediately previous tick of the same calendar day, so a gap or the night starts a new
span and a new `Saw` line. Diaries keep the newest `MemoryStore.MaxDiaryEntries` (500); the trim runs
only when `Observe` appends a `Saw` line. The player has no diary: the player is never an observer.

### RoutineBelief (`RoutineBelief.cs`)

One belief per (observer, subject), keyed `"observer>subject"` in `MemoryStore.Beliefs`: pseudo-counts
per region x block. Each co-located tick adds `strength` to the current (region, block) cell and 1 to
`CoPresenceTicks`.

- Strength is 1.0 for NPC subjects. For the player it is `MemoryStore.PlayerLearningStrength(hearts)`
  = `1 + 0.25 * clamp(hearts, 0, 14)` (1.0 to 4.5) with the observer's hearts, so closer NPCs learn
  the player's routine faster. The observer only learns from time spent together.
- `Unlocked` once `CoPresenceTicks >= UnlockThreshold` (240 ticks, 40 game hours together; the
  shadow tests pin two full days of co-presence). `UnlockThreshold` **is** saved with each belief,
  unlike the ledger thresholds (known issue).
- `BestGuessAt(block)` (top region for one block, its share and the evidence) is used by Find.
  `SeedPrior` (from `NpcSchedules.RoutinePrior`), `Decay` and `BestGuess` exist and are tested but
  nothing in the mod calls them; `Unlocked` is read only by the shadow harness.

### MemoryStore (`MemoryStore.cs`)

`Observe(now, presences, regions, heartsFor)` is the whole per-tick memory update:

1. Group presences by location; the region is `regions.RegionFor(location)`, or `"Other"` if unmapped.
2. For every NPC observer (never the player) and every other character in the same location with
   `|dx| <= 8 and |dy| <= 8` (`CoLocationRadius`, a placeholder to tune):
   `Ledger.Record(observer, subject, location, region, now, "x,y")` with the **subject's** tile;
   the pair's `RoutineBelief.Observe(region, block, now, strength)` and `NoteCoPresence(1)`; and a
   `Saw` diary line if a span starts.
3. Remember this tick's co-located pairs for span detection (in memory only, not saved).

Everything is processed in name order, so diaries and JSON are deterministic. `DiaryOf(npc)` creates
a diary on first use. `AskAround` and `LookFor` belong to Find. `ToJson()` gives
`{"Version":2,"Ledger":"<json>","Diaries":{"Abigail":"<json>"},"Beliefs":{"Abigail>Player":"<json>"}}`
(the inner values are JSON strings). `FromVersion1` migrates old saves (see Persistence).

## Decisions (`src/NpcDecision`)

`IDecisionClient` asks three typed questions. Answers are numbers, never text, and choice options are
always the actions the game actually supports, so the client can never propose an invalid one.
`IBatchDecisionClient.Ask(state, questions)` sends several typed questions about one state in one
round trip where the backend supports it (Laya does; the fakes loop). State strings are built with
`DecisionState` (priority-ordered sections cut by whole lines to `StateBudgetChars` 1250, so the
least important facts drop first) and start with the NPC card (`NpcCard.Render`: temperament, voice,
hearts, date/weather/time — built on the game thread into the snapshot, never from live positions).
The planner's speak/pick pair is one batched request per NPC. `VariedFakeDecisionClient`
(`DecisionBackend = "Varied"`) derives every answer from FNV-1a over (state, question), so shadow
logs show varied stable choices without a model; its yes/no is uniform over 0..1, so the 0.25
speak veto floor lets about 3 in 4 newsy NPCs through (with the Fake client's constant 0.5, all
of them); the plain `FakeDecisionClient` stays the test
baseline. `ResilientDecisionClient` wraps every client with a timeout, a session budget token and a
health gate (`isDown`: while the server is down every call falls back immediately, no HTTP attempt);
the budget token also reaches the inner Laya client so a cancelled budget aborts the in-flight
request, and it is reset when the session ends. The mod re-checks health every 6 ticks off the game
thread, warms the model with one throwaway question after a healthy check, and reports calls,
fallbacks and median/p95 latency in the heartbeat. The morning wait (`MorningWaitMenu`, config
`MorningWaitMs`) holds the day behind a small menu at `DayStarted` when the overnight plan is still
running.

| Method | Returns | Fake, and every fallback | Laya question |
|---|---|---|---|
| `Choose(options, context)` | a probability per option, same order | uniform (1/n) | `choice`: labels `o0..oN`, option text as the description, "Which option fits best?"; reads `probabilities` |
| `Score(context, min, max)` | a value in [min, max] | midpoint | `score`: five levels "very low".."very high"; the expected level (0..4) maps linearly onto [min, max] |
| `YesNo(context, proposition)` | P(true) in [0, 1] | 0.5 | `noul` with the proposition as instructions; reads `noul` |

Callers: the overnight planner (`YesNo`, `Choose`) and the ladder (`YesNo`). Nothing calls `Score`.

**`ResilientDecisionClient`** returns the fallback above when a call is slow or fails. With a
timeout, the call runs on a thread-pool task and the caller waits at most that long, so it blocks the
calling thread: use it only off the game thread. An optional **budget token** caps a batch: once it is
cancelled, every remaining call falls back at once without touching the inner client. Any exception
also falls back; `Fallbacks` counts them, and `LastFallbackReason` says why the newest one happened
(the budget, the health gate, the timeout, the inner error as "Type: message" cut to 200
characters, or a question a batch left out). `RecordingDecisionClient` copies the reason into the
viewer's call log (`DecisionCall.Error`) and the playtest `model` record (`error`).

**`LayaDecisionClient`** speaks the `laya-serve` v0.3.22 protocol (verified from the Laya repo; not
yet run against a live server):

- `POST {BaseUrl}/v1/systemone` with `{"state": context, "model": ..., "questions": {"q": {...}}}`,
  answer under `answers.q`. `IsHealthy()` is `GET {BaseUrl}/health` returning `"status": "ok"`.
- Synchronous `HttpClient.Send`, cancelled by its own timeout (`LayaOptions.Timeout`) or an optional
  budget token. `Authorization: Bearer <ApiKey>` only when a key is set.
- State: null becomes "", then it is cut to `MaxStateChars` (4,000), keeping the start and never
  splitting a surrogate pair. The checkpoints read only 512 to 1,024 tokens anyway.
- `Choose` with no options sends nothing; more than 100 options throws. Missing labels read as 0;
  probabilities are clamped, not renormalized.
- Failures throw `LayaException` (non-2xx with `StatusCode` and the server's `detail`, timeout,
  transport, malformed JSON, missing fields); the budget token throws `OperationCanceledException`.

**Backend selection** (`ModEntry.BuildModel`): `DecisionBackend` = `"Laya"` (any case) builds a
`LayaDecisionClient` from `LayaUrl`, `LayaModel`, `LayaApiKey` and `DecisionTimeoutMs`, and runs a
health check on a background task (logs `Laya is up at ...` or `Laya is not answering at ...`).
Anything else gives `FakeDecisionClient`. Every use goes through `ModEntry.Guarded(budget)`, a new
`ResilientDecisionClient` with the `DecisionTimeoutMs` timeout. The mod does not pass the budget token
into `LayaDecisionClient`, so when the planning budget runs out an in-flight HTTP call is abandoned
rather than cancelled; it still ends at its own timeout.

**Threading rule:** model calls never run on the game thread. They happen only in the `IntentPlanJob`
task, the `BackgroundLadder` worker and the start-up health check. The game thread only enqueues,
drains, polls and serializes.

## Overnight intents (`src/NpcIntents`)

At sleep the mod decides which NPCs would say something tomorrow, and what. The model picks who and
which diary entry; the words come from templates.

`IntentPlanner.Plan(snapshots, seed, sourceDay)`, for each
`NpcMemorySnapshot(Npc, Voice, RecentDiary, RecentLines)`:

1. Drop entries whose `Kind` is in `SkipKinds` (default `TriedToReach`: the attempt is not news, being
   ignored is) and, when `sourceDay` is given, entries from any other calendar day. If nothing is
   left, the NPC is skipped without asking the model.
2. Context: the NPC card (or `npc: <name>` + `voice: <voice>` without one), then a `news:` section
   of the top options as plain sentences (`NewsPhrasing`), news score descending. This matches the
   eval set's phrasing (sidecar/eval/run_eval.py).
3. `YesNo(context, "does <npc> have news for the player?")`: below `SpeakThreshold` (0.25: a veto
   floor — the measured answer band sits at 0.2-0.5 even for real news, so a 0.5 gate vetoed a
   quest at 0.49), or NaN, and the NPC is skipped.
4. Options. With news (the mod always attaches a `NewsContext`): every entry scores through
   `Newsworthiness`; entries under `MinNews` (2.0) are dropped, and an NPC with none left is skipped
   without a model call. A player `Saw` is dropped outright when the day's diary also holds a
   `Talked` entry (the NPC already talked to the player; "I saw you at the saloon yesterday" from
   the saloon conversation reads oddly — playtest review). The top `MaxRecentDiaryEntries` (5) by
   news score (ties: newest first) are the options, as plain sentences. Without news (the legacy
   path, tests and tools): the newest
   `MaxRecentDiaryEntries` (5) entries, deduplicated by summary (the newest copy kept). A summary
   is `Saw Player at Pierre's General Store` for `Saw` (the detail is a place, through
   `PlaceNames`) and `IgnoredBy Player (Emote)` for anything else.
5. `Choose(options, context)` — asked only when there are two or more options (a one-option pick
   is not a question; playtest review), then **sample** one option from the blended pick weights: each
   model probability times the option's news score (a zero probability is a veto; a missing,
   all-zero or non-finite answer leaves the pure news weights, which in the legacy no-news path
   means a uniform pick). Never argmax. One `Random(seed)` serves the whole plan,
   consumed in snapshot order.
6. Render the cited entry with `daysAgo = sourceDay + 1 - DayIndex(entry)` (1 in the mod).
7. Novelty: skip a line equal (ignoring case) to one in `RecentLines`. The mod passes none yet.
8. Sort by the best news score among the NPC's options, then the yes/no probability, then by
   name; keep `MaxNpcsPerDay` (3). Each
   `IntentCandidate` has `Line`, the cited `Source` entry, its `News` (the best news among the
   options, not the sampled one) and a `Reason` such as
   `cited "yesterday Abigail saw Pierre at Pierre's General Store" (sampled p=0.333)`.

With the Fake backend every NPC with news scores 0.5, which passes the threshold, so the plan
becomes the three NPCs with the best news from the day just ended (ties: name ascending) — the
narrow yes/no band no longer decides the speakers (week review, finding 4;
`docs/decisions.md`, D15).

**`LineRenderer`**, the default `ILineRenderer`, is first person and deterministic:

| Entry | Line |
|---|---|
| `Saw` | `I saw {who} at {place} {when}.`, or `I saw {who} {when}.` without a place |
| `IgnoredBy`, subject Player | `I tried to get your attention {when}. You must have been busy.` |
| `Talked`, subject Player | `It was nice talking with you {when}.` |
| `PassedBy`, subject Player | `You walked right past me {when}.` |
| `BirthdayForgotten`, subject Player | `My birthday was {when}, you know.` |
| `GiftReceived`, subject Player | by taste: Love or Like `Thanks again for the {name} {when}.`; Neutral `Thanks for the {name} {when}.`; Dislike `I'm not sure what to do with the {name} you gave me {when}.`; Hate `About the {name} you gave me {when}. Please don't do that again.` (`the gift` without a `name`) |
| `SawGift` | `I saw {who} get a {name} from {giver} {when}, and {reaction}.`: the witness saw the reaction too ("they hated it", "it made their day"; none for a neutral gift) |
| `Heard`, subject Player | a gift: `{from} told me you gave them a {name} {when}.` (`... They weren't happy.` for a disliked or hated one); a quest: `{from} told me you helped them out {when}.`; a retold story names the person it started with instead of "them" (`Sam told me you gave Haley a Sunflower yesterday.`); else `I heard about {who} from {from} {when}.` |
| `QuestHelped`, subject Player | `Thanks for helping me out {when}.` |
| `Festival`, subject Player | `It was nice catching up with you at the festival {when}.` with `with=1`, else `I saw you at the festival {when}.` |
| `MissedFestival`, subject Player | `You missed the festival {when}.` |
| anything else | `I've been thinking about {who}.` |

`{who}` is "you" for the player, otherwise the subject's name. `{when}` (`LineRenderer.When`): 0 or
less "earlier today", 1 "yesterday", under 7 "the other day", else "a while back". Speaker and voice
do not change the templates yet. Every line ends with `LineSanitizer.Sanitize`, which deletes `#`,
`$`, `%`, `{` and `[` (not `}` or `]`).

**`PlaceNames.Display`** maps internal location names to player-facing ones from a table recalled
from the 1.6 maps (verify the wording), e.g. `SeedShop` -> "Pierre's General Store", `Town` ->
"Pelican Town". Region names `Island` and `Other` are in the table too. Unknown names are split at
capitals and underscores (`IslandWest` -> "Island West"). `LineRenderer` accepts another name
function; the mod uses the default. **`VoiceSheets`** has one-line voices for 18 NPCs (fallback
"friendly and plain-spoken"); only the model's context uses them.

**`Newsworthiness`** scores how much an entry is worth telling (`docs/spec/diary.md`, "Newsworthiness"):
housemates seen at home score zero and are never offered; `Saw` of the player scores 2 (+1 at 4+
hearts, −1 per recent citation of the same kind and subject); a subject seen somewhere unusual for
them (their routine belief for the 2-hour block has ≥ 12 total evidence and the region's share is
under 0.15) scores 2; fixed kinds use `NewsworthinessOptions.KindWeights` (`IgnoredBy` 3, `PassedBy`
2, `BirthdayForgotten` 4, ...); `GiftReceived` reads the Detail's `taste` and `birthday` keys. The
planner's `NewsContext` carries the observer's homes, beliefs and hearts **as snapshots** — the plan
job reads beliefs off the game thread, so the mod passes `RoutineBelief.Snapshot()` copies (AGENTS.md:
two threads, two owners).

**Day-end notes** (`MemoryStore.DayEndNotes`, `docs/spec/diary.md`, "Day-end notes") run on the game
thread at `DayEnding` and write `Talked` (on the first conversation of the day, from a dialogue box
whose speaker is that NPC), `PassedBy` (6+ co-located ticks with 2+ hearts, talked to someone else,
never to this NPC) and `BirthdayForgotten` (its birthday, 3+ hearts, no gift today via
`Friendship.GiftsToday`) into the diaries, so planning at `DayEnding` already sees them.

**Diary producers, part 2** (`src/NpcDiaryEvents`, `mod/StardewNpcMod/Patches/`): the Harmony kinds
arrive through read-only postfixes (docs/spec/diary.md, "Harmony") — `GiftPatch` on
`NPC.receiveGift` (queues `GiftReceived`; the drain adds `SawGift` for every NPC the span tracker
saw co-located with the player, minus the recipient) and `QuestPatch` on `Quest.questComplete`
(resolves the quest's target NPC by type; `SocializeQuest` has none and is skipped; an identity set
records each quest once). A postfix only queues into `DiaryEventQueue`; the next tick drains it
through the pure producers (`GiftNotes`, `SawGiftNotes`, `QuestNotes`, `FestivalNotes` in
`src/NpcDiaryEvents` — plain inputs in, `DiaryEntry` out) into `MemoryStore`, so there is one
writer and one order. Special orders never touch `Quest.questComplete`: the tick diffs
`team.completedSpecialOrders` and writes `QuestHelped` for the order's requester (resolved from the
order data by key, falling back to the live order — it can already be gone once everyone claims).
Festivals are captured on `OneSecondUpdateTicked` while `Game1.isFestival()` (attended + actor
names; time does not pass during one, so `TimeChanged` never fires) and written at `DayEnding` —
`Festival` for every NPC that took part (`with=1` when the player talked to it there) or
`MissedFestival` for 4+ heart NPCs when the player never attended; the Detail carries the stable
date key (`spring13`), never the localized display name. The queue also drains at the start of
`DayEnding`, so a gift or quest after the last tick still makes that night's plan. If a
patched method is missing after a game update, the mod logs a warning and skips that patch (the
polling fallbacks in the spec are deferred until one is actually needed).

**`IntentPlanJob`** runs the planner on a background task. `Start(work, budget)` creates a token that
is cancelled after `budget`. The work builds its client with `Guarded(token)`, so after the budget
every remaining call falls back and the plan still finishes promptly. The game thread only polls
`TryTake(out plan, out error)`, which never blocks and hands the plan over once (an empty plan plus
the error if the work threw). `BudgetExhausted` is true only if the budget fired **before** the work
finished, so a plan collected late is not reported as cut short. `Dispose()` cancels the budget.

In the mod, planning starts at `DayEnding` with budget `PlanningBudgetMs` (20 s), seed = DayIndex of
the day just ended, and snapshots of every non-empty diary (all entries, no `RecentLines`).
`CollectPlan` runs on every tick and at `DayStarted`. When the plan is ready it logs each line, adds
each speaker to `_intentsToday` (the ladder's `HasPendingIntent`) and disposes the job. Nothing is
delivered in-game, and the plan is not saved.

## Initiation ladder (`src/NpcInitiation`)

**Retired (2026-10-03, D31).** The mod no longer runs the ladder: the motives decide every attempt
and the live acts come only from them. What stays in use from this project: `InitiationInput` (each
tick's views and leads, built by `ModEntry.BuildViews` and read by `RunMotives` and the viewer),
`PlayerSearch` (asking around, now triggered by `MotiveDrive.Seeking`) and `Heartbeat` (now
"strongest motive" and the motives worker's backlog). `InitiationLadder` and `BackgroundLadder` stay
as a tested library until they are deleted; an old save's `ladder` value is left unread and no longer
written. The rest of this section describes the ladder as it was.

Each NPC carries an **urge** (0..1) to get the player's attention. When it is high enough the ladder
picks the mildest step that fits and records what it **would** do; it never touches the game. Its
knowledge of the player comes only from memory: the NPC's own `LedgerView` of "Player", plus a Find
lead.

| Rung | `InitiationStep` | Min urge | Available when | Unanswered after | Own cap (all NPCs) |
|---|---|---|---|---|---|
| 0 | `Emote` | 0.30 | Near | 6 ticks (1 h), cut at the day's end | - |
| 1 | `Bubble` | 0.45 | Near | 6 ticks | - |
| 2 | `Approach` | 0.60 | Near, or a Find lead with a place | 6 ticks | - |
| 3 | `QueuedLine` | 0.70 | SeenToday | the end of the day, then **Expired** | 2 a day |
| 4 | `Mail` | 0.80 | not SeenToday, and hearts >= 2 | the end of the **next** day | 1 a day |
| 5 | `ForcedDialogue` | 0.95 | Near | 6 ticks | 1 a week |

**Near:** the NPC itself saw the player at a named spot this very tick (`HopCount` 0, `AgeTicks` 0,
`NamedSpot`). **SeenToday:** the NPC itself saw the player earlier today (`HopCount` 0, any detail but
`Gone`). Hearsay counts for neither.

`Tick(now, inputs, diaryFor)` takes one
`InitiationInput(Npc, PlayerView, HasPendingIntent, Hearts, Lead)` per NPC, in name order:

1. **Settle** an open attempt whose window has passed. A `QueuedLine` becomes `Expired` and nothing
   changes. Anything else becomes `Ignored`: urge -0.2 (not below 0), rung = the ignored step + 1
   (escalation), and an `IgnoredBy` diary line — the line only while the rung is live
   (`RecordIgnoredBy`, off in shadow). The event is stamped when the window closed, or on the
   day's last tick if it closed with the day. This runs before the rollover, so an attempt left open
   overnight belongs to its own day.
2. **New day:** urge x 0.5, rung 0, the NPC's attempt count 0.
3. No `PlayerView` at all (the NPC has never seen or heard of the player): stop, no growth.
4. **Grow:** urge += 0.004 + 0.0005 x hearts, plus 0.25 once a day on the first tick with
   `HasPendingIntent`; clamped to [0, 1].
5. **Maybe attempt**, only if no attempt is open, the NPC made fewer than 2 today, all NPCs made fewer
   than 6 today, and 6+ ticks have passed since this NPC's last attempt and last conversation. The
   candidate is the mildest step at or above the rung whose threshold the urge meets, that is
   available and whose own cap is free. It asks
   `p = YesNo(context, "should <npc> try to get the player's attention with <step> now?")` (context:
   urge, hearts, step, the view's detail, age and hop count, and the lead for an Approach from a
   distance). It attempts when a deterministic uniform from FNV-1a(seed, npc, tick) is below `p`,
   then opens the step, counts it, and writes a `TriedToReach` diary line. When the draw says no,
   the same step with the same lead is not asked again for `AskAgainAfterTicks` (6): Jodi was asked
   "Approach now?" every tick while the player was on the farm (playtest 2026-10-02).

**Conversations:** `NoteResponded(npc, tick)` means the player talked to the NPC: urge x 0.5, rung 0,
and the 6-tick cooldown starts. An open attempt becomes `Responded` (no diary line). An NPC the ladder
has never ticked is ignored.

Pace at default tuning: from zero, urge reaches the Emote threshold after about 75 ticks (12.5 game
hours) of the NPC knowing the player at 0 hearts, about 34 at 10 hearts; the overnight halving slows
this and an intent boost speeds it up. Short sessions often log no ladder lines. With the Fake backend
`p` is 0.5, so about half of the eligible ticks become attempts.

All the tuning above is `InitiationOptions` (`src/NpcInitiation/Models.cs`), which is never saved.
`ToJson`/`FromJson` save per-NPC state (urge, rung, day, attempts today, last attempt and contact
ticks, open step and tick, intent-boost day) and the global day and week counters; `OpenStep` is saved
as an **int**. The seed is `Fnv1a.Seed("ladder", Game1.uniqueIDForThisGame)` (a per-save id: verify).

**`BackgroundLadder`** runs the ladder on one worker (a chain of task continuations), so its `YesNo`
calls never block the game thread. `EnqueueTick` copies the inputs; when `LadderMaxBacklog` (6)
operations are already queued or running it drops the tick and counts it (`Dropped`).
`EnqueueResponse` is always accepted (it never calls the model). Diary lines go to scratch diaries on
the worker and come back in each `Result`; the mod applies them on the game thread. `Drain()` never
blocks. `LatestJson` (what the save uses) and `LatestUrges` (read by Find) are snapshots from the last
finished operation. `WaitIdle` is for tests only.

**Response detection** (`ModEntry.OnMenuChanged`): when the world is ready and the new menu is a
`DialogueBox` whose `characterDialogue.speaker` is set, the mod calls
`EnqueueResponse(speaker.Name, now)`. Talking, and the NPC's reaction to a gift, should count;
question boxes and letters have no speaker. Verify in-game, including whether event dialogue counts.

## Motives (`src/NpcMotives`)

Roadmap step 14 (D24, `docs/spec/motives.md`): a character acts only when it has a **motive** with a
subject and its **effective boldness** (boldness + familiarity + intensity) reaches the act's
**cost**. Built and tested as a library (no game types); the mod runs it every tick, and since the
urge ladder was retired (D31) it alone decides attempts. It never reads a live position, never changes the game, and is deterministic:
NPCs in name order, no clock, seeded FNV-1a for the mood roll and the grudge draw.

| Piece | What it does |
|---|---|
| `StressorTable.Of(entry)` | what a diary kind means: motive, valence, magnitude, per-day decay, plastic share, juiciness, emotion; null for kinds that stir nothing (`Saw`, `TriedToReach`, a neutral gift) |
| `Stresses.Elastic` | the fading part, from the last 3 days of diary: magnitude x (0.5 + sensitivity) x decay^days; `Heard` at half the original; `SawGift` stirs jealousy only toward a giver the observer is drawn to (the player at 8+ hearts). `Vent` halves the hurt from entries before each hostile act |
| `RegardBook` | the lasting part, one signed number per (observer, subject), -1..1: retention (Pam 0.2, everyone else 0.5) unless the stress is severe (0.7+ after sensitivity), the yield point (the third ignore, walk-past or brush-off of one subject in 5 days marks 0.3), the 6:00 drift (grudges heal by 0.03 x (0.5 + forgiveness), warmth fades by 0.005), confirmed hearsay at half strength |
| `RegardHistory` | history at install (D29): `SeedOf`/`Seed` turn what the game remembers (gift counts by taste, heart events seen, relationship status, as an `NpcHistory`) into regard toward the player, added onto what is there; `SeenInDiary` and `NpcHistory.Except` leave out what the mod already noted. Pure; the mod reads the game once per save |
| `RegardKeeper` | owns the book on the game thread. `OnNoted` is the body of the `MemoryStore.Noting` hook, so each diary write leaves its mark exactly once; hearsay told by the person it happened to is confirmed at once, hearsay from a witness stays elastic. Also `Drift` and `Relieve` |
| `MoodRoll` | earned mood (the stresses' signed sum) plus 0.3 x (0.5 + sensitivity) x a seeded triangular roll skewed by the emotion biases; 1 day in 40 runs against the character's lean |
| `MotivesEngine` | the motives toward the player, the netted feeling (+ 0.15 x outlook), the candidates (the feeling, labelled by its strongest motive, plus each task), and the act rule over the allowed and available acts whose minimum motive strength the intensity reaches (`ActMinStrength`: a letter needs 0.30, an interrupt 0.60), from the most expensive down: a clear yes at margin >= 0.15, a close call within 0.15; `ResolveClose` tilts the model's answer by 0.10 x outlook and cuts at 0.5, no random draw |
| `MotivesRunner` | the engine over time (rules below) |
| `BackgroundMotives` | runs the runner on one worker, like `BackgroundLadder`: `EnqueueTick`, `EnqueueTalked`, `Drain`; `LatestJson`, `LatestDecisions` and `LatestStates` are safe to read from any thread; a full backlog drops ticks and counts them |
| `MotiveInputBuilder` | one NPC's `MotiveInputs` on the game thread, from its own ledger view (near, seen today, days since a sighting), lead, diary (copied) and regard |
| `MotiveText` | the model's states (the NPC card, the feelings, the act with its parts, cut to the state budget) and options, and the `[shadow]` lines |

**The runner's rules** (shadow semantics; the numbers are in `MotiveOptions`, not saved):
- Every tick each NPC is weighed without the model (`Decide`); that is what the viewer shows. No
  motive, no act.
- An NPC holds at most one **in-person** attempt (emote, bubble, walk-up, interrupt, visit), which
  blocks new attempts until the player answers or its 6-tick window passes, and one **waiting**
  attempt (letter, queued line, request), which blocks only another of its sort. A queued line
  expires at the end of the day; a letter is ignored at the end of the next day.
- Pacing: 2 attempts per NPC a day, 12 in town, 2 queued lines and 1 letter a day, 1 interrupt and
  2 visits a week, 6 ticks after an attempt or a talk. A capped act is unavailable, so the rule
  picks a cheaper one. With 2 or fewer attempts left in town, only motives of intensity 0.5+ may
  act.
- Light acts (an emote, or a bubble that only greets: `MotiveOptions.IsLight`) use none of those
  attempts and the reserve doesn't hold them back; each NPC has 2 a day of its own. Once its or the
  town's attempts are used up, an NPC may still wave (`MotiveInputs.AttentionCapped`); it is
  `Blocked` only when its light acts are used up too.
- **Meetings between ticks** (D32): about once a second the mod checks the player's location
  (`ModEntry.MeetPlayer`). A villager there within the co-location radius that hasn't seen the player
  this tick records the sighting at once (`MemoryStore.NoteMeetings`: a ledger sighting and the
  `Saw` diary entry, marked so the next tick's `Observe` continues the span instead of writing a
  second one), and the motives decide again for those villagers alone (`RunMotives(now, only)`). A
  player running past no longer slips between two ten-minute ticks. Pacing holds: the cooldown
  stops a second act in the same tick. Skipped while the player is busy.
- While the player is busy (a talk, a menu, a scene or a warp's fade: `MotiveInputs.PlayerBusy`,
  set by the mod from `!Context.IsPlayerFree`), no in-person act is decided; the NPC tries again the
  next tick. In the first live test three villagers acted the moment the player walked into the
  Saloon, the gate held all three back, and the runner had already counted them (two later read as
  ignored). This is game state, not a position (AGENTS.md rule 2 is about positions).
- Light acts (a wave, a glare, a greeting bubble) wait for no answer at all, so they are never
  ignored: no open attempt, no `Ignored`, no frustration, no `IgnoredBy` in live (Sid, 2026-10-03:
  "They happen often and it would just make the game unfun to have to react to every single one").
  The `Act` line says "a wave needs no answer". Walk-ups, interrupts and bubbles with a real reason
  still wait.
- After the player's talk with an NPC that day, its in-person acts wait for no answer: the game
  opens no second conversation that day, so the player couldn't answer, and the act would always
  end up ignored (Sid's live test, 2026-10-03). No open attempt, no `Ignored`, no frustration; the
  `Act` line says "no answer expected". Letters and queued lines wait as before.
- An ignored attempt frustrates for the rest of the day: +0.1 intensity per ignore for the bold
  (boldness 0.5+), -0.1 for the shy. A talk with the player answers open attempts and clears it.
- Used up: any face-to-face act is the day's greeting; news and thanks are delivered by an in-person
  act or a letter at once, by a queued line only when the player comes to talk; an emote delivers
  nothing; acting on hurt vents it.
- The model is asked only to pick among motives (when 2+ have strength 0.2+ and could act now, at
  most 5; fallback the strongest; a single actionable one goes without a question) and on a close
  call. A question answered "no" is not asked again until the situation
  changes or 6 ticks pass.
- The grudge: once a day, an NPC whose regard for the player is at or below -0.75 is asked "would
  <npc> hold this against the player?", and a seeded draw against the answer decides. A yes is a
  `Grudge` event, at most one per NPC per 7 days, which only logs in shadow; the game thread then
  raises that NPC's regard by 0.3 (the mod's memory, never the game's friendship).

**Threads.** Inputs are built and regard is applied on the game thread; the runner and its model
calls run on the worker; events are drained on the game thread, which writes the logs. Regard reaches
the worker only as the copied `RegardForPlayer`.

**Wiring it into the mod** (step 14 part 3; done):
1. `ModEntry` keeps a `RegardKeeper` (temperaments from `_temperaments`, `Temperament.Neutral` when
   missing) and a `BackgroundMotives` whose runner asks through `Guarded("motives")`, and sets
   `_memory.Noting` to a handler that calls `RegardKeeper.OnNoted` and appends
   `MotiveRecords.Stress` (and `MotiveRecords.Regard` when regard moved) to the playtest log. The
   hook is not saved: set it again wherever `_memory` is replaced (load, migration, title screen).
2. `RunLadder` writes its diary lines with `_memory.Note` instead of `DiaryOf(npc).Append`, so they
   pass the hook.
3. Each tick, after `RunLadder`: build one `MotiveInputs` per NPC with `MotiveInputBuilder.Build`
   (the same ledger view and lead the ladder got; the plan's best news score for that NPC today; the
   NPC card with a "today" line; `friendshipData.ContainsKey(npc)` as "met", **verify** that the
   game adds the entry at the first meeting), `EnqueueTick`, then drain: every event gets
   `MotiveText.Line` in the SMAPI log (Info for Act, Grudge and Responded; Trace for the rest) and a
   `MotiveRecords.Decision` record; Act and Grudge also go to the viewer's feed; a Grudge calls
   `RegardKeeper.Relieve` and logs the `regard` record.
4. `OnMenuChanged`: `EnqueueTalked` (the ladder's `EnqueueResponse` went with the ladder, D31).
5. The 6:00 tick: `RegardKeeper.Drift()`, then the `regard` snapshot (`MotiveRecords.Snapshot`).
6. Save `regard` (`RegardBook.ToJson`) and `motives` (`BackgroundMotives.LatestJson`); load them
   with `RegardBook.FromJson` and `MotivesRunner.FromJson` (missing or damaged values load empty).
7. `PublishMinds` passes `Motives`, `MotiveStates`, `RegardFor` and the newest line per NPC to
   `MindsInputs`; the cards then show the runner's numbers instead of the page's preview.
8. History at install (wired): at `SaveLoaded`, after `NewRegard`, if the save data has no
   `historySeeded`, build an `NpcHistory` per villager from the game, subtract
   `RegardHistory.SeenInDiary(npc, diary)`, call `RegardHistory.Seed(_regard.Book, ...)`, log each
   `HistorySeed.Line` as `[shadow]` and append `MotiveRecords.History`; then save
   `historySeeded = "1"`.

## Live emotes and bubbles (`src/NpcLive`)

The first behavior to leave shadow (rollout.md; Sid, 2026-10-02: emotes and bubbles first,
friendly and hostile; D30). Only the motives runner's acts go live, never the urge ladder's. Each
has its own switch in `config.json`, off by default.

- **What is shown.** `LivePlanner.From(event, switches, playerName)` takes a runner event of kind
  `Act` whose act is `Emote` or `Bubble` and whose switch is on. An emote's id follows the motive:
  happy 32 for a greeting, heart 20 for gratitude, exclamation 16 for missing the player, news or
  worry, question 8 for curiosity; a hostile one is angry 12 from the bold (boldness 0.5+) and sad
  28 from the shy. A bubble's line is a template per motive, friendly or hostile, picked with
  `Fnv1a(npc, motive, tick)`, from the villager's own voice (`BubbleVoices`: 34 villagers, a
greeting, missing you, news, thanks, worry, hurt, jealous) or the plain lines when it has none for
the feeling, with the farmer's name filled in and the whole line passed through
  `LineSanitizer` (the model writes nothing).
- **The last check.** `LiveGate.WhyNot(act, facts, now)` is the one place outside `CollectPresences`
  and `Observe` that may read live state (AGENTS.md rule 2, D30), and only to say "not now": not in
  multiplayer, not more than `MaxDelayTicks` (1) after the decision, not during an event or a
  festival, not while the player is busy, only in the player's location within `EmoteMaxTiles` (10)
  or `BubbleMaxTiles` (8), only when the villager is visible and isn't already emoting or speaking,
  and not within `MinTicksBetweenActs` (3) of the last act that villager showed (`LiveLedger.LastShownTick`;
  a safety net under the runner's 6-tick cooldown, in case anything ever shows one villager's acts
  back to back).
  The `[live]` line for an act shown names the tick it was decided in. An act held back is logged as `[live] ... not shown (reason)`; the runner still counts it as made
  (it decided on the worker and doesn't know).
- **The breaker.** `LiveBreaker.Run(act, show)` turns one switch off for the session if showing
  throws, and reports the error once; `TripAll` is the console command `npcmod_live off`.
- **Ignored for real.** `LiveLedger` keeps the shown acts not yet answered. When the runner reports
  one `Ignored`, `OnResolved` returns the `IgnoredBy` diary entry to write with `MemoryStore.Note`
  (in shadow nothing is written, since the player never saw the attempt); `Responded` clears it.

### Wiring it into the mod (done, 2026-10-02, `deepseek/live-emotes`)

1. `ModConfig` gains `public LiveSwitches Live { get; set; } = new();` (both off). Read once at
   `Entry`; keep a `LiveBreaker` and a `LiveLedger`; register `npcmod_live off` with
   `helper.ConsoleCommands.Add` to call `TripAll`.
2. Drain the motives events more often than once per ten-minute tick, so the act shows while the
   player is still there: `BackgroundMotives.Drain()` from `UpdateTicked` (game thread), about once
   a second. VERIFY that it is safe to drain outside `TimeChanged` (it is a queue; the shadow lines and
   records stay the same).
3. For each drained event, `LivePlanner.From(ev, _config.Live, Game1.player.Name)`. If not null,
   gather `LiveFacts` on the game thread: `!Context.IsMultiplayer`, `Context.IsPlayerFree`,
   `Game1.eventUp`, `Game1.isFestival()`, the NPC's `currentLocation == Game1.player.currentLocation`,
   the Chebyshev distance of `npc.TilePoint` and `Game1.player.TilePoint` (`Character.Tile` is a
   float `Vector2`; the tile is the int `TilePoint`), busy = `npc.isEmoting` (public, NPC.cs:145;
   `textAboveHeadTimer` is protected int, NPC.cs:160, so an already-showing bubble cannot be read),
   visible = `!npc.IsInvisible`. All verified in the 1.6.15 decompile.
4. `LiveGate.WhyNot(...)`: null means show it, through `_live.Run(act.Act, ...)`:
   `npc.doEmote(act.EmoteId)` or `npc.showTextAboveHead(act.Text, duration: options.BubbleMs)`
   (both confirmed in the decompile, NPC.cs:1373). Log `[live] ` + `LivePlanner.ShownLine(act)` at
   Info and `_ledger.Shown(act)`. A reason means log `[live] ` + `LivePlanner.SkippedLine(act, reason)`
   at Trace. A breaker error logs once at Error.
5. Every drained event also goes to `_ledger.OnResolved(ev)`; an entry returned is written with
   `_memory.Note(npc, entry)`.
6. In `RunMotives`, each input's `PlayerBusy` is `!Context.IsPlayerFree` (done 2026-10-03; not yet
   built against the game), so nothing in person is decided while the player is talking, in a menu
   or a scene.
7. The shadow line for the act stays as it is, so a day with the switches off and one with them on
   give the same `[shadow]` lines apart from the added `[live]` ones.

## Notice-board experiment (`src/NpcBoard`)

Roadmap step 21's first stage (`docs/spec/notice-board.md`): how would each villager react to a
note the player wrote? `NoteReactions.React(decision, readers, note)` cleans the note (the line
sanitizer's characters stripped, double quotes made single so the note can't close its own quote,
space folded, cut to about two sentences, `MaxNoteChars` 200), and asks each reader one typed
`choice` over six reactions (amused, touched, curious, annoyed, offended, indifferent). The state
is the reader's NPC card, the note as quoted data, and how the reader feels about the author.
Flat answers (a fallback, or the plain fake) read as indifference. Each reaction maps to an emote
(ids confirmed in the decompile) and a templated line; `Report` prints the town's split and the
spread across villagers. It calls the model, so it runs off the game thread. Not in the mod yet;
`sidecar/eval/run_notes.py` sends the identical question to a local Laya for every card in
`sidecar/eval/cards.json`.

## Finding the player

**Code.**
- `src/NpcMemory/MemoryStore.cs`: `AskAround` and `LookFor`.
- `src/NpcMemory/Whereabouts.cs`: `Whereabouts`, `WhereaboutsSource`, `AskResult` and
  `WhereaboutsOptions`.
- `src/NpcMemory/RoutineBelief.cs`: `BestGuessAt`.
- `src/NpcInitiation/PlayerSearch.cs`: who asks, and when.
- `src/NpcInitiation/InitiationLadder.cs`: `Available` and `HasLead`.
- The mod: `ModEntry.AskAround` and `ModEntry.RunLadder`.

**Tests.** `tests/NpcMemory.Tests/WhereaboutsTests.cs` and `tests/NpcInitiation.Tests/FindTests.cs`.
**Why.** See decisions.md D18.

Everything here runs on the game thread each tick, after `MemoryStore.Observe`, and reads memory
only (no live positions, no model calls):

1. **Who asks** (`PlayerSearch.Tick`). An NPC asks around when two things are true:
   - its urge from the ladder is at least `AskUrge` (0.45). This comes from
     `BackgroundLadder.LatestUrges`, so it is at most one tick stale;
   - its own view of the player is not a first-hand sighting younger than `FreshTicks` (6 ticks,
     one hour).

   It asks at most once per `AskCooldownTicks` (6). If nobody is with it, it asks nobody, starts no
   cooldown, and tries again next tick. The cooldowns live only in memory.
2. **Asking** (`MemoryStore.AskAround(seeker, "Player", now)`).
   - The seeker's neighbours are the subjects in its own ledger that it sees this very tick
     (first-hand, age 0, NamedSpot). The player and the subject being looked for are never asked.
   - Each neighbour is asked in name order through `Ledger.Gossip(neighbour, seeker, ...)`, so the
     gossip rules hold: capped at the teller's detail, at most two hops, only kept if fresher, and
     `ToldBy` recorded.
   - It returns `AskResult(Asked, Told)`.
3. **Where to look** (`MemoryStore.LookFor(seeker, "Player", now, blockMinutes)`). It returns a
   `Whereabouts` built from the first source that applies:

   | Source | When | Place |
   |---|---|---|
   | `SeenNow` | own view, age 0, NamedSpot | location |
   | `SeenToday` | own view from today, fresh | location, or region once it has coarsened |
   | `Told` | a tip from today (1-2 hops) | location or region; `ToldBy` says who |
   | `Habit` | no usable sighting, and `RoutineBelief.BestGuessAt(this block)` has Evidence >= 12 and Share >= 0.5 | region |
   | `Unknown` | none of the above | none |

   A sighting or tip is usable only while fresh (`MaxLeadAgeTicks` 12 = two game hours) and only
   when its region is not the seeker's own region as of its last observed tick (the same
   span-tracker memory as the habit guard; playtest review). A rejected lead falls through to the
   habit.

   A sighting that has faded to "earlier today" (no place) falls back to the habit. The region
   `Other` is never treated as a place. Habits come from time spent together, weighted by hearts
   for the player, so a habit is "where I usually see you at this hour".
4. **Going to look** (the ladder). `InitiationInput.Lead` carries the `Whereabouts`. `Approach` is
   available when the NPC is near the player, or when it has a lead with a place (`SeenToday`,
   `Told` or `Habit`). Approach needs urge 0.60 and a letter needs 0.80, so a keen NPC goes looking
   before it writes. An Attempt made from a distance carries its lead (`InitiationEvent.Lead`).

**What it looks like in the SMAPI log:**
```
[shadow] Abigail asked Sam about you: Sam saw you at Pelican Town 40 minutes ago.
[shadow] Abigail would go looking for you at Pelican Town (Sam saw you there 40 minutes ago; urge 0.62)
[shadow] Willy would go looking for you at the beach (you're usually there at this hour, 80% of the time; urge 0.64)
```

## NPC Minds viewer (`src/NpcMinds`)

A read-only debug page Sid watches beside the game (`docs/spec/debug-tools.md`, "Live viewer";
`docs/decisions.md`, D23). While a save is loaded, `http://127.0.0.1:8765/` shows one card per
NPC that has a diary, a feed of what happened, and a feed of model calls. It updates every two
seconds and highlights whatever changed since the last update.

**How a card reads** (redesigned 2026-10-02, after Sid: "It's hard to see the important values ...
Make it easier to know where to look"). Top to bottom, the most important first:
1. **A status pill**, in words and a color, worked out by the page from the motives view:
   - green "would wave" (it acts);
   - red when hostile ("would glare", "confronted you · waiting on you");
   - amber "close call: ...?";
   - blue "waved · waiting on you" (an attempt is open);
   - grey "holding back", with the runner's reason in plain words ("can't: today's letter used up",
     "you're not around", "saving today's last tries");
   - grey "calm" for no motive.

   The card's left edge takes the same color. Calm cards are shaded and drop to the bottom of the
   default sort, "Most active first" (state, then the strongest feeling).
2. **The feeling** it would act on: the motive, its strength as a big number and a bar (green
   friendly, red hostile), and the next strongest motives.
3. **"What it would dare"**, the act ladder, which replaces the unlabelled daring bar. Every act is
   shown cheapest first, by name ("wave", "letter", "walk over"; hostile names when the feeling is
   hostile), marked from the runner's checks:
   - ✓ yes;
   - ? close call;
   - struck through: out of reach;
   - faded: not weighed now;
   - outlined: its pick.

   The tooltip gives the daring and cost.
4. **Four tiles:** regard, mood, tries today, saw you.
5. **Today's planned line**, if it has one.
6. **A row of tabs** for the rest: Why (the motives and sources, the mood sum, the daring sum, the
   newest shadow line), About you, Old ladder, Temperament, Diary.

The header gives the clock, the model's health (red when questions fail), and clickable counts of
villagers per state ("3 waiting on you", "1 hostile") that filter the grid; the rest of the stats
go on a small second line. "How to read a card" is a legend above the grid; the page remembers
whether it was closed.

| Per NPC | From |
|---|---|
| portrait (top left of the card) | the villager's neutral portrait (frame 0 of `NPC.Portrait`, 64x64; **verify** the frame layout and that `Portrait` is the current appearance's sheet), cut from the player's own installed game content at `SaveLoaded` on the game thread (`ModEntry.PublishPortraits`), encoded as PNG in memory and handed to the server, which serves `/portrait/<Name>.png` (names are letters, digits and `_` only). Never written to disk or the repo: the art is the game's. A villager without one shows its initials |
| temperament line, status pill, feeling and the act ladder (top of the card) | the seed temperament's summary, and the motives act rule ([motives.md](spec/motives.md), D24). When the snapshot carries the motives runner's latest weighing (`NpcMind.Motives`, from `BackgroundMotives.LatestDecisions` and `LatestStates`), the bar is the runner's boldness + familiarity, striped out to + intensity, with ticks at the costs it weighed (hostile ones include the surcharge), and the card lists the motive it would act on, the act and whether it is a clear yes or a close call, every motive with its source, today's outlook (earned and roll, and an off day), the net feeling, regard, attempts today, what is open or waiting, and its newest `[shadow]` line. Without the runner (a mod build before the wiring), the page falls back to a preview: boldness + 0.03 per heart, striped out to + 0.25 for a strong feeling, against the spec's first-guess act costs. Nothing reads either back |
| urge, rung and its threshold, attempts today, open attempt and how long it has waited (under "Current ladder", below the card's facts) | `InitiationLadder.ReadStates(BackgroundLadder.LatestJson)`, the worker's last finished state. Still what drives attempts until motives replace it |
| hearts, "last saw you", "would look" | the inputs `RunLadder` built this tick (`InitiationInput`: the NPC's own `LedgerView` and `Whereabouts`), so the page shows exactly what the ladder saw |
| today's line, "has a line today" | the collected plan (`_planToday`, `_intentsToday`) |
| "tonight" | today's diary entries scored by `Newsworthiness` the way the planner scores them (skip kinds out, one per summary, `MinNews`), top 3; a preview only, the real plan also asks the model and samples |
| diary | the newest 8 entries, in plain words, with the game time |
| temperament (collapsed) | the seed table from PR #11 (`temperament.json` with overrides applied, loaded at `Entry`): six traits and six emotions as bars around 0.5, a summary of the strongest leanings ("holds grudges, quick to anger"; at most three, 0.1+ from the middle), and the game's own Data/Characters words. Built once per NPC per save (`MindsSnapshotBuilder.TemperamentOf`). Display only: no decision reads it yet |
| model spread (side panel) | the per-day answer table: every question the day asked, keyed by (template with the NPC name replaced by `<npc>`, NPC) -> count and mean, written by `RecordingDecisionClient` on the model workers under a lock, copied for the server, and reset at `DayEnding` just before the overnight plan starts (so the plan's answers count toward the day they plan for; nothing clears them at the 6:00 tick). `MindsSnapshotBuilder.SpreadRows` turns the copy into one row per template: a dot per NPC (at its mean answer, hover for the name), the spread (90th-10th percentile), median, the Spearman correlation with the trait the question should follow (boldness for the attention and close-call questions, chattiness for speak, forgiveness inverted for hold-against; from `LayaCalibration.TraitForTemplate`, which normalizes the mod's real question wordings), the FLAT / NO-FOLLOW marks (`FlatSpread` 0.05, `MinNpcsForSpread` 6, `FollowsTraitMin` 0.3; null when too few NPCs were asked), and the committed calibration row (`data/laya-calibration.json`, card variant A) for comparison. Opening a row lists the NPCs in answer order with their temperament summaries. Recording never changes an answer; the corrections stay off ([laya.md](spec/laya.md), "Character spread") |

**How it fits together.**

```
game thread (end of each TimeChanged, SaveLoaded, DayStarted)
  PublishMinds() -> MindsSnapshotBuilder.Build(_memory, inputs) -> immutable MindsSnapshot
                 -> MindsServer.Publish(snapshot)              one reference swap
ladder worker / plan job
  RecordingDecisionClient -> RingLog<DecisionCall>             every question and answer
viewer thread (MindsServer, TcpListener on 127.0.0.1)
  GET /           the embedded page
  GET /state.json { snapshot, calls } serialized here, on request
```

- **Read-only.** The builder only reads `MemoryStore` and the inputs (a test compares
  `MemoryStore.ToJson()` before and after). `RecordingDecisionClient` wraps each
  `ResilientDecisionClient` that `Guarded(caller, budget)` builds and returns its answers
  unchanged; "fell back" is the wrapped client's `Fallbacks` counter before and after the call
  (one wrapper per caller, one thread per caller). With `MindsViewer` off, no wrapper and no
  server exist.
- **Threads.** The snapshot is built on the game thread (it reads memory), is immutable, and is
  handed over by one volatile reference. Serialization, sockets and the call log never touch
  `Game1`. The feed (`RingLog<FeedItem>`) is filled on the game thread where the `[shadow]`
  lines are logged: ladder events, "asked around" and planned lines.
- **Cost on the game thread.** One pass over the diaries (newest 8 each, today's entries for the
  news preview), the ladder JSON parsed once, the beliefs grouped by observer once. No model
  calls, no I/O.
- **Server.** A raw `TcpListener` bound to `IPAddress.Loopback`, so Windows needs no URL
  reservation or admin rights. GET only (`/`, `/state.json`, `/health`), `Connection: close`,
  2-second socket timeouts, a request whose `Host` is not `127.0.0.1`/`localhost`/`[::1]` gets 403
  (a DNS-rebinding page can't read it), no CORS header. A port already in use logs a warning and
  the mod runs without the viewer. `Mod.Dispose` stops it.
- **The page** is plain HTML, CSS and JavaScript with no external loads, in
  `src/NpcMinds/viewer/index.html`, embedded in `NpcMinds.dll` (logical name
  `NpcMinds.viewer.html`), so the usual `*.dll` deploy copy carries it. Always dark (Sid's choice),
  on the neutral Radix Colors sand scale, with color only on status marks (urge bars blue to amber to red, status dots on badges, deltas, answer bars, feed kinds, and the motives line: hostile, close call, clear yes), never on borders or behind text. Cards sort by most recently changed (the default), feeling (the strength of the motive each would act on), boldness, hearts, urge or name; the choice is
  remembered in the browser. A filter box and a "knows you" toggle hide NPCs with no view of the
  player, no hearts, no urge and no motive. The header says whether the motives runner is running
  (in shadow, beside the urge ladder) or not yet.
- **Not saved, reset** on load and at the title screen: the feed (300 items) and the call log (200).

## Playtest log (`src/NpcMinds/Playtest`)

Every number in the tuning tables is a first guess, so the mod records each decision's parts
while Sid plays (`docs/spec/debug-tools.md`, "Playtest log"). One JSON-lines file per save and
in-game day under `Mods/StardewNpcMod/playtest/<save>/<year>-<season>-<day>.jsonl`, one object
per line with `tick`, `type` and fields. The game thread appends to an in-memory buffer that
flushes at the 6:00 tick and on Saving; model-call records arrive from the ladder worker through
a thread-safe queue drained on the game thread (nothing touches a file off the game thread). A
failed write is logged once at Warn and disables the log — it never throws into the game loop.
The setting is `PlaytestLog` (`ModConfig`, default true in development). Reads only: records
never feed a decision, and live positions appear only in `presence` records, built solely from
`CollectPresences` output as per-tick deltas. `tools/playtest_summary.py` turns a save's folder
into per-day tables (attempts by step, gossip routes, model calls and fallbacks, diary growth
against the 500 cap, slowest ticks; and for motives: outcomes, acts by motive, close calls with the
median model answer and mood tilt, the caps that blocked, would-be friendship losses, stresses by
kind, regard changes and the strongest grudges). Adding a record type is one class in
`src/NpcMinds/Playtest/PlaytestRecords.cs` plus one `Append`/`QueueFromWorker` call. The motives
records are in `MotiveRecords.cs`: `decision` (every part of an act, pass, block, outcome or grudge),
`stress` (what one diary entry stirred and the mark it left) and `regard` (each change, and a
snapshot of every pair at 6:00); the mod writes them once the runner is wired.
The ladder's `Blocked` events (a step the urge cleared but a gate or cap passed over; at most one
per NPC per step per day) go to the playtest log only, at Trace in the SMAPI log: they are tuning
data, and as events they would crowd real attempts out of the log and the viewer's feed.

## Shadow harness and schedule extractor

Neither runs inside the mod. **`src/NpcShadow`** is the step-3 test bed: `ShadowSimulator` follows one
observer and some subject NPCs through simulated days built from their real schedules (`DayPlanner`
resolves each NPC's location, region and tile per tick through `NpcSchedules.ScheduleSimulator`). It
drives a `Ledger`, a `Diary` and `RoutineBelief`s directly (not `MemoryStore`) and writes a
deterministic `ShadowLog` of `Start`, `DayStart`, `Saw`, `Decayed` and `Unlocked` events, rendered as
`DD HHMM  Actor: message`. Co-location there is the same **location** only, because schedule tiles
are destinations, not positions. Its day counter runs on across the year end. It has no gossip,
ladder or intents. Only `tests/NpcShadow.Tests` uses it; it is the easiest place to test multi-day
behaviour (decay to Gone, unlock).

**`src/NpcSchedules` + `tools/ScheduleExtractor`** (step 1) simulate a full year per NPC from raw
schedule scripts, mirroring the game's key order and script commands (verified against decompiled
1.6; details and what is not modelled are in `README.md` and `stardew-source-notes.md`), and count
ticks by region x 2-hour block. `RoutinePrior.Build` turns that into a noisy, seeded prior for another
NPC (family strong, friends weak). Inputs: `fixtures/game/*.json` (32 real 1.6 schedules),
`fixtures/wiki/Abigail.json`, `fixtures/synthetic/Testy.json`.

```
dotnet run --project tools/ScheduleExtractor -- --schedules fixtures/game --out out
```

This writes `out/routines.json` and `out/routines.csv`, and with `--seed` also
`out/prior-<observer>-<seed>.json` (`out/` is gitignored). Relation to the live mod: the mod uses only
`TimeUtils`, `RegionMap` and `Fnv1a`. Seeding family and friends' beliefs with `SeedPrior` from real
schedules (loaded in-game with `npc.getMasterScheduleRawData()` or `GameContent.Load`: verify) is not
wired yet.

**`src/NpcTemperament` + `tools/TemperamentExtractor`** compute a seed temperament per villager
(warmth, sensitivity, forgiveness, chattiness, curiosity, boldness) and six Ekman emotion biases
(anger, disgust, fear, happiness, sadness, surprise), each 0..1 with 0.5 typical, from
the game's dialogue files, gift reaction lines and `Data/Characters` traits: count signals per page
(portrait moods, `?`, `!`, `...`, thanks/sorry/welcome/dismissive/gossip words, words per page),
z-score them across the cast, and add them to offsets from Manner, SocialAnxiety, Optimism and Age.
Fear, disgust and surprise have no portrait code, so they rest on words alone and move half as far.
Hand edits go in an overrides file applied last. The draft table is in `fixtures/game/temperament/`;
the mod does not read it yet. Method, inputs and how to regenerate: `docs/spec/temperament.md`.

```
dotnet run --project tools/TemperamentExtractor -- --dialogue <unpacked dialogue>   --characters fixtures/game/temperament/characters.json --out fixtures/game/temperament
```

## Persistence

One SMAPI save-data entry per save, key `squid.StardewNpcMod.memory`, a `Dictionary<string, string>`:

```
"version": "2"                          MemoryStore.CurrentVersion
"memory":  MemoryStore.ToJson()         ledger, diaries, beliefs (see Memory)
"ladder":  BackgroundLadder.LatestJson  InitiationLadder.ToJson()
"intents", "recentLines"                today's plan and the lines said (step 5)
"regard":  RegardBook.ToJson()          {"observer|subject": value} (wired, step 14 part 3)
"motives": BackgroundMotives.LatestJson the runner's pacing state (wired, step 14 part 3)
```

It is written at `Saving` (`SaveMemory`) and read at `SaveLoaded` (`LoadMemory`). Saving never waits
on the model: the ladder part is the last finished state.

| Saved | Not saved |
|---|---|
| Ledger entries, with hop count, `toldBy` and detail cap | the overnight plan and `_intentsToday` (after a reload, no NPC gets that day's intent boost) |
| Diaries (at most 500 entries each) | span tracking: the first tick after a load starts new spans and new `Saw` lines |
| Routine beliefs, including `UnlockThreshold` | tuning: ledger thresholds, `InitiationOptions`, `IntentPlannerOptions`, radius, diary cap |
| Ladder: per-NPC state and global counters | Find's ask cooldowns |
| | ladder work still queued at save time, and diary lines from ladder results not yet drained (both reach the next save unless the game closes first) |

**Loading and migration** (`ModEntry.LoadMemory`):

- No save data: fresh memory and ladder.
- A `"version"` key: read as the current format. The number is **not** checked, so an incompatible
  future format needs its own branch there (and a `CurrentVersion` bump and a migration test).
- No `"version"` key: a version-1 save from steps 4-5. `MemoryStore.FromVersion1(model, now)` reads
  `ledger` (then the player's, so its observer is "Player"), `npcDiaries`, and `beliefs` (the
  player's, re-keyed `"Player>npc"`), and drops `diary` (the player's own). Old ticks had no year:
  each goes in the current year if it is not later than now, else the previous year
  (`MigrateYearlessTick`). The ladder starts fresh. The migrated player-side entries are kept but
  nothing reads them now that memory is NPC-side.

## Build, test, run

.NET SDK 6.0.300 is installed (no `global.json`). ModBuildConfig finds the game through `GamePath` in
`%USERPROFILE%\stardewvalley.targets`, on the owner's PC
`D:\SteamLibrary\steamapps\common\Stardew Valley`; the mod compiles against the game and SMAPI there.

**Test:** `dotnet test NpcSchedules.sln` runs eight xUnit projects (731 tests after the viewer's temperament section; the table below is from `c97a829`). It does **not** build the mod: no
test project references it. At `c97a829` all 386 pass:

| Project | Tests | | Project | Tests |
|---|---|---|---|---|
| `tests/NpcSchedules.Tests` | 68 | | `tests/NpcDecision.Tests` | 50 |
| `tests/NpcMemory.Tests` | 129 | | `tests/NpcIntents.Tests` | 60 |
| `tests/NpcShadow.Tests` | 31 | | `tests/NpcInitiation.Tests` | 48 |

**Build the mod:** `dotnet build mod/StardewNpcMod`. Two `CS8032` warnings are expected: SMAPI's
analyzers (`NetFieldAnalyzer`, `ObsoleteFieldAnalyzer`) need Microsoft.CodeAnalysis 4.9, newer than
this SDK's compiler, so they don't load. Harmless.

**Deploy** is a manual copy: the csproj sets `EnableModDeploy=false` and `EnableModZip=false`, so a
build never touches the game. `mod/StardewNpcMod/bin/Debug/net6.0/` holds six DLLs, five library PDBs
(the mod's own PDB is embedded; ModBuildConfig sets `DebugType=embedded`), `regions.json` (copied from
`data/regions.json`), `temperament.json` and `temperament-overrides.json` (copied from
`fixtures/game/temperament/`) and `StardewNpcMod.deps.json` (not needed). `manifest.json` is **not** in bin;
take it from `mod/StardewNpcMod/`. The viewer page is inside `NpcMinds.dll`, so nothing extra is copied for it. With the game closed, from the repo root (bash version: `AGENTS.md`):

```powershell
$out = "mod\StardewNpcMod\bin\Debug\net6.0"
$mod = "D:\SteamLibrary\steamapps\common\Stardew Valley\Mods\StardewNpcMod"   # <GamePath>\Mods\StardewNpcMod
Copy-Item "$out\*.dll", "$out\*.pdb", "$out\regions.json", "$out\temperament*.json" $mod -Force
Copy-Item "mod\StardewNpcMod\manifest.json" $mod -Force
```

Don't overwrite the `config.json` there: copy the data files by name, never `*.json` (the build
output has no config today, but a wildcard would take one if it ever appeared). A `shadow-log.txt` in that folder is left over from an old
build; the mod writes no files of its own. Launch through SMAPI (`<GamePath>\StardewModdingAPI.exe`).

**`config.json`** (`mod/StardewNpcMod/ModConfig.cs`; SMAPI creates it on first run; read once in
`Entry`, so restart the game after editing):

| Key | Default | Meaning |
|---|---|---|
| `DecisionBackend` | `"Fake"` | `"Laya"` (any case) uses the Laya server; anything else, the fake |
| `Live` | `{ "Emote": false, "Bubble": false }` | the live switches (D30, "Live emotes and bubbles"): `true` shows that act in the game, friendly and hostile; planned, read once at `Entry` (not wired yet) |
| `LayaUrl` | `"http://127.0.0.1:8000"` | Laya base URL; keep it on loopback |
| `LayaModel` | `"typed-decisions"` | checkpoint: `typed-decisions`, `english` or `multilingual` |
| `LayaApiKey` | null | only if the server was started with `LAYA_API_KEY` |
| `DecisionTimeoutMs` | 1500 | per-call timeout; slower answers fall back |
| `PlanningBudgetMs` | 20000 | total time overnight planning may spend on the model |
| `LadderMaxBacklog` | 6 | ladder operations that may wait before new ticks are dropped |
| `MorningWaitMs` | 10000 | how long the morning wait may hold the day for the overnight plan; 0 disables |
| `MindsViewer` | `true` | serve the NPC Minds viewer on loopback (read-only) |
| `MindsViewerPort` | `8765` | its port; not 8000, which Laya uses |

**Logs:** `%APPDATA%\StardewValley\ErrorLogs\SMAPI-latest.txt`; the mod's lines are tagged
`Stardew NPC Mod`. `[shadow]` lines say what would have happened:

| Line | Level | Meaning |
|---|---|---|
| `Shadow mode ready: co-location radius 8 tiles, decision backend FakeDecisionClient.` | Info | started; names the backend |
| `Memory saved (23 NPC diaries, 74 beliefs).` (also `Memory loaded: ...`) | Info | written at `Saving`, read at `SaveLoaded` |
| `NPC Minds viewer: open http://127.0.0.1:8765/ in a browser (read-only).` | Info | the viewer is serving (`NPC Minds viewer is off: port ... is not available` at Warn if the port is taken) |
| `Migrated memory from the previous save format (...)` | Info | a version-1 save was converted |
| `[shadow] planning tomorrow's intents in the background (19 NPC diaries).` | Info | `DayEnding`: the plan job started |
| `[shadow] Abigail would say: "I saw Pierre at Pierre's General Store yesterday." (cited "Saw Pierre at Pierre's General Store" (sampled p=1))` | Info | a planned line, the diary entry it cites and the chosen option's probability (1 = the only option) |
| `[shadow] overnight planning is still running; its lines will be logged when ready.` | Info | `DayStarted` came before the plan finished |
| `[shadow] planning hit its time budget; some decisions used the fallback.` | Info | the budget fired before the plan finished |
| `[shadow] collected overnight plan: 3 line(s) for today.` | Info | the plan was collected; each line is logged after it |
| `[shadow] collected overnight plan: no lines (no candidates: nothing newsworthy to cite, or the model answered below the speak threshold for everyone).` | Info | the plan was collected empty; the wording varies with the cause (`the plan failed; see the error above` when it threw) |
| `[shadow] Abigail would try Emote (urge 0.31; urge 0.31 >= 0.30 at rung 0; p=0.50)` | Info | a ladder attempt: urge, the step's threshold, the rung, the model's `p` |
| `[shadow] Abigail: Emote ignored (urge 0.35 -> 0.15)` | Info | an attempt's outcome, `ignored`, `responded` or `expired`, with urge before and after |
| `[shadow] <npc> would go looking for you at <place> (...)` | Info | an Approach from a distance (Find) |
| `[shadow] <npc> asked <npcs> about you: ...` | Info | an NPC asked around and learned something (Find) |
| `[shadow] ladder is behind the model; skipped a tick (3 so far).` | Trace | the backlog was full, so a tick was dropped |
| `[shadow] 1600: 29 NPC diaries, max urge 0.31 (Abigail), ladder backlog 1/dropped 0, overnight plan running` | Info | heartbeat every 2 game hours: memory size, the ladder's best urge (and who), its backlog and drop count, and the plan job's state (`none` / `running` / `ready`) |

The first five lines are copied from a real log (an earlier build, same formats); the rest follow the
format strings in `ModEntry` (numbers illustrative). Failures are logged at Error level and name the
stage: `Observation failed`, `Shadow ladder failed`, `Overnight planning failed`, `Failed to save memory`.

## Extension points

`AGENTS.md` ("Where to add things") has the short version. Also note its rule that positional
records (`LedgerView`, `InitiationInput`, `InitiationEvent`, `Whereabouts`) only grow at the end, with
defaults.

**A new diary kind**
1. Write it on the game thread with `_memory.Note(npc, new DiaryEntry(now, subject, "MyKind",
   detail))`: the one writer, which trims to `MaxDiaryEntries` and calls the `Noting` hook (regard).
   Diaries are not thread-safe; a worker hands lines back the way
   `BackgroundLadder.Result.DiaryLines` does.
2. If it stirs a feeling, give it a row in `StressorTable.Of` (`src/NpcMotives`): motive, valence,
   magnitude, decay, plastic share, juiciness, emotion (the table in `docs/spec/motives.md`).
3. The planner will offer it as news unless it is in `IntentPlannerOptions.SkipKinds`. Its option text
   is `MyKind Subject (detail)` (`IntentPlanner.Summarize`; only `Saw` treats the detail as a place).
4. Give it a template in `LineRenderer.Render`, or it renders as "I've been thinking about {who}." Keep
   "you" for the player and `LineSanitizer.Sanitize` last.
5. Tests: `tests/NpcIntents.Tests/LineRendererTests.cs`, `PlannedLineDateAndPlaceTests.cs`, and for a
   stressor row `tests/NpcMotives.Tests`.

**A new decision question**
- Ask through `IDecisionClient` from a background thread only, via the mod's `Guarded(budget)`. Copy
  `IntentPlanJob` for a batch with a time budget, or `BackgroundLadder` for per-tick work with a backlog.
- Options must be things the game can do (at most 100 for Laya); keep the context short (cut at
  4,000 characters). Feed it memory (ledger views, diaries, beliefs), never live positions.
- The fallback (uniform, midpoint, 0.5) is what you get whenever Laya is slow, down or over budget, and
  what `Fake` always returns, so make it a safe default.
- A new question **type** means changing `IDecisionClient`, all three implementations and
  `tests/NpcDecision.Tests`.

**A new ladder step**
- `InitiationStep` values are the rung numbers and the index into `StepThresholds`, and they are saved
  as ints (`OpenStep`). **Append; never renumber or reorder** existing values, or open attempts in old
  saves change meaning. A step that belongs between two existing ones needs a save migration.
- Update `StepThresholds` (a missing entry makes the step unreachable), `Available` (the default is
  never), `ResolveAt` (the default is the 6-tick window), the loop bound in `Candidate`
  (`rung <= (int)InitiationStep.ForcedDialogue`), any new cap (counter, `RollGlobal`, `LadderDto`), the
  log line in `ModEntry.RunLadder` if needed, and `tests/NpcInitiation.Tests`.

**A new location display name**
- Add it to the `Names` table in `src/NpcIntents/PlaceNames.cs` (case-insensitive keys) and a case to
  `PlannedLineDateAndPlaceTests`.
- For a new map, add it to a region in `data/regions.json` too (unmapped locations count as `Other`);
  `RegionMapTests` pins known entries. The deployed `regions.json` is read at start-up, so it can be
  edited without rebuilding.
- `IntentPlanner.Summarize` calls `PlaceNames.Display` directly, so a name function passed to
  `LineRenderer` does not change the option text the model sees.

**A new config option**
- Add a property with a default to `mod/StardewNpcMod/ModConfig.cs` and read `_config.<Name>` in
  `ModEntry`. It is read once, in `Entry`.
- Whether SMAPI adds a new key to an existing `config.json`: verify. If not, the default applies until
  the player adds it.
- Add it to the table above (and to `sidecar/README.md` for Laya settings). Knobs not in config yet:
  `MemoryStore.CoLocationRadius` and `MaxDiaryEntries`, the `Ledger` thresholds, `InitiationOptions`,
  `IntentPlannerOptions`, `LayaOptions.MaxStateChars`.

## Open "verify" items

Several were settled on 2026-09-30 against a local decompile of 1.6.15 and SMAPI 4.5.2
(`stardew-source-notes.md`, "Checked in the 1.6.15 decompile"). The `VERIFY` comments in the code
for those can go in the next code PR.

| Item | Where | Status |
|---|---|---|
| `TimeChanged` fires once per ten-minute tick | `ModEntry.OnTimeChanged` | **settled:** SMAPI raises it once per change of `Game1.timeOfDay`. Normally that is every ten minutes; there are no ticks during festivals (the clock stops), and the end of a festival jumps straight to 22:00 in one event |
| `Game1.locations` covers the static maps; farm building interiors are seen only with the player inside | `ModEntry.CollectPresences` | **settled:** true. `Utility.ForEachLocation` would include interiors if that's ever wanted |
| Off-screen NPCs' positions update in real time | brief, open questions | **settled:** yes, on the host, every tick |
| `Game1.uniqueIDForThisGame` is a per-save id | `ModEntry.NewLadder` | **settled:** a `ulong` per save |
| A conversation (and a gift reaction) opens a `DialogueBox` with that speaker; event dialogue may also count | `ModEntry.OnMenuChanged` | **settled** for talking and gifts: the gift reaction is `Game1.DrawDialogue(GetGiftReaction(...))`, a `Dialogue` whose speaker is the NPC. Event dialogue: still to check in-game |
| The 8-tile radius is a placeholder to tune | `MemoryStore.CoLocationRadius` | open (tuning) |
| `PlaceNames` wording matches the game | `src/NpcIntents/PlaceNames.cs` | open |
| `LayaDecisionClient` has not run against a live server | brief, step 7 | open |
| Rain weights and the extractor's other recalls | `README.md`, "Remaining recalls to verify" | open (`Data/Locations` is data, not code) |
