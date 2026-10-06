# Under Glass: the simulator

The headless simulator for **Under Glass**, the game designed in `docs/under-glass/design.md`. It's separate from the Stardew mod: it has its own solution, targets .NET 8, and references nothing from the game or SMAPI. The mod's `NpcSchedules.sln` doesn't include it.

```bash
dotnet test sim/UnderGlass.sln
dotnet run -c Release --project sim/UnderGlass.Run -- --seeds 200 --days 28            # natural acts, 200 seasons
dotnet run -c Release --project sim/UnderGlass.Run -- --seeds 400 --days 14 --inject   # one placed scandal per run
dotnet run --project sim/UnderGlass.Run -- --log 7 --days 3                            # one seed's event log
# --chat, --retell, --fade and --every override the gossip knobs, for sweeps
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
- **Hubs** (design rule 11, `Gathering`): noon in the square, evenings at the saloon, and market day on Saturday. While one is on, anyone free can pick it like a haunt, and stands at a seeded spot in the crowd.
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
  - The listener gets the story at 0.4 of the teller's juiciness, with the chain of tellers. A name can fill in a listener's "someone". At 0.4, a scandal heard second-hand is passed on only to people who know the culprit well, and news heard second-hand goes no further.
  - Juiciness fades 0.5 a day, or 0.65 for scandals, per whole 24 hours since each person got the story. A witness keeps telling a scandal for about four days.
- **Tiers** (rule 9, `Tier`): trivia (under 2), news (2 to under 4), scandal (a bad act at 4 or more) and upheaval (set per kind; none exist yet).
- **Scandal** (rule 9): when the people who blame the same person reach a quarter of those who know that person (at least 3), the boldest of them confronts that person, once per scandal. The target can be the wrong person.
- **Placed scandals** (`Harness`): natural scandals are rare, so `--inject` places one per run from day 1 at a seeded time. From then on, each minute that someone is awake, free and somewhere it can happen, there is a 1 in 120 chance it happens, by one of them chosen by a seeded draw. Nobody robs their own place of work. Spread metrics then count only the placed act.
- **Who was around** (`Scene`): for every act, how many people were awake within 8 tiles, in the same place but farther, elsewhere, or asleep.
- **Metrics** (`Metrics`), per tier:
  - reach and saturation;
  - the 40-70% band;
  - stories that died;
  - who gets blamed (right, wrong or "someone");
  - confrontations, and how many hit the right person;
  - how often each tier happens a year;
  - bedtimes, wake times, hours slept, alarms slept through, late arrivals and collapses.
- **Determinism:** every draw comes from a named SplitMix64 stream (`Rng`), and a run's log hashes the same on every machine.

Numbers after tuning (2026-10-06):

| | natural acts, 200 seeds x 28 days | one placed scandal, 400 seeds x 14 days |
|---|---|---|
| scandals a year, town-wide | 1.3 (target 1-2) | |
| news a year | 81 (about 1.6 a week; target a few a week) | |
| trivia a year | 421 (about 1.2 a day) | |
| news reach | 28% | |
| scandal witnesses | 0.9 | 0.8 |
| scandals nobody saw | | 52% |
| scandal reach, all | 27% | 28% |
| **witnessed** scandals in the 40-70% band over 3+ days | | **72%** (the 0a gate) |
| witnessed scandals over 70% / under 40% | | 24% / 2% |
| days a witnessed scandal keeps spreading | | 3.5 |
| blamed right / wrong / "someone" | 78% / 3% / 19% | 59% / 2% / 39% |

Sleep: bed at 22:40 on average (spread 1.5 hours, from about 19:00 to 1:00 by person), up at 7:10, 8.5 hours a night. About 1% of alarms are slept through, someone is late for work about once a season, and nobody collapses.

How we got there (2026-10-06):
1. **Who was around.** 57% of placed scandals began with nobody within 8 tiles; the other 12 people were spread over 10 places. Part of that was the harness: "the first moment anyone is able" was often a shopkeeper alone in their own shop at opening time. Placed scandals now happen at a seeded moment while someone is able, never by the keeper in their own shop.
2. **Hubs** (rule 11) brought people together: nobody within 8 tiles fell to 40%.
3. **What witnessed scandals did.** Once someone saw a scandal, gossip carried it too far and too fast: 48% reached more than 70% of the town, and spreading stopped after 2.4 days. How often people chat barely mattered (people at hubs spend hours together). The retell factor and the scandal fade did: a sweep of fade 0.3-0.8 against retell 0.4-0.7 put fade 0.65 and retell 0.4 best, with 72% in the band.

What these say:
- The gossip rules now make a witnessed scandal travel through about half the town over three or four days.
- About half of placed scandals are seen by nobody. Scandals at the clinic yard and in the shops are rarely seen: shelves and bushes halve sight, and a theft takes one minute. In the square they are seen by 1.3 people on average.
- A third of the people who hold a placed scandal blame "someone".

## Next

1. **0a, the rest:**
   - **Traces.** An unseen scandal can still be found later: missing stock noticed by the shopkeeper, a bin left scattered. That gives a "someone did it" story with no witness (design principle 1).
   - **Piecing together "someone"** from two partial accounts (clothing, direction) or from who was known to be nearby.
   - **Noticing someone out at an odd hour** (acting normal, design rule 1).
2. **0b:** a stock-and-flow money model.
3. **0c:** feelings. These are Spinoza's laws (design 3a): power of acting, joy and sadness, love and hate toward the cause, imitation, and reciprocity, built on regard and familiarity.

The Laya adapter (design 5a) will be a separate .NET 10 project that references this library. The simulator itself stays on .NET 8, which Godot 4 C# can use directly.
