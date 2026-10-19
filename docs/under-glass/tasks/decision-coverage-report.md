# Claude handoff: decision coverage report

Status: ready for implementation, 2026-10-19. This PR contains instructions only. Sid will assign
the coding work. Target `codex/reflection-prototype`; follow AGENTS.md and inspect git status first.

## Why this small job matters

Sid wants Laya to act as the primary character decision system. Currently hybrid runs combine
authored routing/company choices, generated inspiration and selective typed decisions. We need
to see what actually reached a model before judging behaviour or speed. Codex is implementing
an independent `--evening-evaluation all` mode and planning broader decision coverage. This report
must work on existing recordings now and tolerate that additive policy field when it arrives.

## Ownership and deliverable

Implement a read-only, standard-library Python CLI at `sim/tools/decision_coverage.py` and tests
at `tests/test_decision_coverage.py`. Update only this task's Status/usage/validation section and
those two files in the first implementation. Do not edit simulation, model adapters, Replay CLI,
shared docs, run service, Run Studio or chain viewer: Codex is working on their integration.
Do not add a web page, model calls, dependencies or generated recordings to the repository.

Suggested interface:

```powershell
python sim/tools/decision_coverage.py path/to/run.json
python sim/tools/decision_coverage.py path/to/run.json --json
python sim/tools/decision_coverage.py path/to/run.json --person Penny
```

Text should be a compact summary, followed by per-person/per-decision-phase rows. JSON should
carry a versioned report schema, denominators and explicit unknown/not-recorded values. Input
must never be modified. Parse an ordinary full recording; raw tape arrays and standalone HTML
are out of scope and should produce a clear error. A recording without Lives data is valid.

## Authoritative inputs (verify against source)

- `UnderGlass.Sim/Replay.cs` writes `livesEvenings` version 1, with `settings`, `choices`,
  `decisions`, `updates`, `outcomes` and `gates`. `Lives/Evenings.cs` defines `EveningChoice`:
  phase, planId, decision, comparison, optional evaluation, context.
- `Lives/Decision.cs` defines request/answer/record. The exact choice ID is
  `request.fallback.opportunity.id`; holder and simulated time are in that opportunity too.
  Answer fields include backend, called, note, packet, response and reportedModel. Packet has
  characters and omitted record IDs. `choices[].evaluation` repeats a record in `decisions`.
  Count each decision ID once; reject conflicting duplicates, not identical repeated serialization.
- Default recordings omit `settings.evaluation`. The new mode records `"All"`; absent means
  legacy/selective metadata, not evidence that an evaluator was configured. Selective only sends
  torn choices within its budget. A missing evaluation alone does not reveal the reason.
- `livesRouting` and `livesCompany` contain separate recorded choice receipts. Inspect their
  source types and count those choice records, not memory updates or opportunities you invent.
- `reflections` is a different mechanism. `ReflectionAnswer.Evaluations` can contain several
  passes for one reflection; a pass is not a new character choice. It lacks the same explicit
  `called` flag as Lives. Report recorded passes/responses/errors separately, and do not pretend
  to know total HTTP attempts from incomplete evidence. Keep generation and evaluation separate.

## Required facts and wording

1. Recorded evening choices, typed evaluation records, `called=true` HTTP attempts, successful
   Laya answers, authored/fallback answers, and choices without an evaluation. Show per-person
   and phase breakdown plus a total. Distinguish observed provider attempts from live requests:
   replay preserves historical `called=true`, so this file cannot prove a network call happened
   during replay. Label these as recorded attempts.
2. Evaluation coverage is unique evaluations / recorded evening choices. HTTP participation is
   recorded attempts / those choices. Zero denominator is unavailable, never 0% or 100%.
   Do not publish a whole-town percentage: ordinary acts have no complete decision denominator.
3. Preserve fallback explanations from recorded notes, grouping exact strings when necessary.
   Distinguish no evaluator evidence from a recorded failed evaluation. Do not infer budget,
   torn-gate, timeout or overflow from absent data. Mark new/unknown backends separately.
4. Number of packets with omitted memory, omitted-item total and character-length range. Missing
   packet is unknown, not zero omissions. Provide the affected decision IDs in JSON; disclose
   details in text only for a requested person or a bounded example list.
5. Compare model-selected action with `request.fallback.selectedId` for evaluated choices only.
   Call this disagreement, not accuracy. Do not confuse `choice.decision.selectedId` (fallback)
   with `choice.evaluation.selectedId` (actual evaluated selection). No inference that a selected
   intention physically happened; outcomes are a separate block.
6. Record policy/model identities if present. Label partial live snapshots (`live.partial`) as
   incomplete. If recording was disabled or a block is absent, say not recorded. No fabricated
   latency, throughput, omitted motives, reasoning, or reasons a model changed its answer.

This is an observer diagnostic, never an input to the simulation. Character knowledge is not
expanded: label the report as an observer tool. Avoid printing full raw model responses/prompts
by default; counts and explicit provenance are the useful starting point.

## Acceptance checks

Use small synthetic JSON fixtures in tests, no service or paid model required:

- older recording without Lives; missing optional fields; zero choices; partial snapshot;
- mixed selective run: evaluated, unevaluated, fallback before HTTP, fallback after HTTP;
- all-choice run with an untorn decision and more than two evaluations/person/day;
- identical evaluation repeated in choices and decisions counts once; conflicts reject clearly;
- omitted recall IDs, missing packets, distinct configured/reported model names;
- held fallback versus evaluated selected ID; never count disagreement as a success metric;
- reflection with multiple passes and incomplete failure evidence;
- person filter updates every denominator; invalid person reports no recorded choices clearly;
- malformed input exits nonzero with a concise message; input bytes unchanged;
- deterministic text/JSON ordering, invariant under input record ordering where IDs are distinct.

Run `python -m unittest discover -s tests -p test_decision_coverage.py`. Before committing, follow
repository-required checks unless Sid explicitly changes that instruction. Include exact commands
and results in the PR. Update this file to mark implemented facts and remaining limits. Do not
claim live performance or model quality based on synthetic tests.

## Later work, not this job

A web coverage summary and links to Life chains can consume the report once the counts are
reviewed. Shared context packing, alternative model support, opportunity-gate changes, routing
and company evaluation belong to the Laya-first implementation and must stay out of this PR.
