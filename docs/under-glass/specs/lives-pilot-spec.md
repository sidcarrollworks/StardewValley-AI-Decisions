# Lives pilot: implementation and acceptance plan

2026-10-15. Continues Claude's assessment after PRs #96–98 merged. Sid asked Codex to
resume this work and defer the archetype editor. Development base: `codex/reflection-prototype`.

## Status

**Design, not a built pilot.** The evidence corrections, observer contract/checkers and
Penny's routine data are built or committed. The contract's example remains illustrative;
its draws, thoughts and outcomes are not experimental results. No `Lives` runtime flag,
history initializer, scheduled evening or learning loop exists yet.

**Built in this change:** `sim/tools/record_lives_baseline.py`, an offline baseline capture
and verification tool. It records today's engine, keeps source/settings/output receipts,
checks the night recorder does not change behavior and checks exact authored tape replay.
It changes no simulation rule. Measurements are in the [baseline report](../experiments/lives-baseline-2026-10-15.md).

The approved [assessment](../lives-gap-assessment.md), especially sections 9–12 and Appendix E,
governs behavior. The [0.2 contract](../contracts/lives-observer-recording-v0.2.md) governs
recording. This document turns them into a build order and resolves implementation choices
as recommendations, explicitly distinguished from Sid's decisions. It does not amend Law 1,
silently approve history readings, or reopen the bars experiment.

## What the first review must let Sid follow

Start with Penny, Pam and Gus **inside the normal town**, not three people in an empty world.
A placed scene can isolate a mechanism; a natural run must preserve other people's actions,
work, travel and interruptions. A smaller selected cast means depth of review and history
authoring, not a name check inside the engine.

At an ordinary encounter, or after an alcohol errand is refused, a person recalls relevant
experience. They may suggest a game together, keep their existing activity, or do nothing.
The other person may agree, postpone or refuse. Agreement creates an intention, not movement
or success. Each later chooses whether to make time and travel; both must actually arrive,
be awake and free, and complete the activity. Each learns only what they perceived.
Gus can notice an expected regular was absent without knowing where she went or why.

The review follows the same evidence into a **later** decision. An interesting-sounding
thought without an effect on a reason, alternative or probability is not evidence of learning.
A probability can change without the sampled action changing; both results are worth showing.

## Preserve the routine baseline

Sid's D54 decision remains binding: bedtime is a habit, actual sleep follows energy, waking
at 03:00 is permitted, and the scheduled hour alone never removes availability.

For the first implementation, the recommended control settings are:

- Keep `BedtimeHabitWeight = 9` as an experimental value, not a new claim about canon or a
  guaranteed nine-in-ten nights. Competing choices affect its probability.
- Do not hold a tired person awake until their habitual hour. Do not introduce an additional
  invitation penalty solely for passing that hour. Existing self-known tiredness, low mood and
  impending duties continue to matter. Record actual sleep cutting a plan short.
- Keep the empty early-morning activity menu visible as a gap. Add solitary activities in a
  separate increment after the shared activity works; do not describe current home idling as
  a completed reading, cooking or chores action. Such actions need time, interruption and
  consequences before they can create evidence of enjoyment.
- Do not change nighttime errand seeking by checking a target's live sleep remotely. Its
  eventual fix must use the seeker's own availability expectations. A last-seen location
  is not a current sighting.

These choices isolate the history experiment. Sleep retuning and new night activities remain
possible later; no missing activity is repaired by forced sleep or forced social success.

The first natural pack covers **35 days**, reaching summer Monday/Thursday/Sunday. Continue
with 112-day runs for autumn recall and retention. The 0.2 fixture uses summer Thursday,
day 31; do not copy its seed or extend the evening to reproduce its story. Penny's actual
summer evenings in PR #98 often ended around 19:50. Her teaching and Saturday whereabouts
still contain documented simulation assumptions. #98 implemented hub weights and bedtime
habits, **not** her complete canon schedule.

## Build order and seams

Each increment stays independently reviewable. None is called a complete pilot until the
closed loop, real recordings and comparisons all pass. No synthetic record is presented as
output from a missing runtime mechanism.

