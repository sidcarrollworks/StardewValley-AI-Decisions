# Lives pilot baseline, 2026-10-15

The routine and prerequisite baseline for the [pilot plan](../specs/lives-pilot-spec.md).
These are real recordings of the existing engine, **not Lives runs**. No model was called,
history initialized, or illustrative contract fixture replayed as a simulation.

## Method

Source: development base `2711963` (merged #98), on `codex/lives-pilot`, with only the new
baseline capture script added under `sim` at capture time. Runtime C# and data unchanged.
Python invokes the Release Replay DLL sequentially after one successful build.

```powershell
python sim/tools/record_lives_baseline.py --out out/lives-baseline-2026-10-15
```

The tool defaults to seeds 1, 2 and 3, 35 days, `--town pelican`. It records two arms:

- **Routine:** the current town with Penny's sourced habitual bedtimes and hub weight.
- **Prerequisites:** the same town plus `--catalog company,invitations,errands --reflection authored`.

No injection, character-inspiration pack, dreams, speech or additional options. This is the
cost and behavior of enabling prerequisites **together**, not the cost of each switch and
not a causal history comparison. Existing defaults are captured in each recording/manifest.

## Results

| Seed | Routine seconds | Prerequisites seconds | Routine JSON MiB | Prerequisites JSON MiB | Authored reflections |
|---|---:|---:|---:|---:|---:|
| 1 | 2.43 | 4.28 | 2.98 | 6.60 | 277 |
| 2 | 2.49 | 4.00 | 3.33 | 6.78 | 251 |
| 3 | 2.12 | 4.32 | 3.22 | 6.83 | 268 |

Elapsed values are single process wall-clock samples: startup, simulation, serialization
and file output, excluding the build. They do not measure CPU time, allocation, peak memory
or hybrid model latency. They are a baseline receipt, not a performance guarantee.

Penny's 34 completed wakings after the initial partial sleep were all before 06:00 in every
arm and seed; one final sleep was incomplete in each. Median waking was between 03:06:30
and 03:10:30 with prerequisites and around 03:07–03:08:30 without them. Her first choice
had only home available on all 34 routine wakings; prerequisites had 34, 33 and 33 such
choices. The two exceptions were deliveries, not new solitary activities. This reproduces
the early-waking gap; it is not evidence that the new history mechanism works.

## Verification

All three verification runs passed comparison of every top-level field:

- Routine seed 1, nights recorder off: identical apart from `label` and omitted `nights`.
- Prerequisites seed 1, nights recorder off: the same comparison passed.
- Prerequisites seed 1, authored tape: identical apart from `label`, including all 277
  reflection records, acts, feelings, movement, night rows and settings.

The log hashes also match: routine seed 1 `3d571cba24527933`; prerequisites seed 1
`f87cca0bb07b5f27`. Equality of the other recorded fields is the stronger check.
The 0.2 structural checker accepts the prerequisites seed-1 recording's 116 base rows
and correctly reports **no Lives data**; this is not a pass for an unbuilt Lives writer.

The harness has 12 offline tests for plan separation, input validation, source/output
preservation, exact field comparison, non-vacuous tape verification, night-summary boundaries,
plan-only operation and failed-build receipts. Existing simulator tests: **828 passed**;
mod library tests: **945 passed**; the mod build passed without deployment.

## Evidence and next use

The [compact evidence receipt](lives-baseline-2026-10-15.json) retains run summaries, arguments,
checksums, compared fields, source commit and a digest of the capture's simulation-source
hash map. The ignored local pack `out/lives-baseline-2026-10-15/` has every JSON recording,
process log, full source hash map, settings and generated summary. Use a new output directory
when rerunning; the tool refuses to replace any existing pack, including a failed one.

Keep these as the pre-Lives control. After implementation, first compare Lives off against
this same engine/configuration; then measure Lives added to the **prerequisites** arm.
A different base commit needs a separately recorded baseline. Compare a history's effect
at a frozen decision input, not between these whole-run arms.
