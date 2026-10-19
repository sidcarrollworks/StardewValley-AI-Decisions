# First all-choice evening check — 2026-10-19

Purpose: verify that queued evening choices can reach Laya regardless of the fallback's torn
status or the two-evaluations/person/day budget. This is a coverage and replay check, not a
comparison of character quality or a whole-town performance benchmark.

## Method

Existing installed local Laya, `typed-decisions`, loopback port 8000. The launcher loaded cached
weights with offline mode; no install or download. It was stopped after this check. We did not
pin or re-download a model revision for this smoke; exact raw answers and requests are saved.
The default town ran seed 7 for seven days with routing, company, evenings, the company/invitations
catalog and the reviewed `sim/history/penny-pam-gus.json` pack. No participant restriction: the
whole existing cast could acquire experience and generate evening choices. No Haiku, generated
inspiration or reflection was enabled, to isolate evening evaluation coverage.

Common arguments:

```text
--days 7 --seed 7 --lives-routing --lives-company --lives-evenings
--catalog company,invitations --history sim/history/penny-pam-gus.json
--lives-laya http://127.0.0.1:8000 --lives-laya-model typed-decisions
```

Add `--evening-evaluation selective` or `--evening-evaluation all`. Outputs in the feature checkout:
`out/laya-first-2026-10-19/{selective,all,replayed}.json` (local artifacts, not committed).
Replay used the same all-choice settings and `--lives-tape all.json` instead of the service URL.

## Observations

| Recorded measure | Selective | All |
|---|---:|---:|
| Evening choices | 151 | 185 |
| Evaluations / recorded HTTP attempts | 29 | 185 |
| Successful Laya answers | 29 | 185 |
| Evaluations on untorn choices | 0 | 135 |
| Most evaluations for one person in one day | 2 | 4 |
| Fallbacks | 0 | 0 |
| Packets omitting recalled text | 0 | 0 |
| Replay CLI reported generation duration | 5.5 s | 7.9 s |

The all run evaluated 94 offers, 61 answers and 30 follow-through decisions. All 185 recorded
evening choices had an evaluation. Both runs saw the same initial conditions, but they diverged
after different choices: their denominators are not paired decision samples. The durations are
single Debug CLI measurements after service readiness, exclude cold startup and are not model
latencies; do not extrapolate them to full Laya control of a town.

Replaying all 185 answers consumed the tape without network access. Every parsed recording field
matched exactly except the top-level `label`, which intentionally contains the live versus tape
CLI arguments. The first raw-file equality check caught this label difference; investigation
confirmed no simulation or receipt difference. Offline tests additionally compare exact traces,
decision receipts and outcomes under scripted weights, observer-off and delayed async responses.

CLI checks verified the Run Studio schema includes the control and rejects unknown values or
all-choice mode without a model/tape. `dotnet test sim/UnderGlass.sln`: 1111 passed (20 evening
tests); `dotnet test NpcSchedules.sln`: 945 passed; Replay and mod builds passed.

## Limits and next step

All still means all **queued evening choices**. Warmth/wish opportunity openings, existing
household scope and per-pair/day offer gates remain. Routes, immediate company and ordinary acts
remain authored. The 1,250-character packet cap and prompt composition are unchanged. No memory
omissions happened in this sample; that does not prove richer histories fit or are interpreted
well. The next stage is personal context and free-time routes, alongside Claude's independent
coverage report in [draft PR #108](https://github.com/sidcarrollworks/Under-Glass/pull/108).
