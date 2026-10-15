# Penny's sourced routine, with bedtimes as habits: measurements, 2026-10-14

What D54 changes, measured on the town that ships with it (`pelican`) and its two comparisons, and
what early waking exposes. These are measurements to read, not pass marks: the town-wide numbers
are diagnostics, and nothing below was tuned toward a target.

**Labels:** **S** sourced canon (with its fixture key); **A** Sid's dated decision; **E** ours, a
simulation or pilot assumption; **VERIFY** not confirmed.

## Sid's decision (A)

Sid, 2026-10-14, answering "What should happen after an early sourced bedtime?" (verbatim):

> Pilot decision: bedtimes are habits, not hard rules.
> Keep Penny’s sourced bedtimes as routine preferences. Actual sleep and waking follow the energy rule: when rested, she wakes—even at 3 am—and can choose among the actions available to her.
> Don’t force sleep until 6 am, introduce a special Penny correction, or make her unavailable merely because her habitual bedtime has passed. Actual sleep can prevent conversation; the schedule alone shouldn’t.
> Record the habitual bedtime, actual sleep onset, waking time, energy, and what she chooses next. Distinguish sourced routines from our simulation assumptions.
> If early waking exposes missing activities or implausible decisions, report those gaps. We’ll assess them through the pilot rather than suppressing the outcome.

## What is sourced and what is ours

| | Value | Standing |
|---|---|---|
| Her evening place | `HubWeights`: `Evening = 0`, so the Saloon's evening gathering is not among her choices | S for the place: her schedule (`fixtures/game/Penny.json`) puts her at the Saloon only on year 1's green-rain day. "Never" is E: the sim has no green rain. The card says both (`OwnWeight.Source`, `.Assumption`), and the record marks it `sourced+ours` |
| Habitual bedtimes, first covering a day wins | 18:30 Tue, Wed, Fri (`Tue`; `Wed`, `Fri`: `GOTO Tue`); 20:00 Sat (`Sat`); 21:00 summer Mon, Thu, Sun (`summer`, `summer_Sun`); 18:40 the other Mon, Thu, Sun in spring and fall (`spring`; no `fall` key), and in winter (`winter` falls back to `spring` while the Community Center is unrestored) | S, each with its key in `KeptHour.Source` (`sourced` in the record). 18:40 through every winter is E: the sim has no Center to restore; that entry carries the assumption (`sourced+ours`) |
| What a bedtime does | From the hour until she next sleeps, going to bed is one of her free-time choices; at the first free-time decision after the hour a pick in progress is reconsidered once | A (a habit, not a rule); the mechanism is E |
| How strongly | `HabitOptions.BedtimeHabitWeight` = 9, against home's weight of about 1: by the weights, bed at the first pick 9 times in 10 (9/(9+1)) when nothing else is open; measured, 84-87% of her nights (below) | E, a first guess |
| Sleep onset | on reaching her bed, as for every bedtime | E (the sim's rule). Canon's hours are departures and she shows asleep on arrival; the walk's length in canon is VERIFY |
| Sleep length and waking | the energy rule: sleep ends when the bar is full | A (Sid's rule 1, kept). Canon wakes everyone at the 06:00 day reset; a 03:00 waking is the sim's, not the game's |
| What she can do on waking | whatever the ordinary decision offers at that hour | A (choose among the actions available); what is available is E |
| Availability | unchanged: free, in reach and askable until asleep | A |

## Method

Built from `claude/lives-routine` (PR B, Release). The town names are `Towns.Named`:
- **`pelican-as-shipped`**: no bedtimes and no own hub weights: the town as it was before D54. Its hashes equal
  the base's (PR A's head, which runs as `a9eadaf`): seed 1 `e9fd83b284f5c1b6`, seed 2 `6ba36224392527e3`,
  seed 3 `156bb4ea9ee275b5`; the errand runs and seed 29's hermit run also match the base's.
- **`pelican`**: the default town, Penny's sourced routine as habits.
- **`pelican-simulated-bedtime`**: her evenings at home (`Evening = 0`) and bed from the body alone, the
  "later simulated bedtime" the fifth direction keeps as the comparison.

```bash
# Penny, seeds 1-3 x 112 days, each town (pass --town every time, so the label names it)
dotnet run -c Release --project sim/UnderGlass.Replay -- --seed N --days 112 --town <town> --out <dir>/<town>-sN.json
# errands, seeds 1 2 3 5 6 7 x 28 days; invitations the same, with invitations in the catalog
dotnet run -c Release --project sim/UnderGlass.Replay -- --seed N --days 28 --town <town> --catalog returns,company,welcome,repair,sides,errands --out ...
# seed 29's hermit run
dotnet run -c Release --project sim/UnderGlass.Replay -- --seed 29 --days 112 --town <town> --0d6 bdefghm --out ...
# town-wide diagnostics
dotnet run -c Release --project sim/UnderGlass.Run -- --seeds 20 --days 112 --town <town>
```

The rows by night come from each recording's `nights` block (one row per sleep: the habitual
bedtime, when and why she set out, onset, waking, energy and her next choice); time with Pam, hours
awake and evenings at home from the moves (5-minute ticks); acts, beliefs, pursuits and regard from
their tables. The readers are throwaway scripts outside the repo; they only read. The measurements
were made with the block's version 1; version 2, after review, adds fields (each row's energy and
options at the hour and at setting out; each routine datum's assumption and standing) and changes no
run, so every hash and count below holds. A night belongs to the day its evening falls on; three
nights (one a seed) end asleep at day 112. "With Pam" is both awake, in the same place, within 8
tiles.

