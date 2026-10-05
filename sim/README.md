# Under Glass: the simulator

The headless simulator for **Under Glass**, the game designed in `docs/under-glass/design.md`. It's separate from the Stardew mod: it has its own solution, targets .NET 8, and references nothing from the game or SMAPI. The mod's `NpcSchedules.sln` doesn't include it.

```bash
dotnet test sim/UnderGlass.sln
dotnet run --project sim/UnderGlass.Run -- --seeds 200 --days 28   # metrics over 200 seasons
dotnet run --project sim/UnderGlass.Run -- --log 7 --days 3         # one seed's event log
```

Needs the .NET 8 SDK. It installs beside the 6.0 SDK the mod uses.

## Phase 0a: the gossip harness (built)

The cast is 12 of Stardew's villagers, used privately until the game has its own (design, section 3), plus the newcomer. They live in five public places and their homes, and follow routines with seeded variation. Acts happen at base rates: rummaging in a bin, theft, a drunk scene, an argument, help, a gift, a stumble.

What's modelled:
- **Layered perception** (design rule 2, `Perception`):
  - **distance bands:** 1.0 at 0-2 tiles, 0.6 at 3-5, 0.3 at 6-8;
  - **line of sight:** walls block, and fences, bushes and shelves halve it;
  - **darkness:** outdoors at night halves it;
  - **watch time:** clarity builds against each act's read time.
- **What a witness takes away:**
  - **What happened:** a witness knows this at clarity 0.3.
  - **Who did it:** a witness needs clarity of `0.9 - 0.7 x familiarity` to tell. A stranger needs a close look. A friend can tell at about 4 tiles, and family who know someone well can tell at 8. Otherwise the witness records "someone".
  - **Confident guesses:** a bold villager with low self-regard names a guess as fact, weighted toward people they know.
- **Gossip** (rule 8):
  - Pairs together for 3 ticks may chat. The chance is 0.3 x (0.5 + chattiness), once per span and again every 2 hours.
  - A teller volunteers its juiciest story at 2 or more, plus 0.5 if the listener knows the believed actor well. It never tells a story back to whoever told it, never tells someone about themselves, and tells one story to at most one listener a day in a town under 20.
  - The listener gets the story at 0.7 of the teller's juiciness, with the chain of tellers. A name can fill in a listener's "someone".
  - Juiciness fades 0.5 a day, or 0.8 for scandals, counted from when each person got the story.
- **Scandal** (rule 9): when the people who blame the same person reach a quarter of those who know that person (at least 3), the boldest of them confronts that person, once per scandal. The target can be the wrong person.
- **Metrics** (`Metrics`), per group of acts:
  - reach and saturation;
  - the 40-70% band;
  - stories that died;
  - who gets blamed (right, wrong or "someone");
  - confrontations, and how many hit the right person.
- **Determinism:** every draw comes from a named SplitMix64 stream (`Rng`), and a run's log hashes the same on every machine.

First numbers (200 seeds x 28 days, 2026-10-05):

| group | reach | reached 90%+ | 40-70% band | never retold | blamed right / wrong / "someone" |
|---|---|---|---|---|---|
| scandal | 30% | 1% | 11% | 48% | 54% / 20% / 25% |
| bad | 23% | 3% | 9% | 58% | 81% / 4% / 15% |
| good | 10% | 0% | 0% | 73% | 71% / 7% / 22% |

About 1.2 confrontations a season; 71% hit the right person; 38% of scandals were confronted.

What these say so far:
- With one listener per teller per day, gossip doesn't saturate this town. A rough model in the brainstorm, without that cap, had 76% saturation.
- Scandals often go unwitnessed: 0.7 witnesses on average, and about half are never retold.
- A fifth of the people who hold a scandal blame the wrong person.
- The design's target band of 40-70% reach is met by only 11% of scandals, so the next step is tuning, not more rules.

## Next

1. **0a, tuning:** the witness rate, the chat rate and the scandal band. Also piecing together "someone" from two partial accounts (clothing, direction), and sightings as diary entries.
2. **0b:** a stock-and-flow money model.
3. **0c:** feelings. These are Spinoza's laws (design 3a): power of acting, joy and sadness, love and hate toward the cause, imitation, and reciprocity, built on regard and familiarity.

The Laya adapter (design 5a) will be a separate .NET 10 project that references this library. The simulator itself stays on .NET 8, which Godot 4 C# can use directly.
