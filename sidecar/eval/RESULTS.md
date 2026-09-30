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
