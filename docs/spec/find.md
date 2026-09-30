# 6. Find (looking for the player, and later for each other)

**Status: done (shadow)** for the player. NPCs looking for each other, and any live movement, are not
started. Brief goal 4; D18; architecture, "Finding the player".

## Player-visible behavior

- **Today (shadow):** `asked Sam about you: ...`, `would go looking for you at Pelican Town (Sam saw
  you there 40 minutes ago; urge 0.62)`.
- **Live (planned, v1):** only the in-location part. An NPC with a lead pointing at the location it
  is already in walks toward the player's last known spot (its own view, never the true position),
  with a bubble "There you are!" if it then sees the player. This is the ladder's `Approach` rung.
- **Live (planned, v2):** an NPC with a lead in another location detours there: the bubble "Have you
  seen @?" to NPCs it passes (rendered only when the player is in that location), then walks to the
  lead's place, looks around for up to an hour, and returns to its schedule. Letters remain the
  fallback if it fails.
- **NPCs looking for each other (planned):** a parent looking for a child at dinner time, a friend
  arranging to meet. Shadow only until the travel code exists; uses the same API with a different
  subject.

## Data model

Done: `Whereabouts(Seeker, Subject, Source, Place, Detail, AgeTicks, HopCount, ToldBy, HabitShare)`
(positional: grows only at the end), `WhereaboutsSource`
(SeenNow, SeenToday, Told, Habit, Unknown), `AskResult`, `WhereaboutsOptions`
(`src/NpcMemory/Whereabouts.cs`); `PlayerSearch` cooldowns (not saved).

Planned: `SearchTrip(Npc, Subject, TargetLocation, StartedTick, State)` held by the game thread for
live trips (not saved; a trip in progress at save time is abandoned and the NPC's schedule restored
by the day reset).

## Triggers and game hooks

Done: each tick, `PlayerSearch.Tick` (asks) then `RunLadder` (the ladder gets `LookFor`'s lead).

Planned live (v2), all verify:
- Route: `WarpPathfindingCache` / `getLocationRoute` in 1.6 (the brief notes a precomputed route
  table; the 1.6 name is unconfirmed).
- Walk: set a `PathFindController` or a synthetic schedule for the rest of the hour; then restore
  the original schedule (see [ladder.md](ladder.md), open questions).
- The trip ends when the NPC's own ledger gets a first-hand view of the player (success), after
  `SearchMaxTicks` (6), or at the NPC's next schedule point.

## Laya questions

None of its own. The decision to go looking is the ladder's `Approach` yes/no, whose state includes
the lead. A planned, optional second question when a lead is weak (Habit or 2-hop Told): "is it worth
<npc> walking to <place> to look for the player?" (`noul`, fallback 0.5). Add only if the shadow log
shows NPCs chasing bad leads.

## Deterministic rules

Done (D18): ask when urge >= 0.45 and no first-hand sighting in the last hour, once per 6 ticks;
answers under the gossip rules; lead order own sighting today > tip today > habit (Evidence >= 3,
Share >= 0.5) > unknown; the region `Other` is never a place; Approach from a distance needs a lead
with a place.

Planned:
- **NPC subjects:** `PlayerSearch` becomes `Search` with a `(seeker, subject)` key; a seeker looks
  for an NPC only for a reason (a planned line about them, family at dinner time from a small table).
  No urge exists for NPC-to-NPC, so the trigger is a rule, not the ladder.
- **Never walk into** the farmhouse, farm buildings, the mines or the desert (a blocklist in
  `data/regions.json`, new key `noSearch`); a lead there becomes a letter.

## Tuning constants

`AskUrge` 0.45, `FreshTicks` 6, `AskCooldownTicks` 6 (`PlayerSearch`); `HabitMinEvidence` 3,
`HabitMinShare` 0.5 (`WhereaboutsOptions`); planned `SearchMaxTicks` 6. None saved.

## Acceptance tests

Existing: `tests/NpcMemory.Tests/WhereaboutsTests.cs`, `tests/NpcInitiation.Tests/FindTests.cs`.
To add: NPC-subject searches follow the same lead order; a lead in a `noSearch` location never
produces an Approach; trips end on success, timeout or the next schedule point (pure state machine,
unit-tested without the game).

In-game (v2): with the switch on, an NPC walks to a tipped location and back, reaches its next
schedule stop, and never enters the farmhouse.

## Status

Done: `MemoryStore.AskAround`, `LookFor`; `PlayerSearch`; the ladder's `HasLead`. Not started: live
approach, trips, NPC subjects, `noSearch`.

## Open questions

- Cross-location travel is the riskiest code in the plan (schedules, warps, pathing). It could be
  cut entirely: letters and queued lines already cover "I was looking for you". See roadmap
  decision 5.