| Increment | Build | Existing seam | Required evidence before moving on |
|---|---|---|---|
| 0. Baseline | Current town, then the existing pilot prerequisites as a separate arm | Replay CLI; `Nights.cs` | Completed pack, source receipts, recorder equivalence, tape replay. Built here. |
| 1. Personal history and recall | Validated character history input; private accounts/readings; signed associations; expectations; bounded cue index; pure comparison inputs | New runtime types beside `Model.cs`; `Simulation.Feelings.cs` for later experience ingestion | Admission and duplicate checks, holder isolation, both signs retained, first-outcome learning, rename/transfer tests. No need for approved canon seeds to test synthetic data. |
| 2. Choices and questions | Immutable opportunity snapshot, typed alternatives/reasons, fallback sampling, pending answers, capped Laya questions | `Simulation.Ask.cs`, `Simulation.Invitations.cs`, `Reflection.cs`, `UnderGlass.Minds` | Same options for both weighers; no action before the answer; not-now/busy/no distinct; same-choice history comparison; deterministic failure/replay. |
| 3. A shared evening with consequences | Plan offer from an encounter or commitment refusal; individual follow-through; game at a shared home; interruption and outcome classification | `Simulation.cs` (`Decide`), `Simulation.Errands.cs`, `Simulation.Acts.cs` (`KindFits`, `AfterAct`) | A real success, refusal, postponement and agreed-but-missed/interrupted path; a later choice reads learning; no second invitation at execution. |
| 4. Record and inspect the whole loop | Write-only Lives sink, storage adapter for 0.2, character-safe request construction, v0.2 viewer adapter and comparison display | `Replay.cs`, `Simulation.Encounters.cs`; `chains-model.js` | Real output passes structure checks, pilot-rule checks tested separately; recorder off gives identical decisions; rewind and other-mind tests. Start the sink alongside 1, rather than reconstructing causes afterward. |
| 5. Natural/model/transfer review | Authored baseline, Laya, Haiku + Laya, Qwen + Laya; Evelyn/George/Alex; population measurements | Replay and recording tools | Inspectable threads and failures, immutable model receipts, exact tapes, same mechanisms in second household, separated CPU/memory/recording/model costs. |

### Runtime state and observer state

The engine owns history accounts, associations, expectations, plans and question state. It reads
those typed records directly. A write-only `ILivesSink` receives immutable event snapshots.
`Replay` owns the recorder and serialization. No simulation decision reads `lives`, observer
labels, comparisons, viewer selection or recording retention settings.

Keep per-holder cue indices, bounded active associations and recent expectation evidence.
Historical evidence/versions can be retained for inspection separately; they must not turn
each choice into a scan of a half-year recording. Enforce the assessment's bounds: three
recalls with a pre-run slot when applicable, an additional warm recall in low mood, at most
five eligible options, three association entries per object preserving each sign, 96 per
holder, and a seed plus six lived expectation entries per key. Test every eviction boundary.
Record discarded/replaced evidence links so a displayed change remains explainable.

Configuration and new record fields are optional, appended with defaults. With Lives absent,
leave the original branches and serialization unchanged: no extra null fields, snapshots or
model receipts. A baseline's existing log hash is necessary but insufficient; compare all
deterministic recording fields and, for identical commands, the bytes.

### History admission

Do not load the illustrative observer fixture as runtime history. Its `observer` objects
contain provenance and proposed effects, which are not decision inputs. Author a separate
input pack with stable fact/account/reading ids, holder, acquisition mode, source keys,
admission standing and review status, validated before initialization.

- Facts, completed pasts, recurring practices and present readings remain distinct.
- A completed past seeds a present association only through the holder's sourced stated link.
  An interpretation we supply stays marked authored and does not seed a number.
- One account never grants another participant's feelings or knowledge. A guess stays a guess;
  source/editorial metadata is not an in-world conversation.
- A stance-less wish can make an activity conceivable without making it enjoyable already.
- Seed once per admitted reading, not once per citation. A source cited in both an account and
  a reading is not another experience. Lived outcomes can outweigh the small seed.
- Preserve the review gate: 0.2 section 17.6 requires Sid's individual review before a proposed
  reading seeds state. Synthetic tests are labelled synthetic. A no-history run starts empty
  and learns from its own outcomes; it is not silently given the fixture's proposed histories.
- Archetypes contain no histories. Copying one does not copy source characters' memories,
  commitments or relationships. Penny's alcohol-provision commitment remains character data,
  with the existing absolute refusal preserved. It does not prohibit accepting or drinking.

### Choice lifecycle

Separate proposing an option, choosing it, attempting it, and completing it. Every opportunity
has a stable id derived from seed/time/holder/trigger and a deterministic ordinal. A pending
ask reserves **no act id**. At resolution, assign an act id only if a real act begins.
Use the next-minute answer boundary from the assessment; a wall-clock model timeout never
advances simulation time or causes a person to act on a reply that has not arrived.

An opportunity freezes the holder's knowledge, own state, constraints, candidate identities
and typed reasons. Eligibility is checked before either weigher, then checked again when an
attempt executes. Actual sleep, distance, a new duty or another activity can prevent execution;
record that outcome without rewriting the earlier choice as if it had foreseen it.

