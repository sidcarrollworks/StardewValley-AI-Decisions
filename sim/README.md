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
- **Traces** (design principle 1; `TraceKind`, `Simulation.Traces.cs`): a theft leaves missing stock that only the keeper notices, by counting (0.15 an hour, for three days); a bin rummage leaves a scattered bin that anyone who can see the spot notices (0.6 an hour x clarity, for 12 hours). A finder learns that it happened, not who did it ("someone"), at half the act's juiciness, so a story with no name is told only the day it is found.
- **The authority** (design rule 16; `AuthorityOptions`, `Authority`, `Simulation.Authority.cs`):
  - **Lewis is mayor.** Every run opens with a town meeting on day 0 (a gathering in the square at 11:30) and a vote for constable at noon. The bold (boldness 0.5 or more) stand; everyone but the newcomer votes, candidates for themselves, the rest for whoever appeals most (familiarity, understanding, boldness, a seeded whim).
  - **The constable patrols** the public places in free time (10:00-12:00 and 19:00-21:00, 45 minutes a stop), takes reports, and carries them to the mayor.
  - **Reports.** The keeper of the place (the victim: Pierre for his store, Shane the chain store, Harvey the clinic yard, Gus the saloon, Lewis the square) always reports, even what they only heard. The constable always reports what they saw or found. Other witnesses and finders report if willing: a chance of 0.2 + 0.6 x boldness, once per scandal. Reports happen when the two are together.
  - **Verdicts.** The mayor weighs each account naming someone: confidence x (1 first-hand, 0.5 hearsay) x his trust in the teller (0.5 + 0.5 x familiarity; 1 for his own). He decides when a name reaches 0.6 and leads the next by 2 to 1. "Someone" counts for nobody, so a case can stay open. He can be wrong when the accounts are, and never accuses himself.
  - **Fairness.** If the accused is close to him (familiarity 0.5 or more, or his household), there is a 2% chance he lets them off.
  - **The ladder.** First verdict: a warning, given in person (`WarnedByMayor`, a news act others can see). Second: restitution and a fine. Third: service. From the fourth: detention, held at the manor for 24 hours (`TakenIn`, also seen). Fines and service are logged until money exists (0b).
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
  - for scandals: known to anyone, known only from a trace, reported to the mayor, decided, decided right, let off, warnings and detentions;
  - who was voted constable across runs.
- **Determinism:** every draw comes from a named SplitMix64 stream (`Rng`), and a run's log hashes the same on every machine.

Numbers with traces and the authority (2026-10-06):

| | natural acts, 200 seeds x 28 days | one placed scandal, 400 seeds x 14 days |
|---|---|---|
| scandals a year, town-wide | 1.3 (target 1-2) | |
| news a year | 81 (about 1.6 a week; target a few a week) | |
| trivia a year | 420 (about 1.2 a day) | |
| news reach | 28% | |
| scandal witnesses | 0.8 | 0.8 |
| scandals nobody saw | | 51% |
| scandals known to anyone (seen, told or found) | 52% | 87% |
| known only from a trace | 7% | 38% |
| **witnessed** scandals in the 40-70% band over 3+ days, by sight and gossip | | **73%** (the 0a gate) |
| the same, counting people who only found a trace | | 66% |
| witnessed scandals over 70% / under 40% | | 30% / 2% |
| reported to the mayor | 51% | 85% |
| decided by the mayor / decided right | 28% / 100% | 25% / 99% |
| blamed right / wrong / "someone" | 68% / 0% / 32% | 44% / 1% / 54% |

Constable voted in (placed runs): Pierre 37%, Pam 17%, Shane 16%, Leah 12%, Emily 7%, Gus 6%, Alex 3%, Haley 1%.

Sleep: bed at 22:40 on average (spread 1.5 hours, from about 19:00 to 1:00 by person), up at 7:10, 8.5 hours a night. About 1% of alarms are slept through, someone is late for work about once a season, and nobody collapses.

How we got there (2026-10-06):
1. **Who was around.** 57% of placed scandals began with nobody within 8 tiles; the other 12 people were spread over 10 places. Part of that was the harness: "the first moment anyone is able" was often a shopkeeper alone in their own shop at opening time. Placed scandals now happen at a seeded moment while someone is able, never by the keeper in their own shop.
2. **Hubs** (rule 11) brought people together: nobody within 8 tiles fell to 40%.
3. **What witnessed scandals did.** Once someone saw a scandal, gossip carried it too far and too fast: 48% reached more than 70% of the town, and spreading stopped after 2.4 days. How often people chat barely mattered (people at hubs spend hours together). The retell factor and the scandal fade did: a sweep of fade 0.3-0.8 against retell 0.4-0.7 put fade 0.65 and retell 0.4 best, with 72% in the band.
4. **Traces.** Switching pieces off one at a time showed traces alone pushed witnessed scandals over the band (to 37% over 70%), because people who only saw the scattered bin counted as knowing. Finders now get half the juiciness, and the gate is also measured by sight and gossip only (73% in the band).

What these say:
- A witnessed scandal travels through about half the town over three or four days.
- Traces bring most unseen scandals to light: 87% of placed scandals are known to someone, against 49% seen.
- The mayor hears of 85% of placed scandals and decides a quarter of them, almost always rightly; the rest stay open because nobody named anyone. That is the opening for piecing together "someone".
- Detention needs four verdicts against one person, so it has not happened in these runs.

## Next

1. **0a, the rest:**
   - **Piecing together "someone"** from two partial accounts (clothing, direction) or from who was known to be nearby. The mayor's open cases are where it pays off.
   - **Noticing someone out at an odd hour** (acting normal, design rule 1).
2. **0b:** a stock-and-flow money model, with fines and restitution, and deterrence for vices (design rule 16).
3. **0c:** feelings. These are Spinoza's laws (design 3a): power of acting, joy and sadness, love and hate toward the cause, imitation, and reciprocity, built on regard and familiarity.

The Laya adapter (design 5a) will be a separate .NET 10 project that references this library. The simulator itself stays on .NET 8, which Godot 4 C# can use directly.
