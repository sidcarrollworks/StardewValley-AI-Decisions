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
| `src/NpcShadow` | `DayPlanner`, `ShadowSimulator`, `ShadowLog` | NpcSchedules, NpcMemory | no (tests only) |
| `tools/ScheduleExtractor` | command line: schedule JSON in, region x block counts out | NpcSchedules | no |
| `mod/StardewNpcMod` | `ModEntry` (every game hook), `ModConfig`, `manifest.json` | the five "yes" projects; game + SMAPI via `Pathoschild.Stardew.ModBuildConfig` 4.3.1 | - |
| `tests/<Name>.Tests` | xUnit tests for `src/<Name>` | that project only | no |

```
mod/StardewNpcMod --+--> NpcIntents -----+--> NpcMemory --> NpcSchedules
                    +--> NpcInitiation --+
                    |                    +--> NpcDecision
                    +--> NpcMemory, NpcDecision, NpcSchedules (also directly)

src/NpcShadow ----------> NpcMemory, NpcSchedules   (tests only)
tools/ScheduleExtractor -> NpcSchedules             (command line)
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
                                   fill _intentsToday
```

Festival capture is NOT in the tick: the clock is stopped for the whole festival
(`Game1.shouldTimePass` is false while `isFestival()`) and the one `TimeChanged` after it (the
22:00 jump) already sees `isFestival()` false, so `CaptureFestival()` runs on
`OneSecondUpdateTicked` instead.

`CollectPresences` builds a `Presence(name, location, tileX, tileY, isPlayer)` for every NPC with
`IsVillager`, from `Game1.locations` plus `Game1.player.currentLocation` (farm buildings are not in
`Game1.locations`, so their occupants are seen only while the player is inside: verify). Hearts come
from `Game1.player.getFriendshipHeartLevelForNPC` (compiles against 1.6; semantics verify). The mod
assumes `TimeChanged` fires once per ten-minute tick (verify).

| Other hook | What the mod does |
|---|---|
| `Entry` | reads `config.json`; loads `regions.json` from the mod folder (a missing or invalid file throws before any event is hooked, so the mod does nothing); builds the decision backend; `ApplyPatches()` (the read-only Harmony postfixes, one list); logs `Shadow mode ready: ...` |
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

In the mod, gossip happens only when an NPC asks around (`MemoryStore.AskAround`; Find).
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
logs show varied stable choices without a model; the plain `FakeDecisionClient` stays the test
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
also falls back; `Fallbacks` counts them.

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
2. Context: `voice: <voice>; recent diary: <summaries of the newest 5 distinct entries>`.
3. `YesNo(context, "does this NPC have something to say today?")`: below `SpeakThreshold` (0.5), or
   NaN, and the NPC is skipped.
4. Options. With news (the mod always attaches a `NewsContext`): every entry scores through
   `Newsworthiness`; entries under `MinNews` are dropped, and an NPC with none left is skipped
   without a model call. The top `MaxRecentDiaryEntries` (5) by news score (ties: newest first) are
   the options. Without news (the legacy path, tests and tools): the newest
   `MaxRecentDiaryEntries` (5) entries, deduplicated by summary (the newest copy kept). A summary
   is `Saw Player at Pierre's General Store` for `Saw` (the detail is a place, through
   `PlaceNames`) and `IgnoredBy Player (Emote)` for anything else.
5. `Choose(options, context)`, then **sample** one option by its probability, never argmax. Missing,
   all-zero or non-finite probabilities give a uniform pick. One `Random(seed)` serves the whole plan,
   consumed in snapshot order.
6. Render the cited entry with `daysAgo = sourceDay + 1 - DayIndex(entry)` (1 in the mod).
7. Novelty: skip a line equal (ignoring case) to one in `RecentLines`. The mod passes none yet.
8. Sort by the yes/no probability, highest first, then the best news score among the NPC's options,
   then by name; keep `MaxNpcsPerDay` (3). Each
   `IntentCandidate` has `Line`, the cited `Source` entry, its `News` (the best news among the
   options, not the sampled one) and a `Reason` such as
   `cited "Saw Pierre at Pierre's General Store" (sampled p=0.333)`.

With the Fake backend every NPC with news scores 0.5, which passes the threshold, and equal
probabilities sort by news, then by name. So the plan becomes the three NPCs with the best news
from the day just ended (ties: name ascending) — no longer simply the alphabetically first three
(`docs/decisions.md`, D15).

**`LineRenderer`**, the default `ILineRenderer`, is first person and deterministic:

