# Laya as the character's decision system

## Direction and status — 2026-10-19

Sid selected Laya as the primary interpreter of a character's situation, not an occasional
tie-breaker. This is the next development priority. Haiku remains an occasional inspiration
source; a larger generative role and Jev/Kev comparisons follow after decision coverage is real.
No requirement for happy, sociable or stable towns is introduced. Law 1 remains unchanged.

First implementation: an opt-in `--evening-evaluation all` evaluates every choice the current
evening consumer actually queues. The existing `selective` policy remains the default control.
This does **not** make the whole town model-driven. Stage 2 now has an opt-in destination
adapter (`--routing-evaluation laya`), including no-history choices and staying put, with
next-minute eligibility checks and exact replay. Its context covers current place, clock, body,
mood, two social traits and recalled evidence. Richer shared personal context and auditing
preference gates remain unfinished; stages 3–5 remain planned.

Sid's sequencing update, 2026-10-19: defer frozen-scene experiments, history/option-order
comparisons, model comparisons and observer test packs until the remaining behavioural
consumers are connected. Proceed through routes, immediate company and ordinary interactions.
Keep implementation correctness checks (knowledge boundaries, fallback, cancellation and exact
replay) with each integration; this defers model-quality experiments, not regression checks.

## Responsibility boundary

The engine owns bodies, physical eligibility, perception/evidence acquisition, travel, action
execution and consequences. Memory stores each holder's accounts, conflicting feelings and
expectations. Laya receives a detached, holder-known scene and legal alternatives, and supplies
weights; the engine samples with the original keyed draw. Models never turn imagination into
witnessed evidence or gain access to another mind or remote live positions.

The target is every meaningful behavioural choice, including ordinary decisions. Reconsider at
arrivals, encounters, interruptions, completion or a material change of known circumstances;
do not ask every movement tick or repeatedly sample an unchanged choice until it succeeds.
Distinguish physical constraints from habits and preferences. Current authored opportunity gates
are implementation limits to audit, not all permanent laws. Retain existing explicit commitments
until separately reviewed; do not silently change them while changing the evaluator.

## Current decision inventory

| Consumer / source | Current control | Next integration |
|---|---|---|
| Private inspiration, `UnderGlass.Minds/ResilientReflectionMind.cs` | Authored or generator proposals; Laya evaluates bounded responses | Keep independent; reuse its recorded requests, failure and context lessons |
| Evening offers, answers, follow-through and concern, `Simulation.Evenings*.cs` | Typed `ILivesDecisionMind`, selective torn gate and two evaluations/person/day | Stage 1: optional all queued choices, exact replay and unchanged execution |
| Destinations, `Simulation.cs` (`Decide`) and `Simulation.RoutingDecisions.cs` | Optional typed route evaluator; authored default; five-option cap and original eligibility gates | Stage 2 partially built: detached choices, next-minute recheck, receipts and tape; richer context/menu audit remain |
| Seeking, game offers and answers, `Simulation.LivesCompany*.cs` | Memory-weighted maths; synchronous consumers | Stage 3: same decision boundary, preserve real encounters and activity lifecycle |
| Ordinary acts and responses, `Simulation.Encounters.cs`, `Simulation.Acts.cs`, `Simulation.Ask.cs`, `Simulation.Invitations.cs` | Authored motives, gates, scores and action weights | Stage 4: audit and connect one family at a time, including selected authored speech |
| Movement, energy, evidence and outcome updates | Engine mechanics | Keep mechanical; private interpretation may later become a separate typed choice |

This inventory identifies integration sites, not proof that all subjective gates are enumerated.

## Stages and acceptance

1. **Evening coverage (built).** `EveningOptions.Evaluation` / CLI
   `--evening-evaluation selective|all`. Selective retains the current torn gate and daily budget.
   All ignores both, but still obeys existing opportunity and execution gates. Require an evaluator
   or tape rather than silently running authored-only. Record the policy in options. Preserve
   option order, draw, refusal/delay, cancellation, next-minute application and exact tape replay.
   Prove untorn choices and a third same-person/day evaluation reach the provider; prove failure
   still records fallback and observer recording/async latency cannot change behaviour.
2. **Personal context and free-time routes.** Build a shared detached decision context for actual
   known situation, body state, archetype tendencies, commitments, ongoing intentions and relevant
   recalled text. Keep numeric fallback reasons separately identifiable. Record which memories
   fit, which are missing and which calls fail before HTTP. Version packet changes; defer the
   comparative packet experiments to stage 5. Audit physical eligibility
   versus preference gates. Route choices must contain actual alternatives, including staying put,
   and be applied only after rechecking the current state. No call may read the live simulation.
3. **Immediate company.** Move seek/offer/answer through that boundary. Preserve who perceived an
   offer, not-now versus refusal, no omniscient searches, completion/interruption and first-outcome
   learning. Record enough to trace history through the selected action and its actual outcome.
4. **Ordinary social acts.** Add acts, replies and authored-line choice in bounded slices. Expand
   legal possibilities without allowing our highest-ranked motive alone to decide the model's
   entire menu. Log opportunity exclusions separately from the character's choice. Do not label
   the whole town Laya-first until an inventory shows which decisions still bypass it.
5. **Performance and model comparison.** Start with held scenes using identical history, options,
   inspiration and keyed draws; remove one relevant history, permute options and paraphrase.
   Then compare short live runs. Measure local latency, HTTP/fallback rates and simulated minutes
   per wall second at several cast sizes before batching/caching. Cache only identical personal
   contexts; batching must not change answer application order. Jev and Kev use the same boundary
   later, with exact model versions and per-provider context limits recorded.

Complete correctness checks before broadening the next consumer; defer comparative runs and
observer test packs until the remaining consumers are connected. These are staged PRs,
not a promise to finish all five in the first implementation. Existing authored controls remain.

## Evaluation and known limits

Count considered opportunities, evaluated choices, actual HTTP requests, successful answers,
fallback reasons and omitted recalled texts separately. A provider evaluation is not necessarily
a network call. An absent evaluation in an old recording does not prove why it was skipped.
The viewer must not claim that returned weights reveal the model's reasoning.

In the first slice, 1,250 characters remains our working request cap, not a universal model limit.
Candidate lines and numeric reasons still take priority over recall. All-choice policy cannot fix
that, a missing activity, or an opportunity excluded before queuing. It also retains the current
once-per-pair/day offer policy, recalled warmth/wish opening, routine baseline and household scope.
Same-choice history comparisons currently re-evaluate only the authored fallback; they are not
Laya counterfactuals. These limitations must accompany any live trial.

Success means history-sensitive, distinct and causally readable decisions with room for mistakes,
hostility and refusal. Do not score by agreement with authored maths or number of successful games.

## Parallel work suitable for Claude

[Draft PR #108](https://github.com/sidcarrollworks/Under-Glass/pull/108) defines a read-only decision coverage report over existing
recordings. It owns a new report script and tests only, so it can proceed without editing the
simulation, packet builder, replay CLI, Run Studio or chain viewer while this work is underway.

Stage 1 validation: see the [live seven-day coverage check](../experiments/laya-first-2026-10-19.md).
