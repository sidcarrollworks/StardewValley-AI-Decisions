# The Under Glass viewer

A page for watching one run of the town: everyone moving through the day, each person's mood,
stance and acts, who regards whom, how each story spread, and the event log, all on one clock.
It only reads a recorded run; it never runs the simulation or changes it.

Prepared local recordings are linked from `http://127.0.0.1:8766/sim/viewer/morning.html`.
Run `sim/tools/start-preview.ps1 -Open` from this checkout to start or reuse its loopback preview.
The morning page compares authored, balanced Laya and experimental local-generation recordings;
opening them needs no running model. See [the simulator guide](../README.md) to record fresh runs.

## Make a run and open it

```bash
# The viewer with the run inside it: one file to open in any browser.
dotnet run -c Release --project sim/UnderGlass.Replay -- --seed 1 --days 56 --html run1.html

# Or the run alone, for the viewer's "Open a run…" button (or drop the file on the page).
dotnet run -c Release --project sim/UnderGlass.Replay -- --seed 1 --days 56 --out run1.json

# Quiet reflection: thoughts, actual candidate lines and their consequences in Inner life.
dotnet run -c Release --project sim/UnderGlass.Replay -- --seed 1 --days 7 --reflection authored --html reflection.html
```

Open `sim/viewer/index.html` directly to load a `.json` run. The tool takes the runner's settings:
`--inject` (place a scandal), `--fo <Name>=<value>`, `--desire off|observe|on`, `--tensions <depth>`,
`--trait <Name>=<Trait>:<value>`, `--feel off|observe|on`, `--catalog <slices>` (the act catalog's slices, as
the runner takes them: `--catalog returns,company,welcome,repair,late`) and `--0d6 <steps>` (hermits, brawlers, moods that spread
and missing people: `--0d6 bdefghm`, as the runner takes it). Two months of the town is about 2.3 MB
and takes a few seconds to record; a year is about 5 MB.

## What it shows

A switch at the top turns the dark look on and off; the choice is remembered in that browser, and
until one is made the page follows the host's theme, then the system's.

- **The whole town**, across the top like the picture of a video player: every place on one plan,
  everyone moving through it, a dashed circle where a gathering is on, a ring where an act happens
  (blue kind, red hostile; larger for a scandal), and an orange outline on each place where things
  have happened in the last two hours, fading as they age. The run gives no positions, so the plan
  is stitched from the doors: the town's hub (the place out of doors with the most doors, the
  square) in the middle, each place hung outside the door that leads to it, roads running across,
  streets joining the doors. A grown town (`--town pelican31`, `--town pelican:60@7`) is stitched
  the same way, its neighbourhoods round the core. Hover for who is
  where; click a person to follow them, or a place to go to its map. Scroll to zoom about the
  pointer (pinch on a touch screen), drag to pan; the keys in its corner, or `+`, `-` and `0`, zoom
  in, out and back to the whole town. Zoomed all the way out, scrolling down scrolls the page.
- **The timeline** under it: a ruler of seasons, weeks and the run's turning points (feuds,
  friendships, reconciliations, scandals) over a fader, then the clock and the playback keys. Click
  the ruler to jump. Space plays; arrow keys step 5 minutes (Shift: an hour); `[` and `]` step a day.
- **Happening now:** up to three active encounters, recent thoughts or waiting intentions,
  narrowed to the person or neighbourhood being followed. Follow a thought to its inner-life
  card. **Previous moment** and **Next moment** skip to recorded encounters, thoughts and tie
  changes; they show no future event content before jumping there. The inspector's **At this
  moment** describes the selected person's present act or intention.
- **Neighbourhood**, beside the tabs, for a grown town with neighbourhoods (`--town pelican:60@7`):
  All, the centre, or one neighbourhood. One choice narrows the town's maps, the population, the
  relationships (the ring, the matrix and the ties), the stories (by who did it) and the log (by
  who is in a line) to that neighbourhood's people and places. On the whole-town map, the rest of
  the town fades back. The inspector says where each person lives. A place belongs to the
  neighbourhood it hangs from, with its road, its shops and its homes (`TownMetrics.PlaceDistricts`);
  the replay records each person's and place's district.
