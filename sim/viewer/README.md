# The Under Glass viewer

A page for watching one run of the town: everyone moving through the day, each person's mood,
stance and acts, who regards whom, how each story spread, and the event log, all on one clock.
It only reads a recorded run; it never runs the simulation or changes it.

## Make a run and open it

```bash
# The viewer with the run inside it: one file to open in any browser.
dotnet run -c Release --project sim/UnderGlass.Replay -- --seed 1 --days 56 --html run1.html

# Or the run alone, for the viewer's "Open a run…" button (or drop the file on the page).
dotnet run -c Release --project sim/UnderGlass.Replay -- --seed 1 --days 56 --out run1.json
```

Open `sim/viewer/index.html` directly to load a `.json` run. The tool takes the runner's settings:
`--inject` (place a scandal), `--fo <Name>=<value>`, `--desire off|observe|on`, `--tensions <depth>`,
`--trait <Name>=<Trait>:<value>` and `--feel off|observe|on`. Two months of the town is about 2.3 MB
and takes a few seconds to record; a year is about 5 MB.

## What it shows

- **The timeline** across the top, under the run's name and its clock: a ruler of seasons, weeks
  and the run's turning points (feuds, friendships, reconciliations, scandals) over a fader, with
  the playback keys under it. Click the ruler to jump. Space plays; arrow keys step 5 minutes
  (Shift: an hour); `[` and `]` step a day.
- **Town:** every place as a small map, grouped and largest first, all at one scale. The square,
  where the town gathers, spans the whole width, with who is there beside it. Each plate is as
  wide as its map, so nothing shifts as people come and go. A dot is a person, coloured by mood
  (dark grey even, blue in good spirits, red low; hollow when asleep). A ring marks an act as it
  happens (blue kind, red hostile), with a line to the other party. A gathering spot shows as a
  dashed circle while it is on. The names under each map are who is there; click one to follow
  them. Each home lists who lives there.
- **Relationships:** the town as a ring, household by household, with a line for each strong tie
  (blue liking, red dislike, dashed when the two feel differently); switch to "change since the
  start" to see who has grown closer or further apart. The grid shows every person's regard for
  every other. Click a pair for both sides over the run and what moved it.
- **Stories:** each act, how far it spread and how fast, how each person first came to know it (saw
  it, found a trace, or heard it from someone, with how many people each went on to tell), what
  each person believes now (and who has the wrong name), and what the mayor and the constable did.
- **The inspector** (right): the selected person's character at the start and the end, mood, power
  of acting and stance over the run, who they like and dislike and who likes them, their recent acts,
  the motives they weighed, and their life record. With nobody selected, the town at this moment.
- **The log**, synced to the clock and filterable (acts, feelings, motives, ties, the authority,
  gossip, sleep and work, money), with a search box and a switch to show only the selected person.

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
- Settings that are not finite numbers (a threshold of `Infinity` to turn something off) are
  written as the strings `"Infinity"`, `"-Infinity"` and `"NaN"`.
