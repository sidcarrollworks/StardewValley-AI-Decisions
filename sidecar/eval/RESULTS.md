# Eval results (run 2026-09-30 against laya-serve on Sid's GPU)

`python run_eval.py` — 6 directional pairs per checkpoint. Each case is a realistic state
(the NPC card plus caller sections); the script posts one batched request per case.

| Checkpoint | Agreement | Median | p95 | Notes |
|---|---|---|---|---|
| `typed-decisions` | **5/6** | 47 ms | 51 ms | the mod's default; keeps it |
| `english` | 3/6 | 43 ms | 62 ms | first call 2,149 ms (lazy checkpoint load) |

Per-case (P = direction as expected):

| Case | typed-decisions | english |
|---|---|---|
| speak: gift news vs housemate news | FAIL (0.33 vs 0.39) | FAIL (0.16 vs 0.18) |
| speak: quest news vs housemate news | PASS (0.34 vs 0.27) | FAIL (0.21 vs 0.22) |
| bubble: shy Penny vs outgoing Sam | PASS (0.14 vs 0.21) | PASS (0.00 vs 0.04) |
| emote: shy Penny vs outgoing Sam | PASS (0.35 vs 0.41) | PASS (0.82 vs 0.83) |
| approach: missing 3 days vs seen 1 hour ago | PASS (0.47 vs 0.19) | FAIL (0.22 vs 0.27) |
| choose: gift vs housemate line | PASS (0.85 vs 0.19) | PASS (0.96 vs 0.09) |

## What this says

- **`typed-decisions` stays the default**: clearly better directional agreement (5/6 vs 3/6).
- **The speak question is the weak spot on both checkpoints.** "Does X have something worth
  telling the player today?" answers cluster at 0.15-0.4 even for a loved gift, which is below the
  0.5 speak threshold — this matches the observed in-game behavior (many model calls, empty plans,
  PR #3). The choice question is the strong primitive (0.85-0.96 when the direction matters), and
  approach/gossip-style questions work on typed-decisions. Before tuning the threshold, try
  rewordings of the speak question on this same set.
- **Latency on the GPU is ~45 ms/call** (two questions batched stay in the same range), so the
  20 s planning budget holds ~400 batched calls — far above a night's plan.
- **Warm-up is real**: the first `english` call took 2.1 s while the checkpoint lazy-loaded.
  A throwaway call after the health check is worth it.

# Re-run 2026-10-01: the phrasing the game now actually sends (news-ranking redesign)

After the in-game week review, the planner sends plain sentences (`NewsPhrasing`: "yesterday the
player gave Haley a Sunflower (a loved gift)") and the speak question is the reworded
"Does X have news for the player?". The eval cases were updated to exactly that phrasing (and the
dull control became a player `Saw`, the everyday baseline that really reaches the model):

| Checkpoint | Agreement | Median | p95 |
|---|---|---|---|
| `typed-decisions` | 5/6 | 41 ms | 45 ms |
| `english` | 4/6 | 33 ms | 47 ms |

Per-case:

| Case | typed-decisions | english |
|---|---|---|
| speak: gift news vs player-saw news | PASS (0.51 vs 0.47) | FAIL (0.31 vs 0.41) |
| speak: quest news vs player-saw news | FAIL (0.49 vs 0.52) | PASS (0.82 vs 0.79) |
| bubble: shy Penny vs outgoing Sam | PASS (0.14 vs 0.21) | PASS (0.00 vs 0.04) |
| emote: shy Penny vs outgoing Sam | PASS (0.35 vs 0.41) | PASS (0.82 vs 0.83) |
| approach: missing 3 days vs seen 1 hour ago | PASS (0.47 vs 0.19) | FAIL (0.22 vs 0.27) |
| choose: gift vs saw line | PASS (0.79 vs 0.11) | PASS (0.93 vs 0.04) |

## What this says

- **The speak answer is a compressed band on every wording** (see speak_experiments.md): newsy
  states measure 0.15-0.51 on typed-decisions, dull states 0.18-0.51 — the question barely
  separates. That is why the week review's finding 4 matters: the answer must not decide the
  speakers. The redesign therefore ranks by news score and demotes the answer to a veto floor
  (`SpeakThreshold` 0.25; a quest state measured 0.49 under the chosen wording and would have
  been gated at 0.5).
- **The choice question remains the strong primitive** on both checkpoints (0.79-0.93), which is
  why the pick blend uses the model probabilities as a modulation, not the decision.
- **`typed-decisions` stays the default** (5/6 vs 4/6, and it rates the gift and the player-saw
  correctly, which english does not).

# Character-spread eval, 2026-10-02

`python eval/run_spread.py` — one fixed question per type, asked once per villager's card (34
cards, built by `tools/CardExporter`). Variant A is the card the mod sends today; variant B adds
the viewer's "leanings:" line (the seed temperament's strongest traits in plain words). Two
mid-game runs (hearts 4 and 6; the model barely moves between them, so hearts 4 is the
calibration reference) and one newcomer run (hearts 0, the newcomer-week welcome question).
Spread = 90th minus 10th percentile; "follows" = Spearman rank correlation with the trait the
question should depend on, in the expected direction, >= 0.3 (inverted for hold_against).
Flat = spread < 0.05.