- **Town:** every place as a small map, grouped and largest first, all at one scale. The square,
  where the town gathers (the place out of doors with the most doors), spans the whole width, with
  who is there beside it. Each plate is as
  wide as its map, so nothing shifts as people come and go. A dot is a person, coloured by mood
  (dark grey even, blue in good spirits, red low; hollow when asleep). A ring marks an act as it
  happens (blue kind, red hostile), with a line to the other party. A gathering spot shows as a
  dashed circle while it is on. The names under each map are who is there; click one to follow
  them. Each home lists who lives there.
- **Population:** everyone in one table, at the clock: where they are, their character (a small bar
  per trait), mood, power of acting, stance with its line over the whole run, their mean regard for
  the town and the town's for them, their acts so far (kind, other, hostile), the stories they know
  and have told, and their feuds and friendships. Above it, who is lowest and highest on each
  measure. Sort by any column or trait; click a row to follow that person. Someone in a sustained spell at the
  clock (28 nights or more withdrawn, a hermit or a brawler; see the inspector) is tagged in the stance column.
- **Inner life:** follow a character's remembered moment into a thought, the line they chose,
  whether they accepted, changed, deferred or rejected the idea, and the attempt and outcome when
  recorded. A card appears when reflection begins; its thought, selected line, weights and model
  receipts appear only when the answer is considered. Later events appear as the clock reaches
  them, and disappear when rewinding. Buttons jump to the remembered act and subsequent acts on
  the same clock. The character selector shares the map and inspector's selection; neighbourhood
  and unfolding/settled filters narrow the list. It shows at most 24 recently changed cards.
  **Dream on waking** marks private imagination from a real sleep interval; it is separate from
  the remembered encounter. **Revisited** links a later thought to its earlier deferred idea.
  Outcomes use recorded life responses and appear only after those responses were resolved.
  Expand **What informed this choice?** for the supplied context, actual candidate lines, relative
  weights, proposal, selected response, recorded backend/fallback note and exact model requests
  when present. These are recorded evidence, not explanations invented by the viewer. Model
  content is displayed as plain text. `--reflection authored` uses the explicit offline baseline;
  `--reflection laya` uses the model evaluator. Older runs need no migration and show an empty
  Inner life view with instructions for recording it.
- **Relationships:** the town as a ring, household by household, with a line for each strong tie
  (blue liking, red dislike, dashed when the two feel differently); switch to "change since the
  start" to see who has grown closer or further apart. The grid shows every person's regard for
  every other; in a bigger town its cells shrink to fit the column (at 60 people, 8 pixels), and the
  ring's names get smaller. Click a pair for both sides over the run and what moved it.
- **Stories:** each act, how far it spread and how fast, how each person first came to know it (saw
  it, found a trace, or heard it from someone, with how many people each went on to tell), what
  each person believes now (and who has the wrong name), and what the mayor and the constable did.
  These details follow the clock by default. **Whole-run analysis** explicitly includes future
  events; jumping to a remembered encounter turns that analysis off again.
- **The inspector** (right): the selected person's character at the start and the end, mood, power
  of acting and stance over the run, who they like and dislike and who likes them, their recent acts,
  the motives they weighed, and their life record. While the gate runs, the stance line shades each
  sustained spell (blue: withdrawn, or a hermit if their free hours out fell too; red: a brawler), a
  second line shows how left out they were each night, and the spells are listed with their dates; click
  one to go to its start. With nobody selected, the town at this moment, with anyone in a spell.
- **The log**, synced to the clock and filterable (acts, feelings, motives, ties, the authority,
  gossip, sleep and work, money), with a search box and a switch to show only the selected person.
- **Explanation:** what the words and numbers mean, as the simulator uses them: the seven traits,
  how a person is now (mood, power, stance, regard), stories and gossip, ties, motives and the
  gate, the mayor and the constable, the life record, money, withdrawal; then this run's act kinds
  with their numbers, what an act's fields mean, every setting with this run's value (in orange
  where it differs from the shipped town's, with the town's value beside it), and the log's lines.
  A search box finds a word or setting anywhere in it.

### Where the definitions come from

The definitions live in the page itself, in the `glossary` block (one item per line): 300
settings, 145 terms and 96 acts, act fields and log lines. Each was written from the code by one
agent and checked against it, line by line, by another (2026-10-08); each names the file and line
it was read from (`source`), with a piece of that line (`at`). An item marked `verify` would show a
"verify" tag; none is. Settings are keyed as the file records them: the feelings' bare (`LoveAt`), the
others with their class (`GossipOptions.ChatChance`). `GlossaryTests` check that every setting the
glossary defines exists in the code, and list (without failing) any recorded setting that has no
definition yet, so a new setting can land before its definition. Numbers quoted in a definition are
the code's at that date: when a constant is tuned, its definition is changed in the same PR.

