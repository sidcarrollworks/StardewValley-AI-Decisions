# Generator option-order probe — 2026-10-08

**Initial finding:** Qwen's proposal is sensitive to the order of the offered actions. Reversing
the choices changed the selected action in three of four fixed scenes. The fourth
kept the confrontation ID while its thought proposed helping. These results do not
support treating this generator's suggestions as dependable character judgment.

## Setup

The probe ran at 2026-10-09 05:45 UTC (October 8 locally) against the existing local
CUDA service: `Qwen/Qwen3-0.6B`, revision
`c1899de289a04d12100db370d81485cdf75e47ca`, thinking disabled, temperature 0.7,
top-p 0.8, top-k 20, at most 180 output tokens. Each request kept its recorded seed.
No model, engine, generation instructions, or sampling settings were changed.

Four actual requests came from `out/reflection-morning-hybrid.json`. Each ran in
this sequence: original packet, reversed executable choices, exact original repeat,
then original order with choice IDs consistently renamed to `a1`, `b2`, `c3`.
The baseline and repeat used the exact recorded `GenerationPrompt` string. Variant
construction preserved every character outside the choices array in the user
message, including its original JSON escape spellings. Reordering preserved the
complete option objects; renaming changed only their IDs. The supplied memory,
context, earlier idea, opportunity, candidate kinds/lines, and system instruction
remained fixed. No retries, output repair, simulation advancement, or Laya calls
were part of this probe.

## Results

| Scene and request ID | Remembered encounter | Baseline | Reversed | Renamed |
|---|---|---|---|---|
| Own kindness — `7:2487:Evelyn:6` | Evelyn helped George | Confront | Help | Confront |
| Received kindness — `7:1341:Leah:4` | Leah remembers Jodi giving a gift | Confront | Help | Confront |
| Received hostility — `7:9053:Jodi:29` | Jodi remembers Leah arguing | Confront | Help | Confront |
| Dream — `7:3265:Jodi:4` | Jodi gave a gift to Leah before sleeping | Confront | Confront ID, helping thought | Confront |

Choices were compared by **kind plus complete line**, not by their temporary ID.
All four baselines chose the first executable option. Three of four reversed
requests chose the new first option. All four renamed requests also chose the first
option. Including exact repeats, that is 15 of 16 responses; these are related
observations from four scenes, not 16 independent trials.

All four exact repeats returned identical raw content, and all four baselines
matched their original morning-run content. Renaming preserved the selected meaning
in all four scenes, although the received-hostility wording changed. This isolates
an order effect in the tested scenes; it does not establish label invariance in
other scenes or runtime reproducibility in general.

All 16 HTTP responses passed the existing structural proposal checks, with no
transport or JSON failures. End-to-end request latency had a median of **835 ms**,
range **663–1,128 ms**. Raw responses, outer metadata, exact prompts, seeds, semantic
maps, timing, and source requests are retained in the
[complete probe audit](reflection-generator-order-2026-10-08.json), with a local copy at
`out/reflection-generator-order-probe.json`. The collection script remains local at
`out/run-generator-order-probe.py`.

## What the prose shows

The reversed dream returned: “I could help Leah by organizing a small activity she
might enjoy, something that would make us both feel connected.” Its `suggestedChoice`
still identified the boundary-setting confrontation line. The current validator
checks the allowed ID and JSON shape, not semantic agreement between thought and act.

The reversed received-hostility response copied its candidate dialogue exactly:
“I'm still thinking about what happened. Could I give you a hand?” The own-kindness
baseline explicitly said it would “choose to argue instead of giving a gift,”
despite the instruction to produce private, tentative self-talk. No clear reversal
of the supplied own/received source roles appeared in these four scenes; that small
negative result does not resolve the perspective errors seen in the broader run.

The tested generator can produce valid short JSON quickly, but its apparent motive
often follows the presentation of the action menu. Balanced Laya evaluation occurs
after generation and cannot remove this bias from the imagined thought. This probe
introduces no correction, preferred action frequency, or acceptance target.

## Bounded follow-up: act types without candidate dialogue

The generator packet now supplies only the allowed `id` and `kind`, alongside copied
`knownSource` fields (`kind`, `ownDeed`, `valence`, `regard`). Its instruction explains
that `ownDeed=true` means the actor performed the remembered act; false means the
subject acted toward them. Laya still receives every complete authored line. This
separates imagining an intention from evaluating its eventual spoken response and
removes the direct channel for copying candidate dialogue.

Eight further calls used the same four recorded scenes and seeds, each in original
and reversed order. Packets were captured from the rebuilt C# adapter through a fake
HTTP handler, then posted to the same real generator. No prompt was reconstructed
independently in Python. Other scene fields and sampling settings remained fixed;
there were no retries, quotas, or added rotations.

| Measure, matched baseline/reverse calls only | With candidate lines | With act types and known source |
|---|---:|---:|
| Structurally valid responses | 8/8 | 8/8 |
| Exact copy of a candidate line | 1/8 | 0/8 |
| First executable option selected | 7/8 | 3/8 |
| Scenes whose selected meaning changes on reversal | 3/4 | 2/4 |

This is **not a demonstrated improvement in coherent inspiration**. Five of the eight
new thoughts described helping while the returned ID meant giving a gift or
confronting. In the received-kindness baseline, the model wrote “giving a gift to
Jodi, which is what I remember from the past,” although the supplied memory says
Jodi gave a gift to the actor. The reversed dream proposed helping Leah “before she
wakes up”; Leah's sleep state was not supplied. Other output narrated numeric-context
concepts such as “my current mood and regard,” despite the private-voice instruction.
Structured source facts therefore do not by themselves establish correct perspective.

All eight calls succeeded, with median end-to-end latency **1,559 ms**, range
**1,113–2,078 ms**. A separate replay briefly overlapped this measurement and received
the service's expected busy rejections; timing was not isolated against other local
work and should not be read as a controlled performance regression. This probe's
eight successful receipts are in the [follow-up audit](reflection-generator-kinds-2026-10-08.json),
also retained locally at `out/reflection-generator-kinds-probe.json`. Its collection
script remains local at `out/run-generator-kinds-probe.py`.

Two new role-direction tests verify exact source copying, absence of candidate
speech from generation messages, and preservation of full lines in Laya's packet;
all 71 focused adapter, receipt and evaluation tests pass. The cleaner input boundary
is implemented, but the 0.6B generator remains experimental. Neither valid JSON nor
less first-position selection establishes sound memory use or thought/action agreement.