## Penny's nights, by day type (seeds 1-3 pooled)

**`pelican`** (sourced habits):

| Day type | Habitual bedtime (S) | Nights | Set out by habit / by body (body before the hour) | Set out, median | Asleep, median (range) | Energy at onset, share of bar | Woke, median (range) | Woke before 03:00 / 03:00-05:00 | Woke why; energy | Next choice: what, and the hour |
|---|---|---|---|---|---|---|---|---|---|---|
| Tue, Wed, Fri | 18:30 | 144 | 124 / 20 (2) | 18:30 | 18:31 (18:21-20:26) | 0.25 (0.15-0.33) | 02:50 (01:51-04:50) | 85 / 59 | rested 144; 1.00 | home 144: 01h 7, 02h 77, 03h 45, 04h 15 |
| Sat | 20:00 | 48 | 4 / 44 (42) | 19:15 | 19:16 (18:46-20:06) | 0.20 (0.18-0.25) | 04:06 (03:45-04:38) | 0 / 48 | rested 48; 1.00 | home 48: 03h 14, 04h 34 |
| summer Mon, Thu, Sun | 21:00 | 36 | 0 / 36 (36) | 19:52 | 19:54 (18:40-20:21) | 0.22 (0.18-0.23) | 04:30 (03:39-04:50) | 0 / 36 | rested 36; 1.00 | home 36: 03h 6, 04h 30 |
| other Mon, Thu, Sun | 18:40 | 108 | 97 / 11 (0) | 18:40 | 18:41 (18:40-20:16) | 0.24 (0.19-0.41) | 03:06 (01:08-04:43) | 43 / 62 | rested 105; 1.00 | home 105: 01h 2, 02h 41, 03h 48, 04h 14 |

- **The habit as a preference.** At the first decision after the hour she picked bed on 123 of 142
  Tuesday, Wednesday and Friday nights and 91 of 108 other Monday, Thursday and Sunday nights
  (84-87%), and home otherwise. Habit set-outs came within 5 minutes of the hour on 218 of 225 nights
  and more than an hour late on the other 7. After a home pick (37 nights) her body sent her later on
  30 and a later pick of bed on 7.
- **Her options then** were home and bed only: her haunts close at 18:00 and the evening gathering
  is not hers. Her first choice after every waking was home, with home the only option offered
  (333 of 333 wakings).
- **Every waking was "rested",** with a full bar; her 09:00 workday alarm never mattered.

**The comparisons, by day type** (asleep / woke, medians):

| Day type | as shipped | simulated bedtime | canon's departure (S) |
|---|---|---|---|
| Tue, Wed, Fri | 21:12 / 05:50 | 20:30 / 04:53 | 18:30 |
| Sat | 21:11 / 05:47 | 20:26 / 04:51 | 20:00 (in Town until then) |
| summer Mon, Thu, Sun | 21:21 / 05:53 | 20:30 / 04:53 | 21:00 |
| other Mon, Thu, Sun | 21:16 / 05:47 | 20:30 / 04:53 | 18:40 |

