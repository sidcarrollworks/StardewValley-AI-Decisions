# Spoken lines for acts: the speech library

## Status

Built as an opt-in presentation slice, 2026-10-12, the second step of the roadmap's agreed order.
With `--speech` on a replay, every act an actor does to a chosen person gets one authored line,
chosen deterministically from the embedded `sim/UnderGlass.Sim/speech-catalog.json` and recorded
beside the act. Off by default: the pinned runs are byte-identical with it off, and with it on
every decision, feeling and log line is identical, because nothing in the simulation reads a line.

The first library was drafted the way the roadmap prescribes for authored content: one Claude
author per archetype wrote new lines with the local dialogue export as style evidence only, a
generic author wrote plain lines, every line was copy-checked against the whole corpus, and three
readers judged each line blind for which voice it sounded like and for rule breaks. Lines the
readers could not place kept their words but lost their archetype tag; lines heard as another
voice or breaking a rule were dropped. The result is awaiting Sid's curation; editing the file and
rebuilding is the whole workflow. Nothing in it is game text.

Coverage, 2026-10-12, the same day: the first library made people repeat themselves because the
relationship and mood tags ruled lines out, so a given speaker in a given state could choose from a
median of three lines while the selector avoided their last three. A tag now names where a line
belongs best rather than the only place it may be said: a line written for the neighbouring band
still fits, at a quarter of the weight of an exact match. On the same seven-day week (seed 7,
authored thoughts, profiles on, the same log hash) the 196 spoken lines went from 60 distinct to
80, and the same speaker saying the same line twice running for one kind fell from 27 times to 12.
`sim/viewer/speech.html` is the curation page: a grid of act kinds by voices showing how many lines
fit each situation, with a recording's actual use on top, and every line editable in place.

Not built: a reply line for the other person, lines for acts without a chosen target, lines for
turning away (`Snubbed`, `TurnedAway` are silent by design), lines that mention an item, a task or
a place (the simulation does not know them), and any effect of speech on the world.

## The contract

`speech-catalog.json` is schema 1 with a provenance string and `lines`. Each line has an `id`
(unique), a `kind` (an act kind name), `archetypes` (archetype ids from the character pack; empty
means any speaker), `relationship` (a subset of household, stranger, acquaintance, familiar, close;
empty means any), `mood` (a subset of low, even, high; empty means any), the `text` (12 to 110
characters, the only placeholder `{subject}`, no `{ } $ # % @`, no control characters, no leading
or trailing space) and descriptive `tags`. The loader rejects anything else and sorts lines by id so
file order never matters.

When an act toward a person begins, `Simulation.Say` reads the speaker's side only: their archetype
from the configured character pack (none without a pack), how well they know the other person
(the same bands the reflection scene uses: household, or familiarity against the catalog's
`NewAt`), and their mood band (squashed mood below -0.2, above 0.2, or between).
`SpeechCatalog.Weight` says how well each line of the kind fits, 0 when it does not:

- a line tagged with another archetype never fits; an archetype line in the speaker's own voice
  counts 3, a line for any speaker 1;
- a relationship or mood tag that names the speaker's band doubles the weight; one that only names
  a neighbouring band halves it; one that names neither rules the line out; an empty tag counts 1;
- the neighbours are one rung of familiarity (stranger, acquaintance, familiar, close) and one mood
  band (low, even, high). A speaker talking to someone in their household may borrow a close line,
  but a household line never leaves the household: sibling quarrels and chores are not said to a
  friend. A low line is never said in high spirits.

Every line with a positive weight is drawn in proportion to it. The last three lines the same
speaker used for the same kind are left out when any other fits. The draw is keyed by the act id on
speech's own stream, so it moves no other system's dice. The record is
`Speech(ActId, Tick, Speaker, Line, LineId, Archetype, Relationship, Mood)`.

A line is a piece of presentation with provenance: the observer can see which library id spoke and
under which bands. The viewer shows it under the act in Stories, in the act's detail ("Said"), in
the timeline hover, in Happening now when no reflection line takes precedence, and in the selected
person's "At this moment", each gated by the same clock as the act. Older recordings have no table
and show nothing; the viewer never invents a line.

## Curation rules

- New wording only. A line is dropped if its normalised text equals, contains or is contained in
  any 20-character-or-longer line of the local dialogue corpus; the check is a reading aid, and
  Sid's review still decides.
- No catchphrases, no quirks repeated across a voice's lines, no invented misspellings or baby talk
  for children, no counseling language, no narration of feelings or traits.
- A line tagged with an archetype must be placeable: the blind readers' majority had to name that
  archetype. Otherwise it is kept as an archetype-free line ("demoted") so its words still serve.
- Hostile kinds are actually unkind, without slurs or threats.
- Nothing names an item, a task or a place.

## Run and inspect

```bash
dotnet run -c Release --project sim/UnderGlass.Replay -- --seed 7 --days 7 --feel on --desire on --catalog returns,company,welcome,repair,sides,late --speech --reflection authored --character-inspiration sim/character-inspiration.json --out speech.json --html speech.html
```

Without `--character-inspiration` only archetype-free lines are used.

To curate, run `sim/tools/start-preview.ps1` and open `http://127.0.0.1:8766/sim/viewer/speech.html`
(add `?run=` and the path of a recording made with `--speech` to see what was said). Each cell is an
act kind and a voice; its squares are the fifteen relationship and mood situations, coloured by how
many lines fit, and situations an act can never be spoken in (a welcome is always said to a
stranger) are faint and left out of every count. Click a cell to read its lines with each one's
chance of being picked, edit the words and tags, add or remove lines, and download the catalog.
Edits are kept in that browser until downloaded or discarded; the page refuses to download a catalog
the loader would reject. `speechCatalog` in the
recording names the library's schema, line count and provenance. Tests: `SpeechCatalogTests`
(validation, weighted deterministic choice, coverage of every kind done to a chosen person,
every archetype tag known to the pack, neighbouring bands), `SpeechSceneTests` (one line per targeted act, the
speaker's side only, the same log hash on and off, the recording table only when on) and the Node
viewer test `test-viewer-speech.cjs` (lines follow the clock, never touched for a future act, text
not markup). `sim/tools/fixtures/speech-weights.json` pins the weight rule on both sides: the C#
test and `test-speech-page.cjs` (the page's copy of the rule, its validation, and an export that
reproduces the stored file) both read it.

## Next

The busiest speaker in the recorded week is the Newcomer, who has no archetype and so draws only on
plain lines (57 of 196 lines). Worse, a welcome is keyed on familiarity, not on who arrived: the
newcomer, knowing nobody, does most of the welcoming, and every Welcomed line is written from the
resident's side ("Welcome to town, Penny"). Sid decided (2026-10-12) that the Newcomer gets a voice
of its own: the player's, typed and read by Laya ([player speech spec](player-speech-spec.md)).
Until a player exists, a reserved `newcomer` voice with arrival-side lines is planned there.

The reply line is the obvious next piece: the other person's answer when an act completes, keyed
to the recorded outcome (returned, rebuffed, accepted, refused) rather than to a guess. After that,
scene tags (time of day, place kind) can narrow lines further once the library is large enough
that narrowing does not starve a kind.