**Source lines drift** as code is added above them: on 2026-10-08, 68 of the 532 pointed at other
code than they were read from, because branches that added lines above them were merged. So each now
carries its `at`, and `GlossaryTests` fail when the line holding it is more than 30 lines from the
pointer. To move every pointer back to its `at`, run that test with `UNDERGLASS_REPOINT=1`:

```bash
UNDERGLASS_REPOINT=1 dotnet test sim/UnderGlass.sln --filter EverySourceLineIsNearWhatItWasReadFrom
```

Each checkpoint does this after merging.

`ViewerPageTests` read the page as text, for what needs no browser: the whole-town map clears its
canvas before each frame, since its plan is clear between places and a zoom or a pan would otherwise
leave the earlier frames showing there.
`ReflectionViewerTests` protect the optional-recording boundary, clock guards and plain-text
rendering of model content. Browser verification checks the displayed chain and timeline jumps.

## The file

`UnderGlass.Sim/Replay.cs` writes it (format `under-glass-run`, version 1); `ReplayTests` check it
against the run. People and places are indexes into `names` and `places`, and the acts' kinds into
`kinds`; the gate's weighings (`pursuits`), the life record (`life`) and each person's own acts
name their act kinds instead. Times, ties included, are game minutes from midnight of day 0.

- **Movements** are packed per person (base64): the ticks since the last move as a varint (7 bits a
  byte, low first), then a byte of place (plus 128 when asleep), then x and y. A row is written only
  when place, tile or sleep changes.
- **Mood and power** are sampled at minute 59 of each hour, in hundredths. **Regard** is a matrix at
  23:59 of each day, in thousandths, and `baseline` the matrix at the start. **Stance** is one value
  per person per night.
- **`feelings`** are the regard changes between two people with their cause:
  `[tick, holder, toward, act, route, change in thousandths]`. Changes toward a kind of person (the
  Kind and Spill routes) are in `kindFeelings`, with an index into `personKinds` in place of
  `toward`. Regard also drifts each night with no cause logged, so the day's changes don't add up to
  the next night's matrix exactly.
- **`events`** are the log lines other than beliefs and tellings, which have tables of their own.
  With the gate only watching (`--desire observe`), its lines are added in time order.
- **`minutesOut`** is each person's minutes awake, away from home and not at work, by season.
- **`reflections`** is optional, absent when reflection is disabled. Unlike the packed tables,
  its records use named camelCase fields and actor/subject names. Each contains `request` (id,
  tick, actor, subject, sourceActId, memory, context and choices with id/kind/line, plus optional
  structured source, catalog proposal, continuity links and sleep opportunity), `answer`
  (thought, suggestedChoice, weights, backend, note, exact generation request/raw response and
  evaluator passes with prompts, raw responses, label mappings and normalized weights/errors), the
  selected `choice`, and timestamped `events` (tick, status, text, actId). The viewer never
  infers an outcome from the end of the recording; an act without an outcome remains unresolved.
- **`withdrawal`** (0d.6; null when the gate is off): `leftOut`, each person's being left out (E) at the
  end of each day, in hundredths; `spells`, every sustained spell as `WithdrawalMetrics` finds it
  (`person`, `from` and `to` as the nights' stance indexes, `kind` hermit, withdrawn or brawler,
  `ended` false if it lasted to the run's end, and `hoursFall`, how far their free hours out fell);
  `contagion`, the mood each person passed on, caught and took on balance; and `rules`, what each 0d.6
  rule did (count and sum; with `WithdrawalWatch`, what it would have done).
- **`settings`** are every switch and number the run used: the feelings' by name, every other
  options class's with the class in front (`GossipOptions.ChatChance`, `BodyOptions.CommuteMinutes`,
  `TownData.Wander`), and how it was set up (`Inject`, `Tensions`, `Traits`). **`defaults`** are the
  same switches and numbers for the shipped town, so the viewer can show which differ.
- Settings that are not finite numbers (a threshold of `Infinity` to turn something off) are
  written as the strings `"Infinity"`, `"-Infinity"` and `"NaN"`.
