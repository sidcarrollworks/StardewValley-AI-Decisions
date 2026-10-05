# Under Glass: the simulator

The headless simulator for **Under Glass**, the game designed in `docs/under-glass/design.md`. It's separate from the Stardew mod: it has its own solution, targets .NET 8, and references nothing from the game or SMAPI. The mod's `NpcSchedules.sln` doesn't include it.

```bash
dotnet test sim/UnderGlass.sln
dotnet run -c Release --project sim/UnderGlass.Run -- --seeds 200 --days 28            # natural acts, 200 seasons
dotnet run -c Release --project sim/UnderGlass.Run -- --seeds 400 --days 14 --inject   # one placed scandal per run
dotnet run --project sim/UnderGlass.Run -- --log 7 --days 3                            # one seed's event log
```

Needs the .NET 8 SDK. It installs beside the 6.0 SDK the mod uses.

## Phase 0a: the gossip harness (built)

The cast is 12 of Stardew's villagers, used privately until the game has its own (design, section 3), plus the newcomer. They live in six public places (the square, the saloon, Pierre's store, the chain store, the beach, the clinic yard), a farm and their homes, joined by four roads.

What's modelled:
- **The clock** (design rule 1, decided 2026-10-05; `Clock`): 24 hours, the date changes at midnight, and there is no end of the day. A game day is about 20 real minutes. Routines and chats run on 5-minute ticks; acts and perception run minute by minute.
- **Bodies** (`Body`, `BodyOptions`):
  - Everyone has an energy bar of their own size. Rest drains a share of the bar (a whole bar in 22 waking hours); work and walking cost extra, the same for everyone, so a bigger bar takes hard work in its stride.
  - People go to bed when they feel tired enough: the share of the bar used plus a body clock that makes the small hours sleepy and the afternoon alert. Each villager's own threshold (`BedAt`) makes night owls and early sleepers.
  - Sleep refills the bar and stops when it is full (11 hours fill an empty bar).
  - Workers set an alarm an hour before work. It wakes them with a chance of 0.35 + 0.6 x how full the bar is; a villager who sleeps through it can be late for work.
  - At zero energy a person collapses, which others can see (`Collapsed`, a news act), and wakes at home.
- **Jobs and haunts** make each day. A job has a place, hours, days off and an effort. Haunts are free-time spots with hours and weights; a villager picks one by a seeded draw and stays 1-3 hours, or goes home.
- **Walking.** People walk 2 tiles a minute, door to door along the roads (`Link`), round walls, fences and shelves, so they are seen on the way.
- **Layered perception** (design rule 2, `Perception`):
  - **distance bands:** 1.0 at 0-2 tiles, 0.6 at 3-5, 0.3 at 6-8;
  - **line of sight:** walls block, and fences, bushes and shelves halve it;
  - **darkness:** outdoors from 20:00 to 6:00 halves it;
  - **watch time:** clarity is the sum over the minutes watched, against each act's read time. Sleepers see nothing.
- **What a witness takes away:**
  - **What happened:** a witness knows this at clarity 0.3.
  - **Who did it:** a witness needs clarity of `0.9 - 0.7 x familiarity` to tell. A stranger needs a close look. A friend can tell at about 4 tiles, and family who know someone well can tell at 8. Otherwise the witness records "someone".
  - **Confident guesses:** a bold villager with low self-regard names a guess as fact, weighted toward people they know.
- **Gossip** (rule 8):
  - Awake pairs together for 15 minutes may chat. The chance is 0.3 x (0.5 + chattiness) per 10 minutes, once per span and again every 2 hours.
  - A teller volunteers its juiciest story at 2 or more, plus 0.5 if the listener knows the believed actor well. It never tells a story back to whoever told it, never tells someone about themselves, and tells one story to at most one listener a day in a town under 20.
  - The listener gets the story at 0.7 of the teller's juiciness, with the chain of tellers. A name can fill in a listener's "someone".
  - Juiciness fades 0.5 a day, or 0.8 for scandals, per whole 24 hours since each person got the story.
- **Tiers** (rule 9, `Tier`): trivia (under 2), news (2 to under 4), scandal (a bad act at 4 or more) and upheaval (set per kind; none exist yet).
- **Scandal** (rule 9): when the people who blame the same person reach a quarter of those who know that person (at least 3), the boldest of them confronts that person, once per scandal. The target can be the wrong person.
- **Placed scandals** (`Harness`): natural scandals are rare, so `--inject` places one per run on day 1 at a seeded time, committed by whoever is first awake, free and somewhere it can happen. Spread metrics then count only the placed act.
- **Metrics** (`Metrics`), per tier:
  - reach and saturation;
  - the 40-70% band;
  - stories that died;
  - who gets blamed (right, wrong or "someone");
  - confrontations, and how many hit the right person;
  - how often each tier happens a year;
  - bedtimes, wake times, hours slept, alarms slept through, late arrivals and collapses.
- **Determinism:** every draw comes from a named SplitMix64 stream (`Rng`), and a run's log hashes the same on every machine.

Numbers on the 24-hour clock (2026-10-05):

| | natural acts, 200 seeds x 28 days | one placed scandal, 400 seeds x 14 days |
|---|---|---|
| scandals a year, town-wide | 1.4 (target 1-2) | |
| news a year | 82 (about 1.6 a week; target a few a week) | |
| trivia a year | 423 (about 1.2 a day) | |
| scandal witnesses | 0.5 | 0.5 |
| scandal reach | 23% | 24% |
| scandals in the 40-70% band | 12% | 7% |
| scandals never retold | 57% | 63% |
| blamed right / wrong / "someone" | 71% / 2% / 27% | 62% / 3% / 34% |

Sleep: bed at 22:30 on average (spread 1.6 hours, from about 19:00 to 1:00 by person), up at 7:00, 8.5 hours a night. About 1% of alarms are slept through, someone is late for work about once a season, and nobody collapses.

What these say:
- The tier rates are near the design's targets, with scandal and trivia rates set by hand until vices and money make them come from pressure (0b).
- Most scandals are still seen by nobody or one person, and more than half are never retold. Spread is the next thing to tune.

The first numbers, on the old 6:00-2:00 day with ten-minute ticks and no roads, are in the history of this file (PR #36).

## Next

1. **0a, tuning,** on placed scandals: the witness rate, the chat rate and the 40-70% band. Also piecing together "someone" from two partial accounts (clothing, direction), sightings as diary entries, and noticing someone out at an odd hour (acting normal, design rule 1).
2. **0b:** a stock-and-flow money model.
3. **0c:** feelings. These are Spinoza's laws (design 3a): power of acting, joy and sadness, love and hate toward the cause, imitation, and reciprocity, built on regard and familiarity.

The Laya adapter (design 5a) will be a separate .NET 10 project that references this library. The simulator itself stays on .NET 8, which Godot 4 C# can use directly.
