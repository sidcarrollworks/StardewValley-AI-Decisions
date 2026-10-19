# Fresh start: plan for the new repo's brief and rules

Draft for Codex and Astra to review (2026-10-19). Claude wrote it at Sid's request; Codex and Astra
do most of the work in the first weeks. This file is the plan only. It is not the brief or the rules.
It belongs in neither repo once the new one exists.

## Why

- **Sid's goal.** People in a small town who decide for themselves, through Laya, from what they
  know, feel and remember. You watch them from above and can see why each one did what they did.
  The Stardew cast and assets are used for a private visual test later. The Stardew mod is no
  longer the base.
- **What went wrong here.** The repo grew by adding things and never removing them:
  - 32,500 lines of the mod;
  - 34,700 lines of Markdown, more than the 18,100 lines of engine, plus 2.7 MB of JSON in `docs/`;
  - 67 on/off switches;
  - 43 pinned run hashes;
  - 54 decision entries and 51 specs.

  Agents start every session from the mod's rules. Authored weights decide almost everything, and
  Laya is the add-on. There is no single thing to watch or to show to someone.
- **What doesn't change.** "Deterministic" means a recorded run replays exactly from its seed and its
  model tape. It never means our weights decide instead of Laya.

## Decisions Sid needs to make first (each with a default)