As shipped and with the simulated bedtime, the body sent her every night (no habit exists there);
both woke her rested every time. The simulated bedtime also has her up before 05:00 on 324 of 333
nights (as shipped 30).

## Penny's year, three towns (seed 1 / 2 / 3, or the sum of three seeds)

| | as shipped | `pelican` | simulated bedtime |
|---|---|---|---|
| Saloon evenings (awake there after 18:00) | 90 / 90 / 88 | 0 | 0 |
| ...with Pam there too | 81 / 82 / 79 | 0 | 0 |
| awake at home after 18:00, median minutes a day: Tue-Wed-Fri, Sat, summer M-Th-Su, other M-Th-Su | 32, 60, 75, 52 | 10, 55, 95, 20 | 125, 125, 130, 125 |
| hours awake 00:00-06:00 | 38 / 41 / 35 | 289 / 295 / 298 | 119 / 120 / 121 |
| ...with Pam awake in the same place | 0 | 0 | 0 |
| from waking to first leaving home, median hours | 3.8 / 4.1 / 3.9 | 6.7 / 6.8 / 6.8 | 4.7 |
| ...of it with nobody else awake at home | 3.4 / 3.5 / 3.4 | 6.0 / 6.0 / 6.1 | 4.2 |
| hours awake and alone at home after waking, a run | 423 / 418 / 414 | 679 / 668 / 687 | 502 / 493 / 528 |
| with Pam, hours (3 seeds): Saloon 18-24 | 359 | 0 | 0 |
| ...Trailer 18-24 | 39 | 20 | 88 |
| ...Trailer 06-12 | 262 | 261 | 259 |
| ...Trailer 12-18 | 80 | 76 | 82 |
| ...Square 06-12 / 12-18 | 60 / 158 | 61 / 156 | 56 / 157 |
| Pam's drunk scenes (3 seeds): all, Penny saw, Penny heard of | 15, 2, 7 | 15, 0, 8 | 15, 0, 8 |
| acts Penny witnessed | 174 / 175 / 132 | 92 / 112 / 111 | 89 / 115 / 115 |
| her `Fond` acts (3 seeds) | 1 (Maru) | 30 (Leah 16, Maru 14) | 32 (Leah 21, Maru 11) |
| her `Return` acts (3 seeds) | 28 | 61 | 62 |
| kindness to her, gifts and help (3 seeds): from outside her home and kin / from them | 62 / 6 | 120 / 7 | 122 / 6 |
| ...the givers (3 seeds) | Vincent 11, Haley 7, Jas 7, Pam 6, Maru 6 | Leah 44, Maru 40, Vincent 11, Pam 7 | Leah 47, Maru 39, Vincent 11, Pam 6 |
| regard at day 111, mean of 3 seeds, Penny to / from: Pam | +0.60 / +0.61 | +0.62 / +0.61 | +0.62 / +0.61 |
| ...Gus | +0.14 / +0.10 | 0.00 / 0.00 | 0.00 / 0.00 |
| ...Leah | +0.46 / +0.44 | +0.65 / +0.57 | +0.66 / +0.58 |
| ...Maru | +0.48 / +0.46 | +0.70 / +0.64 | +0.69 / +0.60 |
| Pam going through the bins (3 seeds); Penny's kin-shame regard changes | 6; 4 | 0; 0 | 0; 0 |
| her minutes out by season (range over 3 seeds x 4 seasons) | 8,285-10,705 | 5,960-7,210 | 5,585-7,305 |
| awake at the Saloon at 20:00, mean | 13.0 / 13.1 / 12.9 | 12.6 / 12.7 / 12.4 | 12.6 / 12.7 / 12.4 |
| the Trailer's purse at day 112, mean of 20 seeds | -877 g | +160 g | +160 g |

**Reading it:**
- **No Saloon evenings.** The habit and the hub weight do what the sources say. On the days whose
  habit acts, she is asleep within a minute of canon's departure (a median 18:31 for 18:30, 18:41
  for 18:40).
