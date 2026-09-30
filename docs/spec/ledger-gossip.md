# 4. Last-seen ledger and gossip

**Status: partial.** The ledger, decay and on-demand gossip (when an NPC asks around) are done.
Ambient gossip (news spreading while NPCs spend time together) and gossip about events rather than
positions are not started. Brief goal 4 and design decision 4; D7, D8, D9, D18; architecture,
"Ledger".

## Player-visible behavior

- **Today (shadow):** `[shadow] Abigail asked Sam about you: Sam saw you at Pelican Town 40 minutes
  ago.`
- **Planned:** NPCs who never met the player learn where they are through others, so a keen NPC can
  find them ([find.md](find.md)); in newcomer week, the town hears about the new farmer before
  meeting them ([newcomer-week.md](newcomer-week.md)). With event gossip, a line can say "Emily told
  me you gave Haley a sunflower", but only when the hearer really was told.

## Data model

Done: one entry per (observer, subject) with location, region, spot, absolute tick, hop count,
`ToldBy`, `DetailCap` (`src/NpcMemory/Ledger.cs`). Detail by age: NamedSpot < 12 ticks, Location
< 48, Region < 96, then EarlierToday, and Gone from the next 6:00.

Planned:
- **Heard diary kind** for event gossip: `Heard`, subject = who the news is about, `Detail` =
  `from=<teller>;kind=<original kind>;subject=<original subject>;name=...` (the original's keys
  copied). Only kinds marked shareable pass on: `GiftReceived`, `SawGift`, `QuestHelped`,
  `Festival`. News weight: the original's minus 1.
- No ledger format change.

## Triggers and game hooks

Done: `MemoryStore.AskAround` from `PlayerSearch.Tick`, on the game thread after `Observe`.

Planned: **ambient gossip**, `MemoryStore.Chat(now)`, called each tick right after `Observe`:
- Pairs of NPCs co-located for at least `ChatMinTicks` (3) consecutive ticks in the current span may
  chat once per span.
- Whether a pair chats: FNV-1a uniform of (seed, a, b, span start tick) below `ChatChance` (0.3). No
  model call: this runs every tick for many pairs and must stay cheap and on the game thread.
- In a chat, each side passes the other its view of the **player** through `Ledger.Gossip` (all D9
  rules hold), and at most one shareable diary event from today as a `Heard` entry, the one with the
  highest news score the listener does not already have.
- NPC-about-NPC positions are not gossiped ambiently (the ledger would churn with little use); only
  `AskAround` does that, when someone is being looked for.

## Laya questions

None. Gossip is mechanical. The model only sees its results (a `Told` lead in the ladder, a `Heard`
entry offered by the planner).

## Deterministic rules

Done (D9): gossip refuses self and Gone views, caps detail at the teller's, allows two hops, never
overwrites fresher knowledge, records `ToldBy`.

Planned:
- A `Heard` entry is never passed on again (one hop for events, which keeps rumours from looping).
- The same event reaches a listener at most once (check its diary for a `Heard` with the same
  original tick, kind and subject).
- The player's own presence doesn't matter: NPCs chat whether or not the player is near. It is
  simulated silently off-screen, as the brief asks, and costs only memory work.

## Tuning constants

| Name | Default | Saved |
|---|---|---|
| `SpotTtl` / `LocationTtl` / `RegionTtl` | 12 / 48 / 96 ticks | no (D7) |
| max hops | 2 | no |
| `ChatMinTicks` | 3 | no |
| `ChatChance` | 0.3 | no |

## Acceptance tests

Existing: `tests/NpcMemory.Tests/LedgerTests.cs`, `WhereaboutsTests.cs`.

To add (`tests/NpcMemory.Tests/ChatTests.cs`):
- Two NPCs together for 3 ticks with a passing seed: the one who saw the player passes it on at one
  more hop; with a failing seed nothing passes; never twice in one span.
- A shareable event becomes a `Heard` entry with `from`; a `Heard` entry is never re-shared; a
  non-shareable kind (`PassedBy`) is never shared.
- Same seed and inputs give the same chats (determinism).
- A 7-day shadow-harness run (`src/NpcShadow`) where the player meets only Lewis on day 1: by day 3
  some NPCs have a hearsay view of the player. This doubles as the newcomer-week spreading test.

In-game: after a day in town, `Told` leads appear in `would go looking` lines for NPCs the player
did not meet that day.

## Status

Done: `src/NpcMemory/Ledger.cs`, `MemoryStore.AskAround`. Not started: `Chat`, `Heard`.

## Open questions

- Is 0.3 per span the right rate? Too high and every NPC always knows where the player is, which is
  what D2 is against. Start low and read the shadow log.
- Should hearts between the two NPCs (friends, family) raise the chat chance? Family and friends
  data comes from `Data/Characters` `FriendsAndFamily` (a name-to-label map, not comprehensive: see
  [routines.md](routines.md)); a natural second step.
