# Lives: history-informed free-time routing

## Status and scope

Built 2026-10-15 as the first live consumer of the
[personal-memory foundation](lives-memory-spec.md). This increment connects perceived feelings,
recall, and competing **existing free-time destinations**. It also records isolated choices with
and without specified starting history. It is opt-in and does not change law 1 or enable needs bars.

The [Lives assessment](../lives-gap-assessment.md), especially section 9, remains the broader pilot
design. This increment does **not** complete the Penny–Pam–Gus thread. It adds no company-seeking
destination, plan, invitation response, pending ask, shared activity, outcome classifier, reflection
trigger, or Laya choice call. It does not alter the existing invitation, errand, commitment, or
reflection rules. Those systems can run under their own existing switches; enabling routing does
not enable them automatically.

There is no shipped, reviewed canon history pack. The integration tests use fictional histories
with explicitly synthetic source standing to exercise admission and causation. A test source called
`Canon` is not evidence about a Stardew character. The illustrative observer fixtures and the
background anchors in `character-inspiration.json` are not loaded as runtime histories.

The offline [`UnderGlass.LivesTrial`](../../../sim/UnderGlass.LivesTrial/Program.cs) generates
actual synthetic-town recordings and a compact comparison page. The
[first measured run](../experiments/lives-routing-2026-10-15.md) also exercises a week of the
normal town with no starting history.

Implementation:

- [`Lives/Choices.cs`](../../../sim/UnderGlass.Sim/Lives/Choices.cs): deterministic choice evaluator
  and controlled history comparisons.
- [`Lives/MemoryFrame.cs`](../../../sim/UnderGlass.Sim/Lives/MemoryFrame.cs) and
  [`Lives/Memory.cs`](../../../sim/UnderGlass.Sim/Lives/Memory.cs): frozen queries, history removal,
  and corrected acquisitions.
- [`Lives/Routing.cs`](../../../sim/UnderGlass.Sim/Lives/Routing.cs) and
  [`Simulation.Lives.cs`](../../../sim/UnderGlass.Sim/Simulation.Lives.cs): live adapter and receipts.
- [`Simulation.cs`](../../../sim/UnderGlass.Sim/Simulation.cs),
  [`Simulation.Feelings.cs`](../../../sim/UnderGlass.Sim/Simulation.Feelings.cs),
  [`Replay.cs`](../../../sim/UnderGlass.Sim/Replay.cs), and
  [`UnderGlass.Replay/Program.cs`](../../../sim/UnderGlass.Replay/Program.cs): integration points.

## What actually changes in a run

`ConfigureLivesRouting(options, history)` configures the simulation before it runs. `Enabled`
defaults to false. When enabled without a history pack, every character begins with empty Lives
memory and can learn from the existing feelings pipeline. A supplied pack is validated and its
account holders must belong to the cast. Each character imports only their own accounts and readings.
No named-character decision branch is added.
History fact, account and reading IDs cannot start with `act:`, which this adapter reserves for live
event causes; a collision is rejected before any personal memory is configured.

The existing movement rules still decide when a free-time choice is possible. Detention, bodily
tiredness, work, patrol and errands keep their existing priority. A person still occupied by a
current haunt or home visit does not get an extra choice merely because memory changed. A habitual
bedtime checkpoint remains the existing reason to reconsider that visit.

At an ordinary free-time choice, the adapter receives the already-built menu in its existing order:
open authored haunts, eligible gatherings, home when present, and the existing habitual-bed option
when offered. It preserves these options and their base weights. It does not add a remembered place,
discover a person remotely, replace the authored routines, or decide that an unavailable action
should become possible. A menu with more than five options, invalid weights, or no positive total
falls back to the original draw and records a skip when observation is enabled. It is never silently
truncated to five.

Each option names its destination as a `Place` object. Home and going to bed share the character's
home place for association purposes, while retaining distinct option identities and base weights.
Recall cues include the possible destination places, current season, weekday/weekend, and the
holder's own person ID. They do not include another character's actual position or private memory.

If nothing relevant is recalled, the simulation uses the exact original `haunt` draw and its
original weights. The receipt marks this as `Legacy`; the generic evaluator does not invent a
replacement selection for it. With a nonempty recall, the new evaluator chooses among the eligible
alternatives and the selected destination goes through the existing movement and stay-duration code.
This changes a destination choice, not the definition or outcome of an encounter there.

## Feelings enter memory through the holder's knowledge

The live writer reads the existing `Feel` and `Undergo` results. It does not read observer
`appraisals`, infer pleasure from visit counts, or manufacture a positive outcome because an activity
was available. The first acquired feeling can affect the next relevant destination choice; it does
not wait for several agreeing experiences.

One holder/act is one stable cause, `act:<id>`. Its already-felt signed amount projects to activity,
the believed person when known, and place when that holder perceived the location. These projections
retain one cause identity. `Sides()` counts the same experience once when an option includes more
than one of those objects. Associations do not feed back into mood or regard; their first live reader
is the destination choice.

