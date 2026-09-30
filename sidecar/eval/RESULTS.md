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
