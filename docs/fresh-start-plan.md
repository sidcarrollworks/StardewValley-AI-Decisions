# Fresh start: plan for the new repo's brief and rules

Draft for Codex and Astra to review (2026-10-19). Claude wrote it at Sid's request; Codex and Astra
do most of the work in the first weeks. This file is the plan only. It is not the brief or the rules.
Revised after Sid approved a selective extraction: preserve the full history-to-learning loop and
history editing/run creation in milestone 1. This temporary plan becomes reference material once
the approved choices have been carried into the new brief and rules.

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
- **What doesn't change.** Personal knowledge boundaries, provenance, conflicting feelings, outcome
  learning and reproducible execution are foundations to preserve. Extract and reconnect them;
  do not rebuild them merely to make the repository new.
- **Reproducibility.** A run replays exactly with its frozen inputs, compatible engine revision,
  seed and model tape. It never means our weights decide instead of Laya.

## Sid's decisions (2026-10-19)

| # | Question | Answer |
|---|---|---|
| 1 | Name and visibility of the new repo | `under-glass`, **private**. The current repo is public and holds data extracted from Stardew (`fixtures/game/`, quoted dialogue in the character and history packs). The new one must not repeat that in public. |
| 2 | Language and engine for milestone 1 | **No game engine for now** (Godot is dropped). Sid asked whether Rust would be better; **open until Sid confirms.** Claude recommends C# on .NET 8. This plan extracts tested code rather than rewriting it, and Rust would turn every extraction into a rewrite of about 20,000 lines with their tests. The engine is also not the bottleneck: Laya's latency per question is. Rust would pay off for a browser (WebAssembly) or Bevy build later, which nothing here needs yet. Codex, as the builder, should weigh in. |
| 3 | Cast for milestone 1 | **Ten people:** Penny, Pam, Gus, Pierre, Caroline, Robin, Demetrius, Leah, Elliott and Maru (the game's spellings). Cast size and identity are data, not engine special cases. Adding another authored character must not require code. Population optimization follows the demo. |
| 4 | Who leads | **Codex builds; Claude reviews.** |
| 5 | What happens to this repo | **Leave it alone; treat it as archived now.** No new work here. It stays readable for extraction. |

## The brief (`BRIEF.md`, at most one page)

The only required reading. Proposed outline; Codex drafts the text.

1. **What Under Glass is**, in three sentences that anyone can follow.
2. **The current milestone.** What a viewer will see, and the test for "done".
3. **How a person decides.**
   - The engine works out what is physically possible and what the person knows.
   - Laya gets that person's known scene and the legal options, and returns weights.
   - The engine draws once, then carries out what was chosen and its consequences.
   - Each person perceives the outcome, learns from their own account and can recall it at a later choice.
   - In normal runs, authored fallback handles model failure and is recorded. An explicit offline
     fallback check remains available; it is never presented as a Laya run.
4. **Principles**, at most six:
   - The world goes on without the player.
   - What someone knows depends on what they saw, heard or were told.
   - Feeling follows the believed cause (Spinoza, adapted).
   - Every consequence can point to its cause.
   - Imagination never becomes evidence.
   - Quiet, hostile or mistaken towns are valid outcomes.
5. **Out of scope for now:** player gameplay, needs bars, a generator role beyond occasional private
   thoughts, Jev and Kev, population optimization, a second viewer. A data-defined cast and the
   ability to edit histories and start runs are in scope now.

## The rules (`AGENTS.md`, at most about 80 lines)

1. **Read `BRIEF.md` first.** Read code before docs. Nothing else is required reading.
2. **Laya decides.**
   - Every meaningful choice is a typed question: the holder's known scene and legal alternatives,
     including refusal, "not now" and staying put.
   - The engine owns bodies, eligibility, perception, travel, execution and consequences.
   - Separate physical impossibility from preferences: habit, fear or a low authored weight must
     not silently remove a legal choice. Record exclusions and any menu reduction.
   - Define and check how legal menus fit the model's option/context limits. Oversized menus
     cannot silently use routine maths while being counted as Laya coverage.
   - A choice is asked again only at arrivals, encounters, interruptions, completions or real changes
     the holder knows about. Never every tick.
3. **Knowledge.**
   - A request holds only what that person knows at that minute: their own state and perceived
     evidence, never another person's mind or unobserved remote positions.
   - One test per decision consumer proves this.
4. **Reproducibility.**
   - Engine draws come from seeded `Rng` with stable keys, and people are processed in name order.
   - Every model request and answer is written to the tape.
   - Each run freezes cast, histories, archetypes, routines, map, configuration and seed in a
     versioned manifest with the engine revision. Subsequent edits never change an old run.
   - Replaying from the tape is exact, and a tape that doesn't match the run is refused.
   - Have one replay test per consumer, and only a few pinned hashes.
5. **Scope.**
   - Temporary feature switches expire at a milestone: adopt the behaviour or delete it. Run inputs,
     explicit fallback checks and recording controls are not temporary feature switches.
   - No comparison towns or experiment modes unless the current milestone needs them.
6. **Docs budget.**
   - The only permanent docs are `BRIEF.md`, `AGENTS.md`, `DECISIONS.md` and one README for the engine.
   - A `DECISIONS.md` entry records a real fork Sid chose, in at most ten lines.
   - An experiment note is written only when it changes a decision.
   - Keep a compact versioned recording shape beside the engine types, with a readable fixture
     and validation for the viewer. Document it in the engine README; do not copy the large
     observer contracts. The engine and browser already need an agreed format in milestone 1.
7. **Visible progress.**
   - Each feature PR shows a usable increment in the shared workflow or improves a recorded decision.
     Necessary build, correctness and security work is allowed without manufacturing a visual feature.
   - Its description says what to open and where to look.
   - Keep PRs small, and stack them rather than letting them conflict.
8. **Stardew material.**
   - Never commit the game's files or data extracted from them.
   - Read them from the local install at run time. Assets are for private visual testing only.
   - Mark game facts that haven't been checked against the decompile as `VERIFY`.
9. **Working together.** One owner per area at a time, and nobody edits files another agent has in
   flight. Ask Sid only about product decisions, never about implementation details.

## Milestone 1: something you can show in two minutes

**"Watch a life change through experience."**

- **What runs:** the ten people of decision 3, with authored starting histories, live long enough to return to
  a meaningful choice after an outcome. The demo takes two minutes to understand; the recording
  may span several days. Laya decides where to spend free time, whom to look for, and whether to
  offer, accept, refuse or put off a game or an evening. There is no player.
- **The required loop:** starting history → recalled context → choice → action → personally
  perceived outcome → changed feeling or expectation → recall at a later choice. Preserve
  conflicting feelings and provenance. A refusal, disappointment or unsuccessful attempt can
  complete the loop; no successful game or changed final action is required.
- **What Sid can do:** in the UI, edit or import starting histories, save/export a pack, choose
  participants, start a new run and inspect its frozen inputs. Show validation errors before launch.
  Experimental edits remain labelled experimental; editing never silently grants canon/reviewed
  status. Histories belong to people, not reusable archetypes. Add another character and their
  history through data and include them in a run without changing engine code.
- **What you see:** one browser page with the town drawn from above.
  - People move along the roads.
  - Time and event markers share one timeline. Selecting a person/event reveals its relevant
    history, choice, outcome and later learning; technical receipts open on demand.
  - Distinguish chosen, attempted, observed and learned. Weights and supplied context show recorded
    inputs and selection, not proof of the model's internal reasoning.
  - Reuse the useful viewer interactions and shared styles; consolidate the workflow rather than
    rebuilding every editor or displaying every memory at once.
- **Done when all of these hold:**
  1. Sid can author a history, select its character and start/watch a run entirely through the UI.
  2. An actual recording exposes the full loop above, including learned evidence reaching a later
     decision request. Someone new can follow that thread without reading a raw log.
  3. A controlled check removes one relevant history and its derived contributions while holding
     scene, options, unrelated evidence and keyed draw fixed. Record both model requests and answers
     and the change, if any, in weights
     and selection. Verify the intended evidence reaches the model, not just the memory store.
     Assess whether the history affects the decision process; do not require a specific action,
     claim sensitivity from one changed sample, or hide a no-effect result. Keep this check focused,
     not a new comparison-town framework.
  4. Frozen inputs and the tape replay exactly; editing today's history cannot alter yesterday's run.
  5. A provider-unavailable/offline check completes with every fallback marked. Normal runs expose
     evaluated choices, calls, failures and exclusions so missing coverage cannot look like success.

## What to copy from this repo, and what to leave

**Copy, with its tests:**

These are sources to extract from, not independent drop-in files. Trace dependencies and preserve
the perception → feeling → learning connections without importing unrelated simulation systems.

| From here | Why |
|---|---|
| `sim/UnderGlass.Sim/Rng.cs`, the clock in `Calendar.cs` and `Model.cs`, places, roads and walking (`Town.cs`, `DefaultTown.cs`), bodies and sleep, `Perception.cs` | The physical world. Already tested and deterministic. |
| `sim/UnderGlass.Sim/Lives/` (`History.cs`, `Memory.cs`, `MemoryFrame.cs`, `Choices.cs`, `Decision.cs`) | Per-person memory, warm and wary associations, expectations, recall, and the decision boundary; audit authored ranking and eligibility when extracting |
| `Simulation.Lives.cs`, company/evening outcome and learning adapters, with their tests | Preserve the connections from perceived outcomes into personal learning and later recall; copying memory storage alone does not preserve the loop |
| PR #113's route execution pattern (`Simulation.RoutingDecisions.cs`) | Queue a detached question, answer between simulated minutes, recheck next minute, record selection separately from execution. Reuse this boundary, not its inherited preference gates, zero-weight exclusions or five-option bypass. |
| `sim/UnderGlass.Minds/LivesLayaMind.cs`, `LivesDecisionTape.cs`; reflection adapter as a reference | Typed transport, validated answers, tapes and failure handling. Lives currently makes one request; balanced label/order evaluation exists in `ResilientReflectionMind.cs` for reflection only. Copying both does not balance ordinary decisions. Choose explicitly whether to port that mechanism, accounting for extra calls. |
| Run/history authoring and viewer components in `sim/viewer/`, required local run-service pieces | Reuse validated history editing/import/export, run creation, shared styles, timeline and causal inspection within one workflow; retain only what milestone 1 needs |
| `sidecar/` (Laya setup and evaluation scripts) | Running Laya locally on the GPU |
| `tools/ScheduleExtractor`, `src/NpcSchedules` (schedule parser and simulator), `sim/tools/export-stardew-dialogue.ps1`, `stardew-source-notes.md` | Routines and canon facts, read from the local install rather than committed |
| `sim/character-inspiration.json` (archetypes), PR #96's corrections | Reviewed character voices. Keep dialogue quotes out of the repo. |

**Leave behind**, readable in the archive:
- The Stardew mod (`src/`, `mod/`, `tests/`).
- The needs bars, the desire-gate variants and the 0d6 steps.
- The act catalog's switch slices. Acts come back as options in Laya's menus, one family at a time.
- The authority and scandal machinery, until a milestone needs it.
- Observer contracts 0.1 and 0.2.
- The separate multi-page tool suite, after preserving the minimum authoring/start/watch workflow
  and useful viewer components above. Do not discard the user's ability to experiment.
- The comparison towns, the pinned hashes, and the docs.

## Order of work

Keep PRs small and demonstrable; split a step when needed. A usable author/start/watch workflow
must accompany the first Laya-enabled slice, not arrive after the simulation has been rebuilt.

1. **Sid answers decisions 1–5.**
2. **Codex/Astra: the repo exists.** `BRIEF.md`, `AGENTS.md`, `DECISIONS.md` (seeded with this
   plan's choices), CI, and an empty engine project that builds. Sid reviews the brief.
3. **The smallest runnable world and page.** Extract clock, map, walking, bodies and perception,
   with tests and a data-defined cast. Show movement in the reused map and start/replay a run from
   the page. Establish the frozen-input manifest and compact recording shape here.
4. **Authored lives and the first Laya choices.** Bring history editing/import/export and participant
   selection into the page alongside memory, recall, the typed client and tape. Route choices use
   PR #113's execution boundary with explicit menu-limit handling and preference/eligibility audit.
   Sid can edit a history, start a run and inspect what actually reached Laya. Replay is exact.
5. **Complete the loop.** Company and evenings: seek, offer, answer, follow through, perceive the
   outcome, learn and revisit a later choice. Connect each step to the same timeline and causal
   inspection. Preserve feelings/learning adapters as well as the memory types.
6. **Milestone acceptance.** Demonstrate all five checks above, including the focused history-removal
   check and adding another authored character without code. Measure simulated minutes per real
   second and model/fallback coverage. Broader model comparisons and population tuning wait.
7. **Milestone review with Sid.** Decide the
   next milestone and retire temporary switches; keep the minimum experimentation workflow working.

Claude reviews each PR for knowledge leaks, replay exactness and scope creep.

## Risks

- **Rebuilding everything you copied.** Copy the pieces in the table above, not the rules around them.
- **The old habits creep back.** Rules 5–7 exist for this. Sid can also check at each milestone review
  whether the docs and the switches have grown.
- **Laya is too slow.** Measure simulated minutes per real second at milestone 1 before adding people.
- **Losing the causal loop.** Feelings and outcome learning are required foundations, not optional
  activities to restore later. Preserve their connections and tests; broader gossip can wait.
- **Rebuilding infrastructure for weeks.** The old repo stays as it is, so its tools remain available for
  reference; bring the author/start/watch path forward with the first slice. Ten is starting data, not a cast limit.
