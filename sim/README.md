# Under Glass: the simulator

The headless simulator for **Under Glass**, the game designed in `docs/under-glass/design.md`. It's separate from the Stardew mod: it has its own solution, targets .NET 8, and references nothing from the game or SMAPI. The mod's `NpcSchedules.sln` doesn't include it.

```bash
dotnet test sim/UnderGlass.sln
dotnet run -c Release --project sim/UnderGlass.Run -- --seeds 200 --days 28            # natural acts, 200 seasons
dotnet run -c Release --project sim/UnderGlass.Run -- --seeds 400 --days 14 --inject   # one placed scandal per run
dotnet run --project sim/UnderGlass.Run -- --log 7 --days 3                            # one seed's event log
# --chat, --retell, --fade and --every override the gossip knobs, for sweeps
# feelings (0c): --feel off|observe|on, --off <law> (repeatable), --plastic <x>, --target-base <x>,
# --fo <Name>=<value> for any FeelingOptions knob, --affect <Kind>=<joy>[,<plastic>] for a feeling row;
# --log also prints the strongest sentiments with their causes, ties and shop switches
# the desire gate (0d): --desire off|observe|on, --trait <Name>=<Trait>:<value> (repeatable),
# --tensions <depth> (0: none); a "desire" block reports the gate (DesireMetrics)
```

Every run formats its log in the invariant culture, so a seed hashes the same on every machine (before 0c, juiciness printed as "1,5" under a German culture).

Needs the .NET 8 SDK. It installs beside the 6.0 SDK the mod uses.

## Phase 0a: the gossip harness (built)

The cast is Stardew's families and the villagers who live alone, 25 in all, used privately until the game has its own (design, section 3), plus the newcomer. They live in six public places (the square, the saloon, Pierre's store, the chain store, the beach, the clinic yard), a farm and their homes, joined by five roads. The families: Pierre, Caroline and Abigail; George, Evelyn and their grandson Alex; Haley and Emily; Pam and Penny; Jodi, Kent, Sam and Vincent; Marnie and her niece Jas (with Shane renting a room); Robin, Demetrius, Maru and Sebastian. Gus, Harvey, Leah and Lewis live alone.

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
  - The listener gets the story at 0.35 of the teller's juiciness, with the chain of tellers. A name can fill in a listener's "someone". A scandal heard second-hand is passed on only to people who know the culprit well, and news heard second-hand goes no further.
  - Juiciness fades 0.5 a day, or 0.8 for scandals, per whole 24 hours since each person got the story. A witness keeps telling a scandal for about three days.
  - With 26 villagers, a teller tells a story to up to two listeners a day.
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
- **Piecing together "someone"** (design rule 16; `Simulation.Suspicion.cs`):
  - Everyone remembers who they saw, where and when: same place, within 8 tiles, in line of sight, kept for three days.
  - A witness who saw "someone", or a finder, recalls who was around the place at the time (30 minutes either side for a witness, the 8 hours before for a finder) and suspects up to 3 of them, strangers first. The keeper of the place is not suspected.
  - Suspicion travels with the story (`Belief.Suspects`) and reaches the mayor as "nearby" names (`Account.Nearby`). Nearby counts 0.25, split over the names, and never decides a case alone.
  - **Interviews:** the constable (or the mayor) questions the people named, most-named first, once each per case, when they meet. Being questioned (`Questioned`) is seen. The culprit may confess (0.25 + 0.5 x (1 - boldness)); a confession decides the case. Anyone else says who they saw there, which can name the culprit or point elsewhere.
  - The culprit keeps no belief about their own act: told about it, they learn nothing new.
- **Families and age** (design rule 17; `Kin`, `Stage`, `Simulation.Family.cs`):
  - Each villager has an age (guesses where Stardew gives none) and kin with roles: parent, child, spouse, sibling, grandparent, guardian, stepparent. Household and kin differ: Shane rents at the ranch.
  - Acts have an age range. Stealing and rummaging need 13, drunk scenes 18, arguments 13. A child squabbles with a sibling who is there (`Squabbled`). Children have lessons with Penny on weekdays.
  - Families cover: kin never report, retell, suspect or confront each other, and leave each other out when questioned. Questioned, they vouch for kin who are suspected (`Account.Alibi`). An alibi takes back half of one "nearby" and never offsets a sighting or a confession. The constable questions the most-suspected person's housemates.
  - A keeper who learns their own kin took from them doesn't report it: a family row (`FamilyRow`, a news act others can see) instead.
  - Only adults stand for constable; everyone 16 and over votes.