- Directly undergone events are `Lived`. A witnessed event targeting the holder is also recorded as
  lived; other first-hand perception is `Witnessed`.
- A witnessed event can still have uncertain identity. `RoutingPerception` separately records
  identity confidence, clarity and the existing presence basis. The acquisition route is not a claim
  that the believed culprit is objectively correct.
- Told events keep the teller and credence. Their felt amount already includes the existing hearsay
  treatment and is not discounted again. They add no place association by reading the underlying
  act's hidden location. An incomplete told record with no teller is not imported.
- Found traces, self-caused readings, and `Refused` acts do not create this adapter's associations.
  An earlier imported feeling is retracted when a later reading invalidates it in these ways.
- A free mishap can be felt and associated with its activity and place without blaming another person.

The adapter synchronizes the final recorded feeling for a cause, rather than adding each mood delta
as a new experience. Duplicate readings are ignored. A later non-lived reading cannot overwrite a
retained lived reading. New knowledge can change the believed person, strength or provenance through
`PersonalMemory.Correct()`.

The existing feelings record stores cumulative affect, whereas the memory association fades.
On correction, the adapter carries forward the cause's retained, already-faded amount and adds only
the change in cumulative affect. A new attribution or clearer source therefore cannot rejuvenate an
old feeling. If all its projections have faded or been trimmed away, a correction with no new affect
writes zero-amount projections: these update the acquisition's attribution and dilution but create
no active association. A previously retracted cause has no retained felt reading; explicit
re-establishment starts from the newly admitted felt amount.

`Correct()` validates the whole replacement before modifying state, removes the prior retained
projections, reverses that acquisition's dilution once, and applies the revised projections under
the same cause. It emits one `correct` before/after receipt. A newer explicit correction may
re-establish a previously retracted cause without undoing dilution twice; an ordinary `Learn()` retry
still cannot resurrect it. Corrections do not restore unrelated entries previously evicted by the
memory bounds. Midnight advances the memory kernel's existing decay once per elapsed day.

The kernel supports expectations and learns from their first supplied outcome, but this live adapter
currently writes **associations only**. It supplies no `ExpectationInput` and has no classifier for
answers, availability, whereabouts, completed activities or follow-through. A failed search is not
silently interpreted as rejection. Generic destination options also supply no expectation keys, so
their chance term is 1 even if a history pack contains an expectation for another purpose.

## Comparing alternatives

The pure evaluator takes one frozen holder frame, an explicit opportunity, one to five options,
personal state and a random unit. It preserves attractions and reservations as separate reported
sides and uses the assessment's experimental weighting:

```text
weight = base × chance × exp(5 × (warm − cold × (1 − boldness) + intended − goingCost))
```

The implementation computes log weights and normalizes after subtracting the maximum to avoid
overflow. It samples a probability distribution rather than taking the highest-weight option.
The keyed random input is `Rng.Unit(seed, "lives-choice", opportunity.Id)`. Generic live routes use
the character's current boldness, `Mood < 0` for the extra warm recall, and zero intended/going cost.
The general API can accept those terms and explicit expectation keys for later callers.

**Bounded recall is a context and eligibility layer, not the complete numeric evidence set.** After
the nonempty-recall gate, each option's warm and cold totals come from all retained associations to
its named objects. They are not restricted to the top three recalled records. Recall entries are
shown as context-only reasons; the calculation uses the separate association causes and relevant
expectation keys. This distinction is explicit in the receipts and remains a pilot review question.

The general evaluator supports required recall IDs on opportunities and options. A missing required
record makes the opportunity unavailable or excludes that option; a zero base also excludes an
option. The current free-time adapter does not use required-recall gating to create new activities.
Reasons retain the base, boldness, each warm/cold cause, expectations and any intention or cost.

Torn diagnostics mark mixed sides above `TornAt` (default 0.025), warmth above that threshold with
chance below 0.5, or the top two probabilities within 10%. They are diagnostics in this increment:
they do not schedule a Laya request. Settings are experimental, not calibrated psychological laws.

## The same choice with and without history

`PersonalMemory.Capture()` creates an immutable `MemoryFrame`. It freezes the state needed by the
production `Recall`, `Sides` and `Expectation` queries; these formulas are not reimplemented for
comparisons. Later learning, retraction, correction or decay cannot alter the frame. A frame has no
mutation API, model or observer sink, and copies no run-long acquisition receipt ledger.

`WithoutHistory(ids)` returns another frozen frame. Removing a fact also removes that holder's
dependent accounts and readings; removing an account removes its readings; removing a reading
removes only itself and its numeric seed. Seeded expectation entries are withdrawn in the same
closure. Sources and other holders' private account/reading IDs are not valid removal targets.
Unknown IDs fail atomically. Repeated removals are idempotent and cumulative.

