# Evening pilot: actual histories, plans and Laya

2026-10-15. Actual simulation output, not the illustrative observer fixture. Implementation:
[evening spec](../specs/lives-evenings-spec.md). History provenance:
[source review](pilot-history-review-2026-10-15.md).

## Start reviewing

The local pack is `out/lives-evenings-review-2026-10-15/index.html`, served at
`http://127.0.0.1:8767/out/lives-evenings-review-2026-10-15/index.html`.
The generated files are ignored by Git; the trial command below reproduces the authored runs.

1. Start with **Authored, seed 7**. Open Pam's Day 1, 07:15 offer to Penny. Follow the answer,
   the two separate 17:00 choices, the 18:00 game and its 18:29 completion. Expand recall and
   alternative odds only where needed. Agreement and actual shared time are separate events.
2. Switch to **Laya, seed 7**. Its first offer agrees but the evening does not happen. Read each
   participant's later choice and observed outcome. The two runs diverge after model choices;
   this is not a matched counterfactual of every later scene.
3. Open **The same choice, with one history removed**. These are matched authored calculations:
   scene, remaining memory, options' inputs and draw stay fixed. Probability movement is distinct
   from a changed selected action. Neither arm re-asks Laya or simulates an alternate future.
4. Select **Gus**, especially authored seed 101, under **Noticed and learned**. His observations
   concern expected Saloon presence. He never receives Penny's private plan or its outcome.

## Fixed matrix and results

35 days in the ordinary town for seeds 7, 29 and 101; one additional live Laya run at seed 7.
Only Penny, Pam and Gus use the evening consumer. Current body/sleep rules, bedtime weight 9,
and ordinary invitations are retained, with no extra bedtime penalty. No success quota, forced
meeting, private generated inspiration or Haiku calls. All fixed seeds are retained.

| Mode / seed | Offers | Completed | Final other endings | Matched comparisons | Changed odds | Changed selection |
| --- | ---: | ---: | --- | ---: | ---: | ---: |
| Authored / 7 | 1 | 1 | — | 8 | 5 | 0 |
| Authored / 29 | 1 | 0 | 1 not now | 12 | 8 | 0 |
| Authored / 101 | 1 | 0 | 1 missed evening | 12 | 6 | 0 |
| Laya / 7 | 6 | 0 | 4 not now, 2 missed evenings | 17 | 10 | 0 |

There are 49 matched comparisons, 29 changes in authored probabilities and no sampled selection
flips. These show that admitted histories enter the choice calculation, not that history has
already produced a visibly different life in every run. Different models and a richer menu of
actions remain open experiments. More offers alone do not establish a better model.

Gus records 70 sightings in authored seed 7, 68 sightings and 2 fully observed misses in seed 29,
and 67 sightings and 3 misses in seed 101. In seed 101 two intentions to help expire without an
eligible encounter. Laya seed 7 records 70 sightings. No Gus help is completed in this matrix;
tests exercise that path, but these recordings demonstrate its opportunity limitation.

## Reproduction and verification

```powershell
dotnet run --project sim/UnderGlass.LivesTrial -- --suite evenings --out out/lives-evenings-review-2026-10-15 --laya-url http://127.0.0.1:8000
```

Omit `--laya-url` for the authored matrix. Live model outputs are not assumed repeatable: the
exported tape records the actual requests and answers. The trial replays the complete Laya
recording before any observer projection and requires exact equality. It then exports the full
ordinary town plus the pilot's Lives receipts. Additional seed-7 care/whereabouts comparison files
contain receipts and the identical ordinary-world hash, rather than duplicated town recordings.

The live run made 16 Laya requests, with no fallback evaluations. Its full recording replayed
exactly. The running local service reported CUDA and model revision
`7b928d828b7b0e022f929d9bd2e44165aa270148`; each tape entry retains configured and reported
model identity. Serialized requests are 806–1,065 characters, with no recalled records omitted. The source pack SHA-256 is
`cc6253fe19691b33021c34cf9922194cdd7481156e9f70e1e0958c4c1adbd57c`.

Validation: the full sim suite passed 1,093 tests, followed by the added minimum-age regression
test passing separately (1,094 total). The 945 mod tests and mod build passed. The sim solution
built without warnings. Generated review JavaScript parses; browser checks cover loading the
recordings, person filtering and expanding plan threads. The run times in `summary.json` were
collected alongside other checks and are not a performance benchmark.

## What remains

This is an opt-in evening extension, not completion of the entire Lives assessment. Runtime
receipts still need integration with the common chain viewer/observer contract. Follow-through
currently compares keeping a plan with carrying on, rather than exposing each alternate activity.
The completed game's affect is still generic; a disappointing shared game needs richer activity
content. The commitment mechanism, learned routines and generated possibilities are not generalized
by this change. Long-run retention and population scaling still need dedicated measurement.

Review the existing threads and bounded model packets before tuning. The next implementation
should make these actual runtime chains visible in the main viewer, then test richer activities
and transfer to another household without named-person branches.
