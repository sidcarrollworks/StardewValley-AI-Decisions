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


# Character-spread eval, 2026-10-02 (revised after review)

`python eval/run_spread.py` — one fixed reference question per type, asked once per villager's
card (34 cards, built by `tools/CardExporter`). Variant A is the card the mod sends today;
variant B adds the viewer's "leanings:" line. Two mid-game runs (hearts 4 and 6; hearts 4 is the
calibration reference) and one newcomer run (hearts 0, the welcome question).

**The questions are the mod's real ones** (review fix): the ladder's
"should <npc> try to get the player's attention with {Emote|Bubble|Approach} now?" with its
`urge=…; hearts=…; step=…; player last seen: …` context line (InitiationLadder.cs:195-204); the
planner's "does <npc> have news for the player?" with a news section (IntentPlanner.cs:108); the
motives' close-call wording with **the NPC's own effective boldness** (seed boldness + the run's
hearts, fixed intensity) and a fixed cost; the choice state describes the reason only, never the
act. Spread = 90th minus 10th percentile; "follows" = Spearman vs the trait in the expected
direction (forgiveness inverted for hold-against) >= 0.3. Flat = spread < 0.05.

## typed-decisions (hearts 4; the mod's checkpoint)

| Question | trait | variant | spread | median | spearman | follows? |
|---|---|---|---|---|---|---|
| attention_emote | boldness | A | 0.233 | 0.398 | +0.26 | no |
| attention_emote | boldness | B | 0.162 | 0.432 | +0.34 | **yes** |
| attention_bubble | boldness | A | 0.247 | 0.386 | +0.37 | **yes** |
| attention_bubble | boldness | B | 0.195 | 0.455 | +0.25 | no |
| attention_approach | boldness | A | 0.074 | 0.572 | +0.06 | no |
| attention_approach | boldness | B | 0.049 | 0.561 | +0.24 | no (and B is FLAT) |
| speak | chattiness | A | 0.034 | 0.478 | -0.39 | **FLAT**, no |
| speak | chattiness | B | 0.035 | 0.468 | -0.10 | **FLAT**, no |
| hold_against | forgiveness (inv.) | A | 0.135 | 0.424 | -0.40 | **yes** |
| hold_against | forgiveness (inv.) | B | 0.146 | 0.430 | -0.35 | **yes** |
| close_friendly | boldness | A | 0.092 | 0.614 | +0.27 | no |
| close_friendly | boldness | B | 0.123 | 0.586 | +0.10 | no |
| close_hostile | boldness | A | 0.098 | 0.658 | +0.20 | no |
| close_hostile | boldness | B | 0.128 | 0.639 | +0.37 | **yes** |
| choose (P walk over) | boldness | A | 0.085 | 0.516 | +0.25 | no |
| choose (P walk over) | boldness | B | 0.142 | 0.535 | +0.01 | no |
| welcome (newcomer) | boldness | A | 0.289 | 0.396 | +0.54 | **yes** |
| welcome (newcomer) | boldness | B | 0.264 | 0.401 | +0.51 | **yes** |

Median latency 30-40 ms per call on the GPU. Hearts 6 moves nothing by more than 0.02.

## english (hearts 4)

| Question | trait | variant | spread | median | spearman | follows? |
|---|---|---|---|---|---|---|
| attention_emote | boldness | A | 0.831 | 0.525 | +0.41 | **yes** |
| attention_emote | boldness | B | 0.743 | 0.391 | +0.52 | **yes** |
| attention_bubble | boldness | A | 0.638 | 0.367 | +0.49 | **yes** |
| attention_bubble | boldness | B | 0.646 | 0.556 | +0.48 | **yes** |
| attention_approach | boldness | A | 0.157 | 0.864 | -0.00 | no |
| attention_approach | boldness | B | 0.226 | 0.785 | +0.02 | no |
| speak | chattiness | A | 0.239 | 0.542 | -0.36 | no |
| speak | chattiness | B | 0.244 | 0.421 | -0.18 | no |
| hold_against | forgiveness (inv.) | A | 0.141 | 0.232 | -0.23 | no |
| hold_against | forgiveness (inv.) | B | 0.123 | 0.205 | -0.20 | no |
| close_friendly | boldness | A | 0.066 | 0.894 | +0.41 | **yes** |
| close_friendly | boldness | B | 0.145 | 0.870 | +0.47 | **yes** |
| close_hostile | boldness | A | 0.083 | 0.944 | +0.36 | **yes** |
| close_hostile | boldness | B | 0.160 | 0.891 | +0.57 | **yes** |
| choose (P walk over) | boldness | A | 0.135 | 0.257 | +0.42 | **yes** |
| choose (P walk over) | boldness | B | 0.189 | 0.326 | +0.54 | **yes** |
| welcome (newcomer) | boldness | A | 0.554 | 0.424 | +0.38 | **yes** |
| welcome (newcomer) | boldness | B | 0.604 | 0.326 | +0.43 | **yes** |

## What this says (the revised run)

- **The old run's conclusions were artifacts** (review, confirmed). With the real question
  wordings and each NPC's own effective boldness, the picture is completely different: hold_against
  follows forgiveness on typed-decisions, the choice question left its ceiling (median 0.52 vs
  0.69 before — the state used to state the act), and english's close-call and choice questions
  follow boldness strongly.
- **Which questions are flat (typed-decisions).** Only **speak** — 0.034 on both variants, and
  its correlation with chattiness is *negative* (-0.39 A). With a real news section, the model
  answers the speak gate at ~0.47 for everyone regardless of chattiness. Approach is borderline
  (0.074 A / 0.049 B, the latter flat) and doesn't follow boldness on either checkpoint.
- **Does card B widen the spread?** Mixed, and it moves levels: emote narrows (0.233 -> 0.162)
  but rises to follow (+0.26 -> +0.34); bubble widens spread but loses its ordering (+0.37 ->
  +0.25); close_hostile gains follows (+0.20 -> +0.37); close_friendly and choose lose ground.
  No consistent win. On english, B is neutral-to-positive.
- **Does the choice variant separate characters better?** On english, yes — it follows boldness
  strongly (+0.42/+0.54) and its spread beats most yes/no questions; on typed-decisions, no
  (+0.25/+0.01). The checkpoint matters more than the question form.
- **The one solid finding: speak is flat and anti-correlated with chattiness on BOTH
  checkpoints.** The question as asked cannot carry personality. Don't fix it with the model:
  keep the news ranking deterministic (D21, speak stays a 0.25 veto floor), and wire chattiness
  into the news weights in code ([temperament.md](temperament.md) plans exactly that) rather
  than a temperament prior over the speak answer.
- **Corrections stay off.** No question is simultaneously flat and trait-following in a way a
  relative rescale would fix on typed-decisions (speak is flat but not following; approach is
  flat-ish but not following). The prior correction is a code-level temperament wiring, not a
  number in the calibration file. `RelativeScale` and `w` remain absent; the panel's job is to
  watch these numbers in real play. The leanings-line recommendation (card B) is weaker than the
  first run suggested — it helps emote and close_hostile and hurts bubble and close_friendly —
  so try it as its own change only if the panel agrees in real play.
