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
`NewAt`), and their mood band (squashed mood below -0.2, above 0.2, or between). Lines that fit
are weighted, not filtered to the most specific: an archetype line counts 3, a matching
relationship or mood doubles the weight each, a plain line counts 1. The line the same speaker
last used for the same kind is left out when any other fits. The draw is keyed by the act id on
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

Without `--character-inspiration` only archetype-free lines are used. `speechCatalog` in the
recording names the library's schema, line count and provenance. Tests: `SpeechCatalogTests`
(validation, weighted deterministic choice, coverage of every kind done to a chosen person,
every archetype tag known to the pack), `SpeechSceneTests` (one line per targeted act, the
speaker's side only, the same log hash on and off, the recording table only when on) and the Node
viewer test `test-viewer-speech.cjs` (lines follow the clock, never touched for a future act, text
not markup).

## Next

The reply line is the obvious next piece: the other person's answer when an act completes, keyed
to the recorded outcome (returned, rebuffed, accepted, refused) rather than to a guess. After that,
scene tags (time of day, place kind) can narrow lines further once the library is large enough
that narrowing does not starve a kind.
