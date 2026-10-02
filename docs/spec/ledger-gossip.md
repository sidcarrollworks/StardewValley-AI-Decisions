# 4. Last-seen ledger and gossip

**Status: partial.** The ledger, decay and on-demand gossip (when an NPC asks around) are done,
and step 7 (PR #16) added ambient gossip (`MemoryStore.Chat`, once per span with a deterministic
FNV-1a draw, both sides pass their view of the player) and the `Heard` diary kind (original kind's
news weight minus 1, one hop, at most once per listener). The juiciness design below (D25,
2026-10-01) is planned and replaces that fixed one-hop rule. Brief goal 4 and design decision 4;
D7, D8, D9, D18, D25; architecture, "Ledger".

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

Done (step 7): the **Heard diary kind** for event gossip: `Heard`, subject = who the news is
about, `Detail` = `from=<teller>;kind=<original kind>;subject=<original subject>;name=...` (the
original's keys copied). Only kinds marked shareable pass on: `GiftReceived`, `SawGift`,
`QuestHelped`, `Festival`.

Planned:
- With juiciness (below), any kind whose juiciness can reach `VolunteerLevel` is shareable. The `Detail` gains `j=<juiciness when told>;hops=<n>`, so how juicy it still is
  can be computed, never stored and changed.
- No ledger format change (positions keep their D9 rules, below).

## Triggers and game hooks

Done: `MemoryStore.AskAround` from `PlayerSearch.Tick`, on the game thread after `Observe`.

Done (step 7): **ambient gossip**, `MemoryStore.Chat(now)`, called each tick right after
`Observe`. Planned changes for juiciness are marked:
- Pairs of NPCs co-located for at least `ChatMinTicks` (3) consecutive ticks in the current span may
  chat once per span.
- Whether a pair chats: FNV-1a uniform of (seed, a, b, span start tick) below `ChatChance` (0.3). No
  model call: this runs every tick for many pairs and must stay cheap and on the game thread.
- In a chat, each side passes the other its view of the **player** through `Ledger.Gossip` (all D9
  rules hold), and at most one shareable event from today as a `Heard` entry, the highest news
  score the listener does not already have. **Planned:** the juiciest one for that listener, at or
  above `VolunteerLevel`, that the listener does not already have (below).
- NPC-about-NPC positions are not gossiped ambiently (the ledger would churn with little use); only
  `AskAround` does that, when someone is being looked for.

## Juiciness: what gets told, to whom, and how fast it fades

Agreed with Sid on 2026-10-01 (D25), replacing the morning's "magnitude" draft. *"Gossip exists
on a scale. 'I saw this person here' is a 1, hardly gossip, and really shouldn't be shared unless
another character asks specifically... 'I saw X digging in the trash near the tavern' has a much
higher gossip level because of disgust. Unless gossip is crazy it really should fade fast."*

- **Base juiciness** per diary kind lives in the stressor table ([motives.md](motives.md),
  "Stressor profiles"), so each kind's emotional effect and gossip value are set in one place.
  Disgust and surprise are what make a story juicy: a position `Saw` is 1, a liked gift seen is
  1.5, a loved gift 2, a hated gift 3, being stood up 3, an argument 3, rummaging in the trash 4,
  a new couple 4, a divorce 5.
- **Volunteering**: in a chat a teller offers a story only when its current juiciness, for that
  listener, is at least `VolunteerLevel` (2). Below that it is told only when asked, which is
  `AskAround` (built). So "I saw the farmer at the saloon" never spreads on its own.
- **For that listener**: the teller adds `KnowsSomeoneBonus` (+0.5) when the listener has regard
  for, or a ledger entry about, anyone in the story. A teller tells people who would care; it
  never reads the listener's feelings.
- **Each retelling** passes the story on at `RetellFactor` (0.7) of the teller's current juiciness.
- **Fading**: juiciness falls by `FadePerDay` (0.5 a day; 0.8 a day for stories at base 4 or more),
  computed from the tick it was heard. Ordinary news is dead within a day or two; a scandal lasts
  about a week and crosses a few listeners.
- **Breadth**: one story each way per chat (as now), and each teller tells the same story to at
  most `RetellsPerDay` (3) listeners a day, so a busy Saloon night can't carry it to the whole town
  at once.
- **Dedupe**: the same event reaches a listener at most once (same original tick, kind and
  subject), whatever the route, so retelling never loops.
- **Positions keep D9.** A last-seen position stays under the two-hop cap and never gains detail;
  a rumour never refreshes anyone's last-seen view.

### How the listener takes it

- **Relevance**: the stress a `Heard` puts on the listener is the original's magnitude x
  `HearsayFactor` (0.5) x relevance, where relevance is 1, or up to 2 when the listener is drawn to
  someone in the story (positive regard >= 0.6, or 8+ hearts when the player is that someone). Sid's
  example: "X gave Y a gift and Y liked it" is casual conversation, until it reaches someone who
  loves X; for them it is `Jealous` ([romance.md](romance.md)).
- **Hearsay is elastic; confirmation makes it plastic** (Sid, 2026-10-01). A `Heard` only moves the
  listener's elastic stress, which fades. It becomes a lasting mark (its plastic share, into regard,
  [motives.md](motives.md)) when confirmed within `ConfirmWindowDays` (7):
  - **first-hand**: the listener sees the same kind of act by the same subject;
  - **from the source**: the teller was in the event (the subject, or the person it was done to);
  - **two routes**: two different tellers by independent routes give half the plastic share.