- **Acting normal** (design rules 1 and 17; `Simulation.Habits.cs`): everyone learns the hours each person keeps out of doors, from what they see. Someone seen out at night (22:00-6:00) at an hour the observer has never seen them out, nor the hour either side, once the observer has seen them out for 6 hours or more, becomes a news story about them (`OutLate`). Anyone living with a parent or guardian has a curfew (children 20:00, teens 22:00, grown children 1:00); a parent who learns their child was out past it has a family row with them.
- **Money** (phase 0b; `Economy`, `MoneyOptions`, `Simulation.Money.cs`):
  - A purse per household, a pocket per person, and the town's own purse. Money crosses the town's edge only through outside accounts, and the town's cash changes by exactly that (a test checks it to the gram).
  - Weekly paydays: wages, pensions and outside sales; earners keep 15%; allowances for Abigail, Alex, Haley, Vincent and Jas; groceries at Pierre's (restocked at 60%) or the chain (10% cheaper; its takings leave town). Drinks at the saloon in the evening. Everyday spending of what a purse holds beyond four weeks of costs, and a pocket beyond 300g.
  - Wants, priced by person, bought when the pocket covers them.
  - **Temptation** replaces the fixed rates for theft and rummaging: motive (an unmet want, a household short of a week's groceries, a dare for the bold) against believed risk (people in sight x the next ladder step x (1.2 - boldness)). Each tempted scandal records why (`SimResult.Motives`).
  - **The ladder pays:** restitution and a 100g fine on the second verdict, from pocket then purse; what can't be paid is three hours of service in the square (`Service`, seen).
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
  - suspicion (anyone suspected, the culprit among the suspects) and interviews (how many, how many innocent, confessions, wrong verdicts);
  - family rows, alibis and sibling squabbles;
  - odd-hour sightings a season, of whom, at what hours;
  - money: the town's cash by season, what crossed its edge, purses at the end, households in debt; tempted scandals by motive and by whom; fines paid and service served;
  - who was voted constable across runs.
- **Determinism:** every draw comes from a named SplitMix64 stream (`Rng`), and a run's log hashes the same on every machine.

Numbers with every family (26 villagers), 2026-10-06:

| | natural acts, 200 seeds x 28 days | one placed scandal, 400 seeds x 14 days |
|---|---|---|
| scandals a year, town-wide | 1.4 (target 1-2) | |
| news a year | 95 (about 1.8 a week) | |
| trivia a year | 524 (about 1.4 a day) | |
| news reach | 29% | |
| scandal witnesses | 1.8 | 1.4 |
| scandals nobody saw | | 45% |
| scandals known to anyone (seen, told or found) | 81% | 90% |
| known only from a trace | 12% | 34% |
| **witnessed** scandals in the 40-70% band over 3+ days, by sight and gossip | | **57%** (the 0a gate) |
| the same, counting people who only found a trace | | 51% |
| witnessed scandals over 70% / below the band (sight and gossip) | | 23% / 20% |
| reported to the mayor | 75% | 87% |
| someone suspected / the culprit among the suspects | 54% / 28% | 78% / 41% |
| questioned a scandal / of them innocent | 1.4 / 87% | 2.6 / 86% |
| confessions | 6 in 200 seasons | 73 in 400 runs |
| decided by the mayor / decided right | 43% / 97% | 47% / 99% |
| family rows / family alibis | 0 / 51 | 4 / 585 |
| sibling squabbles | about 0.2 a day | |
| seen out at an odd hour | about 1 a season (Shane, Sam, Pam, Sebastian, Abigail) | |

Constable voted in (placed runs): Pierre 21%, Maru 19%, Demetrius 11%, Abigail 7%, Shane 7%, Robin 6%, Alex 6%, Pam 6%, Sam 5%, Kent 4%, George 3%, Leah 3%, Gus 1%, Emily 1%.

Sleep: bed at 22:20 on average (spread 2.1 hours by person), up at 6:45, 8.4 hours a night. About 1% of alarms are slept through, someone is late for work about twice a season, and nobody collapses.

How we got there (2026-10-06):
1. **Who was around.** 57% of placed scandals began with nobody within 8 tiles; the other 12 people were spread over 10 places. Part of that was the harness: "the first moment anyone is able" was often a shopkeeper alone in their own shop at opening time. Placed scandals now happen at a seeded moment while someone is able, never by the keeper in their own shop.
2. **Hubs** (rule 11) brought people together: nobody within 8 tiles fell to 40%.
3. **What witnessed scandals did.** Once someone saw a scandal, gossip carried it too far and too fast: 48% reached more than 70% of the town, and spreading stopped after 2.4 days. How often people chat barely mattered (people at hubs spend hours together). The retell factor and the scandal fade did: a sweep of fade 0.3-0.8 against retell 0.4-0.7 put fade 0.65 and retell 0.4 best, with 72% in the band.
4. **Traces.** Switching pieces off one at a time showed traces alone pushed witnessed scandals over the band (to 37% over 70%), because people who only saw the scattered bin counted as knowing. Finders now get half the juiciness, and the gate is also measured by sight and gossip only (73% in the band).
5. **Every family** (26 villagers) gives tellers two listeners a day. Witnessed scandals overshot (41% over 70%); a sweep put the scandal fade back to 0.8 (the mod's value) and the retell factor at 0.35. 57% land in the band; the rest depend on how many saw it.

What these say:
- A witnessed scandal travels through about half the town over three days. In the bigger town the spread depends more on how many saw it: crowds at the hubs carry it far, one solitary witness doesn't.
- Traces bring most unseen scandals to light: 90% of placed scandals are known to someone, against 55% seen.
- The mayor hears of 87% of placed scandals and decides 47%, almost always rightly; most of the gain over traces alone is confessions. Most people questioned are innocent, more so now that suspects' families are questioned for alibis.
- Family rows are rare: they need a keeper to learn that their own kin took from them.
- Detention needs four verdicts against one person, so it has not happened in these runs.

## Phase 0b: money (built)

A year of the town (100 seeds x 112 days, 2026-10-06):

| | |
|---|---|
| town cash at each season's end | 9,300 -> 18,950 -> 20,290 -> 20,100 -> 19,770 g |
| in from outside / out, a week | 7,254 / 6,600 g |
| unexplained change in town cash | 0.000 g |
| households in debt at the end | 1 (Pam and Penny's trailer) |
| tempted scandals a year | 2.8: need 1.9 (Pam rummaging), want 0.8 (Abigail stealing), thrill 0.1 |
| the culprit among anyone's suspects | 9% (thieves wait until nobody is watching) |
| fines | all part-paid so far (the fined are mostly Pam, in debt), each followed by service |

## Phase 0c: feelings (built)

Spinoza's laws (design section 3a) on top of 0a and 0b. The spec's rules are F1-F18 (feeling) and S1-S10 (steering); the code is `Feelings.cs` (options and the pure rules) and `Simulation.Feelings.cs`.

What's modelled:
- **Regard** for every other person, -1 to 1 (love and hate, law 3), seeded from households (0.6) and friends (0.4), and healing toward that seed: 0.005 a day, faster on a day together with no new slight. **Regard for kinds of people** (law 9): prejudice, which weighs on someone as far as they are a stranger. **Mood**, the last three days of joy and sadness, which with need, an unmet want and being held gives each person's **power of acting**.
- **Every belief is felt.** Each act kind has a feeling row: who is pleased or hurt, how much, the share that becomes regard for its cause, how freely the cause is believed to act, and at whom it is aimed. The feeling is read from the holder's belief, never the truth, and goes to whoever they believe caused it. A witness who saw only "someone, a young man" holds it against young men; when they later hear the name, the feeling moves onto that person exactly.
- **The laws as factors:** feeling with those we love or hate (law 5) and with those like us (law 6, by household, life stage, kind and workplace); blame by freedom and known hardship (law 10: officials doing their job are blamed less, and housemates know when the purse was short); presence (law 11: seen close counts most; hearsay moves mood only, until a second independent teller agrees or a public consequence confirms it); temperament (law 12: sensitivity and retention); love after conquered hate (law 8, III P44).
- **What is done to someone:** being warned, taken in, questioned, set to service or rowed with at home is felt, and blamed on the official less by those who know they gave cause. An innocent who is named resents whoever named them; the guilty feel shame instead. Kin feel shame for each new person who knows, more when it is public, and a scandal costs the culprit's kin some standing.
- **Sentiments** (Sims 4): Grateful, Hurt, Approving, Indignant, Pleased, Envious, Wary, Wronged, Ashamed and Reconciled, each with the act that caused it, fading over about a season. No rule reads them; the log cites them ("why Abi Stole Kim Hurt since d0 act 0").
- **Steering:** whom gifts, help and arguments are aimed at (someone free and in reach, by regard; the other party takes part), who acts (the glad give and help more, the sad drink more), the gossip close tie, the mayor's trust and sway, who reports, whom a witness suspects or guesses, who confronts (nobody confronts someone they love), a grudge against a keeper as a motive to steal, and where each household buys its groceries.
- **Switches.** `FeelingOptions.Off` reproduces the runs from before 0c byte for byte (two pinned hashes). `FeelingOptions.Observe` feels everything and changes no act or belief (checked over 10 seeds x 14 days). Worlds built by tests feel nothing unless they ask; the town steers at `PlasticScale` 2.

The phase gate (2026-10-06; 400 seeds x 14 days with a placed scandal, and 200 seeds x 112 days):

| | Check | Result | |
|---|---|---|---|
| 1 | witnessed placed scandals in the 40-70% band (by sight and gossip) | 60% in the band, 22% over 70% (needs 52% or more, 28% or less) | passes |
| 1b | the authority | decided right 99%, reported 88% | passes |
| 2 | money | 0.000 g unexplained; 2.6 tempted scandals a year, none from grievance | passes |
| 3 | E0 | the pinned hashes hold with feelings off; observing changes nothing | passes |
| 4 | E1: a new feud and a new friendship between households | 0% of seed-years (needs 60%); no war towns, no dead towns | **fails** |
| 5 | regard at season ends | mean change +0.007 to +0.019; moved 0.1 or more 3.3% after one season, then 5.5-7.9% (band 5-25%); below -0.2: 0.0% (band 1-5%) | **partly** |
| 6 | tests | all pass | passes |
| 7 | Sid's read of 10 seeds (`--log`) | | open |

A year of the town, with feelings steering (200 seeds x 112 days):
- **The power of acting:** mean 0.56 (spread 0.05). Lowest: Penny 0.44 and Pam 0.45, whose trailer runs short.
- **Sentiments a season:** Grateful 63, Approving 18, Indignant 18, Hurt 12, Wronged 2, Wary 1.4, Ashamed 0.7.
- **Toward a scandal's culprit, each holder's net change:** saw it -0.055; saw or found it and heard the name -0.011; told twice by independent tellers -0.009; confirmed by a public consequence -0.032. Hearsay alone moves mood, not regard.
- **Being named:** 12% of the innocents someone named end at -0.1 or below toward a namer; someone confronted wrongly drops 0.22 toward their confronter. Kin feel 0.5 steps of shame per scandal.
- **Whom acts are aimed at:** 64% of gifts and help go to someone the giver loves (0.4 or more); 55% of arguments are inside a household. News 95 and trivia 478 a year (trivia was 524: a gift now needs someone in reach).
- **Groceries:** 0.17 households a year switch shops; on average 3.0 households shop at the chain at the start and 3.2 at the end.
- **Run time:** 200 seed-years took 6.5 minutes on 4 cores.

How we got there:
1. **Observing first.** With feelings only watching, every 0a and 0b number was the same as before.
2. **Company.** Company joy at 0.02 a chat lifted everyone's power of acting to about 0.68, with a spread of 0.05, so the day's events hardly told. At 0.005 they do, and Penny and Pam come out lowest.
3. **Steering with regard frozen** (`--plastic 0`) kept the band: 59%, with 22% over 70%. The reach rule cut trivia by a tenth.
4. **A dead town.** At the spec's first guesses, only 2.8% of pairs moved 0.1 or more in a year and every seed was a dead town. A sweep of 50 seed-years each:

   | Setting | moved 0.1+ | below -0.2 | friendships a year | feuds a year | reconciliations a year |
   |---|---|---|---|---|---|
   | plastic 1 (200 seed-years) | 2.8% | 0.0% | 0 | 0 | 0 |
   | plastic 1, target base 0.05 | 2.6% | 0.0% | 0 | 0 | 0 |
   | plastic 2 | 7.9% | 0.0% | 0 | 0 | 0.08 |
   | plastic 2, target base 0.05 | 7.4% | 0.0% | 0 | 0 | 0.04 |
   | plastic 2, drift 0.002 a day | 9.8% | 0.0% | 0.02 | 0 | 0.14 |
   | plastic 3, target base 0.05 | 12.1% | 0.2% | 0.28 | 0.08 | 0.58 |
   | plastic 2, target base 0.05, joy of arguments, gifts and help doubled | 15.5% | 0.2% | 1.2 | 0.04 | 0.88 |

   The town runs at plastic 2, the lowest setting inside the band for pairs moved.
5. **Why no feuds.** Acts aimed at someone still come at town-wide rates and land on whoever is in reach, which is mostly family at home: in one seed, Evelyn gave George 52 gifts in a year and George argued with Evelyn 8 times. Only about a dozen arguments a year fall between households, spread over many pairs, and the drift heals each in about a month. Nobody hurt can answer back, and kindness to someone outside the family is rarely returned. That is rule 10's desire gate, which comes next.
6. **Review.** Five reviewers, each finding checked by a skeptic. 16 of 18 findings were confirmed and fixed:
   - eight in the code: an actor at zero weight still acting; kin asked only for an alibi feeling named; a fine paid in full not felt; the presence ablation missing one route; and metric and runner fixes;
   - eight in the tests, where a check could not fail; those tests now fail when the rule they guard is broken (113 tests).

   The first test pass also found one real bug: a "someone" turned into a name of the same kind kept the prejudice.

**Each law off** (E2, 50 seed-years each). A law stays if removing it moves a story metric by 20% or more. In a town without feuds or friendships, the story metrics can't move. What does move:
- reconcile off: no reconciliations;
- presence off: three times as many, and more pairs move;
- shame off: no shame steps;
- imitation off: the drop toward a culprit triples, because bystanders' small changes no longer dilute it.

E2 is to be rerun once the desire gate makes the town lively.

## Phase 0d.1: the desire gate (built)

Rule 10, so that the hurt can answer back and kindness is returned (Sid, 2026-10-06). Designed by a panel (three designs, three judges; the "minimal" design won 2-1, with grafts), built in `Simulation.Desire.cs`, with `DesireMath` in `Feelings.cs`.

What's modelled:
- **Character** (0d.0): every trait is read from each person's current character, which starts as the cast's card and can be set (`SetTrait`, for tests and phase 0e). Nothing changes it yet.
- **Motives from events**, only from what the holder saw or took part in (rule 9's robbed keeper excepted): being argued with stirs **Answer**; a gift or help, **Return**; a hurt from someone still loved (0.2 or more), **MakeUp**; a theft from one's shop or being named while innocent, **Retaliate**. A motive fades over 7 days; a hostile one is stronger by the grudge. Argued with over one's own scandal: they gave cause, and feel shame instead.
- **Missing someone** (Fond): love above 0.2 grows into a wish to give, the more days since a day together (10 days to full).
- **The gate**: boldness + 0.5 x familiarity + 0.5 x intensity + (power of acting - 0.5), against a gift 0.4, help 0.5, an argument 0.5 + 0.3 hostile + 0.1 for each hostile act the other did to them in 28 days (fear). Decided in code more than 0.15 either side; inside, a seeded close call (logistic of 8 x margin, tilted by mood) asked again only when the motive moves 0.1. Two attempts a pair a day; one act a person a tick; a 3-day cooldown per ordered pair for arguments and confrontations. A declined help falls back to a gift (story 2).
- **Keeping away**: the hurt who cannot answer avoid that person for a week (no kindness to them), and go home from a haunt where they meet. A **stance** builds from hurts: the bold grow combative, the shy withdrawn (it raises the home's pull), and kindness eases it; it fades over a month. This is the loop by which hermits and brawlers can emerge (Sid, 2026-10-07).
- **Families cover**: the gate never acts between kin or housemates. The town-wide rates of gifts, help and arguments stay, as everyday occasions the gate answers.
- **The record**: every life's events (done, undergone, declined, avoided, withdrew, lapsed) with severity and how they turned out (answered, returned, rebuffed, avoided, ignored), for phase 0e's plasticity (rule 18).
- **Built and off**: light snubs and turning away with the third-slight mark (they drained feuds from 7.9 to 0.6 a year), pity at a mishap, the tone of a day's first meeting.
- **Switches**: with the gate off the town is 0c's (pinned, `c0488cc6bf81e64f`); with motives watched but not acted on, the same hash.

The gate (2026-10-07; 400 seeds x 14 days with a placed scandal, and 200 seeds x 112 days):

| | Check | Result | |
|---|---|---|---|
| 1 | witnessed placed scandals in the 40-70% band | 55% in the band, 25% over 70% | passes |
| 1b | the authority | decided right 99%, reported 87% | passes |
| 2 | money | 0.000 g; 2.7 tempted scandals a year, none from grievance | passes |
| 4 | E1: a new feud and a new friendship between households | **68%** of seed-years (0c: 0%); no war towns, no dead towns | passes |
| 5 | regard at season ends | below -0.2: 0.6% after one season, then 1.1-1.8%; mean change +0.011 to +0.025; moved 0.1+ 6.3-16% | passes from the second season |
| 6 | tests | 164 pass | passes |

A year of the town: feuds 8.1 and friendships 1.07 a year, reconciliations 0.7; most feuded: Sam and Shane (co-workers at the chain, in 109 of 200 seeds), then Emily and Pierre; friendships most often Penny and Vincent, Jas and Penny (the teacher and her pupils). 81% of arguments are with someone disliked, 11% inside a household (0c: 1% and 55%). News 273 and trivia 861 a year (0c: 95 and 478). A seed-year takes about 17 s of one core.

How we got there (50 seed-years each unless said):

| Setting | E1 | feuds | friendships | trivia | mean change d111 | moved 0.1+ d111 |
|---|---|---|---|---|---|---|
| base gate (30, then 200 seed-years) | 70%, 60% | 8.1 | 0.89 | 618 | +0.021 | 12.6% |
| + light acts | 32% | 0.6 | 1.18 | 653 | +0.029 | 12.5% |
| + light acts + stance 1 | 54% | 1.3 | 1.28 | 686 | +0.027 | 12.4% |
| + Fond 7 days | 76% | 7.2 | 1.66 | 1307 | **+0.051** | 22.5% |
| + Fond 10 days | 68% | 8.0 | 1.06 | 860 | +0.030 | 16.1% |
| + Fond 10 + stance + power (shipped; 200 seed-years) | 64% | 7.8 | 1.05 | 858 | +0.025 | 16.0% |
| the same, after the review's fixes (200 seed-years) | 68% | 8.1 | 1.07 | 861 | +0.025 | 16.1% |

Two defects were found by the gate's tests and fixed: a confrontation ignored the cooldown, and turning away while withdrawing did not cite its quarrel, so the arguer escalated against the one keeping away.

A review of the gate (three reviewers, each finding checked by a skeptic) confirmed nine more, all fixed:
- A kindness that cited something other than one's own kindness was never returned: a peace offering after an argument, or help after a stumble. Only the return of one's own kindness now stops there.
- Acts the actor undergoes (warned, taken in, questioned, service, a family row, a mishap) were recorded as things they did. They are recorded as undergone only.
- Turning away during a weighing let the same person begin a second act in that tick. One act a person a tick now holds for it too.
- Every curt greeting drew the same close call, because its motive cited no act. It is keyed by the greeting's minute.
- Tests added: watching with the gate and every added rule on changes nothing (T12b); the gate pinned on a year of seed 1 three ways (base `a123526358094b43`, stance and power `07505a08bf1a9f78`, shipped `f2af5b9f5b1fb4c4`); the cooldown checked for confrontations too; no overlapping acts; actor-side rows never recorded as done; a kindness's intensity carries no grudge (now `DesireMath.Intensity`).

## Phase 0d.5: tensions, the gate's metrics, the parameter space and E2 (built)

- **Starting tensions** (Sid, 0c question 1): Pierre and Shane both ways, Sebastian toward Demetrius, Abigail toward Pierre, at -0.3. Each is its pair's baseline, so it lasts and heals back there, and a pair seeded below a feud's -0.3 both ways is never counted as a new feud. `--tensions <depth>` sets them (0: none). Phase 0c's pinned town (P1, P2) starts without them; P3 pins the town as it ships (`e9fd83b284f5c1b6`).
- **Missing someone is slower:** `FondDays` 14, was 10. At 10, regard drifted up faster each season of a three-year run (check 7, below). A stopgap: Sid's model of missing people (how prone each person is, secure friendships missing less, gifts kept for birthdays and festivals) comes in 0d.6.
- **Long runs no longer slow down.** A three-year seed took 123 s on Sid's PC, the cost of a day climbing with the run: a profile (dotnet-trace, on Sid's PC) found 68% of the time in three rules rescanning every belief a person had ever held, and 12% in finding act kinds by a linear search. Beliefs are now indexed as they are written (the stories each person might still tell, dropped once they fade out of reach; and their scandal beliefs), act kinds come from a dictionary, and the acts in progress and the scandal acts from short lists. Nothing the rules do changes: every pin holds and a 336-day seed hashes the same as before. That seed now takes 31 s in the cloud container, and a day costs about the same at the end of a run as at the start.
- **`DesireMetrics`** (`DesireMetrics.cs`; the runner's "desire" block): motives stirred and weighed; acts by the gate and at the town's rates; arguments answered in kind and kindness returned across households; avoids, withdrawals and gave-cause; dislike split into across households, at home, and across without the seeded pairs; outcomes; stance at season ends with hermits and brawlers; how much of the feuding one person accounts for; and a fringe line for the boldest and the shyest, with free hours a day out of home (awake, not at work, recorded per season while the gate is on). A review (three reviewers, each finding checked by a skeptic) found ten metric and test defects, all fixed: answering windows cut off by the run's end, pairs that keep arguing counted inside households, run-length scaling, hours out counting work, scheduled acts counted as the town's rates, and tests that could not fail.
- **Runner:** `--desire off|observe|on`, `--trait <Name>=<Trait>:<value>`, `--tensions <depth>`.
- **Golden stories:** G1, a feud from answering back (seed 1: Lewis and Pam, day 10); G2, a friendship from kindness returned (seed 5: Gus, who keeps the saloon, and Alex, day 27).

The gate, as the town ships (2026-10-07; 400 seeds x 14 days with a placed scandal, 200 seeds x 112 days, and 50 seeds x 336 days):

| | Check | Result | |
|---|---|---|---|
| 1 | witnessed placed scandals in the 40-70% band | 55% in the band, 25% over 70% | passes |
| 1b | the authority | decided right 99%, reported 87% | passes |
| 2 | money | 0.000 g; 2.7 tempted scandals a year, none from grievance | passes |
| 4 | E1 | **60%** of seed-years (66% with Fond at 10 days); no war towns, no dead towns | passes, at the floor |
| 5 | regard at season ends | below -0.2: 1.1% at d27 (0.6% without the seeded pairs), then 1.6-2.1%; mean change +0.010 to +0.020; moved 0.1+ 5.8-13.8% | passes from the second season |
| 6 | parameter space (54 cells x 20 seed-years, Fond at 5-10 days, run on Sid's PC) | **14 of 54 lively** (26%; 11 if three cells exactly on a threshold fail), against 9 needed | passes |
| 7 | three years | year 3: below -0.2 3.1-3.9%, mean change +0.027 to +0.028, moved 0.1+ 18.2-20.3%; moved at d335 1.46 x d111 (limit 1.5); no war towns, no dead towns at any year's end | passes |
| 8 | fringe (reported) | brawlers yes, hermits no (below) | reported |
| 10 | tests | 174 pass | passes |

A year of the town: feuds 7.72 and friendships 0.89 a year, reconciliations 1.4 (0.7 without the tensions: Sebastian comes round to his stepfather in most seeds, by living with him). Across households, 41% of arguments are answered in kind within a day and 90% within a week, and 62% of kindnesses are returned within a week (counting only acts whose window ends inside the run). News 265 and trivia 697 a year. E2, the parameter space and the fringe were measured with Fond at 10 days, before the three-year check.

**Three years** (check 7). With Fond at 10 days, regard drifted up faster each season: mean change +0.090 and 34% of pairs moved at d335 (2.1 x d111), gifts from the gate doubling to 763 a year as friends missed each other, gave, and grew closer. Turning stance off left the drift as it was (+0.112); turning Fond off stopped it (+0.020); Fond at 14 days holds regard flat through year 3 (below). A year 3 of the town as it ships: feuds 7.2 and friendships 2.4 a year (more over three years than in the first, as people come to know each other), with every seed-span holding a new feud and a new friendship.

| day | 111 | 223 | 251 | 279 | 307 | 335 |
|---|---|---|---|---|---|---|
| mean change | +0.022 | +0.026 | +0.028 | +0.028 | +0.027 | +0.028 |
| below -0.2 | 1.9% | 2.9% | 3.1% | 3.3% | 3.8% | 3.9% |
| moved 0.1+ | 13.9% | 17.4% | 18.2% | 19.0% | 19.8% | 20.3% |

Pam's trailer runs deeper into debt every year (-3,200 g after three years), and the town's cash falls about 300 g a season after the first year: a phase 0b question for later.

**The first season.** At depths 0, 0.2, 0.3 and 0.4 (50 seed-years each), dislike that was not seeded is 0.5-0.6% at d27 in every case: the tensions hold their own pairs and spark nothing new in the first season, and add no feuds. Sid keeps them: the first season is the starter season.

**The parameter space** (check 6; `PlasticScale` {1.5, 2, 2.5} x `FearPerHit` {0.05, 0.1, 0.15} x `FondDays` {5, 7, 10} x `StanceWeight` {0, 0.5}). Lively cells per plastic and FondDays group, of six:

| | Fond 5 days | 7 | 10 |
|---|---|---|---|
| plastic 1.5 | 1 | 0 | 0 |
| plastic 2 | 0 | 4 | **6** (the town's group before Fond went to 14 days) |
| plastic 2.5 | 0 | 0 | 3 |

Plastic and FondDays work as one dial on how fast regard moves, so the lively cells lie on a diagonal. At plastic 1.5 E1 fails; at 2.5 regard moves too much and all three war towns appear; Fond at 5 days runs away (at plastic 2.5, 170-220 new friendships a year). FearPerHit 0.15 drops dislike under the 1% floor. StanceWeight changes the verdict in 2 of 27 pairs. The sweep ran in 58 minutes on Sid's Ryzen 7 7800X3D.

**E2** (each rule off, 50 seed-years; a rule earns its place by moving a story metric by 20% or more):

| Off | E1 | Feuds | Friendships | Reconciliations | Other | Verdict |
|---|---|---|---|---|---|---|
| none (shipped) | 70% | 7.08 | 1.08 | 1.66 | | |
| the gate | 0% | 0 | 0 | 0.98 | news 95 | essential |
| Answer | 2% | 0.02 | 1.56 | 1.42 | | essential |
| Return | 0% | 7.86 | 0 | 1.14 | kindness returned 45% | essential |
| Retaliate | 68% | 5.62 (-21%) | 1.06 | 1.62 | avoids -35% | keeps its place |
| Stance | 64% | 7.82 | 1.08 | 2.08 (+25%) | brawlers 0 | keeps its place |
| Fond | 64% | 7.20 | 0.98 | 1.66 | returned 58% (-17%), trivia -28% | under the bar on story metrics |
| MakeUp | 60% | 7.28 | 0.90 (-17%) | 1.72 | | under the bar (rare: 2 a year) |
| Avoid | 60% | 7.22 | 0.96 | 1.72 | | under the bar |
| Withdraw | 60% | 7.60 | 1.00 | 1.62 | | under the bar |

Rules under the bar stay for now and are a question for Sid, not a cut: avoiding and withdrawing are what 0d.6 builds the hermit on.

**The fringe** (check 8; 50 seed-years each). Brawlers emerge (3.5 people a seed-year reach +0.5 at a season end; Alex, Sam and Abigail most). Hermits do not: at boldness 0.02 Penny's stance ends at -0.01 and her free time out of home stays at 5.9 hours a day in season 1 and in season 4, as at her own boldness; the most withdrawn person in the town ends at -0.05. Alex at boldness 0.98 acts as he does at 0.80, since he already clears every gate he meets; only his stance rises (+0.61 to +0.69). The causes, from `docs/under-glass/withdrawal-research.md`: hurts at home and undergone hurts never move stance, and any kindness undoes a withdrawn stance at once. G3 and G4 are therefore not pinned; 0d.6 builds the paths Sid asked for.

## Next

1. **0a, left for later:** partial accounts (clothing, direction) that narrow "someone" further.
2. **0b, left for later:** shops trading only while the keeper is at the counter, prices that move with stock (design rule 12), promises and debts (rule 13), and choosing Pierre's or the chain by regard (0c).
3. **0c, left for later:** the third-slight mark (0c question 6), familiarity falling over time and forgetting weighted by regard (both move the 0a band), avoidance and haunts chosen by regard, law 7 (norms and reactions), law 13 (wonder), courting and jealousy, secrets, saving regard.
4. **0d.6:** missing people and gifts (Sid's model, replacing Fond's 14-day stopgap), hermits, brawlers and moods that spread (`docs/under-glass/withdrawal-research.md`, `masking-research.md`): measure first, then hurts at home (households argue), mood contagion (bad heavier at home only), shy plus left out, recovery, patience with the combative, expression as a seventh trait, and the first-greeting tone read through it. Then the town and acts as JSON, the bots, the story sifter and the replay viewer.
5. **0e:** character over time and generations (design rule 18): plasticity read from the life record, inheritance with mutation, the life course, time skips.

The Laya adapter (design 5a) will be a separate .NET 10 project that references this library. The simulator itself stays on .NET 8, which Godot 4 C# can use directly.