Refactor the Ask's margin calculation from its current yes/no sampling so Lives can use its
same state costs with three outcomes. Leave the old Ask path unchanged with Lives off.
The assessment's probabilistic yes/not-now/no replaces a clear-band certainty only with Lives on.
Not-now needs a recorded temporary reason. Busy before an ask is made is not a response and
must not reserve a refusal act. For an in-reach worker who can receive an offer, recommend
making the known duty a contingent not-now cost; work still prevents immediate game execution.
Do not let the asker see the worker's private mood or numeric cost through that answer.

Route the resulting goal after body/work/patrol constraints. Resolve precedence with an active
errand explicitly: keeping a new plan must not silently delete a promised delivery. Preserve
the old errand path until the general choice has an alternative and a recorded reason for
postponing it. Simultaneous offers use name order and stable ordinals; each pair has one open
plan and at most one offer a day. No watching-dependent behavior.

### Actions and learning

A planned game has a real duration and participants. It requires actual co-location and free
participants; home eligibility generalizes to housemates rather than their ages or names.
Record start, completion or interruption and each participant's acquired account. An agreed
plan whose game never completed cannot use the completion joy. The original `PlayedGame`
effect must not run a second time when Lives writes its felt shares.

Classify outcomes before updating expectations:

| Observed result | Learn | Do not infer |
|---|---|---|
| No offer | Nothing about the other person's answer | A rejection |
| Busy before asking | Availability, on observed evidence | No, a refusal act, or a reason from the target's private state |
| Not now | Temporary availability | Dislike or rejection |
| No / commitment refusal | One answer entry; existing refusal hurt where appropriate | An additional association for the same hurt or a weakened absolute commitment |
| Yes | One answer entry; a shared plan | Attendance or enjoyment |
| Agreed, absent | One follow-through entry in the observer's own account | A second whereabouts miss, a second no, betrayal, or the absent person's destination |
| Together and completed | One follow-through result; each person's felt experience | Identical enjoyment or the other person's private feelings |
| Not found without a plan | Whereabouts only | A broken promise or sadness from a promise that never existed |

An agreed evening may disappoint. Keep the mood effect and a cold association toward the
hoped-for evening distinct from an attitude toward Pam. It affects later decisions about
keeping that situation; it is not an extra penalty on every offer to Pam. Count an event
referenced through person/place/activity once per decision contribution, with a stable cause
id. Do not sum several aliases of the same joy. Document and test the aggregation rule before
integrating it; the observer must retain all cited routes even when the magnitude counts once.

Learning begins on the first outcome. `DeriveAfter` changes when a prose/typed summary is
available, not when evidence starts affecting a probability. Keep contradictory outcomes and
original source readings visible. A summary is not new independent evidence. Leave adaptive
regard set-points off for this pilot; their town-wide effects need their own comparison.

Penny's zero Evening hub weight excludes that routine, not the entire Saloon. A later learned
place option can be eligible through a separate known activity or purpose without rewriting
the sourced starting card or treating the place as forbidden. Record that distinction; test
it with a renamed character. Do not add a hidden generic pull that defeats the routine fix.

### Model boundary

Keep the hybrid architecture. The generator may supply private wording and propose a
possibility; the engine checks whether it is executable. Laya and the fallback then receive
the **same** option set, reasons and constraints. Both include carrying on, refusal and
postponement where eligible. Generated explanation is never a fact, account, association or
expectation writer.

Recommend the contract's thought-only path for opportunities that are not torn: generate
when the reflection trigger warrants it, but spend no Laya evaluation on an answer the engine
will discard. Torn questions are capped at two per person-day; preserve separate trigger
budgets from the assessment. Counts distinguish generator requests, logical questions, Laya
passes, retries, failures and fallback choices. Balanced evaluation can make several calls
for one question. Preserve the 1,250-character Laya request budget and record any omitted
context; omit whole optional fields, not half a reason or only one side of a conflict.

All model work stays off the simulation mutation path. Apply answers in stable request order.
Save immutable requests, exact model identity, evaluation mode, responses, structured failure
stage and fallback. An edited history request must invalidate a tape. Haiku versus Qwen is a
whole-mind comparison when their generated proposals differ; it does not isolate Laya.

## Same-choice comparisons

Build these at the **decision snapshot**, not by rerunning two towns and matching by minute.
After trajectories diverge, equal clock times do not establish equal situations.

1. Freeze the world, holder's own state, other evidence, eligible option identities/order and
   random unit. Fork an immutable calculation input; never modify the live person's store.
2. Remove the selected reading/history contribution and the seeded association or expectation
   derived from it. Leaving its derived numeric entry behind would make the comparison false.
   Keep independent lived evidence. Record the removed dependency closure by id.
3. Recompute recall/reasons and numeric weights using the production functions, not a second
   copy of the formula in a report generator. Explicitly identify any recall-slot replacement.
4. Report changes in reasons, probabilities and sampled action separately. Use the same draw
   in both fallback arms; do not search for a seed that makes the action flip.
