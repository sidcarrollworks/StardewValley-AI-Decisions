# 4. Last-seen ledger and gossip

**Status: done in shadow; the magnitude design below is planned.** The ledger, decay and on-demand
gossip (when an NPC asks around) are done; step 7 (PR #16) added ambient gossip (`MemoryStore.Chat`,
once per span with a deterministic FNV-1a draw, both sides pass their view of the player) and the
`Heard` diary kind (original kind's news weight minus 1, one hop, at most once per listener). Brief
goal 4 and design decision 4; D7, D8, D9, D18, D25; architecture, "Ledger".

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
  `Festival`. News weight: the original's minus the hops it has travelled.
- **Gossip magnitude** (Sid, 2026-10-01): a hard one-hop cap is boring; news should travel as far
  as it matters. Each shareable event carries a magnitude — its news weight — and the number of
  relays it can survive (and how fast it fades) comes from that magnitude.
- No ledger format change (positions keep their D9 rules, below).

## Triggers and game hooks

Done: `MemoryStore.AskAround` from `PlayerSearch.Tick`, on the game thread after `Observe`.

Planned: **ambient gossip**, `MemoryStore.Chat(now)`, called each tick right after `Observe`:
- Pairs of NPCs co-located for at least `ChatMinTicks` (3) consecutive ticks in the current span may
  chat once per span.
- Whether a pair chats: FNV-1a uniform of (seed, a, b, span start tick) below `ChatChance` (0.3). No
  model call: this runs every tick for many pairs and must stay cheap and on the game thread.
- In a chat, each side passes the other its view of the **player** through `Ledger.Gossip` (all D9
  rules hold), and at most one shareable diary event as a `Heard` entry, the one with the highest
  news score the listener does not already have.
- NPC-about-NPC positions are not gossiped ambiently (the ledger would churn with little use); only
  `AskAround` does that, when someone is being looked for.

## Gossip magnitude: how far news travels, and how it fades

- A `Heard` entry stores the event's **remaining magnitude**, starting at the original event's news
  weight ([diary.md](diary.md)): 4 for a quest helped, 3 for a festival, 2 for a saw, 1 for small
  talk that is shared at all.
- **Hops**: an entry can be retold while its remaining magnitude is above `MinRetellMagnitude` (1);
  each retelling passes it on with `magnitude - 1`, and the news weight the planner sees falls by
  the same step. So weight-4 news can cross three listeners ("Pierre told Jodi, who told Caroline,
  who told me"), while a weight-2 saw dies at the first listener — as today, but for a reason now.
  Sid: *"gossip can get a magnitude value applied to it which helps determine how many times the
  gossip hops or fades."*
- **Fade**: at each 6:00, every unretold `Heard` entry loses 1 magnitude; old news fades to zero
  and stops being shareable (it stays in the diary for lines).
- The same event reaches a listener at most once (check its diary for a `Heard` with the same
  original tick, kind and subject), whatever the route, so the fan-out never loops.
- **Positions do not get magnitude.** A last-seen position stays under the D9 two-hop cap and never
  gains detail; magnitude belongs to event gossip. A position tip and an event rumour are different
  things, and a rumour does not refresh anyone's last-seen view.

## Laya questions

None. Gossip is mechanical. The model only sees its results (a `Told` lead in the ladder, a `Heard`
entry offered by the planner).

## Deterministic rules

Done (D9): gossip refuses self and Gone views, caps detail at the teller's, allows two hops, never
overwrites fresher knowledge, records `ToldBy`.

Planned:
- Magnitude as above: hops and daily fade are a pure function of the entry's magnitude; the
  retelling draw uses the same FNV-1a seed family, so the same save replays the same spread.
- The player's own presence doesn't matter: NPCs chat whether or not the player is near. It is
  simulated silently off-screen, as the brief asks, and costs only memory work.

## Tuning constants

| Name | Default | Saved |
|---|---|---|
| `SpotTtl` / `LocationTtl` / `RegionTtl` | 12 / 48 / 96 ticks | no (D7) |
| position max hops | 2 | no |
| event `MinRetellMagnitude` | 1 | no |
| event daily fade | 1 magnitude at each 6:00 | no |
| `ChatMinTicks` | 3 | no |
| `ChatChance` | 0.3 | no |

## Acceptance tests

Existing: `tests/NpcMemory.Tests/LedgerTests.cs`, `WhereaboutsTests.cs`, `ChatTests.cs` (step 7).

To add:
- A weight-4 event travels three listeners with magnitude 4 → 3 → 2 and stops; a weight-2 event
  dies at the first listener; the same listener never hears the same event twice by two routes.
- An unretold `Heard` loses 1 magnitude each 6:00 and becomes unshareable at zero.
- A retold rumour never refreshes or creates a position ledger entry.
- Same seed and inputs give the same chats and the same routes (determinism).
- A 7-day shadow-harness run (`src/NpcShadow`) where the player meets only Lewis on day 1: by day 3
  some NPCs have a hearsay view of the player, and by day 5 a weight-4 event the player caused has
  reached NPCs they never met. This doubles as the newcomer-week spreading test.

In-game: after a day in town, `Told` leads appear in `would go looking` lines for NPCs the player
did not meet that day; a quest-help line appears days later from an NPC the player never told.

## Status

Done: `src/NpcMemory/Ledger.cs`, `MemoryStore.AskAround`, `MemoryStore.Chat` (PR #16). Not
started: gossip magnitude (above).

## Open questions

- Is 0.3 per span the right rate? Too high and every NPC always knows where the player is, which is
  what D2 is against. Start low and read the shadow log.
- Should hearts between the two NPCs (friends, family) raise the chat chance? Family and friends
  data comes from `Data/Characters` `FriendsAndFamily` (a name-to-label map, not comprehensive: see
  [routines.md](routines.md)); a natural second step.
- Should `Heard` entries older than a day still be citable in lines? Today the planner prefers
  fresh entries; with magnitude, a slowly-fading rumour may be the only thing some NPC knows.
  Recommendation: yes, citeable while magnitude > 0.