- **Saturday and summer evenings end early.** After a 04:06 or 04:30 waking her body is tired
  before 20:00 and 21:00 (gap G3). Her summer Monday, Thursday and Sunday evenings at home run
  from about 18:15 to 19:50 (a median 95 minutes awake at home after 18:00), not canon's 18:00 to
  21:00.
- **She hears of Pam's scenes rather than seeing them**, and her regard for Gus stays at 0: she no
  longer meets him.
- **She misses Leah and Maru.** They spend their evenings at the Saloon, so she sees them less, and
  missing them (`Fond`, 14 days) brings gifts both ways at the Square: kindness to her nearly doubles
  and her regard for both rises. The same happens with the simulated bedtime, so it comes from her
  evenings at home, not from the hour. Emergent; not tuned.
- **The household's money.** Her drinks (12 g on about 90 evenings a year) no longer leave the
  Trailer's purse. As shipped it ended a year in debt (the only household that did); now it ends
  at +160 g, and Pam's need-driven bin rummaging, and with it Penny's kin shame, does not occur
  (town-wide below). The placed-scandal shame check keeps a Penny and Pam case on seed 14.
- **The morning with Pam is unchanged.** Pam wakes at a median 09:02 and is asleep by 00:33; she is
  at the Saloon from a median 16:40. Penny leaves for work at 09:30-09:35, so the two are awake at
  home together for about half an hour on workday and Saturday mornings and over two hours on
  Sunday mornings, as before; in the evening, a few minutes a day.

## Errands, invitations and the hermit run

Errands (seeds 1, 2, 3, 5, 6, 7 x 28 days, `--catalog returns,company,welcome,repair,sides,errands`):

| | as shipped | `pelican` | simulated bedtime |
|---|---|---|---|
| favours asked of Penny (refused) | 31 (17) | 17 (9) | 21 (9) |
| ...by Pam (refused: beer, D52) | 9 (9) | 5 (5) | 6 (6) |
| promises Penny made | 14 | 8 | 12 |
| favours town-wide (refused) | 476 (189) | 438 (152) | 440 (153) |
| asked of her between her habitual bedtime and sleep | none (no habit) | 0 | none (no habit) |

With `invitations` added to the same catalog (the same seeds): invitations to Penny fall from 8
(mostly evenings at the Saloon) to 1, and hers from 2 to 0; none falls between her habitual
bedtime and her sleep.

Seed 29 with `--0d6 bdefghm` (the hermit story, `ReplayTests`): as shipped she is a hermit from day
73 (her hours out down 53%); in `pelican` from day 36 (down 53%); with the simulated bedtime she has
no spell. The test still passes. One seed: a story to read, not a rate.

## Town-wide (20 seeds x 112 days; diagnostics, not gates)

| | as shipped | `pelican` | simulated bedtime |
|---|---|---|---|
| feuds / friendships / reconciliations a year | 6.70 / 1.25 / 0.85 | 5.60 / 1.15 / 1.20 | 5.75 / 1.10 / 1.05 |
| seeds with a new feud and a new friendship | 70% | 85% | 80% |
| natural scandals (20 seed-years) | 44 | 12 | 13 |
| tempted scandals a year (Pam's, from need) | 2.2 (1.6) | 0.6 (0) | 0.7 (0) |
| households in debt at the end | 1.0 | 0.0 | 0.0 |
| lowest power of acting | Penny 0.44, Pam 0.44 | Newcomer 0.51 | Newcomer 0.51 |
| commonest named story (V1) | Scandal Pam 85% | Feud Sam-Shane 60% | Feud Sam-Shane 60% |
| desire, stirred a year: Answer, Return, Retaliate | 144.3, 204.6, 6.7 | 129.2, 226.1, 1.2 | 126.4, 226.2, 1.7 |
| `Fond` weighed and clear a year | 41.6 | 63.9 | 64.7 |
| the gate's acts a year: argued, gifts, help | 130.7, 228.4, 16.1 | 115.5, 270.9, 15.9 | 112.6, 271.9, 15.7 |
| arguments across households; answered in 1 d / 7 d | 146.4; 41% / 89% | 131.1; 42% / 90% | 128.3; 41% / 89% |
| kindness returned within 7 d | 63% | 65% | 65% |
| avoids / withdrawals a year | 6.0 / 35.2 | 3.6 / 22.6 | 4.3 / 26.0 |
| brawlers a seed-year | 2.00 | 1.85 | 1.60 |
| Penny (the fringe line): acts a year; free hours out a day, season 1 and 4 | 26; 5.9, 6.1 | 48; 3.9, 4.0 | 46; 3.9, 4.1 |
| the town's bed and rising (means) | 22:19, 06:45 | 22:13, 06:39 | 22:17, 06:43 |

Most of the change is the same in both new towns, so it comes from her evenings, not her hour: the
Trailer out of debt, Pam's bin rummaging gone with the scandal stories it fed, and a little less
feuding.

## The recorder's cost (seed 1, 112 days, `pelican`; three runs each, one process a run)

Re-measured with the block's version 2 (each row also carries the energy and options at the hour
and at setting out).

