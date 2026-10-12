# Character voice trial, 2026-10-12

## Result

The opt-in character context, reviewed source pack and paired comparison tool are ready for
experiments. **Distinct, coherent generated voices are unresolved.** Qwen3-0.6B often ignores
cadence and conflicting impulses, invents hidden feelings, reverses roles or proposes prose that
does not match its typed suggested act. More personality description alone has not fixed this.

The final eight-pair trial produced 15 local generator + Laya answers and one explicit authored
fallback. All 75 completed evaluator passes included the full candidate lines and fitted the
1,250-character semantic budget; none reported server truncation. The missing five passes belong
to Shane's overlong generated thought, rejected before evaluation. The trial returns nonzero for
that failure; passing implementation tests are separate from this live reliability result.

## Setup and evidence

- Start with [paired thoughts](../../../out/character-voices-2026-10-12/comparison.html).
  [Original requests and raw receipts](../../../out/character-voices-2026-10-12/comparison.json)
  preserve failures, prompts, routing, seeds, timings and evaluator rotations.
- Then inspect the [seven-day hybrid recording](../../../out/character-voices-2026-10-12/hybrid-seed7.html).
  Inner life → What informed this choice? shows the actual private voice, selected initial history
  and source keys. This history never becomes an encounter or shared evidence.
- [Source review](character-voices-2026-10-12.md) describes the installed-game extraction,
  admission of 101 anchors, exclusions and editorial archetype choices.

Generator: the existing pinned local **Qwen/Qwen3-0.6B**, non-thinking template, temperature 0.7,
request-derived seed and 180 output tokens. Evaluator: **typed-decisions**, Balanced mode with five
complete response lines in these hypothetical scenes. Within a pair, actor, subject, request ID,
remembered source, mood, candidate lines and fallback proposal are identical. Only the character
snapshot changes. The profile adapter adds generation instructions and compact evaluator framing;
this tests the entire context path rather than isolating biography as a causal variable. No action
is sampled or performed by the paired trial. Name hiding is a reading aid, not a blind experiment.

The runtime canonical pack hash is
`67aff67c40e47fb339d8ed304328b3e7647671a7cc6755496dd73ed4e4cc5c4e`.
The JSON file SHA256 is
`b1277a098bcf945d8a485bbb5ba7df9ca893aff095219c8875c509ee9382a63e`.
These hash different representations. Sources and all anchors participate in the canonical hash.

## Prompt experiments retained locally

| Variant | Profile-arm fallbacks, out of 8 | Whole-profile copies observed |
|---|---:|---:|
| Initial full instructions, no examples | 1 | 0 |
| Short profile instructions, no examples | 2 | 2 |
| Short instructions with cadence examples | 5 | 5 |
| Examples with an explicit new-reaction task | 3 | 3 |
| Final restored full instructions, no examples | 1 | 0 |

The example/new-reaction variant also copied example fragments in four valid answers: seven of
eight raw profile-arm outputs copied supplied material. Those responses ended normally, so this
was task following, not a token cutoff. Explicitly saying not to copy did not solve it. Runtime
profiles therefore contain descriptions only; the freshly authored examples remain in the source
review as human references. Earlier variants are retained as `comparison-initial`,
`comparison-short-prompt`, `comparison-cadence-first` and `comparison-cadence-reaction` under the
local output directory. Some source receipts were added between variants, so this small sequence
is diagnostic evidence, not a controlled prompt ranking or a general model benchmark.

The final baseline is the least-regressed observed integration, not a claim of good prose.
Elliott still imagines helping someone else while suggesting confrontation; Sam invents Jodi's
hidden uncertainty; George refers to current mood. Adding character context can also introduce
unsupported plans. Current validators reject malformed, overlong and unsupported typed choices;
they do not establish semantic agreement between the prose and the act.

## Recorded week and checks

Seed 7 covers seven days with returns, company, welcome, repair, sides and late enabled; feelings
and motives are on, quiet reflection chance 0.35 and waking dream chance 0.15. It contains 63
reflections: 62 local generator + Laya and one overlong-generation authored fallback (Emily).
Exact decision-tape replay matches every JSON field except the command label. The
[verification receipt](../../../out/character-voices-2026-10-12/verification.json) records hashes,
settings and checks. Observer checks confirmed that selected private history appears only after
the thought is considered and disappears when rewound before it.

The full simulation suite passed 694 cases before a final added provenance-mutation regression;
all 19 voice-trial cases then passed, bringing the suite to 695 cases. The mod suite passed 945
tests and the local mod build succeeded. Viewer behavior (13), generator contracts (22), corpus
parser (20) and observer-pack receipts (7) also passed. These are implementation checks; the live
paired trial still reports its one explicit failure.

## Next decision

Keep hybrid as the product direction. Use this same pack and scene suite to compare a more capable
local generator before adding more prompt prose or fine-tuning. Review whether a thought could
belong to another character, preserves the source roles, and implies the suggested action. Address
thought/action agreement as a separate contract. More concrete encounters and context-tagged
authored speech can progress alongside this model work; none requires a quota of kindness or
hostility. Starting histories are compact reviewed anchors, not every canonical Stardew memory.