Independent lived evidence, its current decay and cumulative dilution counts stay fixed. Removing
history does not rerun the past, restore evidence evicted earlier, or undo relationships and events
that may already have happened because of that history. `HistoryIds` retains the original removable
identity set; `RemovedIds` lists the cumulative closure.

`LivesChoices.Compare` holds the captured scene, candidate identities and order, base weights,
personal state, settings and random unit constant. It removes the specified history, then recomputes
recall, eligibility, reasons, probabilities and selection. It reports reason and probability changes
even if the sampled action stays the same. This is a fallback-only local intervention, not two
diverging full runs or a claim about a counterfactual model answer.

Each comparison records `RemovedHistoryIds` for the requested intervention and
`RemovedDependencyIds` for the complete removal closure, including the requested records. The
opportunity ID identifies the held choice across its two decision receipts.

The comparison is explicitly non-comparable if the opportunity disappears, either side requires
the original legacy draw, or eligible alternatives change. It preserves both decisions and a reason
instead of manufacturing a matched result. Live comparisons are observation-only: their alternate
selection never becomes the character's goal. A run-level requested history ID is compared only for
holders whose private identity set contains it.

## Recording and command line

`ReplayOptions.LivesRouting` carries `Enabled`, `Record`, `CompareHistory`, `Memory` settings and
`Choices` settings. `ReplayOptions.History` carries the optional validated version-1 history pack.
Direct simulation callers configure the same routing options. Turning `Record` off suppresses
routing receipts and comparisons while leaving the routing and learned state in effect.

The Replay CLI adds:

| Flag | Meaning |
|---|---|
| `--lives-routing` | Enable this destination-choice increment. |
| `--history <file>` | Read an explicitly supplied history pack; requires routing. |
| `--compare-history <id>` | Request a local history intervention; repeat for multiple IDs. Requires routing. |
| `--no-lives-record` | Keep routing active but omit its observation block and comparisons. Requires routing. |

For a lived-evidence run with no pre-run history or model service:

```powershell
dotnet run -c Release --project sim/UnderGlass.Replay -- --seed 7 --days 7 --lives-routing --out lives-routing-seed7.json
```

After separately authoring and reviewing a valid pack, substitute its actual filename and record ID:

```powershell
dotnet run -c Release --project sim/UnderGlass.Replay -- --seed 7 --days 7 --lives-routing --history "your-reviewed-pack.json" --compare-history "your-reading-id" --out lives-history-seed7.json
```

Those placeholder files and IDs are not shipped canon content. Existing reflection/model flags
remain separate; adding `--lives-routing` does not start model calls or reinterpret existing hybrid
reflections as Lives choices. The CLI exposes enable/history/recording controls; the numeric memory
and choice settings above currently require programmatic options.

Enabled, recorded runs gain an additive **`livesRouting`** block, with `version: 1`,
`stage: "free-time-routing"`, and `backend: "authored-keyed-draw"`. It contains options, the validated
history snapshot when supplied, `historySnapshotHash`, decisions/comparisons, skipped menus, memory
changes, and perception metadata. The hash is lowercase SHA-256 of the validated pack serialized by
the local `System.Text.Json` options (camel-case properties and named enums). It is **not JCS**, a
general canonical-JSON hash, or the history hash contract for the full pilot. No supplied pack means
null snapshot and hash. The usual recording version remains unchanged because this is additive.

This is an experimental routing receipt schema, **not** the full
[Lives observer v0.2 contract](../contracts/lives-observer-recording-v0.2.md). It does not write a
`lives` block or `choiceQuestions`, and the existing chain viewer does not interpret these new
receipts. Ordinary run playback can show changed movement and existing events; it does not yet
display this causal comparison. Observer arrays are never consulted to choose, feel or learn.

## Bounds, verification and next work

Recall output and active associations keep the memory foundation's bounds. Frame capture shares
validated immutable record values but copies working collections and rebuilds indexes. Memory writes
also rebuild the holder index. Initial history validation/copying currently occurs for each holder;
expectation keys, dilution counters and acquisition receipts have no whole-run population bound.
Recorded decisions and changes also accumulate when recording is on. Avoiding a run-long archive
scan during recall is useful, but this implementation has **not** established large-population cost
or recording size. Allocation, time and storage measurement remain necessary before a scale claim.

Focused tests exercise real routing selection, unchanged legacy choices where nothing is recalled,
history comparisons, knowledge boundaries, correction and recording independence. Kernel tests cover
immutable frames and dependency removal. Synthetic scenes establish mechanics; they do not establish
character fidelity or a meaningful complete evening. Exact seeded repetition and recorder-on/off
equivalence remain required invariants.

Next work is to admit the pilot's actual histories individually, connect recalled possibilities to
reflection and company/plan choices, classify what each participant can perceive of the result, and
use learned expectations in a later choice. That work must retain refusal, postponement and failure,
then export the full observer chain so the same-choice comparison is readable in the viewer.