| # | Question | Default |
|---|---|---|
| 1 | Name and visibility of the new repo | `under-glass`, **private**. The current repo is public and holds data extracted from Stardew (`fixtures/game/`, quoted dialogue in the character and history packs). The new one must not repeat that in public. |
| 2 | Language and engine for milestone 1 | The simulation stays C# on .NET 8, harvested from this repo, plus one browser page to watch it. Godot with Stardew assets comes after milestone 1. |
| 3 | Cast for milestone 1 | Penny, Pam and Gus, plus 3–5 people they meet. Grow to the whole cast only after the demo works. |
| 4 | Who leads | Codex or Astra builds. Claude reviews PRs and writes no code until milestone 1 lands, unless asked. |
| 5 | What happens to this repo | Merge or close the open PRs (#108, #113, the rest), then archive it read-only as a reference. No new features here. |

## The brief (`BRIEF.md`, at most one page)

The only required reading. Proposed outline; Codex drafts the text.

1. **What Under Glass is**, in three sentences that anyone can follow.
2. **The current milestone.** What a viewer will see, and the test for "done".
3. **How a person decides.**
   - The engine works out what is physically possible and what the person knows.
   - Laya gets that person's known scene and the legal options, and returns weights.
   - The engine draws once, then carries out what was chosen and its consequences.
   - The authored fallback is used only when the model fails, and the run records that it was.
4. **Principles**, at most six:
   - The world goes on without the player.
   - What someone knows depends on what they saw, heard or were told.
   - Feeling follows the believed cause (Spinoza, adapted).
   - Every consequence can point to its cause.
   - Imagination never becomes evidence.
   - Quiet, hostile or mistaken towns are valid outcomes.
5. **Out of scope for now:** player gameplay, needs bars, a generator role beyond occasional private
   thoughts, Jev and Kev, population scaling, a second viewer.

## The rules (`AGENTS.md`, at most about 80 lines)

1. **Read `BRIEF.md` first.** Read code before docs. Nothing else is required reading.
2. **Laya decides.**
   - Every meaningful choice is a typed question: the holder's known scene and legal alternatives,
     including refusal, "not now" and staying put.
   - The engine owns bodies, eligibility, perception, travel, execution and consequences.
   - A choice is asked again only at arrivals, encounters, interruptions, completions or real changes
     the holder knows about. Never every tick.
3. **Knowledge.**
   - A request holds only what that person knows at that minute: never another person's mind, and
     never live positions.
   - One test per decision consumer proves this.
4. **Reproducibility.**
   - Engine draws come from seeded `Rng` with stable keys, and people are processed in name order.
   - Every model request and answer is written to the tape.
   - Replaying from the tape is exact, and a tape that doesn't match the run is refused.
   - Have one replay test per consumer, and only a few pinned hashes.
5. **Scope.**
   - No feature stays behind a switch for longer than one milestone: it becomes part of the game or
     it gets deleted.
   - No comparison towns or experiment modes unless the current milestone needs them.
6. **Docs budget.**
   - The only permanent docs are `BRIEF.md`, `AGENTS.md`, `DECISIONS.md` and one README for the engine.
   - A `DECISIONS.md` entry records a real fork Sid chose, in at most ten lines.
   - An experiment note is written only when it changes a decision.
   - No recording contract until two different programs read the format.
7. **Visible progress.**
   - Each PR either shows something new in the viewer or improves a recorded decision.
   - Its description says what to open and where to look.
   - Keep PRs small, and stack them rather than letting them conflict.
8. **Stardew material.**
   - Never commit the game's files or data extracted from them.
   - Read them from the local install at run time. Assets are for private visual testing only.
   - Mark game facts that haven't been checked against the decompile as `VERIFY`.
9. **Working together.** One owner per area at a time, and nobody edits files another agent has in
   flight. Ask Sid only about product decisions, never about implementation details.

## Milestone 1: something you can show in two minutes

**"Watch a day in Penny's town."**

- **What runs:** about six people live one day. Laya decides where to spend free time, whom to look
  for, and whether to offer, accept, refuse or put off a game or an evening. There is no player.
- **What you see:** one browser page with the town drawn from above.
  - People move along the roads.
  - Click someone to see what they knew, what they were asked, the options, Laya's weights, the
    draw, and what actually happened.
  - The same run replays exactly from its tape.
- **Done when:** someone who has never seen the project watches it and can say why one person did
  one thing. Also, a run with Laya switched off still completes, and every fallback it used is marked.

## What to copy from this repo, and what to leave

**Copy, with its tests:**

| From here | Why |
|---|---|
| `sim/UnderGlass.Sim/Rng.cs`, the clock in `Calendar.cs` and `Model.cs`, places, roads and walking (`Town.cs`, `DefaultTown.cs`), bodies and sleep, `Perception.cs` | The physical world. Already tested and deterministic. |
| `sim/UnderGlass.Sim/Lives/` (`History.cs`, `Memory.cs`, `MemoryFrame.cs`, `Choices.cs`, `Decision.cs`) | Per-person memory, warm and wary associations, expectations, recall, and the decision boundary |
| PR #113's route pattern (`Simulation.RoutingDecisions.cs`, not yet merged) | Queue a detached question, answer it off the clock, recheck next minute, record it. This is the model for every consumer. |
| `sim/UnderGlass.Minds/` (`LivesLayaMind.cs`, `LivesDecisionTape.cs`, `ResilientReflectionMind.cs`), balanced evaluation | The Laya client, the tapes, failure handling and the label-order fix |
| `sidecar/` (Laya setup and evaluation scripts) | Running Laya locally on the GPU |
| `tools/ScheduleExtractor`, `src/NpcSchedules` (schedule parser and simulator), `sim/tools/export-stardew-dialogue.ps1`, `stardew-source-notes.md` | Routines and canon facts, read from the local install rather than committed |
| `sim/character-inspiration.json` (archetypes), PR #96's corrections | Reviewed character voices. Keep dialogue quotes out of the repo. |

**Leave behind**, readable in the archive:
- The Stardew mod (`src/`, `mod/`, `tests/`).
- The needs bars, the desire-gate variants and the 0d6 steps.
- The act catalog's switch slices. Acts come back as options in Laya's menus, one family at a time.
- The authority and scandal machinery, until a milestone needs it.
- Observer contracts 0.1 and 0.2.
- The seven viewers, and the run and history studios.
- The comparison towns, the pinned hashes, and the docs.

## Order of work

Each step is one PR in the new repo.

1. **Sid answers decisions 1–5.**
2. **Codex/Astra: the repo exists.** `BRIEF.md`, `AGENTS.md`, `DECISIONS.md` (seeded with this
   plan's choices), CI, and an empty engine project that builds. Sid reviews the brief.
3. **The world.** Harvest the clock, map, walking, bodies and perception for about six people, with
   their tests, and add a replay test.
4. **Decisions.** Harvest memory, the decision boundary, the Laya client and the tape. Free-time
   routes go through Laya, using PR #113's pattern. Replay is exact.
5. **The page.** One viewer: the map, moving people, and click-to-see decisions.
6. **Company and evenings** through the same boundary: seek, offer, answer and follow through.
7. **Milestone review with Sid.** Watch a day together. Decide the next milestone, and what to delete.

Claude reviews each PR for knowledge leaks, replay exactness and scope creep.

## Risks

- **Rebuilding everything you copied.** Copy the pieces in the table above, not the rules around them.
- **The old habits creep back.** Rules 5–7 exist for this. Sid can also check at each milestone review
  whether the docs and the switches have grown.
- **Laya is too slow.** Measure simulated minutes per real second at milestone 1 before adding people.
- **Losing behaviour that took tuning to get right** (gossip, feelings). It's still in the archive.
  Bring it back as a menu option when a milestone needs it.
