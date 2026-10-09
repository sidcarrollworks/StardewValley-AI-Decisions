# Under Glass: a plan to explore the simulator

> **Historical exploration guide.** This tour records the simulator and questions of
> 2026-10-08. Use the [current roadmap](roadmap.md) for what to build next and the
> [reflection spec](specs/reflection-spec.md) for the newer observer experiments. Earlier
> outcome limits and the queue below are not current acceptance criteria or work order.

Written 2026-10-08 for Sid, after #47-#77 were merged into main. It walks through the simulator in
the order it was built, with commands to run and what to look for in each, and ends with the decisions
that were open then. The whole plan takes about three hours, and each part stands alone.

Run everything from the repo root in Git Bash. You need the .NET 8 SDK. Build in Release (`-c Release`):
a long run in Debug is several times slower. The authored simulations in this tour are seeded.
Hybrid runs also need the same recorded model answers and inputs for exact replay; a seed alone
does not make fresh model responses reproducible.

## 0. Check the working branch (10 minutes)

```bash
git status
dotnet test sim/UnderGlass.sln -c Release
```

The current suite has 607 tests; this guide originally described 408. CI runs the same suite on
every pull request (`.github/workflows/tests.yml`). Under Glass now uses `codex/reflection-prototype`
as its development base, with feature PRs targeting it; follow `AGENTS.md` rather than switching
an existing checkout to `main` to follow this tour.

## 1. What the simulator is (15 minutes of reading)

Under Glass is the game in `docs/under-glass/design.md`: a farming town that runs on simulation rules
instead of a script. `sim/` is its headless simulator. It has its own solution on .NET 8 and never
references the game or SMAPI.

- **The town:** Stardew's families and the villagers who live alone, 25 people, plus the newcomer.
  They live in six public places (the square, the saloon, Pierre's, the chain store, the beach, the
  clinic yard), a farm and their homes. The clock runs minute by minute, 24 hours a day.
- **A day:** people wake, work, go where their haunts send them at each hour, and sleep. Acts happen
  (gifts, help, arguments, stumbles, and now and then a scandal such as a theft). Anyone within 8 tiles
  may see an act, more clearly the closer they are.
- **Each phase added a layer.** `sim/README.md` has a section for each:

