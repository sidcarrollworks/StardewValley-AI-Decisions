# 5. Routine learning

**Status: partial (priors and decay wired; one asset-path bug fixed 2026-10-02).** Beliefs learn
from co-presence, weighted by hearts for the player, and Find uses `BestGuessAt`. Family priors,
daily decay and the `Seeded` flag are wired (PR #16). The seeding bug from #16 is fixed: the mod
loads `Characters/schedules/<Name>` (not `Data/Schedules`, which threw on the first villager and
seeded nothing), one missing asset skips only that villager, and a failed run never sets `Seeded`,
so misses seed on the next load. A use for "unlocked" is not wired. Brief goal 5 and design
decision 5; D11; architecture, "RoutineBelief".

## Player-visible behavior

- **Today:** only through Find: `Willy would go looking for you at the beach (you're usually there
  at this hour, 80% of the time)`.
- **Planned:** family and friends know roughly where each other are from day one (Jas knows where
  Vincent usually plays), so they find each other and mention each other sensibly without a learning
  period. Habits fade if the player changes routine. Lines can mention a habit once the pair is
  unlocked: "You're usually at the beach in the mornings, aren't you?"

## Data model

Done: `RoutineBelief(Observer, Subject, BlockMinutes)` with counts per region x block,
`CoPresenceTicks`, `UnlockThreshold` (saved: known issue), keyed `"observer>subject"` in
`MemoryStore.Beliefs`.

Planned:
- Stop saving `UnlockThreshold`: read it from `RoutineOptions.UnlockThreshold` (240) at use, keep
  reading the old field on load and ignore it. This matches D7 (tuning is not saved). No version
  bump: the field is simply ignored.
- A flag `Seeded` on the belief (saved) so a prior is applied once per pair per save.
- A new diary kind is **not** needed; habits are facts in the belief, not events.

## Triggers and game hooks

| When | What |
|---|---|
| `SaveLoaded` (after `LoadMemory`) | seed priors for family and friend pairs that are not yet `Seeded` |
| each tick (done) | `Observe` adds co-presence counts |
| `DayEnding` | `Decay(DailyDecay)` on every belief that had no co-presence today |

**Seeding priors.** For each NPC observer, for each subject in its `Data/Characters`
`FriendsAndFamily`. In 1.6.15 that field is a `Dictionary<string, string>`: the key is the other
NPC's internal name, the value an optional word used in dialogue ("mom"). The game says the list
"isn't necessarily comprehensive", and it doesn't say family or friend, so:
- **family** = the pair shares a home (the first `Home` entry's `Location`, or the `homes` table in
  `data/regions.json`); **friend** = listed but living elsewhere;
- the list is one NPC's view; don't assume the other NPC lists it back.

Steps:
1. Load the subject's raw schedule with `npc.getMasterScheduleRawData()` (confirmed; it loads
   `Characters\schedules\<Name>`), or `helper.GameContent.Load<Dictionary<string, string>>("Characters/schedules/<Name>")`.
   Loading through the content pipeline also picks up other mods' edits.
2. `RoutineExtractor.Extract` then `RoutinePrior.Build(routine, observer, saveSeed, options)` with
   family strength for family, friend strength for friends (the existing `PriorOptions`).
3. `belief.SeedPrior(...)`, mark `Seeded`.

This runs once per save, on the game thread, at load. It is pure CPU (a year of simulated schedule
per subject), so measure it: if it takes more than ~200 ms in total, move the extraction to a
background task and apply the priors on the game thread when done. No model call either way.

The player has no schedule, so there is never a prior about the player. The player's routine is
learned only from time together, as today.

## Laya questions

None. Routines are counted, not judged. (A possible later question, "is the player somewhere
unusual for them right now?", is answered deterministically by newsworthiness instead.)

## Deterministic rules

Done: each co-located tick adds `1 + 0.25 x clamp(hearts, 0, 14)` for the player, 1 for NPCs;
`BestGuessAt(block)` gives the top region, share and evidence; Find needs Evidence >= 3 and
Share >= 0.5.

Planned:
- **Decay:** multiply every count by `DailyDecay` (0.97) at day end on days without co-presence.
  A habit not refreshed for about three weeks halves. Priors decay too, so family knowledge that is
  never confirmed fades a little; that is acceptable.
- **Unlock** gates *speech*, not Find: a line may mention a habit only for an unlocked pair. Find
  keeps its own evidence floor.
- `Observe` ignores its tick argument today (`docs/decisions.md`, "Open work"). Leave it; decay by
  day covers what the tick would have been for.

## Tuning constants

| Name | Default | Saved |
|---|---|---|
| `UnlockThreshold` | 240 co-present ticks | today yes (to fix), then no |
| `DailyDecay` | 0.97 | no |
| habit floors for Find | Evidence 3, Share 0.5 | no (`WhereaboutsOptions`) |
| prior strengths | `PriorOptions` (family, friend) | no |

## Acceptance tests

- `tests/NpcMemory.Tests/RoutineBeliefTests.cs`: a belief loaded with an old `UnlockThreshold` of 999
  uses the option value; `Seeded` round-trips; seeding twice is a no-op.
- Decay: a day without co-presence scales every count by 0.97; a day with co-presence doesn't decay.
- Prior wiring (pure part): given a fake `FriendsAndFamily` table and fixture schedules
  (`fixtures/game`), the right pairs get priors with family stronger than friends, deterministic by
  save seed.
- Shadow harness: Jas with a family prior of Vincent gets a `Habit` lead for him on day 1.

In-game: log the number of seeded pairs and the time taken at load; confirm it is once per save.

## Status

Done: `src/NpcMemory/RoutineBelief.cs` (including `SeedPrior`, `Decay`, `BestGuess`, the `Seeded`
flag and the `LastObservedDay` day tracker), `src/NpcSchedules/RoutinePrior.cs`,
`RoutineExtractor.cs`. Step 7 landed: the saved `UnlockThreshold` is ignored on read (tuning is
not saved — an old 999 reads as the default 240), `MemoryStore.DecayBeliefs` runs at `DayEnding`
(`DailyDecay` 0.97 on every belief untouched that day), and the mod seeds family priors from the
game's own schedules once per save (same-home pairs, `FamilyStrength` 40, spouse skipped).
Fixed 2026-10-02: the seeding loop loaded `Data/Schedules/<Name>`, which does not exist in 1.6 —
the first load threw, the outer catch swallowed it, and no priors were ever seeded in-game
(`stardew-source-notes.md`, "Motives verify pass", "Schedules": the asset is
`Characters/schedules/<Name>`, NPC.cs:5993). The loop now loads that asset through
`Helper.GameContent.Load` (other mods' edits included), one missing asset skips only that
villager (Trace), and since a failed run never sets `Seeded`, misses seed on the next load.
The one gap from the acceptance list: with the playtest's `MinHabitEvidence` 12, a fresh prior
(about 3-4 pseudo-counts per block) does not alone clear the lead floor — priors sharpen real
co-presence rather than inventing leads. Revisit when distinct-day evidence lands.

## Open questions

- Married or dating NPCs' schedules come from `marriage_*` keys, which the extractor does not model
  (README, "Not modelled"). Priors about a spouse would be wrong; skip spouses as subjects.
