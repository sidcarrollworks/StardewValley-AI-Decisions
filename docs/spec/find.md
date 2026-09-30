# 6. Find, and NPCs coming to find the player (visits)

**Status: done (shadow)** for asking around and choosing where to look. Visits (an NPC leaving what
it is doing and walking to another map to find the player), NPCs looking for each other, and any
live movement are not started. Brief goal 4; D18; architecture, "Finding the player".

**Sid's decision (2026-09-30):** NPCs should come and find the player, but only when they want to
very badly. A shop may close for a while because its keeper went out; that is realism, as long as it
is rare. Target: **1 to 2 visits a week on average, across the whole town**. Some characters are
more prone to it than others, and whether a visit happens should come from the model's judgment of
the character and the relationship, not from a fixed table.

## Player-visible behavior

- **Today (shadow):** `asked Sam about you: ...`, `would go looking for you at Pelican Town (Sam saw
  you there 40 minutes ago; urge 0.62)`.
- **In the same map (ladder `Approach`):** an NPC with a lead to a spot in the map it is already in
  walks over. See [ladder.md](ladder.md).
- **Visits (new ladder rung `Visit`):** once or twice a week, some NPC who misses the player badly
  (high urge, and the model judges it in character) sets off to where it believes the player is,
  which may be another map, including the farm. On the way it may ask NPCs it passes whether they've
  seen the player (the existing asking-around, now with a visible bubble "Have you seen @?" when the
  player is in that map). If it finds the player, it shows a bubble ("There you are!") and has a
  line queued for when the player talks to it: its planned news if it has some, otherwise a
  "came to find you" line in its voice. Then it goes back to its day.
- **Shops close while the keeper is out.** If Pierre goes looking for the player, Pierre's shop is
  closed until he is back at the counter, because the game only opens a shop when its owner is there
  (1.6 `Data/Shops` owner areas: verify). The mod does nothing special for this; it follows from the
  NPC being elsewhere. Visits during opening hours are allowed but rare by construction (below).
- **Not found:** after about an hour at the place it looked, the NPC gives up and goes back. The
  next day it may say so ("I went all the way to the beach looking for you yesterday").
- **Shadow:** `[shadow] Pierre would close the shop and go looking for you at the farm (Robin saw
  you there an hour ago; urge 0.93; p=0.71)`, then `[shadow] Pierre would give up and go back`.
- **NPCs looking for each other (later):** a parent looking for a child at dinner time. Shadow only;
  same machinery with a different subject and a rule-based trigger.

## Data model

Done: `Whereabouts(Seeker, Subject, Source, Place, Detail, AgeTicks, HopCount, ToldBy, HabitShare)`
(positional: grows only at the end), `WhereaboutsSource` (SeenNow, SeenToday, Told, Habit, Unknown),
`AskResult`, `WhereaboutsOptions` (`src/NpcMemory/Whereabouts.cs`); `PlayerSearch` cooldowns (not
saved).

Planned:
- `InitiationStep.Visit = 6`, **appended** (steps are saved as ints; [ladder.md](ladder.md)).
- Ladder state gains visit counters (global visits this week, the day of the last visit, per-NPC
  visits this week), saved in the existing `ladder` JSON with defaults for old saves.
- `VisitTrip(Npc, TargetLocation, TargetPlace, StartedTick, State)` with states
  `Leaving -> Travelling -> Searching -> (Found | GaveUp) -> Returning -> Done`, held by the game
  thread. Not saved: a trip in progress at save time ends with the day, and the day reset restores
  the schedule.
- Diary kind `WentLooking` (subject Player; detail `place=<location>;found=0|1`; news weight 4, or 2
  when found, since the meeting itself is the news then). See [diary.md](diary.md).
- `data/regions.json` gains `noSearch`: locations an NPC never walks into to look (the farmhouse and
  other farm buildings, the mines and Skull Cavern, the desert, Ginger Island). A lead there cannot
  become a visit; it may become a letter instead.
- `data/visits.json` (small table): NPCs that never visit because they can't plausibly travel
  (Krobus, the Wizard, the Dwarf, Sandy, children: names verify against 1.6 data). This is the only
  fixed list; who is *prone* to visiting is the model's call.

## Triggers and game hooks