| Phase | What it added | Status |
|---|---|---|
| 0a | gossip by juiciness, beliefs and sources, traces, the constable and the mayor | on |
| 0b | money: households, purses, wants and needs, thefts from temptation, fines | on |
| 0c | feelings (Spinoza's affects): regard for each person, mood, sentiments with their causes | on |
| 0d.1, 0d.5 | the desire gate: what happens stirs motives (answer, return, make up, retaliate), and people act on them if they dare | on |
| 0d.6 | hermits, brawlers, moods that spread, missing people; steps b to j | built, every step off |
| Town T0-T4 | the town as one value, the 31- and 60-person towns, the generator, the town as a JSON file | built, opt-in |
| Variety, m-0 to m-2 | story events, V1-V8 (how different the runs are), scenario checks, forked runs | measures only |
| Act catalog, batch 1 | six slices of new acts: returns, company, welcome, repair, sides, late | built, every slice off |
| Batch 2, b2-0 | the seams batch 2's acts will plug into | built, changes no run |

"Off" means the shipped town runs as it did before the work. Pinned hashes in `PinnedTests.cs` prove
that: P3 (`e9fd83b284f5c1b6`) is the shipped town.

## 2. One seed, line by line (20 minutes)

```bash
dotnet run -c Release --project sim/UnderGlass.Run -- --log 7 --days 3 > log7.txt
```

About 360 lines. Each line is a day (`d0`), a time and an event. From seed 7:

| Line | Reads as |
|---|---|
| `d0 05:30 wake Robin rested 115` | Robin wakes rested; 115 is her energy |
| `d0 21:45 act 3 GaveGift by Leah to Jodi at Saloon` | act 3: Leah gives Jodi a gift at the saloon |
| `d0 14:35 belief Kent 2 Jodi Witnessed 1` | Kent now believes Jodi did act 2; he saw it; its juiciness is 1 |
| `d0 21:50 told Emily Kent 4` | Emily tells Kent about act 4 |
| `d0 21:45 regard Abigail Leah +0.011 -> 0.011 act 3 Imitation` | Abigail's regard for Leah rises after act 3, by the imitation of the affects |
| `d0 21:45 stirred Jodi Return Leah act 3 felt 0.20` | the gift stirs in Jodi a wish to return it |
| `d0 21:50 desire Jodi Return Leah act 3: GaveGift intensity 0.20 eff 0.71 cost 0.40 clear` | the gate weighs it: the wish is strong enough for its cost, so she acts |
| `d0 21:50 why Jodi GaveGift Leah Grateful since d0 act 3` | she gives Leah a gift back, and the log names why |
| `d0 12:00 elected constable Kent 2 of 23` | the town elects its constable |
| `d0 00:00 wants Pam 213` | Pam wants something that costs 213 g |

After the log: the strongest sentiments with their causes, ties, shop switches, the pairs whose regard
moved most, the run's **hash**, and the five shyest people.

Try:
- Run it twice: the hash is the same (`bc63ebe157dca125`).
- Follow act 3 through the file: who saw it, who was told, how regard moved, and Jodi's gift back.
- Run `--log 8` and compare the first day.

## 3. Watch a run (30 minutes)

```bash
dotnet run -c Release --project sim/UnderGlass.Replay -- --seed 7 --days 56 --html run7.html
```

Open `run7.html` in a browser (it holds the viewer and the run). Seed 7 matches the log above. The
viewer's guide is `sim/viewer/README.md`.

- **Across the top:** the whole town on one plan, everyone moving. A ring marks an act (blue kind, red
  hostile). Under it is the timeline: Space plays, the arrow keys step 5 minutes (Shift: an hour), and
  `[` and `]` step a day. Click the ruler to jump to a feud or a scandal.
- **Town:** each place as a small map, with who is there.
- **Population:** everyone in one table at the clock. Sort by stance, mood or any trait.
- **Relationships:** the ring of households with strong ties, and the grid of everyone's regard. Switch
  to "change since the start". Click a pair to see what moved it.
- **Stories:** each act, how far and how fast it spread, how each person came to know it, and who
  believes the wrong name.
- **Explanation:** every setting and term, with its value and the line of code it comes from.
- **The inspector** (right): click anyone to see their character, mood, stance, likes and dislikes,
  the motives they weighed, and their life record.

Look for: a feud forming (two people arguing, each answering the other), a gift returned, a story
reaching someone who never saw it, the constable questioning the wrong person.

## 4. The town in numbers (20 minutes)

```bash
dotnet run -c Release --project sim/UnderGlass.Run -- --seeds 100 --days 112 > year.txt
```

A year (112 days, four seasons of 28) for 100 seeds, in a few minutes. The summary comes in blocks:

- **The 0a table:** for trivia, news and scandal, how many acts, how many saw each, how far each
  spread, and whether people believe the right name.
- **Sleep, familiarity, the constable, scandals, suspicion, families, money, temptation:** one line each.
- **feelings:** power of acting; how regard moved; **ties a year** (feuds, friendships); the top feuds.
  E1 is "seeds with a new feud and a new friendship": the share of seed-years in which two households
  start a feud and two start a friendship. Its gate is 60% or more.
- **desire:** what was stirred, how the gate weighed it, and the acts it chose.
- **withdrawal:** who is left out, and the spells (withdrawn, hermit, brawler). Empty while 0d.6 is off.
- **variety:** V1-V8, each with its target in brackets.
- **story:** kindness returned or ignored, threads of acts, how long feuds last.

## 5. Hermits, brawlers and moods that spread: 0d.6 (30 minutes)

Each step is a letter in `--0d6`:

| Step | What it does |
|---|---|
| b | hurts with no motive (from kin, a housemate, the constable) still move stance, and households argue back |
| c | moods spread: after a chat, each person's mood moves toward the other's |
| d | being left out withdraws the shy, and a withdrawn stance takes a stranger's kindness only in part |
| e | the withdrawn stay in: home pulls harder, and they chat and seek loved ones less |
| f | recovery: a stance fades with time, and one's own kindness returned eases it in full |
| g | patience runs out with someone who keeps arguing, and keeping away from someone makes them more combative |
| h | expression: only the hurt that shows is answered and passed on; the held part withdraws a person |
| t | the first greeting's tone: a day's first chat may be taken as warm or curt (`Tone` sets the chance) |
| m | missing people (your model): missing someone loved grows with days apart; birthdays and festivals are occasions for gifts |
| i | a mishap weighs on the shy (your answer A4) |
| j | a combative stance takes a stranger's kindness only in part (A10) |

```bash
dotnet run -c Release --project sim/UnderGlass.Run -- --seeds 100 --days 112 --0d6 bcdefghmt > review.txt
dotnet run -c Release --project sim/UnderGlass.Run -- --seeds 100 --days 112 --0d6 bcdefghmtij > review-ij.txt
```

Compare the two withdrawal blocks. Measured on 2026-10-08: hermits 0.28 a seed-year rise to 0.36 with
i and j, brawlers 1.40 to 1.56, and E1 stays near 77%. Children are never counted as hermits (A9).

Watch one:

```bash
dotnet run -c Release --project sim/UnderGlass.Replay -- --seed 29 --days 112 --0d6 bcdefghmtij --html run29.html
```

In Population, sort by stance: a person in a spell is tagged. The inspector shades each spell on their
stance line (blue withdrawn or hermit, red brawler).

Linus isn't in the 25-person town; he arrives in the 31-person town. With 0d.6's steps on he was a
hermit in 81 of 100 seed-years:

```bash
dotnet run -c Release --project sim/UnderGlass.Run -- --town pelican31 --seeds 50 --days 112 --0d6 bcdefghmtij > linus.txt
```

Look at "spells by person" in the withdrawal block.

## 6. The act catalog, batch 1 (30 minutes)

Six slices, each behind a switch (the acts spec, `docs/under-glass/specs/acts-spec.md`):

| Slice | Acts |
|---|---|
| returns | Thanked, Complimented, Joked |
| company | PlayedGame, TreatedToDrink |
| welcome | Welcomed, and the Curious motive |
| repair | Apologised, and the Remorse motive |
| sides | Mocked, StoodUpFor, Comforted |
| late | LateForWork |

```bash
dotnet run -c Release --project sim/UnderGlass.Run -- --log 2 --days 14 --catalog returns,company,welcome,repair,sides,late > acts2.txt
dotnet run -c Release --project sim/UnderGlass.Replay -- --seed 2 --days 56 --catalog returns,company,welcome,repair,sides,late --html acts2.html
```

In the viewer, the new acts read as sentences ("Haley paid Penny a compliment", "Sam came in late for
work"). In seed 2's first four weeks, compliments and welcomes are the commonest new acts, with about
16 mockings.

The batch gate (`sim/README.md`, "The batch gate"): without Sides, batch 1 passes every gate on both
bases. With Sides, friendships rise 38% on the review's set, past the 30% limit.

```bash
dotnet run -c Release --project sim/UnderGlass.Run -- --seeds 100 --days 112 --catalog returns,company,welcome,repair,late > b1.txt
dotnet run -c Release --project sim/UnderGlass.Run -- --seeds 100 --days 112 --catalog returns,company,welcome,repair,sides,late > b1-sides.txt
```

Compare "ties a year" and the story block.

## 7. Bigger towns (20 minutes)

```bash
dotnet run -c Release --project sim/UnderGlass.Run -- --town pelican31 --seeds 50 --days 112 > t31.txt
dotnet run -c Release --project sim/UnderGlass.Run -- --town pelican:60@1 --seeds 8 --days 56 > t60.txt
dotnet run -c Release --project sim/UnderGlass.Replay -- --town pelican:60@7 --seed 1 --days 28 --html town60.html
```

`pelican:60@7` is the 60-person town the generator builds from town seed 7. The town block
(`TownMetrics`) says whether a bigger town behaves as a town or as a crowd: how many tellings stay
inside a neighbourhood, against chance. In the viewer, the neighbourhood picker beside the tabs narrows
every tab to one neighbourhood.

Edit a town by hand:

```bash
dotnet run -c Release --project sim/UnderGlass.Run -- --town pelican31 --dump-town town31.json
# change someone's traits or haunts in town31.json, then:
dotnet run -c Release --project sim/UnderGlass.Run -- --town file:town31.json --seeds 20 --days 56
```

## 8. Scenario checks (15 minutes)

A check stages a scene many times (someone rummaging in a bin with one loner nearby, or in the square
on market day) and tests how far the story reaches against its own criterion. The table is in `sim/README.md`,
"Reach in context".

```bash
dotnet run -c Release --project sim/UnderGlass.Run -- --check C3
dotnet run -c Release --project sim/UnderGlass.Run -- --check today
```

Each check prints what it measured, its criterion, and "passes" or "fails". C3 runs 400 two-week runs
in a few minutes. `today` runs C2, C3, C4, C6, C10, C11 and C13, each at its own size, so it takes much
longer: start it in a second terminal, or add `--seeds 100` for a quicker, noisier look. C3, for
example, stages someone rummaging in a bin at the clinic yard with one loner nearby, and wants it to
reach 20% or less of the actor's circle in 70% or more of runs.

## 9. Variety and forks (15 minutes)

```bash
dotnet run -c Release --project sim/UnderGlass.Run -- --seeds 50 --days 112 --forks 3 > forks.txt
dotnet run -c Release --project sim/UnderGlass.Run -- --town pelican:60@1 --town-seeds --seeds 20 --days 112 > towns.txt
```

The variety line measures how different the runs are. For example, V1 is the share of runs that
tell the commonest named story (target 30% or less). `--forks 3` reruns each seed three times from day 28 with
a new seed, and V7 says how much of the rest changed: 92-93%, above its 25-60% target. Day 28 settles
little of the detail that follows, while the year's headline changes in about a third of forks. `--town-seeds` builds a new 60-person town for each run.

## 10. Historical decisions and queue (2026-10-08)

1. **C2 and C4 (thanks against gifts and treats):** local's plan is that Thanked stays the light reply,
   and each person's traits pick a gift or a treat for a bigger return. Yes, or change it?
2. **Sides:** friendships rise 38% with it on the review's set, past the 30% limit. Keep it as it
   is, or cut it back?

Earlier queue, with the answers recorded in the specs; the current roadmap supersedes this order:
- **Shipping 0d.6** (A1, A2, A6): every step on, including i and j; Pam and Penny argue; tone 0.003.
  It changes the shipped town and moves P3 and every pin built on it, so it gets its own PR.
- **The act-slice answers:** C1, C2, C4, C5, C7, C8, C10, C12, and C3 as a relative trivia budget.
- **B5:** the social pressure not to spread a scandal.
- **D1:** forgetting in the 60-person town at about 0.5% a day, then a sweep.
- **Batch 2:** the rest of b2-0, then its slices.

The full list of questions and your answers is on issue #46.

## Where things live

| What | Where |
|---|---|
| How to run it, and what each phase found | `sim/README.md` |
| The viewer | `sim/viewer/README.md`, `sim/viewer/index.html` |
| The game's design | `docs/under-glass/design.md` |
| The specs: 0c, the desire gate, 0d.6, the town, the act catalog, batch 2 | `docs/under-glass/specs/` |
| Research | `docs/under-glass/*-research.md`, `actions-and-twists.md` |
| Current work order | `docs/under-glass/roadmap.md` |
| Historical work queue and questions | issue #46 |