5. If removal changes eligibility, generates a different candidate set or requires a different
   scene, mark the comparison non-comparable with a reason. A wish enabling the only game
   proposal is such a case; never insert the missing option merely to make a matched test pass.
6. Model counterfactuals need a fresh recorded model answer for the changed request. A fallback
   counterfactual remains labelled fallback even beside an actual Laya choice. A longer-run
   intervention with history removed is a separate experiment, not this controlled comparison.

Required cases: Penny's expectation that Pam is away; mixed warmth/burden toward Pam; Pam's
two-sided Saloon feeling; the hobby wish; Gus's regular-customer expectation; a completed past
with a sourced present link in the second household. Include a same-action/different-chance
case and a non-comparable case. Do not require every admissible history to move a number.

## Recording and viewer contract decisions

Use 0.2 for the runtime writer; preserve the frozen 0.1 files and its existing reader.
Add an exact-version adapter rather than changing the version guard and treating the shapes
as interchangeable. The archetype editor is deferred; snapshot display does not require it.

- Use explicit per-person field projections from contract 4.2, including its narrower view of
  someone else's attempt. Merely deleting `observer` is insufficient. Character labels and
  model text are built from holder-visible inputs and also receive human sample review.
- Apply acquisition time and `seq`, snapshots and learning updates, pending outcomes and actual
  sleep from moves. Do not show final strength or outcome while scrubbing an earlier minute.
- Copy the configured character pack into a content-addressed artifact beside the recording;
  retain its hash, snapshot and source commit. This supports uncommitted editor exports and
  offline review better than relying on a git blob alone. This is an implementation choice.
- Keep the logical object format first; measure it before choosing compact rows or chunks.
  Browser readers require structural validity, not the experimental formulas. Run the rule
  checker separately against the declared rule revision.
- Test canonical hashing against the supplied JCS vectors before selecting/implementing the
  serializer. A sorted-key JSON serializer alone does not establish RFC 8785 compliance.
- A shared plan reveals shared terms only. A world outcome is observer-only. Treat unavailable
  prior snapshots as unavailable, never as zero. Preserve not-now and busy in base rows with
  `accepted: null`, `askMade`, and no invented act id.

## Acceptance pack

First run the baseline command below. For the runtime increments, keep the following distinct:

| Pack | Purpose | What counts as a pass |
|---|---|---|
| Placed mechanism cases | Trigger every rare branch without waiting for a natural occurrence | All legal endings recorded, causal ids/knowledge gates correct, no artificial success guarantee; explicitly synthetic setup |
| Natural authored: six seeds, 35 days; then 112 days | Inspect actual frequency, interruption and learning | Read complete and unfinished threads, include unremarkable and poor outcomes; no social success quota |
| Matched decision comparisons | Isolate a history's contribution | Held-input receipts, dependency removal, genuine non-comparable results, production formulas |
| Laya and hybrid variants | Inspect model behavior and fallback | Exact receipts/tapes, shared eligible alternatives, failure paths, no fabricated evidence |
| Second household | Transfer the mechanisms | Evelyn/George/Alex use the same rules; source/knowledge distinctions survive; no name switches |
| Scale checks | Measure costs separately | 26/31/60 populations; prerequisites-only baseline first; runtime CPU/allocation/peak memory, serialized bytes, per-person recall/choice/model counts separately; a 60-person 112-day run |

The baseline tool's wall time is not CPU cost and its file size is not working memory.
The first runtime benchmark must add allocation/peak-memory measurements. Use the assessment's
1.25x time and 1.2x memory ratios as investigation warnings against prerequisites on, not as
outcome tuning targets. No per-pick run-long scans; model budget cannot depend on who's watched.

## Run the baseline now

From the checkout root, with Python and .NET 8:

```powershell
python sim/tools/record_lives_baseline.py --out out/lives-baseline-review --plan-only
# Use a different fresh directory to execute; an existing plan/pack is preserved.
python sim/tools/record_lives_baseline.py --out out/lives-baseline-run
python -m unittest discover -s tests -p test_lives_baseline.py
```

Defaults: seeds 1, 2, 3; 35 days; the current `pelican` town. Six main recordings and three
checks (night recording off in each arm for seed 1; authored tape replay for seed 1).
No injection, history, character prompt pack, dreams, speech, models or model servers. The
prerequisites arm enables exactly `company,invitations,errands` and authored reflection at
the CLI's recorded defaults. Output includes JSON runs, logs, source and checksum receipts,
settings, a summary and night diagnostics. Existing output directories are refused. A failed
build/run leaves a failed manifest and logs rather than a misleading completed result.

The next implementation is increment 1, with synthetic admission/learning tests and the
write-only sink seam. The complete pilot additionally needs individual source-reading review;
that gate does not prevent building or testing the general mechanisms.
