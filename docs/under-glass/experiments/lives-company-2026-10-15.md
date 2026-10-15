# Immediate company: first recorded trial

2026-10-15. Actual simulation output from the opt-in [company consumer](../specs/lives-company-spec.md),
with no model calls. This tests an immediate shared activity, not the full future-evening pilot.
The current routine baseline, sleep rules and bedtime weight are retained. No canon history pack
is loaded implicitly; the controlled histories below describe fictional Ann and Bo.

## Reproduce and inspect

From the repository, choose a fresh output directory:

```powershell
dotnet run -c Release --project sim/UnderGlass.LivesTrial -- --suite company --out out/company-review
```

Serve the repository and open `out/company-review/index.html`. The page links the ordinary Run
Viewer, raw recordings and synthetic history. It reads recorded choices and outcomes; it does not
run another choice calculation. The separate `livesCompany` and `livesRouting` experiment blocks
are not yet the full Lives observer contract or input to the existing chain viewer.

Start with **Town, seed 7**, filter **Completed**, then expand **Pam → Penny, Day 4, 08:50**.
The offer, Penny's next-minute answer, completed game and each participant's learning are visible.
Open “Memories that came to mind” to inspect the choice's recall and the receipts for its evidence.
Then inspect a **NotFound** episode: missing someone updates the search expectation, not whether
that person rejects the seeker. Finally choose **Synthetic household, seed 7** and open its
held-choice comparisons; the scene, alternatives and sampled draw are held fixed.

## Fixed matrix and results

All specified seeds were retained, including quiet runs and unchanged selections.

| Run group | Seeds and duration | Episodes | Completed |
|---|---|---:|---:|
| Normal town, no starting history | 7, 29, 101 × 7 days | 309 | 31 |
| Fictional household | 1–16 × 2 days | 17 | 8 |
| Fictional missed searches | 7, 29, 101 × 2 days | 6 | 0 |
| Sole warm opening removed in comparison | 7 × 2 days | 2 | 2 |
| Total | 23 recordings | 334 | 41 |

The normal town's final stages were 31 completed, 2 interrupted, 22 not-now answers, 9 refusals,
183 failed searches, 39 unavailable encounters and 23 aborted searches. These are distinct
outcomes, not a combined “failure” rate. Failed searches are frequent in this first consumer:
destinations are recent personal sightings or a known shared home, not an oracle for where
someone currently is. Candidate selection and search timing remain limited.

The **36 matched history comparisons all changed probabilities; none changed the selected
option with the held draw**. That establishes a numerical influence, not a demonstrated change
in behavior for these sampled decisions. One additional comparison removed the only warm
opening and changed eligibility; it is explicitly non-comparable, not counted as a matched delta.
These are immediate decision counterfactuals, not alternate full histories after the choice.

The fictional fixtures use equal yes/no weights with state-weighted not-now and large seeking
weights to expose the mechanism. They are not recommended town tuning. The normal town uses
ordinary company defaults and learns from its own run. The three normal-town recordings took
roughly 1–5 seconds each locally; concurrent checks and startup make this unsuitable as a speed
benchmark. There were no model requests, so these timings say nothing about hybrid throughput.

## Checks and limits

The simulation suite covers 1,043 tests. The full run passed 1,042 and found one stale glossary
source-reference check; repointing those references and rerunning all five glossary checks passed.
The 945 mod tests and mod build also passed. Final review found no outstanding blocker.
Targeted checks include off-switch compatibility, observer-recording neutrality, local knowledge,
legacy/new invitation pacing, refusal versus not-now learning, interrupted versus completed games,
and warm-recall eligibility recalculated separately for each history comparison.

Completed games currently give sensitivity-scaled catalog joy. There is no subjective bad game,
future appointment or Laya answer evaluation in this slice. Those limits matter more than the
number of completed games. The next pilot work is reviewed sourced history, future plans with
independent participant experiences, and the model/observer connections needed to follow that
complete thread.