## typed-decisions (hearts 4)

| Question | trait | variant | spread | median | min | max | spearman | follows? |
|---|---|---|---|---|---|---|---|---|
| attention_emote | boldness | A | 0.238 | 0.323 | 0.230 | 0.551 | +0.23 | no |
| attention_emote | boldness | B | 0.258 | 0.452 | 0.247 | 0.681 | -0.24 | no |
| attention_bubble | boldness | A | 0.106 | 0.220 | 0.150 | 0.346 | +0.24 | no |
| attention_bubble | boldness | B | 0.114 | 0.249 | 0.129 | 0.348 | +0.51 | **yes** |
| approach | boldness | A | 0.099 | 0.084 | 0.026 | 0.157 | -0.19 | no |
| approach | boldness | B | 0.068 | 0.115 | 0.055 | 0.153 | -0.27 | no |
| speak | chattiness | A | 0.167 | 0.366 | 0.243 | 0.497 | +0.32 | **yes** |
| speak | chattiness | B | 0.173 | 0.350 | 0.175 | 0.484 | +0.34 | **yes** |
| hold_against | forgiveness (inv.) | A | 0.106 | 0.426 | 0.331 | 0.501 | -0.23 | no |
| hold_against | forgiveness (inv.) | B | 0.111 | 0.429 | 0.334 | 0.535 | -0.12 | no |
| close_friendly | boldness | A | 0.070 | 0.597 | 0.521 | 0.645 | +0.03 | no |
| close_friendly | boldness | B | 0.248 | 0.530 | 0.258 | 0.625 | -0.41 | no (wrong way) |
| close_hostile | boldness | A | 0.089 | 0.625 | 0.499 | 0.687 | +0.23 | no |
| close_hostile | boldness | B | 0.118 | 0.598 | 0.493 | 0.674 | +0.07 | no |
| choose (P walk over) | boldness | A | 0.096 | 0.692 | 0.630 | 0.788 | -0.12 | no |
| choose (P walk over) | boldness | B | 0.109 | 0.695 | 0.600 | 0.771 | -0.18 | no |
| welcome (newcomer) | boldness | A | 0.300 | 0.394 | 0.134 | 0.523 | +0.48 | **yes** |
| welcome (newcomer) | boldness | B | 0.259 | 0.397 | 0.183 | 0.545 | +0.55 | **yes** |

Median latency 30-42 ms per call on the GPU.

## english (hearts 4)