| | the simulation alone | recording without `nights` | recording with `nights` |
|---|---|---|---|
| CPU | 4.67-4.78 s | 5.09-5.47 s | 4.89-5.41 s |
| allocated | 1,189 MB | 1,316 MB | 1,335 MB |
| peak working set | 125-126 MB | 202 MB | 218-219 MB |
| file | | 10.23 MB | 11.68 MB |

The `nights` block is 1.45 MB for 112 days (12% of the file; 1.17 MB at version 1): 2,931 rows on
seed 1. Its CPU cost is inside run-to-run noise; it adds about 19 MB of allocation and 17 MB of peak
memory. No model is called. The hash is the same with it on or off (`8a002fea26b541fe`).

## Not represented: canon recorded, not modelled (S)

- **Rain** (the sim has no weather): `rain` has her reading at home from 18:00, `rain2` home from
  16:00 with dishes at 19:00; bed 21:00. The rain coin is `Game1.random.NextBool()`.
- **Dated days:** the 9th and 23rd (dishes at home from 16:00, bed 18:40, unless a farmer has 6
  hearts with Penny or with Sam); winter 4 (Town at 16:00, dishes at home at 19:00, bed 21:00);
  winter 15 (`winter_15`, the Beach 16:00-23:50; the Night Market's first day, dates VERIFY); the
  Desert Festival's second day (bed 24:40; dates VERIFY); year 1's green rain (the Saloon all day).
- **Saturday:** in Town (29,91) from 17:00 to 20:00, beside Jas and Vincent. The sim has her home
  from 18:00 (E).
- **Teaching:** Tuesday, Wednesday and Friday 09:00-14:00 at the museum. The sim keeps Monday to
  Friday 10:00-14:00 at the Square, for her and the two children (E).
- **Afternoons** in Town and the Forest, with the children (inferred, E). The sim's haunts stay
  guesses.
- **Winter after the Community Center's restoration:** the Center, dishes at home from 18:00, bed
  21:00. The sim has no Center, so 18:40 all year (E).
- **Waking:** canon wakes everyone at 06:00, and has no state for "in bed, awake". The sim's
  waking comes from the energy rule (A).
- **Grown towns:** her `Evening = 0` doesn't cover a grown town's `Green:<hood>` hubs (17:00-20:00);
  generated people who take after her card don't inherit her fields.

## What early waking exposes (reported, not fixed)

Sid asked for these to be reported and assessed through the pilot. None is suppressed or tuned here.

| # | Kind | Gap | Measured |
|---|---|---|---|
| G1 | missing activities, at night | When she wakes, home is her only option, and home has nothing in it: no reading, no dishes, no cooking, no lessons to prepare, no going back to sleep. She stands about the trailer's floor (the sofa tile most often) and picks home again every 1-3 hours. Canon has her reading and washing dishes at home (rain and summer evenings, S) | first choice after waking: home on 333 of 333 wakings, with home the only option offered; her only acts 00:00-06:00 are 12 stumbles in three seeds, alone (as shipped 1) |
| G2 | missing activities, early morning | Nothing she likes opens before work: her haunts open at 14:00, the market at 09:00 on Saturdays, noon at the square at 11:30 | from waking to first leaving home, median 6.7-6.8 h, 6.0-6.1 h of it with nobody else awake at home; 668-687 h a run alone and awake at home after waking (as shipped 414-423); first out at a median 09:35 on workdays (to work), 10:05 on Saturdays |
| G3 | the energy rule against two sourced hours | Her 20:00 (Saturday) and 21:00 (summer Monday, Thursday, Sunday) habits seldom act: after a 04:00-04:30 waking her body sends her first. Whether a habit should also hold off bed until its hour is a question for Sid; rule 1 says villagers go to bed when tired enough | the body set her out on 44 of 48 Saturdays (42 before 20:00, median 19:15) and 36 of 36 summer nights (all before 21:00, median 19:52) |
| G4 | the shape of sleep | Sleep lasts about 8-9 hours for everyone (waking drains a bar in 22 hours, sleep fills it in 11), so an 18:30 bedtime is a 02:50 waking. Nobody lies awake or dozes off again; canon has no awake-in-bed state either | woke before 03:00 on 85 of 144 Tue-Wed-Fri nights and 43 of 105 other Mon-Thu-Sun nights; awake 00:00-06:00 289-298 h a run (as shipped 35-41, simulated bedtime 119-121) |
| G5 | implausible decision, not hers alone | Delivering an errand ignores the hour and whether the one sought is asleep (`LookFor`, `Simulation.Errands.cs:337`): people awake at night, early risers and night owls alike, set out to find someone at their last-seen place | Penny, woken early, went looking for Haley at the Square at 02:41 (seed 3, day 9) and at 02:45, then the Beach at 03:45 (seed 7, day 5). In both new towns Abigail, still up, looked for Shane at 00:00, 01:00 and 02:00 (seed 1, day 18; asleep at 02:52) and Gus for Leah at 00:00 (seed 2, day 7; asleep at 00:57); as shipped and with the simulated bedtime, Vincent, a child woken early, looked for Penny at 01:31 and 02:35 (seed 7, day 11) |
| G6 | the household | Nobody is awake with her at either end of her night: Pam is at the Saloon from about 16:40, asleep at 00:33 and up at 09:02 | with Pam awake at home 00:00-06:00: 0 h in every town; at home together 18-24: 20 h over three seeds (as shipped 39, simulated bedtime 88) |
| G7 | open question | An ask after her habitual hour weighs only her body, job and mood. That follows Sid's ruling; whether a habit should make "not now" likelier can't be seen in these runs, because nobody asked her then | 0 favours and 0 invitations asked of her between her habitual hour and sleep in 12 catalog runs; invitations to her fell from 8 to 1. The placed test (`RoutineTests`, a 19:00 habit for everyone) shows such an ask is asked and answered |
| G8 | already there, town-wide | George, Jas and Vincent wake before 05:00 every night, and Evelyn nearly every night, with the same empty options; town-wide the first choice after waking is home on about nine wakings in ten | 333 of 333 nights (Evelyn 331) in all three towns; home on 7,721 of 8,658 first choices after waking (`pelican`, 3 seeds) |
| G9 | measures that read hours out | Her free hours out fall by a third, and the hermit measure compares hours out with her own first season | free hours out a day 3.9-4.0 (as shipped 5.9-6.1); seed 29's hermit spell starts on day 36 (as shipped 73) |

**For the pilot** (from the measurements, not decided here):
- **Her evening.** Her summer Monday, Thursday and Sunday evening at home is about 18:15-19:50 in the
  sim, not canon's 18:00-21:00. Pam is at the Saloon then.
- **When she and Pam overlap.** They are awake at home together for about half an hour on workday
  and Saturday mornings (09:00-09:30) and over two hours on Sunday mornings; at night, never.
- **Invitations.** Few people ask her anything in the evening now, because she is home alone.
- **Routines that learn.** Sid's direction is that later experience should be able to change a
  routine. Nothing does yet: a card is fixed for the run, and an own weight of 0 removes the hub from
  her draws outright, so no experience could bring the Saloon's evening back. How routines change is
  open for the pilot.
- **Pam's routine.** A bedtime past midnight is written as canon writes it, on its evening's day
  (`pam_sleep` at 2400 is 24:00, minute 1440), so Pam's sourced hours can be carried the same way;
  none is set here.