Done: each tick, `PlayerSearch.Tick` (asks) then `RunLadder` (the ladder gets `LookFor`'s lead).

Planned, live (every game call here is verify, and needs the spike in [roadmap.md](roadmap.md)
step 11 before it is built):
- **Start:** the game thread drains a `Visit` attempt, checks `LiveGate` (no event, festival or
  menu; the NPC is not in a cutscene; the NPC's current map is not `noSearch`), and creates a trip.
- **Travel:** build a path from the NPC's map to the target with the game's route table
  (`WarpPathfindingCache` / `getLocationRoute` in 1.6: verify names) and hand it to the NPC as a
  one-off schedule for the rest of the trip. Keep the NPC's original schedule and restore it at the
  end.
- **Search:** at the target, the trip ends in `Found` as soon as the NPC's **own ledger** has a
  first-hand, age-0 sighting of the player (the ordinary `Observe` path: no peeking at the player's
  position), or in `GaveUp` after `SearchMaxTicks` (6) at the target.
- **Return:** restore the original schedule and send the NPC to where that schedule says it should
  be now (`checkSchedule`-style re-entry: verify; this is the same open problem as `Approach` in
  [ladder.md](ladder.md)).
- **Bubbles on the way:** only rendered when the player is in that map (brief: "render text only when
  the player is in the location").

## Laya questions

| Question | Type | State | Fallback |
|---|---|---|---|
| "Would <npc> drop what they are doing right now and go looking for the player?" | `noul` | the NPC card ([laya.md](laya.md)) plus: relationship (hearts, dating or married, days since they last talked), why (planned news, or just missing them), what the NPC would leave ("minding the shop until 5 PM", from its **own** schedule, which it knows), and the lead (place, how it knows, how old) | 0.5 |

This replaces the ladder's generic "should <npc> try to get the player's attention with Visit now?"
for this rung, because the answer depends on things only this rung needs (what the NPC is walking
away from). With the fallback, visits still happen (urge and caps gate them), just without character.

## Deterministic rules

Done (D18): ask when urge >= 0.45 and no first-hand sighting in the last hour, once per 6 ticks;
answers under the gossip rules; lead order own sighting today > tip today > habit (Evidence >= 3,
Share >= 0.5) > unknown; the region `Other` is never a place.

Planned:
- **Approach vs Visit.** `Approach` from a lead is limited to leads in the NPC's **current map**.
  A lead in another map makes `Visit` available instead. This changes today's shadow behavior, where
  Approach (urge 0.60) covers any lead with a place; update `FindTests` accordingly.
- **`Visit` is available when:** the lead has a place in another map that is not `noSearch`; hearts
  >= 2; the NPC is not in `data/visits.json`; time is between 9:00 and 20:00; not a festival day.
- **Threshold:** urge >= `VisitThreshold` (0.90). Only an NPC that has been missing the player for a
  long time, has news, or was ignored repeatedly gets there.
- **Caps:** at most `MaxVisitsPerWeek` (2) across all NPCs, at most 1 per NPC per week, and at least
  `MinDaysBetweenVisits` (2) days between any two visits. Weeks are Monday to Sunday, as for forced
  dialogue.
- **Visit is asked before the mildest step.** The ladder normally picks the mildest available step,
  so an NPC that hasn't seen the player today would always write a letter (0.80) before it ever
  visits. So when `Visit` is available and the urge meets its threshold, the ladder asks the visit
  question first; if the draw says no, it carries on with the mildest available step as today. This
  is the one exception to "mildest first", and it only applies at the top of the urge range.
- **Draw:** as for every rung, FNV-1a uniform below the model's `p`.
- **Tuning target, not a quota:** the caps guarantee "never more than 2 a week". The average (1 to 2)
  comes from tuning `VisitThreshold` against shadow logs; nothing forces a visit to happen. The
  heartbeat logs visits per week so the average can be read off a week of play.
- A visit counts as the day's attempt for that NPC (the per-NPC and global daily caps still apply).
- **Response:** the trip's `Found` state counts as the player responding only if the player then
  talks to the NPC within the usual 6-tick window; otherwise the visit is `Ignored` (urge -0.2 and an
  `IgnoredBy` line), like any other rung. `GaveUp` is neither: urge is halved (the NPC tried) and a
  `WentLooking` line with `found=0` is written.
- **NPC subjects (later):** `PlayerSearch` becomes `Search` keyed by (seeker, subject); an NPC
  looks for another NPC only for a rule-based reason (a small table), since there is no urge between
  NPCs.

## Tuning constants

`AskUrge` 0.45, `FreshTicks` 6, `AskCooldownTicks` 6 (`PlayerSearch`); `HabitMinEvidence` 3,
`HabitMinShare` 0.5 (`WhereaboutsOptions`). New in `InitiationOptions`: `VisitThreshold` 0.90 (the
`StepThresholds` entry for step 6), `MaxVisitsPerWeek` 2, `MaxVisitsPerNpcPerWeek` 1,
`MinDaysBetweenVisits` 2, `VisitEarliest` 900, `VisitLatest` 2000, `SearchMaxTicks` 6. None saved.

## Acceptance tests

Existing: `tests/NpcMemory.Tests/WhereaboutsTests.cs`, `tests/NpcInitiation.Tests/FindTests.cs`.

To add:
- A lead in the current map offers `Approach`; a lead in another map offers `Visit` only, and only
  at urge >= 0.90, hearts >= 2, inside the hours, not on a festival day, not for an excluded NPC,
  not into a `noSearch` map.
- Caps: a third visit in a week, a second for the same NPC, or one a day after the last visit is
  never attempted.
- A saved ladder from before this change loads with zero visit counters.
- The trip state machine (pure, in `src/NpcInitiation/VisitTrip.cs`): Found on a first-hand age-0
  sighting at the target, GaveUp after 6 ticks, Done after Returning; the outcome's urge and diary
  effects.
- Pacing simulation: a seeded 8-week run over the shadow harness with the varied fake gives an
  average between 1 and 2 visits a week and never more than 2 in any week. This is the test that
  keeps the target honest when other tuning changes.

In-game (after the travel spike): Pierre leaves the shop, the shop is closed while he is away, he
reaches the farm, finds or doesn't find the player, and is back behind the counter with his normal
schedule afterwards; nobody ever walks into the farmhouse.

## Status

Done: `MemoryStore.AskAround`, `LookFor`; `PlayerSearch`; the ladder's `HasLead`. Not started:
`Visit`, trips, the travel code, `noSearch`, `visits.json`, NPC subjects.

## Open questions

- The travel spike: can a one-off cross-map path be given to an NPC and its schedule restored
  afterwards, reliably? Everything live here depends on it.
- Should a shopkeeper leave a sign or a note when the shop closes? Nice touch, not needed for v1.
- Should the player's spouse be allowed to come looking? Spouses are left out of the ladder when
  live until marriage behavior is tested ([ladder.md](ladder.md)).