| Question | trait | variant | spread | median | min | max | spearman | follows? |
|---|---|---|---|---|---|---|---|---|
| attention_emote | boldness | A | 0.490 | 0.521 | 0.144 | 0.890 | +0.18 | no |
| attention_emote | boldness | B | 0.304 | 0.738 | 0.254 | 0.921 | -0.26 | no |
| attention_bubble | boldness | A | 0.079 | 0.066 | 0.012 | 0.201 | +0.31 | **yes** |
| attention_bubble | boldness | B | 0.093 | 0.063 | 0.001 | 0.244 | +0.47 | **yes** |
| approach | boldness | A | 0.001 | 0.000 | 0.000 | 0.037 | -0.13 | **FLAT**, no |
| approach | boldness | B | 0.003 | 0.000 | 0.000 | 0.030 | +0.05 | **FLAT**, no |
| speak | chattiness | A | 0.107 | 0.120 | 0.064 | 0.260 | +0.03 | no |
| speak | chattiness | B | 0.076 | 0.106 | 0.035 | 0.216 | +0.02 | no |
| hold_against | forgiveness (inv.) | A | 0.153 | 0.252 | 0.105 | 0.383 | -0.06 | no |
| hold_against | forgiveness (inv.) | B | 0.109 | 0.205 | 0.104 | 0.359 | +0.11 | no |
| close_friendly | boldness | A | 0.066 | 0.890 | 0.490 | 0.936 | +0.28 | no |
| close_friendly | boldness | B | 0.118 | 0.859 | 0.672 | 0.929 | +0.26 | no |
| close_hostile | boldness | A | 0.099 | 0.905 | 0.756 | 0.955 | +0.24 | no |
| close_hostile | boldness | B | 0.135 | 0.865 | 0.665 | 0.918 | +0.44 | **yes** |
| choose (P walk over) | boldness | A | 0.073 | 0.925 | 0.875 | 0.972 | -0.18 | no |
| choose (P walk over) | boldness | B | 0.069 | 0.934 | 0.868 | 0.968 | -0.13 | no |
| welcome (newcomer) | boldness | A | 0.596 | 0.471 | 0.007 | 0.773 | +0.38 | **yes** |
| welcome (newcomer) | boldness | B | 0.623 | 0.271 | 0.031 | 0.836 | +0.47 | **yes** |

## What this says

**Which questions are flat.** Almost none on typed-decisions (the closest are close_friendly A at
0.070 and approach B at 0.068, both above the 0.05 line). English is flatter: approach is exactly
flat (spread 0.001-0.003, median 0.000 — the model says no for everyone) and speak is compressed
(median 0.106-0.120). On typed-decisions the problem is not flatness, it is direction.

**Does card B widen the spread?** Usually, but not reliably, and it moves the level at least as
much as the spread. typed-decisions: emote 0.238 -> 0.258 (and median 0.323 -> 0.452 — a big level
shift), close_friendly 0.070 -> 0.248, hostile 0.089 -> 0.118, choose 0.096 -> 0.109, but approach
narrows 0.099 -> 0.068. The one ordering fix is bubble: +0.24 -> +0.51. The one ordering casualty
is close_friendly, which goes wrong-way -0.41. So the leanings line is a real improvement for the
attention questions but not a general fix.

**Does the choice variant separate characters better?** No. P(walk over) sits at a ceiling on
both checkpoints (typed-decisions median 0.69-0.70, min 0.60; english median 0.93) and its
correlation with boldness is negative in both variants. "Walk over" is not a close call for this
model — every villager prefers it. The widest spreads belong to yes/no questions (close_friendly B
on typed-decisions; emote A on english).

**Conclusion.** The correction for the questions that fail is the text one, not the numeric ones:
the traits must reach the card in plain words (variant B), and the card must keep its budget
priority. The numeric corrections stay off (`RelativeScale` and `w` absent from
`data/laya-calibration.json`): the failing questions are not flat, so a relative rescale cannot
fix their ordering, and a temperament prior would fight the questions that already follow their
trait (speak, welcome). Recommend adding the leanings line to the real card as a separate change
(it shifts answer levels, so re-run this eval and re-calibrate after), and re-running this eval
whenever the card or a question's wording changes. typed-decisions remains the default; its
numbers are committed in `data/laya-calibration.json`.