| Entry | Line |
|---|---|
| `Saw` | `I saw {who} at {place} {when}.`, or `I saw {who} {when}.` without a place |
| `IgnoredBy`, subject Player | `I tried to get your attention {when}. You must have been busy.` |
| `Talked`, subject Player | `It was nice talking with you {when}.` |
| `PassedBy`, subject Player | `You walked right past me {when}.` |
| `BirthdayForgotten`, subject Player | `My birthday was {when}, you know.` |
| `GiftReceived`, subject Player | `Thanks again for the {name} {when}.` (or `the gift` without a `name`) |
| `SawGift` | `I saw {who} get a {name} {when}.` |
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
   then opens the step, counts it, and writes a `TriedToReach` diary line.

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
   | `SeenToday` | own view from today, older | location, or region once it has coarsened |
   | `Told` | a tip from today (1-2 hops) | location or region; `ToldBy` says who |
   | `Habit` | no usable sighting, and `RoutineBelief.BestGuessAt(this block)` has Evidence >= 3 and Share >= 0.5 | region |
   | `Unknown` | none of the above | none |

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

## Persistence

One SMAPI save-data entry per save, key `squid.StardewNpcMod.memory`, a `Dictionary<string, string>`:

```
"version": "2"                          MemoryStore.CurrentVersion
"memory":  MemoryStore.ToJson()         ledger, diaries, beliefs (see Memory)
"ladder":  BackgroundLadder.LatestJson  InitiationLadder.ToJson()
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

**Test:** `dotnet test NpcSchedules.sln` runs six xUnit projects. It does **not** build the mod: no
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
`data/regions.json`) and `StardewNpcMod.deps.json` (not needed). `manifest.json` is **not** in bin;
take it from `mod/StardewNpcMod/`. With the game closed, from the repo root (bash version: `AGENTS.md`):

```powershell
$out = "mod\StardewNpcMod\bin\Debug\net6.0"
$mod = "D:\SteamLibrary\steamapps\common\Stardew Valley\Mods\StardewNpcMod"   # <GamePath>\Mods\StardewNpcMod
Copy-Item "$out\*.dll", "$out\*.pdb", "$out\regions.json" $mod -Force
Copy-Item "mod\StardewNpcMod\manifest.json" $mod -Force
```

Don't overwrite the `config.json` there. A `shadow-log.txt` in that folder is left over from an old
build; the mod writes no files of its own. Launch through SMAPI (`<GamePath>\StardewModdingAPI.exe`).

**`config.json`** (`mod/StardewNpcMod/ModConfig.cs`; SMAPI creates it on first run; read once in
`Entry`, so restart the game after editing):

| Key | Default | Meaning |
|---|---|---|
| `DecisionBackend` | `"Fake"` | `"Laya"` (any case) uses the Laya server; anything else, the fake |
| `LayaUrl` | `"http://127.0.0.1:8000"` | Laya base URL; keep it on loopback |
| `LayaModel` | `"typed-decisions"` | checkpoint: `typed-decisions`, `english` or `multilingual` |
| `LayaApiKey` | null | only if the server was started with `LAYA_API_KEY` |
| `DecisionTimeoutMs` | 1500 | per-call timeout; slower answers fall back |
| `PlanningBudgetMs` | 20000 | total time overnight planning may spend on the model |
| `LadderMaxBacklog` | 6 | ladder operations that may wait before new ticks are dropped |

**Logs:** `%APPDATA%\StardewValley\ErrorLogs\SMAPI-latest.txt`; the mod's lines are tagged
`Stardew NPC Mod`. `[shadow]` lines say what would have happened:

| Line | Level | Meaning |
|---|---|---|
| `Shadow mode ready: co-location radius 8 tiles, decision backend FakeDecisionClient.` | Info | started; names the backend |
| `Memory saved (23 NPC diaries, 74 beliefs).` (also `Memory loaded: ...`) | Info | written at `Saving`, read at `SaveLoaded` |
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
1. Append it on the game thread:
   `_memory.DiaryOf(npc).Append(new DiaryEntry(now, subject, "MyKind", detail))`. Diaries are not
   thread-safe; a worker hands lines back the way `BackgroundLadder.Result.DiaryLines` does. Only
   `Observe` trims, so call `TrimTo(_memory.MaxDiaryEntries)` if you append a lot.
2. The planner will offer it as news unless it is in `IntentPlannerOptions.SkipKinds`. Its option text
   is `MyKind Subject (detail)` (`IntentPlanner.Summarize`; only `Saw` treats the detail as a place).
3. Give it a template in `LineRenderer.Render`, or it renders as "I've been thinking about {who}." Keep
   "you" for the player and `LineSanitizer.Sanitize` last.
4. Tests: `tests/NpcIntents.Tests/LineRendererTests.cs`, `PlannedLineDateAndPlaceTests.cs`.

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