- Nothing in the system lies, so every rumour is true; confirmation decides when it sticks, not
  whether it is believed. False or distorted rumours would be a later feature.
- On a bad day the listener takes hearsay worse in tone only; the numbers stay as above.

## Laya questions

None. Gossip is mechanical. The model only sees its results (a `Told` lead in the ladder, a `Heard`
entry offered by the planner).

## Deterministic rules

Done (D9): gossip refuses self and Gone views, caps detail at the teller's, allows two hops, never
overwrites fresher knowledge, records `ToldBy`.

Planned:
- Juiciness as above: telling, retelling, fading and confirmation are pure functions of the
  diary; the chat draw uses the same FNV-1a seed family, so the same save replays the same spread.
- The player's own presence doesn't matter: NPCs chat whether or not the player is near. It is
  simulated silently off-screen, as the brief asks, and costs only memory work.

## Tuning constants

| Name | Default | Saved |
|---|---|---|
| `SpotTtl` / `LocationTtl` / `RegionTtl` | 12 / 48 / 96 ticks | no (D7) |
| position max hops | 2 | no |
| `VolunteerLevel` | 2 | no |
| `KnowsSomeoneBonus` | 0.5 | no |
| `RetellFactor` | 0.7 | no |
| `FadePerDay` | 0.5 (0.8 at base 4+) | no |
| `RetellsPerDay` | 3 per teller per story | no |
| `HearsayFactor` | 0.5 | no |
| `ConfirmWindowDays` | 7 | no |
| base juiciness per kind | stressor table, [motives.md](motives.md) | no |
| `ChatMinTicks` | 3 | no |
| `ChatChance` | 0.3 | no |

## Acceptance tests

Existing: `tests/NpcMemory.Tests/LedgerTests.cs`, `WhereaboutsTests.cs`, `ChatTests.cs` (step 7).

To add:
- A position `Saw` (1) is never volunteered; a juiciness-4 story is retold at 2.8, then 1.96 and
  stops; a story fades below `VolunteerLevel` on schedule; a teller tells one story to at most 3
  listeners a day; the same listener never hears the same event twice by two routes.
- A liked-gift story reaches a listener who loves the giver as `Jealous` with relevance 2, and
  everyone else as casual news.
- A `Heard` moves only elastic stress; a first-hand sighting, a teller from the event, or two
  independent tellers within 7 days move regard (two tellers by half).
- A retold rumour never refreshes or creates a position ledger entry.
- Same seed and inputs give the same chats and the same routes (determinism).
- A 7-day shadow-harness run (`src/NpcShadow`) where the player meets only Lewis on day 1: by day 3
  some NPCs have a hearsay view of the player, and by day 5 a juicy event the player caused has
  reached NPCs they never met, while their plain sightings have not. This doubles as the newcomer-week spreading test.

In-game: after a day in town, `Told` leads appear in `would go looking` lines for NPCs the player
did not meet that day; a quest-help line appears days later from an NPC the player never told.

## Status

Done: `src/NpcMemory/Ledger.cs`, `MemoryStore.AskAround`, `MemoryStore.Chat` and `Heard` (PR #16).
Built with motives (2026-10-02, `src/NpcMotives`): how the listener takes it, in part. A `Heard`
puts the original's stress at `HearsayFactor` on the listener (elastic); told by the person it
happened to, it is confirmed at once and leaves half the original's lasting mark in regard
(`RegardKeeper`); told by a witness, it stays elastic. Today's gossip passes on only the teller's
own entries, one hop, so a `Heard` `GiftReceived` or `QuestHelped` always comes from the source.
Not started: juiciness (above), relevance, first-hand confirmation and the two-routes half (they
matter once retelling goes past one hop).

## Open questions

- Is 0.3 per span the right rate? Too high and every NPC always knows where the player is, which is
  what D2 is against. Start low and read the shadow log.
- Should hearts between the two NPCs (friends, family) raise the chat chance? Family and friends
  data comes from `Data/Characters` `FriendsAndFamily` (a name-to-label map, not comprehensive: see
  [routines.md](routines.md)); a natural second step.
- Should `Heard` entries older than a day still be citable in lines? Today the planner prefers
  fresh entries; with juiciness, a slowly fading scandal may be the only thing some NPC knows.
  Recommendation: yes, citable while juiciness is above 1.
